"""Owned filesystem/in-memory controller controls; no CI identity or native/service execution."""
import copy
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

SCRIPT = Path(__file__).resolve().parents[2] / 'scripts/qualify-disposable-ci.py'
SPEC = importlib.util.spec_from_file_location('cleanup_matrix', SCRIPT)
Q = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(Q)


def controller(directory):
    value = object.__new__(Q.Qualification)
    value.evidence = Path(directory) / 'retained'
    value.journal = {'mappingHash': 'b' * 64, 'owners': [], 'observations': []}
    value.prepared_mapping = {'Mode': 'package', 'Root': str(Q.ROOT), 'ContentHash': 'b' * 64,
                              'Entries': [{'Identity': 'unit-source-only', 'Origin': 'source'}]}
    value.issued = lambda item: {'runId': ('a' if item['label'] == 'retry' else 'c') * 32}
    value.diagnostics = lambda item: Path(directory) / item['label']
    value.observe = lambda kind, **fields: value.journal['observations'].append({'kind': kind, **fields})
    return value


def terminal(mapping=None):
    # Exact nullable mapping shape of an independent down-created terminal, not an issued CI result.
    return {'runId': 'a' * 32, 'ownerId': 'a' * 32, 'terminalStatus': 'cancelled',
            'terminalOutcome': {'exitCode': 'Cancelled'}, 'terminalAtUtc': '2026-11-03T00:00:00Z',
            'sourceMappingHash': mapping, 'terminalCause': 'run-cancelled'}


def write_terminal(value, record=None, mapping=None):
    directory = value.diagnostics({'label': 'retry'})
    Q.write(directory / 'selected-test.json', terminal() if record is None else record)
    Q.write(directory / 'selected-source-mapping.json', value.prepared_mapping if mapping is None else mapping)


