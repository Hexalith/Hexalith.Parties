"""Local controller safety simulations only; none are hosted/runtime qualification evidence."""
import copy
from datetime import datetime, timedelta, timezone
import importlib.util
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import tempfile
import unittest
from unittest.mock import Mock, patch

SCRIPT = Path(__file__).resolve().parents[2] / 'scripts' / 'qualify-disposable-ci.py'
SPEC = importlib.util.spec_from_file_location('qualification', SCRIPT)
Q = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(Q)


def retry_controller(diagnostics, events):
    """An in-memory retry-with-peer owner whose only real filesystem state is its diagnostics directory."""
    value = object.__new__(Q.Qualification)
    value.journal = {'mappingHash': 'b' * 64, 'owners': [], 'observations': []}
    value.prepared_mapping = {'Mode': 'package', 'Root': str(Q.ROOT), 'ContentHash': 'b' * 64,
                              'Entries': [{'Identity': 'unit-source-only', 'Origin': 'source'}]}
    value.evidence = Path(diagnostics).parent / 'retained'
    value.issued = lambda item: {'runId': ('a' if item['label'] == 'retry' else 'c') * 32}
    value.diagnostics = lambda item: Path(diagnostics)
    value.observe = lambda kind, **fields: value.journal['observations'].append({'kind': kind, **fields})
    value.begin = lambda label: {'label': label}
    value.ready = lambda item: None
    value.peer_scope = lambda item: {'containers': ['unit-peer'], 'claimHashes': {'unit': 'same'}}
    value.attach = lambda *args: {'unit-admission': 'same-original-expiry'}
    value.state = lambda item: {'testRun': None}
    value.containers = lambda item: ['unit-owned-container']
    value.withdrawn = lambda item, admitted: None
    value.control = lambda item, action: None
    value.children = {'peer': Mock()}
    value.capture_result = lambda item: 0

    def down(item, tag, expected=True):
        events.append(('down', tag, expected))
        if tag == 'provisioning-lock-refusal':
            # The exact terminal and mapping the durable cleanup boundary requires; unit doubles only.
            Q.write(Path(diagnostics) / 'selected-test.json', {
                'runId': 'a' * 32, 'ownerId': 'a' * 32, 'terminalStatus': 'cancelled',
                'terminalOutcome': {'exitCode': 'Cancelled'}, 'terminalAtUtc': '2026-11-03T00:00:00Z',
                'sourceMappingHash': None, 'terminalCause': 'run-cancelled'})
            Q.write(Path(diagnostics) / 'selected-source-mapping.json', value.prepared_mapping)
    value.down = down
    return value


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
            # A linked ancestor is refused even though the final component is an ordinary single-link file.
            (root / 'real' / 'dir').mkdir(parents=True)
            (root / 'real' / 'dir' / 'x.json').write_text('{"foreign": true}')
            (root / 'link').symlink_to(root / 'real', target_is_directory=True)
            (root / 'plain').mkdir()
            (root / 'plain' / 'dir').symlink_to(root / 'real' / 'dir', target_is_directory=True)
            for path in (root / 'link' / 'dir' / 'x.json', root / 'plain' / 'dir' / 'x.json'):
                with self.subTest(linkedParent=str(path.relative_to(root))), self.assertRaisesRegex(ValueError, 'linked'):
                    Q.read(path)
            self.assertEqual(Q.read(root / 'real' / 'dir' / 'x.json'), {'foreign': True})

    def test_oversized_nonregular_and_missing_metadata_keep_their_outcomes(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            oversized = root / 'oversized.json'
            with oversized.open('wb') as output:
                output.truncate(8_388_609)
            fifo = root / 'fifo.json'
            os.mkfifo(fifo)
            (root / 'directory.json').mkdir()
            listener = socket.socket(socket.AF_UNIX)
            previous = os.getcwd()
            try:
                os.chdir(root)  # A relative bind keeps the socket path inside the AF_UNIX length limit.
                listener.bind('socket.json')
            finally:
                os.chdir(previous)
            self.addCleanup(listener.close)
            for path in (oversized, fifo, root / 'directory.json', root / 'socket.json'):
                with self.subTest(path=path.name), self.assertRaisesRegex(ValueError, 'nonregular or oversized'):
                    Q.read(path)
            # Ancestors need only search permission, as for the previous path-based reader.
            (root / 'search-only').mkdir()
            (root / 'search-only' / 'x.json').write_text('{"searchable": true}')
            (root / 'search-only').chmod(0o311)
            try:
                self.assertEqual(Q.read(root / 'search-only' / 'x.json'), {'searchable': True})
            finally:
                (root / 'search-only').chmod(0o755)
            with self.assertRaises(FileNotFoundError):
                Q.read(root / 'missing' / 'x.json')
            (root / 'empty.json').write_text('')
            with self.assertRaises(json.JSONDecodeError):
                Q.read(root / 'empty.json')

    def test_metadata_swapped_after_link_checks_is_refused_not_followed(self):
        """Replace the metadata or its parent right after the reader's own link checks, then require a refusal."""
        for swap in ('parent-symlink', 'file-symlink', 'file-hardlink'):
            with self.subTest(swap=swap), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                (root / 'meta').mkdir()
                target = root / 'meta' / 'x.json'
                target.write_text('{"genuine": true}')
                (root / 'outside').mkdir()
                (root / 'outside' / 'x.json').write_text('{"foreign": true}')
                trigger = 'meta' if swap == 'parent-symlink' else 'x.json'
                swapped = []

                def replace():
                    if swapped:
                        return
                    swapped.append(swap)
                    if swap == 'parent-symlink':
                        (root / 'meta').rename(root / 'meta-held')
                        (root / 'meta').symlink_to(root / 'outside', target_is_directory=True)
                    else:
                        target.rename(root / 'x.json.held')
                        if swap == 'file-symlink':
                            target.symlink_to(root / 'outside' / 'x.json')
                        else:
                            os.link(root / 'outside' / 'x.json', target)

                real_open, real_stat = os.open, os.stat

                def opening(name, *args, **kwargs):
                    # A component-wise reader opens each name itself: swap just before that open.
                    if os.fspath(name) == trigger:
                        replace()
                    return real_open(name, *args, **kwargs)

                def checking(name, *args, **kwargs):
                    # A path-based reader checks the whole path first: swap right after its following stat.
                    # Python 3.12 routes is_symlink() through os.stat(follow_symlinks=False); skip that probe.
                    result = real_stat(name, *args, **kwargs)
                    if os.fspath(name) == os.fspath(target) and kwargs.get('follow_symlinks', True):
                        replace()
                    return result

                with patch.object(Q.os, 'open', side_effect=opening), patch.object(Q.os, 'stat', side_effect=checking):
                    with self.assertRaisesRegex(ValueError, 'linked|nonregular'):
                        Q.read(target)
                self.assertEqual(swapped, [swap])
                self.assertEqual((root / 'outside' / 'x.json').read_text(), '{"foreign": true}')

    def test_metadata_swapped_after_the_descriptor_check_returns_the_held_bytes(self):
        """Swap the file or its parent after fstat admits the descriptor; only the held bytes may be returned."""
        for swap in ('parent-symlink', 'file-symlink', 'file-hardlink', 'file-replaced'):
            with self.subTest(swap=swap), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                (root / 'meta').mkdir()
                target = root / 'meta' / 'x.json'
                target.write_text('{"genuine": true}')
                (root / 'outside').mkdir()
                (root / 'outside' / 'x.json').write_text('{"foreign": true}')
                swapped = []
                real_fstat = os.fstat

                def admitted(descriptor):
                    # A reader that reopened the path by name after this check would now read the foreign bytes.
                    result = real_fstat(descriptor)
                    if not swapped:
                        swapped.append(swap)
                        if swap == 'parent-symlink':
                            (root / 'meta').rename(root / 'meta-held')
                            (root / 'meta').symlink_to(root / 'outside', target_is_directory=True)
                        else:
                            target.rename(root / 'x.json.held')
                            if swap == 'file-symlink':
                                target.symlink_to(root / 'outside' / 'x.json')
                            elif swap == 'file-hardlink':
                                os.link(root / 'outside' / 'x.json', target)
                            else:
                                target.write_text('{"foreign": true}')
                    return result

                with patch.object(Q.os, 'fstat', side_effect=admitted):
                    self.assertEqual(Q.read(target), {'genuine': True})
                self.assertEqual(swapped, [swap])

    def test_writer_and_directory_admission_refuse_a_parent_swapped_after_the_check(self):
        """Swap an ancestor to a link right after its admission check; nothing may be created or written through it."""
        for operation in ('write', 'directory'):
            with self.subTest(operation=operation), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                parent = root / 'meta'
                parent.mkdir()
                (parent / 'x.json').write_text('{"genuine": true}')
                (root / 'outside').mkdir()
                (root / 'outside' / 'x.json').write_text('{"foreign": true}')
                swapped = []

                def replace():
                    if not swapped:
                        swapped.append(operation)
                        parent.rename(root / 'meta-held')
                        parent.symlink_to(root / 'outside', target_is_directory=True)

                real_open, real_stat, real_lstat = os.open, os.stat, os.lstat

                def opening(name, *args, **kwargs):
                    # A held walk admits each component by opening it: swap just before that open.
                    if os.fspath(name) == 'meta':
                        replace()
                    return real_open(name, *args, **kwargs)

                def checking(name, *args, **kwargs):
                    # A by-name walk checks the whole ancestor first: swap right after its no-follow probe.
                    result = real_stat(name, *args, **kwargs)
                    if os.fspath(name) == os.fspath(parent) and not kwargs.get('follow_symlinks', True):
                        replace()
                    return result

                def probing(name, *args, **kwargs):
                    result = real_lstat(name, *args, **kwargs)
                    if os.fspath(name) == os.fspath(parent):
                        replace()
                    return result

                with patch.object(Q.os, 'open', side_effect=opening), patch.object(Q.os, 'stat', side_effect=checking), \
                        patch.object(Q.os, 'lstat', side_effect=probing):
                    with self.assertRaisesRegex(ValueError, 'linked'):
                        if operation == 'write':
                            Q.write(parent / 'x.json', {'replacement': True})
                        else:
                            Q.private_directory(parent / 'sub' / 'leaf')
                self.assertEqual(swapped, [operation])
                self.assertEqual(sorted(os.listdir(root / 'outside')), ['x.json'])
                self.assertEqual((root / 'outside' / 'x.json').read_text(), '{"foreign": true}')
                self.assertEqual(sorted(os.listdir(root / 'meta-held')), ['x.json'])
                self.assertEqual((root / 'meta-held' / 'x.json').read_text(), '{"genuine": true}')

    def test_writer_refuses_a_parent_swapped_after_admission(self):
        """Replace the admitted parent before the temporary file exists; the writer must refuse, not follow or move it."""
        for swap, refusal in (('symlink', 'linked'), ('directory', 'replaced')):
            with self.subTest(swap=swap), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                parent = root / 'meta'
                parent.mkdir()
                (parent / 'x.json').write_text('{"genuine": true}')
                (root / 'outside').mkdir()
                (root / 'outside' / 'x.json').write_text('{"foreign": true}')
                swapped = []
                real_open = os.open

                def opening(name, *args, **kwargs):
                    entry = os.path.basename(os.fspath(name))
                    if not swapped and entry.startswith('x.json.') and entry.endswith('.new'):
                        swapped.append(entry)
                        parent.rename(root / 'meta-held')
                        if swap == 'symlink':
                            parent.symlink_to(root / 'outside', target_is_directory=True)
                        else:
                            # A real directory is no link, so only the held parent's identity check refuses it.
                            parent.mkdir()
                    return real_open(name, *args, **kwargs)

                with patch.object(Q.os, 'open', side_effect=opening):
                    with self.assertRaisesRegex(ValueError, refusal):
                        Q.write(parent / 'x.json', {'replacement': True})
                self.assertEqual(len(swapped), 1)
                self.assertEqual(sorted(os.listdir(root / 'outside')), ['x.json'])
                self.assertEqual((root / 'outside' / 'x.json').read_text(), '{"foreign": true}')
                if swap == 'directory':
                    self.assertEqual(os.listdir(parent), [])
                # The refused temporary is removed from the held directory, and the genuine file is unchanged.
                self.assertEqual(sorted(os.listdir(root / 'meta-held')), ['x.json'])
                self.assertEqual((root / 'meta-held' / 'x.json').read_text(), '{"genuine": true}')

    def test_writer_never_installs_a_final_entry_swapped_before_replace(self):
        """Swap the temporary or destination entry right before the rename; only the written file may be installed."""
        for swap in ('temporary-symlink', 'temporary-hardlink', 'destination-symlink', 'destination-hardlink'):
            with self.subTest(swap=swap), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                parent = root / 'meta'
                parent.mkdir()
                target = parent / 'x.json'
                target.write_text('{"genuine": true}')
                (root / 'outside').mkdir()
                foreign = root / 'outside' / 'x.json'
                foreign.write_text('{"foreign": true}')
                swapped = []
                real_replace = os.replace

                def replacing(source, destination, *args, **kwargs):
                    if not swapped:
                        swapped.append(swap)
                        entry = parent / (os.path.basename(os.fspath(source)) if swap.startswith('temporary') else 'x.json')
                        entry.rename(root / 'held')
                        if swap.endswith('symlink'):
                            entry.symlink_to(foreign)
                        else:
                            os.link(foreign, entry)
                    return real_replace(source, destination, *args, **kwargs)

                with patch.object(Q.os, 'replace', side_effect=replacing):
                    if swap.startswith('temporary'):
                        with self.assertRaisesRegex(ValueError, 'Unsafe evidence destination'):
                            Q.write(target, {'replacement': True})
                    else:
                        Q.write(target, {'replacement': True})
                self.assertEqual(swapped, [swap])
                self.assertEqual(foreign.read_text(), '{"foreign": true}')
                if swap.startswith('temporary'):
                    # A refused install never leaves a foreign link at the destination.
                    self.assertFalse(os.path.lexists(target))
                else:
                    info = os.lstat(target)
                    self.assertTrue(Q.stat.S_ISREG(info.st_mode))
                    self.assertEqual((info.st_nlink, info.st_mode & 0o777), (1, 0o600))
                    self.assertEqual(Q.read(target), {'replacement': True})
                    self.assertEqual(os.stat(foreign).st_nlink, 1)
                self.assertEqual(sorted(name for name in os.listdir(parent) if name != 'x.json'), [])

    def test_ordinary_write_keeps_private_modes_and_atomic_replacement(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            target = root / 'created' / 'nested' / 'x.json'
            Q.write(target, {'first': True})
            Q.write(target, {'second': True})
            self.assertEqual(Q.read(target), {'second': True})
            self.assertEqual(target.read_text(), json.dumps({'second': True}, indent=2, sort_keys=True) + '\n')
            self.assertEqual((target.stat().st_mode & 0o777, target.stat().st_nlink), (0o600, 1))
            for directory in (root / 'created', root / 'created' / 'nested'):
                self.assertEqual(directory.stat().st_mode & 0o777, 0o700)
            self.assertEqual(os.listdir(target.parent), ['x.json'])
            self.assertEqual(Q.private_directory(root / 'created' / 'nested'), root / 'created' / 'nested')

    def test_public_result_is_created_in_its_held_parent_without_following_a_link(self):
        """A launch whose public-result parent became a link refuses before any output file or child exists."""
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / 'outside').mkdir()
            (root / 'passing').symlink_to(root / 'outside', target_is_directory=True)
            owner = object.__new__(Q.Qualification)
            item = {'label': 'passing', 'receipt': str(root / 'passing' / 'receipt.json'),
                    'launchRecord': str(root / 'passing' / 'launch.json'),
                    'publicResult': str(root / 'passing' / 'public-result.json')}
            with patch.object(Q.subprocess, 'Popen') as spawn:
                with self.assertRaisesRegex(ValueError, 'linked'):
                    owner.launch(item)
                spawn.assert_not_called()
            self.assertEqual(os.listdir(root / 'outside'), [])

    def test_directory_admission_refuses_an_existing_leaf_not_private_to_this_user(self):
        """An existing target owned by another uid, or open to group or other, is refused before use; shared
        pre-existing ancestors stay admissible."""
        for case in ('foreign-owner', 'group-bits', 'other-bits'):
            with self.subTest(case=case), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                leaf = root / 'leaf'
                leaf.mkdir(mode=0o700)
                if case == 'group-bits':
                    leaf.chmod(0o750)
                elif case == 'other-bits':
                    leaf.chmod(0o701)
                # Only root can create a directory for another uid, so the foreign case moves the effective owner.
                owner = os.geteuid() + 1 if case == 'foreign-owner' else os.geteuid()
                with patch.object(Q.os, 'geteuid', return_value=owner):
                    with self.assertRaisesRegex(ValueError, re.escape(str(leaf))):
                        Q.private_directory(leaf)
                self.assertEqual(os.listdir(leaf), [])
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            shared = root / 'shared'
            shared.mkdir()
            shared.chmod(0o755)
            (root / 'existing').mkdir(mode=0o700)
            for target in (shared / 'made' / 'leaf', root / 'existing'):
                with self.subTest(admitted=str(target.relative_to(root))):
                    self.assertEqual(Q.private_directory(target), target)
                    info = os.stat(target)
                    self.assertEqual((info.st_uid, info.st_mode & 0o077), (os.geteuid(), 0))
            self.assertEqual(os.stat(shared / 'made').st_mode & 0o777, 0o700)

    def test_directory_admission_refuses_a_component_swapped_between_mkdir_and_reopen(self):
        """Replace a component right after the call makes it; a different or non-empty directory is refused."""
        for position in ('intermediate', 'leaf'):
            for swap in ('different-directory', 'non-empty-directory'):
                with self.subTest(position=position, swap=swap), tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    target = root / 'sub' / 'leaf'
                    made = root / 'sub' if position == 'intermediate' else target
                    swapped = []
                    real_mkdir = os.mkdir

                    def making(name, *args, **kwargs):
                        result = real_mkdir(name, *args, **kwargs)
                        if not swapped and os.fspath(name) == made.name:
                            swapped.append(swap)
                            made.rename(root / 'made-held')
                            real_mkdir(made, 0o700)
                            if swap == 'different-directory':
                                os.chmod(made, 0o755)
                            else:
                                (made / 'planted').write_text('foreign')
                        return result

                    with patch.object(Q.os, 'mkdir', side_effect=making):
                        with self.assertRaisesRegex(ValueError, 'replaced'):
                            Q.private_directory(target)
                    self.assertEqual(swapped, [swap])
                    # Nothing was created inside the substituted directory, and the genuine one stays empty.
                    self.assertEqual(os.listdir(made), [] if swap == 'different-directory' else ['planted'])
                    self.assertEqual(os.listdir(root / 'made-held'), [])

    def test_retry_locks_refuse_a_link_or_nonregular_entry_without_creating_through_it(self):
        """Each retry lock is held only as a single-link regular file in the diagnostics directory."""
        for name in ('attachments.lock', 'provisioning.lock'):
            for entry in ('symlink', 'dangling-symlink', 'fifo', 'directory', 'hardlink'):
                with self.subTest(lock=name, entry=entry), tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    diagnostics = root / 'diagnostics'
                    diagnostics.mkdir()
                    outside = root / 'outside'
                    outside.mkdir()
                    lock = diagnostics / name
                    if entry in ('symlink', 'hardlink'):
                        (outside / 'target.lock').write_text('')
                        if entry == 'symlink':
                            lock.symlink_to(outside / 'target.lock')
                        else:
                            os.link(outside / 'target.lock', lock)
                    elif entry == 'dangling-symlink':
                        lock.symlink_to(outside / 'created.lock')
                    elif entry == 'fifo':
                        os.mkfifo(lock)
                    else:
                        lock.mkdir()
                    events = []
                    value = retry_controller(diagnostics, events)
                    with patch.object(Q.HELPER, 'retain_artifacts'), self.assertRaisesRegex(ValueError, 'linked|nonregular'):
                        value.retry_with_peer()
                    self.assertEqual(events, [])
                    self.assertEqual(sorted(os.listdir(outside)), ['target.lock'] if entry in ('symlink', 'hardlink') else [])
                    self.assertFalse(os.path.lexists(outside / 'created.lock'))
                    if entry in ('symlink', 'hardlink'):
                        self.assertEqual((outside / 'target.lock').read_text(), '')
        with tempfile.TemporaryDirectory() as temporary:
            # Ordinary locks are created private, and an existing single-link lock is reused in place so both
            # sides contend on the same inode.
            diagnostics = Path(temporary) / 'diagnostics'
            diagnostics.mkdir()
            (diagnostics / 'provisioning.lock').write_text('')
            existing = os.stat(diagnostics / 'provisioning.lock').st_ino
            events = []
            value = retry_controller(diagnostics, events)
            with patch.object(Q.HELPER, 'retain_artifacts'):
                value.retry_with_peer()
            self.assertEqual(events[:2], [('down', 'withdrawal-lock-refusal', False),
                                          ('down', 'provisioning-lock-refusal', False)])
            created = os.lstat(diagnostics / 'attachments.lock')
            self.assertTrue(Q.stat.S_ISREG(created.st_mode))
            self.assertEqual((created.st_mode & 0o777, created.st_nlink), (0o600, 1))
            self.assertEqual(os.stat(diagnostics / 'provisioning.lock').st_ino, existing)

    def test_launch_refuses_an_existing_launch_record_entry_or_a_separate_record_parent(self):
        """A dangling launchRecord link counts as an existing record, and the record must share publicResult's parent."""
        for case in ('dangling-link', 'separate-parent'):
            with self.subTest(case=case), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                (root / 'passing').mkdir(mode=0o700)
                (root / 'other').mkdir(mode=0o700)
                (root / 'outside').mkdir()
                record = root / ('passing' if case == 'dangling-link' else 'other') / 'launch.json'
                if case == 'dangling-link':
                    record.symlink_to(root / 'outside' / 'launch.json')
                owner = object.__new__(Q.Qualification)
                owner.children = {}
                # Reached only after a launch: the baseline's identity check then refuses with another message.
                owner.recover_launch = lambda item: {'pid': 1, 'birth': '1', 'gateStatus': 'waiting'}
                item = {'label': 'passing', 'receipt': str(root / 'passing' / 'receipt.json'),
                        'launchRecord': str(record), 'publicResult': str(root / 'passing' / 'public-result.json')}
                with patch.object(Q.subprocess, 'Popen') as spawn:
                    with self.assertRaisesRegex(ValueError, '(?i)launch process record'):
                        owner.launch(item)
                    spawn.assert_not_called()
                self.assertFalse(os.path.lexists(root / 'passing' / 'public-result.json'))
                self.assertEqual(os.listdir(root / 'outside'), [])

    def test_peer_scope_hashes_the_validated_claim_bytes(self):
        """peer_scope digests the exact claim bytes it validated, even if the claim is replaced afterwards."""
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            run_id = 'a' * 32
            claim = 'c' * 64
            claims = root / 'g4-runs' / 'claims'
            (claims / 'owners').mkdir(parents=True)
            (root / 'workspace').mkdir()
            Q.write(claims / 'owners' / (run_id + '.json'), [claim])
            genuine = json.dumps({'OwnerRunId': run_id}).encode()
            (claims / (claim + '.json')).write_bytes(genuine)
            value = object.__new__(Q.Qualification)
            value.issued = lambda item: {'runId': run_id, 'stateDirectory': str(root / 'g4-runs')}
            value.state = lambda item: {'status': 'Ready', 'testRun': None, 'workspace': str(root / 'workspace'),
                                        'appHostProcessId': os.getpid()}
            value.containers = lambda item: ['container']
            real_fstat = os.fstat
            replaced = []

            def admitted(descriptor):
                result = real_fstat(descriptor)
                if not replaced and os.readlink(f'/proc/self/fd/{descriptor}').endswith(claim + '.json'):
                    replaced.append(True)
                    (claims / (claim + '.json')).rename(claims / 'held.json')
                    (claims / (claim + '.json')).write_text(json.dumps({'OwnerRunId': run_id, 'replaced': True}))
                return result

            with patch.object(Q.os, 'fstat', side_effect=admitted):
                scope = value.peer_scope({'label': 'peer'})
            self.assertEqual(replaced, [True])
            self.assertEqual(scope['claimHashes'], {claim: Q.hashlib.sha256(genuine).hexdigest()})
            for invalid in ([run_id], {'OwnerRunId': 'b' * 32}):
                with self.subTest(claim=invalid):
                    (claims / (claim + '.json')).unlink()
                    (claims / (claim + '.json')).write_text(json.dumps(invalid))
                    with self.assertRaisesRegex(ValueError, 'Peer claim ownership changed'):
                        value.peer_scope({'label': 'peer'})

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
        with patch.object(Q, 'birth', return_value='replacement'), patch.object(Q.os, 'killpg') as group, \
                patch.object(Q.os, 'kill') as kill:
            with self.assertRaises(ValueError):
                controller.process_signal({'pid': 123, 'birth': 'original', 'processGroup': 123}, 2)
            group.assert_not_called()
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
