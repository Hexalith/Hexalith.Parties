#!/usr/bin/env python3
"""Explicit hosted lifecycle observations; local controller tests provide no hosted credit."""
from __future__ import annotations

import argparse
import errno
import fcntl
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import select
import shutil
import signal
import stat
import subprocess
import sys
import time
from datetime import datetime, timedelta, timezone
from xml.etree import ElementTree

ROOT = Path(__file__).resolve().parents[1]


class _PinnedHelper:
    """Load the pinned Builds integration helper on first use, so private reader removal never depends on it."""

    _module = None

    def __getattr__(self, name):
        module = type(self)._module
        if module is None:
            spec = importlib.util.spec_from_file_location(
                'integration_owner', ROOT / 'references/Hexalith.Builds/Github/run-module-integration/run_module_integration.py')
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            type(self)._module = module
        return getattr(module, name)


HELPER = _PinnedHelper()
# Each injected lock has exactly one runtime refusal: the metadata lock HXR037, the provisioning lock HXR023.
LOCK_REFUSALS = {'withdrawal-lock-refusal': 'HXR037', 'provisioning-lock-refusal': 'HXR023'}
TOOL = ['dotnet', 'tool', 'run', 'hexalith-module', '--']
RUN_ID = re.compile(r'[0-9a-f]{32}')
LABEL = 'hexalith.g4.run'
MANIFEST = 'module/hexalith.module-manifest.v2.json'
PROFILE = 'lifecycle-package'
CANCELLED = []
ATTACHED_PROFILE = 'attached-lifecycle-package'
ATTACHED_ENTRY = 'tests/Hexalith.Parties.Platform.AttachedLifecycleTests/Hexalith.Parties.Platform.AttachedLifecycleTests.csproj'
POLICY = 'hexalith.platform.candidate-and-attachment-policy.v1'
INTENTIONAL_FAILURE = 'HEXALITH_ATTACHED_LIFECYCLE_INTENTIONAL_FAILURE:'
OWNER_LABELS = ('passing', 'failing', 'peer', 'retry', 'partial', 'cancel', 'collision-owner', 'collision-peer',
                'collision-challenger', 'timeout', 'attached-owner', 'expiry-owner', 'expiry-peer', 'owner-cancel', 'invalid-owner')
ATTACHED_LABELS = ('attached-failure-child', 'expiry-child', 'owner-cancel-child', 'cancel-child')
MATRIX_CASES = ('passing', 'failing', 'retry-with-peer', 'partial-startup', 'collision', 'startup-timeout',
                'owner-cancellation', 'attached-failure', 'hold-expiry', 'invalid-handoff')
SELECTIONS = {'matrix': MATRIX_CASES, 'focused-ci': ('passing', 'failing', 'partial-startup'),
              **{case: (case,) for case in MATRIX_CASES}}


class QualificationRefusal(ValueError):
    """A fixed public code; private exception text never enters retained diagnostics."""

    def __init__(self, code):
        if code not in ('HXLQ107', 'HXLQ108'):
            raise ValueError('Unknown qualification refusal code')
        self.code = code
        super().__init__('Lifecycle qualification refused')


def failure_metadata(error, operation=None):
    """Return a bounded allowlist of metadata, independent of paths and payloads."""
    names = ('ValueError', 'KeyError', 'TypeError', 'OSError', 'FileNotFoundError',
             'PermissionError', 'ProcessLookupError', 'TimeoutError', 'TimeoutExpired',
             'CalledProcessError', 'SubprocessError', 'JSONDecodeError', 'QualificationRefusal')
    name = type(error).__name__
    name = name if name in names else 'Exception'
    if isinstance(error, QualificationRefusal):
        code = error.code
    elif operation == 'remove-reader':
        code = 'HXLQ106'
    elif isinstance(error, (TimeoutError, subprocess.TimeoutExpired)):
        code = 'HXLQ104'
    elif isinstance(error, FileNotFoundError):
        code = 'HXLQ101'
    elif isinstance(error, OSError):
        code = 'HXLQ103'
    elif isinstance(error, subprocess.SubprocessError):
        code = 'HXLQ105'
    else:
        code = 'HXLQ102'
    return {'failureCode': code, 'failureType': name, 'redacted': True}


def verify_runtime_cache(packages, version):
    """Observe restored binaries without starting them; missing/partial caches refuse."""
    require(isinstance(version, str) and re.fullmatch(r'[0-9]+(?:[.][0-9]+){2}(?:-[0-9a-z.-]+)?', version),
            'Invalid verified Aspire version')
    root = Path(packages)
    observations = []
    for component, binary in [('aspire.hosting.orchestration.linux-x64', 'dcp'),
                              ('aspire.dashboard.sdk.linux-x64', 'Aspire.Dashboard')]:
        path = root / component / version / 'tools' / binary
        try:
            require(not any(part.is_symlink() for part in (path, *path.parents)), 'Linked runtime cache')
            info = path.stat()
            require(stat.S_ISREG(info.st_mode) and 0 < info.st_size <= 536_870_912 and os.access(path, os.X_OK),
                    'Missing executable runtime cache')
        except (OSError, ValueError):
            raise QualificationRefusal('HXLQ108') from None
        observations.append({'component': component, 'version': version, 'executablePresent': True,
                             'bytes': info.st_size})
    return observations


def now():
    return datetime.now(timezone.utc).isoformat()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def open_unlinked(name, flags, parent, *, final=False, mode=0o600):
    """Open one component relative to its held parent; a link there is refused instead of followed.

    A final entry that cannot be opened as a file keeps its nonregular refusal (a socket raises ENXIO, for example).
    The mode applies only when flags create the entry.
    """
    try:
        return os.open(name, flags | os.O_NOFOLLOW | os.O_CLOEXEC, mode, dir_fd=parent)
    except OSError as error:
        if error.errno == errno.ENOENT:
            raise
        try:
            mode = os.lstat(name, dir_fd=parent).st_mode
        except OSError:
            raise error from None
        if stat.S_ISLNK(mode):
            raise ValueError('Refusing linked qualification metadata') from None
        if final and not stat.S_ISREG(mode):
            raise ValueError('Refusing nonregular or oversized qualification metadata') from None
        raise


def private_to_user(info):
    """Whether a directory's status shows this effective user as owner and no group or other permission bits."""
    return info.st_uid == os.geteuid() and not info.st_mode & 0o077


def made_directory(component, parent, path):
    """Reopen a component this call just made; only an empty directory private to this user is the one it made.

    The reopened descriptor is readable, so emptiness is checked on the held directory itself, never by name.
    Linux has no mkdir that returns a descriptor, so an empty 0o700 directory of this user substituted between the mkdir
    and the reopen is indistinguishable from the one made and is still admitted.
    """
    try:
        child = open_unlinked(component, os.O_RDONLY | os.O_DIRECTORY, parent)
    except NotADirectoryError:
        raise ValueError(f'Refusing a replaced qualification directory: {path}') from None
    try:
        info = os.fstat(child)
        require(stat.S_ISDIR(info.st_mode) and private_to_user(info) and not os.listdir(child),
                f'Refusing a replaced qualification directory: {path}')
        return child
    except BaseException:
        os.close(child)
        raise


def held_directory(path, create=False):
    """Return a descriptor for a directory opened component by component without following links.

    Each component is opened relative to its held parent, so search permission suffices and a link at any component
    is refused. With create, a missing component is made 0o700 relative to its held parent and reopened there; it is
    admitted only as an empty directory owned by this user without group or other bits, so a directory substituted
    between the mkdir and the reopen is refused, except an empty 0o700 directory of this same user, which no check can tell
    from the one made. A component made concurrently by another caller is admitted as an existing one. Nothing is created
    by path. The caller closes the descriptor.
    """
    path = Path(path).absolute()
    descriptor = os.open(path.anchor, os.O_PATH | os.O_DIRECTORY | os.O_CLOEXEC)
    try:
        for component in path.parts[1:]:
            try:
                child = open_unlinked(component, os.O_PATH | os.O_DIRECTORY, descriptor)
            except FileNotFoundError:
                if not create:
                    raise
                try:
                    os.mkdir(component, 0o700, dir_fd=descriptor)
                except FileExistsError:
                    child = open_unlinked(component, os.O_PATH | os.O_DIRECTORY, descriptor)
                else:
                    child = made_directory(component, descriptor, path)
            os.close(descriptor)
            descriptor = child
        return descriptor
    except BaseException:
        os.close(descriptor)
        raise


def read_bytes(path, limit=8_388_608):
    """Read held bytes under the selected artifact bound or explicit operational metadata contract.

    Ancestors are held through held_directory, and size and link count are checked on the open file before its
    bytes are read from it. A symbolic link at any component or a hard-linked file is refused. A file or ancestor
    replaced after it was opened cannot change the result, because the bytes come only from the held descriptor and
    the path is never reopened by name. An explicit None limit admits operational journal/observation metadata
    under the same uncapped contract as write(); artifact reads retain their default or declared byte bounds.
    """
    path = Path(path).absolute()
    descriptor = held_directory(path.parent)
    try:
        child = open_unlinked(path.name, os.O_RDONLY | os.O_NONBLOCK | os.O_NOCTTY, descriptor, final=True)
        os.close(descriptor)
        descriptor = child
        info = os.fstat(descriptor)
        require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1 and (limit is None or info.st_size <= limit),
                'Refusing nonregular or oversized qualification metadata')
        body = bytearray()
        while limit is None or len(body) <= limit:
            chunk = os.read(descriptor, 65536 if limit is None else min(65536, limit + 1 - len(body)))
            if not chunk:
                break
            body.extend(chunk)
        require(limit is None or len(body) <= limit, 'Refusing nonregular or oversized qualification metadata')
        return bytes(body)
    finally:
        os.close(descriptor)


def read(path, limit=8_388_608):
    """Read JSON through the selected read_bytes contract, retaining its link and replacement refusals."""
    return json.loads(read_bytes(path, limit).decode('utf-8'))


def optional_read_bytes(path, limit=8_388_608):
    """Return held metadata bytes, or None only when a component is genuinely absent."""
    try:
        return read_bytes(path, limit)
    except FileNotFoundError:
        return None


def optional_read(path, limit=8_388_608):
    """Admit an optional JSON object once; unsafe or malformed records never become absence."""
    body = optional_read_bytes(path, limit)
    if body is None:
        return None
    try:
        value = json.loads(body.decode('utf-8'))
    except RecursionError:
        raise ValueError('Qualification metadata exceeds the decoder depth limit') from None
    require(isinstance(value, dict), 'Qualification metadata must be an object')
    return value


def private_directory(path):
    """Admit a directory through held_directory, creating missing components 0o700; a link at any component is refused.

    The target itself, whether it existed or was just made, must be owned by this user with no group or other bits.
    Pre-existing shared ancestors such as / or the job's temporary root are admitted as they are.
    """
    descriptor = held_directory(path, create=True)
    try:
        require(private_to_user(os.fstat(descriptor)),
                f'Refusing a qualification directory that is not private to this user: {Path(path).absolute()}')
    finally:
        os.close(descriptor)
    return Path(path)


def open_lock(directory, name):
    """Open a lock file in its held directory without following a link or requiring exclusive creation.

    The controller and the tool must contend on the same inode, so an existing entry is reused; it must be a
    single-link regular file. A missing one is created 0o600. The caller closes the descriptor.
    """
    parent = held_directory(directory)
    try:
        # A link is refused by O_NOFOLLOW, so nothing is created through it; another entry type is refused by type.
        descriptor = open_unlinked(name, os.O_RDWR | os.O_CREAT, parent, final=True, mode=0o600)
    finally:
        os.close(parent)
    try:
        info = os.fstat(descriptor)
        require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1, 'Refusing nonregular or linked qualification lock')
        return descriptor
    except BaseException:
        os.close(descriptor)
        raise


def same_file(first, second):
    return (first.st_dev, first.st_ino) == (second.st_dev, second.st_ino)


def write(path, value):
    """Atomically replace JSON metadata inside its held parent without creating, writing or renaming by path.

    The temporary file is created exclusively without following links (0o600), written and fsynced through its own
    descriptor, and renamed relative to the held parent. A parent that no longer resolves to the held directory is
    refused before the rename, and a destination entry that is not the written file afterwards is removed and refused.
    """
    return write_atomic_bytes(path, (json.dumps(value, indent=2, sort_keys=True) + '\n').encode())


def write_retained_bytes(path, body):
    """Publish already validated retained bytes using the same held-directory atomic boundary."""
    require(isinstance(body, bytes) and len(body) <= 8_388_608, 'Retained artifact exceeds its bounded byte contract')
    return write_atomic_bytes(path, body)


