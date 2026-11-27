#!/usr/bin/env python3
"""Verify the published catalog in disposable build storage, retaining reports.

Local: python3 scripts/verify-catalog-runtime.py
CI:    python3 scripts/verify-catalog-runtime.py --configuration Release
The complete build/preparation/test sequence owns one temporary workspace.
On failed runtime shutdown, runtime-custody in the reports directory retains
fixture ownership files for cleanup retry; compiled build outputs are removed.
"""

import argparse
from datetime import datetime, timezone
import fcntl
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tempfile
import time


ROOT = Path(__file__).resolve().parents[1]
PROJECT = "tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj"
TOPOLOGY = "Hexalith.Parties.IntegrationTests.Topology."
METHODS = (
    TOPOLOGY + "PartiesTopologyIsolationTests.ConcurrentSessions_IsolateStreamsRejectCrossCallsAndSurvivePeerDisposalAsync",
    TOPOLOGY + "PartiesTopologyCleanupTests.Cleanup_RechecksWorkerExitAfterSdkStopDisposesTheProvider",
    TOPOLOGY + "PartiesTopologyCleanupTests.OrdinaryCliWrapper_ExitingBeforeFirstPoll_LeavesItsRuntimeAdoptedThenTerminated",
)


def running_group(group):
    """Ignore exited zombies while checking owned Linux group members."""
    for directory in Path("/proc").iterdir():
        if not directory.name.isdigit():
            continue
        try:
            fields = (directory / "stat").read_text().rpartition(") ")[2].split()
            if len(fields) > 2 and int(fields[2]) == group and fields[0] not in ("Z", "X"):
                return True
        except (OSError, ValueError):
            continue
    return False


def stop_group(process):
    """Request managed cancellation, then quiesce remaining owned children."""
    try:
        os.killpg(process.pid, signal.SIGINT)
    except ProcessLookupError:
        pass
    try:
        process.wait(timeout=20)
    except subprocess.TimeoutExpired:
        pass
    # The leader may have exited while a descendant ignored cancellation.
    try:
        os.killpg(process.pid, signal.SIGKILL)
    except ProcessLookupError:
        pass
    process.wait()
    deadline = time.monotonic() + 5
    while running_group(process.pid):
        if time.monotonic() >= deadline:
            raise RuntimeError("Owned verification processes did not stop")
        time.sleep(0.02)


def execute(command, environment, log):
    """Keep command diagnostics and stop its process group before cleanup."""
    print(f"Running {log.name}; reports: {log.parent}", flush=True)
    with log.open("wb") as output:
        process = subprocess.Popen(
            command, cwd=ROOT, env=environment, stdout=output,
            stderr=subprocess.STDOUT, start_new_session=True,
        )
        try:
            status = process.wait()
        except BaseException:
            stop_group(process)
            raise
        if running_group(process.pid):
            stop_group(process)
    if status:
        raise subprocess.CalledProcessError(status, command)


