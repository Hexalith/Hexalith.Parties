"""Local controller safety simulations only; none are hosted/runtime qualification evidence."""
import copy
from datetime import datetime, timedelta, timezone
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[2] / 'scripts' / 'qualify-disposable-ci.py'
SPEC = importlib.util.spec_from_file_location('qualification', SCRIPT)
Q = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(Q)


class ControllerSafetyTests(unittest.TestCase):
    def test_local_process_cannot_become_a_hosted_positive_owner(self):
        with patch.dict(os.environ, {}, clear=True), patch.object(Q.subprocess, 'Popen') as child:
            with self.assertRaises(ValueError):
                Q.Qualification()
            child.assert_not_called()

    def test_atomic_recovery_tolerates_an_interrupted_prior_temporary_write(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / 'journal.json.new').write_text('interrupted')
            Q.write(root / 'journal.json', {'classification': 'unit-simulation'})
            self.assertEqual(Q.read(root / 'journal.json'), {'classification': 'unit-simulation'})
            self.assertEqual((root / 'journal.json.new').read_text(), 'interrupted')
            self.assertEqual((root / 'journal.json').stat().st_mode & 0o777, 0o600)

    def test_linked_and_hardlinked_metadata_are_refused(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original = root / 'original.json'
            original.write_text('{}')
            symbolic = root / 'symbolic.json'
            symbolic.symlink_to(original)
            hard = root / 'hard.json'
            os.link(original, hard)
            for path in (symbolic, hard):
                with self.subTest(path=path.name), self.assertRaises(ValueError):
                    Q.read(path)
                with self.subTest(write=path.name), self.assertRaises(ValueError):
                    Q.write(path, {'replacement': True})
            self.assertEqual(original.read_text(), '{}')

    def test_missing_or_unknown_cleanup_never_verifies_zero_resources(self):
        owner = {'runId': 'a' * 32}
        completed = subprocess.CompletedProcess([], 0)
        inventory = {'containerIds': [], 'processIds': [], 'workspaceIds': [], 'claimIds': [],
                     'discoveryFailures': [], 'unverified': False}
        result = {'runId': owner['runId'], 'outcome': {'exitCode': 'Success'}, 'remainingResources': inventory}
        self.assertTrue(Q.HELPER.cleanup_verified(completed, result, owner))
        for key in inventory:
            changed = copy.deepcopy(result)
            del changed['remainingResources'][key]
            with self.subTest(missing=key):
                self.assertFalse(Q.HELPER.cleanup_verified(completed, changed, owner))
        changed = copy.deepcopy(result)
        changed['remainingResources']['unverified'] = True
        self.assertFalse(Q.HELPER.cleanup_verified(completed, changed, owner))
        changed = copy.deepcopy(result)
        changed['remainingResources']['processIds'] = [123]
        self.assertFalse(Q.HELPER.cleanup_verified(completed, changed, owner))
        changed = copy.deepcopy(result)
        changed['runId'] = 'b' * 32
        self.assertFalse(Q.HELPER.cleanup_verified(completed, changed, owner))

    def test_first_outcome_requires_real_nonempty_time_status_and_mapping_fields(self):
        record = {'runId': 'a' * 32, 'ownerId': 'a' * 32, 'terminalStatus': 'failed',
                  'terminalOutcome': {'exitCode': 'ProductOrTest'}, 'terminalAtUtc': '2026-11-03T00:00:00Z',
                  'sourceMappingHash': 'b' * 64, 'cleanupDisposition': 'pending'}
        first = Q.first_outcome(record)
        record['cleanupDisposition'] = 'complete'
        self.assertEqual(Q.first_outcome(record), first)
        for key in first:
            if key == 'terminalCause':
                continue
            changed = dict(record)
            changed.pop(key)
            with self.subTest(missing=key), self.assertRaises(ValueError):
                Q.first_outcome(changed)
        record['terminalAtUtc'] = '2026-11-03T00:00:00'
        with self.assertRaises(ValueError):
            Q.first_outcome(record)

    def test_released_terminal_status_and_outcome_are_checked_independently(self):
        # Actual selected-test result contract: passing is completed/Success, never passed.
        record = {'runId': 'a' * 32, 'ownerId': 'a' * 32, 'terminalStatus': 'completed',
                  'terminalOutcome': {'exitCode': 'Success'}, 'terminalAtUtc': '2026-11-03T00:00:00Z',
                  'sourceMappingHash': 'b' * 64}
        self.assertEqual(Q.declared_terminal(record, 'pass')['terminalStatus'], 'completed')
        for status, outcome in [('passed', 'Success'), ('completed', 'ProductOrTest'), ('failed', 'Success')]:
            changed = {**record, 'terminalStatus': status, 'terminalOutcome': {'exitCode': outcome}}
            with self.subTest(status=status, outcome=outcome), self.assertRaises(ValueError):
                Q.declared_terminal(changed, 'pass')
        record.update(terminalStatus='failed', terminalOutcome={'exitCode': 'ProductOrTest'})
        self.assertEqual(Q.declared_terminal(record, 'fail')['terminalStatus'], 'failed')
        for status, outcome in [('failed', 'Success'), ('cancelled', 'Cancelled'), ('completed', 'ProductOrTest')]:
            changed = {**record, 'terminalStatus': status, 'terminalOutcome': {'exitCode': outcome}}
            with self.subTest(status=status, outcome=outcome), self.assertRaises(ValueError):
                Q.declared_terminal(changed, 'fail')

    def test_cancellation_result_requires_real_phase_signal_and_actual_status_outcome_time(self):
        item = {'runId': 'a' * 32, 'cancellationPhase': 'execution',
                'cancellationReadyAtUtc': '2026-11-03T00:00:00Z',
                'cancellationSignals': [{'signal': 2, 'observedAtUtc': '2026-11-03T00:00:02Z'}]}
        record = {'runId': 'a' * 32, 'ownerId': 'a' * 32, 'terminalStatus': 'cancelled',
                  'terminalOutcome': {'exitCode': 'Cancelled'}, 'terminalAtUtc': '2026-11-03T00:00:03Z',
                  'sourceMappingHash': 'b' * 64}
        self.assertEqual(Q.cancellation_terminal(record, item), 'cancelled')
        for changes in ({'terminalStatus': 'completed'}, {'terminalOutcome': {'exitCode': 'Success'}},
                        {'terminalAtUtc': '2026-11-03T00:00:01Z'}, {'runId': 'c' * 32}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                Q.cancellation_terminal({**record, **changes}, item)
        for key in ('cancellationPhase', 'cancellationReadyAtUtc', 'cancellationSignals'):
            changed = dict(item)
            changed.pop(key)
            with self.subTest(missing=key), self.assertRaises(ValueError):
                Q.cancellation_terminal(record, changed)

    def test_cancellation_preserves_prior_product_failure_without_claiming_api_order(self):
        item = {'runId': 'a' * 32, 'cancellationPhase': 'startup',
                'cancellationReadyAtUtc': '2026-11-03T00:00:00Z',
                'cancellationSignals': [{'signal': 15, 'observedAtUtc': '2026-11-03T00:00:02Z'}]}
        record = {'runId': 'a' * 32, 'ownerId': 'a' * 32, 'terminalStatus': 'failed',
                  'terminalOutcome': {'exitCode': 'ProductOrTest'}, 'terminalAtUtc': '2026-11-03T00:00:01Z',
                  'sourceMappingHash': 'b' * 64}
        first = Q.first_outcome(record)
        self.assertEqual(Q.cancellation_terminal(record, item, first), 'product-failure-before-observed-signal')
        self.assertEqual(record['terminalStatus'], 'failed')
        with self.assertRaises(ValueError):
            Q.cancellation_terminal(record, item)
        for field, value in [('terminalAtUtc', '2026-11-03T00:00:01.5Z'), ('terminalOutcome', {'exitCode': 'Cancelled'}),
                             ('terminalStatus', 'cancelled')]:
            with self.subTest(changed=field), self.assertRaises(ValueError):
                Q.cancellation_terminal({**record, field: value}, item, first)

    def test_independent_cancellation_finalizer_requires_hold_and_preserves_retry_records(self):
        controller = object.__new__(Q.Qualification)
        item = {'runId': 'a' * 32, 'cancellationPhase': 'execution',
                'cancellationReadyAtUtc': '2026-11-03T00:00:00Z',
                'cancellationSignals': [{'signal': 2, 'observedAtUtc': '2026-11-03T00:00:02Z'}],
                'admission': {'attachedRunId': 'b' * 32, 'expiresAtUtc': '2026-11-03T00:04:00Z'}}
        record = {'runId': 'a' * 32, 'ownerId': 'a' * 32, 'terminalStatus': 'cancelled',
                  'terminalOutcome': {'exitCode': 'Cancelled'}, 'terminalAtUtc': '2026-11-03T00:00:03Z',
                  'sourceMappingHash': 'b' * 64}
        controller.archived = lambda _: dict(record)
        controller.down = lambda *args, **kwargs: None
        controller.observe = lambda *args, **kwargs: None
        with patch.object(controller, 'withdrawn', return_value={'actual': 'unit-simulation'}) as withdrawn:
            controller.finalize_cancellation(item, None, 'cancelled')
            self.assertEqual(withdrawn.call_count, 2)
            self.assertTrue(all(call.kwargs['reason'] == 'ci-job-cancelled' for call in withdrawn.call_args_list))
        missing = {key: value for key, value in item.items() if key != 'admission'}
        with self.assertRaises(ValueError):
            controller.finalize_cancellation(missing, None, 'cancelled')
        with self.assertRaises(ValueError):
            controller.finalize_cancellation(item, None, 'success')
        with patch.object(controller, 'withdrawn', side_effect=[{'expiry': 'original'}, {'expiry': 'changed'}]):
            with self.assertRaises(ValueError):
                controller.finalize_cancellation(item, None, 'cancelled')
        changed = {**record, 'terminalAtUtc': '2026-11-03T00:00:04Z'}
        records = iter([record, changed])
        controller.archived = lambda _: next(records)
        with patch.object(controller, 'withdrawn', return_value={'actual': 'unit-simulation'}):
            with self.assertRaises(ValueError):
                controller.finalize_cancellation(item, None, 'cancelled')

    def test_invalid_first_outcome_cannot_prevent_exact_cleanup_of_any_owner(self):
        controller = object.__new__(Q.Qualification)
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            receipt = directory / 'receipt.json'
            receipt.write_text('{}')
            (directory / 'selected-test.json').write_text('{}')
            controller.journal = {'owners': [{'label': 'cancel', 'receipt': str(receipt), 'cancellationPhase': 'startup'},
                                             {'label': 'peer', 'receipt': str(receipt)}]}
            controller.evidence = directory
            controller.issued = lambda _: {'runId': 'a' * 32}
            controller.diagnostics = lambda _: directory
            controller.resume_host = lambda _: None
            controller.recover_launch = lambda _: None
            controller.settled_child = lambda _: None
            controller.observe = lambda *args, **kwargs: None
            with patch.object(controller, 'down') as down, patch.object(Q.HELPER, 'retain_artifacts'):
                self.assertEqual(controller.finalize('cancelled'), 1)
                self.assertEqual(down.call_count, 2)

    def test_resume_failure_still_cleans_every_valid_unit_owner_and_retains_uncertainty(self):
        # Explicit result-field/unit receipt doubles; no actual CI identity or native owner is created.
        for failure in (ProcessLookupError('unit exit-before-SIGCONT race'), KeyError('unit malformed pause'),
                        ValueError('unit unsafe pause bookkeeping')):
            with self.subTest(failure=type(failure).__name__), tempfile.TemporaryDirectory() as temporary:
                controller = object.__new__(Q.Qualification)
                directory = Path(temporary)
                receipt = directory / 'unit-receipt'
                receipt.write_text('unit double, not a CI receipt')
                controller.journal = {'owners': [{'label': 'passing', 'receipt': str(receipt)},
                                                 {'label': 'peer', 'receipt': str(receipt)}]}
                controller.evidence = directory
                controller.issued = lambda _: {'runId': 'a' * 32}
                controller.diagnostics = lambda _: directory
                controller.recover_launch = lambda _: None
                controller.settled_child = lambda _: None
                observations = []
                controller.observe = lambda kind, **value: observations.append({'kind': kind, **value})
                with patch.object(controller, 'resume_host', side_effect=failure), \
                     patch.object(controller, 'down') as down, patch.object(Q.HELPER, 'retain_artifacts') as retain:
                    self.assertEqual(controller.finalize('cancelled'), 1)
                self.assertEqual(down.call_count, 2)
                self.assertEqual(retain.call_count, 2)
                resumes = [value for value in observations if value['kind'] == 'owned-host-resume-unverified']
                self.assertEqual(len(resumes), 2)
                self.assertTrue(all(value['failureType'] == type(failure).__name__ for value in resumes))
                self.assertEqual(observations[-1]['issuedOwners'], 2)
                self.assertEqual(len(observations[-1]['failures']), 2)
                self.assertFalse(observations[-1]['issuedOwnersCleanupVerified'])
                self.assertFalse(observations[-1]['candidateAcceptance'])

    def test_resume_and_down_failures_still_retain_artifacts_for_each_valid_unit_owner(self):
        with tempfile.TemporaryDirectory() as temporary:
            controller = object.__new__(Q.Qualification)
            directory = Path(temporary)
            receipt = directory / 'unit-receipt'
            receipt.write_text('unit double, not a CI receipt')
            controller.journal = {'owners': [{'label': 'passing', 'receipt': str(receipt)},
                                             {'label': 'peer', 'receipt': str(receipt)}]}
            controller.evidence = directory
            controller.issued = lambda _: {'runId': 'a' * 32}
            controller.diagnostics = lambda _: directory
            controller.recover_launch = lambda _: None
            observations = []
            controller.observe = lambda kind, **value: observations.append({'kind': kind, **value})
            with patch.object(controller, 'resume_host', side_effect=ProcessLookupError('unit vanished process')), \
                 patch.object(controller, 'down', side_effect=ValueError('unit exact down refusal')) as down, \
                 patch.object(Q.HELPER, 'retain_artifacts') as retain:
                self.assertEqual(controller.finalize('cancelled'), 1)
            self.assertEqual(down.call_count, 2)
            self.assertEqual(retain.call_count, 2)
            self.assertEqual(len([value for value in observations if value['kind'] == 'owned-host-resume-unverified']), 2)
            self.assertFalse(observations[-1]['issuedOwnersCleanupVerified'])

    def test_finite_admission_and_retry_cannot_replace_the_original_expiry(self):
        accepted = datetime.now(timezone.utc)
        attachment = {'ownerRunId': 'a' * 32, 'attachedRunId': 'b' * 32,
                      'originalAttachmentRequestId': 'c' * 32, 'environmentId': 'd' * 32,
                      'requestedHoldSeconds': 240, 'effectiveHoldSeconds': 240,
                      'acceptedAtUtc': accepted.isoformat(), 'expiresAtUtc': (accepted + timedelta(seconds=240)).isoformat(),
                      'endedAtUtc': None, 'outcome': None}
        Q.active_attachment(attachment, 'a' * 32)
        Q.same_expiry(attachment, dict(attachment))
        for key in ('acceptedAtUtc', 'expiresAtUtc', 'attachedRunId', 'originalAttachmentRequestId'):
            changed = dict(attachment)
            changed[key] = 'changed'
            with self.subTest(field=key), self.assertRaises(ValueError):
                Q.same_expiry(attachment, changed)
        attachment['expiresAtUtc'] = (accepted - timedelta(seconds=1)).isoformat()
        with self.assertRaises(ValueError):
            Q.active_attachment(attachment, 'a' * 32)

    def test_reused_controller_process_is_never_signalled(self):
        controller = object.__new__(Q.Qualification)
        with patch.object(Q, 'birth', return_value='replacement'), patch.object(Q.os, 'killpg') as kill:
            with self.assertRaises(ValueError):
                controller.process_signal({'pid': 123, 'birth': 'original', 'processGroup': 123}, 2)
            kill.assert_not_called()

    def test_receipt_cannot_escape_its_recorded_owner_role(self):
        controller = object.__new__(Q.Qualification)
        controller.temporary = Path('/unit-simulation/controller')
        with patch.object(Q.HELPER, 'receipt_owner') as read:
            for item in ({'label': 'foreign', 'receipt': '/tmp/foreign.json'},
                         {'label': 'peer', 'receipt': '/tmp/foreign.json'}):
                with self.subTest(item=item), self.assertRaises(ValueError):
                    controller.issued(item)
            read.assert_not_called()

    def test_empty_finalization_is_not_claimed_as_runtime_cleanup_verification(self):
        controller = object.__new__(Q.Qualification)
        controller.journal = {'owners': []}
        observations = []
        controller.observe = lambda kind, **values: observations.append((kind, values))
        self.assertEqual(controller.finalize('failure'), 0)
        self.assertEqual(observations[0][1]['allocationStatus'], 'not-allocated')
        self.assertFalse(observations[0][1]['issuedOwnersCleanupVerified'])
        self.assertFalse(observations[0][1]['candidateAcceptance'])


if __name__ == '__main__':
    unittest.main()
