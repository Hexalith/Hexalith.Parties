#!/usr/bin/env python3
"""Observe one genuine CI owner with bounded redacted metadata; execution and cleanup stay Builds-owned."""
from __future__ import annotations

import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import threading
import time
from collections import deque
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[1]
_spec = importlib.util.spec_from_file_location(
    'startup_integration_owner', ROOT / 'references/Hexalith.Builds/Github/run-module-integration/run_module_integration.py')
HELPER = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(HELPER)
MANIFEST = 'module/hexalith.module-manifest.v2.json'
PROFILE = 'platform-current-package'
ENTRY = 'tests/Hexalith.Parties.Platform.IntegrationTests/Hexalith.Parties.Platform.IntegrationTests.csproj'
LIMIT = 1_048_576
PHASES = frozenset(('source-validated', 'bootstrap-submitting', 'bootstrap-committed', 'tenant-submitting',
                   'tenant-committed', 'member-submitting', 'member-committed', 'tenants-native-observing',
                   'tenants-native-observed', 'memories-sidecar-observing', 'memories-sidecar-observed',
                   'memories-submit-started', 'memories-submit-accepted', 'memories-submit-refused',
                   'memories-active-observing', 'memories-active-observed'))
EXIT_CODES = frozenset(('Success', 'UsageOrManifest', 'PrerequisiteUnavailable', 'TopologyOrLifecycle',
                        'ProductOrTest', 'PersistedState', 'EvidenceSchemaOrPolicy', 'Cancelled'))
PHASE_NAMES = frozenset(('None', 'Usage', 'Manifest', 'Prerequisite', 'Topology', 'Test', 'PersistedState', 'Evidence', 'Cleanup'))
HOSTS = ('eventstore', 'parties', 'tenants', 'tenants-api', 'memories', 'memories-domain-service')
FILES = ('native-scope.json', 'catalog.json', 'selected-closure.json', 'activation.json', 'preparation.json',
         'current-reader-receipt.json', 'current-owner-observation.json') + tuple(
             host + suffix for host in HOSTS for suffix in ('.observation.json', '.ready.json', '.committed.json'))


class ObservationPending(ValueError):
    """An owned producer is still creating or atomically replacing metadata; no bytes were accepted."""


def require(value, message='Unsafe startup observation'):
    if not value:
        raise ValueError(message)


def read_bytes(path):
    """Read only a bounded regular owned file, through held nonlinked parent directories."""
    held = HELPER.open_directory(Path(path.anchor), path.parent, create=False)
    try:
        descriptor = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK, dir_fd=held)
        with os.fdopen(descriptor, 'rb') as stream:
            before = os.fstat(stream.fileno())
            require(stat.S_ISREG(before.st_mode) and before.st_nlink in (0, 1) and before.st_uid == os.geteuid()
                    and before.st_size <= LIMIT)
            if before.st_nlink == 0 or before.st_size == 0:
                raise ObservationPending()
            body = stream.read(LIMIT + 1)
            after = os.fstat(stream.fileno())
            require(stat.S_ISREG(after.st_mode) and after.st_nlink in (0, 1) and after.st_uid == before.st_uid
                    and after.st_mode == before.st_mode and after.st_size <= LIMIT)
            if after.st_nlink == 0 or len(body) != before.st_size or any(getattr(before, key) != getattr(after, key)
                    for key in ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns')):
                raise ObservationPending()
            return body
    finally:
        os.close(held)


def read(path):
    try:
        value = HELPER.read_json(read_bytes(path).decode('utf-8'))
    except json.JSONDecodeError as error:
        # A directly written document can be visible before its complete JSON arrives.
        raise ObservationPending() from error
    require(isinstance(value, dict))
    return value


def helper_phase_documents(evidence):
    """Yield the helper's raw execution and latest exact-down results in the legacy or per-attempt layout."""
    try:
        execution = read(evidence / 'execution.json')
    except FileNotFoundError:
        execution = None
    if execution is not None and execution.get('schema') == 'hexalith.module.ci-integration-execution.v1':
        execution = execution.get('result')
        require(execution is None or isinstance(execution, dict))
    if execution is not None:
        yield 'execution.json', execution
    numbers = sorted(int(match.group(1)) for match in (re.fullmatch(r'cleanup-([1-9][0-9]?)\.json', path.name) for path in evidence.glob('cleanup-*.json')) if match)
    if numbers:
        require(numbers == list(range(1, len(numbers) + 1)) and not (evidence / 'cleanup.json').exists())
        record = read(evidence / f'cleanup-{numbers[-1]}.json')
        require(record.get('schema') == 'hexalith.module.ci-integration-cleanup.v1')
        if isinstance(record.get('result'), dict):
            yield 'cleanup.json', record['result']
        return
    try:
        yield 'cleanup.json', read(evidence / 'cleanup.json')
    except FileNotFoundError:
        return


def exists(path):
    """Observe fixed receipt existence without opening or exporting its contents."""
    try:
        held = HELPER.open_directory(Path(path.anchor), path.parent, create=False)
    except FileNotFoundError:
        return False
    try:
        try:
            info = os.stat(path.name, dir_fd=held, follow_symlinks=False)
        except FileNotFoundError:
            return False
        require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1 and info.st_uid == os.geteuid()
                and info.st_size <= 8_388_608)
        return info.st_size > 0
    finally:
        os.close(held)


