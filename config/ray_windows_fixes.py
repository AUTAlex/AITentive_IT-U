"""
Windows-specific reliability fixes for Ray Tune.

Fixes intermittent PermissionError / WinError 5 caused by:
- antivirus
- delayed file handle release
- NTFS locking semantics

This module retries ONLY:
- atomic os.replace() used by Ray checkpointing
- file creation for Ray experiment artifacts

Safe to import once at process startup.
"""

import os
import time
import builtins
import platform
import io
from pathlib import Path
import logging

# -------------------------------
# Configuration
# -------------------------------

MAX_RETRIES = 20
BASE_SLEEP_S = 0.3
RAY_PATH_MARKER = "driver_artifacts"

# -------------------------------
# Guard: Windows only
# -------------------------------

if platform.system() != "Windows":
    # No-op on non-Windows systems
    def apply():
        pass
else:
    # -------------------------------
    # Patch os.replace
    # -------------------------------

    _orig_os_replace = os.replace

    def _os_replace_with_retry(src, dst):
        path_str = str(dst)

        if RAY_PATH_MARKER in path_str:
            for i in range(MAX_RETRIES):
                try:
                    return _orig_os_replace(src, dst)
                except PermissionError:
                    if i == MAX_RETRIES - 1:
                        logging.warning(f"Could not replace {src}")
                        return
                    time.sleep(BASE_SLEEP_S * (i + 1))
        return _orig_os_replace(src, dst)

    # -------------------------------
    # Patch open(...)
    # -------------------------------

    _orig_open = builtins.open
    _orig_io_open = io.open

    def _wrap_open_with_retry(orig_open):
        def _open_with_retry(file, mode="r", *args, **kwargs):
            path_str = str(file)
            
            if (
                RAY_PATH_MARKER in path_str
                and ("w" in mode or "a" in mode)
            ):
                for i in range(MAX_RETRIES):
                    try:
                        return orig_open(file, mode, *args, **kwargs)
                    except PermissionError:
                        if i == MAX_RETRIES - 1:
                            p = Path(file)
                            tmp_path = p.with_name(p.name + "_tmp")
                            logging.warning(f"Could not write to {file}, write to {tmp_path} instead.")
                            return orig_open(tmp_path, mode, *args, **kwargs)
                        time.sleep(BASE_SLEEP_S * (i + 1))
            return orig_open(file, mode, *args, **kwargs)
    
        return _open_with_retry

    # -------------------------------
    # Public entry point
    # -------------------------------

    def apply():
        os.replace = _os_replace_with_retry
        builtins.open = _wrap_open_with_retry(_orig_open)
        io.open = _wrap_open_with_retry(_orig_io_open)
