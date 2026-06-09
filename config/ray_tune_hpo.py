import os
import sys
import time
import uuid
import tempfile
import subprocess
from pathlib import Path
from typing import List, Optional, Tuple
from queue import Queue, Empty 
import threading
import socket
import random
import re
import shutil
from TBXLoggerMidHParams import TBXLoggerMidHParams
from TrialFailureCallback import TrialFailureCallback

import yaml
from tensorboard.backend.event_processing import event_multiplexer as emux
import gc
from tensorboard.backend.event_processing import event_accumulator as ea

import unity_helper
import textUtils
import workload
import torch
import shutil

os.makedirs(r"C:\Temp", exist_ok=True)
os.environ["TEMP"] = r"C:\Temp"
os.environ["TMP"]  = r"C:\Temp"
os.environ["TMPDIR"] = r"C:\Temp"

import logging
import logger_config

logger_config.setup_logging("ray_hpo_debug.log")
logging.info("HPO Script Initialized")

import ray_windows_fixes
ray_windows_fixes.apply()

from ray.air import FailureConfig
import ray
from ray import tune
from ray.air import session
from ray.tune.search.optuna import OptunaSearch
from ray.tune.schedulers import ASHAScheduler

os.environ["TUNE_WARN_EXCESSIVE_EXPERIMENT_CHECKPOINT_SYNC_THRESHOLD_S"] = "1"

_LINE_RE = re.compile(
    r"\[INFO\]\s+([^.]+)\.\s*Step:\s*(\d+)\.(?:.*?Mean Reward:\s*([+-]?\d+(?:\.\d+)?))?",
    re.IGNORECASE,
)



