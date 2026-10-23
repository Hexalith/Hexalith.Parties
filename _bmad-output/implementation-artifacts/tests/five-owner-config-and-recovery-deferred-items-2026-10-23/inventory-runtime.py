"""Run the isolated SDK inventory ACL control and always publish sanitized results."""
import argparse
import http.server
import json
import os
import pathlib
import signal
import socket
import subprocess
import tempfile
import threading
import time
import urllib.request
import uuid


class CommandFailure(RuntimeError):
    """Keep command diagnostics and credentials out of the public receipt."""

    def __init__(self, code=1):
        super().__init__("Owned command failed")
        self.code = code if 0 < code < 126 else 1


def stop_group(process):
    """Bound teardown of the owned session, including descendants after parent exit."""
    try:
        os.killpg(process.pid, signal.SIGTERM)
    except ProcessLookupError:
        pass
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        pass
    finally:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        process.wait(timeout=5)


def run_owned(command, *, timeout=30, check=True, **kwargs):
    """Run each command in its own group with a deadline and unconditional teardown."""
    with subprocess.Popen(command, start_new_session=True, **kwargs) as process:
        try:
            out, err = process.communicate(timeout=timeout)
            result = subprocess.CompletedProcess(command, process.returncode, out, err)
            if check and result.returncode:
                raise CommandFailure(result.returncode)
            return result
        finally:
            stop_group(process)


def run(*command, timeout=30, check=True):
    return run_owned(command, timeout=timeout, check=check, text=True,
                     stdout=subprocess.PIPE, stderr=subprocess.PIPE)


def port():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]


def execute_tests(args, config, receipt):
    log = args.output_directory / "inventory-runtime-tests.log"
    with log.open("w") as output:
        log.chmod(0o600)
        result = run_owned(
            ["dotnet", f"tests/Hexalith.Parties.Tests/bin/{args.configuration}/net10.0/Hexalith.Parties.Tests.dll",
             "-method", "*ActualSdkHealthDetectsInventoryPrefixAclDenialAndRepairInBothModes"],
            timeout=args.test_timeout, check=False, cwd=str(args.workspace.resolve()),
            env=dict(os.environ, PARTIES_MEMORIES_INVENTORY_ACL_CONFIG=str(config)),
            stdout=output, stderr=subprocess.STDOUT)
    receipt["sdkHealthTestExitCode"] = result.returncode
    if result.returncode:
        raise CommandFailure(result.returncode)


def qualify(args, receipt, owned):
    name = owned["name"]
    username = "parties-probe-" + uuid.uuid4().hex[:8]
    command = lambda *parts: run(*parts, timeout=args.command_timeout).stdout.strip()
    command("docker", "run", "--rm", "-d", "--name", name, "-p", "127.0.0.1::6379",
            "redis:7.4-alpine", "redis-server", "--save", "", "--appendonly", "no")
    redis_port = json.loads(command("docker", "inspect", name))[0]["NetworkSettings"]["Ports"]["6379/tcp"][0]["HostPort"]
    deadline = time.monotonic() + 25
    while True:
        try:
            if run("docker", "exec", name, "redis-cli", "ping",
                   timeout=min(args.command_timeout, max(.1, deadline - time.monotonic()))).stdout.strip() == "PONG":
                break
        except (CommandFailure, subprocess.TimeoutExpired):
            pass
        if time.monotonic() >= deadline:
            raise TimeoutError("Redis readiness deadline")
        time.sleep(.1)
    password = "synthetic-" + uuid.uuid4().hex
    command("docker", "exec", name, "redis-cli", "ACL", "SETUSER", username, "on", ">" + password,
            "~*parties:memory-mapping:*", "+@all")
    directory = pathlib.Path(owned["temporary"].name)
    (directory / "components").mkdir()
    (directory / "components/state.yaml").write_text(
        'apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: statestore\nspec:\n'
        '  type: state.redis\n  version: v1\n  metadata:\n'
        f'  - name: redisHost\n    value: "127.0.0.1:{redis_port}"\n'
        f'  - name: redisUsername\n    value: "{username}"\n'
        f'  - name: redisPassword\n    value: "{password}"\n'
        '  - name: enableTLS\n    value: "false"\n')
    http_port, grpc_port, internal_port = port(), port(), port()
    token = "synthetic-" + uuid.uuid4().hex
    owned["daprLog"] = (directory / "daprd.log").open("w")
    owned["process"] = subprocess.Popen(
        [str(args.daprd), "--app-id", name, "--dapr-http-port", str(http_port), "--dapr-grpc-port", str(grpc_port),
         "--dapr-internal-grpc-port", str(internal_port), "--resources-path", str(directory / "components"), "--enable-metrics=false"],
        start_new_session=True, env=dict(os.environ, DAPR_API_TOKEN=token), stdout=owned["daprLog"], stderr=subprocess.STDOUT)
    deadline = time.monotonic() + 25
    while True:
        try:
            request = urllib.request.Request(f"http://127.0.0.1:{http_port}/v1.0/healthz/outbound", headers={"dapr-api-token": token})
            with urllib.request.urlopen(request, timeout=.5) as response:
                if response.status == 204:
                    break
        except Exception:
            pass
        if owned["process"].poll() is not None or time.monotonic() >= deadline:
            raise TimeoutError("Dapr readiness deadline")
        time.sleep(.1)
    receipt["authenticatedNonDefaultAclUser"] = any(
        "user=" + username in line for line in command("docker", "exec", name, "redis-cli", "CLIENT", "LIST").splitlines())
    if not receipt["authenticatedNonDefaultAclUser"]:
        raise CommandFailure()

    class Repair(http.server.BaseHTTPRequestHandler):
        def do_GET(self):
            if self.path != "/grant-inventory":
                self.send_error(404)
                return
            try:
                command("docker", "exec", name, "redis-cli", "ACL", "SETUSER", username, "~*parties:memory-ingestion:*")
                receipt["inventoryPrefixGrantApplied"] = True
                self.send_response(204)
                self.end_headers()
            except Exception:
                self.send_error(500)

        def log_message(self, *arguments):
            pass

    controller = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Repair)
    controller.daemon_threads = True
    controller.block_on_close = False
    owned["controller"] = controller
    owned["thread"] = threading.Thread(target=controller.serve_forever, kwargs={"poll_interval": .1}, daemon=True)
    owned["thread"].start()
    config = directory / "private.json"
    config.write_text(json.dumps({"daprHttpEndpoint": f"http://127.0.0.1:{http_port}", "daprGrpcEndpoint": f"http://127.0.0.1:{grpc_port}",
                                 "daprApiToken": token, "stateStoreName": "statestore",
                                 "inventoryAclRepairEndpoint": f"http://127.0.0.1:{controller.server_port}/grant-inventory"}))
    config.chmod(0o600)
    execute_tests(args, config, receipt)
    receipt["allReservedSentinelsAbsent"] = not command("docker", "exec", name, "redis-cli", "KEYS", "*parties:memory-*")
    if not receipt["allReservedSentinelsAbsent"]:
        raise CommandFailure()
    receipt["daprdVersion"] = command(str(args.daprd), "--version")
    receipt["redisVersion"] = command("docker", "exec", name, "redis-cli", "INFO", "server").split("redis_version:")[1].splitlines()[0]


