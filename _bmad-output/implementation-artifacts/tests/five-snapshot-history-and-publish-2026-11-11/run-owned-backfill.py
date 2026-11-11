#!/usr/bin/env python3
"""Qualify only an exclusively owned disposable Redis/Dapr backfill provider."""

import argparse
import json
import os
from pathlib import Path
import secrets
import signal
import socket
import subprocess
import tempfile
import time
import urllib.request
import uuid


def command(args):
    return subprocess.run(args, text=True, capture_output=True, check=True, timeout=60).stdout.strip()


def port():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]


def confirmed_container_absent(identifier):
    # A successful exact filtered listing proves absence. Daemon/transport errors
    # never become missing-resource evidence, unlike a failed inspect command.
    import re
    if not re.fullmatch(r"[0-9a-f]{12,64}", identifier):
        raise ValueError("Invalid owned container identity")
    query = subprocess.run(["docker", "container", "ls", "--all", "--quiet", "--no-trunc", "--filter", "id=" + identifier],
                           text=True, capture_output=True, timeout=10, check=True)
    matches = query.stdout.splitlines()
    if any(not re.fullmatch(r"[0-9a-f]{64}", value) or not value.startswith(identifier) for value in matches):
        raise ValueError("Owned container listing returned an unexpected identity")
    return not matches


