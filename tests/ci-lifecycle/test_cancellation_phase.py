"""Explicit unit state/receipt doubles only; no fabricated positive CI context or native qualification."""
import copy
from datetime import datetime, timedelta, timezone
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[2] / 'scripts/qualify-disposable-ci.py'
SPEC = importlib.util.spec_from_file_location('cancellation_phase', SCRIPT)
Q = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(Q)
CONTEXT = {'kind': 'unit-state-control-only'}


def facts(phase='startup'):
    clock = datetime.now(timezone.utc) - timedelta(seconds=3)
    stamp = lambda seconds: (clock + timedelta(seconds=seconds)).isoformat()
    owner = {'runId': 'a' * 32, 'attemptId': 'b' * 32, 'environmentId': 'c' * 32, 'executionContext': CONTEXT}
    item = {'cancellationPhase': phase, 'cancellationReadyAtUtc': stamp(0),
            'cancellationSignals': [{'signal': 2, 'observedAtUtc': stamp(1)}]}
    snapshot = {**owner, 'status': 'Starting', 'stateUpdatedAtUtc': stamp(0),
                'readStartedAtUtc': stamp(1.1), 'observedAtUtc': stamp(1.2),
                'firstOutcome': None, 'stateTestTerminal': None, 'nativeExecution': None}
    if phase == 'execution':
        item['executionCheckpoint'] = {'runId': owner['runId'], 'realPersistenceAssertionsPassed': True,
                                       'processId': 123, 'birth': '456'}
        item['admission'] = {'ownerRunId': owner['runId'], 'environmentId': owner['environmentId'],
                             'attachedRunId': 'd' * 32, 'originalAttachmentRequestId': 'e' * 32,
                             'requestedHoldSeconds': 240, 'effectiveHoldSeconds': 240,
                             'acceptedAtUtc': stamp(0), 'expiresAtUtc': stamp(240),
                             'endedAtUtc': None, 'outcome': None}
        snapshot.update(status='Ready', nativeExecution={'processId': 123, 'birth': '456'}, admission=copy.deepcopy(item['admission']))
    item['cancellationSnapshot'] = snapshot
    return owner, item


