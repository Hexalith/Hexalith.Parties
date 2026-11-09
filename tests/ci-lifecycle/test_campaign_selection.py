"""Resource-free campaign and bounded diagnostic controls; no qualification credit."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

SPEC = importlib.util.spec_from_file_location('campaign_selection', Path(__file__).resolve().parents[2] / 'scripts/qualify-disposable-ci.py')
Q = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(Q)


class CampaignSelectionTests(unittest.TestCase):
    def controller(self):
        value = object.__new__(Q.Qualification)
        value.journal = {'observations': []}
        value.save = Mock()
        value.observe = lambda kind, **fields: value.journal['observations'].append({'kind': kind, **fields})
        value.ordinary = Mock()
        value.retry_with_peer = Mock()
        value.partial_startup = Mock()
        value.finalize = Mock(return_value=0)
        return value

    def test_focused_selection_executes_required_cases_and_never_claims_full_matrix(self):
        value = self.controller()
        self.assertEqual(Q.run_matrix(value, selection='focused-ci'), 0)
        value.retry_with_peer.assert_not_called()
        self.assertEqual(value.ordinary.call_count, 2)
        value.partial_startup.assert_called_once()
        campaign = value.journal['campaign']
        self.assertEqual(campaign['selection'], 'focused-ci')
        self.assertEqual(campaign['completedCases'], campaign['selectedCases'])
        self.assertEqual(campaign['selectedRequiredCount'], 3)
        self.assertEqual(campaign['fullRequiredCount'], 4)
        self.assertFalse(campaign['fullMatrixPassed'])
        self.assertEqual(campaign['status'], 'selected-passed')
        self.assertFalse(campaign['candidateAcceptance'])

    def test_full_matrix_keeps_lock_retry_peer_and_partial_startup(self):
        value = self.controller()
        Q.run_matrix(value)
        value.retry_with_peer.assert_called_once()
        self.assertEqual(value.journal['campaign']['completedCases'], list(Q.MATRIX_CASES))
        self.assertTrue(value.journal['campaign']['fullMatrixPassed'])

    def test_missing_case_or_finalizer_failure_never_passes(self):
        for fail_cleanup in (False, True):
            with self.subTest(cleanup=fail_cleanup):
                value = self.controller()
                if fail_cleanup:
                    value.finalize.return_value = 1
                else:
                    value.partial_startup.side_effect = TimeoutError('private-path credential=value')
                with self.assertRaises((ValueError, TimeoutError)):
                    Q.run_matrix(value, selection='focused-ci')
                self.assertFalse(value.journal['campaign']['fullMatrixPassed'])
                self.assertEqual(value.journal['campaign']['status'], 'incomplete')
                value.finalize.assert_called_once()
                self.assertNotIn('credential', json.dumps(value.journal))

    def test_unknown_selection_is_refused_before_any_scenario(self):
        value = self.controller()
        with self.assertRaises(ValueError):
            Q.run_matrix(value, selection='all-but-slow')
        value.ordinary.assert_not_called()

    def test_metadata_is_fixed_bounded_and_never_publishes_exception_payload(self):
        for error in (ValueError('secret=' + 'x' * 10000), FileNotFoundError('/private/cache'),
                      PermissionError('/private/reader'), TimeoutError('private acknowledgement')):
            record = Q.failure_metadata(error)
            self.assertRegex(record['failureCode'], r'^HXLQ[0-9]{3}$')
            self.assertLess(len(json.dumps(record)), 256)
            self.assertNotIn('private', json.dumps(record))
            self.assertNotIn('secret', json.dumps(record))
        self.assertEqual(Q.failure_metadata(OSError('secret'), 'remove-reader')['failureCode'], 'HXLQ106')

    def test_missing_partial_linked_and_nonexecutable_runtime_cache_refuse(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaises(ValueError) as caught:
                Q.verify_runtime_cache(root, '13.2.2')
            self.assertEqual(Q.failure_metadata(caught.exception)['failureCode'], 'HXLQ108')
            dcp = root / 'aspire.hosting.orchestration.linux-x64/13.2.2/tools/dcp'
            dashboard = root / 'aspire.dashboard.sdk.linux-x64/13.2.2/tools/Aspire.Dashboard'
            for path in (dcp, dashboard):
                path.parent.mkdir(parents=True)
                path.write_bytes(b'controlled fixture, never executed')
                path.chmod(0o700)
            self.assertEqual(len(Q.verify_runtime_cache(root, '13.2.2')), 2)
            dashboard.unlink()
            with self.assertRaises(ValueError):
                Q.verify_runtime_cache(root, '13.2.2')
            dashboard.symlink_to(dcp)
            with self.assertRaises(ValueError):
                Q.verify_runtime_cache(root, '13.2.2')
            dashboard.unlink()
            dashboard.write_bytes(b'not executable')
            with self.assertRaises(ValueError):
                Q.verify_runtime_cache(root, '13.2.2')
            with self.assertRaises(ValueError):
                Q.verify_runtime_cache(root, '../foreign')

    def test_each_real_lock_stage_requires_its_exact_refusal_code(self):
        value = object.__new__(Q.Qualification)
        owner = {'runId': 'a' * 32}
        value.issued = lambda item: owner
        value.containers = Mock()
        for tag, code in [('withdrawal-lock-refusal', 'HXR037'), ('provisioning-lock-refusal', 'HXR023')]:
            for observed in (code, 'HXR999', None):
                with self.subTest(stage=tag, code=observed):
                    value.tool = Mock(return_value=(Mock(returncode=3), {'outcome': {'ruleId': observed}}))
                    with patch.object(Q.HELPER, 'cleanup_verified', return_value=False):
                        if observed == code:
                            value.down({}, tag, expected=False)
                        else:
                            with self.assertRaises(ValueError):
                                value.down({}, tag, expected=False)
        value.containers.assert_not_called()

    def test_missing_launch_acknowledgement_has_specific_fixed_code(self):
        with tempfile.TemporaryDirectory() as directory:
            value = object.__new__(Q.Qualification)
            value.temporary = Path(directory)
            item = {'label': 'retry', 'receipt': str(Path(directory) / 'retry/receipt.json'),
                    'publicResult': str(Path(directory) / 'retry/public-result.json'),
                    'launchRecord': str(Path(directory) / 'retry/launch.json')}
            with patch.object(Q.time, 'monotonic', side_effect=[0, 6]):
                with self.assertRaises(ValueError) as caught:
                    value.recover_launch(item)
            self.assertEqual(Q.failure_metadata(caught.exception)['failureCode'], 'HXLQ107')


if __name__ == '__main__':
    unittest.main()
