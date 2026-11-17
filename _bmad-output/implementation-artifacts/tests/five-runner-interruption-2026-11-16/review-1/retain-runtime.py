import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

destination = Path(sys.argv[1])
destination.mkdir(parents=True, exist_ok=False)
scratch = Path(os.environ["TMPDIR"])
arguments = ["dotnet", sys.argv[2], "-method", sys.argv[3], *sys.argv[4:], "-parallelMode", "none", "-failSkips", "-result-xml", str(destination / "tests.xml")]
environment = os.environ.copy()
environment["PARTIES_MEMORIES_OWNED_RUNTIME"] = "1"
(destination / "command.json").write_text(json.dumps({"Arguments": arguments, "Prepared": environment["Parties__Testing__PreparedRuntimeManifest"], "Scratch": str(scratch)}, indent=2))
started = time.monotonic()
with (destination / "runtime.log").open("w") as stream:
    child = subprocess.Popen(arguments, env=environment, stdout=stream, stderr=subprocess.STDOUT)
    (destination / "runner-pid.json").write_text(json.dumps({"Pid": child.pid}))
    code = child.wait()
for path in scratch.iterdir():
    if path.is_dir() and (path.name.startswith("parties-five-memory-runtime-") or path.name.startswith("runner-interruption-") or path.name.startswith("hexalith-parties-topology-")):
        shutil.copytree(path, destination / path.name)
retained = []
for path in scratch.glob("hexalith-parties-topology-*"):
    session = path.name.removeprefix("hexalith-parties-topology-")
    controls = []
    for role in ["scheduler", "placement", "sentry"]:
        inspected = subprocess.run(["docker", "inspect", "hexalith-parties-dapr-" + role + "-" + session], capture_output=True, text=True)
        controls.append({"Role": role, "InspectExitCode": inspected.returncode, "Inspection": json.loads(inspected.stdout) if inspected.returncode == 0 else None})
    retained.append({"Session": session, "OriginalRoot": str(path), "RetainedPrivateRoot": str(destination / path.name), "Controls": controls})
if retained:
    (destination / "retained-sessions-private.json").write_text(json.dumps(retained, indent=2))
    print("Failed session custody retained; reconcile before release-scratch acknowledgement:", destination, flush=True)
    release_deadline = time.monotonic() + 600
    while not (destination / "release-scratch").exists():
        if time.monotonic() > release_deadline:
            raise TimeoutError("Failed-session custody was copied privately; timed out awaiting exact reconciliation before wrapper cleanup")
        time.sleep(0.1)
(destination / "summary.json").write_text(json.dumps({"ExitCode": code, "ElapsedSeconds": time.monotonic() - started}, indent=2))
print(destination.name, code, flush=True)
sys.exit(code)
