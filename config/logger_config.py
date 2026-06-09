import logging
import sys
from pathlib import Path

def setup_logging(log_filename="hpo_master_log.log"):
    # 1. Get the root logger
    logger = logging.getLogger() 
    logger.setLevel(logging.INFO)

    # Clean existing handlers (prevents double-logging if called twice)
    if logger.hasHandlers():
        logger.handlers.clear()

    # 2. Create the format
    formatter = logging.Formatter('%(asctime)s | %(levelname)s | %(message)s', datefmt='%Y-%m-%d %H:%M:%S')

    # 3. Handler for Command Line (CMD)
    cmd_handler = logging.StreamHandler(sys.stdout)
    cmd_handler.setFormatter(formatter)
    cmd_handler.setLevel(logging.WARNING)
    logger.addHandler(cmd_handler)

    # 4. Handler for File
    file_handler = logging.FileHandler(log_filename)
    file_handler.setLevel(logging.DEBUG)
    file_handler.setFormatter(formatter)
    logger.addHandler(file_handler)

    return logger