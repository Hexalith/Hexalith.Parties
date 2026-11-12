"""Resource-free lifecycle controls. Test doubles supply no hosted execution or qualification credit."""
import copy
from datetime import datetime, timedelta, timezone
import importlib.util
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

SPEC = importlib.util.spec_from_file_location('part3_lifecycle', Path(__file__).resolve().parents[2] / 'scripts/qualify-disposable-ci.py')
Q = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(Q)
CONTEXT = {'kind': 'local-control-no-hosted-credit'}


def prepared(root):
    value = object.__new__(Q.Qualification)
    value.temporary = Q.private_directory(root / 'controller')
    value.evidence = Q.private_directory(root / 'retained')
    value.context = CONTEXT
    value.children = {}
    value.journal_path = value.temporary / 'journal.json'
    owner = {'runId': 'a' * 32, 'environmentId': 'b' * 32, 'stateDirectory': str(root / 'state'), 'executionContext': CONTEXT}
    item = {'label': 'attached-owner'}
    value.journal = {'source': 'c' * 40, 'owners': [item], 'attachedRuns': [], 'observations': []}
    value.issued = lambda _: owner
    value.diagnostics = lambda _: root / 'diagnostics'
    Q.private_directory(value.diagnostics(item))
    clock = datetime.now(timezone.utc) - timedelta(seconds=2)
    entry = {'label': 'attached-failure-child', 'kind': 'attached-suite', 'ownerLabel': item['label'], 'ownerRunId': owner['runId'],
             'environmentId': owner['environmentId'], 'executionContext': CONTEXT, 'source': value.journal['source'],
             'profile': Q.ATTACHED_PROFILE, 'holdSeconds': 180, 'requestedAtUtc': clock.isoformat(),
             'manifestHash': 'd' * 64, 'entryHash': 'e' * 64, 'beforeAttachmentIds': [],
             'ownerWorkspace': str(root / 'workspace')}
    directory = Q.private_directory(value.temporary / entry['label'])
    entry.update(receipt=str(directory / 'receipt.json'), publicResult=str(directory / 'public-result.json'), launchRecord=str(directory / 'launch.json'))
    hold = {'attachedRunId': 'f' * 32, 'originalAttachmentRequestId': '1' * 32, 'ownerRunId': owner['runId'],
            'environmentId': owner['environmentId'], 'requestedHoldSeconds': 180, 'effectiveHoldSeconds': 180,
            'acceptedAtUtc': (clock + timedelta(seconds=1)).isoformat(), 'expiresAtUtc': (clock + timedelta(seconds=181)).isoformat(),
            'execution': {'profile': Q.ATTACHED_PROFILE, 'mode': 'package', 'manifestHash': entry['manifestHash'].upper(),
                          'entryHash': entry['entryHash'].upper(), 'requestedAtUtc': (clock + timedelta(seconds=.5)).isoformat()}}
    Q.write(value.diagnostics(item) / 'attachments.json', {'ownerRunId': owner['runId'], 'attachments': [hold]})
    value.journal['attachedRuns'].append(entry)
    value.save()
    return value, owner, item, entry, hold


