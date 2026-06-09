from pathlib import Path
import os
import json


def printElapsedTime(start, end, functionName):
    hours, rem = divmod(end-start, 3600)
    minutes, seconds = divmod(rem, 60)
    
    print("Elapsed time during computation of \"{}\": {:0>2}:{:0>2}:{:05.2f}".format(functionName, int(hours), int(minutes), seconds))


def get_file_names_for_config_string(rawDataFileName, configString):
    fileName = rawDataFileName.replace('raw.csv', '')
    parts = configString.split('NT')

    behaviouralDataFileName = "{0}{1}.json".format(fileName, parts[0])
    reactionTimeFileName = "{0}_rt_{1}{2}.json".format(fileName, 'NT', parts[1])

    return (behaviouralDataFileName, reactionTimeFileName)


def get_model_name(model_config_file, environment_config_file):
    return str(Path(model_config_file).with_suffix('').name) + str(Path(environment_config_file).with_suffix('').name)


def get_model_path(session_dir, model_name):
    return Path(get_project_root(), "Assets", "Models", session_dir, model_name)


def get_ray_path(session_dir):
    return Path(get_project_root(), "Assets", "Ray", session_dir)


def find_searcher_file(directory):
    try:
        for filename in os.listdir(directory):
            if filename.startswith("searcher") and filename.endswith(".pkl"):
                return os.path.join(directory, filename)
    except FileNotFoundError:
        print(f"Directory not found: {directory}")
    return None


def remove_left_directory(path):
    directories = str(path).split(os.path.sep)

    if len(directories) >= 2:
        remaining_directories = directories[1:]
        modified_path = os.path.sep.join(remaining_directories)

        return modified_path
    else:
        return path
    

def find_first_value(json_obj, key):
    """
    Recursively searches through the JSON object and returns the first value for the specified key.
    
    :param json_obj: The JSON object (which can be a dictionary or a list).
    :param key: The key whose value is to be found.
    :return: The value associated with the first occurrence of the specified key, or None if the key is not found.
    """
    if isinstance(json_obj, dict):
        if key in json_obj:
            return json_obj[key]
        
        for k, v in json_obj.items():
            result = find_first_value(v, key)
            
            if result is not None:
                return result
            
    elif isinstance(json_obj, list):
        for item in json_obj:
            result = find_first_value(item, key)
            
            if result is not None:
                return result
    
    return None


def get_absolute_path_for_file(file_path):
    path = Path(file_path)

    # 1. Try as given (absolute or relative from current working directory)
    if not path.exists():
        # 2. Try resolving relative to the script location
        script_dir = Path(__file__).parent.resolve()
        candidate = script_dir / path
        if candidate.exists():
            path = candidate
        else:
            # 3. Try resolving relative to project root (where train.py likely lives)
            project_root = Path.cwd().resolve()
            candidate2 = project_root / path
            if candidate2.exists():
                path = candidate2
            else:
                raise FileNotFoundError(
                    f"Could not find JSON file: {file_path}\n"
                    f"Tried: {path.resolve()}, {candidate.resolve()}, {candidate2.resolve()}"
                )
        
    return path


def get_project_root():
    return Path(__file__).parent.parent.resolve()


def load_json_from_file(file_path):
    path = get_absolute_path_for_file(file_path)
    
    with open(path, "r", encoding="utf-8") as file:
        json_data = json.load(file)

    return json_data
