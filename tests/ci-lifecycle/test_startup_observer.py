"""Hermetic observation/ownership tests; these grant no native or CI acceptance."""
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import signal
import sys
import tempfile
import threading
import time
import unittest
from types import SimpleNamespace
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('startup_observer', ROOT / 'scripts/observe-current-owner-startup.py')
O = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(O)


class StartupObserverTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.runner = self.root / 'runner'
        self.runner.mkdir()
        self.environment = patch.dict(os.environ, {
            'GITHUB_ACTIONS': 'true', 'GITHUB_REPOSITORY': 'fixture/root', 'GITHUB_RUN_ID': '123',
            'GITHUB_RUN_ATTEMPT': '1', 'GITHUB_JOB': 'observe', 'RUNNER_ENVIRONMENT': 'github-hosted',
            'RUNNER_OS': 'Linux', 'GITHUB_WORKSPACE': str(self.root), 'RUNNER_TEMP': str(self.runner),
            'XDG_DATA_HOME': str(self.root / 'data'), 'GITHUB_SHA': 'd' * 40,
        })
        self.environment.start()
        self.addCleanup(self.environment.stop)
        self.source = patch.object(O, 'source_revision', return_value='d' * 40)
        self.source.start()
        self.addCleanup(self.source.stop)
        self.state_root = self.root / 'data/hexalith-builds/g4-runs'
        self.state_root.mkdir(parents=True)
        self.owner = {'schema': O.HELPER.SCHEMA, 'runId': 'a' * 32, 'attemptId': 'b' * 32,
                      'environmentId': 'c' * 32, 'executionContext': O.HELPER.ci_context(),
                      'stateDirectory': str(self.state_root)}
        self.receipt = self.runner / 'hexalith-ci-owner.json'
        self.workspace = self.state_root.parent / 'g4-workspaces' / self.owner['runId']
        self.catalog = self.workspace / 'platform-components' / (self.owner['environmentId'] + '-catalog')
        self.catalog.mkdir(parents=True)
        for name in (O.MANIFEST, O.ENTRY):
            p = self.root / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text('{}')
        self.state = {key: self.owner[key] for key in ('runId', 'attemptId', 'environmentId', 'executionContext')}
        self.state.update(schema='hexalith.g4-run-state.v1', workspace=str(self.workspace), manifestHash=self.digest(O.MANIFEST),
                          selectedTest={'moduleId': 'parties', 'name': O.PROFILE, 'mode': 'package', 'entry': O.ENTRY,
                                        'manifestHash': self.digest(O.MANIFEST), 'entryHash': self.digest(O.ENTRY)})
        self.write(self.receipt, self.owner)
        self.state_path = self.state_root / (self.owner['runId'] + '.json')
        self.write(self.state_path, self.state)
        self.mapping_path = self.workspace / 'source-mapping/mapping.json'
        self.mapping = {'Root': str(self.root), 'ActiveModule': 'Hexalith.Parties',
                        'Entries': [{'Identity': 'Hexalith.Parties', 'Origin': 'source', 'Path': str(self.root)}]}
        self.write(self.mapping_path, self.mapping)
        self.progress = {'RunId': self.owner['runId'], 'step': 'source-validated', 'httpStatus': None,
                         'complete': False, 'observedAtUtc': '2026-11-06T12:00:00Z'}
        self.write(self.catalog / 'preparation-progress.json', self.progress)
        self.readiness = {'schema': 'hexalith.g4-readiness-progress.v1', 'runId': self.owner['runId'], 'resources': [
            {'name': 'parties', 'kind': 'project', 'state': 'Healthy', 'detail': 'state=Running;health=Healthy',
             'observedAt': '2026-11-06T12:00:00Z', 'endpoints': [{'url': 'http://private.invalid/secret'}], 'processId': 42}]}
        self.readiness_path = self.workspace / 'readiness-progress.json'
        self.write(self.readiness_path, self.readiness)
        self.evidence = self.root / 'TestResults/startup-observer'
        self.observer = O.Observer(self.root, self.receipt, self.evidence)

    def digest(self, name):
        return hashlib.sha256((self.root / name).read_bytes()).hexdigest()

    def write(self, path, value):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value))
        path.chmod(0o600)

    def test_exact_owner_exports_only_allowlisted_metadata_and_existence(self):
        self.write(self.catalog / 'native-scope.json', {'credential': 'must-never-be-read-or-exported'})
        # Actual Platform progress files can be 0644 inside their private owning directory.
        (self.catalog / 'preparation-progress.json').chmod(0o644)
        result = self.observer.snapshot()
        self.assertTrue(result['files']['native-scope.json'])
        self.assertFalse(result['files']['parties.committed.json'])
        self.assertEqual(result['preparation']['phase'], 'source-validated')
        self.observer.samples.append(result)
        self.observer.publish()
        output = self.evidence / 'startup-observation.json'
        text = output.read_text()
        for secret in ('credential', 'must-never', 'private.invalid', 'processId', str(self.workspace)):
            self.assertNotIn(secret, text)
        self.assertEqual(output.stat().st_mode & 0o777, 0o600)
        self.assertFalse(json.loads(text)['qualificationCredit'])

    def test_actual_selected_source_bytes_accept_uppercase_durable_digests(self):
        # Match the genuine 4.44.11 state serializer using the selected repository's actual bytes.
        for name in (O.MANIFEST, O.ENTRY):
            (self.root / name).write_bytes((ROOT / name).read_bytes())
        self.state['manifestHash'] = self.digest(O.MANIFEST).upper()
        self.state['selectedTest']['manifestHash'] = self.digest(O.MANIFEST).upper()
        self.state['selectedTest']['entryHash'] = self.digest(O.ENTRY).upper()
        self.write(self.state_path, self.state)
        self.assertEqual(self.observer.snapshot()['phase'], 'observing')
        # Case may differ across independently serialized metadata; exact digest bytes must agree.
        self.state['selectedTest']['manifestHash'] = self.digest(O.MANIFEST)
        self.write(self.state_path, self.state)
        self.assertEqual(self.observer.snapshot()['phase'], 'observing')

    def test_digest_shape_and_mismatch_refuse_at_every_source_binding(self):
        for field in ('state-manifest', 'selected-manifest', 'selected-entry'):
            for value in (None, True, 64, 'a' * 63, 'g' * 64, 'a' * 64 + '\n', 'e' * 64):
                with self.subTest(field=field, value=value):
                    state = copy.deepcopy(self.state)
                    if field == 'state-manifest': state['manifestHash'] = value
                    if field == 'selected-manifest': state['selectedTest']['manifestHash'] = value
                    if field == 'selected-entry': state['selectedTest']['entryHash'] = value
                    self.write(self.state_path, state)
                    with self.assertRaises(ValueError): self.observer.snapshot()
        self.write(self.state_path, self.state)

    def test_published_preparation_bytes_export_after_all_six_participants_commit(self):
        # Exact ed991837/real local 44862 serialized shape; only the issued ID is redacted.
        body = (b'{"RunId":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","step":"memories-active-observed",'
                b'"httpStatus":null,"complete":false,"observedAtUtc":"2026-11-06T20:21:40.4627917+00:00"}')
        path = self.catalog / 'preparation-progress.json'
        path.write_bytes(body)
        for host in O.HOSTS:
            (self.catalog / (host + '.committed.json')).write_bytes(b'{}')
        result = self.observer.snapshot()
        self.assertTrue(all(result['files'][host + '.committed.json'] for host in O.HOSTS))
        self.assertEqual(result['preparation'], {'phase': 'memories-active-observed', 'status': None,
                                                'observedAt': '2026-11-06T20:21:40.462791+00:00'})

    def test_atomic_replacement_never_accepts_changed_read_and_retries_complete_new_file(self):
        path = self.catalog / 'preparation-progress.json'
        replacement = self.catalog / 'progress.tmp'
        replacement.write_bytes(path.read_bytes())
        actual_fstat = os.fstat
        calls = 0
        def replace_during_read(descriptor):
            nonlocal calls
            calls += 1
            if calls == 2:
                # Ensure the filesystem records a distinct ctime for its real atomic unlink.
                time.sleep(0.02)
                os.replace(replacement, path)
            return actual_fstat(descriptor)
        with patch.object(O.os, 'fstat', side_effect=replace_during_read):
            with self.assertRaises(O.ObservationPending): O.read(path)
        self.assertEqual(O.read(path), self.progress)

    def test_directly_created_empty_receipt_and_partial_json_remain_pending(self):
        receipt = self.catalog / 'current-owner-observation.json'
        receipt.touch()
        self.assertFalse(O.exists(receipt))
        receipt.write_bytes(b'{}')
        self.assertTrue(O.exists(receipt))
        progress = self.catalog / 'preparation-progress.json'
        for body in (b'', b'{"RunId":'):
            progress.write_bytes(body)
            with self.assertRaises(O.ObservationPending): O.read(progress)
        self.write(progress, self.progress)
        self.assertEqual(self.observer.snapshot()['preparation']['phase'], 'source-validated')

    def test_pending_metadata_is_retried_without_copying_unvalidated_values(self):
        complete = self.observer.snapshot()
        stop = SimpleNamespace(wait=lambda seconds: next(waits))
        waits = iter((False, True))
        with patch.object(self.observer, 'snapshot', side_effect=(O.ObservationPending(), complete)):
            self.observer.observe(stop)
        self.assertFalse(self.observer.failure)
        self.assertEqual(self.observer.samples[0]['phase'], 'metadata-pending')
        self.assertEqual(self.observer.samples[-1]['preparation']['phase'], 'source-validated')
        self.assertEqual(self.observer.observation_termination, 'normal-stop')

    def test_pending_metadata_cannot_extend_the_original_observer_bound(self):
        stop = SimpleNamespace(wait=lambda seconds: False)
        with patch.object(self.observer, 'snapshot', side_effect=O.ObservationPending), \
             patch.object(O.time, 'monotonic', side_effect=(0, 1201)):
            self.observer.observe(stop)
        self.assertTrue(self.observer.failure)
        self.assertEqual(self.observer.refusal_reason, 'metadata-pending-bound')
        self.assertEqual(self.observer.observation_termination, 'budget-exhausted')

    def test_waiting_for_required_metadata_reports_time_budget_exhaustion(self):
        for phase in ('awaiting-receipt', 'awaiting-state', 'awaiting-mapping'):
            with self.subTest(phase=phase):
                observer = O.Observer(self.root, self.receipt, self.evidence)
                with patch.object(observer, 'snapshot', return_value={'phase': phase}), \
                     patch.object(O.time, 'monotonic', side_effect=(0, 1201)):
                    observer.observe(SimpleNamespace(wait=lambda seconds: False))
                output = json.loads((self.evidence / 'startup-observation.json').read_text())
                self.assertEqual(output['observationTermination'], 'budget-exhausted')
                self.assertFalse(output['observerRefused'])
                self.assertEqual(output['helperOutcome'], {'returned': False})
                self.assertEqual(output['observations'][-1]['phase'], phase)

    def test_waiting_poll_budget_remains_exactly_six_hundred_samples(self):
        with patch.object(self.observer, 'snapshot', return_value={'phase': 'awaiting-receipt'}) as snapshot, \
             patch.object(O.time, 'monotonic', return_value=0):
            self.observer.observe(SimpleNamespace(wait=lambda seconds: False))
        self.assertEqual(snapshot.call_count, 600)
        output = json.loads((self.evidence / 'startup-observation.json').read_text())
        self.assertEqual(output['observationTermination'], 'budget-exhausted')
        self.assertFalse(output['observerRefused'])

    def test_actual_helper_outcomes_stay_separate_from_observer_refusal(self):
        self.observer.snapshot()
        evidence = self.root / 'TestResults/module-integration'
        self.write(evidence / 'execution.json', {'runId': self.owner['runId'], 'status': 'completed',
                   'outcome': {'exitCode': 'Success', 'phase': 'None', 'category': 'None', 'ruleId': None},
                   'diagnostics': [{'message': 'private-helper-payload'}]})
        self.write(evidence / 'cleanup.json', {'runId': self.owner['runId'],
                   'outcome': {'exitCode': 'Success', 'phase': 'Cleanup'},
                   'remainingResources': {'unverified': False, **{key: [] for key in
                       ('containerIds', 'processIds', 'workspaceIds', 'claimIds', 'discoveryFailures')}}})
        self.observer.failure = True
        self.observer.refusal_reason = 'preparation-shape'
        self.observer.finish(0)
        output = json.loads((self.evidence / 'startup-observation.json').read_text())
        self.assertTrue(output['observerRefused'])
        self.assertEqual(output['helperOutcome']['exitCode'], 0)
        self.assertEqual(output['helperOutcome']['execution.json'], {'exitCode': 'Success', 'phase': 'None'})
        self.assertTrue(output['helperOutcome']['cleanup.json']['reportedEmpty'])
        self.assertNotIn('private-helper-payload', json.dumps(output))
        self.assertFalse(output['qualificationCredit'])

    def test_foreign_helper_outcome_is_refused_without_exporting_its_fields(self):
        self.observer.snapshot()
        self.write(self.root / 'TestResults/module-integration/execution.json',
                   {'runId': 'e' * 32, 'outcome': {'exitCode': 'private-helper-payload', 'phase': 'None'}})
        self.observer.finish(4)
        output = json.loads((self.evidence / 'startup-observation.json').read_text())
        self.assertTrue(output['observerRefused'])
        self.assertEqual(output['refusalReason'], 'helper-outcome')
        self.assertEqual(output['helperOutcome']['exitCode'], 4)
        self.assertNotIn('private-helper-payload', json.dumps(output))
        self.assertNotIn('e' * 32, json.dumps(output))

    def test_foreign_receipt_job_is_refused(self):
        self.owner['executionContext'] = {**self.owner['executionContext'], 'jobId': 'foreign'}
        self.write(self.receipt, self.owner)
        with self.assertRaises(ValueError): self.observer.snapshot()
        self.assertIsNone(self.observer.owner)

    def test_foreign_or_changed_owner_metadata_is_refused(self):
        self.state['attemptId'] = 'e' * 32
        self.write(self.state_path, self.state)
        with self.assertRaises(ValueError): self.observer.snapshot()

    def test_foreign_workspace_and_changed_source_bindings_are_refused(self):
        for mutation in ('workspace', 'manifest', 'entry', 'mapping'):
            with self.subTest(mutation=mutation):
                state = copy.deepcopy(self.state)
                mapping = copy.deepcopy(self.mapping)
                if mutation == 'workspace': state['workspace'] = str(self.root / 'foreign')
                if mutation == 'manifest': state['selectedTest']['manifestHash'] = 'e' * 64
                if mutation == 'entry': state['selectedTest']['entryHash'] = 'e' * 64
                if mutation == 'mapping': mapping['Entries'][0]['Path'] = str(self.root / 'foreign')
                self.write(self.state_path, state)
                self.write(self.mapping_path, mapping)
                with self.assertRaises(ValueError): self.observer.snapshot()
        self.write(self.state_path, self.state)
        self.write(self.mapping_path, self.mapping)

    def test_foreign_progress_and_untrusted_shapes_are_refused(self):
        for change in ({'RunId': 'e' * 32}, {'step': 'arbitrary-private-value'}, {'httpStatus': True}, {'complete': True}):
            with self.subTest(change=change):
                self.write(self.catalog / 'preparation-progress.json', {**self.progress, **change})
                with self.assertRaises(ValueError): self.observer.snapshot()

    def test_foreign_readiness_and_secret_details_are_refused(self):
        for change in ({'runId': 'e' * 32}, {'resources': [{'name': 'parties', 'state': 'Healthy', 'detail': 'http://private.invalid/token'}]}):
            with self.subTest(change=change):
                self.write(self.readiness_path, {**self.readiness, **change})
                with self.assertRaises(ValueError): self.observer.snapshot()

    def test_linked_or_oversized_inputs_are_refused(self):
        target = self.root / 'foreign.json'
        self.write(target, self.readiness)
        self.readiness_path.unlink()
        self.readiness_path.symlink_to(target)
        with self.assertRaises(OSError): self.observer.snapshot()
        self.readiness_path.unlink()
        self.readiness_path.write_bytes(b' ' * (O.LIMIT + 1))
        with self.assertRaises(ValueError): self.observer.snapshot()
        self.readiness_path.unlink()
        os.link(target, self.readiness_path)
        with self.assertRaises(ValueError): self.observer.snapshot()

    def test_receipt_contents_are_never_opened_and_links_are_refused(self):
        path = self.catalog / 'parties.committed.json'
        path.write_bytes(b'not JSON and deliberately never read')
        self.assertTrue(self.observer.snapshot()['files']['parties.committed.json'])
        path.unlink()
        path.symlink_to(self.receipt)
        with self.assertRaises(ValueError): self.observer.snapshot()

    def test_checkout_revision_requires_the_real_workflow_source(self):
        self.source.stop()
        with patch.object(O.subprocess, 'run', return_value=SimpleNamespace(stdout='e' * 40)):
            with self.assertRaises(ValueError): O.source_revision(self.root)

    def test_observer_refusal_remains_redacted_and_bounded(self):
        self.write(self.readiness_path, {**self.readiness, 'runId': 'e' * 32})
        self.observer.observe(threading.Event())
        output = json.loads((self.evidence / 'startup-observation.json').read_text())
        self.assertTrue(output['observerRefused'])
        self.assertEqual(output['refusalReason'], 'readiness-shape')
        self.assertEqual(output['observationTermination'], 'refused')
        self.assertNotIn('e' * 32, json.dumps(output))
        self.assertLess((self.evidence / 'startup-observation.json').stat().st_size, O.LIMIT)

    def test_full_execution_and_cancelled_cleanup_are_delegated_to_the_released_helper(self):
        if hasattr(O.HELPER, 'write_process_record'):
            # A session-recording helper defers exact cleanup after a cancellation to the always() finalizer.
            self.assert_session_helper_delegation()
            return
        for cancelled, cleanup, expected in ((False, 0, 0), (False, 1, 1), (True, 0, 130)):
            with self.subTest(cancelled=cancelled, cleanup=cleanup):
                child = SimpleNamespace(communicate=lambda timeout: ('{"outcome":{"exitCode":"Success"}}', None),
                                        returncode=0, poll=lambda: None, send_signal=lambda number: None)
                def launch(*args, **kwargs):
                    if cancelled: signal.getsignal(signal.SIGTERM)(signal.SIGTERM, None)
                    return child
                def delegate(root, manifest, profiles, receipt, evidence):
                    self.assertEqual((manifest, profiles), (O.MANIFEST, [O.PROFILE]))
                    return O.HELPER.execute_integration(['fixed-tool'], root, receipt, evidence, 30)
                with patch.object(O, 'ROOT', self.root), patch.object(O.Observer, 'observe'), \
                     patch.object(O.HELPER, 'run_integration', side_effect=delegate), \
                     patch.object(O.HELPER.subprocess, 'Popen', side_effect=launch), \
                     patch.object(O.HELPER, 'finalize', return_value=cleanup) as finalize:
                    self.assertEqual(O.run(), expected)
                    self.assertEqual(finalize.call_count, 1)
                    self.assertEqual(finalize.call_args.kwargs['job_status'], 'cancelled' if cancelled else '')

    def assert_session_helper_delegation(self):
        tools = self.root / 'fake-tools'
        tools.mkdir()
        tool = tools / 'fixed-tool'
        tool.write_text('#!' + sys.executable + '\n' + 'import json, os, pathlib, signal, sys, time\n'
                        'def stop(number, frame):\n'
                        '    print(json.dumps({"outcome": {"exitCode": "Cancelled"}}), flush=True)\n'
                        '    sys.exit(130)\n'
                        'signal.signal(signal.SIGINT, stop)\n'
                        'if os.environ.get("FAKE_WAIT") == "1":\n'
                        '    pathlib.Path(os.environ["FAKE_READY"]).write_text("ready")\n'
                        '    while True:\n'
                        '        time.sleep(0.02)\n'
                        'print(json.dumps({"outcome": {"exitCode": "Success"}}))\n')
        tool.chmod(0o700)
        ready = self.root / 'fake-tool-ready'
        for cancelled, cleanup, expected in ((False, 0, 0), (False, 1, 1), (True, 0, 130)):
            with self.subTest(cancelled=cancelled, cleanup=cleanup):
                O.HELPER.process_record_path(self.receipt).unlink(missing_ok=True)
                ready.unlink(missing_ok=True)

                def cancel_when_ready():
                    deadline = time.monotonic() + 10
                    while not ready.exists() and time.monotonic() < deadline:
                        time.sleep(0.01)
                    os.kill(os.getpid(), signal.SIGTERM)

                def delegate(root, manifest, profiles, receipt, evidence):
                    self.assertEqual((manifest, profiles), (O.MANIFEST, [O.PROFILE]))
                    if cancelled:
                        threading.Thread(target=cancel_when_ready, daemon=True).start()
                    return O.HELPER.execute_integration(['fixed-tool'], root, receipt, evidence, 30)

                environment = {'PATH': str(tools) + os.pathsep + os.environ['PATH'], 'FAKE_WAIT': '1' if cancelled else '0',
                               'FAKE_READY': str(ready)}
                with patch.dict(os.environ, environment), patch.object(O, 'ROOT', self.root), patch.object(O.Observer, 'observe'), \
                     patch.object(O.HELPER, 'run_integration', side_effect=delegate), \
                     patch.object(O.HELPER, 'finalize', return_value=cleanup) as finalize:
                    self.assertEqual(O.run(), expected)
                    self.assertEqual(finalize.call_count, 0 if cancelled else 1)
                    if not cancelled:
                        self.assertEqual(finalize.call_args.kwargs['job_status'], '')

    def test_workflow_preserves_fixed_profile_and_always_cleanup_without_raw_uploads(self):
        workflow = (ROOT / '.github/workflows/current-owner-startup-diagnostics.yml').read_text()
        self.assertIn('workflow_dispatch:', workflow)
        self.assertIn('python3 -B scripts/observe-current-owner-startup.py', workflow)
        self.assertIn('Independent', workflow)
        self.assertIn('if: always()', workflow.split('name: Independently finalize', 1)[1])
        self.assertIn('finalize --receipt "$RUNNER_TEMP/hexalith-ci-owner.json"', workflow)
        self.assertIn('path: TestResults/startup-observer/startup-observation.json', workflow)
        self.assertNotIn('path: TestResults/module-integration', workflow)
        for bypass in ('continue-on-error:', 'secrets: inherit', 'submodules: recursive', 'GITHUB_ACTIONS:'):
            self.assertNotIn(bypass, workflow)


if __name__ == '__main__':
    unittest.main()