class AttachedAdmissionTests(unittest.TestCase):
    def test_recovery_uses_exact_admission_without_an_attached_owner_receipt(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, owner, item, entry, hold = prepared(Path(temporary))
            self.assertFalse(Path(entry['receipt']).exists())
            self.assertEqual(value.attached_admission(entry), hold)
            saved = Q.read(value.journal_path)['attachedRuns'][0]
            self.assertEqual(saved['admission'], hold)
            self.assertEqual(saved['ownerRunId'], owner['runId'])

    def test_foreign_mismatched_ambiguous_and_renewed_admissions_refuse(self):
        mutations = {
            'owner': lambda h: h.update(ownerRunId='2' * 32),
            'environment': lambda h: h.update(environmentId='2' * 32),
            'manifest': lambda h: h['execution'].update(manifestHash='2' * 64),
            'entry': lambda h: h['execution'].update(entryHash='2' * 64),
            'mode': lambda h: h['execution'].update(mode='source'),
            'hold': lambda h: h.update(effectiveHoldSeconds=240),
            'expiry': lambda h: h.update(expiresAtUtc=(Q.utc(h['expiresAtUtc']) + timedelta(seconds=1)).isoformat()),
            'before-launch': lambda h: h['execution'].update(requestedAtUtc='2020-01-01T00:00:00Z'),
        }
        for fault, mutate in mutations.items():
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temporary:
                value, _, item, entry, hold = prepared(Path(temporary))
                mutate(hold)
                Q.write(value.diagnostics(item) / 'attachments.json', {'ownerRunId': entry['ownerRunId'], 'attachments': [hold]})
                with self.assertRaises(ValueError):
                    value.attached_admission(entry)
        with tempfile.TemporaryDirectory() as temporary:
            value, _, item, entry, hold = prepared(Path(temporary))
            other = {**hold, 'attachedRunId': '2' * 32, 'originalAttachmentRequestId': '3' * 32}
            Q.write(value.diagnostics(item) / 'attachments.json', {'ownerRunId': entry['ownerRunId'], 'attachments': [hold, other]})
            with self.assertRaisesRegex(ValueError, 'Ambiguous'):
                value.attached_admission(entry)

    def test_job_revision_profile_and_existing_admission_cannot_be_rebound(self):
        for key, wrong in [('executionContext', {'kind': 'foreign'}), ('source', '2' * 40), ('profile', 'other')]:
            with self.subTest(field=key), tempfile.TemporaryDirectory() as temporary:
                value, _, _, entry, _ = prepared(Path(temporary))
                entry[key] = wrong
                with self.assertRaises(ValueError):
                    value.attached_admission(entry)
        with tempfile.TemporaryDirectory() as temporary:
            value, _, _, entry, hold = prepared(Path(temporary))
            entry['beforeAttachmentIds'] = [hold['attachedRunId']]
            self.assertIsNone(value.attached_admission(entry, required=False))
            with self.assertRaises(ValueError):
                value.attached_admission(entry)

    def test_attached_gate_recovery_binds_actual_process_record_without_receipt(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, _, _, entry, _ = prepared(Path(temporary))
            record = {key: entry[key] for key in ('label', 'receipt', 'publicResult')}
            record.update(pid=1234, processGroup=1234, birth='5678', gateStatus='closed')
            Q.write(Path(entry['launchRecord']), record)
            with patch.object(Q, 'birth', return_value=None):
                self.assertEqual(value.recover_launch(entry), record)
            self.assertFalse(Path(entry['receipt']).exists())

    def test_no_admission_after_release_is_unverified_while_closed_gate_is_unallocated(self):
        for released in (False, True):
            with self.subTest(released=released), tempfile.TemporaryDirectory() as temporary:
                value, _, item, entry, _ = prepared(Path(temporary))
                Q.write(value.diagnostics(item) / 'attachments.json', {'ownerRunId': entry['ownerRunId'], 'attachments': []})
                record = {key: entry[key] for key in ('label', 'receipt', 'publicResult')}
                record.update(pid=1234, processGroup=1234, birth='5678', gateStatus='released' if released else 'closed')
                Q.write(Path(entry['launchRecord']), record)
                with patch.object(Q, 'birth', return_value=None):
                    if released:
                        with self.assertRaisesRegex(ValueError, 'admission'):
                            value.finalize_attached(entry)
                    else:
                        value.finalize_attached(entry)

    def test_post_constructor_interruption_recovers_the_real_attached_gate_without_receipt(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            value, _, item, entry, _ = prepared(root)
            Q.write(value.diagnostics(item) / 'attachments.json', {'ownerRunId': entry['ownerRunId'], 'attachments': []})
            marker = root / 'payload-executed'
            script = Path(Q.__file__)
            harmless = [sys.executable, '-c', f'from pathlib import Path; Path({str(marker)!r}).write_text("executed")']
            source = ('import importlib.util; '
                      f'spec=importlib.util.spec_from_file_location("gate", {str(script)!r}); '
                      'q=importlib.util.module_from_spec(spec); spec.loader.exec_module(q); '
                      f'raise SystemExit(q.gated_exec({entry!r}, {harmless!r}))')
            real_popen = subprocess.Popen
            children = []
            def lose_result(_command, **options):
                children.append(real_popen([sys.executable, '-B', '-c', source], **options))
                raise OSError('local post-constructor interruption')
            try:
                with patch.object(Q.subprocess, 'Popen', side_effect=lose_result):
                    with self.assertRaises(OSError):
                        value.launch(entry)
                value.finalize_attached(entry)
                self.assertNotEqual(children[0].wait(timeout=5), 0)
                self.assertFalse(marker.exists())
                self.assertFalse(Path(entry['receipt']).exists())
                self.assertEqual(entry['pid'], children[0].pid)
            finally:
                for child in children:
                    if child.poll() is None:
                        child.kill()
                    child.wait(timeout=5)
                    if child.stdin is not None:
                        child.stdin.close()

    def test_attached_intent_write_failure_cannot_create_or_release_a_process(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, owner, item, _, _ = prepared(Path(temporary))
            value.journal['attachedRuns'] = []
            value.state = lambda _: {'workspace': str(Path(temporary) / 'workspace')}
            with patch.object(value, 'save', side_effect=OSError('unit save fault')), patch.object(value, 'launch') as launch:
                with self.assertRaises(OSError):
                    value.begin_attached(item, 'attached-failure-child')
                launch.assert_not_called()


class AttachedResultTests(unittest.TestCase):
    def result(self, root):
        value, owner, item, entry, hold = prepared(root)
        directory = Q.private_directory(Path(owner['stateDirectory']) / 'diagnostics' / hold['attachedRunId'])
        result = {'schema': 'hexalith.test-result.v1', 'policyId': Q.POLICY,
                  'bindings': {'run': {'role': 'attached', 'runId': hold['attachedRunId'], 'attachedRunId': hold['attachedRunId'],
                                       'ownerRunId': owner['runId'], 'environmentId': owner['environmentId']},
                               'environment': {'executionContext': CONTEXT},
                               'attachments': {'holds': [{'attachedRunId': hold['attachedRunId'],
                                                         'originalAttachmentRequestId': hold['originalAttachmentRequestId'],
                                                         'expiresAt': hold['expiresAtUtc']}]}},
                  'terminal': {'outcome': 'failed', 'cause': 'test-failure', 'recordedAt': Q.now()},
                  'termination': {'ownedWork': [{'id': 'process:1234', 'status': 'terminated'}],
                                  'environmentAccess': [{'id': 'access:attached', 'status': 'terminated'}],
                                  'leftoverWorkIds': [], 'failures': []},
                  'retention': {'disposition': 'not-retained'}, 'diagnostics': []}
        report = b'<TestRun />'
        filename = 'native-' + Q.hashlib.sha256(report).hexdigest() + '.trx'
        (directory / filename).write_bytes(report)
        result['diagnostics'] = [{'id': 'native-report', 'reference': filename, 'redacted': True,
                                  'classification': 'internal', 'contentIdentity': 'sha256:' + Q.hashlib.sha256(report).hexdigest()}]
        hold.update(outcome='failed', endedAtUtc=result['terminal']['recordedAt'], endReason='terminal-outcome')
        hold['execution']['cause'] = 'test-failure'
        result['bindings']['attachments']['holds'][0].update(acceptedAt=hold['acceptedAtUtc'],
            effectiveHoldSeconds=hold['effectiveHoldSeconds'], requestedHoldSeconds=hold['requestedHoldSeconds'],
            end={'state': 'ended', 'reason': 'terminal-outcome'})
        Q.write(value.diagnostics(item) / 'attachments.json', {'ownerRunId': owner['runId'], 'attachments': [hold]})
        Q.write(directory / 'test-result.json', result)
        return value, entry, directory, result

    def test_classified_attached_result_is_retained_and_first_terminal_is_immutable(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, entry, directory, result = self.result(Path(temporary))
            self.assertEqual(value.attached_result(entry, 'failed', 'test-failure'), result)
            self.assertEqual(Q.read(value.evidence / entry['label'] / 'test-result.json'), result)
            result['terminal']['recordedAt'] = '2020-01-01T00:00:00Z'
            Q.write(directory / 'test-result.json', result)
            with self.assertRaises(ValueError):
                value.attached_result(entry)

    def test_failed_or_leftover_work_access_and_local_retention_cannot_pass(self):
        mutations = [lambda r: r['termination']['failures'].append('process:1234'),
                     lambda r: r['termination']['leftoverWorkIds'].append('process:1234'),
                     lambda r: r['termination']['ownedWork'][0].update(status='failed'),
                     lambda r: r['termination']['environmentAccess'][0].update(status='pending'),
                     lambda r: r['retention'].update(disposition='failure-retained'),
                     lambda r: r.update(diagnostics=[])]
        for index, mutate in enumerate(mutations):
            with self.subTest(fault=index), tempfile.TemporaryDirectory() as temporary:
                value, entry, directory, result = self.result(Path(temporary))
                mutate(result)
                Q.write(directory / 'test-result.json', result)
                with self.assertRaises(ValueError):
                    value.attached_result(entry)

    def test_private_access_file_or_changed_snapshot_refuses(self):
        for fault in ('credentials', 'environment', 'expiry'):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temporary:
                value, entry, directory, result = self.result(Path(temporary))
                if fault == 'credentials':
                    workspace = Q.private_directory(Path(entry['ownerWorkspace']) / 'attachments' / result['bindings']['run']['runId'])
                    (workspace / 'native-endpoint-credentials.json').write_bytes(b'local dummy private-file sentinel')
                elif fault == 'environment':
                    result['bindings']['run']['environmentId'] = '2' * 32
                else:
                    result['bindings']['attachments']['holds'][0]['expiresAt'] = '2020-01-01T00:00:00Z'
                Q.write(directory / 'test-result.json', result)
                with self.assertRaises(ValueError):
                    value.attached_result(entry)

    def test_retained_native_report_requires_its_classification_and_content_identity(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, entry, directory, result = self.result(Path(temporary))
            report = b'<TestRun />'
            filename = 'native-' + Q.hashlib.sha256(report).hexdigest() + '.trx'
            (directory / filename).write_bytes(report)
            result['diagnostics'] = [{'id': 'native-report', 'reference': filename, 'redacted': True, 'classification': 'internal',
                                      'contentIdentity': 'sha256:' + Q.hashlib.sha256(report).hexdigest()}]
            Q.write(directory / 'test-result.json', result)
            value.attached_result(entry)
            self.assertEqual((value.evidence / entry['label'] / filename).read_bytes(), report)
            result['diagnostics'][0]['contentIdentity'] = 'sha256:' + '0' * 64
            Q.write(directory / 'test-result.json', result)
            with self.assertRaises(ValueError):
                value.attached_result(entry)


    def test_retention_uses_exact_held_buffers_after_replacement_or_disappearance(self):
        for disappear in (False, True):
            with self.subTest(disappear=disappear), tempfile.TemporaryDirectory() as temporary:
                value, entry, directory, result = self.result(Path(temporary))
                report_name = result['diagnostics'][0]['reference']
                expected = {name: (directory / name).read_bytes() for name in ('test-result.json', report_name)}
                actual_read = Q.read_bytes
                calls = {}
                def held(path, *args):
                    path = Path(path)
                    body = actual_read(path, *args)
                    if path.parent == directory and path.name in expected:
                        calls[path.name] = calls.get(path.name, 0) + 1
                        if disappear: path.unlink()
                        else: path.write_bytes(b'replaced unvalidated input')
                    return body
                with patch.object(Q, 'read_bytes', side_effect=held):
                    value.attached_result(entry)
                for name, body in expected.items():
                    self.assertEqual(calls[name], 1)
                    self.assertEqual((value.evidence / entry['label'] / name).read_bytes(), body)

    def test_missing_diagnostic_is_failure_and_private_artifacts_cannot_be_mislabeled(self):
        for name in ('missing', 'native-endpoint-credentials.json', 'test-environment.json'):
            with self.subTest(reference=name), tempfile.TemporaryDirectory() as temporary:
                value, entry, directory, result = self.result(Path(temporary))
                if name == 'missing':
                    (directory / result['diagnostics'][0]['reference']).unlink()
                else:
                    data = b'local dummy private access sentinel'
                    (directory / name).write_bytes(data)
                    result['diagnostics'].append({'id': 'lifecycle', 'reference': name, 'classification': 'internal', 'redacted': True,
                                                  'contentIdentity': 'sha256:' + Q.hashlib.sha256(data).hexdigest()})
                    Q.write(directory / 'test-result.json', result)
                with self.assertRaises((ValueError, FileNotFoundError)):
                    value.attached_result(entry)
                self.assertFalse((value.evidence / entry['label']).exists())

    def test_dangling_private_access_links_are_remaining_entries(self):
        for name in ('native-endpoint-credentials.json', 'test-environment.json'):
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                value, entry, directory, result = self.result(Path(temporary))
                workspace = Q.private_directory(Path(entry['ownerWorkspace']) / 'attachments' / result['bindings']['run']['runId'])
                (workspace / name).symlink_to(workspace / 'missing-target')
                with self.assertRaisesRegex(ValueError, 'private access'):
                    value.attached_result(entry)

    def test_intentional_failure_requires_its_failed_test_assertion_marker(self):
        for fault in ('none', 'service-failure', 'passed-test', 'different-test'):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temporary:
                value, entry, directory, result = self.result(Path(temporary))
                entry['intentionalFailureExpected'] = True
                message = Q.INTENTIONAL_FAILURE + result['bindings']['run']['runId']
                if fault == 'service-failure': message = 'The service returned 503.'
                outcome = 'Passed' if fault == 'passed-test' else 'Failed'
                test = 'OtherTest' if fault == 'different-test' else 'ReadRealServicesThenObserveAttachedControl'
                report = f'<TestRun><Results><UnitTestResult testName="{test}" outcome="{outcome}"><Output><ErrorInfo><Message>{message}</Message></ErrorInfo></Output></UnitTestResult></Results></TestRun>'.encode()
                digest = Q.hashlib.sha256(report).hexdigest()
                filename = 'native-' + digest + '.trx'
                (directory / filename).write_bytes(report)
                result['diagnostics'] = [{'id': 'native-report', 'reference': filename, 'classification': 'internal', 'redacted': True,
                                          'contentIdentity': 'sha256:' + digest}]
                Q.write(directory / 'test-result.json', result)
                if fault == 'none': value.attached_result(entry)
                else:
                    with self.assertRaisesRegex(ValueError, 'intentional assertion'):
                        value.attached_result(entry)


class TimeoutAndCancellationTests(unittest.TestCase):
    def test_timeout_requires_original_clock_duration_and_override(self):
        clock = datetime.now(timezone.utc) - timedelta(seconds=700)
        original = {'createdAt': clock.isoformat(), 'startupDeadlineUtc': (clock + timedelta(seconds=600)).isoformat(),
                    'effectiveDeadlineSeconds': 600, 'effectiveDeadlineIsOverride': False}
        budget = {'origin': 'environment-requested', 'originAt': original['createdAt'], 'expiresAt': original['startupDeadlineUtc'],
                  'effectiveSeconds': 600, 'actualDurationSeconds': 601, 'override': {'state': 'not-overridden'}}
        result = {'bindings': {'deadlines': {'startup': budget}},
                  'terminal': {'outcome': 'failed', 'cause': 'startup-timeout', 'recordedAt': (clock + timedelta(seconds=601)).isoformat()}}
        self.assertEqual(Q.startup_timeout_observation(result, original), budget)
        for key, wrong in [('effectiveSeconds', 601), ('actualDurationSeconds', 599), ('expiresAt', clock.isoformat()),
                           ('override', {'state': 'declared', 'requestedSeconds': 600, 'justification': 'new'})]:
            changed = copy.deepcopy(result)
            changed['bindings']['deadlines']['startup'][key] = wrong
            with self.subTest(key=key), self.assertRaises(ValueError):
                Q.startup_timeout_observation(changed, original)
        original.update(effectiveDeadlineIsOverride=True, effectiveDeadlineJustification='original human-owned declaration')
        budget['override'] = {'state': 'declared', 'requestedSeconds': 600, 'justification': original['effectiveDeadlineJustification']}
        Q.startup_timeout_observation(result, original)
        budget['override']['justification'] = 'substitution'
        with self.assertRaises(ValueError):
            Q.startup_timeout_observation(result, original)

    def test_failure_then_operator_cancellation_preserves_failure_without_cancellation_credit(self):
        clock = datetime.now(timezone.utc) - timedelta(seconds=10)
        owner = {'runId': 'a' * 32}
        first = {'runId': owner['runId'], 'ownerId': owner['runId'], 'sourceMappingHash': 'b' * 64,
                 'terminalStatus': 'failed', 'terminalOutcome': {'exitCode': 'ProductOrTest'}, 'terminalAtUtc': clock.isoformat(), 'terminalCause': 'test-failure'}
        item = {'runId': owner['runId'], 'cancellationPhase': 'failure', 'failureBeforeCancellation': first,
                'cancellationReadyAtUtc': (clock + timedelta(seconds=1)).isoformat(),
                'cancellationSignals': [{'signal': signal.SIGTERM, 'observedAtUtc': (clock + timedelta(seconds=2)).isoformat()}]}
        Q.cancellation_phase(item, owner, CONTEXT)
        self.assertEqual(Q.cancellation_terminal(first, item, first), 'product-failure-before-observed-signal')
        changed = {**first, 'terminalAtUtc': (clock + timedelta(seconds=3)).isoformat()}
        with self.assertRaises(ValueError):
            Q.cancellation_terminal(changed, item, first)
        item['cancellationReadyAtUtc'] = (clock - timedelta(seconds=1)).isoformat()
        with self.assertRaises(ValueError):
            Q.cancellation_phase(item, owner, CONTEXT)

    def test_every_individual_case_retains_exact_accounting_and_finalization(self):
        for case in Q.MATRIX_CASES:
            with self.subTest(case=case):
                value = object.__new__(Q.Qualification)
                value.journal = {'observations': []}
                value.observe = Mock()
                value.checkpoint = Mock()
                for method in ('ordinary', 'retry_with_peer', 'partial_startup', 'collision', 'startup_timeout', 'attached_case', 'invalid_handoff'):
                    setattr(value, method, Mock())
                value.finalize = Mock(return_value=0)
                self.assertEqual(Q.run_matrix(value, case), 0)
                self.assertEqual(value.journal['campaign']['completedCases'], [case])
                self.assertFalse(value.journal['campaign']['fullMatrixPassed'])
                value.finalize.assert_called_once()

    def test_attached_finalization_failure_does_not_skip_any_owner_cleanup(self):
        value = object.__new__(Q.Qualification)
        value.journal = {'owners': [{'label': 'first'}, {'label': 'second'}], 'attachedRuns': [{'label': 'child'}], 'observations': []}
        calls = []
        value.finalize_owner = lambda item, status, issued: calls.append(item['label'])
        value.finalize_attached = Mock(side_effect=ValueError('private unit failure'))
        value.observe = lambda kind, **fields: value.journal['observations'].append({'kind': kind, **fields})
        self.assertEqual(value.finalize('failure'), 1)
        self.assertEqual(calls, ['second', 'first'])
        self.assertEqual(value.journal['observations'][-1]['failures'][0]['label'], 'child')
        self.assertNotIn('private', str(value.journal))

class ScenarioOrchestrationTests(unittest.TestCase):
    def attached(self, root, scenario='owner-cancellation', fault=None, kind='ci'):
        value, entry, directory, result = AttachedResultTests().result(root)
        item = value.journal['owners'][0]
        owner = value.issued(item)
        owner['executionContext'] = value.context = {'kind': kind, 'testControl': 'no-hosted-credit'}
        entry['executionContext'] = value.context
        result['bindings']['environment']['executionContext'] = value.context
        hold = Q.read(value.diagnostics(item) / 'attachments.json')['attachments'][0]
        expected = ('ci-job-cancelled' if kind == 'ci' else 'owner-cancelled') if scenario == 'owner-cancellation' else 'hold-expired'
        if scenario == 'attached-failure': expected = 'test-failure'
        observed = 'owner-cancelled' if fault == 'wrong-cause' else expected
        result['terminal'].update(outcome='failed' if scenario == 'attached-failure' else 'withdrawn', cause=observed)
        hold.update(outcome=result['terminal']['outcome'], endReason='terminal-outcome' if scenario == 'attached-failure' else observed)
        hold['execution'].update(cause=observed, exitCode=4 if scenario == 'attached-failure' else 130, ruleId='HXT007' if scenario == 'attached-failure' else 'HXC130')
        own = result['bindings']['attachments']['holds'][0]
        own['end']['reason'] = hold['endReason']
        if scenario == 'hold-expiry':
            accepted = datetime.now(timezone.utc) - timedelta(seconds=121)
            hold.update(acceptedAtUtc=accepted.isoformat(), expiresAtUtc=(accepted + timedelta(seconds=120)).isoformat(), effectiveHoldSeconds=120, requestedHoldSeconds=120, endReason='expired')
            entry.update(holdSeconds=120, requestedAtUtc=(accepted - timedelta(seconds=1)).isoformat())
            own.update(acceptedAt=hold['acceptedAtUtc'], expiresAt=hold['expiresAtUtc'], effectiveHoldSeconds=120, requestedHoldSeconds=120, end={'state': 'ended', 'reason': 'expired'})
            result['terminal']['recordedAt'] = hold['expiresAtUtc']
        hold['endedAtUtc'] = result['terminal']['recordedAt']
        if scenario == 'attached-failure':
            message = 'Unrelated service failure after readiness' if fault == 'service-failure' else Q.INTENTIONAL_FAILURE + hold['attachedRunId']
            report = f'<TestRun><UnitTestResult testName="ReadRealServicesThenObserveAttachedControl" outcome="Failed"><Output><ErrorInfo><Message>{message}</Message></ErrorInfo></Output></UnitTestResult></TestRun>'.encode()
            digest = Q.hashlib.sha256(report).hexdigest()
            filename = 'native-' + digest + '.trx'
            (directory / filename).write_bytes(report)
            result['diagnostics'] = [{'id': 'native-report', 'classification': 'internal', 'redacted': True, 'reference': filename, 'contentIdentity': 'sha256:' + digest}]
        Q.write(value.diagnostics(item) / 'attachments.json', {'ownerRunId': owner['runId'], 'attachments': [hold]})
        Q.write(directory / 'test-result.json', result)
        entry['admission'] = hold
        item.update(receipt=str(root / 'receipt.json'), publicResult=str(root / 'public.json'))
        Q.write(Path(item['receipt']), {'localTestReceipt': True})
        cancelled = scenario == 'owner-cancellation'
        exit_code, outcome = (130, 'Cancelled') if cancelled else (0, 'Success')
        if fault == 'command-exit': exit_code = 3
        diagnostics = [{'ruleId': 'HXT008', 'category': 'EvidencePolicy'}] if fault == 'command-diagnostic' else []
        Q.write(Path(item['publicResult']), {'runId': owner['runId'], 'outcome': {'exitCode': outcome}, 'diagnostics': diagnostics})
        owner_child = Mock(returncode=exit_code)
        owner_child.poll.return_value = exit_code
        attached_exit = 4 if scenario == 'attached-failure' else 130
        attached_child = Mock(returncode=attached_exit)
        attached_child.poll.return_value = attached_exit
        Q.write(Path(entry['publicResult']), {'runId': hold['attachedRunId'], 'outcome': {'exitCode': 'ProductOrTest' if scenario == 'attached-failure' else 'Cancelled', 'category': 'ProductOrTest' if scenario == 'attached-failure' else 'Cancelled', 'ruleId': hold['execution']['ruleId']}, 'diagnostics': [{'category': 'None', 'ruleId': 'HXI013'}]})
        value.children = {item['label']: owner_child, entry['label']: attached_child}
        value.begin = Mock(return_value=item)
        value.ready = Mock()
        value.begin_attached = Mock(return_value=entry)
        value.attached_control = Mock()
        value.control = Mock()
        value.process_signal = Mock()
        value.state = Mock(return_value={'testRun': None})
        value.down = Mock()
        value.invalid_lineage = Mock()
        first = {'runId': owner['runId'], 'ownerId': owner['runId'], 'terminalStatus': 'cancelled' if cancelled else 'completed',
                 'terminalOutcome': {'exitCode': outcome}, 'terminalAtUtc': Q.now(), 'sourceMappingHash': 'a' * 64}
        value.archived = Mock(return_value=first)
        scope = {'containers': {'container-one': 'original-start', 'container-two': 'original-start'}, 'processes': {'apphost': {'processId': 17, 'birth': 'original-birth'}}}
        after = copy.deepcopy(scope)
        if fault == 'stopped-container': del after['containers']['container-one']
        if fault == 'missing-process': after['processes'] = {}
        value.running_owner_inventory = Mock(side_effect=[scope, after, after])
        value.peer_scope = Mock(return_value={'peer': 'unchanged'})
        if scenario == 'hold-expiry':
            peer = {'label': 'expiry-peer', 'receipt': item['receipt'], 'publicResult': str(root / 'peer-public.json')}
            Q.write(Path(peer['publicResult']), {'runId': owner['runId'], 'outcome': {'exitCode': 'Success'}, 'diagnostics': []})
            value.begin = Mock(side_effect=[item, peer])
            peer_child = Mock(returncode=0)
            peer_child.poll.return_value = 0
            value.children[peer['label']] = peer_child
        calls = Mock()
        calls.attach_mock(value.begin, 'begin')
        calls.attach_mock(value.ready, 'ready')
        calls.attach_mock(value.begin_attached, 'attached')
        value.owner_start_calls = calls
        return value, entry

    def test_ci_owner_cancellation_requires_actual_cause_running_originals_and_original_exit(self):
        for fault in (None, 'wrong-cause', 'stopped-container', 'missing-process', 'command-exit', 'command-diagnostic'):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temporary:
                value, entry = self.attached(Path(temporary), fault=fault)
                if fault is None:
                    value.attached_case('owner-cancellation')
                    self.assertEqual(entry['firstTerminal']['cause'], 'ci-job-cancelled')
                    value.down.assert_called_once()
                    value.process_signal.assert_called_once()
                else:
                    with self.assertRaises(ValueError): value.attached_case('owner-cancellation')
                    value.down.assert_not_called()

    def test_local_owner_cancellation_uses_its_actual_local_cause(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, entry = self.attached(Path(temporary), kind='local')
            value.attached_case('owner-cancellation')
            self.assertEqual(entry['firstTerminal']['cause'], 'owner-cancelled')

    def test_attached_failure_requires_intentional_report_and_preserves_original_command_failure(self):
        for fault in (None, 'service-failure', 'command-exit', 'command-diagnostic'):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temporary:
                value, _ = self.attached(Path(temporary), 'attached-failure', fault)
                if fault is None:
                    value.attached_case('attached-failure')
                    value.down.assert_called_once()
                else:
                    with self.assertRaises(ValueError): value.attached_case('attached-failure')
                    value.down.assert_not_called()

    def test_hold_expiry_orchestration_rechecks_original_owner_and_peer(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, _ = self.attached(Path(temporary), 'hold-expiry')
            value.attached_case('hold-expiry')
            self.assertEqual([record[0] for record in value.owner_start_calls.mock_calls[:5]], ['begin', 'begin', 'ready', 'ready', 'attached'])
            value.invalid_lineage.assert_called_once_with(value.journal['owners'][0], value.journal['attachedRuns'][0]['admission'], expected_reason='attachment-expired')
            self.assertGreaterEqual(value.peer_scope.call_count, 3)
            value.down.assert_called_once()

    def test_collision_requires_exact_blocker_and_unchanged_peer(self):
        for fault in (None, 'wrong-rule', 'wrong-owner', 'changed-peer', 'wrong-bound-port', 'remote-endpoint', 'not-listening'):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                value = object.__new__(Q.Qualification)
                item, peer = {'label': 'collision-owner'}, {'label': 'collision-peer'}
                challenger = {'label': 'collision-challenger', 'publicResult': str(root / 'collision.json')}
                owner = {'runId': 'a' * 32}
                Q.write(root / 'plan.json', {'ports': {'eventStoreHttp': 23456}})
                host = 'eventstore.example' if fault == 'remote-endpoint' else 'localhost'
                Q.write(root / 'readiness.json', {'eventStoreEndpoint': f'http://{host}:{40645 if fault == "wrong-bound-port" else 23456}'})
                connection = unittest.mock.MagicMock(side_effect=ConnectionRefusedError('closed') if fault == 'not-listening' else None)
                public = {'outcome': {'ruleId': 'HXR031' if fault != 'wrong-rule' else 'HXR020'},
                          'diagnostics': [{'ruleId': 'HXR031', 'message': 'blocked by ' + ('b' * 32 if fault == 'wrong-owner' else owner['runId'])}]}
                Q.write(Path(challenger['publicResult']), public)
                value.begin = Mock(side_effect=[item, peer, challenger])
                value.ready = Mock()
                calls = Mock()
                calls.attach_mock(value.begin, 'begin')
                calls.attach_mock(value.ready, 'ready')
                value.issued = Mock(return_value=owner)
                value.state = Mock(return_value={'workspace': str(root)})
                value.children = {entry['label']: Mock() for entry in (item, peer, challenger)}
                value.capture_result = lambda entry: 3 if entry == challenger else 0
                value.peer_scope = Mock(side_effect=[{'owner': 'original'}, {'peer': 'original'}, {'owner': 'original'}, {'peer': 'changed' if fault == 'changed-peer' else 'original'}])
                value.control = Mock()
                if fault is None:
                    with patch.object(Q.socket, 'create_connection', connection):
                        value.collision()
                    connection.assert_called_once_with(('localhost', 23456), timeout=5)
                    self.assertEqual(value.begin.call_args_list[-1], unittest.mock.call('collision-challenger', eventstore_port=23456, wait_workspace=False))
                    self.assertEqual(value.ready.call_args_list, [unittest.mock.call(item), unittest.mock.call(peer)])
                    self.assertEqual([record[0] for record in calls.mock_calls[:5]], ['begin', 'begin', 'ready', 'ready', 'begin'])
                    self.assertEqual(value.control.call_count, 2)
                elif fault in ('wrong-bound-port', 'remote-endpoint', 'not-listening'):
                    # Only a live owner endpoint on the claimed port may be challenged; a claim alone is no proof.
                    with patch.object(Q.socket, 'create_connection', connection), \
                            self.assertRaises(ConnectionRefusedError if fault == 'not-listening' else ValueError):
                        value.collision()
                    self.assertEqual(value.begin.call_count, 2)
                    value.control.assert_not_called()
                else:
                    with patch.object(Q.socket, 'create_connection', connection), self.assertRaises(ValueError):
                        value.collision()
                    value.control.assert_not_called()

    def test_startup_timeout_orchestration_preserves_actual_deadline_and_cleanup_retry(self):
        for fault in (None, 'early-success', 'changed-override', 'changed-after-down'):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                value = object.__new__(Q.Qualification)
                item = {'label': 'timeout'}
                owner = {'runId': 'a' * 32, 'stateDirectory': str(root)}
                clock = datetime.now(timezone.utc) - timedelta(seconds=700)
                original = {'createdAt': clock.isoformat(), 'startupDeadlineUtc': (clock + timedelta(seconds=600)).isoformat(),
                            'effectiveDeadlineSeconds': 600, 'effectiveDeadlineIsOverride': True, 'effectiveDeadlineJustification': 'original override'}
                result = {'terminal': {'outcome': 'failed', 'cause': 'startup-timeout', 'recordedAt': (clock + timedelta(seconds=601)).isoformat()},
                          'bindings': {'deadlines': {'startup': {'origin': 'environment-requested', 'originAt': original['createdAt'], 'expiresAt': original['startupDeadlineUtc'],
                            'effectiveSeconds': 600, 'actualDurationSeconds': 601, 'override': {'state': 'declared', 'requestedSeconds': 600, 'justification': 'changed' if fault == 'changed-override' else 'original override'}}}}}
                path = root / 'diagnostics' / owner['runId'] / 'test-result.json'
                Q.write(path, result)
                value.start = Mock(return_value=item)
                value.state = Mock(return_value=original)
                value.issued = Mock(return_value=owner)
                value.children = {'timeout': Mock()}
                value.capture_result = Mock(return_value=0 if fault == 'early-success' else 4)
                value.observe = Mock()
                value.resume_host = Mock()
                def cleanup(*args):
                    if fault == 'changed-after-down':
                        result['terminal']['recordedAt'] = Q.now()
                        Q.write(path, result)
                value.down = Mock(side_effect=cleanup)
                if fault is None:
                    value.startup_timeout()
                    value.start.assert_called_once_with('timeout', startup=True)
                    value.down.assert_called_once()
                else:
                    with self.assertRaises(ValueError): value.startup_timeout()

    def test_running_inventory_refuses_stopped_containers_and_missing_processes(self):
        value = object.__new__(Q.Qualification)
        owner = {'runId': 'a' * 32}
        value.issued = Mock(return_value=owner)
        value.state = Mock(return_value={'appHostProcessId': 123, 'readinessResults': []})
        identity = 'b' * 64
        fields = Q.json.dumps(identity) + '\ttrue\t"2026-11-11T00:00:00Z"\t' + Q.json.dumps(owner['runId'])
        with patch.object(Q.subprocess, 'check_output', side_effect=[identity, fields]), patch.object(Q, 'owned_native_process', return_value={'processId': 123, 'birth': 'original'}):
            observed = value.running_owner_inventory({})
            self.assertEqual(observed['containers'], {identity: '2026-11-11T00:00:00Z'})
        with patch.object(Q.subprocess, 'check_output', side_effect=[identity, fields.replace('\ttrue\t', '\tfalse\t')]):
            with self.assertRaises(ValueError): value.running_owner_inventory({})
        with patch.object(Q.subprocess, 'check_output', side_effect=[identity, fields]), patch.object(Q, 'owned_native_process', side_effect=ValueError('original process missing')):
            with self.assertRaises(ValueError): value.running_owner_inventory({})


if __name__ == '__main__':
    unittest.main()
