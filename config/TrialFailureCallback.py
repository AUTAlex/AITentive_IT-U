from ray.tune import Callback
import logging
import shutil
from pathlib import Path
import textUtils

class TrialFailureCallback(Callback):

    @staticmethod
    def determine_retry_flag(session_path: Path) -> str:
        subdirs = [
            p for p in session_path.iterdir()
            if p.is_dir() and p.name != "run_logs"
        ]

        if not subdirs:
            return "--force"

        for d in subdirs:
            if not (d / "checkpoint.pt").exists():
                return "--force"

        return "--resume"
    
    @staticmethod
    def update_retry_flag(ml_args: list, session_dir: str, model_name: str) -> list:
        ml_args = [a for a in ml_args if a not in ("--resume", "--force")]

        session_path = textUtils.get_model_path(session_dir, model_name)
        retry_flag = TrialFailureCallback.determine_retry_flag(session_path)

        ml_args.append(retry_flag)

        return ml_args


    def on_trial_recover(self, iteration, trials, trial, **info):
        model_name = f"trial_{trial.trial_id}"
        session_dir = trial.config.get("_session_dir")

        session_path = textUtils.get_model_path(session_dir, model_name)
        retry_flag = TrialFailureCallback.determine_retry_flag(session_path)

        ml_args = list(trial.config.get("_ml_args"))

        ml_args = self.update_retry_flag(ml_args, session_dir, model_name)

        trial.config["_ml_args"] = ml_args

        logging.info(
            f"{trial.trial_id}: next retry will run with {retry_flag}"
        )