class CancellationPhaseTests(unittest.TestCase):
    def test_startup_requires_actual_starting_without_execution_or_first_terminal(self):
        owner, item = facts()
        Q.cancellation_phase(item, owner, CONTEXT)
        for changes in ({'status': 'Ready'}, {'status': 'Ready', 'nativeExecution': {'processId': 123, 'birth': '456'}},
                        {'status': 'Failed'}, {'stateTestTerminal': {'terminalStatus': 'failed'}},
                        {'firstOutcome': {'terminalStatus': 'failed'}}):
            changed = copy.deepcopy(item)
            changed['cancellationSnapshot'].update(changes)
            with self.subTest(actual=changes), self.assertRaises(ValueError):
                Q.cancellation_phase(changed, owner, CONTEXT)

    def test_missing_snapshot_bad_job_or_time_cannot_supply_phase_credit(self):
        owner, item = facts()
        for snapshot in (None, {}, {**item['cancellationSnapshot'], 'failureType': 'FileNotFoundError'},
                         {**item['cancellationSnapshot'], 'executionContext': {'kind': 'other-unit-context'}},
                         {**item['cancellationSnapshot'], 'runId': 'f' * 32},
                         {**item['cancellationSnapshot'], 'readStartedAtUtc': item['cancellationReadyAtUtc']}):
            changed = {**item, 'cancellationSnapshot': snapshot}
            with self.subTest(snapshot=snapshot), self.assertRaises((ValueError, KeyError)):
                Q.cancellation_phase(changed, owner, CONTEXT)

    def test_execution_requires_same_running_process_and_original_active_finite_admission(self):
        owner, item = facts('execution')
        Q.cancellation_phase(item, owner, CONTEXT)
        for changes in ({'status': 'Starting'}, {'nativeExecution': None},
                        {'nativeExecution': {'processId': 123, 'birth': 'reused'}},
                        {'admission': {**item['admission'], 'expiresAtUtc': '2026-01-01T00:00:00Z'}},
                        {'admission': {**item['admission'], 'outcome': 'withdrawn'}},
                        {'admission': {**item['admission'], 'environmentId': 'f' * 32}}):
            changed = copy.deepcopy(item)
            changed['cancellationSnapshot'].update(changes)
            with self.subTest(actual=changes), self.assertRaises(ValueError):
                Q.cancellation_phase(changed, owner, CONTEXT)

    def test_attached_execution_checkpoint_matches_the_live_signal_time_identity(self):
        owner, item = facts('execution')
        item['attachedExecutionCheckpoint'] = {'processId': 789, 'birth': 'original-attached-birth'}
        item['cancellationSnapshot']['attachedExecution'] = {'processId': 789, 'birth': 'original-attached-birth'}
        Q.cancellation_phase(item, owner, CONTEXT)
        for observed in (None, {'processId': 790, 'birth': 'original-attached-birth'}, {'processId': 789, 'birth': 'reused-birth'}):
            changed = copy.deepcopy(item)
            changed['cancellationSnapshot']['attachedExecution'] = observed
            with self.subTest(observed=observed), self.assertRaises(ValueError):
                Q.cancellation_phase(changed, owner, CONTEXT)
        del item['cancellationSnapshot']['attachedExecution']
        with self.assertRaises(ValueError):
            Q.cancellation_phase(item, owner, CONTEXT)

    def test_signal_snapshot_reads_current_state_without_archived_starting_fallback(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            owner, item = facts()
            owner['stateDirectory'] = str(root / 'native-unit-state')
            active = Path(owner['stateDirectory']) / (owner['runId'] + '.json')
            diagnostic = Path(owner['stateDirectory']) / 'diagnostics' / owner['runId']
            Q.write(active, {**owner, 'status': 'Ready', 'updatedAt': Q.now()})
            Q.write(diagnostic / 'owner.json', {**owner, 'status': 'Starting', 'updatedAt': Q.now()})
            controller = object.__new__(Q.Qualification)
            controller.context = CONTEXT
            controller.issued = lambda _: owner
            controller.diagnostics = lambda _: diagnostic
            controller.save = lambda: Q.write(root / 'retained-journal.json', item)
            controller.observe = lambda kind, **value: Q.write(root / 'retained-snapshot.json', value)
            with patch.object(controller, 'state') as archived_fallback:
                controller.cancellation_snapshot(item)
                archived_fallback.assert_not_called()
            self.assertEqual(item['cancellationSnapshot']['status'], 'Ready')
            self.assertEqual(Q.read(root / 'retained-journal.json')['cancellationSnapshot']['status'], 'Ready')
            with self.assertRaises(ValueError):
                Q.cancellation_phase(item, owner, CONTEXT)
            active.unlink()
            controller.cancellation_snapshot(item)
            self.assertEqual(item['cancellationSnapshot']['failureType'], 'FileNotFoundError')
            with self.assertRaises(ValueError):
                Q.cancellation_phase(item, owner, CONTEXT)

    def test_missing_invalid_or_mismatched_snapshot_cannot_suppress_any_receipt_cleanup(self):
        for actual in (None, {}, {'failureType': 'OSError'}, {'status': 'Ready'}):
            with self.subTest(snapshot=actual), tempfile.TemporaryDirectory() as temporary:
                directory = Path(temporary)
                owner, item = facts()
                receipt = directory / 'unit-receipt'
                receipt.write_text('unit double, not a CI receipt')
                item.update(label='cancel', receipt=str(receipt), cancellationSnapshot=actual)
                controller = object.__new__(Q.Qualification)
                controller.context = CONTEXT
                controller.evidence = directory
                controller.journal = {'owners': [item, {'label': 'peer', 'receipt': str(receipt)}]}
                controller.issued = lambda _: owner
                controller.recover_launch = lambda _: None
                controller.diagnostics = lambda _: directory
                controller.resume_host = lambda _: None
                controller.settled_child = lambda _: None
                observations = []
                controller.observe = lambda kind, **value: observations.append(value)
                with patch.object(controller, 'down') as down, patch.object(Q.HELPER, 'retain_artifacts') as retain, \
                     patch.object(controller, 'finalize_cancellation') as credit:
                    self.assertEqual(controller.finalize('cancelled'), 1)
                self.assertEqual(down.call_count, 2)
                self.assertEqual(retain.call_count, 2)
                credit.assert_not_called()
                self.assertFalse(observations[-1]['issuedOwnersCleanupVerified'])
                self.assertFalse(observations[-1]['candidateAcceptance'])


if __name__ == '__main__':
    unittest.main()
