"""Focused verifier and runner controls; all mutations stay in disposable copies."""
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

sys.dont_write_bytecode = True
BATCH = Path(__file__).resolve().parents[1]
ROOT = BATCH.parents[3]
RETAINED = Path('_bmad-output/implementation-artifacts/tests/ext-parties-1-owner-delivery-2026-10-25/resume-recovery')


def sha(data):
    return hashlib.sha256(data).hexdigest()


class ImplementationControls(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='legacy-review1-verifier-')
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name) / 'root'
        self.evidence = Path(self.directory.name) / 'evidence'
        self.evidence.mkdir()
        for name in ['verify-implementation.py', 'historical-protocol-proof.json', 'original-retry-workflow.cs.txt']:
            shutil.copyfile(BATCH / name, self.evidence / name)
        receipt = json.loads((BATCH / 'implementation-verification.json').read_text())
        for row in receipt['source_files']:
            target = self.root / row['path']
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / row['path'], target)
            row['lf_normalized_sha256'] = sha(target.read_bytes().replace(b'\r\n', b'\n'))
        artifact = self.root / 'fixture-binary.dll'
        artifact.write_bytes(b'qualified fixture binary\x00\x01')
        receipt['executed_artifacts'] = [{'path': 'fixture-binary.dll', 'sha256': sha(artifact.read_bytes())}]
        self.receipt = self.evidence / 'fixture-receipt.json'
        self.receipt.write_text(json.dumps(receipt))

    def run_verifier(self, *args):
        return subprocess.run([sys.executable, '-O', str(self.evidence / 'verify-implementation.py'), '--root', str(self.root),
            '--receipt', str(self.receipt), *args], capture_output=True, text=True)

    def test_lf_and_crlf_source_text_pass_under_optimization(self):
        for eol in [b'\n', b'\r\n']:
            with self.subTest(eol=eol):
                for row in json.loads(self.receipt.read_text())['source_files']:
                    target = self.root / row['path']
                    target.write_bytes(target.read_bytes().replace(b'\r\n', b'\n').replace(b'\n', eol))
                result = self.run_verifier('--artifacts')
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertTrue(json.loads(result.stdout)['historical_run_body_recomputed'])

    def test_artifact_mutation_fails_under_optimization(self):
        (self.root / 'fixture-binary.dll').write_bytes(b'changed binary')
        self.assertNotEqual(self.run_verifier('--artifacts').returncode, 0)

    def test_helper_body_mutation_fails_even_with_matching_source_hash(self):
        proof = json.loads((self.evidence / 'historical-protocol-proof.json').read_text())
        helper = self.root / proof['historical_helper']
        helper.write_bytes(helper.read_bytes().replace(b'Indexed: true', b'Indexed: false'))
        receipt = json.loads(self.receipt.read_text())
        for row in receipt['source_files']:
            if row['path'] == proof['historical_helper']:
                row['lf_normalized_sha256'] = sha(helper.read_bytes().replace(b'\r\n', b'\n'))
        self.receipt.write_text(json.dumps(receipt))
        result = self.run_verifier()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('RunAsync differs', result.stderr)

    def test_empty_and_duplicate_source_inventories_fail_under_optimization(self):
        receipt = json.loads(self.receipt.read_text())
        for sources in [[], [receipt['source_files'][0], receipt['source_files'][0]]]:
            with self.subTest(count=len(sources)):
                changed = dict(receipt, source_files=sources)
                self.receipt.write_text(json.dumps(changed))
                self.assertNotEqual(self.run_verifier().returncode, 0)


class PreservationControls(unittest.TestCase):
    def test_original_and_all_inventory_mutations_under_optimization(self):
        with tempfile.TemporaryDirectory(prefix='legacy-review1-preservation-') as temp:
            def run(root):
                return subprocess.run([sys.executable, '-O', str(BATCH / 'verify-preservation.py'), '--root', str(root)], capture_output=True, text=True)
            mutations = ['original', 'manifest-omission', 'manifest-duplicate', 'sources-omission', 'sources-duplicate', 'history-duplicate', 'missing-receipt']
            for mutation in mutations:
                with self.subTest(mutation=mutation):
                    root = Path(temp) / mutation
                    retained = root / RETAINED
                    shutil.copytree(ROOT / RETAINED, retained)
                    if mutation.startswith('manifest-'):
                        path = retained / 'SHA256SUMS'
                        lines = path.read_text().splitlines()
                        lines = lines[:-1] if mutation.endswith('omission') else lines + [lines[0]]
                        path.write_text('\n'.join(lines) + '\n')
                    if mutation.startswith('sources-'):
                        path = retained / 'local-source-and-artifact-hashes.json'
                        data = json.loads(path.read_text())
                        data['sources'] = data['sources'][:-1] if mutation.endswith('omission') else [data['sources'][0], *data['sources'][1:-1], data['sources'][0]]
                        path.write_text(json.dumps(data))
                    if mutation == 'history-duplicate':
                        path = retained / 'historical-preservation.json'
                        data = json.loads(path.read_text())
                        data['files'][-1] = data['files'][0]
                        path.write_text(json.dumps(data))
                    if mutation == 'missing-receipt':
                        (retained / 'historical-live-present/live-gate.txt').unlink()
                    result = run(root)
                    self.assertEqual(result.returncode == 0, mutation == 'original', result.stderr)
        # The historical repository artifacts are only read throughout these controls.
        result = subprocess.run([sys.executable, '-O', str(BATCH / 'verify-preservation.py')], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)


