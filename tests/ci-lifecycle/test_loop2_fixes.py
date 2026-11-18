"""Local process and workflow controls for the Story 2.8 loop-2 fixes; none are hosted qualification evidence."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import signal
import subprocess  # nosec B404 -- fixed interpreter, bash and this test's temporary fixtures only.
import sys
import tempfile
import time
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / 'scripts' / 'qualify-disposable-ci.py'
SPEC = importlib.util.spec_from_file_location('loop2_qualification', SCRIPT)
Q = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(Q)
LIFECYCLE = ROOT / '.github/workflows/ci-lifecycle-qualification.yml'
STARTUP = ROOT / '.github/workflows/current-owner-startup-diagnostics.yml'
READER = '${{ runner.temp }}/hexalith-lifecycle-registry.startup-reader'

# The gated launch leader is the SDK muxer: it ignores SIGINT and waits for the tool it runs.
MUXER = r'''
import signal, subprocess, sys
signal.signal(signal.SIGINT, lambda number, frame: None)
sys.exit(subprocess.call([sys.executable, '-B', *sys.argv[1:]]))
'''
# The tool composes an "AppHost" in its own process group and records withdrawal before stopping it.
TOOL = r'''
import os, pathlib, signal, subprocess, sys, time
root = pathlib.Path(sys.argv[1])
host = subprocess.Popen([sys.executable, '-B', str(root / 'apphost.py'), str(root)])
def cancel(number, frame):
    time.sleep(0.3)  # The withdrawal commit takes time; anything signalled together with the tool sees no mark yet.
    (root / 'withdrawn').write_text(str(number))
    host.terminate()
    host.wait(timeout=20)
    sys.exit(130)
signal.signal(signal.SIGINT, cancel)
signal.signal(signal.SIGTERM, cancel)
while not (root / 'apphost-ready').exists():
    time.sleep(0.01)
(root / 'tool-ready').write_text(str(os.getpid()))
while True:
    time.sleep(0.05)
'''
# Like the AppHost's default host lifetime, it stops on SIGINT or SIGTERM; it records whether withdrawal came first.
APPHOST = r'''
import json, os, pathlib, signal, sys, time
root = pathlib.Path(sys.argv[1])
def observed(number, frame):
    with (root / 'apphost-signals').open('a') as stream:
        stream.write(json.dumps({'signal': number, 'withdrawn': (root / 'withdrawn').exists()}) + '\n')
    sys.exit(0)
signal.signal(signal.SIGINT, observed)
signal.signal(signal.SIGTERM, observed)
(root / 'apphost-ready').write_text(str(os.getpid()))
while True:
    time.sleep(0.05)
'''
SIGNAL_PROBE = """#!/bin/sh
trap 'printf "%s\\n" "$*" > "$SIGNAL_MARKER"; exit 1' INT TERM
: > "$STARTED_MARKER"
while :; do sleep 0.05; done
"""


def wait_for(path, seconds=20):
    deadline = time.monotonic() + seconds
    while not path.exists():
        if time.monotonic() > deadline:
            raise AssertionError('Fixture checkpoint never appeared: ' + path.name)
        time.sleep(0.02)


class ToolOnlySignalTests(unittest.TestCase):
    """Q's interrupt reaches only the tool's own processes; the composed AppHost is stopped by the tool after withdrawal."""

    def launch(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        for name, body in (('muxer.py', MUXER), ('tool.py', TOOL), ('apphost.py', APPHOST)):
            (root / name).write_text(body)
        leader = subprocess.Popen([sys.executable, '-B', str(root / 'muxer.py'), str(root / 'tool.py'), str(root)],  # nosec B603
                                  stdin=subprocess.DEVNULL, start_new_session=True)
        self.addCleanup(self.stop, leader)
        wait_for(root / 'tool-ready')
        return root, leader, {'pid': leader.pid, 'birth': Q.birth(leader.pid), 'processGroup': leader.pid,
                              'receipt': str(root / 'no-receipt.json')}

    @staticmethod
    def stop(leader):
        if leader.poll() is None:
            try:
                os.killpg(leader.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            leader.wait(timeout=10)

    @staticmethod
    def signals(root):
        return [json.loads(line) for line in (root / 'apphost-signals').read_text().splitlines()]

    def test_interrupt_reaches_only_the_tool_and_no_signal_precedes_withdrawal(self):
        root, leader, item = self.launch()
        tool = int((root / 'tool-ready').read_text())
        host = int((root / 'apphost-ready').read_text())
        self.assertEqual(set(Q.tool_processes(item['pid'], item['birth'])), {leader.pid, tool})
        controller = object.__new__(Q.Qualification)
        observed = []
        controller.observe = lambda kind, **values: observed.append((kind, values))
        controller.process_signal(item, signal.SIGINT)
        self.assertEqual(leader.wait(timeout=30), 130)
        self.assertEqual(self.signals(root), [{'signal': signal.SIGTERM, 'withdrawn': True}])
        self.assertEqual(observed[0][0], 'owned-process-signal')
        self.assertEqual(observed[0][1]['toolProcessIds'], sorted([leader.pid, tool]))
        self.assertNotIn(host, observed[0][1]['toolProcessIds'])

    def test_control_a_whole_group_interrupt_reaches_the_apphost_before_withdrawal(self):
        # Proves the fixture detects the defect that the tool-only interrupt removes.
        root, leader, _ = self.launch()
        os.killpg(leader.pid, signal.SIGINT)
        leader.wait(timeout=30)
        self.assertEqual(self.signals(root)[0], {'signal': signal.SIGINT, 'withdrawn': False})

    def test_reused_or_exited_leader_has_no_tool_processes(self):
        _, leader, item = self.launch()
        self.assertEqual(Q.tool_processes(item['pid'], 'reused-birth'), {})
        with patch.object(Q.os, 'kill') as kill:
            self.assertEqual(Q.signal_tool_processes({item['pid']: 'reused-birth'}, signal.SIGINT), [])
            kill.assert_not_called()
        self.stop(leader)
        self.assertEqual(Q.tool_processes(item['pid'], item['birth']), {})

    def test_partial_startup_kills_only_the_tool_and_resumes_the_apphost_before_the_exact_down(self):
        controller = object.__new__(Q.Qualification)
        calls = []
        item = {'label': 'partial'}
        child = Mock(returncode=-signal.SIGKILL)
        controller.children = {'partial': child}
        controller.start = lambda label, startup=False: item
        controller.checkpoint = lambda *arguments: None
        controller.issued = lambda value: {'runId': 'a' * 32}
        controller.observe = lambda kind, **values: None
        controller.evidence = Path(tempfile.gettempdir())
        controller.process_signal = lambda value, number: calls.append(('signal', number))
        controller.resume_host = lambda value: calls.append(('resume',))
        controller.down = lambda value, tag, **options: calls.append(('down', tag))
        controller.cleanup_outcome = lambda value, checkpoint, required=False: {
            'terminalStatus': 'cancelled', 'terminalOutcome': {'exitCode': 'Cancelled'}}
        # Replacing HELPER itself keeps its lazy loader from importing the Builds submodule, which may be absent.
        with patch.object(Q, 'private_directory', side_effect=lambda path: path), patch.object(Q, 'HELPER', Mock()):
            controller.partial_startup()
        self.assertEqual(calls[:3], [('signal', signal.SIGKILL), ('resume',), ('down', 'partial-startup-recovery')])


class StableProcessHandleTests(unittest.TestCase):
    """Real process exits cannot redirect an admitted signal to a replacement process."""

    def launch(self):
        child = subprocess.Popen([sys.executable, '-B', '-c', 'import sys; sys.stdin.buffer.read()'],  # nosec B603
                                 stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        self.addCleanup(self.stop, child)
        self.assertIsNone(child.poll())
        return child

    @staticmethod
    def stop(child):
        if child.poll() is None:
            child.kill()
        child.wait(timeout=10)
        child.stdin.close()

    def assert_closed(self, opened):
        self.assertEqual(len(opened), 1)
        with self.assertRaises(OSError):
            os.fstat(opened[0])

    @staticmethod
    def capture_open(opened):
        original = os.pidfd_open

        def capture(pid):
            descriptor = original(pid)
            opened.append(descriptor)
            return descriptor

        return capture

    def test_owned_process_receives_signal_through_a_closed_afterward_handle(self):
        child = self.launch()
        born = Q.birth(child.pid)
        opened = []
        with patch.object(Q.os, 'pidfd_open', side_effect=self.capture_open(opened)), \
                patch.object(Q.os, 'kill') as numeric:
            self.assertEqual(Q.signal_tool_processes({child.pid: born}, signal.SIGTERM), [child.pid])
            numeric.assert_not_called()
        self.assertEqual(child.wait(timeout=10), -signal.SIGTERM)
        self.assert_closed(opened)

    def test_foreign_birth_is_refused_after_open_and_closes_the_handle(self):
        child = self.launch()
        opened = []
        with patch.object(Q.os, 'pidfd_open', side_effect=self.capture_open(opened)), \
                patch.object(Q.signal, 'pidfd_send_signal') as send, patch.object(Q.os, 'kill') as numeric:
            self.assertEqual(Q.signal_tool_processes({child.pid: 'foreign-birth'}, signal.SIGTERM), [])
            send.assert_not_called()
            numeric.assert_not_called()
        self.assertIsNone(child.poll())
        self.assert_closed(opened)

    def test_exit_after_identity_check_keeps_signal_bound_to_the_dead_process(self):
        original = self.launch()
        born = Q.birth(original.pid)
        opened = []
        replacements = []
        send = signal.pidfd_send_signal

        def replace_after_check(pid):
            self.assertEqual(pid, original.pid)
            self.assertEqual(Q.os.readlink('/proc/self/fd/' + str(opened[0])), 'anon_inode:[pidfd]')
            # The admitted process really exits after the handle is held. A new
            # unrelated process must survive the attempted delivery to that handle.
            send(opened[0], signal.SIGKILL)
            original.wait(timeout=10)
            replacements.append(self.launch())
            return born

        with patch.object(Q.os, 'pidfd_open', side_effect=self.capture_open(opened)), \
                patch.object(Q, 'birth', side_effect=replace_after_check), patch.object(Q.os, 'kill') as numeric:
            self.assertEqual(Q.signal_tool_processes({original.pid: born}, signal.SIGTERM), [])
            numeric.assert_not_called()
        self.assertEqual(original.returncode, -signal.SIGKILL)
        self.assertEqual(len(replacements), 1)
        self.assertIsNone(replacements[0].poll())
        self.assert_closed(opened)

    def test_process_exited_before_open_has_no_signal_or_numeric_fallback(self):
        child = self.launch()
        born = Q.birth(child.pid)
        self.stop(child)
        with patch.object(Q.signal, 'pidfd_send_signal') as send, patch.object(Q.os, 'kill') as numeric:
            self.assertEqual(Q.signal_tool_processes({child.pid: born}, signal.SIGTERM), [])
            send.assert_not_called()
            numeric.assert_not_called()

    def test_signal_failure_closes_handle_without_numeric_fallback(self):
        child = self.launch()
        born = Q.birth(child.pid)
        opened = []
        with patch.object(Q.os, 'pidfd_open', side_effect=self.capture_open(opened)), \
                patch.object(Q.signal, 'pidfd_send_signal', side_effect=PermissionError('refused')), \
                patch.object(Q.os, 'kill') as numeric:
            with self.assertRaises(PermissionError):
                Q.signal_tool_processes({child.pid: born}, signal.SIGTERM)
            numeric.assert_not_called()
        self.assertIsNone(child.poll())
        self.assert_closed(opened)


def step(workflow, name):
    return workflow.split('      - name: ' + name + '\n', 1)[1].split('\n      - ', 1)[0]


def run_block(text):
    """Return the literal run block, which ends at the first line outside its ten-space indentation."""
    lines = []
    for line in text.split('        run: |\n', 1)[1].splitlines():
        if line.strip() and not line.startswith(' ' * 10):
            break
        lines.append(line[10:])
    return '\n'.join(lines) + '\n'


CURL = """#!/bin/sh
url=""
out=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    -o) out="$2"; shift 2 ;;
    https://*) url="$1"; shift ;;
    *) shift ;;
  esac