def write_atomic_bytes(path, body):
    """Atomically publish metadata or bounded artifact bytes without following links."""
    path = Path(path).absolute()
    parent = held_directory(path.parent, create=True)
    temporary = None
    try:
        try:
            existing = os.lstat(path.name, dir_fd=parent)
        except FileNotFoundError:
            existing = None
        require(existing is None or (stat.S_ISREG(existing.st_mode) and existing.st_nlink == 1),
                'Unsafe evidence destination')
        for _ in range(100):
            candidate = f'{path.name}.{os.urandom(6).hex()}.new'
            try:
                descriptor = os.open(candidate, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC,
                                     0o600, dir_fd=parent)
            except FileExistsError:
                continue
            temporary = candidate
            break
        else:
            raise FileExistsError(errno.EEXIST, 'No unused temporary qualification metadata name')
        with os.fdopen(descriptor, 'wb') as output:
            output.write(body)
            output.flush()
            os.fsync(output.fileno())
            written = os.fstat(output.fileno())
        current = held_directory(path.parent)
        try:
            require(same_file(os.fstat(current), os.fstat(parent)), 'Refusing replaced qualification path')
        finally:
            os.close(current)
        os.replace(temporary, path.name, src_dir_fd=parent, dst_dir_fd=parent)
        temporary = None
        try:
            installed = os.lstat(path.name, dir_fd=parent)
        except FileNotFoundError:
            installed = None
        if installed is None or not same_file(installed, written):
            if installed is not None:
                os.unlink(path.name, dir_fd=parent)
            raise ValueError('Unsafe evidence destination')
    finally:
        try:
            if temporary is not None:
                try:
                    os.unlink(temporary, dir_fd=parent)
                except OSError:
                    pass  # Never replace the original outcome; a leftover temporary is never read as metadata.
        finally:
            os.close(parent)


def birth(pid):
    try:
        fields = Path(f'/proc/{pid}/stat').read_text().rpartition(')')[2].split()
        return None if fields[0] == 'Z' else fields[19]
    except (FileNotFoundError, ProcessLookupError):
        return None


def tool_processes(leader, leader_birth):
    """The gated launch leader (the tool muxer) and its live direct children; never the AppHost, DCP or test host.

    Composed processes are the tool's descendants, so the first interrupt reaches only the tool, which records
    withdrawal before it stops anything it composed.
    """
    if birth(leader) != leader_birth:
        return {}
    try:
        session = os.getsid(leader)
    except ProcessLookupError:
        return {}
    processes = {leader: leader_birth}
    for entry in os.scandir('/proc'):
        if not entry.name.isdigit() or int(entry.name) == leader:
            continue
        try:
            fields = Path(f'/proc/{entry.name}/stat').read_text().rpartition(')')[2].split()
        except (FileNotFoundError, ProcessLookupError, NotADirectoryError):
            continue
        if len(fields) > 19 and fields[0] not in ('Z', 'X') and int(fields[1]) == leader and int(fields[3]) == session:
            processes[int(entry.name)] = fields[19]
    return processes


def signal_tool_processes(processes, number):
    """Bind each signal to a stable process handle before verifying the recorded birth."""
    delivered = []
    for pid, born in sorted(processes.items()):
        try:
            descriptor = os.pidfd_open(pid)
        except ProcessLookupError:
            continue
        try:
            if birth(pid) != born:
                continue
            try:
                signal.pidfd_send_signal(descriptor, number)
            except ProcessLookupError:
                continue
            delivered.append(pid)
        finally:
            os.close(descriptor)
    return delivered


def gated_exec(item, command):
    """Test-controller handshake only; no CLI can execute before its real process record is durable."""
    pid = os.getpid()
    record = {key: item[key] for key in ('label', 'receipt', 'publicResult')}
    record.update(pid=pid, birth=birth(pid), processGroup=os.getpgrp(), gateStatus='waiting', observedAtUtc=now())
    require(record['birth'] is not None and record['processGroup'] == pid, 'Launch child is not its own actual process group')
    write(item['launchRecord'], record)
    readable, _, _ = select.select([sys.stdin.buffer], [], [], 30)
    if not readable or sys.stdin.buffer.read(1) != b'1':
        write(item['launchRecord'], {**record, 'gateStatus': 'closed', 'closedAtUtc': now()})
        return 1
    released = {**record, 'gateStatus': 'released', 'releasedAtUtc': now()}
    write(item['launchRecord'], released)
    if item.get('kind') == 'attached-suite':
        return supervise_attached_command(item, command, released)
    with open(os.devnull, 'rb') as empty:
        os.dup2(empty.fileno(), sys.stdin.fileno())
    os.execvp(command[0], command)


def supervise_attached_command(item, command, released):
    """Retain the real attached command exit across the original controller's own exit."""
    child = None
    child_birth = None
    pending_signals = []

    def flush_signals():
        # Keep early cancellation until both the child and its stable birth identity are bound.
        while child is not None and child_birth is not None:
            try:
                number = pending_signals.pop(0)
            except IndexError:
                return  # A reentrant signal handler may already have drained the queue.
            signal_tool_processes(tool_processes(child.pid, child_birth), number)

    def forward(number, _frame):
        # This attacher composes no services. Still target only its muxer/tool identities.
        pending_signals.append(number)
        flush_signals()

    for number in (signal.SIGINT, signal.SIGTERM):
        signal.signal(number, forward)
    with subprocess.Popen(command, stdin=subprocess.DEVNULL) as child:
        child_birth = birth(child.pid)
        flush_signals()
        exit_code = child.wait()
    write(item['launchRecord'], {**released, 'commandExitCode': exit_code, 'commandExitedAtUtc': now()})
    return exit_code


def launch_owner(label):
    context = HELPER.ci_context()
    require(ROOT == HELPER.job_root('GITHUB_WORKSPACE'), 'Launch child must use the actual checkout root')
    temporary = HELPER.job_root('RUNNER_TEMP') / 'hexalith-ci-qualification'
    journal = read(temporary / 'journal.json', limit=None)
    require(journal['executionContext'] == context and journal['source'] == os.environ.get('GITHUB_SHA'),
            'Launch intent belongs to another job or revision')
    matches = [item for item in journal['owners'] + journal.get('attachedRuns', []) if item['label'] == label]
    require(label in OWNER_LABELS + ATTACHED_LABELS and len(matches) == 1,
            'Launch child has no unique durable controller role')
    item = matches[0]
    for key, filename in [('receipt', 'receipt.json'), ('launchRecord', 'launch.json'), ('publicResult', 'public-result.json')]:
        require(Path(item[key]) == temporary / label / filename, 'Launch intent escaped its exact controller role')
    if item.get('kind') == 'attached-suite':
        owners = [value for value in journal['owners'] if value['label'] == item['ownerLabel']]
        require(label in ATTACHED_LABELS and len(owners) == 1, 'Attached launch lacks its unique original owner')
        owner = HELPER.receipt_owner(Path(owners[0]['receipt']), context)
        require(item['ownerRunId'] == owner['runId'] and item['environmentId'] == owner['environmentId']
                and item['executionContext'] == context and item['source'] == journal['source']
                and item['profile'] == ATTACHED_PROFILE and item['holdSeconds'] in (120, 180, 240),
                'Attached launch owner/job/profile/hold differs')
        return gated_exec(item, TOOL + ['test', '--manifest=' + MANIFEST, '--mode=package', '--profile=' + ATTACHED_PROFILE,
                                       '--attach-to=' + owner['runId'], '--hold-seconds=' + str(item['holdSeconds']), '--output=json'])
    command = TOOL + ['test', '--manifest=' + MANIFEST, '--mode=package', '--profile=' + PROFILE,
                      '--ci-owner-receipt=' + item['receipt'], '--output=json']
    if item.get('eventStorePort') is not None:
        require(type(item['eventStorePort']) is int and 1 <= item['eventStorePort'] <= 65535, 'Invalid requested endpoint')
        command.append('--eventstore-port=' + str(item['eventStorePort']))
    return gated_exec(item, command)


def observed_first_outcome(record):
    require(isinstance(record, dict) and isinstance(record.get('terminalOutcome'), dict),
            'First outcome must have the actual structured result fields')
    required = ('runId', 'ownerId', 'terminalStatus', 'terminalOutcome', 'terminalAtUtc')
    require(all(record.get(key) is not None for key in required), 'First-outcome identity, status, time or mapping is absent')
    require(RUN_ID.fullmatch(record['runId']), 'First outcome has an invalid issued run identity')
    utc(record['terminalAtUtc'])
    return {key: record.get(key) for key in (*required, 'sourceMappingHash', 'terminalCause')}


def first_outcome(record):
    first = observed_first_outcome(record)
    require(first['sourceMappingHash'] is not None, 'First-outcome identity, status, time or mapping is absent')
    return first


def remove_reader_directory(value, runner_temp):
    """Remove only the workflow's private reader tree, without following foreign links."""
    if not value:
        return
    path = Path(value)
    root = Path(runner_temp).resolve(strict=True)
    require(path.parent == root and path.name.startswith('hexalith-lifecycle-registry.')
            and not path.is_symlink(), 'Refusing a foreign credential directory')
    config = path / 'config.json'
    require(not config.is_symlink(), 'Refusing a linked credential file')
    config.unlink(missing_ok=True)
    if path.exists():
        require(shutil.rmtree.avoids_symlink_attacks, 'Safe private-directory removal is unavailable')
        shutil.rmtree(path)
    require(not path.exists() and not path.is_symlink(), 'Private reader removal could not be verified')


def declared_terminal(record, action):
    require(action in ('pass', 'fail'), 'Unknown declared qualification outcome')
    first = first_outcome(record)
    expected_status, expected_exit = ('completed', 'Success') if action == 'pass' else ('failed', 'ProductOrTest')
    require(record.get('terminalStatus') == expected_status, 'Declared suite terminal status differs from the released contract')
    require(record.get('terminalOutcome', {}).get('exitCode') == expected_exit,
            'Declared suite terminal outcome contradicts its required passing/failing result')
    return first


def utc(value):
    require(isinstance(value, str), 'An actual absolute UTC timestamp is required')
    parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(parsed.utcoffset() == timedelta(0), 'Timestamp must use UTC')
    return parsed


def cancellation_terminal(record, item, prior=None):
    first = first_outcome(record)
    require(item.get('cancellationPhase') in ('startup', 'execution', 'failure'), 'Missing actual cancellation phase')
    ready = utc(item.get('cancellationReadyAtUtc'))
    signals = item.get('cancellationSignals', [])
    require(signals and all(value.get('signal') in (signal.SIGINT, signal.SIGTERM) for value in signals),
            'Actual external cancellation signal evidence is absent')
    received = min(utc(value.get('observedAtUtc')) for value in signals)
    terminal = utc(first['terminalAtUtc'])
    require(ready <= received, 'Cancellation signal preceded the actual ready checkpoint')
    require(first['runId'] == item['runId'] and first['ownerId'] == item['runId'], 'Cancellation terminal owner differs')
    if prior is not None:
        require(first == prior, 'Finalization changed the first terminal outcome/time')
    if first['terminalStatus'] == 'failed':
        require(first['terminalOutcome'].get('exitCode') == 'ProductOrTest' and prior is not None and terminal <= received,
                'Cancellation is obscured by an unqualified product/prerequisite failure')
        # The API request happened before this runner signal. Only the parent's API receipt can order it.
        return 'product-failure-before-observed-signal'
    require(first['terminalStatus'] == 'cancelled' and first['terminalOutcome'].get('exitCode') == 'Cancelled',
            'Actual cancellation status/outcome differs from the released contract')
    require(received <= terminal <= datetime.now(timezone.utc), 'Cancellation terminal time is outside actual signal/finalization')
    return 'cancelled'



def startup_timeout_observation(result, original):
    """Require the exact original finite clock and declaration override, never a replacement timeout."""
    deadline = result['bindings']['deadlines']['startup']
    require(result['terminal']['outcome'] == 'failed' and result['terminal']['cause'] == 'startup-timeout'
            and deadline['origin'] == 'environment-requested'
            and utc(deadline['originAt']) == utc(original['createdAt'])
            and utc(deadline['expiresAt']) == utc(original['startupDeadlineUtc'])
            and deadline['effectiveSeconds'] == original['effectiveDeadlineSeconds']
            and deadline.get('actualDurationSeconds', -1) >= deadline['effectiveSeconds'],
            'Original startup timeout timing was not preserved')
    override = deadline['override']
    if original['effectiveDeadlineIsOverride']:
        require(override.get('state') == 'declared' and override.get('requestedSeconds') == original['effectiveDeadlineSeconds']
                and override.get('justification') == original['effectiveDeadlineJustification'],
                'Original startup override was not preserved')
    else:
        require(override == {'state': 'not-overridden'}, 'Default startup deadline acquired an override')
    require(utc(result['terminal']['recordedAt']) >= utc(deadline['expiresAt']), 'Startup failure preceded its original timeout')
    return deadline