def cleanup_owned(tests, sidecar, container, result):
    errors = []
    processes = {}
    result["ownedProcessIds"] = {}
    for label, process in (("tests", tests), ("sidecar", sidecar)):
        if process is None:
            continue
        result["ownedProcessIds"][label] = process.pid
        try:
            if process.poll() is None:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass  # An exited process is still waited/reaped below.
                except Exception as failure:
                    errors.append({"resource": label, "operation": "terminate", "error": type(failure).__name__})
        except Exception as failure:
            errors.append({"resource": label, "operation": "observe", "error": type(failure).__name__})
        try:
            process.wait(timeout=10)
        except Exception as failure:
            errors.append({"resource": label, "operation": "reap", "error": type(failure).__name__})
        try:
            processes[label] = process.poll() is not None
        except Exception as failure:
            processes[label] = False
            errors.append({"resource": label, "operation": "confirm-exit", "error": type(failure).__name__})
    result["ownedProcessesExited"] = all(processes.values())
    result["processCleanup"] = processes
    if container:
        result["ownedContainerId"] = container
        removal = None
        try:
            removal = subprocess.run(["docker", "rm", "--force", container], capture_output=True, timeout=30)
            result["containerRemovalExitCode"] = removal.returncode
        except Exception as failure:
            errors.append({"resource": "container", "operation": "remove", "error": type(failure).__name__})
        # Always query, even if removal failed or raced with automatic --rm.
        try:
            result["ownedContainerAbsent"] = confirmed_container_absent(container)
            if not result["ownedContainerAbsent"]:
                errors.append({"resource": "container", "operation": "confirm-absence", "error": "still-present"})
            elif removal is not None and removal.returncode != 0:
                result["containerRemovalExitRaceConfirmedAbsent"] = True
        except Exception as failure:
            result["ownedContainerAbsent"] = False
            errors.append({"resource": "container", "operation": "confirm-absence", "error": type(failure).__name__})
    result["cleanupErrors"] = errors
    result["cleanupSucceeded"] = result["ownedProcessesExited"] and (not container or result.get("ownedContainerAbsent") is True) and not errors


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--assembly", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    options = parser.parse_args()
    options.output.mkdir(parents=True, exist_ok=False)
    image = "redis/redis-stack:latest@sha256:880df9c228597cb0d15b585f39a4327d6ee2d8b0d0f155e3f75dba9a761d4ec3"
    container = None
    sidecar = None
    tests = None
    result = {"scope": "owned synthetic provider; no deployed writer drain or historical completeness"}
    completed = False
    try:
        container = command(["docker", "run", "--detach", "--rm", "--publish", "127.0.0.1::6379", image])
        redis_port = command(["docker", "port", container, "6379/tcp"]).rsplit(":", 1)[1]
        ready_until = time.monotonic() + 30
        while True:
            ping = subprocess.run(["docker", "exec", container, "redis-cli", "PING"], text=True, capture_output=True, timeout=5)
            if ping.returncode == 0 and ping.stdout.strip() == "PONG":
                break
            if time.monotonic() >= ready_until:
                raise RuntimeError("Owned Redis did not become ready")
            time.sleep(0.1)
        with tempfile.TemporaryDirectory(prefix="parties-backfill-provider-") as temporary:
            private = Path(temporary)
            os.chmod(private, 0o700)
            app = "parties-backfill-rehearsal-" + uuid.uuid4().hex
            store = "backfill-" + uuid.uuid4().hex[:12]
            http, grpc, internal, metrics = port(), port(), port(), port()
            token = secrets.token_hex(24)
            (private / "state.yaml").write_text(
                f"apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: {store}\nspec:\n  type: state.redis\n  version: v1\n  metadata:\n  - name: redisHost\n    value: 127.0.0.1:{redis_port}\n  - name: redisPassword\n    value: ''\n  - name: actorStateStore\n    value: 'true'\n")
            config = private / "runtime.json"
            config.write_text(json.dumps({"ownedDisposableStore": True, "daprAppId": app, "stateStoreName": store,
                                         "daprHttpEndpoint": f"http://127.0.0.1:{http}", "daprGrpcEndpoint": f"http://127.0.0.1:{grpc}",
                                         "daprApiToken": token, "redisEndpoint": f"127.0.0.1:{redis_port}"}))
            os.chmod(config, 0o600)
            daprd = Path.home() / ".dapr/bin/daprd"
            result["daprd"] = command([str(daprd), "--version"])
            result["redis"] = command(["docker", "exec", container, "redis-server", "--version"])
            result["image"] = image
            environment = dict(os.environ, DAPR_API_TOKEN=token)
            with (private / "daprd.log").open("w") as log:
                sidecar = subprocess.Popen([str(daprd), "--app-id", app, "--dapr-http-port", str(http), "--dapr-grpc-port", str(grpc),
                                            "--dapr-internal-grpc-port", str(internal), "--metrics-port", str(metrics),
                                            "--dapr-listen-addresses", "127.0.0.1", "--resources-path", str(private), "--log-level", "warn",
                                            "--enable-app-health-check=false"], env=environment, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
                ready_until = time.monotonic() + 30
                while True:
                    try:
                        request = urllib.request.Request(f"http://127.0.0.1:{http}/v1.0/healthz/outbound", headers={"dapr-api-token": token})
                        with urllib.request.urlopen(request, timeout=1) as response:
                            if response.status == 204:
                                break
                    except OSError:
                        pass
                    if sidecar.poll() is not None or time.monotonic() >= ready_until:
                        raise RuntimeError("Owned Dapr provider did not become ready")
                    time.sleep(0.1)
                environment["PARTIES_BACKFILL_STATE_RUNTIME_CONFIG"] = str(config)
                invocation = ["dotnet", str(options.assembly.resolve()), "-class", "Hexalith.Parties.Tests.Projections.PartyLegacyReadModelBackfillRuntimeTests",
                              "-class", "Hexalith.Parties.Tests.Projections.PartyLegacyBackfillProcessRuntimeTests", "-failSkips",
                              "-result-xml", str((options.output / "backfill.xml").resolve()), "-diagnostics"]
                with (options.output / "backfill.log").open("w") as test_log:
                    tests = subprocess.Popen(invocation, env=environment, stdout=test_log, stderr=subprocess.STDOUT, start_new_session=True)
                    result["testExitCode"] = tests.wait(timeout=420)
                if result["testExitCode"] != 0:
                    raise RuntimeError("Owned backfill tests failed; inspect retained result XML")
        completed = True
    finally:
        cleanup_owned(tests, sidecar, container, result)
        (options.output / "receipt.json").write_text(json.dumps(result, indent=2) + "\n")
        if completed and not result["cleanupSucceeded"]:
            raise RuntimeError("Owned cleanup incomplete; inspect receipt.json")


if __name__ == "__main__":
    main()
