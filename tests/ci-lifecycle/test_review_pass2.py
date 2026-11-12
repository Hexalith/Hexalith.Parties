"""Second review regression controls; no real-service or hosted qualification credit."""
from datetime import datetime, timedelta, timezone
import os
from pathlib import Path
import re
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import Mock, patch

import test_attached_lifecycle as lifecycle
Q = lifecycle.Q


class LaunchContractTests(unittest.TestCase):
    def test_launch_owner_preserves_explicit_port_and_attached_owner_hold_arguments(self):
        for attached in (False, True):
            with self.subTest(attached=attached), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                controller_root = root / 'hexalith-ci-qualification'
                owner = {'label': 'collision-owner'}
                child = {'label': 'expiry-child' if attached else 'collision-challenger'}
                for item in (owner, child):
                    item.update({key: str(controller_root / item['label'] / filename) for key, filename in
                                 [('receipt', 'receipt.json'), ('launchRecord', 'launch.json'), ('publicResult', 'public-result.json')]})
                context = {'test': 'local-control'}
                receipt = {'runId': 'a' * 32, 'environmentId': 'b' * 32}
                if attached:
                    child.update(kind='attached-suite', ownerLabel=owner['label'], ownerRunId=receipt['runId'],
                                 environmentId=receipt['environmentId'], executionContext=context, source='c' * 40,
                                 profile=Q.ATTACHED_PROFILE, holdSeconds=120)
                else:
                    child['eventStorePort'] = 23456
                journal = {'executionContext': context, 'source': 'c' * 40,
                           'owners': [owner] if attached else [owner, child], 'attachedRuns': [child] if attached else []}
                Q.write(controller_root / 'journal.json', journal)
                helper = Mock()
                helper.ci_context.return_value = context
                helper.job_root.side_effect = lambda key: Q.ROOT if key == 'GITHUB_WORKSPACE' else root
                helper.receipt_owner.return_value = receipt
                with patch.object(Q, 'HELPER', helper), patch.dict(os.environ, GITHUB_SHA=journal['source']), \
                        patch.object(Q, 'gated_exec', return_value=17) as gate:
                    self.assertEqual(Q.launch_owner(child['label']), 17)
                expected = Q.TOOL + ['test', '--manifest=' + Q.MANIFEST, '--mode=package',
                                    '--profile=' + (Q.ATTACHED_PROFILE if attached else Q.PROFILE)]
                expected += ['--attach-to=' + receipt['runId'], '--hold-seconds=120', '--output=json'] if attached else [
                             '--ci-owner-receipt=' + child['receipt'], '--output=json', '--eventstore-port=23456']
                gate.assert_called_once_with(child, expected)

    def test_explicit_port_is_journaled_before_launch_release(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, _, _, _, _ = lifecycle.prepared(Path(temporary))
            def launch(item):
                self.assertEqual(Q.read(value.journal_path)['owners'][-1]['eventStorePort'], 23456)
                raise InterruptedError('test interruption before release')
            value.launch = launch
            with self.assertRaises(InterruptedError):
                value.begin('collision-challenger', eventstore_port=23456, wait_workspace=False)

    def test_owner_labels_match_the_csharp_native_diagnostic_binder(self):
        source = (Q.ROOT / 'tests/Hexalith.Parties.Platform.IntegrationTests/NativeDiagnosticBinding.cs').read_text()
        guard = re.search(r'if \(label is not \((.*?)\)\)', source, re.S).group(1)
        self.assertEqual(set(re.findall(r'"([a-z-]+)"', guard)), set(Q.OWNER_LABELS))


class AttachedHandshakeTests(unittest.TestCase):
    def test_real_begin_writes_exact_control_and_binds_every_ready_marker_field(self):
        for fault in [None, 'ownerRunId', 'environmentId', 'originalAttachmentRequestId', 'expiresAtUtc',
                      'runId', 'profile', 'realServiceReadsPassed', 'observedAtUtc']:
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                value, owner, item, _, held = lifecycle.prepared(root)
                value.journal['attachedRuns'] = []
                value.state = Mock(return_value={'workspace': str(root / 'owner-workspace')})
                Q.write(value.diagnostics(item) / 'attachments.json', {'ownerRunId': owner['runId'], 'attachments': []})
                controls = Path(owner['stateDirectory']).parent / 'ci-qualification' / held['attachedRunId']
                def launch(entry):
                    accepted = datetime.now(timezone.utc)
                    held.update(acceptedAtUtc=accepted.isoformat(), expiresAtUtc=(accepted + timedelta(seconds=180)).isoformat())
                    held['execution'].update(manifestHash=entry['manifestHash'], entryHash=entry['entryHash'], requestedAtUtc=Q.now())
                    Q.write(value.diagnostics(item) / 'attachments.json', {'ownerRunId': owner['runId'], 'attachments': [held]})
                    marker = {key: held[key] for key in ('ownerRunId', 'environmentId', 'originalAttachmentRequestId', 'expiresAtUtc')}
                    marker.update(runId=held['attachedRunId'], profile=Q.ATTACHED_PROFILE, realServiceReadsPassed=True,
                                  observedAtUtc=Q.now(), processId=1234)
                    if fault == 'realServiceReadsPassed': marker[fault] = False
                    elif fault == 'observedAtUtc': marker[fault] = held['expiresAtUtc']
                    elif fault: marker[fault] = 'changed'
                    Q.write(controls / 'execution-ready.json', marker)
                    value.children[entry['label']] = Mock()
                value.launch = launch
                with patch.object(Q, 'owned_native_process', return_value={'processId': 1234, 'birth': 'original'}):
                    if fault:
                        with self.assertRaisesRegex(ValueError, 'checkpoint'): value.begin_attached(item, 'attached-failure-child')
                    else:
                        entry = value.begin_attached(item, 'attached-failure-child')
                        self.assertEqual(entry['executionCheckpoint']['birth'], 'original')
                        self.assertEqual(Q.read(controls / 'control.json'),
                                         {key: held[key] for key in ('ownerRunId', 'environmentId', 'originalAttachmentRequestId')}
                                         | {'runId': held['attachedRunId'], 'action': 'wait'})


class LineageRefusalTests(unittest.TestCase):
    def test_only_exact_expired_ended_unknown_refusals_with_unchanged_ledger_pass(self):
        for reason in ('attachment-ended', 'attachment-expired'):
            for fault in (None, 'success', 'admission', 'wrong-rule', 'wrong-reason', 'changed-ledger'):
                with self.subTest(reason=reason, fault=fault), tempfile.TemporaryDirectory() as temporary:
                    value, _, item, _, hold = lifecycle.prepared(Path(temporary))
                    tags = []
                    def tool(owner_item, arguments, tag):
                        tags.append(tag)
                        expected = 'unknown-attachment-request' if '--attachment-request-id=' + 'f' * 32 in arguments else reason
                        public = {'attachment': {'admitted': True} if fault == 'admission' else None,
                                  'diagnostics': [{'ruleId': 'HXR033' if fault == 'wrong-rule' else 'HXR036',
                                                   'field': 'owner-not-live' if fault == 'wrong-reason' else expected}]}
                        if fault == 'changed-ledger': Q.write(value.diagnostics(item) / 'attachments.json', {'attachments': []})
                        return Mock(returncode=0 if fault == 'success' else 3), public
                    value.tool = tool
                    if fault:
                        with self.assertRaises(ValueError): value.invalid_lineage(item, hold, expected_reason=reason)
                    else:
                        value.invalid_lineage(item, hold, expected_reason=reason)
                        self.assertEqual(tags, ['invalid-attached-lineage-' + reason, 'invalid-attached-lineage-unknown-attachment-request'])

    def test_invalid_handoff_cannot_hide_an_unrelated_refusal(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, _, item, _, hold = lifecycle.prepared(Path(temporary))
            value.start = Mock(return_value=item)
            value.attach = Mock(return_value=hold)
            value.tool = Mock(side_effect=[(Mock(returncode=0), {}), (Mock(returncode=3),
                                    {'diagnostics': [{'ruleId': 'HXR033', 'field': 'owner-not-live'}]})])
            value.control = Mock()
            with self.assertRaises(ValueError): value.invalid_handoff()
            value.control.assert_not_called()

    def test_invalid_handoff_passes_only_with_exact_ended_and_unknown_refusals(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, _, item, _, hold = lifecycle.prepared(Path(temporary))
            value.start = Mock(return_value=item)
            value.attach = Mock(return_value=hold)
            tags = []
            def tool(owner_item, arguments, tag):
                tags.append(tag)
                if tag == 'invalid-handoff-end':
                    self.assertIn('--attached-run-id=' + hold['attachedRunId'], arguments)
                    return Mock(returncode=0), {}
                # Builds Retry refuses an explicitly ended hold as attachment-ended, never as expired.
                reason = 'unknown-attachment-request' if '--attachment-request-id=' + 'f' * 32 in arguments else 'attachment-ended'
                return Mock(returncode=3), {'attachment': None, 'diagnostics': [{'ruleId': 'HXR036', 'field': reason}]}
            value.tool = tool
            value.control = Mock()
            value.children = {item['label']: Mock()}
            value.capture_result = Mock(return_value=0)
            value.invalid_handoff()
            self.assertEqual(tags, ['invalid-handoff-end', 'invalid-attached-lineage-attachment-ended',
                                    'invalid-attached-lineage-unknown-attachment-request'])
            value.control.assert_called_once_with(item, 'pass')


class AttachedCommandTests(unittest.TestCase):
    def test_attached_exit_public_mapping_and_cleanup_diagnostics_are_verified_for_each_cause(self):
        for scenario in ('owner-cancellation', 'hold-expiry', 'attached-failure'):
            for fault in (None, 'exit', 'outcome', 'rule', 'category', 'diagnostic', 'missing-public', 'rebound-failure', 'foreign-run'):
                with self.subTest(scenario=scenario, fault=fault), tempfile.TemporaryDirectory() as temporary:
                    value, entry = lifecycle.ScenarioOrchestrationTests().attached(Path(temporary), scenario)
                    if fault == 'exit':
                        value.children[entry['label']].returncode = 3
                        value.children[entry['label']].poll.return_value = 3
                    public_path = Path(entry['publicResult'])
                    public = Q.read(public_path)
                    if fault in ('outcome', 'rule', 'category'):
                        public['outcome'][{'outcome': 'exitCode', 'rule': 'ruleId', 'category': 'category'}[fault]] = 'unexpected'
                    if fault == 'diagnostic': public['diagnostics'].append({'ruleId': 'HXT008', 'category': 'EvidencePolicy'})
                    if fault == 'foreign-run': public['runId'] = '2' * 32  # another attached run of the same owner
                    Q.write(public_path, public)
                    if fault == 'rebound-failure':
                        if scenario != 'attached-failure': continue
                        document = Q.read(value.diagnostics(value.journal['owners'][0]) / 'attachments.json')
                        document['attachments'][0]['execution'].update(exitCode=6, ruleId='HXT008')
                        Q.write(value.diagnostics(value.journal['owners'][0]) / 'attachments.json', document)
                        entry['admission']['execution'].update(exitCode=6, ruleId='HXT008')
                        value.children[entry['label']].returncode = 6
                        value.children[entry['label']].poll.return_value = 6
                        public['outcome'].update(exitCode='EvidenceSchemaOrPolicy', category='EvidencePolicy', ruleId='HXT008')
                        Q.write(public_path, public)
                    if fault == 'missing-public': public_path.unlink()
                    if fault:
                        with self.assertRaises((ValueError, FileNotFoundError)): value.attached_case(scenario)
                        value.down.assert_not_called()
                    else:
                        value.attached_case(scenario)
                        self.assertIn('commandExitCode', entry)

    def test_finalizer_mirrors_the_builds_relay_for_every_failed_outcome(self):
        # (cause, ledger exit, ledger rule) -> (CLI exit, public exit name, category, rule) per AttachedSuiteCommand.Relay.
        cases = [('startup-failure', 3, 'HXR033', (3, 'TopologyOrLifecycle', 'TopologyOrLifecycle', 'HXR033')),
                 ('test-failure', 6, 'HXT008', (6, 'EvidenceSchemaOrPolicy', 'EvidencePolicy', 'HXT008')),
                 ('test-failure', 3, 'HXT009', (3, 'TopologyOrLifecycle', 'TopologyOrLifecycle', 'HXT009')),
                 ('test-failure', 4, 'HXT004', (4, 'ProductOrTest', 'ProductOrTest', 'HXT004')),
                 ('startup-failure', None, None, (4, 'ProductOrTest', 'ProductOrTest', 'HXT007')),
                 ('test-failure', 130, 'HXC130', (4, 'ProductOrTest', 'ProductOrTest', 'HXC130'))]
        for cause, code, rule, expected in cases:
            for fault in (None, 'category', 'exit'):
                with self.subTest(cause=cause, code=code, fault=fault), tempfile.TemporaryDirectory() as temporary:
                    value, entry = lifecycle.ScenarioOrchestrationTests().attached(Path(temporary), 'attached-failure')
                    ledger = value.diagnostics(value.journal['owners'][0]) / 'attachments.json'
                    document = Q.read(ledger)
                    for execution in (document['attachments'][0]['execution'], entry['admission']['execution']):
                        execution.update(cause=cause, exitCode=code, ruleId=rule)
                    Q.write(ledger, document)
                    exit_code = 5 if fault == 'exit' else expected[0]
                    value.children[entry['label']].returncode = exit_code
                    value.children[entry['label']].poll.return_value = exit_code
                    public = Q.read(Path(entry['publicResult']))
                    public['outcome'] = {'exitCode': expected[1], 'category': 'Usage' if fault == 'category' else expected[2], 'ruleId': expected[3]}
                    Q.write(Path(entry['publicResult']), public)
                    value.recover_launch = Mock(return_value={'pid': 1234, 'birth': 'original', 'gateStatus': 'released'})
                    value.settled_child = Mock()
                    value.attached_result = Mock(return_value={'terminal': {'outcome': 'failed', 'cause': cause}})
                    if fault:
                        with self.assertRaisesRegex(ValueError, 'differs'): value.finalize_attached(entry)
                    else:
                        value.finalize_attached(entry)
                        self.assertEqual(entry['commandExitCode'], expected[0])

    def test_finalizer_checks_attached_command_and_requires_an_exit_observation(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, entry = lifecycle.ScenarioOrchestrationTests().attached(Path(temporary), 'attached-failure')
            value.recover_launch = Mock(return_value={'pid': 1234, 'birth': 'original', 'gateStatus': 'released'})
            value.settled_child = Mock()
            value.finalize_attached(entry)
            del value.children[entry['label']]
            value.finalize_attached(entry)
            del entry['commandExitCode']
            with self.assertRaisesRegex(ValueError, 'exit code'): value.finalize_attached(entry)


class FailureCancellationMainTests(unittest.TestCase):
    def controller(self, root):
        value = object.__new__(Q.Qualification)
        first = {'runId': 'a' * 32, 'ownerId': 'a' * 32, 'terminalStatus': 'failed',
                 'terminalOutcome': {'exitCode': 'ProductOrTest'}, 'terminalCause': 'test-failure',
                 'terminalAtUtc': (datetime.now(timezone.utc) - timedelta(seconds=10)).isoformat(), 'sourceMappingHash': 'b' * 64}
        item = {'label': 'cancel', 'pid': 1234, 'birth': 'original', 'cancellationPhase': 'failure'}
        value.start = Mock(return_value=item)
        value.owner = Mock(return_value=item)
        value.issued = Mock(return_value={'runId': first['runId']})
        value.control = Mock()
        value.children = {'cancel': Mock()}
        value.capture_result = Mock(return_value=4)
        value.archived = Mock(return_value=first)
        value.diagnostics = Mock(return_value=root)
        Q.write(root / 'selected-test.json', first)
        for name in ('observe', 'save', 'resume_host', 'process_signal', 'cancellation_snapshot', 'down'):
            setattr(value, name, Mock())
        value.finalize = lambda status: (value.finalize_cancellation(item, item.get('terminalBeforeSignalForward'), status), 0)[1]
        return value, item, first

    def invoke(self, value, *arguments):
        with patch.object(Q, 'Qualification', return_value=value), patch.object(sys, 'argv', ['qualify', *arguments]):
            return Q.main()

    def test_failure_start_requires_failed_exit_and_durable_failed_result(self):
        for fault in (None, 'exit', 'terminal'):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temporary:
                value, item, first = self.controller(Path(temporary))
                if fault == 'exit': value.capture_result.return_value = 0
                if fault == 'terminal': first['terminalStatus'] = 'completed'
                if fault:
                    with self.assertRaises(ValueError): self.invoke(value, 'start-cancellation', '--phase', 'failure')
                    self.assertNotIn('failureBeforeCancellation', item)
                else:
                    self.assertEqual(self.invoke(value, 'start-cancellation', '--phase', 'failure'), 0)
                    self.assertEqual(item['failureBeforeCancellation'], Q.first_outcome(first))
                    value.start.assert_called_once_with('cancel', startup=False, cancellation_phase='failure')
                    value.control.assert_called_once_with(item, 'fail')

    def test_main_wait_and_finalize_preserve_failure_without_live_owner_or_cancellation_credit(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, item, first = self.controller(Path(temporary))
            self.invoke(value, 'start-cancellation', '--phase', 'failure')
            Q.CANCELLED[:] = [{'signal': signal.SIGTERM, 'observedAtUtc': Q.now()}]
            try:
                with patch.object(Q.signal, 'signal'), patch.object(Q, 'birth', return_value=None):
                    self.assertEqual(self.invoke(value, 'wait-cancellation'), 1)
                value.process_signal.assert_not_called()
                self.assertEqual(item['terminalBeforeSignalForward'], item['failureBeforeCancellation'])
                self.assertEqual(self.invoke(value, 'finalize', '--job-status', 'cancelled'), 0)
                self.assertEqual(value.observe.call_args.kwargs['classification'], 'product-failure-before-observed-signal')
                for classification in ('cancelled', 'unexpected'):
                    with patch.object(Q, 'cancellation_terminal', return_value=classification):
                        with self.assertRaises(ValueError): self.invoke(value, 'finalize', '--job-status', 'cancelled')
                first['terminalAtUtc'] = Q.now()
                with self.assertRaises(ValueError): self.invoke(value, 'finalize', '--job-status', 'cancelled')
            finally:
                Q.CANCELLED.clear()


class ArtifactBoundTests(unittest.TestCase):
    def test_large_writer_created_metadata_is_readable_and_recoverable_without_relaxing_artifacts(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            workspace, runner = root / 'workspace', root / 'runner'
            value = object.__new__(Q.Qualification)
            value.temporary = Q.private_directory(runner / 'hexalith-ci-qualification')
            value.evidence = Q.private_directory(workspace / 'TestResults/ci-lifecycle')
            value.journal_path = value.temporary / 'journal.json'
            context = {'kind': 'local-control'}
            owner = {'label': 'collision-challenger', 'eventStorePort': 23456}
            owner.update({key: str(value.temporary / owner['label'] / filename) for key, filename in
                          [('receipt', 'receipt.json'), ('launchRecord', 'launch.json'), ('publicResult', 'public-result.json')]})
            value.journal = {'schema': 'hexalith.ci-lifecycle-qualification.v1', 'classification': 'baseline-only',
                             'source': 'c' * 40, 'mappingHash': 'd' * 64, 'executionContext': context,
                             'owners': [owner], 'observations': [{'padding': 'x' * 8_388_609}]}
            value.save()
            self.assertEqual(Q.read(value.journal_path, limit=None), value.journal)
            self.assertEqual(Q.optional_read(value.evidence / 'observations.json', limit=None), value.journal)
            helper = Mock()
            helper.ci_context.return_value = context
            helper.job_root.side_effect = lambda key: workspace if key == 'GITHUB_WORKSPACE' else runner
            with patch.object(Q, 'ROOT', workspace), patch.object(Q, 'HELPER', helper), \
                    patch.object(Q.subprocess, 'check_output', return_value=value.journal['source']), \
                    patch.dict(os.environ, GITHUB_SHA=value.journal['source']), patch.object(Q, 'gated_exec', return_value=17) as gate:
                recovered = Q.Qualification(finalizing=True)
                self.assertEqual(recovered.journal, value.journal)
                self.assertEqual(Q.launch_owner(owner['label']), 17)
                gate.assert_called_once()
            with self.assertRaises(ValueError): Q.read_bytes(value.journal_path)
            with self.assertRaises(ValueError): Q.write_retained_bytes(root / 'artifact.json', b'x' * 8_388_609)
            linked = root / 'linked.json'
            linked.symlink_to(value.journal_path)
            for reader in (Q.read, Q.optional_read):
                with self.subTest(reader=reader.__name__), self.assertRaises((OSError, ValueError)):
                    reader(linked, limit=None)
            linked.unlink()
            os.link(value.journal_path, linked)
            for reader in (Q.read, Q.optional_read):
                with self.subTest(reader=reader.__name__), self.assertRaises(ValueError):
                    reader(linked, limit=None)


class DurableAttachedExitTests(unittest.TestCase):
    def test_real_launch_exit_survives_controller_loss_and_exact_signal_forwarding(self):
        for interrupted in (False, True):
            with self.subTest(interrupted=interrupted), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                value, entry = lifecycle.ScenarioOrchestrationTests().attached(root, 'owner-cancellation' if interrupted else 'attached-failure')
                public = Q.read(Path(entry['publicResult']))
                ready = root / 'command-ready'
                signals = root / 'command-signals'
                command_code = ("import json, pathlib, signal, sys, time\n"
                                f"public = {public!r}\n"
                                "def interrupted(number, _frame):\n"
                                f"    with open({str(signals)!r}, 'a') as output: output.write(str(number) + '\\n')\n"
                                "    time.sleep(.1)\n"
                                "    print(json.dumps(public), flush=True)\n"
                                "    sys.exit(130)\n"
                                "signal.signal(signal.SIGINT, interrupted)\n"
                                f"pathlib.Path({str(ready)!r}).touch()\n"
                                + ("time.sleep(30)" if interrupted else "print(json.dumps(public), flush=True); sys.exit(4)"))
                muxer = ('import signal, subprocess, sys; '
                         'signal.signal(signal.SIGINT, signal.SIG_IGN); '
                         'sys.exit(subprocess.call(sys.argv[1:]))')
                command = [sys.executable, '-B', '-c', muxer, sys.executable, '-B', '-c', command_code]
                source = ('import importlib.util; '
                          f'spec=importlib.util.spec_from_file_location("exit_gate", {Q.__file__!r}); '
                          'q=importlib.util.module_from_spec(spec); spec.loader.exec_module(q); '
                          f'raise SystemExit(q.gated_exec({entry!r}, {command!r}))')
                with open(entry['publicResult'], 'w') as output:
                    child = subprocess.Popen([sys.executable, '-B', '-c', source], stdin=subprocess.PIPE,
                                             stdout=output, stderr=subprocess.PIPE, start_new_session=True)
                try:
                    deadline = time.monotonic() + 10
                    while not Path(entry['launchRecord']).exists() and time.monotonic() < deadline:
                        time.sleep(.01)
                    self.assertEqual(Q.read(Path(entry['launchRecord']))['gateStatus'], 'waiting')
                    value.recover_launch(entry)
                    value.save()
                    child.stdin.write(b'1')
                    child.stdin.flush()
                    if interrupted:
                        while not ready.exists() and time.monotonic() < deadline:
                            time.sleep(.01)
                        self.assertTrue(ready.exists())
                        value.recover_launch(entry)
                        # Use the production signal boundary: only the supervisor is signalled,
                        # then it forwards once to its exact muxer/tool children.
                        with patch.object(value, 'receipt_present', return_value=False), \
                                patch.object(Q, 'signal_tool_processes', wraps=Q.signal_tool_processes) as send:
                            value.process_signal = Q.Qualification.process_signal.__get__(value)
                            value.process_signal(entry, signal.SIGINT)
                            send.assert_called_once_with({child.pid: entry['birth']}, signal.SIGINT)
                    self.assertEqual(child.wait(timeout=10), 130 if interrupted else 4, child.stderr.read().decode())
                    if interrupted: self.assertEqual(signals.read_text().splitlines(), [str(signal.SIGINT)])
                    record = Q.read(Path(entry['launchRecord']))
                    self.assertEqual(record['commandExitCode'], child.returncode)
                    self.assertEqual(record['gateStatus'], 'released')
                    # A new controller reads the saved journal and actual gate record, with no Popen object.
                    recovered = object.__new__(Q.Qualification)
                    for attribute in ('temporary', 'evidence', 'journal_path', 'context'):
                        setattr(recovered, attribute, getattr(value, attribute))
                    recovered.journal = Q.read(value.journal_path)
                    recovered.children = {}
                    recovered.issued = value.issued
                    recovered.diagnostics = value.diagnostics
                    recovered_entry = recovered.journal['attachedRuns'][0]
                    self.assertNotIn('commandExitCode', recovered_entry)
                    recovered.finalize_attached(recovered_entry)
                    self.assertEqual(recovered_entry['commandExitCode'], child.returncode)
                    # A changed exit record cannot rebind the originally journaled process identity.
                    del recovered_entry['commandExitCode']
                    record['birth'] = '123456789012345'
                    Q.write(Path(entry['launchRecord']), record)
                    with self.assertRaisesRegex(ValueError, 'launch identity'):
                        recovered.finalize_attached(recovered_entry)
                finally:
                    if child.poll() is None:
                        child.kill()
                        child.wait(timeout=10)
                    child.stdin.close()
                    child.stderr.close()


class EarlyAttachedSignalTests(unittest.TestCase):
    def test_cancellation_during_process_construction_or_birth_capture_is_forwarded_once_after_binding(self):
        for phase in ('construction', 'birth'):
            for number in (signal.SIGINT, signal.SIGTERM):
                with self.subTest(phase=phase, number=number), tempfile.TemporaryDirectory() as temporary:
                    handlers = {}
                    child = Mock(pid=1234)
                    child.__enter__ = Mock(return_value=child)
                    child.__exit__ = Mock(return_value=False)
                    child.wait.return_value = 130
                    processes = {1234: 'original-birth', 1235: 'tool-birth'}
                    def construct(*arguments, **options):
                        if phase == 'construction': handlers[number](number, None)
                        send.assert_not_called()
                        return child
                    def capture(pid):
                        self.assertEqual(pid, child.pid)
                        if phase == 'birth': handlers[number](number, None)
                        send.assert_not_called()
                        return 'original-birth'
                    item = {'launchRecord': str(Path(temporary) / 'launch.json')}
                    released = {'gateStatus': 'released', 'pid': 1233, 'birth': 'monitor-birth'}
                    with patch.object(Q.signal, 'signal', side_effect=lambda key, handler: handlers.update({key: handler})), \
                            patch.object(Q.subprocess, 'Popen', side_effect=construct), patch.object(Q, 'birth', side_effect=capture), \
                            patch.object(Q, 'tool_processes', return_value=processes) as inventory, \
                            patch.object(Q, 'signal_tool_processes') as send:
                        self.assertEqual(Q.supervise_attached_command(item, ['controlled-test-command'], released), 130)
                        inventory.assert_called_once_with(child.pid, 'original-birth')
                        send.assert_called_once_with(processes, number)
                    self.assertEqual(Q.read(Path(item['launchRecord']))['commandExitCode'], 130)
