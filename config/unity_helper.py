import subprocess
from pathlib import Path
import sys
import os
import io
import textUtils
import workload
from trainer_monitor import TrainerMonitor


def call_function(function_name, *args, logFile=""):

    strArgs = []

    for arg in args:
        strArgs.append(str(arg))

    print("Executing Unity function: {}".format(function_name) + "\nArgs:\t" + '\n\t'.join([elem for elem in strArgs]))
    if logFile != "":
        print("Writing log files to {}.".format(logFile))

    command = ["Unity", "-accept-apiupdate", "-batchmode", "-quit", "-executeMethod", function_name]
    command.extend(strArgs)
    command.extend(["-logFile", str(logFile)])

    try:
        result = subprocess.run(command, 
                                text=True, 
                                shell=True,
                                stdout=sys.stdout)
    except io.UnsupportedOperation:
        result = subprocess.run(command, 
                                text=True, 
                                shell=True,
                                stdout=subprocess.PIPE)
        
        print(result.stdout)

    return result.returncode


def start_training(model_config_file, session_dir, model_name, ml_args, monitor_training=False, resume=False):

    command = build_mlagents_command(
        model_config_file,
        session_dir,
        model_name,
        ml_args,
        resume,
    )

    print("Start training:\n" +
          "\t model_config_file: {} \n".format(str(model_config_file)) +
          "\t session_dir: {} \n".format(str(session_dir)) +
          "\t model_name: {} \n".format(str(model_name)) +
          "\t command: {} \n".format(command))
    
    if monitor_training:
        monitor = TrainerMonitor(command, stall_timeout=300)
        return monitor.start()

    result = subprocess.run(command, 
                            text=True, 
                            shell=True,
                            stdout=sys.stdout)

    return result.returncode


def set_default_ml_args(ml_args):
    set_value_ml_args(ml_args, "num-envs", workload.get_number_of_environments_for_workload())
    set_value_ml_args(ml_args, "time-scale", 20)
    set_value_ml_args(ml_args, "timeout-wait", 120)
    set_value_ml_args(ml_args, "max-lifetime-restarts", -1)
    set_value_ml_args(ml_args, "restarts-rate-limit-n", -1)
    set_value_ml_args(ml_args, "width", 640)
    set_value_ml_args(ml_args, "height", 360)


def set_value_ml_args(ml_args, arg_name, value):
    existing = next((arg for arg in ml_args if arg.startswith(f"--{arg_name}")), None)

    if existing is None:
        ml_args.append(f"--{arg_name}={value}")


def build_mlagents_command(
    model_config_file,
    session_dir,
    model_name,
    ml_args=None,
    resume=False
):
    
    ml_args = list(ml_args) if ml_args else []
    set_default_ml_args(ml_args)

    command = ["mlagents-learn",
                str(model_config_file),
                "--run-id=" + str(textUtils.get_model_path(session_dir, model_name)), 
                "--env=" + str(textUtils.get_absolute_path_for_file(Path("..", "Build", "TrainingEnvironment")))]
    
    command.extend(ml_args)

    if resume:
        command.append("--resume")

    return command


def convertBehaviourMeasurementOfSession(sessionPath, scoreString, configString, supervisorSettingsPath, behavioralDataCollectionSettingsPath):
    sub_dirs = [d for d in sessionPath.glob('*') if d.is_dir()]

    totalEnv = len(sub_dirs)
    count = 0

    print("Total number of environments: {0}".format(totalEnv))

    for dir in sub_dirs:
        scorePath = Path(dir, scoreString)
        rawDataPath = [d for d in scorePath.glob('*raw*')][0]
        rawDataFileName = os.path.basename(rawDataPath)

        (behaviouralDataFileName, reactionTimeFileName) = textUtils.get_file_names_for_config_string(rawDataFileName, configString)

        if (not os.path.isfile(Path(scorePath, behaviouralDataFileName)) or not os.path.isfile(Path(scorePath, reactionTimeFileName))):
            call_function("API.ConvertRawToBinData", 
                          supervisorSettingsPath, 
                          behavioralDataCollectionSettingsPath, 
                          rawDataPath, 
                          logFile=Path("..", "Logs", "LogFileConvertRawToBinData.txt"))
        else:
            print("Data already converted for environment {0}. Skip conversion...".format(dir))

        count += 1
        print("{0}/{1} environments converted".format(count, totalEnv))