"""Run the real catalog driver with synthetic build and verification commands."""

import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest


SCRIPT = Path(__file__).with_name("verify-catalog-runtime.py")
FAKE = r'''
import json, os, pathlib, sys, time
phase = sys.argv[1]
arguments = sys.argv[2:]
root = pathlib.Path(os.environ['FIXTURE_ROOT'])
build = pathlib.Path(arguments[arguments.index('--artifacts-path') + 1])
configuration = arguments[arguments.index('--configuration') + 1]
assert pathlib.Path.cwd() == root
assert pathlib.Path(os.environ['PARTIES_TEST_REPOSITORY_ROOT']) == root
if phase != 'test' and not os.environ.get('REUSE_PREBUILT'):
    assert build.parent == pathlib.Path(os.environ['TMPDIR']).parent
if phase in ('build', 'test'):
    assert '-p:HexalithUsePublishedSdkForHostedTests=true' in arguments
    assert '-p:CopyLocalLockFileAssemblies=true' in arguments
assert os.environ['TMPDIR'] == os.environ['TMP'] == os.environ['TEMP']
receipt = build / 'runtime-profile' / configuration / 'prepared.json'
assert os.environ['Parties__Testing__PreparedRuntimeManifest'] == str(receipt)
assert os.environ['PARTIES_MTLS_TOPOLOGY_TESTS'] == 'true'
with (root / 'calls.jsonl').open('a') as log:
    log.write(json.dumps({'phase': phase, 'arguments': arguments, 'build': str(build), 'pid': os.getpid(),
                          'sdk': os.environ.get('HexalithEventStoreVersion')}) + chr(10))
print('Synthetic diagnostic for ' + phase, flush=True)
if phase == 'build':
    (build / 'bin').mkdir(parents=True)
    (build / 'bin' / 'generated.dll').write_bytes(b'generated')
elif phase == 'prepare':
    receipt.parent.mkdir(parents=True, exist_ok=True)
    (receipt.parent / 'host.json').write_text('{"host":"observed"}')
    archives = receipt.parent / 'public-archives'
    archives.mkdir()
    (archives / 'package.nupkg').write_bytes(b'disposable archive')
    if not os.environ.get('NO_MANIFEST'):
        receipt.write_text('{"prepared":true}')
elif phase == 'test':
    assert receipt.is_file() and (build / 'bin' / 'generated.dll').is_file()
    assert arguments[arguments.index('--minimum-expected-tests') + 1] == '4'
    assert arguments[arguments.index('--fail-skips') + 1] == 'on'
    assert '--no-build' in arguments and '--no-restore' in arguments
    methods = arguments[arguments.index('--filter-method') + 1:arguments.index('--minimum-expected-tests')]
    assert [method.rsplit('.', 1)[-1] for method in methods] == [
        'ConcurrentSessions_IsolateStreamsRejectCrossCallsAndSurvivePeerDisposalAsync',
        'Cleanup_RechecksWorkerExitAfterSdkStopDisposesTheProvider',
        'OrdinaryCliWrapper_ExitingBeforeFirstPoll_LeavesItsRuntimeAdoptedThenTerminated',
    ]
    results = pathlib.Path(arguments[arguments.index('--results-directory') + 1])
    assert os.environ.get('REUSE_PREBUILT') or not results.is_relative_to(build.parent)
    assert pathlib.Path(os.environ['TMPDIR']) == results / 'runtime-custody'
    # SDK11 MTP actually starts the test process in its external output folder.
    os.chdir(build / 'bin')
    assert pathlib.Path(os.environ['PARTIES_TEST_REPOSITORY_ROOT']) != pathlib.Path.cwd()
    (results / 'runtime.trx').write_text('<TestRun/>')
    if os.environ.get('WAIT_PHASE') == phase or os.environ.get('FAIL_PHASE') == phase:
        custody = pathlib.Path(os.environ['TMPDIR']) / 'owned-session'
        custody.mkdir()
        (custody / '.owned-control-plane-network').write_text('owned-network')
        (custody / 'ca.pem').write_text('synthetic certificate custody')
if os.environ.get('WAIT_PHASE') == phase:
    descendant = None
    if os.environ.get('IGNORE_TERM_DESCENDANT'):
        import subprocess
        child = subprocess.Popen([sys.executable, '-u', '-c',
            "import signal,time;signal.signal(signal.SIGINT,signal.SIG_IGN);signal.signal(signal.SIGTERM,signal.SIG_IGN);print('ready',flush=True);time.sleep(60)"],
            stdout=subprocess.PIPE, text=True)
        assert child.stdout.readline().strip() == 'ready'
        descendant = child.pid
    (root / 'ready.json').write_text(json.dumps({'pid': os.getpid(), 'descendant': descendant, 'workspace': str(build.parent)}))
    time.sleep(60)
if os.environ.get('FAIL_PHASE') == phase:
    sys.exit(23)
'''


