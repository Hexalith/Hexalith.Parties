#!/usr/bin/env python3
"""Explicit hosted lifecycle observations; local controller tests provide no hosted credit."""
from __future__ import annotations

import argparse
import fcntl
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import signal
import stat
import subprocess
import sys
import tempfile
import time
from datetime import datetime, timedelta, timezone

ROOT = Path(__file__).resolve().parents[1]
_spec = importlib.util.spec_from_file_location(
    'integration_owner', ROOT / 'references/Hexalith.Builds/Github/run-module-integration/run_module_integration.py')
HELPER = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(HELPER)
TOOL = ['dotnet', 'tool', 'run', 'hexalith-module', '--']
RUN_ID = re.compile(r'[0-9a-f]{32}')
LABEL = 'hexalith.g4.run'
MANIFEST = 'module/hexalith.module-manifest.v2.json'
PROFILE = 'lifecycle-package'
CANCELLED = []


def now():
    return datetime.now(timezone.utc).isoformat()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read(path):
    path = Path(path)
    require(not path.is_symlink(), 'Refusing linked qualification metadata')
    info = path.stat()
    require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1 and info.st_size <= 8_388_608,
            'Refusing nonregular or oversized qualification metadata')
    return json.loads(path.read_text())


def private_directory(path):
    path = Path(path)
    require(not any(part.is_symlink() for part in (path, *path.parents)), 'Refusing linked qualification path')
    path.mkdir(parents=True, exist_ok=True, mode=0o700)
    return path


def write(path, value):
    path = Path(path)
    private_directory(path.parent)
    require(not path.exists() or (not path.is_symlink() and path.stat().st_nlink == 1), 'Unsafe evidence destination')
    descriptor, temporary = tempfile.mkstemp(prefix=path.name + '.', suffix='.new', dir=path.parent)
    try:
        with os.fdopen(descriptor, 'w') as output:
            json.dump(value, output, indent=2, sort_keys=True)
            output.write('\n')
        os.replace(temporary, path)
    finally:
        Path(temporary).unlink(missing_ok=True)


def birth(pid):
    try:
        return Path(f'/proc/{pid}/stat').read_text().rpartition(')')[2].split()[19]
    except (FileNotFoundError, ProcessLookupError):
        return None


