"""Bounded actual-process controls of the controller gate; no CLI, fake CI context or native owner is used."""
import importlib.util
import json
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[2] / 'scripts/qualify-disposable-ci.py'
SPEC = importlib.util.spec_from_file_location('launch_qualification', SCRIPT)
Q = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(Q)
REAL_POPEN = subprocess.Popen
IMPORT = ('import importlib.util, sys; '
          f'spec=importlib.util.spec_from_file_location("qualification", {str(SCRIPT)!r}); '
          'q=importlib.util.module_from_spec(spec); spec.loader.exec_module(q); ')


def controller(directory):
    value = object.__new__(Q.Qualification)
    value.temporary = Q.private_directory(Path(directory) / 'controller')
    value.evidence = Q.private_directory(Path(directory) / 'retained-evidence')
    value.journal_path = value.temporary / 'journal.json'
    value.journal = {'classification': 'local-process-control-no-hosted-credit', 'owners': [], 'observations': []}
    value.children = {}
    value.save()
    return value


def gated_command(item, marker):
    journal = str(Path(item['receipt']).parent.parent / 'journal.json')
    payload = ('import json, os; from pathlib import Path; '
               f'entry=next(item for item in json.loads(Path({journal!r}).read_text())["owners"] if item["label"]=={item["label"]!r}); '
               'assert entry["pid"]==os.getpid() and entry["processGroup"]==os.getpgrp(); '
               f'Path({str(marker)!r}).write_text("executed after durable actual process bookkeeping")')
    harmless = [sys.executable, '-c', payload]
    return [sys.executable, '-B', '-c', IMPORT + f'raise SystemExit(q.gated_exec({item!r}, {harmless!r}))']


def independent_finalizer(original):
    recovered = controller(original.temporary.parent / 'unused')
    recovered.temporary = original.temporary
    recovered.evidence = original.evidence
    recovered.journal_path = original.journal_path
    recovered.journal = Q.read(recovered.journal_path)
    result = recovered.finalize('failure')
    return result, recovered.journal['observations'][-1]