class CleanupMatrixTests(unittest.TestCase):
    def test_begin_sets_the_owned_wait_control_before_any_other_owner_can_be_launched(self):
        with tempfile.TemporaryDirectory() as directory:
            value = controller(directory)
            value.temporary = Path(directory) / 'controller'
            events = []
            value.save = Mock()
            value.launch = lambda item: events.append('launch')
            value.state = lambda item: {'workspace': 'unit-owned'}
            def poll(item, predicate, seconds):
                self.assertEqual(seconds, 600)
                self.assertTrue(predicate())
                events.append('original-workspace-bound')
            value.poll = poll
            value.control = lambda item, action: events.append(('control', action))
            item = value.begin('retry')
            self.assertEqual(item['label'], 'retry')
            self.assertEqual(events, ['launch', 'original-workspace-bound', ('control', 'wait')])

    def test_refused_bookkeeping_can_be_pending_but_durable_boundary_requires_a_real_outcome(self):
        with tempfile.TemporaryDirectory() as directory:
            value = controller(directory)
            item = {'label': 'retry'}
            self.assertIsNone(value.cleanup_outcome(item, 'withdrawal-refused'))
            with self.assertRaises(ValueError):
                value.cleanup_outcome(item, 'withdrawal-recorded', required=True)
            write_terminal(value)
            observed = value.cleanup_outcome(item, 'withdrawal-recorded', required=True)
            self.assertEqual(observed, terminal())
            self.assertIsNone(observed['sourceMappingHash'])
            self.assertTrue(value.journal['observations'][-1]['originalMappingVerified'])
            # Preserve the original strict normal terminal contract and its missing-mapping assertion.
            with self.assertRaises(ValueError):
                Q.first_outcome(terminal())
            with patch.object(Q.HELPER, 'retain_artifacts'), self.assertRaises(ValueError):
                value.archived(item)

    def test_cleanup_does_not_accept_a_changed_owner_time_or_present_mapping(self):
        with tempfile.TemporaryDirectory() as directory:
            value = controller(directory)
            for changes in ({'runId': 'd' * 32}, {'ownerId': 'd' * 32},
                            {'terminalAtUtc': None}, {'terminalAtUtc': '2026-11-03T00:00:00'},
                            {'sourceMappingHash': 'd' * 64}):
                with self.subTest(changes=changes):
                    write_terminal(value, {**terminal(), **changes})
                    with self.assertRaises(ValueError):
                        value.cleanup_outcome({'label': 'retry'}, 'withdrawal-recorded', required=True)

    def test_mapping_requires_the_entire_original_prepared_document_not_just_a_declared_hash(self):
        with tempfile.TemporaryDirectory() as directory:
            value = controller(directory)
            for changes in ({'Root': '/foreign'}, {'Mode': 'source'}, {'ContentHash': 'd' * 64},
                            {'Entries': [{'Identity': 'foreign', 'Origin': 'source'}]}):
                with self.subTest(changes=changes):
                    write_terminal(value, mapping={**value.prepared_mapping, **changes})
                    with self.assertRaises(ValueError):
                        value.cleanup_outcome({'label': 'retry'}, 'withdrawal-recorded', required=True)
            write_terminal(value)
            (value.diagnostics({'label': 'retry'}) / 'selected-source-mapping.json').unlink()
            with self.assertRaises(FileNotFoundError):
                value.cleanup_outcome({'label': 'retry'}, 'withdrawal-recorded', required=True)

    def test_linked_outcome_or_mapping_cannot_supply_the_required_snapshot(self):
        with tempfile.TemporaryDirectory() as directory:
            value = controller(directory)
            foreign = Path(directory) / 'foreign.json'
            Q.write(foreign, terminal())
            for name in ('selected-test.json', 'selected-source-mapping.json'):
                write_terminal(value)
                path = value.diagnostics({'label': 'retry'}) / name
                path.unlink()
                path.symlink_to(foreign)
                with self.subTest(name=name), self.assertRaises((OSError, ValueError)):
                    value.cleanup_outcome({'label': 'retry'}, 'withdrawal-recorded', required=True)
                path.unlink()

    def test_a_linked_owner_directory_cannot_supply_even_an_early_outcome(self):
        with tempfile.TemporaryDirectory() as directory:
            value = controller(directory)
            write_terminal(value)
            actual = value.diagnostics({'label': 'retry'})
            linked = Path(directory) / 'linked-owner'
            linked.symlink_to(actual, target_is_directory=True)
            value.diagnostics = lambda item: linked
            with self.assertRaises(OSError):
                value.cleanup_outcome({'label': 'retry'}, 'withdrawal-refused')

    def test_retry_starts_both_owners_before_waiting_and_checks_both_lock_boundaries(self):
        for early_present in (False, True):
            with self.subTest(early_present=early_present), tempfile.TemporaryDirectory() as directory:
                value = controller(directory)
                events = []
                value.begin = lambda label: events.append(('begin', label)) or {'label': label}
                value.ready = lambda item: events.append(('ready', item['label']))
                scope = {'containers': ['unit-peer'], 'workspace': 'unit-workspace', 'claimHashes': {'unit': 'same'}}
                value.peer_scope = lambda item: copy.deepcopy(scope)
                value.attach = lambda *args: {'unit-admission': 'same-original-expiry'}
                value.state = lambda item: {'testRun': None}
                value.containers = lambda item: ['unit-owned-container']
                value.withdrawn = lambda item, admitted: events.append(('withdrawn', admitted))
                value.control = lambda item, action: events.append(('control', item['label'], action))
                value.children = {'peer': Mock()}
                value.capture_result = lambda item: 0
                diagnostic = value.diagnostics({'label': 'retry'})
                diagnostic.mkdir()
                def down(item, tag, expected=True):
                    events.append(('down', tag, expected))
                    if tag == 'withdrawal-lock-refusal' and early_present:
                        write_terminal(value)
                    if tag == 'provisioning-lock-refusal':
                        write_terminal(value)
                value.down = down
                with patch.object(Q.HELPER, 'retain_artifacts') as retained:
                    value.retry_with_peer()
                self.assertEqual(events[:4], [('begin', 'retry'), ('begin', 'peer'), ('ready', 'retry'), ('ready', 'peer')])
                downs = [item for item in events if item[0] == 'down']
                self.assertEqual(downs, [('down', 'withdrawal-lock-refusal', False),
                                         ('down', 'provisioning-lock-refusal', False),
                                         ('down', 'exact-owner-cleanup-retry', True),
                                         ('down', 'peer-idempotent-cleanup', True)])
                self.assertEqual(retained.call_count, 1)
                snapshots = [x for x in value.journal['observations'] if x['kind'] == 'cleanup-first-outcome']
                self.assertEqual([x['present'] for x in snapshots], [early_present, True, True])
                self.assertEqual(snapshots[1]['firstOutcome'], snapshots[2]['firstOutcome'])
                if early_present:
                    self.assertEqual(snapshots[0]['firstOutcome'], snapshots[1]['firstOutcome'])
                self.assertIn(('control', 'peer', 'pass'), events)

    def test_matrix_preserves_first_failure_while_all_five_owner_finalizers_are_attempted(self):
        with tempfile.TemporaryDirectory() as directory:
            value = controller(directory)
            value.journal['owners'] = [{'label': x} for x in ['passing', 'failing', 'retry', 'peer', 'partial']]
            first = ValueError('private assertion payload must never be exported')
            value.ordinary = Mock(side_effect=first)
            value.retry_with_peer = Mock()
            value.partial_startup = Mock()
            value.collision = Mock()
            value.startup_timeout = Mock()
            value.attached_case = Mock()
            value.invalid_handoff = Mock()
            attempted = []
            def finalize(item, status, issued):
                attempted.append(item['label'])
                issued.append(item['label'])
                if item['label'] in ('retry', 'peer'):
                    raise OSError('private cleanup payload must never be exported')
            value.finalize_owner = finalize
            with self.assertRaises(ValueError) as caught:
                Q.run_matrix(value)
            self.assertIs(caught.exception, first)
            self.assertEqual(attempted, ['partial', 'peer', 'retry', 'failing', 'passing'])
            self.assertEqual(value.journal['firstFailure']['phase'], 'scenario')
            final = next(x for x in value.journal['observations'] if x['kind'] == 'finalizer-result')
            self.assertEqual(len(final['failures']), 2)
            self.assertFalse(final['issuedOwnersCleanupVerified'])
            self.assertEqual(len([x for x in value.journal['observations'] if x['kind'] == 'qualification-failure']), 2)
            self.assertNotIn('private', json.dumps(value.journal))

    def test_changed_early_terminal_is_refused_before_exact_retry_can_stop_resources(self):
        with tempfile.TemporaryDirectory() as directory:
            value = controller(directory)
            value.begin = lambda label: {'label': label}
            value.ready = lambda item: None
            value.peer_scope = lambda item: {'unit-peer': 'unchanged'}
            value.attach = lambda *args: {'unit-admission': 'same-original-expiry'}
            value.containers = lambda item: ['unit-owned-container']
            value.diagnostics({'label': 'retry'}).mkdir()
            first = terminal()
            value.cleanup_outcome = Mock(side_effect=[first, {**first, 'terminalAtUtc': '2026-11-03T00:00:01Z'}])
            value.down = Mock()
            with self.assertRaises(ValueError):
                value.retry_with_peer()
            self.assertEqual([call.args[1] for call in value.down.call_args_list],
                             ['withdrawal-lock-refusal', 'provisioning-lock-refusal'])

    def test_finalizer_exception_cannot_replace_the_original_scenario_exception(self):
        with tempfile.TemporaryDirectory() as directory:
            value = controller(directory)
            first = TimeoutError('private first payload')
            value.ordinary = Mock(side_effect=first)
            value.retry_with_peer = Mock()
            value.partial_startup = Mock()
            value.collision = Mock()
            value.startup_timeout = Mock()
            value.attached_case = Mock()
            value.invalid_handoff = Mock()
            value.finalize = Mock(side_effect=OSError('private later payload'))
            with self.assertRaises(TimeoutError) as caught:
                Q.run_matrix(value)
            self.assertIs(caught.exception, first)
            self.assertNotIn('private', json.dumps(value.journal))
            self.assertEqual(value.journal['firstFailure']['failureType'], 'TimeoutError')

    def test_matrix_with_no_scenario_failure_still_refuses_incomplete_finalization(self):
        with tempfile.TemporaryDirectory() as directory:
            value = controller(directory)
            value.ordinary = Mock()
            value.retry_with_peer = Mock()
            value.partial_startup = Mock()
            value.collision = Mock()
            value.startup_timeout = Mock()
            value.attached_case = Mock()
            value.invalid_handoff = Mock()
            value.finalize = Mock(return_value=1)
            with self.assertRaises(ValueError):
                Q.run_matrix(value)
            self.assertEqual(value.journal['firstFailure']['phase'], 'finalization')

    def test_down_failure_is_preserved_when_retention_also_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            value = controller(directory)
            first = ValueError('private down payload')
            value.resume_host = Mock()
            value.down = Mock(side_effect=first)
            with patch.object(Q.HELPER, 'retain_artifacts', side_effect=OSError('private retention payload')) as retained:
                with self.assertRaises(ValueError) as caught:
                    value.cleanup_owned({'label': 'retry'}, {'runId': 'a' * 32}, 'failure')
            self.assertIs(caught.exception, first)
            retained.assert_called_once()
            observations = value.journal['observations']
            self.assertEqual([x['checkpoint'] for x in observations], ['exact-down', 'retain-artifacts'])
            self.assertNotIn('private', json.dumps(observations))

    def test_failed_observation_cannot_prevent_down_retention_or_replace_the_first_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            value = controller(directory)
            first = ValueError('private down payload')
            value.resume_host = Mock()
            value.down = Mock(side_effect=first)
            value.observe = Mock(side_effect=OSError('private observation payload'))
            output = io.StringIO()
            with patch.object(Q.HELPER, 'retain_artifacts') as retained, patch.object(Q.sys, 'stderr', output):
                with self.assertRaises(ValueError) as caught:
                    value.cleanup_owned({'label': 'retry'}, {'runId': 'a' * 32}, 'failure')
            self.assertIs(caught.exception, first)
            value.down.assert_called_once()
            retained.assert_called_once()
            self.assertIn('owner-finalizer-observation-unverified', output.getvalue())
            self.assertNotIn('private', output.getvalue())

    def test_failed_failure_recording_preserves_the_scenario_error_and_still_finalizes(self):
        with tempfile.TemporaryDirectory() as directory:
            value = controller(directory)
            first = ValueError('private original payload')
            value.ordinary = Mock(side_effect=first)
            value.retry_with_peer = Mock()
            value.partial_startup = Mock()
            value.collision = Mock()
            value.startup_timeout = Mock()
            value.attached_case = Mock()
            value.invalid_handoff = Mock()
            value.finalize = Mock(return_value=1)
            actual_observe = value.observe
            def observe(kind, **fields):
                if kind == 'qualification-failure':
                    raise OSError('private failed recording payload')
                actual_observe(kind, **fields)
            value.observe = observe
            output = io.StringIO()
            with patch.object(Q.sys, 'stderr', output), self.assertRaises(ValueError) as caught:
                Q.run_matrix(value)
            self.assertIs(caught.exception, first)
            value.finalize.assert_called_once()
            self.assertEqual(value.journal['firstFailure']['phase'], 'scenario')
            self.assertIn('failure-observation-unverified', output.getvalue())
            self.assertNotIn('private', output.getvalue())