def first_outcome(record):
    require(isinstance(record, dict) and isinstance(record.get('terminalOutcome'), dict),
            'First outcome must have the actual structured result fields')
    required = ('runId', 'ownerId', 'terminalStatus', 'terminalOutcome', 'terminalAtUtc', 'sourceMappingHash')
    require(all(record.get(key) is not None for key in required), 'First-outcome identity, status, time or mapping is absent')
    require(RUN_ID.fullmatch(record['runId']), 'First outcome has an invalid issued run identity')
    utc(record['terminalAtUtc'])
    return {key: record.get(key) for key in (*required, 'terminalCause')}


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
    require(item.get('cancellationPhase') in ('startup', 'execution'), 'Missing actual cancellation phase')
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
        self.journal = read(self.journal_path) if self.journal_path.exists() else {
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
        if mapping is not None and not (self.evidence / 'inputs.json').exists():
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

    def owner(self, label):
        return next(item for item in self.journal['owners'] if item['label'] == label)

    def issued(self, item):
        require(item.get('label') in ('passing', 'failing', 'peer', 'retry', 'partial', 'cancel'), 'Unknown controller owner role')
        expected = self.temporary / item['label'] / 'receipt.json'
        require(Path(item['receipt']) == expected and not expected.is_symlink(), 'Receipt escaped its issued controller role')
        return HELPER.receipt_owner(expected, self.context)

    def state(self, item):
        owner = self.issued(item)
        directory = Path(owner['stateDirectory'])
        paths = [directory / (owner['runId'] + '.json'), directory / 'diagnostics' / owner['runId'] / 'owner.json']
        path = next((path for path in paths if path.exists()), None)
        if path is None:
            raise FileNotFoundError('Owner metadata has not yet been published')
        value = read(path)
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
            path = claims_root / (claim + '.json')
            record = read(path)
            require(record.get('OwnerRunId') == owner['runId'], 'Peer claim ownership changed')
            claims[claim] = hashlib.sha256(path.read_bytes()).hexdigest()
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
            path = Path(item['receipt'])
            if path.exists():
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

    def start(self, label, startup=False, cancellation_phase=None):
        require(label not in [item['label'] for item in self.journal['owners']], 'Do not replace an existing owner attempt')
        directory = private_directory(self.temporary / label)
        receipt = directory / 'receipt.json'
        output = directory / 'public-result.json'
        descriptor = os.open(output, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(descriptor, 'w') as stream:
            child = subprocess.Popen(TOOL + ['test', '--manifest=' + MANIFEST, '--mode=package', '--profile=' + PROFILE,
                                     '--ci-owner-receipt=' + str(receipt), '--output=json'], cwd=ROOT,
                                     stdin=subprocess.DEVNULL, stdout=stream, stderr=subprocess.DEVNULL, start_new_session=True)
        item = {'label': label, 'receipt': str(receipt), 'pid': child.pid, 'birth': birth(child.pid),
                'processGroup': os.getpgid(child.pid), 'publicResult': str(output)}
        require(item['birth'] is not None and item['processGroup'] == item['pid'], 'Cannot establish owned controller child')
        if cancellation_phase is not None:
            item['cancellationPhase'] = cancellation_phase
        self.children[label] = child
        self.journal['owners'].append(item)
        self.save()
        self.poll(item, lambda: self.state(item).get('workspace') is not None, 600)
        controls = self.control(item, 'wait')
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
                if not marker.exists():
                    return False
                try:
                    value = read(marker)
                except json.JSONDecodeError:
                    return False
                require(value.get('runId') == self.issued(item)['runId'] and value.get('realPersistenceAssertionsPassed') is True,
                        'Fixture checkpoint is missing its actual owner or real persistence assertion')
                write(self.evidence / (label + '-execution-ready.json'), value)
                return True
            self.poll(item, executing, 900)
            self.record_images(item)
        return item

    def capture_result(self, item):
        child = self.children.get(item['label'])
        if child is not None and child.poll() is not None:
            path = Path(item['publicResult'])
            if path.stat().st_size:
                value = read(path)
                self.observe('public-execution-result', ownerRunId=self.issued(item)['runId'] if Path(item['receipt']).exists() else None,
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
        before = read(attachments) if attachments.exists() else None
        completed, result = self.tool(item, command, tag, environment)
        if negative:
            require((read(attachments) if attachments.exists() else None) == before, 'Negative probe recorded an attachment')
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
        owner = self.issued(item) if Path(item['receipt']).exists() else None
        os.killpg(item['processGroup'], number)
        self.observe('owned-process-signal', ownerRunId=owner['runId'] if owner else None, pid=item['pid'], signal=number)

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
        peer = self.start('peer')
        peer_before = self.peer_scope(peer)
        self.observe('actual-active-peer', ownerRunId=self.issued(peer)['runId'], scope=peer_before)
        item = self.start('retry')
        admitted = self.attach(item, 'retry-admission')
        directory = self.diagnostics(item)
        # Real OS locks affect only this issued owner's metadata; no production fault flag.
        with (directory / 'attachments.lock').open('a+') as attachments, (directory / 'provisioning.lock').open('a+') as provisioning:
            fcntl.flock(attachments, fcntl.LOCK_EX | fcntl.LOCK_NB)
            fcntl.flock(provisioning, fcntl.LOCK_EX | fcntl.LOCK_NB)
            self.down(item, 'withdrawal-lock-refusal', expected=False)
            first = first_outcome(self.archived(item))
            require(first is not None, 'Cleanup refusal did not preserve an actual first outcome')
            require(self.containers(item), 'Withdrawal refusal stopped resources before withdrawal')
            fcntl.flock(attachments, fcntl.LOCK_UN)
            self.down(item, 'provisioning-lock-refusal', expected=False)
            self.withdrawn(item, admitted)
            require(self.containers(item), 'No live resources observed after durable withdrawal/before stopping')
            self.observe('withdrawn-before-stop', ownerRunId=self.issued(item)['runId'], liveContainers=self.containers(item))
            require(self.peer_scope(peer) == peer_before, 'Cleanup fault changed active peer resources, process, workspace or claims')
            require(self.state(peer).get('testRun') is None, 'Peer execution ended during another owner fault')
        self.down(item, 'exact-owner-cleanup-retry')
        require(first_outcome(self.archived(item)) == first, 'Cleanup retry replaced the first outcome/time')
        require(self.peer_scope(peer) == peer_before, 'Cleanup retry changed the active peer')
        self.observe('active-peer-preserved', ownerRunId=self.issued(peer)['runId'], scope=self.peer_scope(peer))
        self.control(peer, 'pass')
        self.children['peer'].wait(timeout=150)
        require(self.capture_result(peer) == 0, 'Preserved peer did not complete its real required test')
        self.down(peer, 'peer-idempotent-cleanup')

    def partial_startup(self):
        item = self.start('partial', startup=True)
        self.process_signal(item, signal.SIGKILL)
        self.children['partial'].wait(timeout=10)
        require(self.children['partial'].returncode == -signal.SIGKILL, 'Partial-startup interruption was not observed')
        self.observe('actual-partial-startup-interruption', ownerRunId=self.issued(item)['runId'], exitCode=-signal.SIGKILL)
        self.down(item, 'partial-startup-recovery')
        first = first_outcome(self.archived(item))
        require(first['terminalStatus'] == 'cancelled' and first['terminalOutcome'].get('exitCode') == 'Cancelled',
                'Interrupted partial startup did not retain the actual cancelled outcome')
        self.down(item, 'partial-startup-idempotent-retry')
        require(first_outcome(self.archived(item)) == first, 'Partial-startup retry changed the first outcome/time')

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
        require(classification == 'cancelled', 'Preserved prior product failure supplies no cancellation qualification')

    def finalize(self, job_status):
        failures = []
        issued_owners = 0
        for item in reversed(self.journal['owners']):
            try:
                if not Path(item['receipt']).exists() and birth(item['pid']) == item['birth']:
                    self.process_signal(item, signal.SIGINT)
                    deadline = time.monotonic() + 90
                    while birth(item['pid']) == item['birth'] and time.monotonic() < deadline:
                        time.sleep(0.1)
                    require(birth(item['pid']) != item['birth'], 'Unissued owned child has not stopped')
                if not Path(item['receipt']).exists():
                    self.observe('not-allocated', label=item['label'])
                    continue
                owner = self.issued(item)
                item['runId'] = owner['runId']
                issued_owners += 1
                prior = None
                prior_error = False
                terminal = self.diagnostics(item) / 'selected-test.json'
                if item.get('cancellationPhase') and terminal.exists():
                    try:
                        prior = first_outcome(read(terminal))
                        self.observe('terminal-before-independent-finalization', ownerRunId=owner['runId'], firstOutcome=prior)
                    except (OSError, ValueError, KeyError, TypeError):
                        prior_error = True
                self.resume_host(item)
                self.down(item, 'independent-finalizer', job_status=job_status)
                HELPER.retain_artifacts(owner, private_directory(self.evidence / item['label']))
                self.settled_child(item)
                require(not prior_error, 'The pre-cleanup first outcome could not be verified')
                if item.get('cancellationPhase'):
                    self.finalize_cancellation(item, item.get('terminalBeforeSignalForward', prior), job_status)
            except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
                failures.append({'label': item['label'], 'failureType': type(error).__name__})
        self.observe('finalizer-result', failures=failures, issuedOwners=issued_owners,
                     allocationStatus='allocated' if issued_owners else 'not-allocated',
                     issuedOwnersCleanupVerified=issued_owners > 0 and not failures, candidateAcceptance=False)
        return 1 if failures else 0


def receive_signal(number, _frame):
    CANCELLED.append({'signal': number, 'observedAtUtc': now()})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('matrix', 'start-cancellation', 'wait-cancellation', 'finalize'))
    parser.add_argument('--phase', choices=('startup', 'execution'))
    parser.add_argument('--job-status', choices=('', 'success', 'failure', 'cancelled'), default='')
    args = parser.parse_args()
    qualification = Qualification(finalizing=args.command == 'finalize')
    if args.command == 'finalize':
        return qualification.finalize(args.job_status)
    if args.command == 'start-cancellation':
        require(args.phase is not None, 'A real cancellation phase is required')
        item = qualification.start('cancel', startup=args.phase == 'startup', cancellation_phase=args.phase)
        item['runId'] = qualification.issued(item)['runId']
        if args.phase == 'execution':
            item['admission'] = qualification.attach(item, 'cancellation-active-hold')
        item['cancellationReadyAtUtc'] = now()
        qualification.save()
        qualification.observe('actual-cancellation-ready', phase=args.phase, ownerRunId=item['runId'],
                              admission=item.get('admission'), readyAtUtc=item['cancellationReadyAtUtc'])
        return 0
    if args.command == 'wait-cancellation':
        item = qualification.owner('cancel')
        for number in (signal.SIGINT, signal.SIGTERM):
            signal.signal(number, receive_signal)
        deadline = time.monotonic() + 120
        while time.monotonic() < deadline and not CANCELLED:
            require(birth(item['pid']) == item['birth'], 'Owner terminated before actual GitHub cancellation')
            time.sleep(0.2)
        require(CANCELLED, 'No real external cancellation arrived; this is not cancellation evidence')
        item['cancellationSignals'] = list(CANCELLED)
        terminal = qualification.diagnostics(item) / 'selected-test.json'
        if terminal.exists():
            item['terminalBeforeSignalForward'] = first_outcome(read(terminal))
        qualification.save()
        qualification.observe('external-cancellation-signal', signals=item['cancellationSignals'],
                              terminalBeforeSignalForward=item.get('terminalBeforeSignalForward'))
        qualification.resume_host(item)
        qualification.process_signal(item, signal.SIGINT)
        return 1
    try:
        qualification.ordinary('passing', 'pass')
        qualification.ordinary('failing', 'fail')
        qualification.retry_with_peer()
        qualification.partial_startup()
        return 0
    finally:
        cleanup = qualification.finalize('')
        require(cleanup == 0, 'Bounded exact-owner finalization is incomplete')


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        # No private descriptor, credential, arbitrary exception payload or raw child stderr is published.
        print('Lifecycle qualification refused: ' + type(error).__name__, file=sys.stderr)
        raise SystemExit(1)