def active_attachment(attachment, run_id):
    identities = ('ownerRunId', 'attachedRunId', 'originalAttachmentRequestId', 'environmentId')
    require(all(isinstance(attachment.get(key), str) and RUN_ID.fullmatch(attachment[key]) for key in identities),
            'Admission lacks real issued identities')
    require(attachment['ownerRunId'] == run_id and attachment.get('requestedHoldSeconds') == 240
            and attachment.get('effectiveHoldSeconds') == 240, 'Admission changed the requested finite hold')
    require(isinstance(attachment.get('acceptedAtUtc'), str) and isinstance(attachment.get('expiresAtUtc'), str),
            'Admission lacks actual acceptance/expiry timestamps')
    accepted = datetime.fromisoformat(attachment['acceptedAtUtc'].replace('Z', '+00:00'))
    expires = datetime.fromisoformat(attachment['expiresAtUtc'].replace('Z', '+00:00'))
    require(accepted.tzinfo is not None and expires - accepted == timedelta(seconds=240)
            and expires > datetime.now(timezone.utc), 'The original finite hold is not active')
    require(attachment.get('endedAtUtc') is None and attachment.get('outcome') is None, 'Admission hold already ended')


def same_expiry(first, observed):
    keys = ('originalAttachmentRequestId', 'attachedRunId', 'acceptedAtUtc', 'expiresAtUtc', 'effectiveHoldSeconds')
    require(all(first.get(key) == observed.get(key) for key in keys), 'Attachment lineage or original expiry changed')


def owned_native_process(pid, run_id):
    require(isinstance(pid, int) and pid > 1, 'Native checkpoint has no actual process identity')
    started = birth(pid)
    require(started is not None and ('HEXALITH_G4_RUN_ID=' + run_id).encode()
            in Path(f'/proc/{pid}/environ').read_bytes().split(b'\0'), 'Native execution is absent, reused or not owned')
    return {'processId': pid, 'birth': started}


def cancellation_phase(item, owner, context):
    if item.get('cancellationPhase') == 'failure':
        first = item.get('failureBeforeCancellation')
        require(first is not None and first['runId'] == first['ownerId'] == owner['runId']
                and first['terminalStatus'] == 'failed' and first['terminalOutcome'].get('exitCode') == 'ProductOrTest'
                and utc(first['terminalAtUtc']) <= utc(item['cancellationReadyAtUtc'])
                <= min(utc(value['observedAtUtc']) for value in item['cancellationSignals']),
                'Failure-before-cancellation lacks its original durable ordering')
        return
    snapshot = item.get('cancellationSnapshot')
    require(isinstance(snapshot, dict) and 'failureType' not in snapshot, 'Actual signal-time owner snapshot is unavailable')
    require(all(snapshot.get(key) == owner[key] for key in ('runId', 'attemptId', 'environmentId'))
            and snapshot.get('executionContext') == owner['executionContext'] == context, 'Signal-time owner/job binding differs')
    received = min(utc(value['observedAtUtc']) for value in item['cancellationSignals'])
    observed = utc(snapshot['observedAtUtc'])
    require(utc(item['cancellationReadyAtUtc']) <= received <= utc(snapshot['readStartedAtUtc']) <= observed,
            'Owner snapshot was not observed after the actual external signal')
    require(utc(snapshot['stateUpdatedAtUtc']) <= observed, 'Owner state time is ahead of the actual snapshot')
    require('firstOutcome' in snapshot and 'stateTestTerminal' in snapshot
            and snapshot['firstOutcome'] is None and snapshot['stateTestTerminal'] is None,
            'Owner had a terminal outcome at the cancellation phase observation')
    if item['cancellationPhase'] == 'startup':
        require(snapshot.get('status') == 'Starting' and snapshot.get('nativeExecution') is None,
                'Requested startup cancellation was not observed in actual Starting state')
        return
    require(item['cancellationPhase'] == 'execution' and snapshot.get('status') == 'Ready',
            'Requested execution cancellation was not observed in an active Ready owner')
    checkpoint = item['executionCheckpoint']
    require(checkpoint.get('runId') == owner['runId'] and checkpoint.get('realPersistenceAssertionsPassed') is True
            and snapshot.get('nativeExecution') == {key: checkpoint[key] for key in ('processId', 'birth')},
            'Original native execution is no longer running in the signal-time snapshot')
    if item.get('attachedExecutionCheckpoint'):
        expected = item['attachedExecutionCheckpoint']
        require(snapshot.get('attachedExecution') == {key: expected[key] for key in ('processId', 'birth')},
                'The actual attached suite was not running at operator cancellation')
    admitted, active = item['admission'], snapshot['admission']
    same_expiry(admitted, active)
    require(active.get('ownerRunId') == owner['runId'] and active.get('environmentId') == owner['environmentId']
            and active.get('requestedHoldSeconds') == active.get('effectiveHoldSeconds') == 240
            and active.get('endedAtUtc') is None and active.get('outcome') is None,
            'Signal-time admission is not the actual active original finite hold')
    require(utc(active['acceptedAtUtc']) <= received <= observed < utc(active['expiresAtUtc'])
            and utc(active['expiresAtUtc']) - utc(active['acceptedAtUtc']) == timedelta(seconds=240),
            'Original finite hold was not active at the actual signal and snapshot')


