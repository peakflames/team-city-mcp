#!/usr/bin/env python3
"""Build automation for TeamCityMcpServers."""

import os
import signal
import subprocess
import sys

SOLUTION = "TeamCityMcpServers.sln"
STDIO_PROJECT = "src/TeamCityMcpServer"
HTTP_PROJECT = "src/TeamCityRemoteMcpServer"
PID_FILE = ".server.pid"


def cmd_build():
    result = subprocess.run(["dotnet", "build", SOLUTION], check=False)
    sys.exit(result.returncode)


def cmd_run():
    result = subprocess.run(["dotnet", "run", "--project", STDIO_PROJECT], check=False)
    sys.exit(result.returncode)


def cmd_start():
    if os.path.exists(PID_FILE):
        with open(PID_FILE) as f:
            pid = int(f.read().strip())
        try:
            os.kill(pid, 0)
            print(f"HTTP server is already running (PID {pid}).")
            sys.exit(1)
        except (OSError, ProcessLookupError):
            pass

    proc = subprocess.Popen(
        ["dotnet", "run", "--project", HTTP_PROJECT],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    with open(PID_FILE, "w") as f:
        f.write(str(proc.pid))
    print(f"HTTP server started (PID {proc.pid}).")


def cmd_stop():
    if not os.path.exists(PID_FILE):
        print("No PID file found — server may not be running.")
        sys.exit(1)

    with open(PID_FILE) as f:
        pid = int(f.read().strip())

    try:
        os.kill(pid, signal.SIGTERM)
        print(f"HTTP server stopped (PID {pid}).")
    except (OSError, ProcessLookupError):
        print(f"Process {pid} not found — may have already stopped.")
    finally:
        os.remove(PID_FILE)


def cmd_status():
    if not os.path.exists(PID_FILE):
        print("HTTP server is not running (no PID file).")
        return

    with open(PID_FILE) as f:
        pid = int(f.read().strip())

    try:
        os.kill(pid, 0)
        print(f"HTTP server is running (PID {pid}).")
    except (OSError, ProcessLookupError):
        print(f"HTTP server is not running (stale PID {pid}).")
        os.remove(PID_FILE)


COMMANDS = {
    "build": cmd_build,
    "run": cmd_run,
    "start": cmd_start,
    "stop": cmd_stop,
    "status": cmd_status,
}

if __name__ == "__main__":
    if len(sys.argv) < 2 or sys.argv[1] not in COMMANDS:
        print(f"Usage: python build.py [{' | '.join(COMMANDS)}]")
        sys.exit(1)
    COMMANDS[sys.argv[1]]()
