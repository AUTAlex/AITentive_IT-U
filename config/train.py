#!/usr/bin/env python3

import argparse
from pathlib import Path
import shutil
import unity_helper
import time
import textUtils
import os
import yaml
import ray_tune_hpo

import numpy as np
np.float = float


def Main():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "model_config_file",
        type=str,
        help=(
            "Path to the YAML configuration file. "
            "This can be either a standard ML-Agents trainer config (for example a PPO YAML) "
            "that specifies concrete hyperparameters, or an HPO controller YAML that contains "
            "a top-level 'hpo:' section defining a hyperparameter search space. "
            "If an HPO config is detected, Ray Tune will be launched automatically to perform optimization."
        ),
    )
    parser.add_argument(
        "environment_config_file",
        help="JSON configuration file that defines the Unity environment build parameters.",
        type=str,
    )
    parser.add_argument(
        "session_dir",
        help="Name of the session directory where trained models and logs are stored inside Assets/Models/.",
        type=str,
    )
    parser.add_argument(
        "--num-ray-envs",
        "-r",
        help="Number of Unity environments to run in parallel. Default is auto-detected.",
        type=int,
    )
    parser.add_argument(
        "--monitor-training",
        "-m",
        action="store_true",
        help="Enable watchdog monitoring to detect trainer stalls and dump diagnostic information."
    )

    args, ml_args = parser.parse_known_args()

    if __is_hpo_config(args.model_config_file):
        ray_tune_hpo.run_hpo(
            hpo_config_file=args.model_config_file,
            environment_config_file=args.environment_config_file,
            session_dir=args.session_dir,
            num_ray_envs=args.num_ray_envs,
            ml_args=ml_args
        )
        return

    startTime = time.time()

    train_model(model_config_file = args.model_config_file, 
                environment_config_file = args.environment_config_file, 
                session_dir = args.session_dir,
                ml_args = ml_args,
                monitor_training = args.monitor_training)
    enrich_model(model_config_file = args.model_config_file, 
                 environment_config_file = args.environment_config_file, 
                 session_dir = args.session_dir)

    endTime = time.time()

    textUtils.printElapsedTime(startTime, endTime, "train")


def train_model(model_config_file, environment_config_file, session_dir, ml_args, monitor_training):
    session_dir = __get_top_level(session_dir)

    model_name = textUtils.get_model_name(model_config_file = model_config_file, 
                                          environment_config_file = environment_config_file)
    model_path = textUtils.get_model_path(session_dir = session_dir, 
                                          model_name = model_name)

    resume = __check_for_existing_model(model_path)
    
    code = unity_helper.call_function("BuildScript.BuildTrainingEnvironment",
                                      Path("config", environment_config_file),
                                      logFile=Path("..", "Logs", "LogFileBuild.txt"))

    if code != 0:
        print("Error in Build: check LogFileBuild.txt for more details.")
        exit()

    unity_helper.start_training(model_config_file = model_config_file, 
                                session_dir = session_dir, 
                                model_name = model_name,
                                resume = resume,
                                ml_args = ml_args,
                                monitor_training = monitor_training)


def enrich_model(model_config_file, environment_config_file, session_dir):
    session_dir = __get_top_level(session_dir)

    model_name = textUtils.get_model_name(model_config_file = model_config_file, 
                                          environment_config_file = environment_config_file)
    model_path = textUtils.get_model_path(session_dir = session_dir, 
                                          model_name = model_name)
    
    target_dir = Path(model_path, Path(environment_config_file).name)
    shutil.copyfile(environment_config_file, target_dir)

    code = unity_helper.call_function("PostProcessing.EnrichModels",
                                      textUtils.remove_left_directory(target_dir),
                                      logFile=Path("..", "Logs", "LogFilePostProcessing.txt"))

    if code != 0:
        return
    

def __check_for_existing_model(model_path):
    resume = False
    
    if Path.exists(model_path):
        print("There is already a model with the same id. Do you want to overwrite the model (O) or continue training [C])?")

        i = input()

        if  i == "o" or i == "O":
            print("Deleting of old session id...")
            long_path = r"\\?\{}".format(model_path.resolve())
            shutil.rmtree(long_path)
            try:
                Path.unlink(Path(str(model_path) + '.meta'))
            except FileNotFoundError: 
                print("Could not find and delete {}.".format(Path(str(model_path) + '.meta')))
        else:
            resume = True

    return resume


def __rename_model(model_prefix, model_name, model_path):
    try:
        path = Path(model_path, "{}.onnx".format(model_prefix))
        new_path = path.replace(Path(path.parent, "{}{}.onnx".format(model_prefix, model_name)))
        print("File {} renamed to {}".format(path, new_path))
    except FileNotFoundError: 
        print("Could not find file {}.".format(path))


def __get_top_level(file_path):
    normalized_path = os.path.normpath(file_path)
    path_components = normalized_path.split(os.path.sep)
    top_level_directory = path_components[-1]
    
    return top_level_directory

def __is_hpo_config(path):
    try:
        with open(path, "r") as f:
            y = yaml.safe_load(f)
        return isinstance(y, dict) and "hpo" in y
    except Exception:
        return False


if __name__ == '__main__':
    Main()