class Qualification:
    """Own only receipt-issued siblings from the current real GitHub job."""

    def __init__(self, finalizing=False):
        self.context = HELPER.ci_context()
        require(ROOT == HELPER.job_root('GITHUB_WORKSPACE'), 'Qualification must use the actual checkout root')
        self.temporary = private_directory(HELPER.job_root('RUNNER_TEMP') / 'hexalith-ci-qualification')
        self.evidence = private_directory(ROOT / 'TestResults/ci-lifecycle')
        self.journal_path = self.temporary / 'journal.json'
        self.children = {}
        head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
        require(head == os.environ.get('GITHUB_SHA'), 'Checkout is not the workflow source revision')
        HELPER.verify_tool(ROOT)
        mapping = None
        if not finalizing:
            HELPER.verify_preparation()
            mapping = read(Path(os.environ['CustomAfterMicrosoftCommonProps']).parent / 'mapping.json')
            require(mapping.get('Root') == str(ROOT) and mapping.get('ContentHash') == os.environ['HEXALITH_SOURCE_MAPPING_HASH'],
                    'Qualification mapping differs from the strict prepared source')
        self.prepared_mapping = mapping
        journal = optional_read(self.journal_path, limit=None)
        self.journal = journal if journal is not None else {
            'schema': 'hexalith.ci-lifecycle-qualification.v1', 'classification': 'baseline-only',
            'source': head, 'mappingHash': mapping['ContentHash'] if mapping else None,
            'executionContext': self.context, 'owners': [], 'observations': []}
        require(self.journal['executionContext'] == self.context and self.journal['source'] == head,
                'Refusing another job or revision journal')
        require(isinstance(self.journal.get('mappingHash'), str) and re.fullmatch(r'[0-9a-f]{64}', self.journal['mappingHash']),
                'The durable journal lacks the actual prepared mapping identity')
        if mapping is not None:
            require(self.journal['mappingHash'] == mapping['ContentHash'], 'Prepared mapping changed within this job')
        self.save()
        if mapping is not None and optional_read(self.evidence / 'inputs.json') is None:
            write(self.evidence / 'inputs.json', {'source': head, 'executionContext': self.context,
                  'toolManifestSha256': hashlib.sha256((ROOT / '.config/dotnet-tools.json').read_bytes()).hexdigest(),
                  'mappingHash': mapping['ContentHash'], 'platformIdentity': mapping.get('PlatformIdentity'),
                  'candidateAcceptance': False})

    def save(self):
        write(self.journal_path, self.journal)
        write(self.evidence / 'observations.json', self.journal)

    def observe(self, kind, **values):
        item = {'kind': kind, 'observedAtUtc': now(), **values}
        self.journal['observations'].append(item)
        self.save()
        print(json.dumps(item), flush=True)

    def checkpoint(self, scenario, step):
        require((scenario, step) in {
            *(( 'matrix', case) for case in MATRIX_CASES),
            ('retry', 'launch-owners'), ('retry', 'ready-owners'), ('retry', 'withdrawal-refusal'),
            ('retry', 'first-outcome'), ('retry', 'resources-after-refusal'), ('retry', 'provisioning-refusal'),
            ('retry', 'withdrawal-before-stop'), ('retry', 'peer-before-retry'), ('retry', 'cleanup-retry'),
            ('retry', 'peer-after-retry'), ('retry', 'peer-completion'),
            ('partial', 'interrupt'), ('partial', 'cleanup'), ('partial', 'cleanup-retry'),
        }, 'Unknown qualification checkpoint')
        self.journal['checkpoint'] = {'scenario': scenario, 'step': step}
        self.observe('scenario-checkpoint', **self.journal['checkpoint'])

    def record_failure(self, error, phase):
        require(phase in ('scenario', 'finalization'), 'Unknown qualification failure phase')
        failure = {'phase': phase, 'checkpoint': self.journal.get('checkpoint'),
                   **failure_metadata(error), 'observedAtUtc': now()}
        self.journal.setdefault('firstFailure', failure)
        try:
            self.observe('qualification-failure', failure=failure, firstFailure=self.journal['firstFailure'])
        except (OSError, ValueError, KeyError, TypeError) as observation_error:
            print(json.dumps({'kind': 'failure-observation-unverified', 'failure': failure,
                              'observationFailureType': type(observation_error).__name__}), file=sys.stderr, flush=True)

    def owner(self, label):
        return next(item for item in self.journal['owners'] if item['label'] == label)

    def issued(self, item):
        require(item.get('label') in OWNER_LABELS, 'Unknown controller owner role')
        expected = self.temporary / item['label'] / 'receipt.json'
        require(Path(item['receipt']) == expected, 'Receipt escaped its issued controller role')
        return HELPER.receipt_owner(expected, self.context)

    def receipt_present(self, item):
        """Check receipt admission as held regular-file bytes, leaving owner validation to issued()."""
        return optional_read_bytes(Path(item['receipt'])) is not None

    def state(self, item):
        owner = self.issued(item)
        directory = Path(owner['stateDirectory'])
        paths = [directory / (owner['runId'] + '.json'), directory / 'diagnostics' / owner['runId'] / 'owner.json']
        value = None
        for path in paths:
            value = optional_read(path)
            if value is not None:
                break
        if value is None:
            raise FileNotFoundError('Owner metadata has not yet been published')
        require(value.get('runId') == owner['runId'] and value.get('executionContext') == self.context,
                'Owner metadata lost its actual job binding')
        return value

    def diagnostics(self, item):
        owner = self.issued(item)
        return Path(owner['stateDirectory']) / 'diagnostics' / owner['runId']

    def control(self, item, action):
        require(action in ('wait', 'pass', 'fail'), 'Unsupported fixture action')
        owner = self.issued(item)
        directory = private_directory(Path(owner['stateDirectory']).parent / 'ci-qualification' / owner['runId'])
        value = {'runId': owner['runId'], 'action': action}
        write(directory / 'control.json', value)
        self.observe('fixture-control', ownerRunId=owner['runId'], action=action,
                     configurationSha256=hashlib.sha256(json.dumps(value, sort_keys=True).encode()).hexdigest())
        return directory

    def tool(self, item, arguments, tag, environment=None):
        owner = self.issued(item)
        result = subprocess.run(TOOL + arguments, cwd=ROOT, env=environment, stdin=subprocess.DEVNULL,
                                capture_output=True, text=True, timeout=150, check=False)
        value = HELPER.read_json(result.stdout)
        write(self.evidence / (item['label'] + '-' + tag + '.json'), value)
        self.observe(tag, ownerRunId=owner['runId'], exitCode=result.returncode, result=value)
        return result, value

    def containers(self, item):
        owner = self.issued(item)
        output = subprocess.check_output(['docker', 'ps', '-aq', '--filter', f'label={LABEL}={owner["runId"]}'], text=True, timeout=20)
        identities = output.split()
        require(all(re.fullmatch(r'[0-9a-f]{12,64}', value) for value in identities), 'Docker discovery returned invalid identities')
        return identities

    def running_owner_inventory(self, item):
        """Observe exact running composed resources; exited containers and terminated/reused processes refuse."""
        owner = self.issued(item)
        state = self.state(item)
        ids = subprocess.check_output(['docker', 'ps', '-q', '--no-trunc', '--filter', f'label={LABEL}={owner["runId"]}'], text=True, timeout=20).split()
        require(ids and all(re.fullmatch(r'[0-9a-f]{64}', identity) for identity in ids), 'Original running owner containers are unavailable')
        containers = {}
        for identity in ids:
            template = '{{json .Id}}\t{{json .State.Running}}\t{{json .State.StartedAt}}\t{{json (index .Config.Labels "' + LABEL + '")}}'
            fields = subprocess.check_output(['docker', 'inspect', '--format', template, identity], text=True, timeout=20).strip().split('\t')
            require(len(fields) == 4, 'Running owner container observation is malformed')
            observed, running, started, run_id = map(json.loads, fields)
            require(observed == identity and running is True and run_id == owner['runId'], 'An original owner container stopped or changed')
            utc(started)
            containers[identity] = started
        processes = {'apphost': state.get('appHostProcessId')}
        for resource in state.get('readinessResults', []):
            if resource.get('kind') in ('project', 'executable'):
                require(resource.get('processId') is not None, 'A composed owner resource has no process identity')
                processes[resource['name']] = resource['processId']
        observed_processes = {}
        for name, pid in processes.items():
            observed_processes[name] = owned_native_process(pid, owner['runId'])
        return {'containers': containers, 'processes': observed_processes}

    def verify_original_owner_command(self, item, cancelled=False):
        expected_exit, expected_outcome = (130, 'Cancelled') if cancelled else (0, 'Success')
        require(self.capture_result(item) == expected_exit, 'Original owner command failed its expected exit contract')
        public = read(Path(item['publicResult']))
        require(public.get('runId') == self.issued(item)['runId'] and public.get('outcome', {}).get('exitCode') == expected_outcome,
                'Original owner command public outcome differs')
        require(isinstance(public.get('diagnostics'), list) and all(
            diagnostic.get('category') == 'None' or (cancelled and diagnostic.get('category') == 'Cancelled' and diagnostic.get('ruleId') == 'HXC130')
            for diagnostic in public['diagnostics']), 'Original owner command reported a cleanup or evidence failure')

    def peer_scope(self, item):
        owner = self.issued(item)
        state = self.state(item)
        require(state['status'] == 'Ready' and state.get('testRun') is None, 'Peer is not an active ready owner')
        workspace = Path(state['workspace'])
        require(workspace.is_dir() and not workspace.is_symlink(), 'Peer workspace is unavailable')
        pid = state.get('appHostProcessId')
        require(pid is not None and birth(pid) is not None, 'Peer AppHost is unavailable')
        claims_root = Path(owner['stateDirectory']) / 'claims'
        claim_ids = read(claims_root / 'owners' / (owner['runId'] + '.json'))
        require(isinstance(claim_ids, list), 'Peer claim receipt is unavailable')
        claims = {}
        for claim in claim_ids:
            require(isinstance(claim, str) and re.fullmatch(r'[0-9a-f]{64}', claim), 'Peer claim identity is invalid')
            # Hash the same held bytes that were validated; never reopen the claim by name.
            body = read_bytes(claims_root / (claim + '.json'))
            record = json.loads(body.decode('utf-8'))
            require(isinstance(record, dict) and record.get('OwnerRunId') == owner['runId'], 'Peer claim ownership changed')
            claims[claim] = hashlib.sha256(body).hexdigest()
        return {'containers': sorted(self.containers(item)), 'workspace': str(workspace),
                'appHostProcessId': pid, 'appHostBirth': birth(pid), 'claimHashes': claims}

    def record_images(self, item):
        records = []
        for identity in self.containers(item):
            label = subprocess.check_output(['docker', 'inspect', '--format', '{{index .Config.Labels "' + LABEL + '"}}', identity], text=True, timeout=20).strip()
            require(label == self.issued(item)['runId'], 'Refusing foreign container observation')
            template = '{{json .Id}}\t{{json .Config.Image}}\t{{json .Image}}'
            fields = subprocess.check_output(['docker', 'inspect', '--format', template, identity], text=True, timeout=20).strip().split('\t')
            records.append(dict(zip(('containerId', 'imageReference', 'imageContentId'), map(json.loads, fields))))
        require(records, 'Real owned container identities are required')
        self.observe('actual-images', ownerRunId=self.issued(item)['runId'], images=records)

    def poll(self, item, predicate, seconds):
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            if self.receipt_present(item):
                self.issued(item)
                try:
                    if predicate():
                        return
                except FileNotFoundError:
                    pass
            child = self.children.get(item['label'])
            if child is not None and child.poll() is not None:
                self.capture_result(item)
                raise ValueError('Owner terminated before the required real checkpoint')
            require(not CANCELLED, 'Actual external cancellation interrupted the observation')
            time.sleep(0.1)
        raise TimeoutError('Required real lifecycle checkpoint was not observed within its finite bound')

    def recover_launch(self, item):
        require(item.get('label') in OWNER_LABELS + ATTACHED_LABELS, 'Unknown launch recovery role')
        for key, filename in [('receipt', 'receipt.json'), ('publicResult', 'public-result.json')]:
            require(Path(item[key]) == self.temporary / item['label'] / filename, 'Launch recovery escaped its exact receipt/output role')
        expected = self.temporary / item['label'] / 'launch.json'
        require(Path(item['launchRecord']) == expected, 'Launch record escaped its exact controller role')
        private_directory(expected.parent)
        deadline = time.monotonic() + 5
        record = optional_read(expected)
        while record is None and time.monotonic() < deadline:
            time.sleep(0.05)
            record = optional_read(expected)
        if record is None:
            raise QualificationRefusal('HXLQ107')
        require(isinstance(record, dict), 'Launch process record is malformed')
        require(all(record.get(key) == item[key] for key in ('label', 'receipt', 'publicResult')),
                'Launch process record differs from its durable role/receipt intent')
        require(isinstance(record.get('pid'), int) and record['pid'] > 1
                and record.get('processGroup') == record['pid']
                and isinstance(record.get('birth'), str) and record['birth'].isdigit()
                and record.get('gateStatus') in ('waiting', 'closed', 'released'), 'Actual launch process identity is unavailable')
        if item.get('kind') == 'attached-suite':
            require(all(record[key] == item[key] for key in ('pid', 'birth', 'processGroup') if key in item),
                    'Attached launch identity changed after journal publication')
        if birth(record['pid']) == record['birth']:
            require(os.getpgid(record['pid']) == record['processGroup'], 'Launch process group changed')
        item.update({key: record[key] for key in ('pid', 'birth', 'processGroup')})
        item['launchGateStatus'] = record['gateStatus']
        return record

    def launch(self, item):
        public = Path(item['publicResult']).absolute()
        launch_record = Path(item['launchRecord']).absolute()
        require(launch_record.parent == public.parent and launch_record.name != public.name,
                'The launch process record must share the public result parent')
        parent = held_directory(public.parent)
        try:
            # Any entry at the record name, including a dangling link, is a previous record; never test by path.
            try:
                os.lstat(launch_record.name, dir_fd=parent)
            except FileNotFoundError:
                pass
            else:
                raise ValueError('Do not replace a previous launch process record')
            descriptor = os.open(public.name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC,
                                 0o600, dir_fd=parent)
        finally:
            os.close(parent)
        child = None
        try:
            with os.fdopen(descriptor, 'w') as stream:
                child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), '--launch-owner', item['label']],
                                         cwd=ROOT, stdin=subprocess.PIPE, stdout=stream, stderr=subprocess.DEVNULL,
                                         start_new_session=True, bufsize=0)
            self.children[item['label']] = child
            record = self.recover_launch(item)
            require(child.pid == record['pid'] and birth(child.pid) == record['birth'] and record['gateStatus'] == 'waiting',
                    'Launch gate is not waiting in its actual created child')
            self.save()
            require(child.stdin.write(b'1') == 1, 'Launch gate was not released')
        finally:
            if child is not None and child.stdin is not None:
                child.stdin.close()

    def finish_unissued_launch(self, item):
        record = self.recover_launch(item)
        if birth(record['pid']) == record['birth']:
            self.process_signal(item, signal.SIGINT)
            self.settled_child(item)
        if self.receipt_present(item):
            return False
        record = self.recover_launch(item)
        require(birth(record['pid']) != record['birth'], 'Unissued launch child is still able to execute')
        require(record['gateStatus'] in ('waiting', 'closed'), 'Released launch lacks a recoverable receipt; allocation is uncertain')
        self.observe('not-allocated-before-launch-gate', label=item['label'], actualProcessId=record['pid'], gateStatus=record['gateStatus'])
        return True

    def begin(self, label, cancellation_phase=None, eventstore_port=None, wait_workspace=True):
        require(label not in [item['label'] for item in self.journal['owners']], 'Do not replace an existing owner attempt')
        directory = private_directory(self.temporary / label)
        item = {'label': label, 'receipt': str(directory / 'receipt.json'),
                'launchRecord': str(directory / 'launch.json'), 'publicResult': str(directory / 'public-result.json')}
        if cancellation_phase is not None:
            item['cancellationPhase'] = cancellation_phase
        if eventstore_port is not None:
            require(type(eventstore_port) is int and 1 <= eventstore_port <= 65535, 'Invalid explicit EventStore port')
            item['eventStorePort'] = eventstore_port
        self.journal['owners'].append(item)
        self.save()
        self.launch(item)
        if wait_workspace:
            self.poll(item, lambda: self.state(item).get('workspace') is not None, 600)
            self.control(item, 'wait')
        return item

    def start(self, label, startup=False, cancellation_phase=None):
        return self.ready(self.begin(label, cancellation_phase), startup)

    def ready(self, item, startup=False):
        label = item['label']
        owner = self.issued(item)
        controls = Path(owner['stateDirectory']).parent / 'ci-qualification' / owner['runId']
        if startup:
            def starting():
                state = self.state(item)
                require(state['status'] == 'Starting', 'The real startup window was missed; no startup cancellation credit')
                return state.get('appHostProcessId') is not None and bool(self.containers(item))
            self.poll(item, starting, 600)
            self.pause_host(item)
        else:
            def executing():
                marker = controls / 'execution-ready.json'
                value = optional_read(marker)
                if value is None:
                    return False
                require(value.get('runId') == self.issued(item)['runId'] and value.get('realPersistenceAssertionsPassed') is True,
                        'Fixture checkpoint is missing its actual owner or real persistence assertion')
                item['executionCheckpoint'] = {**value, **owned_native_process(value['processId'], self.issued(item)['runId'])}
                self.save()
                write(self.evidence / (label + '-execution-ready.json'), value)
                return True
            self.poll(item, executing, 900)
            self.record_images(item)
        return item

    def cancellation_snapshot(self, item):
        snapshot = {'readStartedAtUtc': now()}
        try:
            owner = self.issued(item)
            # Only current tracked state can classify a signal; the archived initial owner is never a fallback.
            state = read(Path(owner['stateDirectory']) / (owner['runId'] + '.json'))
            require(isinstance(state, dict), 'Current cancellation owner state is malformed')
            require(all(state.get(key) == owner[key] for key in ('runId', 'attemptId', 'environmentId', 'executionContext'))
                    and state['executionContext'] == self.context, 'Current cancellation state lost its real owner/job binding')
            snapshot.update({key: state[key] for key in ('runId', 'attemptId', 'environmentId', 'executionContext', 'status')})
            snapshot['stateUpdatedAtUtc'] = state['updatedAt']
            snapshot['stateTestTerminal'] = first_outcome(state['testRun']) if state.get('testRun') is not None else None
            terminal = self.diagnostics(item) / 'selected-test.json'
            record = optional_read(terminal)
            snapshot['firstOutcome'] = first_outcome(record) if record is not None else None
            snapshot['nativeExecution'] = None
            if item.get('executionCheckpoint'):
                snapshot['nativeExecution'] = owned_native_process(item['executionCheckpoint']['processId'], owner['runId'])
            if item.get('attachedExecutionCheckpoint'):
                attached = item['attachedExecutionCheckpoint']
                snapshot['attachedExecution'] = owned_native_process(attached['processId'], item['admission']['attachedRunId'])
            if item['cancellationPhase'] == 'execution':
                admitted = item['admission']
                document = read(self.diagnostics(item) / 'attachments.json')
                require(isinstance(document, dict) and isinstance(document.get('attachments'), list),
                        'Current cancellation admissions are malformed')
                active = next((value for value in document['attachments']
                               if isinstance(value, dict) and value.get('attachedRunId') == admitted['attachedRunId']), None)
                require(active is not None, 'The actual admitted hold is missing at cancellation')
                active_attachment(active, owner['runId'])
                same_expiry(admitted, active)
                snapshot['admission'] = active
        except (OSError, ValueError, KeyError, TypeError) as error:
            snapshot['failureType'] = type(error).__name__
        snapshot['observedAtUtc'] = now()
        item['cancellationSnapshot'] = snapshot
        self.save()
        self.observe('actual-cancellation-state-snapshot', snapshot=snapshot)

    def capture_result(self, item):
        child = self.children.get(item['label'])
        if child is not None and child.poll() is not None:
            path = Path(item['publicResult'])
            body = read_bytes(path)
            if body:
                value = json.loads(body.decode('utf-8'))
                require(isinstance(value, dict), 'Qualification public result must be an object')
                self.observe('public-execution-result', ownerRunId=self.issued(item)['runId'] if self.receipt_present(item) else None,
                             exitCode=child.returncode, result=value)
            return child.returncode
        return None

    def attach(self, item, tag, negative=False, previous=None):
        state = self.state(item)
        command = ['attach', '--owner-run-id=' + state['runId'], '--mode=package',
                   '--composition-identity=' + state['manifestHash'], '--hold-seconds=240', '--output=json']
        if previous:
            command.append('--attachment-request-id=' + previous['originalAttachmentRequestId'])
        environment = os.environ.copy()
        if negative:
            environment['GITHUB_JOB'] = self.context['jobId'] + '-disjoint-negative-probe'
        attachments = self.diagnostics(item) / 'attachments.json'
        before = optional_read(attachments)
        completed, result = self.tool(item, command, tag, environment)
        if negative:
            require(optional_read(attachments) == before, 'Negative probe recorded an attachment')
            require(completed.returncode != 0 and result.get('attachment') is None, 'Disjoint job probe was accepted')
            require(any(value.get('ruleId') == 'HXR033' for value in result.get('diagnostics', [])), 'Missing job-context refusal')
            self.observe('negative-context-only', callerJob=environment['GITHUB_JOB'], actualJob=self.context['jobId'])
            return None
        require(completed.returncode == 0 and result.get('attachment'), 'Real same-job finite admission was refused')
        attachment = result['attachment']
        active_attachment(attachment, state['runId'])
        if previous:
            same_expiry(previous, attachment)
        return attachment

    def archived(self, item):
        owner = self.issued(item)
        destination = private_directory(self.evidence / item['label'])
        HELPER.retain_artifacts(owner, destination)
        result = read(self.diagnostics(item) / 'selected-test.json')
        require(result.get('runId') == owner['runId'] and result.get('sourceMappingHash') == self.journal['mappingHash'],
                'Terminal outcome belongs to another owner or prepared mapping')
        return result

    def cleanup_outcome(self, item, checkpoint, required=False):
        """Keep actual early outcomes; require original mapping proof at the durable boundary."""
        require(checkpoint in ('withdrawal-refused', 'withdrawal-recorded', 'retry-completed',
                               'partial-cleaned', 'partial-retried'), 'Unknown cleanup outcome checkpoint')
        owner = self.issued(item)
        directory = self.diagnostics(item)
        record, mapping = None, None
        try:
            held = HELPER.open_directory(Path(directory.anchor), directory, create=False)
        except FileNotFoundError:
            held = None
        if held is not None:
            try:
                try:
                    record = HELPER.read_json(HELPER.read_private_artifact(held, 'selected-test.json', HELPER.FIXED_METADATA_BYTES).decode())
                except FileNotFoundError:
                    pass
                if required and record is not None:
                    mapping = HELPER.read_json(HELPER.read_private_artifact(held, 'selected-source-mapping.json', HELPER.FIXED_METADATA_BYTES).decode())
            finally:
                os.close(held)
        first = observed_first_outcome(record) if record is not None else None
        require(not required or first is not None, 'Cleanup has no actual durable first outcome')
        if first is not None:
            require(first['runId'] == first['ownerId'] == owner['runId'], 'Cleanup outcome belongs to another owner')
            require(first['sourceMappingHash'] in (None, self.journal['mappingHash']), 'Cleanup outcome changed prepared mapping')
        if required:
            require(self.prepared_mapping is not None and mapping == self.prepared_mapping
                    and mapping.get('Root') == str(ROOT) and mapping.get('Mode') == 'package'
                    and mapping.get('ContentHash') == self.journal['mappingHash'],
                    'Cleanup lacks the actual original owner mapping')
        self.observe('cleanup-first-outcome', ownerRunId=owner['runId'], checkpoint=checkpoint,
                     present=first is not None, originalMappingVerified=required, firstOutcome=first)
        return first

    def withdrawn(self, item, admitted, reason=None):
        document = read(self.diagnostics(item) / 'attachments.json')
        require(isinstance(document, dict) and isinstance(document.get('attachments'), list), 'Withdrawal document is absent or malformed')
        observed = next((value for value in document['attachments']
                         if isinstance(value, dict) and value.get('attachedRunId') == admitted['attachedRunId']), None)
        require(observed is not None, 'The admitted attachment is missing from the durable withdrawal document')
        same_expiry(admitted, observed)
        require(observed.get('outcome') == 'withdrawn' and observed.get('endedAtUtc') is not None,
                'CI cleanup did not durably withdraw the active hold')
        require(observed.get('endReason') in ('ci-job-completed', 'ci-job-cancelled')
                and (reason is None or observed['endReason'] == reason), 'Hold ended by expiry or an unexpected reason')
        require(datetime.fromisoformat(observed['endedAtUtc'].replace('Z', '+00:00'))
                < datetime.fromisoformat(observed['expiresAtUtc'].replace('Z', '+00:00')), 'Withdrawal did not precede original expiry')
        self.observe('actual-withdrawal', ownerRunId=self.issued(item)['runId'], attachment=observed)
        return observed

    def down(self, item, tag, expected=True, job_status=''):
        owner = self.issued(item)
        environment = os.environ.copy()
        if job_status:
            environment['HEXALITH_CI_JOB_STATUS'] = job_status
        completed, result = self.tool(item, ['down', '--run-id=' + owner['runId'], '--output=json'], tag, environment)
        verified = HELPER.cleanup_verified(completed, result, owner)
        require(verified == expected, 'Exact-owner cleanup result did not match the explicit qualification condition')
        if not expected:
            # An injected lock refusal counts only as that lock's own runtime refusal.
            outcome = result.get('outcome') if isinstance(result, dict) else None
            require(tag in LOCK_REFUSALS and completed.returncode != 0 and isinstance(outcome, dict)
                    and outcome.get('ruleId') == LOCK_REFUSALS[tag],
                    'Expected refusal was not the injected lock\'s own runtime refusal')
        if verified:
            require(not self.containers(item), 'Independent exact-label discovery still finds owned containers')
        return result

    def settled_child(self, item):
        deadline = time.monotonic() + 90
        while time.monotonic() < deadline:
            child = self.children.get(item['label'])
            if child is not None and child.poll() is not None:
                return
            if birth(item['pid']) != item['birth']:
                return
            time.sleep(0.1)
        raise TimeoutError('The exact controller-owned invocation remains after resource cleanup')

    def process_signal(self, item, number):
        require(birth(item['pid']) == item['birth'], 'Refusing a missing or reused controller process')
        require(os.getpgid(item['pid']) == item['processGroup'] == item['pid'], 'Refusing a foreign process group')
        owner = self.issued(item) if self.receipt_present(item) else None
        # Owners target their muxer/tool. An attached monitor alone forwards to its muxer/tool once.
        # The AppHost, DCP and native test host are left to the owner tool or the exact down.
        processes = {item['pid']: item['birth']} if item.get('kind') == 'attached-suite' else tool_processes(item['pid'], item['birth'])
        delivered = signal_tool_processes(processes, number)
        self.observe('owned-process-signal', ownerRunId=owner['runId'] if owner else None, pid=item['pid'], signal=number,
                     toolProcessIds=delivered)

    def pause_host(self, item):
        state = self.state(item)
        pid = state['appHostProcessId']
        require(state['status'] == 'Starting' and birth(pid) is not None, 'No actual starting AppHost to pause')
        require(('HEXALITH_G4_RUN_ID=' + state['runId']).encode() in Path(f'/proc/{pid}/environ').read_bytes().split(b'\0'),
                'Refusing an AppHost without the exact owned environment tag')
        item['pausedHost'] = {'pid': pid, 'birth': birth(pid)}
        self.save()
        os.kill(pid, signal.SIGSTOP)
        self.observe('startup-cancellation-ready', ownerRunId=state['runId'], appHostProcessId=pid,
                     actualOwnedContainers=self.containers(item), startupDeadlineUtc=state['startupDeadlineUtc'])

    def resume_host(self, item):
        paused = item.get('pausedHost')
        if paused and birth(paused['pid']) == paused['birth']:
            self.issued(item)
            os.kill(paused['pid'], signal.SIGCONT)
            self.observe('owned-host-resumed', ownerRunId=self.issued(item)['runId'], pid=paused['pid'])

    def attached_admission(self, entry, required=True):
        """Recover one accepted execution from the original owner ledger; attachments have no owner receipt."""
        item = self.owner(entry['ownerLabel'])
        owner = self.issued(item)
        require(entry['ownerRunId'] == owner['runId'] and entry['environmentId'] == owner['environmentId'],
                'Attached intent differs from its original owner')
        require(entry['executionContext'] == owner['executionContext'] == self.context
                and entry['source'] == self.journal['source'] and entry['profile'] == ATTACHED_PROFILE,
                'Attached intent lost its exact job/source/profile')
        document = optional_read(self.diagnostics(item) / 'attachments.json')
        if document is None:
            require(not required, 'Attached owner ledger is unavailable')
            return None
        require(document.get('ownerRunId') == owner['runId'] and isinstance(document.get('attachments'), list),
                'Attached owner ledger differs')
        matches = []
        for hold in document['attachments']:
            require(isinstance(hold, dict), 'Malformed attached ledger entry')
            if hold.get('attachedRunId') in entry['beforeAttachmentIds']:
                continue
            execution = hold.get('execution')
            if not isinstance(execution, dict) or execution.get('profile') != entry['profile']:
                continue
            require(hold.get('ownerRunId') == entry['ownerRunId'] and hold.get('environmentId') == entry['environmentId']
                    and execution.get('mode') == 'package' and execution.get('manifestHash', '').lower() == entry['manifestHash']
                    and execution.get('entryHash', '').lower() == entry['entryHash']
                    and utc(execution['requestedAtUtc']) >= utc(entry['requestedAtUtc'])
                    and utc(hold['acceptedAtUtc']) >= utc(entry['requestedAtUtc'])
                    and hold.get('requestedHoldSeconds') == hold.get('effectiveHoldSeconds') == entry['holdSeconds'],
                    'Attached admission differs from the journaled launch')
            require(all(isinstance(hold.get(key), str) and RUN_ID.fullmatch(hold[key]) for key in
                        ('attachedRunId', 'originalAttachmentRequestId', 'ownerRunId', 'environmentId')),
                    'Attached admission lacks issued identities')
            require(utc(hold['expiresAtUtc']) - utc(hold['acceptedAtUtc']) == timedelta(seconds=entry['holdSeconds']),
                    'Attached admission changed its original hold')
            matches.append(hold)
        require(len(matches) <= 1, 'Ambiguous attached launch admissions')
        if not matches:
            require(not required, 'Released attached launch has no exact admission proof')
            return None
        admitted = matches[0]
        if entry.get('admission'):
            same_expiry(entry['admission'], admitted)
        else:
            entry['admission'] = admitted
            self.save()
        return admitted

    def begin_attached(self, item, label, hold_seconds=180):
        owner = self.issued(item)
        require(label in ATTACHED_LABELS and not any(value['label'] == label for value in self.journal.get('attachedRuns', [])),
                'Unknown or duplicate attached launch role')
        document = optional_read(self.diagnostics(item) / 'attachments.json')
        before = document['attachments'] if document is not None else []
        directory = private_directory(self.temporary / label)
        project = ROOT / ATTACHED_ENTRY
        entry = {'label': label, 'kind': 'attached-suite', 'ownerLabel': item['label'], 'ownerRunId': owner['runId'],
                 'environmentId': owner['environmentId'], 'executionContext': self.context, 'source': self.journal['source'],
                 'profile': ATTACHED_PROFILE, 'ownerWorkspace': self.state(item)['workspace'],
                 'holdSeconds': hold_seconds, 'requestedAtUtc': now(),
                 'manifestHash': hashlib.sha256(read_bytes(ROOT / MANIFEST)).hexdigest(),
                 'entryHash': hashlib.sha256(read_bytes(project)).hexdigest(),
                 'beforeAttachmentIds': [value['attachedRunId'] for value in before],
                 'receipt': str(directory / 'receipt.json'), 'launchRecord': str(directory / 'launch.json'),
                 'publicResult': str(directory / 'public-result.json')}
        self.journal.setdefault('attachedRuns', []).append(entry)
        self.save()  # No process may be created or released before the durable attached intent.
        self.launch(entry)
        deadline = time.monotonic() + 30
        admitted = None
        while time.monotonic() < deadline:
            admitted = self.attached_admission(entry, required=False)
            if admitted is not None:
                break
            child = self.children[label]
            require(child.poll() is None, 'Attached command ended before an exact admission')
            time.sleep(0.1)
        require(admitted is not None, 'Attached launch was not admitted within its pickup bound')
        self.attached_control(entry, 'wait')
        controls = Path(owner['stateDirectory']).parent / 'ci-qualification' / admitted['attachedRunId']
        deadline = time.monotonic() + min(120, hold_seconds)
        while time.monotonic() < deadline:
            marker = optional_read(controls / 'execution-ready.json')
            if marker is not None:
                require(all(marker.get(key) == admitted[key] for key in
                            ('ownerRunId', 'environmentId', 'originalAttachmentRequestId', 'expiresAtUtc'))
                        and marker.get('runId') == admitted['attachedRunId'] and marker.get('profile') == ATTACHED_PROFILE
                        and marker.get('realServiceReadsPassed') is True and utc(marker['observedAtUtc']) < utc(admitted['expiresAtUtc']),
                        'Attached checkpoint lost its real service/hold binding')
                entry['executionCheckpoint'] = {**marker, **owned_native_process(marker['processId'], admitted['attachedRunId'])}
                self.observe('attached-execution-ready', launch=label, checkpoint=entry['executionCheckpoint'])
                return entry
            require(self.children[label].poll() is None and datetime.now(timezone.utc) < utc(admitted['expiresAtUtc']),
                    'Attached execution missed the original finite hold')
            time.sleep(0.1)
        raise TimeoutError('Attached real-service checkpoint was not observed')

    def attached_control(self, entry, action):
        require(action in ('wait', 'pass', 'fail'), 'Unknown attached control')
        hold = self.attached_admission(entry)
        owner = self.issued(self.owner(entry['ownerLabel']))
        directory = private_directory(Path(owner['stateDirectory']).parent / 'ci-qualification' / hold['attachedRunId'])
        write(directory / 'control.json', {key: hold[key] for key in ('ownerRunId', 'environmentId', 'originalAttachmentRequestId')}
              | {'runId': hold['attachedRunId'], 'action': action})
        self.observe('attached-fixture-control', attachedRunId=hold['attachedRunId'], action=action)

    def attached_result(self, entry, outcome=None, cause=None, before_teardown=False):
        hold = self.attached_admission(entry)
        owner = self.issued(self.owner(entry['ownerLabel']))
        directory = Path(owner['stateDirectory']) / 'diagnostics' / hold['attachedRunId']
        result_bytes = read_bytes(directory / 'test-result.json')
        result = HELPER.read_json(result_bytes.decode('utf-8'))
        run = result['bindings']['run']
        require(result.get('schema') == 'hexalith.test-result.v1' and result.get('policyId') == POLICY
                and run.get('role') == 'attached' and run.get('runId') == run.get('attachedRunId') == hold['attachedRunId']
                and run.get('ownerRunId') == owner['runId'] and run.get('environmentId') == owner['environmentId']
                and result['bindings']['environment']['executionContext'] == self.context,
                'Attached result lost its exact job/owner/environment binding')
        own_holds = [value for value in result['bindings']['attachments']['holds'] if value['attachedRunId'] == hold['attachedRunId']]
        require(len(own_holds) == 1 and own_holds[0]['originalAttachmentRequestId'] == hold['originalAttachmentRequestId']
                and utc(own_holds[0]['expiresAt']) == utc(hold['expiresAtUtc']), 'Attached result changed its original lineage/expiry')
        terminal = result['terminal']
        own_hold = own_holds[0]
        require(terminal['outcome'] == hold.get('outcome') and terminal['cause'] == hold.get('execution', {}).get('cause')
                and utc(terminal['recordedAt']) == utc(hold.get('endedAtUtc'))
                and own_hold.get('end', {}).get('state') == 'ended'
                and own_hold['end'].get('reason') == hold.get('endReason')
                and utc(own_hold['acceptedAt']) == utc(hold['acceptedAtUtc'])
                and own_hold['effectiveHoldSeconds'] == hold['effectiveHoldSeconds']
                and own_hold['requestedHoldSeconds'] == hold['requestedHoldSeconds'],
                'Attached terminal or hold snapshot differs from its exact original ledger')
        require((terminal['cause'] == 'hold-expired') == (utc(terminal['recordedAt']) >= utc(hold['expiresAtUtc'])),
                'Attached first outcome contradicts its exclusive original expiry')
        if outcome is not None:
            require(terminal['outcome'] == outcome and terminal['cause'] == cause,
                    'Attached first outcome or cause differs')
        require(result['retention']['disposition'] == 'not-retained', 'CI attached failure applied local retention')
        termination = result['termination']
        require(not termination['leftoverWorkIds'] and not termination['failures']
                and termination['ownedWork'] and termination['environmentAccess']
                and all(value['status'] == 'terminated' for value in termination['ownedWork'] + termination['environmentAccess']),
                'Attached work/access termination failed or remains unverified')
        if entry.get('executionCheckpoint'):
            checkpoint = entry['executionCheckpoint']
            require(birth(checkpoint['processId']) != checkpoint['birth'], 'Attached native work survives its terminal result')
        workspace = Path(entry['ownerWorkspace']) / 'attachments' / hold['attachedRunId']
        require(not os.path.lexists(workspace / 'test-environment.json') and not os.path.lexists(workspace / 'native-endpoint-credentials.json'),
                'Attached private access survives termination')
        if before_teardown:
            require(self.running_owner_inventory(self.owner(entry['ownerLabel'])) == entry['ownerRunningInventory'],
                    'An original owner resource stopped before attached termination was observed')
        first = result['terminal']
        if entry.get('firstTerminal') is not None:
            require(entry['firstTerminal'] == first, 'Attached retry changed the first outcome/time')
        else:
            entry['firstTerminal'] = first
        if first['outcome'] == 'failed' and first['cause'] == 'test-failure':
            require(any(value.get('id') == 'native-report' and value.get('reference', '').endswith('.trx')
                        for value in result.get('diagnostics', [])), 'Attached test failure lacks its inspected native result')
        retained = [('test-result.json', result_bytes)]
        identities, filenames = set(), set()
        diagnostics = result.get('diagnostics', [])
        require(isinstance(diagnostics, list) and len(diagnostics) <= len(HELPER.DIAGNOSTICS), 'Invalid attached diagnostic inventory')
        intentional_report = False
        for diagnostic in diagnostics:
            reference = diagnostic.get('reference', '')
            require(reference not in ('native-endpoint-credentials.json', 'test-environment.json'), 'Private access artifacts cannot be retained')
            identity, reference, maximum, digest = HELPER.declared_diagnostic_reference(diagnostic, identities, filenames)
            identities.add(identity)
            filenames.add(reference)
            data = read_bytes(directory / reference, maximum)
            require(hashlib.sha256(data).hexdigest() == digest, 'Attached diagnostic content identity differs')
            if identity == 'executing-tool':
                manifest = HELPER.read_json(data.decode('utf-8'))
                binaries, tool = manifest.get('binaries'), result['bindings'].get('tool', {})
                require(set(manifest) == {'schema', 'algorithm', 'binaries'} and manifest.get('schema') == 'hexalith.executing-tool-manifest.v1'
                        and manifest.get('algorithm') == 'sha256' and tool.get('state') == 'populated'
                        and tool.get('name') == 'hexalith-module' and tool.get('contentIdentity') == 'sha256:' + digest
                        and isinstance(binaries, list) and len(binaries) == 2
                        and [value.get('name') for value in binaries] == ['Hexalith.Builds.Module.Cli.dll', 'Hexalith.Builds.Tooling.dll']
                        and all(set(value) == {'name', 'contentIdentity'} and re.fullmatch(r'sha256:[a-f0-9]{64}', value.get('contentIdentity', '')) for value in binaries),
                        'Attached executing-tool content differs from its tool binding')
            elif identity != 'native-report':
                require(HELPER.read_json(data.decode('utf-8')).get('runId') == hold['attachedRunId'], 'Attached diagnostic names another run')
            elif entry.get('intentionalFailureExpected'):
                try:
                    document = ElementTree.fromstring(data)
                except ElementTree.ParseError:
                    raise ValueError('Attached intentional native report is malformed') from None
                marker = INTENTIONAL_FAILURE + hold['attachedRunId']
                intentional_report = any(node.get('outcome', '').lower() == 'failed'
                    and node.get('testName', '').endswith('ReadRealServicesThenObserveAttachedControl')
                    and any(marker in (message.text or '') for message in node.iter() if message.tag.rsplit('}', 1)[-1] == 'Message')
                    for node in document.iter() if node.tag.rsplit('}', 1)[-1] == 'UnitTestResult')
            retained.append((reference, data))
        require(not entry.get('intentionalFailureExpected') or intentional_report, 'Attached failure lacks its intentional assertion in the inspected native report')
        destination = private_directory(self.evidence / entry['label'])
        for filename, data in retained:
            write_retained_bytes(destination / filename, data)
        self.observe('attached-terminal-result', launch=entry['label'], firstTerminal=first, terminationVerified=True)
        return result

    def verify_attached_command(self, entry, result):
        terminal = result['terminal']
        outcome, cause = terminal['outcome'], terminal['cause']
        if outcome == 'passed':
            expected = (0, 'Success', None, 'None')
        elif outcome in ('withdrawn', 'cancelled'):
            expected = (130, 'Cancelled', 'HXC130', 'Cancelled')
        elif cause == 'test-failure':
            expected = (4, 'ProductOrTest', 'HXT007', 'ProductOrTest')
        else:
            execution = self.attached_admission(entry)['execution']
            code = execution.get('exitCode')
            categories = {1: ('UsageOrManifest', 'Usage'), 2: ('PrerequisiteUnavailable', 'PrerequisiteUnavailable'),
                          3: ('TopologyOrLifecycle', 'TopologyOrLifecycle'), 4: ('ProductOrTest', 'ProductOrTest'),
                          5: ('PersistedState', 'PersistedState'), 6: ('EvidenceSchemaOrPolicy', 'EvidencePolicy')}
            require(code in categories, 'Attached failure lacks its original CLI exit mapping')
            name, category = categories[code]
            expected = (code, name, execution.get('ruleId') or 'HXT007', category)
        child = self.children.get(entry['label'])
        if child is not None:
            require(child.poll() is not None, 'Attached command has not exited')
            entry['commandExitCode'] = child.returncode
            self.save()
        elif entry.get('commandExitCode') is None:
            identity = {key: entry[key] for key in ('pid', 'birth', 'processGroup') if key in entry}
            record = self.recover_launch(entry)
            require(all(record.get(key) == value for key, value in identity.items()), 'Attached CLI exit record changed its launch identity')
            require(record['gateStatus'] == 'released' and type(record.get('commandExitCode')) is int
                    and utc(record['releasedAtUtc']) <= utc(record.get('commandExitedAtUtc')),
                    'Attached CLI lacks a durable observed exit code')
            entry['commandExitCode'] = record['commandExitCode']
            self.save()
        require(entry.get('commandExitCode') == expected[0], 'Attached CLI exit code differs from its first outcome')
        public = read(Path(entry['publicResult']))
        summary = public.get('outcome', {})
        require(public.get('runId') == self.attached_admission(entry)['attachedRunId']
                and summary.get('exitCode') == expected[1] and summary.get('category') == expected[3]
                and (expected[2] is None or summary.get('ruleId') == expected[2]),
                'Attached CLI public outcome differs from its first result')
        require(isinstance(public.get('diagnostics'), list) and all(value.get('category') == 'None'
                for value in public['diagnostics']), 'Attached CLI reported a cleanup or evidence failure')
        write(self.evidence / entry['label'] / 'public-result.json', public)
        self.observe('attached-command-verified', launch=entry['label'], exitCode=expected[0], cause=cause)

    def finalize_attached(self, entry):
        record = self.recover_launch(entry)
        hold = self.attached_admission(entry, required=False)
        if hold is None:
            if birth(record['pid']) == record['birth']:
                self.process_signal(entry, signal.SIGINT)
                self.settled_child(entry)
            require(record['gateStatus'] in ('waiting', 'closed'), 'Released attached launch lacks exact admission proof')
            return
        self.settled_child(entry)
        result = self.attached_result(entry)
        self.verify_attached_command(entry, result)

    def collision(self):
        item = self.begin('collision-owner')
        peer = self.begin('collision-peer')
        self.ready(item)
        self.ready(peer)
        before, other = self.peer_scope(item), self.peer_scope(peer)
        plan = read(Path(self.state(item)['workspace']) / 'plan.json')
        challenger = self.begin('collision-challenger', eventstore_port=plan['ports']['eventStoreHttp'], wait_workspace=False)
        self.children[challenger['label']].wait(timeout=690)
        require(self.capture_result(challenger) != 0, 'Explicit endpoint collision unexpectedly passed')
        public = read(Path(challenger['publicResult']))
        require(public.get('outcome', {}).get('ruleId') == 'HXR031'
                and any(value.get('ruleId') == 'HXR031' and self.issued(item)['runId'] in value.get('message', '')
                        for value in public.get('diagnostics', [])), 'Collision did not name the live blocking owner')
        require(self.peer_scope(item) == before and self.peer_scope(peer) == other, 'Collision changed an existing owner or peer')
        for existing in (item, peer):
            self.control(existing, 'pass')
            self.children[existing['label']].wait(timeout=150)
            require(self.capture_result(existing) == 0, 'Preserved collision owner did not complete real assertions')

    def startup_timeout(self):
        item = self.start('timeout', startup=True)
        original = self.state(item)
        expiry = utc(original['startupDeadlineUtc'])
        # Keep only the exact Starting AppHost paused; the original runner clock decides failure.
        self.children[item['label']].wait(timeout=max(1, (expiry - datetime.now(timezone.utc)).total_seconds()) + 95)
        require(self.capture_result(item) != 0, 'Startup deadline did not fail')
        owner = self.issued(item)
        result = read(Path(owner['stateDirectory']) / 'diagnostics' / owner['runId'] / 'test-result.json')
        deadline = startup_timeout_observation(result, original)
        self.observe('actual-startup-timeout', terminal=result['terminal'], startup=deadline)
        self.resume_host(item)
        first = result['terminal']
        self.down(item, 'startup-timeout-exact-cleanup')
        require(read(Path(owner['stateDirectory']) / 'diagnostics' / owner['runId'] / 'test-result.json')['terminal'] == first,
                'Startup timeout cleanup changed the first failure')

    def attached_case(self, scenario):
        labels = {'attached-failure': ('attached-owner', 'attached-failure-child'),
                  'hold-expiry': ('expiry-owner', 'expiry-child'),
                  'owner-cancellation': ('owner-cancel', 'owner-cancel-child')}
        owner_label, attached_label = labels[scenario]
        item = self.begin(owner_label)
        peer = self.begin('expiry-peer') if scenario == 'hold-expiry' else None
        self.ready(item)
        if peer:
            self.ready(peer)
        scope = self.peer_scope(peer) if peer else None
        entry = self.begin_attached(item, attached_label, 120 if scenario == 'hold-expiry' else 180)
        cancellation_cause = 'ci-job-cancelled' if self.issued(item)['executionContext']['kind'] == 'ci' else 'owner-cancelled'
        if scenario == 'attached-failure':
            entry['intentionalFailureExpected'] = True
            self.save()
            self.attached_control(entry, 'fail')
        elif scenario == 'owner-cancellation':
            entry['ownerRunningInventory'] = self.running_owner_inventory(item)
            self.save()
            self.process_signal(item, signal.SIGINT)
            deadline = time.monotonic() + 100
            while time.monotonic() < deadline:
                require(self.running_owner_inventory(item) == entry['ownerRunningInventory'], 'Missed attached termination before the first owner resource stopped')
                owner = self.issued(item)
                path = Path(owner['stateDirectory']) / 'diagnostics' / entry['admission']['attachedRunId'] / 'test-result.json'
                if optional_read_bytes(path) is not None:
                    self.attached_result(entry, 'withdrawn', cancellation_cause, before_teardown=True)
                    break
                time.sleep(0.01)
            else:
                raise TimeoutError('Attached termination was not observed before owner teardown')
        self.children[entry['label']].wait(timeout=220)
        expected = ('failed', 'test-failure') if scenario == 'attached-failure' else ('withdrawn', 'hold-expired' if peer else cancellation_cause)
        result = self.attached_result(entry, *expected)
        self.verify_attached_command(entry, result)
        if scenario != 'owner-cancellation':
            require(self.state(item).get('testRun') is None, 'Attached outcome replaced the active owner outcome')
            if peer:
                require(self.peer_scope(peer) == scope, 'Hold expiry changed its unrelated active peer')
                self.invalid_lineage(item, entry['admission'], expected_reason='attachment-expired')
            self.control(item, 'pass')
        self.children[item['label']].wait(timeout=150)
        self.verify_original_owner_command(item, cancelled=scenario == 'owner-cancellation')
        owner_first = first_outcome(self.archived(item))
        require(owner_first['terminalStatus'] == ('cancelled' if scenario == 'owner-cancellation' else 'completed'),
                'Attached terminal result changed the owner outcome')
        self.down(item, 'attached-case-exact-cleanup')
        require(first_outcome(self.archived(item)) == owner_first, 'Attached cleanup retry changed owner first outcome')
        self.attached_result(entry, *expected)
        if peer:
            require(self.peer_scope(peer) == scope, 'Attached exact cleanup changed its unrelated peer')
            self.control(peer, 'pass')
            self.children[peer['label']].wait(timeout=150)
            require(self.capture_result(peer) == 0, 'Expiry peer did not complete real assertions')

    def invalid_lineage(self, item, hold=None, expected_reason='attachment-ended'):
        owner = self.issued(item)
        require(expected_reason in ('attachment-ended', 'attachment-expired'), 'Unknown expected lineage refusal')
        identities = [(hold['originalAttachmentRequestId'], expected_reason)] if hold else []
        # Unknown lineage must never be interpreted as a fresh request.
        identities.append(('f' * 32, 'unknown-attachment-request'))
        for lineage, reason in identities:
            before = read(self.diagnostics(item) / 'attachments.json')
            result, public = self.tool(item, ['test', '--manifest=' + MANIFEST, '--mode=package', '--profile=' + ATTACHED_PROFILE,
                                            '--attach-to=' + owner['runId'], '--attachment-request-id=' + lineage,
                                            '--output=json'], 'invalid-attached-lineage-' + reason)
            require(result.returncode != 0 and public.get('attachment') is None
                    and any(value.get('ruleId') == 'HXR036' and value.get('field') == reason
                            for value in public.get('diagnostics', []))
                    and read(self.diagnostics(item) / 'attachments.json') == before,
                    'Invalid attached lineage created a fixture admission or qualifying result')

    def invalid_handoff(self):
        item = self.start('invalid-owner')
        # Retire an actual hold before presenting it as an attached suite lineage.
        admitted = self.attach(item, 'invalid-handoff-hold')
        completed, _ = self.tool(item, ['attach', '--owner-run-id=' + self.issued(item)['runId'], '--end',
                                       '--attached-run-id=' + admitted['attachedRunId'], '--outcome=passed', '--output=json'],
                                'invalid-handoff-end')
        require(completed.returncode == 0, 'Could not end the actual finite hold')
        self.invalid_lineage(item, admitted)
        self.control(item, 'pass')
        self.children[item['label']].wait(timeout=150)
        require(self.capture_result(item) == 0, 'Invalid handoff changed the real owner suite')

    def ordinary(self, label, action):
        item = self.start(label)
        self.attach(item, label + '-cross-job', negative=True)
        admitted = self.attach(item, label + '-same-job')
        self.attach(item, label + '-retry-hold', previous=admitted)
        self.control(item, action)
        child = self.children[label]
        child.wait(timeout=150)
        outcome = self.capture_result(item)
        require((outcome == 0) == (action == 'pass'), 'Passing/failing suite first outcome is wrong')
        record = self.archived(item)
        first = declared_terminal(record, action)
        self.withdrawn(item, admitted, reason='ci-job-completed')
        self.down(item, label + '-idempotent-down')
        require(first_outcome(self.archived(item)) == first, 'Retry changed the first terminal outcome')
        state = self.state(item)
        before = read(self.diagnostics(item) / 'attachments.json')
        completed, refused = self.tool(item, ['attach', '--owner-run-id=' + state['runId'], '--mode=package',
                                             '--composition-identity=' + state['manifestHash'], '--output=json'], label + '-completed-refusal')
        require(completed.returncode != 0 and refused.get('attachment') is None, 'Completed owner accepted an attachment')
        require(read(self.diagnostics(item) / 'attachments.json') == before, 'Refusal recorded an accepted attachment')

    def retry_with_peer(self):
        self.checkpoint('retry', 'launch-owners')
        item = self.begin('retry')
        peer = self.begin('peer')
        self.checkpoint('retry', 'ready-owners')
        self.ready(item)
        self.ready(peer)
        peer_before = self.peer_scope(peer)
        self.observe('actual-active-peer', ownerRunId=self.issued(peer)['runId'], scope=peer_before)
        admitted = self.attach(item, 'retry-admission')
        directory = self.diagnostics(item)
        # Real OS locks affect only this issued owner's metadata; no production fault flag. Each lock is opened in
        # the held diagnostics directory without following a link, and must be the single-link file the tool locks.
        with os.fdopen(open_lock(directory, 'attachments.lock'), 'r+b', buffering=0) as attachments, \
                os.fdopen(open_lock(directory, 'provisioning.lock'), 'r+b', buffering=0) as provisioning:
            fcntl.flock(attachments, fcntl.LOCK_EX | fcntl.LOCK_NB)
            fcntl.flock(provisioning, fcntl.LOCK_EX | fcntl.LOCK_NB)
            self.checkpoint('retry', 'withdrawal-refusal')
            self.down(item, 'withdrawal-lock-refusal', expected=False)
            self.checkpoint('retry', 'first-outcome')
            first = self.cleanup_outcome(item, 'withdrawal-refused')
            self.checkpoint('retry', 'resources-after-refusal')
            require(self.containers(item), 'Withdrawal refusal stopped resources before withdrawal')
            fcntl.flock(attachments, fcntl.LOCK_UN)
            self.checkpoint('retry', 'provisioning-refusal')
            self.down(item, 'provisioning-lock-refusal', expected=False)
            durable = self.cleanup_outcome(item, 'withdrawal-recorded', required=True)
            require(first is None or durable == first, 'Withdrawal replaced the earlier actual first outcome/time')
            first = durable
            self.checkpoint('retry', 'withdrawal-before-stop')
            self.withdrawn(item, admitted)
            require(self.containers(item), 'No live resources observed after durable withdrawal/before stopping')
            self.observe('withdrawn-before-stop', ownerRunId=self.issued(item)['runId'], liveContainers=self.containers(item))
            self.checkpoint('retry', 'peer-before-retry')
            require(self.peer_scope(peer) == peer_before, 'Cleanup fault changed active peer resources, process, workspace or claims')
            require(self.state(peer).get('testRun') is None, 'Peer execution ended during another owner fault')
        self.checkpoint('retry', 'cleanup-retry')
        self.down(item, 'exact-owner-cleanup-retry')
        HELPER.retain_artifacts(self.issued(item), private_directory(self.evidence / item['label']))
        require(self.cleanup_outcome(item, 'retry-completed', required=True) == first, 'Cleanup retry replaced the first outcome/time')
        self.checkpoint('retry', 'peer-after-retry')
        require(self.peer_scope(peer) == peer_before, 'Cleanup retry changed the active peer')
        self.observe('active-peer-preserved', ownerRunId=self.issued(peer)['runId'], scope=self.peer_scope(peer))
        self.checkpoint('retry', 'peer-completion')
        self.control(peer, 'pass')
        self.children['peer'].wait(timeout=150)
        require(self.capture_result(peer) == 0, 'Preserved peer did not complete its real required test')
        self.down(peer, 'peer-idempotent-cleanup')

    def partial_startup(self):
        item = self.start('partial', startup=True)
        self.checkpoint('partial', 'interrupt')
        self.process_signal(item, signal.SIGKILL)
        self.children['partial'].wait(timeout=10)
        require(self.children['partial'].returncode == -signal.SIGKILL, 'Partial-startup interruption was not observed')
        self.observe('actual-partial-startup-interruption', ownerRunId=self.issued(item)['runId'], exitCode=-signal.SIGKILL)
        self.checkpoint('partial', 'cleanup')
        # Only the tool was killed; its paused AppHost must run again so the exact down can withdraw and stop it.
        self.resume_host(item)
        self.down(item, 'partial-startup-recovery')
        HELPER.retain_artifacts(self.issued(item), private_directory(self.evidence / item['label']))
        first = self.cleanup_outcome(item, 'partial-cleaned', required=True)
        require(first['terminalStatus'] == 'cancelled' and first['terminalOutcome'].get('exitCode') == 'Cancelled',
                'Interrupted partial startup did not retain the actual cancelled outcome')
        self.checkpoint('partial', 'cleanup-retry')
        self.down(item, 'partial-startup-idempotent-retry')
        HELPER.retain_artifacts(self.issued(item), private_directory(self.evidence / item['label']))
        require(self.cleanup_outcome(item, 'partial-retried', required=True) == first, 'Partial-startup retry changed the first outcome/time')

    def finalize_cancellation(self, item, prior, job_status):
        require(job_status == 'cancelled', 'Cancellation qualification requires actual cancelled workflow job status')
        record = self.archived(item)
        classification = cancellation_terminal(record, item, prior)
        first = first_outcome(record)
        admitted = item.get('admission')
        withdrawn = None
        if item['cancellationPhase'] == 'execution':
            require(admitted is not None, 'The actual pre-cancellation admission is absent from the durable journal')
            withdrawn = self.withdrawn(item, admitted, reason='ci-job-cancelled')
        else:
            require(admitted is None, 'A starting owner must not have accepted an execution hold')
        self.down(item, 'post-cancellation-exact-retry', job_status=job_status)
        require(first_outcome(self.archived(item)) == first, 'Exact retry changed cancellation first outcome/time')
        if admitted is not None:
            require(self.withdrawn(item, admitted, reason='ci-job-cancelled') == withdrawn,
                    'Exact retry changed the original finite hold or withdrawal')
        self.observe('actual-cancellation-terminal', ownerRunId=item['runId'], phase=item['cancellationPhase'],
                     firstOutcome=first, originalAdmission=admitted, withdrawal=withdrawn, classification=classification,
                     parentApiReceiptRequired=True, candidateAcceptance=False)
        if item['cancellationPhase'] == 'failure':
            require(classification == 'product-failure-before-observed-signal' and first == item['failureBeforeCancellation'],
                    'Failure-before-cancellation changed its durable failed result')
        else:
            require(classification == 'cancelled', 'Preserved prior product failure supplies no cancellation qualification')

    def cleanup_owned(self, item, owner, job_status):
        resume_failure = None
        first_error = None
        failures = []
        try:
            self.resume_host(item)
        except (OSError, ValueError, KeyError, TypeError) as error:
            first_error = error
            resume_failure = {**failure_metadata(error), 'failureObservedAtUtc': now()}
            failures.append({'checkpoint': 'resume-host', **resume_failure})
        try:
            self.down(item, 'independent-finalizer', job_status=job_status)
        except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
            if first_error is None:
                first_error = error
            failures.append({'checkpoint': 'exact-down', **failure_metadata(error), 'failureObservedAtUtc': now()})
        try:
            HELPER.retain_artifacts(owner, private_directory(self.evidence / item['label']))
        except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
            if first_error is None:
                first_error = error
            failures.append({'checkpoint': 'retain-artifacts', **failure_metadata(error), 'failureObservedAtUtc': now()})
        # Observation failures cannot prevent either exact down or artifact retention above.
        for failure in failures:
            try:
                self.observe('owner-finalizer-failure', label=item['label'], **failure)
            except (OSError, ValueError, KeyError, TypeError) as error:
                print(json.dumps({'kind': 'owner-finalizer-observation-unverified', 'label': item['label'],
                                  'checkpoint': failure['checkpoint'], 'failureType': type(error).__name__}), file=sys.stderr, flush=True)
                if first_error is None:
                    first_error = error
        if resume_failure is not None:
            try:
                self.observe('owned-host-resume-unverified', ownerRunId=owner['runId'], **resume_failure)
            except (OSError, ValueError, KeyError, TypeError) as error:
                print(json.dumps({'kind': 'owner-finalizer-observation-unverified', 'label': item['label'],
                                  'checkpoint': 'resume-host', 'failureType': type(error).__name__}), file=sys.stderr, flush=True)
                if first_error is None:
                    first_error = error
        if first_error is not None:
            raise first_error
        return resume_failure

    def finalize_owner(self, item, job_status, issued):
        launch_error = None
        if not self.receipt_present(item):
            try:
                if self.finish_unissued_launch(item):
                    return False
            except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
                launch_error = error
        if not self.receipt_present(item):
            raise ValueError('Launch or allocation remains unverified') from launch_error
        owner = self.issued(item)
        item['runId'] = owner['runId']
        issued.append(owner['runId'])
        try:
            self.recover_launch(item)
        except (OSError, ValueError, KeyError, TypeError) as error:
            launch_error = error
        prior, prior_error = None, False
        terminal = self.diagnostics(item) / 'selected-test.json'
        if item.get('cancellationPhase'):
            try:
                record = optional_read(terminal)
                if record is not None:
                    prior = first_outcome(record)
                    self.observe('terminal-before-independent-finalization', ownerRunId=owner['runId'], firstOutcome=prior)
            except (OSError, ValueError, KeyError, TypeError):
                prior_error = True
        resume_failure = self.cleanup_owned(item, owner, job_status)
        require(launch_error is None, 'Exact receipt cleanup ran, but launch process recovery remains unverified')
        self.settled_child(item)
        require(not prior_error, 'The pre-cleanup first outcome could not be verified')
        require(resume_failure is None, 'Exact cleanup ran, but owned host resume bookkeeping remains unverified')
        if item.get('cancellationPhase'):
            cancellation_phase(item, owner, self.context)
            self.finalize_cancellation(item, item.get('terminalBeforeSignalForward', prior), job_status)
        return True

    def finalize(self, job_status):
        failures = []
        issued = []
        for item in reversed(self.journal['owners']):
            try:
                self.finalize_owner(item, job_status, issued)
            except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
                failures.append({'label': item['label'], **failure_metadata(error)})
        for entry in self.journal.get('attachedRuns', []):
            try:
                self.finalize_attached(entry)
            except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
                failures.append({'label': entry['label'], **failure_metadata(error)})
        self.observe('finalizer-result', failures=failures, issuedOwners=len(issued),
                     allocationStatus='allocated' if issued else 'unverified' if failures else 'not-allocated',
                     issuedOwnersCleanupVerified=bool(issued) and not failures, candidateAcceptance=False)
        return 1 if failures else 0