def run_hpo(
    hpo_config_file: str,
    environment_config_file: str,
    session_dir: str,
    num_ray_envs: int,
    ml_args:  Optional[List[str]] = None,
):
    cfg = __read_yaml(hpo_config_file)
    h = cfg.get("hpo")
    if not isinstance(h, dict):
        raise ValueError(f"{hpo_config_file} must contain a top-level 'hpo:' section.")

    __build_environment(environment_config_file)

    template_yaml = h["template_yaml"]
    template_path = Path(template_yaml)
    if not template_path.exists():
        raise FileNotFoundError(
            f"Template YAML not found: {template_path.resolve()}"
        )
    template_text = template_path.read_text()

    if "search_space" not in h or not isinstance(h["search_space"], dict):
        raise ValueError("Missing 'search_space' in the HPO config.")

    missing = [k for k in h["search_space"].keys() if f"__{k}__" not in template_text]
    if missing:
        raise ValueError(
            f"Template {template_path} missing placeholders for: {missing}"
        )

    metric_tag = h.get("metric_tag", "Environment/Cumulative Reward")
    direction = h.get("direction", "maximize").lower()
    if direction not in ("maximize", "minimize"):
        raise ValueError("direction must be 'maximize' or 'minimize'")
    mode = "max" if direction == "maximize" else "min"

    num_samples = int(h.get("num_samples", 20))
    scheduler_name = h.get("scheduler", "asha").lower()
    grace_iter = int(h.get("grace_iter", 3))
    local_dir = h.get("local_dir", "ray_results")
    max_t = h.get("max_t", 100)
    reduction_factor = h.get("reduction_factor", 2)
    sampler_name = h.get("sampler", "optuna").lower()
    no_episode_penalty = h.get("no_episode_penalty", -1000000 if mode == "max" else 1000000)
    smoothing = h.get("smoothing", 0.6)

    # Capacity limiting: per-trial envs and overall concurrency
    capacity = workload.get_number_of_environments_for_workload(0.5)
    per_trial_num_envs = int(h.get("per_trial_num_envs", 1))
    if per_trial_num_envs < 1:
        per_trial_num_envs = 1
    max_concurrent_trials = num_ray_envs or max(1, capacity // per_trial_num_envs)

    from ray import tune as _t
    space = {}
    for key, spec in h["search_space"].items():
        t = str(spec["type"]).lower()
        if t == "choice":
            space[key] = _t.choice(spec["values"])
        elif t == "randint":
            space[key] = _t.randint(int(spec["low"]), int(spec["high"]))
        elif t == "uniform":
            space[key] = _t.uniform(float(spec["low"]), float(spec["high"]))
        elif t == "loguniform":
            space[key] = _t.loguniform(float(spec["low"]), float(spec["high"]))
        else:
            raise ValueError(f"Unknown search type: {t} for {key}")
        
    for key, value in h["defaults"].items():
        space[key] = float(value)

    fixed_cfg = {
        "_environment_config_file": environment_config_file,
        "_session_dir": session_dir,
        "_ml_args": ml_args,
        "_metric_tag": metric_tag,
        "_template_text": template_text,
        "_per_trial_num_envs": per_trial_num_envs,
        "_mode": mode,
        "_no_episode_penalty": no_episode_penalty,
        "_smoothing": smoothing
    }

    ray.init(
        ignore_reinit_error=True,
        include_dashboard=False,
        dashboard_host="127.0.0.1",
        dashboard_port=8265,
    )

    if sampler_name == "optuna":
        search = OptunaSearch(metric="reward", mode=mode)
    elif sampler_name == "random":
        search = None  # Ray's default random search
    else:
        raise ValueError("sampler must be 'optuna' or 'random'")
    
    full_path = textUtils.get_ray_path(local_dir) / session_dir
    if (full_path).exists():
        pkl = textUtils.find_searcher_file(full_path)
        search.restore(str(full_path / pkl))
        logging.info(f"{len(search._ot_study.trials)} previous trails loaded from {str(full_path / pkl)}. Best current score: {search._ot_study.best_value}")

    scheduler = None
    if scheduler_name == "asha":
        scheduler = ASHAScheduler(
            time_attr="global_step", 
            metric="reward",
            mode=mode,
            grace_period=grace_iter,
            reduction_factor = reduction_factor,
            max_t=max_t,
        )

    tuner = tune.Tuner(
        tune.with_parameters(__trainable),
        param_space={**space, **fixed_cfg} if not (full_path).exists() else fixed_cfg,
        tune_config=tune.TuneConfig(
            search_alg=search,
            scheduler=scheduler,
            num_samples=num_samples,
            max_concurrent_trials=max_concurrent_trials,
            trial_dirname_creator=__short_trial_name
        ),
        run_config=tune.RunConfig(
            name=session_dir,
            storage_path=textUtils.get_ray_path(local_dir),
            verbose=1,
            sync_config=ray.tune.SyncConfig(sync_artifacts=False),
            failure_config=FailureConfig(max_failures=10),
            callbacks=[
                TBXLoggerMidHParams(),
                TrialFailureCallback()
            ]
        )
    )

    results = tuner.fit()
    best = results.get_best_result(metric="reward", mode=mode)

    logging.info("\n=== Best Trial ===")
    logging.info("Reward:", best.metrics.get("reward"))
    logging.info("Params:", best.config)



def __trainable(config: dict):
    ml_args = config.pop("_ml_args")
    template_text = config.pop("_template_text")
    per_trial_num_envs = int(config.pop("_per_trial_num_envs"))
    session_dir = config.pop("_session_dir")

    ray_dir = Path(session.get_trial_dir())
    trial_suffix = session.get_trial_id()
    model_name = f"trial_{trial_suffix}"

    replaces = dict(config)

    rendered = __render_template(
        template_text, replaces
    )

    tmp_yaml = ray_dir / f"{model_name}.yaml"
    tmp_yaml.write_text(rendered)

    block_size = max(4, per_trial_num_envs)
    base_port = __pick_base_port(block_size)

    cmd = __create_mlagents_cmd(
        yaml_path=tmp_yaml,
        model_name = model_name,
        session_dir=session_dir, 
        ml_args=ml_args,
        base_port=base_port,
        trial_id=trial_suffix,
        num_envs=per_trial_num_envs
    )

    ret = __run_training(cmd, config, tmp_yaml)
    time_scale = round(int(get_param(ml_args, '--time-scale')) / 2)

    while ret == 999 and time_scale >= 1:
        logging.info(f"Trial {model_name} crashed. Retrying with --time-scale={time_scale}.")
        set_param(ml_args, '--time-scale', time_scale)
        ml_args = TrialFailureCallback.update_retry_flag(ml_args, session_dir, model_name)

        cmd = __create_mlagents_cmd(
            yaml_path=tmp_yaml,
            model_name = model_name,
            session_dir=session_dir, 
            ml_args=ml_args,
            base_port=base_port,
            trial_id=trial_suffix,
            num_envs=per_trial_num_envs
        )

        time_scale = round(time_scale / 2)
        ret = __run_training(cmd, config, tmp_yaml)

    if ret == 999:
        logging.info(f"Trial {model_name} exceed all time_scale retries. Terminating trail...")
        session.report({
            "reward": config["_no_episode_penalty"],
            "global_step": 0,
        })

        return

    if ret != 0:
        logging.info(f"Trial {model_name} crashed with return code {ret}.")
        raise RuntimeError(f"mlagents-learn failed with code {ret}.")
        

def __run_training(cmd: list[str], config: dict, tmp_yaml: Path):
    mode = config.get("_mode")
    metric_tag = config.get("_metric_tag")
    no_episode_penalty = config.get("_no_episode_penalty")
    smoothing = config.get("_smoothing")

    creationflags = 0
    if os.name == "nt":
        creationflags = getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0)

    proc = subprocess.Popen(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        bufsize=65536,
        creationflags=creationflags,
    )

    log_file = tmp_yaml.with_suffix(".log")

    # --- Setup Threaded Reader ---
    q = Queue()
    t = threading.Thread(target=__enqueue_output, args=(proc.stdout, q))
    t.daemon = True # Thread dies when main thread exits
    t.start()

    best_val = None
    extremum = max if mode == "max" else min
    behavior, _ = __getBehaviorTag(metric_tag)
    ema_reward = None

    last_output_time = time.time()
    MAX_STALE_TIME = 650 
    ret = -1

    with open(log_file, "a", buffering=65536) as logf:
        try:
            while True:
                # 1. Try to get a line from the queue without blocking
                try:
                    line = q.get_nowait()
                except Empty:
                    line = None

                if line:
                    sys.stdout.write(line)
                    logf.write(line)
                    last_output_time = time.time()
                    
                    parsed = parse_training_line(line)

                    logging.debug(f"{session.get_trial_id()}: {parsed}")

                    if parsed:
                        behaviorName, step, val = parsed

                        if behavior == behaviorName:
                            val = val if val is not None else no_episode_penalty
                            if ema_reward is None:
                                ema_reward = val
                            else:
                                ema_reward = smoothing * ema_reward + (1 - smoothing) * val

                            best_val = ema_reward if best_val is None else extremum(best_val, ema_reward)

                            logging.debug(f"{session.get_trial_id()}: reward: {best_val}; ema_reward: {ema_reward}; global_step: {step}")
                            session.report({"reward": best_val, "global_step": step})
                else:
                    # Small sleep to keep CPU usage low
                    time.sleep(0.1)

                # 2. Check if process exited
                poll_status = proc.poll()
                if poll_status is not None:
                    # Process ended naturally
                    ret = poll_status
                    break

                # 3. Watchdog check: If main thread hasn't seen a line in 650s
                if (time.time() - last_output_time) > MAX_STALE_TIME:
                    logging.warning(f"\n[WATCHDOG] Stale process detected (PID {proc.pid}; ID {session.get_trial_id()}). Killing tree...")
                    ret = 999 
                    break

        finally:
            __terminate_process_tree(proc)
            return ret