@unittest.skipUnless(sys.platform == "linux", "Runtime verification owns Linux process groups")
class VerifyCatalogRuntimeTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="catalog-driver-test-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name) / "parties source"
        self.scripts = self.root / "scripts"
        self.scripts.mkdir(parents=True)
        self.runner = self.scripts / SCRIPT.name
        shutil.copy2(SCRIPT, self.runner)
        self.fake = self.root / "fake.py"
        self.fake.write_text(FAKE)
        binaries = self.root / "tools"
        binaries.mkdir()
        dotnet = binaries / "dotnet"
        dotnet.write_text(f"#!{sys.executable}\n" + FAKE)
        dotnet.chmod(0o755)
        (self.scripts / "prepare-aspire-runtime-hosts.sh").write_text(
            '#!/usr/bin/env bash\nexec "$FAKE_PYTHON" "$FAKE_TOOL" prepare "$@"\n'
        )
        self.results = self.root / "TestResults" / "verification"
        self.scratch = self.root / "scratch"
        self.scratch.mkdir()
        self.environment = dict(
            os.environ, PATH=str(binaries) + os.pathsep + os.environ["PATH"],
            FIXTURE_ROOT=str(self.root), FAKE_PYTHON=sys.executable,
            FAKE_TOOL=str(self.fake), TMPDIR=str(self.scratch),
        )
        self.environment.pop("HexalithEventStoreVersion", None)

    def command(self):
        return [sys.executable, "-B", str(self.runner), "--results-directory", str(self.results)]

    def run_driver(self, **environment):
        return subprocess.run(
            self.command(), env=dict(self.environment, **environment),
            capture_output=True, text=True, timeout=20,
        )

    def test_release_keeps_same_routing_and_required_published_properties(self):
        result = subprocess.run(
            self.command() + ["--configuration", "Release"],
            env=self.environment, capture_output=True, text=True, timeout=20,
        )
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        for call in self.calls():
            self.assertIn("Release", call["arguments"])
            self.assertEqual("5.4.0", call["sdk"])
        self.assert_workspace_removed()

    def test_preserves_an_explicit_runtime_sdk(self):
        result = self.run_driver(HexalithEventStoreVersion="4.2.0")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual(["4.2.0"] * 3, [call["sdk"] for call in self.calls()])

    def test_reuses_prebuilt_outputs_without_building_and_preserves_their_lifetime(self):
        build = self.root / "prebuilt"
        (build / "bin").mkdir(parents=True)
        binary = build / "bin" / "generated.dll"
        binary.write_bytes(b"generated")
        result = subprocess.run(
            self.command() + ["--configuration", "Release", "--reuse-build", "--artifacts-path", str(build)],
            env=dict(self.environment, REUSE_PREBUILT="true"), capture_output=True, text=True, timeout=20,
        )
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        calls = self.calls()
        self.assertEqual(["prepare", "test"], [call["phase"] for call in calls])
        self.assertEqual([None, None], [call["sdk"] for call in calls])
        self.assertIn("--reuse-build", calls[0]["arguments"])
        self.assertEqual(b"generated", binary.read_bytes())
        self.assertTrue(json.loads((self.results / "verification.json").read_text())["reusedBuild"])
        self.assertEqual([], list(self.scratch.iterdir()))

    def calls(self):
        return [json.loads(line) for line in (self.root / "calls.jsonl").read_text().splitlines()]

    def assert_workspace_removed(self):
        self.assertEqual([], list(self.scratch.iterdir()))
        for call in self.calls():
            self.assertFalse(Path(call["build"]).parent.exists())
        self.assertTrue(json.loads((self.results / "verification.json").read_text())["workspaceRemoved"])

    def test_complete_sequence_shares_workspace_and_retains_only_reports(self):
        result = self.run_driver()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        calls = self.calls()
        self.assertEqual(["build", "prepare", "test"], [call["phase"] for call in calls])
        self.assertEqual(1, len({call["build"] for call in calls}))
        self.assertIn("Debug", calls[0]["arguments"])
        self.assertTrue((self.results / "runtime.trx").is_file())
        self.assertEqual({"prepared.json", "host.json"}, {p.name for p in (self.results / "runtime-profile").iterdir()})
        self.assertFalse(list(self.results.rglob("*.dll")))
        self.assertFalse(list(self.results.rglob("*.nupkg")))
        self.assertFalse((self.results / "runtime-custody").exists())
        self.assert_workspace_removed()

    def test_each_failed_stage_preserves_status_and_partial_diagnostics(self):
        for phase in ("build", "prepare", "test"):
            with self.subTest(phase=phase):
                if self.results.exists():
                    shutil.rmtree(self.results)
                (self.root / "calls.jsonl").unlink(missing_ok=True)
                result = self.run_driver(FAIL_PHASE=phase)
                self.assertEqual(23, result.returncode, result.stdout + result.stderr)
                self.assertIn("Synthetic diagnostic", (self.results / f"{phase}.log").read_text())
                self.assertEqual(23, json.loads((self.results / "verification.json").read_text())["exitCode"])
                if phase != "build":
                    self.assertTrue((self.results / "runtime-profile" / "host.json").is_file())
                if phase == "test":
                    self.assertEqual("owned-network", (self.results / "runtime-custody/owned-session/.owned-control-plane-network").read_text())
                self.assert_workspace_removed()

    def test_missing_prepared_manifest_refuses_test_execution(self):
        result = self.run_driver(NO_MANIFEST="1")
        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertEqual(["build", "prepare"], [call["phase"] for call in self.calls()])
        self.assertIn("prepared.json", result.stderr)
        self.assert_workspace_removed()

    def test_cancellation_stops_child_retains_receipts_and_removes_workspace(self):
        process = subprocess.Popen(
            self.command(), env=dict(self.environment, WAIT_PHASE="test", IGNORE_TERM_DESCENDANT="1"),
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, start_new_session=True,
        )
        try:
            deadline = time.monotonic() + 10
            while not (self.root / "ready.json").exists() and process.poll() is None:
                if time.monotonic() >= deadline:
                    self.fail("Synthetic test process did not become ready")
                time.sleep(0.01)
            self.assertTrue((self.root / "ready.json").is_file())
            child = json.loads((self.root / "ready.json").read_text())
            process.send_signal(signal.SIGTERM)
            output, error = process.communicate(timeout=10)
            self.assertEqual(143, process.returncode, output + error)
            with self.assertRaises(ProcessLookupError):
                os.kill(child["pid"], 0)
            descendant = Path(f"/proc/{child['descendant']}/stat")
            if descendant.exists():
                self.assertIn(descendant.read_text().rpartition(") ")[2].split()[0], ("Z", "X"))
            self.assertEqual(143, json.loads((self.results / "verification.json").read_text())["exitCode"])
            self.assertTrue((self.results / "runtime-profile" / "prepared.json").is_file())
            self.assertTrue((self.results / "runtime-custody/owned-session/ca.pem").is_file())
            self.assert_workspace_removed()
        finally:
            if process.poll() is None:
                process.kill()
                process.communicate(timeout=10)

    def test_existing_results_are_preserved_and_not_reused(self):
        self.results.mkdir(parents=True)
        preserved = self.results / "previous.trx"
        preserved.write_text("previous result")
        result = self.run_driver()
        self.assertEqual(2, result.returncode)
        self.assertEqual("previous result", preserved.read_text())
        self.assertFalse((self.root / "calls.jsonl").exists())

    def test_empty_results_directory_is_exclusively_owned_before_logs_exist(self):
        code = """
import os,pathlib,runpy,sys,time
script, results, ready = sys.argv[1:]
module = runpy.run_path(script)
def hold(*args, **kwargs):
    pathlib.Path(ready).write_text('owned')
    time.sleep(60)
module['main'].__globals__['verify'] = hold
sys.argv = [script, '--results-directory', results]
sys.exit(module['main']())
"""
        ready = self.root / "exclusive-ready"
        process = subprocess.Popen(
            [sys.executable, "-c", code, str(self.runner), str(self.results), str(ready)],
            env=self.environment, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
        )
        try:
            deadline = time.monotonic() + 10
            while not ready.exists() and process.poll() is None:
                if time.monotonic() >= deadline:
                    self.fail("First verifier did not reserve its reports directory")
                time.sleep(0.01)
            self.assertTrue(ready.exists())
            result = self.run_driver()
            self.assertEqual(2, result.returncode, result.stdout + result.stderr)
            self.assertIn("already owned", result.stderr)
            self.assertFalse((self.root / "calls.jsonl").exists())
            process.terminate()
            process.communicate(timeout=10)
            self.assertFalse((self.results / ".verification-owner").exists())
        finally:
            if process.poll() is None:
                process.kill()
                process.communicate(timeout=10)