done
printf '%s\\n' "$url" >> "$FIXTURE_LOG/curl"
printf 'archive:%s' "$url" > "$out"
"""
TAR = """#!/bin/sh
for last; do :; done
printf '%s\\n' "$last" >> "$FIXTURE_LOG/tar"
"""
DOTNET = """#!/bin/sh
printf '%s\\n' "$*" >> "$FIXTURE_LOG/dotnet"
"""
CLI_URL = 'https://github.com/dapr/cli/releases/download/v1.18.2/dapr_linux_amd64.tar.gz'
RUNTIME_URL = 'https://github.com/dapr/dapr/releases/download/v1.18.7/{}_linux_amd64.tar.gz'


def digest(url):
    return hashlib.sha256(('archive:' + url).encode()).hexdigest()


PINNED = {'CI_DAPR_CLI_SHA256': digest(CLI_URL), 'CI_DAPRD_SHA256': digest(RUNTIME_URL.format('daprd')),
          'CI_PLACEMENT_SHA256': digest(RUNTIME_URL.format('placement')),
          'CI_SCHEDULER_SHA256': digest(RUNTIME_URL.format('scheduler'))}


class DaprInstallExecutionTests(unittest.TestCase):
    """The real install-step bodies run under bash with stubbed downloads; a refused archive is never extracted."""

    def install(self, body, shell, changes):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            tools = root / 'bin'
            tools.mkdir()
            for name, script in (('curl', CURL), ('tar', TAR), ('dotnet', DOTNET)):
                (tools / name).write_text(script)
                (tools / name).chmod(0o700)
            (root / 'log').mkdir()
            (root / 'runner').mkdir()
            (root / 'step.sh').write_text(body)
            environment = {key: value for key, value in os.environ.items() if not key.startswith('CI_')}
            environment.update(PATH=str(tools) + os.pathsep + os.environ['PATH'], FIXTURE_LOG=str(root / 'log'),
                               RUNNER_TEMP=str(root / 'runner'), GITHUB_PATH=str(root / 'github-path'),
                               GITHUB_ENV=str(root / 'github-env'), CI_ASPIRE_VERSION='13.6.4',
                               CI_DAPR_CLI_VERSION='1.18.2', CI_DAPR_RUNTIME_VERSION='1.18.7')
            for key, value in {**PINNED, **changes}.items():
                if value is None:
                    environment.pop(key, None)
                else:
                    environment[key] = value
            completed = subprocess.run([*shell, str(root / 'step.sh')], cwd=root, env=environment,  # nosec B603
                                       capture_output=True, text=True, timeout=60, check=False)
            extracted = (root / 'log/tar').read_text().split() if (root / 'log/tar').exists() else []
            return completed, extracted

    def check(self, body, shell):
        swapped = {'CI_DAPRD_SHA256': PINNED['CI_PLACEMENT_SHA256'], 'CI_PLACEMENT_SHA256': PINNED['CI_DAPRD_SHA256']}
        cases = {
            'verified': ({}, True, ['dapr', 'daprd', 'placement', 'scheduler']),
            'cli-mismatch': ({'CI_DAPR_CLI_SHA256': 'a' * 64}, False, []),
            'runtime-mismatch': ({'CI_PLACEMENT_SHA256': 'b' * 64}, False, ['dapr', 'daprd']),
            'runtime-missing': ({'CI_SCHEDULER_SHA256': None}, False, ['dapr', 'daprd', 'placement']),
            'runtime-malformed': ({'CI_DAPRD_SHA256': 'not-a-digest'}, False, ['dapr']),
            'swapped-variables': (swapped, False, ['dapr']),
        }
        for name, (changes, accepted, extracted) in cases.items():
            with self.subTest(name=name):
                completed, actual = self.install(body, shell, changes)
                self.assertEqual(completed.returncode == 0, accepted, completed.stderr)
                self.assertEqual(actual, extracted)

    def test_lifecycle_install_step_extracts_only_verified_archives(self):
        body = run_block(step(LIFECYCLE.read_text(), 'Install toolchain without provisioning services'))
        self.check(body, ['bash', '-e'])

    def test_startup_diagnostics_install_step_extracts_only_verified_archives(self):
        body = run_block(step(STARTUP.read_text(), 'Prepare integration binaries'))
        self.check(body, ['bash', '--noprofile', '--norc', '-eo', 'pipefail'])


READER_CREDENTIALS = {'REGISTRY_READER_USERNAME': 'fake-reader', 'REGISTRY_READER_PASSWORD': 'fake-reader-password'}
READER_CONFIG = {'auths': {'registry.hexalith.com': {'auth': 'ZmFrZS1yZWFkZXI6ZmFrZS1yZWFkZXItcGFzc3dvcmQ='}}}


class PullReaderConfigurationExecutionTests(unittest.TestCase):
    """The real reader-step bodies run under bash with fake credentials; only an exact private config is written."""

    def configure(self, workflow, outside=False, **changes):
        text = step(workflow, 'Configure only the dedicated pull reader')
        name = re.search(r'HEXALITH_LIFECYCLE_REGISTRY_CONFIG: \$\{\{ runner\.temp \}\}/(\S+)\n', text).group(1)
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            for folder in ('runner', 'outside'):
                (root / folder).mkdir()
            directory = root / ('outside' if outside else 'runner') / name
            (root / 'step.sh').write_text(run_block(text))
            environment = {**os.environ, **READER_CREDENTIALS, 'RUNNER_TEMP': str(root / 'runner'),
                           'HEXALITH_LIFECYCLE_REGISTRY_CONFIG': str(directory), **changes}
            # With no umask, the observed modes are only the ones the step itself requests.
            completed = subprocess.run(['bash', '-e', str(root / 'step.sh')], cwd=root, env=environment, umask=0,  # nosec B603
                                       capture_output=True, text=True, timeout=60, check=False)
            config = directory / 'config.json'
            written = (json.loads(config.read_text()), config.stat().st_mode & 0o7777,
                       directory.stat().st_mode & 0o7777) if config.exists() else None
            return completed, written, directory.exists()

    def check(self, workflow):
        completed, written, _ = self.configure(workflow)
        self.assertEqual(completed.returncode, 0, completed.stderr)
        self.assertEqual(written, (READER_CONFIG, 0o600, 0o700))
        refusals = {
            'empty-username': (False, {'REGISTRY_READER_USERNAME': ''}, 'Dedicated pull-only reader is required'),
            'empty-password': (False, {'REGISTRY_READER_PASSWORD': ''}, 'Dedicated pull-only reader is required'),
            'outside-runner-temp': (True, {}, 'Refusing a reader directory outside the job temporary root'),
        }
        for name, (outside, changes, message) in refusals.items():
            with self.subTest(name=name):
                completed, written, created = self.configure(workflow, outside, **changes)
                self.assertNotEqual(completed.returncode, 0)
                self.assertIn(message, completed.stderr)
                self.assertIsNone(written)
                self.assertFalse(created)

    def test_lifecycle_reader_step_writes_only_the_exact_private_config(self):
        self.check(LIFECYCLE.read_text())

    def test_startup_diagnostics_reader_step_writes_only_the_exact_private_config(self):
        self.check(STARTUP.read_text())


class StartupDiagnosticsWorkflowTests(unittest.TestCase):
    def setUp(self):
        self.workflow = STARTUP.read_text()

    def test_toolchain_versions_and_digests_refuse_before_any_download(self):
        body = step(self.workflow, 'Install integration toolchain without starting services')
        script = '\n'.join(line[10:] for line in body.split("python3 - <<'PYTHON'\n", 1)[1].split('\n          PYTHON', 1)[0].splitlines())
        catalog = ('<Project><PropertyGroup><HexalithAspireAppHostSdkVersion>13.6.4</HexalithAspireAppHostSdkVersion>'
                   '<HexalithDaprRuntimeVersion>1.18.7</HexalithDaprRuntimeVersion><HexalithDaprCliVersion>1.18.2</HexalithDaprCliVersion>'
                   '</PropertyGroup></Project>')
        digests = {'schema': 'hexalith.builds.dapr-toolchain-sha256.v1',
                   'cli': {'1.18.2': {'dapr_linux_amd64.tar.gz': 'a' * 64}},
                   'runtime': {'1.18.7': {'daprd_linux_amd64.tar.gz': 'b' * 64, 'placement_linux_amd64.tar.gz': 'c' * 64,
                                          'scheduler_linux_amd64.tar.gz': 'd' * 64}}}
        cases = {
            'pinned': (catalog, digests, True),
            'no-digest-file': (catalog, None, False),
            'other-schema': (catalog, {**digests, 'schema': 'other'}, False),
            'missing-runtime-digest': (catalog, {**digests, 'runtime': {'1.18.7': {'daprd_linux_amd64.tar.gz': 'b' * 64}}}, False),
            'other-cli-version': (catalog, {**digests, 'cli': {'1.18.1': {'dapr_linux_amd64.tar.gz': 'a' * 64}}}, False),
            'malformed-digest': (catalog, {**digests, 'cli': {'1.18.2': {'dapr_linux_amd64.tar.gz': 'not-a-digest'}}}, False),
            'missing-catalog-property': (catalog.replace('<HexalithDaprCliVersion>1.18.2</HexalithDaprCliVersion>', ''), digests, False),
        }
        for name, (props, document, accepted) in cases.items():
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                (root / 'references/Hexalith.Builds/Props').mkdir(parents=True)
                (root / 'references/Hexalith.Builds/Props/Directory.Packages.props').write_text(props)
                if document is not None:
                    (root / 'references/Hexalith.Builds/Props/dapr-toolchain-sha256.json').write_text(json.dumps(document))
                environment = root / 'github-env'
                environment.write_text('')
                completed = subprocess.run([sys.executable, '-I', '-c', script], cwd=root, capture_output=True, text=True,  # nosec B603
                                           env={**os.environ, 'GITHUB_ENV': str(environment)}, timeout=60, check=False)
                self.assertEqual(completed.returncode == 0, accepted, completed.stderr)
                exported = environment.read_text()
                if accepted:
                    self.assertIn('CI_DAPR_CLI_VERSION=1.18.2', exported)
                    self.assertIn('CI_DAPRD_SHA256=' + 'b' * 64, exported)
                    self.assertIn('CI_SCHEDULER_SHA256=' + 'd' * 64, exported)
                else:
                    self.assertEqual(exported, '')

    def test_registry_reader_reaches_only_the_observer_and_is_never_exported(self):
        self.assertNotIn('DOCKER_CONFIG=', self.workflow)
        self.assertNotIn('docker login', self.workflow)
        self.assertNotIn('HEXALITH_CI_REGISTRY_CONFIG', self.workflow)
        configure = step(self.workflow, 'Configure only the dedicated pull reader')
        self.assertNotIn('GITHUB_ENV', configure)
        self.assertIn('HEXALITH_LIFECYCLE_REGISTRY_CONFIG: ' + READER, configure)
        self.assertIn('DOCKER_CONFIG: ' + READER, step(self.workflow, 'Observe the actual current-owner startup before standard cleanup'))
        self.assertEqual(self.workflow.count('DOCKER_CONFIG:'), 1)
        removal = step(self.workflow, 'Remove the private reader file')
        self.assertTrue(removal.lstrip().startswith('if: always()'))
        self.assertIn('HEXALITH_LIFECYCLE_REGISTRY_CONFIG: ' + READER, removal)
        self.assertIn('run: python3 -B scripts/qualify-disposable-ci.py remove-reader', removal)
        self.assertLess(self.workflow.index('- name: Configure only the dedicated pull reader'),
                        self.workflow.index('- name: Observe the actual current-owner startup before standard cleanup'))
        checkout = self.workflow.split('      - uses: actions/checkout@', 1)[1].split('\n      - ', 1)[0]
        self.assertIn('persist-credentials: false', checkout)
        self.assertTrue(Path(READER.replace('${{ runner.temp }}/', '')).name.startswith('hexalith-lifecycle-registry.'))

    def test_reader_removal_accepts_the_startup_reader_name(self):
        with tempfile.TemporaryDirectory() as temporary:
            runner = Path(temporary)
            reader = runner / 'hexalith-lifecycle-registry.startup-reader'
            reader.mkdir(mode=0o700)
            (reader / 'config.json').write_text('{"auths": {}}')
            Q.remove_reader_directory(str(reader), str(runner))
            self.assertFalse(reader.exists())

    def observer_step(self, body):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            probe = root / 'bin' / 'python3'
            probe.parent.mkdir()
            probe.write_text(SIGNAL_PROBE)
            probe.chmod(0o700)
            (root / 'step.sh').write_text(body + '\n')
            markers = {'SIGNAL_MARKER': str(root / 'signal'), 'STARTED_MARKER': str(root / 'started')}
            environment = {**os.environ, **markers, 'PATH': str(probe.parent) + os.pathsep + os.environ['PATH']}
            shell = subprocess.Popen(['bash', '-e', str(root / 'step.sh')], cwd=root, env=environment,  # nosec B603
                                     stdin=subprocess.DEVNULL, start_new_session=True)
            try:
                wait_for(root / 'started', 10)
                # The hosted runner cancels a step by signalling only the step's own shell process.
                os.kill(shell.pid, signal.SIGINT)
                try:
                    shell.wait(timeout=3)
                except subprocess.TimeoutExpired:
                    return None
                return (root / 'signal').read_text().split() if (root / 'signal').exists() else None
            finally:
                if shell.poll() is None:
                    os.killpg(shell.pid, signal.SIGKILL)
                    shell.wait(timeout=5)

    def test_observer_receives_the_runner_signal_sent_only_to_its_step_process(self):
        body = re.search(r'- name: Observe the actual current-owner startup before standard cleanup\n'
                         r'(?:        [^\n]*\n)*?        run: ([^\n]+)\n', self.workflow).group(1)
        self.assertEqual(self.observer_step(body), ['-B', 'scripts/observe-current-owner-startup.py'])
        # Control: without exec the step shell keeps the signal from the observer.
        self.assertIsNone(self.observer_step(body.replace('exec ', '', 1)))


if __name__ == '__main__':
    unittest.main()