def timestamp(value):
    require(isinstance(value, str) and len(value) <= 64)
    parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(parsed.tzinfo is not None and parsed.utcoffset().total_seconds() == 0)
    return parsed.isoformat()


def detail(value):
    require(isinstance(value, str) and re.fullmatch(r'state=(?:Running|Starting|Waiting|Finished|FailedToStart|Stopping|Stopped|NotStarted|Exited|Unknown|unknown|unreported);health=(?:Healthy|Unhealthy|Degraded|Unknown|unknown|unreported)', value))
    return value


def resources(items):
    require(isinstance(items, list) and len(items) <= 64)
    result = []
    for item in items:
        require(isinstance(item, dict) and re.fullmatch(r'[a-z][a-z0-9-]{0,127}', item.get('name', ''))
                and item.get('state') in ('Healthy', 'Unhealthy', 'Unknown', 'Starting', 'Running', 'Waiting', 'Finished'))
        row = {key: item[key] for key in ('name', 'state')}
        if 'kind' in item:
            require(item['kind'] in ('project', 'container', 'executable'))
            row['kind'] = item['kind']
        if item.get('detail') is not None:
            row['detail'] = detail(item['detail'])
        if item.get('observedAt') is not None:
            row['observedAt'] = timestamp(item['observedAt'])
        result.append(row)
    require(len({row['name'] for row in result}) == len(result))
    return result


def normalized_digest(value):
    require(isinstance(value, str) and re.fullmatch(r'[a-fA-F0-9]{64}', value, re.ASCII) is not None)
    return value.lower()


def source_revision(root):
    completed = subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=root, capture_output=True, text=True, check=True, timeout=10)
    revision = completed.stdout.strip()
    require(re.fullmatch(r'[a-f0-9]{40}', revision) and revision == os.environ.get('GITHUB_SHA'), 'Checkout differs from actual workflow source')
    return revision


