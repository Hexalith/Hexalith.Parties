#!/usr/bin/env python3
"""Run selected Memories controls and retain every attempted owned invocation."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import signal
import socket
import subprocess
import sys
import time
import urllib.request


class OwnedRun:
    def __init__(self, output, invocation):
        self.output = output
        self.processes = []
        self.logs = []
        self.receipt = {'invocation': invocation, 'outcome': 'preparing', 'exit_code': None, 'attempts': [], 'binary_sha256': {}}
        self.save()

    def save(self):
        (self.output / 'receipt.json').write_text(json.dumps(self.receipt, indent=2) + '\n')

    def hash_binary(self, label, path):
        path = Path(path).resolve()
        row = {'path': str(path)}
        try:
            row['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
        except OSError as error:
            row['failure'] = type(error).__name__
        self.receipt['binary_sha256'][label] = row
        self.save()

    def launch(self, label, command, cwd=None, env=None):
        row = {'label': label, 'command': command, 'cwd': str(cwd) if cwd else None, 'outcome': 'attempting', 'exit_code': None}
        self.receipt['attempts'].append(row)
        self.save()  # Attempted argv survives Popen failure and startup timeout.
        try:
            log = (self.output / (label + '.log')).open('w')
            self.logs.append(log)
            process = subprocess.Popen(command, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        except OSError as error:
            row.update(outcome='spawn_failed', failure=type(error).__name__)
            self.save()
            raise
        self.processes.append((process, row))
        row.update(pid=process.pid, outcome='started')
        self.save()
        return process, row

    def wait_test(self, process, row, timeout):
        try:
            code = process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            row.update(outcome='timed_out', failure='test_timeout')
            self.save()
            raise
        row.update(exit_code=code, outcome='passed' if code == 0 else 'failed')
        self.save()
        return code

    def fail(self, error):
        self.receipt.update(outcome='failed', exit_code=1, failure={'type': type(error).__name__, 'message': str(error)})
        self.save()  # Persist the failure before owned teardown.

    def cleanup(self):
        for process, _ in reversed(self.processes):
            try:
                os.killpg(process.pid, signal.SIGTERM)
            except ProcessLookupError:
                pass
        for process, row in reversed(self.processes):
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                process.wait(timeout=5)
            row['cleanup_exit_code'] = process.poll()
        for log in self.logs:
            log.close()
        self.receipt['cleanup_complete'] = True
        self.save()


def port():
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        return sock.getsockname()[1]


def ready(run, process, row, probe, timeout):
    deadline = time.monotonic() + timeout
    while True:
        if process.poll() is not None:
            row.update(outcome='startup_failed', exit_code=process.poll())
            run.save()
            raise RuntimeError(row['label'] + ' exited before readiness')
        try:
            if probe():
                row['outcome'] = 'ready'
                run.save()
                return
        except (OSError, ValueError):
            pass
        if time.monotonic() >= deadline:
            row.update(outcome='timed_out', failure='readiness_timeout')
            run.save()
            raise TimeoutError(row['label'] + ' readiness timeout')
        time.sleep(.1)


def http_ready(listen):
    with urllib.request.urlopen(f'http://127.0.0.1:{listen}/healthz', timeout=1) as response:
        return response.status == 200


def redis_ready(listen):
    with socket.create_connection(('127.0.0.1', listen), timeout=1) as sock:
        sock.sendall(b'*1\r\n$4\r\nPING\r\n')
        return sock.recv(50).startswith(b'+PONG')


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument('--runtime-root', type=Path, required=True)
    parser.add_argument('--owner-root', type=Path, default=Path(__file__).resolve().parents[4] / 'references/Hexalith.Memories')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--scope', choices=['focused', 'affected'], default='affected')
    parser.add_argument('--method', action='append', help='Run only these xUnit methods instead of the scope defaults.')
    parser.add_argument('--startup-timeout', type=float, default=45)
    parser.add_argument('--test-timeout', type=float, default=1200)
    options = parser.parse_args(argv)
    owner, runtime, output = options.owner_root.resolve(), options.runtime_root.resolve(), options.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    run = OwnedRun(output, [sys.executable, str(Path(__file__).resolve()), *(argv if argv is not None else sys.argv[1:])])
    dll = owner / 'tests/Hexalith.Memories.IntegrationTests/bin/Debug/net10.0/Hexalith.Memories.IntegrationTests.dll'
    test_command = ['dotnet', str(dll)]
    if options.method:
        for method in options.method:
            test_command += ['-method', method]
    elif options.scope == 'focused':
        test_command += ['-method', '*NamespaceMigration*', '-method', '*OriginalSameNameRawRetry*']
    else:
        for name in ['NaturalLanguageErasureIntegrationTests', 'RetryWorkflowErasureIntegrationTests', 'TenantIngestionLifecycleIntegrationTests']:
            test_command += ['-class', 'Hexalith.Memories.IntegrationTests.Ingestion.' + name]
    test_command += ['-xml', str(output / 'results.xml')]
    run.receipt['test_invocation'] = {'command': test_command, 'cwd': str(owner), 'outcome': 'not_attempted'}
    binaries = {'test_dll': dll, 'server_dll': dll.parent / 'Hexalith.Memories.Server.dll',
        'daprd': Path.home() / '.dapr/bin/daprd', 'dotnet': Path(shutil.which('dotnet') or 'dotnet')}
    for name in ['dapr/placement', 'dapr/scheduler', 'native-redis/redis-server', 'native-redis/redisearch.so', 'native-falkor/redis-server', 'native-falkor/falkordb.so']:
        binaries[name] = runtime / name
    for label, path in binaries.items():
        run.hash_binary(label, path)
    run.hash_binary('runner', Path(__file__))
    code = 1
    try:
        pp, pr, ph, sp, se, sc, sh = [port() for _ in range(7)]
        controls = [
            ('placement', [str(runtime / 'dapr/placement'), '--port', str(pp), '--listen-address', '127.0.0.1', '--initial-cluster', f'dapr-placement-0=127.0.0.1:{pr}', '--healthz-listen-address', '127.0.0.1', '--healthz-port', str(ph), '--enable-metrics=false'], ph),
            ('scheduler', [str(runtime / 'dapr/scheduler'), '--port', str(sp), '--listen-address', '127.0.0.1', '--override-broadcast-host-port', f'127.0.0.1:{sp}', '--etcd-initial-cluster', f'dapr-scheduler-server-0=http://127.0.0.1:{se}', '--etcd-client-port', str(sc), '--etcd-data-dir', str(output / 'scheduler-data'), '--healthz-listen-address', '127.0.0.1', '--healthz-port', str(sh), '--enable-metrics=false'], sh)]
        for label, command, health in controls:
            process, row = run.launch(label, command)
            ready(run, process, row, lambda health=health: http_ready(health), options.startup_timeout)
        rp, fp = port(), port()
        for label, binary, module, listen in [('redis', runtime / 'native-redis/redis-server', runtime / 'native-redis/redisearch.so', rp), ('falkor', runtime / 'native-falkor/redis-server', runtime / 'native-falkor/falkordb.so', fp)]:
            data = output / label
            data.mkdir()
            command = [str(binary), '--port', str(listen), '--bind', '127.0.0.1', '--dir', str(data), '--save', '', '--appendonly', 'yes', '--loadmodule', str(module)]
            process, row = run.launch(label, command)
            ready(run, process, row, lambda listen=listen: redis_ready(listen), options.startup_timeout)
        env = dict(os.environ, MEMORIES_TEST_OWNED_NATIVE='1', MEMORIES_TEST_REDISSTACK_CONNECTION=f'127.0.0.1:{rp}',
            MEMORIES_TEST_FALKORDB_CONNECTION=f'127.0.0.1:{fp}', MEMORIES_DAPR_PLACEMENT_HOST_ADDRESS=f'127.0.0.1:{pp}', MEMORIES_DAPR_SCHEDULER_HOST_ADDRESS=f'127.0.0.1:{sp}')
        process, row = run.launch('tests', test_command, cwd=owner, env=env)
        run.receipt['test_invocation'] = row
        code = run.wait_test(process, row, options.test_timeout)
        run.receipt.update(outcome='passed' if code == 0 else 'failed', exit_code=code)
        run.save()
    except (OSError, RuntimeError, subprocess.TimeoutExpired) as error:
        run.fail(error)
    finally:
        run.cleanup()
    print(json.dumps({'outcome': run.receipt['outcome'], 'exit_code': code, 'receipt': str(output / 'receipt.json')}))
    return code


if __name__ == '__main__':
    sys.exit(main())