def __enqueue_output(out, queue):
    """Background thread function to read from the pipe."""
    try:
        for line in iter(out.readline, ''):
            queue.put(line)
        out.close()
    except Exception:
        pass


def __get_step_number(log_line: str) -> int:
    match = re.search(r"Step:\s*(\d+)", log_line)
    if not match:
        return -1
    return int(match.group(1))


def __contains_word(text: str, word: str) -> bool:
    pattern = r'\b' + re.escape(word) + r'\b'
    return re.search(pattern, text, flags=re.IGNORECASE) is not None


def parse_training_line(line: str) -> Optional[Tuple[str, int, Optional[float]]]:
    m = _LINE_RE.search(line)
    
    if not m:
        return None
    
    behavior = m.group(1).strip()
    step = int(m.group(2))
    mean_reward = float(m.group(3)) if m.group(3) is not None else None
    return behavior, step, mean_reward


def __read_yaml(path: str) -> dict:
    with open(path, "r") as f:
        return yaml.safe_load(f) or {}


def __render_template(text: str, replaces: dict) -> str:
    out = text
    for k, v in replaces.items():
        out = out.replace(f"__{k}__", str(v))
    return out


def __latest_event_dir(run_dir: Path) -> Optional[Path]:
    return run_dir if run_dir.exists() else None


def __iter_event_runs(logdir: Path):
    # Yield subdirs that actually contain TF event files.
    for root, _, files in os.walk(str(logdir)):
        if any(f.startswith("events.out.tfevents") for f in files):
            yield root