class Observer:
    def __init__(self, root, receipt, evidence):
        self.context = HELPER.ci_context()
        self.root, self.receipt, self.evidence = HELPER.checked_paths(root, receipt, evidence)
        self.source = source_revision(self.root)
        self.owner = None
        self.samples = deque(maxlen=32)
        self.failure = False
        self.observation_termination = 'observing'
        self.refusal_reason = None
        self.checkpoint = 'source-binding'
        self.helper_outcome = {'returned': False}
        self.last = None

    def snapshot(self):
        self.checkpoint = 'source-binding'
        require(source_revision(self.root) == self.source)
        self.checkpoint = 'receipt-binding'
        try:
            read(self.receipt)
        except FileNotFoundError:
            require(self.owner is None, 'Issued receipt disappeared')
            return {'phase': 'awaiting-receipt'}
        owner = HELPER.receipt_owner(self.receipt, self.context)
        require(self.owner is None or self.owner == owner, 'Issued owner changed')
        self.owner = owner
        self.checkpoint = 'state-binding'
        state_root = Path(owner['stateDirectory'])
        try:
            state = read(state_root / (owner['runId'] + '.json'))
        except FileNotFoundError:
            return {'phase': 'awaiting-state'}
        require(state.get('schema') == 'hexalith.g4-run-state.v1' and all(state.get(key) == owner[key]
                for key in ('runId', 'attemptId', 'environmentId', 'executionContext')))
        self.checkpoint = 'selected-source-binding'
        selected = state.get('selectedTest', {})
        require(isinstance(selected, dict))
        require(selected.get('moduleId') == 'parties' and selected.get('name') == PROFILE
                and selected.get('mode') == 'package' and selected.get('entry') == ENTRY
                and normalized_digest(selected.get('manifestHash')) == normalized_digest(state.get('manifestHash')) == hashlib.sha256(read_bytes(self.root / MANIFEST)).hexdigest()
                and normalized_digest(selected.get('entryHash')) == hashlib.sha256(read_bytes(self.root / ENTRY)).hexdigest())
        workspace = state_root.parent / 'g4-workspaces' / owner['runId']
        require(state.get('workspace') == str(workspace))
        self.checkpoint = 'mapping-binding'
        try:
            mapping = read(workspace / 'source-mapping/mapping.json')
        except FileNotFoundError:
            return {'phase': 'awaiting-mapping'}
        entries = mapping.get('Entries')
        require(isinstance(entries, list) and len(entries) <= 32 and all(isinstance(item, dict) for item in entries))
        active = [item for item in entries if item.get('Identity') == 'Hexalith.Parties']
        require(mapping.get('Root') == str(self.root) and mapping.get('ActiveModule') == 'Hexalith.Parties'
                and len(active) == 1 and active[0].get('Origin') == 'source' and active[0].get('Path') == str(self.root))
        self.checkpoint = 'receipt-availability'
        catalog = workspace / 'platform-components' / (owner['environmentId'] + '-catalog')
        result = {'phase': 'observing', 'files': {name: exists(catalog / name) for name in FILES}}
        for name, schema, field in [('readiness-progress.json', 'hexalith.g4-readiness-progress.v1', 'resources'),
                                    ('startup-failure.json', 'hexalith.g4-startup-failure.v1', 'unreadyResources')]:
            self.checkpoint = 'readiness-shape' if name == 'readiness-progress.json' else 'startup-failure-shape'
            try:
                document = read(workspace / name)
            except FileNotFoundError:
                continue
            require(document.get('schema') == schema and document.get('runId') == owner['runId'])
            safe = {'resources': resources(document.get(field, []))}
            if name == 'startup-failure.json':
                require(re.fullmatch(r'HXR[0-9]{3}', document.get('ruleId', '')))
                safe.update(ruleId=document['ruleId'], status=detail(document.get('status')))
            result[name] = safe
        self.checkpoint = 'preparation-shape'
        try:
            progress = read(catalog / 'preparation-progress.json')
        except FileNotFoundError:
            pass
        else:
            require(progress.get('RunId') == owner['runId'] and progress.get('step') in PHASES
                    and progress.get('complete') is False and (progress.get('httpStatus') is None
                    or type(progress['httpStatus']) is int and 100 <= progress['httpStatus'] <= 599))
            result['preparation'] = {'phase': progress['step'], 'status': progress.get('httpStatus'),
                                     'observedAt': timestamp(progress.get('observedAtUtc'))}
        return result

    def publish(self):
        value = {'schema': 'hexalith.parties.startup-observation.v1', 'qualificationCredit': False,
                 'source': self.source, 'runId': self.owner['runId'] if self.owner else None,
                 'observerRefused': self.failure, 'observationTermination': self.observation_termination,
                 'refusalReason': self.refusal_reason,
                 'helperOutcome': self.helper_outcome, 'observations': list(self.samples)}
        body = (json.dumps(value, sort_keys=True) + '\n').encode()
        require(len(body) <= LIMIT)
        held = HELPER.open_directory(self.root, self.evidence)
        temporary = '.startup-observation.new'
        try:
            require(os.fstat(held).st_mode & 0o077 == 0)
            try:
                info = os.stat('startup-observation.json', dir_fd=held, follow_symlinks=False)
            except FileNotFoundError:
                pass
            else:
                require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1)
            descriptor = os.open(temporary, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600, dir_fd=held)
            with os.fdopen(descriptor, 'wb') as stream:
                stream.write(body)
            os.replace(temporary, 'startup-observation.json', src_dir_fd=held, dst_dir_fd=held)
        finally:
            try:
                os.unlink(temporary, dir_fd=held)
            except FileNotFoundError:
                pass
            os.close(held)

    def observe(self, stop):
        deadline = time.monotonic() + 1200
        pending = False
        try:
            for _ in range(600):
                try:
                    snapshot = self.snapshot()
                    pending = False
                except ObservationPending:
                    pending = True
                    snapshot = {'phase': 'metadata-pending', 'checkpoint': self.checkpoint}
                if snapshot != self.last:
                    self.last = snapshot
                    self.samples.append({'observedAt': datetime.now(timezone.utc).isoformat(), **snapshot})
                    self.publish()
                if stop.wait(2):
                    self.observation_termination = 'normal-stop'
                    self.publish()
                    return
                if time.monotonic() >= deadline:
                    break
            self.observation_termination = 'budget-exhausted'
            if pending:
                self.failure = True
                self.refusal_reason = 'metadata-pending-bound'
            self.publish()
        except (OSError, ValueError, KeyError, TypeError, RecursionError, subprocess.SubprocessError):
            self.failure = True
            self.observation_termination = 'refused'
            self.refusal_reason = self.checkpoint
            # Fixed checkpoints only; no exception payload, private paths, URLs or child output.
            try:
                self.publish()
            except (OSError, ValueError, KeyError, TypeError, RecursionError):
                pass

    def finish(self, outcome):
        """Keep only bounded enums/booleans from this invocation's existing helper results, after its cleanup."""
        self.helper_outcome = {'returned': outcome is not None}
        try:
            if outcome is not None:
                require(type(outcome) is int and -255 <= outcome <= 255)
                self.helper_outcome['exitCode'] = outcome
            if self.owner is not None:
                require(source_revision(self.root) == self.source)
                read(self.receipt)
                require(HELPER.receipt_owner(self.receipt, self.context) == self.owner)
                evidence = self.root / 'TestResults/module-integration'
                for name, document in helper_phase_documents(evidence):
                    require(document.get('runId') == self.owner['runId'])
                    reported = document.get('outcome', {})
                    require(isinstance(reported, dict) and reported.get('exitCode') in EXIT_CODES
                            and reported.get('phase') in PHASE_NAMES)
                    safe = {'exitCode': reported['exitCode'], 'phase': reported['phase']}
                    if name == 'cleanup.json':
                        remaining = document.get('remainingResources')
                        require(isinstance(remaining, dict))
                        safe['reportedEmpty'] = remaining.get('unverified') is False and all(
                            remaining.get(key) == [] for key in ('containerIds', 'processIds', 'workspaceIds', 'claimIds', 'discoveryFailures'))
                    self.helper_outcome[name] = safe
            self.publish()
        except (OSError, ValueError, KeyError, TypeError, RecursionError, subprocess.SubprocessError):
            self.failure = True
            self.observation_termination = 'refused'
            self.refusal_reason = self.refusal_reason or 'helper-outcome'
            try:
                self.publish()
            except (OSError, ValueError, KeyError, TypeError, RecursionError):
                pass


def run():
    root = ROOT
    receipt = HELPER.job_root('RUNNER_TEMP') / 'hexalith-ci-owner.json'
    evidence = root / 'TestResults/startup-observer'
    observer = Observer(root, receipt, evidence)
    stop = threading.Event()
    worker = threading.Thread(target=observer.observe, args=(stop,), daemon=True)
    worker.start()
    outcome = None
    try:
        # The released helper owns all execution, signal forwarding, outcomes and finally cleanup.
        outcome = HELPER.run_integration(root, MANIFEST, [PROFILE], receipt, root / 'TestResults/module-integration')
    finally:
        stop.set()
        worker.join(timeout=15)
        if not worker.is_alive():
            observer.finish(outcome)
    require(not worker.is_alive(), 'Observer did not stop within its bound')
    return outcome or int(observer.failure)


if __name__ == '__main__':
    try:
        raise SystemExit(run())
    except (OSError, ValueError, KeyError, TypeError, RecursionError, subprocess.SubprocessError) as error:
        print('Startup observation refused: ' + type(error).__name__, file=sys.stderr)
        raise SystemExit(1)
