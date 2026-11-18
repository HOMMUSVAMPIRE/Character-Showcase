"""
A stand-in for Unity, for run_tests.py's tests (test_run_tests.py): run as Unity would be, it writes the results and the
log the test chose and exits as Unity would, so the script can be tested without Unity. FAKE_UNITY_CONFIG names a JSON
file: {platform: {"results": canned XML path or null, "log": a canned log's path or null, "exit": code}}. Each run's
arguments are appended to FAKE_UNITY_CALLS (a JSON line each), so a test can see what it was asked to run.
"""
import json
import os
import shutil
import sys


def argument(name):
    return sys.argv[sys.argv.index(name) + 1] if name in sys.argv else None


def main():
    with open(os.environ["FAKE_UNITY_CONFIG"], encoding="utf-8") as f:
        config = json.load(f)
    with open(os.environ["FAKE_UNITY_CALLS"], "a", encoding="utf-8") as f:
        f.write(json.dumps(sys.argv[1:]) + "\n")
    run = config[argument("-testPlatform")]
    if run.get("log"):
        shutil.copyfile(run["log"], argument("-logFile"))
    else:
        with open(argument("-logFile"), "w", encoding="utf-8") as f:
            f.write("Fake Unity ran.\n")
    if run.get("results"):
        shutil.copyfile(run["results"], argument("-testResults"))
    return run.get("exit", 0)


if __name__ == "__main__":
    sys.exit(main())