class ReaderDirectoryTests(unittest.TestCase):
    def test_actual_cleanup_command_never_constructs_a_hosted_qualification_owner(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            owned = root / 'hexalith-lifecycle-registry.unit'
            owned.mkdir()
            (owned / 'config.json').write_text('unit reader')
            with patch.dict(os.environ, {'RUNNER_TEMP': str(root),
                                         'HEXALITH_LIFECYCLE_REGISTRY_CONFIG': str(owned)}, clear=True), \
                 patch.object(Q.sys, 'argv', ['qualify-disposable-ci.py', 'remove-reader']), \
                 patch.object(Q, 'Qualification') as constructor:
                self.assertEqual(Q.main(), 0)
                constructor.assert_not_called()
            self.assertFalse(owned.exists())

    def test_nested_docker_metadata_is_removed_with_the_owned_reader_tree(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            owned = root / 'hexalith-lifecycle-registry.unit'
            (owned / 'metadata' / 'nested').mkdir(parents=True)
            (owned / 'config.json').write_text('unit-secret-never-read')
            (owned / 'metadata/nested/data').write_text('unit metadata')
            Q.remove_reader_directory(str(owned), root)
            self.assertFalse(owned.exists())
            Q.remove_reader_directory(str(owned), root)
            Q.remove_reader_directory(None, None)

    def test_foreign_root_prefix_and_linked_root_or_config_are_refused(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            foreign = root / 'foreign'
            foreign.mkdir()
            secret = foreign / 'config.json'
            secret.write_text('unit-secret-never-read')
            sibling = root / 'hexalith-lifecycle-registry.link'
            sibling.symlink_to(foreign, target_is_directory=True)
            for path in (foreign, sibling, root / 'nested/hexalith-lifecycle-registry.unit'):
                with self.subTest(path=path.name), self.assertRaises(ValueError):
                    Q.remove_reader_directory(str(path), root)
            owned = root / 'hexalith-lifecycle-registry.unit'
            owned.mkdir()
            (owned / 'config.json').symlink_to(secret)
            with self.assertRaises(ValueError):
                Q.remove_reader_directory(str(owned), root)
            self.assertEqual(secret.read_text(), 'unit-secret-never-read')
            self.assertTrue(owned.is_dir())

    def test_nested_links_are_unlinked_without_traversing_the_foreign_target(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            foreign = root / 'foreign'
            foreign.mkdir()
            marker = foreign / 'marker'
            marker.write_text('preserve foreign content')
            owned = root / 'hexalith-lifecycle-registry.unit'
            owned.mkdir()
            (owned / 'config.json').write_text('unit reader')
            (owned / 'nested-link').symlink_to(foreign, target_is_directory=True)
            Q.remove_reader_directory(str(owned), root)
            self.assertEqual(marker.read_text(), 'preserve foreign content')
            self.assertFalse(owned.exists())

    def test_reader_unlink_precedes_a_reported_tree_removal_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            owned = root / 'hexalith-lifecycle-registry.unit'
            owned.mkdir()
            config = owned / 'config.json'
            config.write_text('unit reader')
            with patch.object(Q.shutil, 'rmtree', side_effect=OSError('unit refused tree removal')) as remove:
                remove.avoids_symlink_attacks = True
                with self.assertRaises(OSError):
                    Q.remove_reader_directory(str(owned), root)
            self.assertFalse(config.exists())
            self.assertTrue(owned.is_dir())


if __name__ == '__main__':
    unittest.main()