class LaunchRecoveryTests(unittest.TestCase):
    def test_launch_recovery_refuses_a_dangling_record_instead_of_polling_for_absence(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = controller(directory)
            root = Q.private_directory(owner.temporary / 'passing')
            record = root / 'launch.json'
            record.symlink_to(root / 'missing-target')
            item = {'label': 'passing', 'receipt': str(root / 'receipt.json'),
                    'publicResult': str(root / 'public-result.json'), 'launchRecord': str(record)}
            with patch.object(Q.time, 'monotonic', side_effect=[0, 6]), patch.object(Q.time, 'sleep') as sleep:
                with self.assertRaisesRegex(ValueError, 'linked'):
                    owner.recover_launch(item)
                sleep.assert_not_called()

    def test_launch_recovery_validates_the_record_bytes_from_its_held_descriptor(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = controller(directory)
            root = Q.private_directory(owner.temporary / 'passing')
            path = root / 'launch.json'
            item = {'label': 'passing', 'receipt': str(root / 'receipt.json'),
                    'publicResult': str(root / 'public-result.json'), 'launchRecord': str(path)}
            record = {key: item[key] for key in ('label', 'receipt', 'publicResult')}
            record.update(pid=123, birth='456', processGroup=123, gateStatus='closed')
            Q.write(path, record)
            original = Q.os.fstat
            swapped = []
            def admitting(descriptor):
                info = original(descriptor)
                if not swapped and info.st_ino == path.stat().st_ino:
                    swapped.append(True)
                    path.rename(root / 'held.json')
                    path.symlink_to(root / 'missing-target')
                return info
            with patch.object(Q.os, 'fstat', side_effect=admitting), patch.object(Q, 'birth', return_value=None):
                self.assertEqual(owner.recover_launch(item), record)
            self.assertEqual(swapped, [True])
            self.assertEqual(item['pid'], 123)

    def test_failed_intent_save_never_starts_a_process(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = controller(directory)
            with patch.object(owner, 'save', side_effect=OSError('unit intent write fault')), patch.object(Q.subprocess, 'Popen') as spawn:
                with self.assertRaises(OSError):
                    owner.start('passing')
                spawn.assert_not_called()

    def test_post_constructor_exception_recovers_actual_gated_child_from_durable_intent(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = controller(directory)
            marker = Path(directory) / 'executed'
            children = []
            def lose_constructor_result(_command, **options):
                durable = Q.read(owner.journal_path)['owners']
                self.assertEqual(len(durable), 1)
                self.assertNotIn('pid', durable[0])
                children.append(REAL_POPEN(gated_command(durable[0], marker), **options))
                raise OSError('unit post-constructor fault')
            try:
                with patch.object(Q.subprocess, 'Popen', side_effect=lose_constructor_result):
                    with self.assertRaises(OSError):
                        owner.start('passing')
                result, final = independent_finalizer(owner)
                self.assertEqual(result, 0)
                self.assertEqual(final['allocationStatus'], 'not-allocated')
                self.assertFalse(final['issuedOwnersCleanupVerified'])
                self.assertFalse(marker.exists())
                self.assertFalse(Path(owner.journal['owners'][0]['receipt']).exists())
                self.assertNotEqual(children[0].wait(timeout=5), 0)
                print(json.dumps({'classification': 'local-process-control', 'boundary': 'post-constructor-exception',
                                  'actualChildPid': children[0].pid, 'payloadExecuted': marker.exists(),
                                  'nativeOwnerIssued': False, 'finalizer': final}), flush=True)
            finally:
                for child in children:
                    if child.poll() is None:
                        child.kill()
                    child.wait(timeout=5)
                    child.stdin.close()

    def test_parent_termination_inside_constructor_cannot_release_actual_child(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            owner = controller(root)
            marker, blocked = root / 'executed', root / 'constructor-blocked.json'
            setup = (IMPORT + 'import pathlib, subprocess, time, os; '
                     f'root=pathlib.Path({directory!r}); '
                     'owner=object.__new__(q.Qualification); '
                     'owner.temporary=root/"controller"; owner.evidence=root/"retained-evidence"; '
                     'owner.journal_path=owner.temporary/"journal.json"; '
                     'owner.journal=q.read(owner.journal_path); owner.children={}; '
                     'original=subprocess.Popen;\n')
            body = ('def blocked_constructor(_command, **options):\n'
                    '    item=q.read(owner.journal_path)["owners"][0]\n'
                    f'    child=original({gated_command({"label": "passing", "receipt": str(owner.temporary / "passing/receipt.json"), "launchRecord": str(owner.temporary / "passing/launch.json"), "publicResult": str(owner.temporary / "passing/public-result.json")}, marker)!r}, **options)\n'
                    f'    q.write({str(blocked)!r}, {{"actualParentPid":os.getpid(),"actualChildPid":child.pid}})\n'
                    '    time.sleep(20)\n'
                    '    return child\n'
                    'subprocess.Popen=blocked_constructor\nowner.start("passing")\n')
            parent = REAL_POPEN([sys.executable, '-B', '-c', setup + body], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            try:
                deadline = time.monotonic() + 5
                while not blocked.exists() and time.monotonic() < deadline and parent.poll() is None:
                    time.sleep(0.05)
                self.assertTrue(blocked.exists(), 'Actual parent did not reach the bounded constructor boundary')
                actual = Q.read(blocked)
                self.assertEqual(actual['actualParentPid'], parent.pid)
                self.assertEqual(len(Q.read(owner.journal_path)['owners']), 1)
                parent.terminate()
                self.assertEqual(parent.wait(timeout=5), -signal.SIGTERM)
                result, final = independent_finalizer(owner)
                self.assertEqual(result, 0)
                self.assertFalse(marker.exists())
                self.assertFalse(final['issuedOwnersCleanupVerified'])
                self.assertEqual(final['allocationStatus'], 'not-allocated')
                self.assertFalse(Path(Q.read(owner.journal_path)['owners'][0]['receipt']).exists())
                print(json.dumps({'classification': 'local-process-control', 'boundary': 'actual-parent-SIGTERM-during-constructor',
                                  **actual, 'payloadExecuted': marker.exists(), 'nativeOwnerIssued': False, 'finalizer': final}), flush=True)
            finally:
                if parent.poll() is None:
                    parent.kill()
                parent.wait(timeout=5)
                # Recover only the real child indexed by this test controller's durable intent.
                recovered = controller(root / 'fallback')
                recovered.temporary, recovered.evidence, recovered.journal_path = owner.temporary, owner.evidence, owner.journal_path
                recovered.journal = Q.read(owner.journal_path)
                recovered.finalize('failure')

    def test_immediate_process_group_bookkeeping_failure_keeps_gate_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = controller(directory)
            marker = Path(directory) / 'executed'
            children = []
            def spawn(_command, **options):
                item = Q.read(owner.journal_path)['owners'][0]
                child = REAL_POPEN(gated_command(item, marker), **options)
                children.append(child)
                return child
            try:
                with patch.object(Q.subprocess, 'Popen', side_effect=spawn), patch.object(Q.os, 'getpgid', side_effect=OSError('unit group fault')):
                    with self.assertRaises(OSError):
                        owner.start('passing')
                self.assertNotEqual(children[0].wait(timeout=5), 0)
                result, final = independent_finalizer(owner)
                self.assertEqual(result, 0)
                self.assertFalse(marker.exists())
                self.assertFalse(final['issuedOwnersCleanupVerified'])
            finally:
                for child in children:
                    if child.poll() is None:
                        child.kill()
                    child.wait(timeout=5)
                    child.stdin.close()

    def test_released_process_without_any_receipt_remains_explicitly_unverified(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = controller(directory)
            marker = Path(directory) / 'executed'
            item = {'label': 'passing', 'receipt': str(owner.temporary / 'passing/receipt.json'),
                    'launchRecord': str(owner.temporary / 'passing/launch.json'),
                    'publicResult': str(owner.temporary / 'passing/public-result.json')}
            Q.private_directory(owner.temporary / 'passing')
            owner.journal['owners'].append(item)
            owner.save()
            children = []
            def spawn(_command, **options):
                child = REAL_POPEN(gated_command(item, marker), **options)
                children.append(child)
                return child
            try:
                with patch.object(Q.subprocess, 'Popen', side_effect=spawn):
                    owner.launch(item)
                child = children[0]
                self.assertEqual(child.wait(timeout=5), 0)
                self.assertTrue(marker.exists())
                self.assertEqual(Q.read(owner.journal_path)['owners'][0]['pid'], child.pid)
                result, final = independent_finalizer(owner)
                self.assertEqual(result, 1)
                self.assertEqual(final['allocationStatus'], 'unverified')
                self.assertFalse(final['issuedOwnersCleanupVerified'])
                self.assertTrue(final['failures'])
                print(json.dumps({'classification': 'local-process-control', 'boundary': 'released-without-receipt',
                                  'actualChildPid': child.pid, 'payloadExecuted': marker.exists(),
                                  'nativeOwnerIssued': False, 'finalizer': final}), flush=True)
            finally:
                for child in children:
                    if child.poll() is None:
                        child.kill()
                    child.wait(timeout=5)
                    child.stdin.close()

    def test_process_bookkeeping_save_failure_cannot_release_child(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = controller(directory)
            marker = Path(directory) / 'executed'
            children, saves = [], []
            original_save = owner.save
            def save():
                saves.append(True)
                if len(saves) == 2:
                    raise OSError('unit actual-process journal write fault')
                original_save()
            def spawn(_command, **options):
                item = Q.read(owner.journal_path)['owners'][0]
                child = REAL_POPEN(gated_command(item, marker), **options)
                children.append(child)
                return child
            try:
                with patch.object(owner, 'save', side_effect=save), patch.object(Q.subprocess, 'Popen', side_effect=spawn):
                    with self.assertRaises(OSError):
                        owner.start('passing')
                self.assertNotEqual(children[0].wait(timeout=5), 0)
                self.assertNotIn('pid', Q.read(owner.journal_path)['owners'][0])
                result, final = independent_finalizer(owner)
                self.assertEqual(result, 0)
                self.assertFalse(marker.exists())
                self.assertFalse(final['issuedOwnersCleanupVerified'])
            finally:
                for child in children:
                    if child.poll() is None:
                        child.kill()
                    child.wait(timeout=5)
                    child.stdin.close()

    def test_launch_record_validation_failure_cannot_block_exact_receipt_cleanup(self):
        # Result-field fault simulation only: helper/CLI identities here are explicitly unit doubles.
        with tempfile.TemporaryDirectory() as directory:
            owner = controller(directory)
            for label in ('passing', 'peer'):
                receipt = Path(directory) / (label + '-unit-receipt')
                receipt.write_text('unit double, not a CI receipt')
                owner.journal['owners'].append({'label': label, 'receipt': str(receipt)})
            owner.issued = lambda _: {'runId': 'a' * 32}
            owner.diagnostics = lambda _: Path(directory)
            owner.resume_host = lambda _: None
            with patch.object(owner, 'recover_launch', side_effect=ValueError('unit corrupt process bookkeeping')), \
                 patch.object(owner, 'down') as down, patch.object(Q.HELPER, 'retain_artifacts'):
                self.assertEqual(owner.finalize('failure'), 1)
            self.assertEqual(down.call_count, 2)
            final = owner.journal['observations'][-1]
            self.assertEqual(final['issuedOwners'], 2)
            self.assertEqual(len(final['failures']), 2)
            self.assertFalse(final['issuedOwnersCleanupVerified'])

    def test_unknown_launch_never_becomes_not_allocated_or_prevents_peer_cleanup(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = controller(directory)
            owner.journal['owners'] = [{'label': 'passing', 'receipt': str(Path(directory) / 'missing')},
                                       {'label': 'peer', 'receipt': str(Path(directory) / 'missing-peer')}]
            with patch.object(owner, 'recover_launch', side_effect=FileNotFoundError('unit unavailable launch metadata')):
                result = owner.finalize('failure')
            self.assertEqual(result, 1)
            final = owner.journal['observations'][-1]
            self.assertEqual(final['allocationStatus'], 'unverified')
            self.assertEqual(len(final['failures']), 2)
            self.assertFalse(final['issuedOwnersCleanupVerified'])


if __name__ == '__main__':
    unittest.main()