class RunnerControls(unittest.TestCase):
    def test_startup_failure_and_timeout_retain_invocations_and_binary_hashes(self):
        for script, expected in [('#!/bin/sh\nexit 19\n', 'startup_failed'), ('#!/bin/sh\nsleep 30\n', 'timed_out')]:
            with self.subTest(expected=expected), tempfile.TemporaryDirectory(prefix='legacy-review1-runner-') as temp:
                root = Path(temp)
                placement = root / 'runtime/dapr/placement'
                placement.parent.mkdir(parents=True)
                placement.write_text(script)
                placement.chmod(0o700)
                dll = root / 'owner/tests/Hexalith.Memories.IntegrationTests/bin/Debug/net10.0/Hexalith.Memories.IntegrationTests.dll'
                dll.parent.mkdir(parents=True)
                dll.write_bytes(b'fixture test assembly')
                server = dll.parent / 'Hexalith.Memories.Server.dll'
                server.write_bytes(b'fixture server assembly')
                output = root / 'output'
                result = subprocess.run([sys.executable, '-O', str(BATCH / 'run-owned-runtime.py'), '--runtime-root', str(root / 'runtime'),
                    '--owner-root', str(root / 'owner'), '--output', str(output), '--startup-timeout', '0.2'], capture_output=True, text=True)
                self.assertNotEqual(result.returncode, 0)
                receipt = json.loads((output / 'receipt.json').read_text())
                self.assertEqual(receipt['outcome'], 'failed')
                self.assertEqual(receipt['attempts'][0]['outcome'], expected)
                self.assertEqual(receipt['attempts'][0]['command'][0], str(placement))
                self.assertEqual(receipt['test_invocation']['outcome'], 'not_attempted')
                self.assertIn(str(dll), receipt['test_invocation']['command'])
                self.assertEqual(receipt['binary_sha256']['test_dll']['sha256'], sha(dll.read_bytes()))
                self.assertEqual(receipt['binary_sha256']['server_dll']['sha256'], sha(server.read_bytes()))
                self.assertIn('sha256', receipt['binary_sha256']['daprd'])
                self.assertTrue(receipt['cleanup_complete'])

    def test_test_timeout_is_persisted_before_cleanup(self):
        spec = importlib.util.spec_from_file_location('legacy_owned_runner', BATCH / 'run-owned-runtime.py')
        runner = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(runner)
        with tempfile.TemporaryDirectory(prefix='legacy-review1-timeout-') as temp:
            output = Path(temp)
            run = runner.OwnedRun(output, ['fixture invocation'])
            process = Mock(pid=99999999)
            process.wait.side_effect = subprocess.TimeoutExpired(['fixture'], .01)
            def spawn(*args, **kwargs):
                saved = json.loads((output / 'receipt.json').read_text())
                self.assertEqual(saved['attempts'][0]['outcome'], 'attempting')
                return process
            with patch.object(runner.subprocess, 'Popen', side_effect=spawn):
                process, row = run.launch('tests', ['fixture'])
            with self.assertRaises(subprocess.TimeoutExpired) as failure:
                run.wait_test(process, row, .01)
            run.fail(failure.exception)
            saved = json.loads((output / 'receipt.json').read_text())
            self.assertEqual(saved['attempts'][0]['outcome'], 'timed_out')
            self.assertEqual(saved['outcome'], 'failed')
            self.assertEqual(saved['exit_code'], 1)
            self.assertNotIn('cleanup_complete', saved)
            for log in run.logs:
                log.close()


if __name__ == '__main__':
    unittest.main()