def verify(configuration, results, *, reuse_build=False, artifacts=None):
    """Retain compact diagnostics after all consumers finish using the build."""
    workspace = None
    status = 0
    # Native fixtures deliberately retain these files when Docker/worker cleanup
    # fails. Their ownership markers and certificates must outlive build cleanup.
    custody = results / "runtime-custody"
    try:
        with tempfile.TemporaryDirectory(prefix="parties-catalog-verify-") as temporary:
            workspace = Path(temporary)
            build = (artifacts or ROOT / "artifacts") if reuse_build else workspace / "build"
            child_temporary = workspace / "tmp"
            child_temporary.mkdir()
            environment = dict(os.environ, TMPDIR=str(child_temporary),
                               TMP=str(child_temporary), TEMP=str(child_temporary))
            if not reuse_build:
                # Qualify the current EventStore SDK with the Tenants 8.x package cohort.
                # Keep one compatible EventStore SDK across build, preparation and test.
                environment.setdefault("HexalithEventStoreVersion", "5.5.2")
            environment["PARTIES_MTLS_TOPOLOGY_TESTS"] = "true"
            environment["PARTIES_TEST_REPOSITORY_ROOT"] = str(ROOT)
            receipts = build / "runtime-profile" / configuration
            environment["Parties__Testing__PreparedRuntimeManifest"] = str(receipts / "prepared.json")
            properties = ["-p:HexalithUsePublishedSdkForHostedTests=true",
                          "-p:CopyLocalLockFileAssemblies=true"]
            output = ["--configuration", configuration]
            if not reuse_build or artifacts is not None:
                output += ["--artifacts-path", str(build)]
            # Keep active jobs out of systemd's age-based cleanup.
            lock = os.open(workspace, os.O_RDONLY)
            try:
                fcntl.flock(lock, fcntl.LOCK_SH)
                try:
                    if not reuse_build:
                        execute(["dotnet", "build", PROJECT, *output, "--disable-build-servers",
                                 "-m:1", "-warnaserror", *properties], environment, results / "build.log")
                    preparation = ["bash", str(ROOT / "scripts/prepare-aspire-runtime-hosts.sh"),
                                   "--configuration", configuration]
                    if not reuse_build or artifacts is not None:
                        preparation += ["--artifacts-path", str(build)]
                    if reuse_build:
                        preparation += ["--reuse-build", "true"]
                    execute(preparation, environment, results / "prepare.log")
                    if not (receipts / "prepared.json").is_file():
                        raise RuntimeError("Runtime preparation did not produce prepared.json")
                    custody.mkdir(mode=0o700)
                    test_environment = dict(environment, TMPDIR=str(custody),
                                            TMP=str(custody), TEMP=str(custody))
                    execute(["dotnet", "test", "--project", PROJECT, *output, *properties,
                             "--no-build", "--no-restore", "--filter-method", *METHODS,
                             "--minimum-expected-tests", "4", "--fail-skips", "on",
                             "--report-xunit-trx", "--results-directory", str(results)],
                            test_environment, results / "test.log")
                finally:
                    # These are historical diagnostics, not a reusable prepared cohort:
                    # their temporary binary paths stop existing when this job exits.
                    if receipts.is_dir():
                        retained = results / "runtime-profile"
                        retained.mkdir()
                        for receipt in receipts.glob("*.json"):
                            if receipt.is_file() and not receipt.is_symlink():
                                shutil.copy2(receipt, retained / receipt.name)
            finally:
                os.close(lock)
    except BaseException as error:
        if isinstance(error, subprocess.CalledProcessError):
            status = error.returncode if error.returncode > 0 else 128 - error.returncode
        elif isinstance(error, SystemExit) and isinstance(error.code, int):
            status = error.code
        else:
            status = 1
        raise
    finally:
        if custody.is_dir() and not any(custody.iterdir()):
            custody.rmdir()
        (results / "verification.json").write_text(json.dumps({
            "configuration": configuration, "exitCode": status,
            "workspaceRemoved": workspace is None or not workspace.exists(),
            "runtimeCustodyRetained": custody.exists(),
            "reusedBuild": reuse_build,
            "retainedRuntimeProfile": ("prebuilt solution inputs retained" if reuse_build
                                       else "historical diagnostics; temporary binaries removed"),
        }, indent=2) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Debug")
    parser.add_argument("--results-directory", type=Path)
    parser.add_argument("--reuse-build", action="store_true",
                        help="Consume the successfully built standalone runtime graph without building")
    parser.add_argument("--artifacts-path", type=Path,
                        help="The exact SDK artifact root used by the solution build")
    args = parser.parse_args()
    results = args.results_directory or ROOT / "TestResults/catalog-runtime" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S.%fZ")
    results = results.resolve()
    results.mkdir(parents=True, exist_ok=True)
    owner = results / ".verification-owner"
    try:
        with owner.open("x") as ownership:
            ownership.write(str(os.getpid()) + "\n")
    except FileExistsError:
        parser.error(f"Results directory is already owned: {results}")
    if any(path != owner for path in results.iterdir()):
        owner.unlink()
        parser.error(f"Results directory must be empty: {results}")
    print(f"Verification reports: {results}", flush=True)

    def cancelled(number, _frame):
        signal.signal(signal.SIGINT, signal.SIG_IGN)
        signal.signal(signal.SIGTERM, signal.SIG_IGN)
        raise SystemExit(128 + number)

    for event in (signal.SIGINT, signal.SIGTERM):
        signal.signal(event, cancelled)
    try:
        if args.artifacts_path is not None and not args.reuse_build:
            parser.error("--artifacts-path requires --reuse-build")
        verify(args.configuration, results, reuse_build=args.reuse_build,
               artifacts=args.artifacts_path.resolve() if args.artifacts_path else None)
    except subprocess.CalledProcessError as error:
        print(f"Verification failed; inspect {results}", file=sys.stderr)
        return error.returncode if error.returncode > 0 else 128 - error.returncode
    except (OSError, RuntimeError) as error:
        print(f"Verification failed: {error}; inspect {results}", file=sys.stderr)
        return 1
    finally:
        owner.unlink(missing_ok=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