def __read_best_scalar(run_root: Path, metric_tag: str, no_episode_penalty: int):
    behavior, tag = __getBehaviorTag(metric_tag)

    latest_step_overall = 0
    best_value_overall = None

    for run_dir in __iter_event_runs(run_root):
        acc = ea.EventAccumulator(run_dir)
        acc.Reload()

        # Match the tag per-run like your original logic.
        run_name = os.path.basename(run_dir)
        t = tag if (behavior and run_name == behavior) else metric_tag

        scalars = acc.Tags().get("scalars", [])
        if t in scalars:
            ev = acc.Scalars(t)
            if ev:
                latest_step_overall = max(latest_step_overall, ev[-1].step)
                best_in_run = max(e.value for e in ev)
                best_value_overall = (
                    best_in_run if best_value_overall is None
                    else max(best_value_overall, best_in_run)
                )
    
    return 0, no_episode_penalty


def __getBehaviorTag(metric_tag: str):
    if "/" in metric_tag:
        behavior, tag = metric_tag.split("/", 1)
    else:
        behavior, tag = None, metric_tag

    return behavior, tag


def __create_mlagents_cmd(
    yaml_path: Path,
    model_name: str,
    session_dir: str,
    ml_args:List[str],
    base_port: int,
    trial_id: str,
    num_envs: int,
    resume: bool = False
):
    
    ml_args = list(ml_args)
    ml_args.append(f"--num-envs={num_envs}")
    ml_args.append(f"--base-port={base_port}")
    
    cmd = unity_helper.build_mlagents_command(
        model_config_file=yaml_path,
        session_dir=session_dir,
        model_name=model_name,
        ml_args=ml_args,
        resume=resume
    )

    cmd += ["--env-args", f"--hpo_trial_id={trial_id}"]

    return cmd


def set_param(args, param, value, add_if_missing=True):
    i = 0
    while i < len(args):
        a = args[i]

        # case: --param=X
        if a.startswith(param + "="):
            args[i] = f"{param}={value}"
            return args

        # case: --param X
        if a == param:
            if i + 1 < len(args):
                args[i + 1] = str(value)
            else:
                args.append(str(value))
            return args

        i += 1

    if add_if_missing:
        args.extend([param, str(value)])

    return args


def get_param(args, param, default=None):
    i = 0
    while i < len(args):
        a = args[i]

        # case: --param=X
        if a.startswith(param + "="):
            return a.split("=", 1)[1]

        # case: --param X
        if a == param:
            if i + 1 < len(args):
                return args[i + 1]
            return None

        i += 1

    return default


def __terminate_process_tree(proc: subprocess.Popen):
    try:
        if proc.poll() is None:
            proc.terminate()
            try:
                proc.wait(timeout=10)
            except subprocess.TimeoutExpired:
                proc.kill()
    except Exception:
        pass


def __build_environment(environment_config_file: str):
    build_code = unity_helper.call_function(
        "BuildScript.BuildTrainingEnvironment",
        Path("config", environment_config_file),
        logFile=Path("..", "Logs", "LogFileBuild.txt"),
    )
    if build_code != 0:
        logging.info("Error in Build: check ..\\Logs\\LogFileBuild.txt.")
        sys.exit(1)


def __short_trial_name(trial):
    # Use the internal trial_id or any other simple identifier
    return f"trial_{trial.trial_id}"


def __port_is_free(port: int) -> bool:
    # Check both IPv4 and IPv6 listeners
    for family in (socket.AF_INET, socket.AF_INET6):
        try:
            s = socket.socket(family, socket.SOCK_STREAM)
            s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            s.bind(("" if family == socket.AF_INET else "::", port))
            s.close()
        except OSError:
            return False
    return True


def __pick_base_port(block_size: int, min_port: int = 5005, max_port: int = 65000, max_tries: int = 50) -> int:
    """
    Pick a base port such that [base_port, base_port + block_size - 1] are free.
    block_size should be >= number of parallel Unity workers this trial will spawn
    (usually equals --num-envs). We add slack by using block_size as-is.
    """
    # Put some randomness to spread concurrent trials
    start = random.randint(min_port, min(min_port + 5000, max_port - block_size - 1))
    for offset in range(0, max_port - block_size - start, block_size):
        candidate = start + offset
        ok = True
        for p in range(candidate, candidate + block_size):
            if not __port_is_free(p):
                ok = False
                break
        if ok:
            return candidate
        if offset // block_size > max_tries:
            break

    # Fallback: brute force from min_port
    for candidate in range(min_port, max_port - block_size):
        ok = True
        for p in range(candidate, candidate + block_size):
            if not __port_is_free(p):
                ok = False
                break
        if ok:
            return candidate

    raise RuntimeError("Could not find a free base port range")