def receive_signal(number, _frame):
    CANCELLED.append({'signal': number, 'observedAtUtc': now()})


def run_matrix(qualification, selection='matrix'):
    require(selection in SELECTIONS, 'Unknown qualification selection')
    selected = list(SELECTIONS[selection])
    campaign = {'selection': selection, 'requiredCases': list(MATRIX_CASES), 'selectedCases': selected,
                'completedCases': [], 'selectedRequiredCount': len(selected), 'fullRequiredCount': len(MATRIX_CASES),
                'status': 'incomplete', 'fullMatrixPassed': False, 'candidateAcceptance': False}
    require('campaign' not in qualification.journal, 'Do not replace a prior campaign attempt')
    qualification.journal['campaign'] = campaign
    qualification.observe('campaign-selection', campaign=campaign)
    first_error = None
    try:
        for step, action in [('passing', lambda: qualification.ordinary('passing', 'pass')),
                             ('failing', lambda: qualification.ordinary('failing', 'fail')),
                             ('retry-with-peer', qualification.retry_with_peer),
                             ('partial-startup', qualification.partial_startup),
                             ('collision', qualification.collision), ('startup-timeout', qualification.startup_timeout),
                             ('owner-cancellation', lambda: qualification.attached_case('owner-cancellation')),
                             ('attached-failure', lambda: qualification.attached_case('attached-failure')),
                             ('hold-expiry', lambda: qualification.attached_case('hold-expiry')),
                             ('invalid-handoff', qualification.invalid_handoff)]:
            if step not in selected:
                continue
            qualification.checkpoint('matrix', step)
            action()
            campaign['completedCases'].append(step)
            qualification.observe('campaign-progress', campaign=campaign)
        return 0
    except BaseException as error:
        first_error = error
        qualification.record_failure(error, 'scenario')
        raise
    finally:
        try:
            cleanup = qualification.finalize('')
            require(cleanup == 0, 'Bounded exact-owner finalization is incomplete')
            if first_error is None and campaign['completedCases'] == selected:
                campaign['status'] = 'selected-passed'
                campaign['fullMatrixPassed'] = selection == 'matrix' and selected == list(MATRIX_CASES)
            qualification.observe('campaign-result', campaign=campaign)
        except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
            campaign['status'] = 'incomplete'
            campaign['fullMatrixPassed'] = False
            qualification.record_failure(error, 'finalization')
            if first_error is None:
                raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=(*SELECTIONS, 'start-cancellation', 'wait-cancellation', 'finalize', 'remove-reader', 'verify-runtime-cache'))
    parser.add_argument('--packages')
    parser.add_argument('--aspire-version')
    parser.add_argument('--phase', choices=('startup', 'execution', 'failure'))
    parser.add_argument('--job-status', choices=('', 'success', 'failure', 'cancelled'), default='')
    args = parser.parse_args()
    if args.command == 'verify-runtime-cache':
        require(args.packages is not None, 'Runtime cache location is required')
        print(json.dumps({'kind': 'runtime-cache-presence', 'components': verify_runtime_cache(args.packages, args.aspire_version),
                          'contentQualification': False}))
        return 0
    if args.command == 'remove-reader':
        value = os.environ.get('HEXALITH_LIFECYCLE_REGISTRY_CONFIG')
        remove_reader_directory(value, os.environ['RUNNER_TEMP'] if value else None)
        return 0
    if args.command == 'wait-cancellation':
        # Install the handlers first: constructing the controller verifies the tool, and a signal then must not be lost.
        for number in (signal.SIGINT, signal.SIGTERM):
            signal.signal(number, receive_signal)
    qualification = Qualification(finalizing=args.command == 'finalize')
    if args.command == 'finalize':
        return qualification.finalize(args.job_status)
    if args.command == 'start-cancellation':
        require(args.phase is not None, 'A real cancellation phase is required')
        item = qualification.start('cancel', startup=args.phase == 'startup', cancellation_phase=args.phase)
        item['runId'] = qualification.issued(item)['runId']
        if args.phase == 'execution':
            attached = qualification.begin_attached(item, 'cancel-child', 240)
            item['admission'] = attached['admission']
            item['attachedExecutionCheckpoint'] = attached['executionCheckpoint']
        elif args.phase == 'failure':
            qualification.control(item, 'fail')
            qualification.children[item['label']].wait(timeout=150)
            require(qualification.capture_result(item) != 0, 'Failure-before-cancellation did not actually fail')
            item['failureBeforeCancellation'] = declared_terminal(qualification.archived(item), 'fail')
        item['cancellationReadyAtUtc'] = now()
        qualification.save()
        qualification.observe('actual-cancellation-ready', phase=args.phase, ownerRunId=item['runId'],
                              admission=item.get('admission'), readyAtUtc=item['cancellationReadyAtUtc'])
        return 0
    if args.command == 'wait-cancellation':
        item = qualification.owner('cancel')
        deadline = time.monotonic() + 120
        while time.monotonic() < deadline and not CANCELLED:
            require(item['cancellationPhase'] == 'failure' or birth(item['pid']) == item['birth'],
                    'Owner terminated before actual GitHub cancellation')
            time.sleep(0.2)
        require(CANCELLED, 'No real external cancellation arrived; this is not cancellation evidence')
        item['cancellationSignals'] = list(CANCELLED)
        qualification.cancellation_snapshot(item)
        terminal = qualification.diagnostics(item) / 'selected-test.json'
        record = optional_read(terminal)
        if record is not None:
            item['terminalBeforeSignalForward'] = first_outcome(record)
        qualification.save()
        qualification.observe('external-cancellation-signal', signals=item['cancellationSignals'],
                              terminalBeforeSignalForward=item.get('terminalBeforeSignalForward'))
        qualification.resume_host(item)
        if birth(item['pid']) == item['birth']:
            qualification.process_signal(item, signal.SIGINT)
        else:
            require(item['cancellationPhase'] == 'failure', 'Active owner vanished before forwarding cancellation')
        return 1
    return run_matrix(qualification, selection=args.command)


if __name__ == '__main__':
    try:
        raise SystemExit(launch_owner(sys.argv[2]) if len(sys.argv) == 3 and sys.argv[1] == '--launch-owner' else main())
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        # No private descriptor, credential, arbitrary exception payload or raw child stderr is published.
        operation = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] == 'remove-reader' else None
        print(json.dumps({'kind': 'qualification-refused', **failure_metadata(error, operation)}), file=sys.stderr)
        raise SystemExit(1)
