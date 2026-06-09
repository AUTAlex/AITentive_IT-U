import subprocess
import threading
import time
import sys
import psutil
import faulthandler


class TrainerMonitor:

    def __init__(self, command, stall_timeout=300, check_interval=10):
        self.command = command
        self.stall_timeout = stall_timeout
        self.check_interval = check_interval
        self.last_output_time = time.time()
        self.process = None

        faulthandler.enable()

    def start(self):
        print("Launching training process...")
        self.process = subprocess.Popen(
            self.command,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            universal_newlines=True,
            bufsize=1,
            shell=True
        )

        threading.Thread(target=self._read_output, daemon=True).start()
        threading.Thread(target=self._monitor, daemon=True).start()

        self.process.wait()
        return self.process.returncode

    def _read_output(self):
        for line in self.process.stdout:
            print(line, end="")
            self.last_output_time = time.time()

    def _monitor(self):
        while True:
            time.sleep(self.check_interval)

            idle = time.time() - self.last_output_time

            if idle > self.stall_timeout:
                print("\n TRAINER STALL DETECTED")
                print(f"No stdout activity for {idle:.1f} seconds")

                print("\n=== Python Stack Dump ===")
                faulthandler.dump_traceback(file=sys.stderr, all_threads=True)

                print("\n=== Child Processes ===")
                try:
                    parent = psutil.Process(self.process.pid)
                    children = parent.children(recursive=True)
                    for child in children:
                        print(f"PID {child.pid} | {child.name()} | Status: {child.status()}")
                except Exception as e:
                    print("Process inspection failed:", e)

                print("\n========================\n")