def finalize(args, receipt, owned):
    """Attempt every cleanup even when an earlier cleanup failed."""
    errors = []
    for key in ("controller", "process", "daprLog", "temporary"):
        resource = owned.get(key)
        if resource is None:
            continue
        try:
            if key == "controller":
                resource.shutdown()
                resource.server_close()
                owned["thread"].join(timeout=2)
            elif key == "process":
                stop_group(resource)
                receipt["ownedDaprStopped"] = resource.poll() is not None
            elif key == "daprLog":
                resource.close()
            else:
                resource.cleanup()
        except Exception as error:
            errors.append(type(error).__name__)
    try:
        removed = run("docker", "rm", "-f", owned["name"], timeout=args.command_timeout, check=False)
        inspected = run("docker", "inspect", owned["name"], timeout=args.command_timeout, check=False)
        receipt["ownedRedisRemoved"] = inspected.returncode != 0 and "no such object:" in (inspected.stderr or "").casefold()
        if not receipt["ownedRedisRemoved"]:
            errors.append("ContainerCleanupUnverified")
    except Exception as error:
        errors.append(type(error).__name__)
    receipt["cleanupErrors"] = errors
    return not errors


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", type=pathlib.Path, required=True)
    parser.add_argument("--output-directory", type=pathlib.Path, required=True)
    parser.add_argument("--daprd", type=pathlib.Path, default=pathlib.Path.home() / ".dapr/bin/daprd")
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Debug")
    parser.add_argument("--command-timeout", type=float, default=30)
    parser.add_argument("--test-timeout", type=float, default=90)
    args = parser.parse_args(argv)
    if args.command_timeout <= 0 or args.test_timeout <= 0:
        parser.error("Deadlines must be positive")
    args.output_directory.mkdir(parents=True, exist_ok=True)
    receipt = {"status": "failed"}
    owned = {"name": "parties-owner-inventory-" + uuid.uuid4().hex[:12]}
    code = 1
    try:
        owned["temporary"] = tempfile.TemporaryDirectory(prefix="parties-owner-inventory-")
        qualify(args, receipt, owned)
        code = 0
    except Exception as error:
        receipt["failureType"] = type(error).__name__
        code = error.code if isinstance(error, CommandFailure) else 1
    finally:
        try:
            cleaned = finalize(args, receipt, owned)
        except Exception as error:
            receipt["cleanupErrors"] = [type(error).__name__]
            cleaned = False
        if not cleaned and code == 0:
            code = 1
        receipt["status"] = "passed" if code == 0 else "failed"
        receipt["exitCode"] = code
        (args.output_directory / "inventory-runtime.json").write_text(json.dumps(receipt, indent=2) + "\n")
        print(json.dumps(receipt, indent=2))
    return code


if __name__ == "__main__":
    raise SystemExit(main())
