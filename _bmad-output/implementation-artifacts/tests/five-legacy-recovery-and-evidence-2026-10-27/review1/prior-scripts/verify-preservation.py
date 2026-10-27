#!/usr/bin/env python3
"""Validate retained EXT evidence without reading its former temporary paths."""
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[4]
RETAINED = ROOT / '_bmad-output/implementation-artifacts/tests/ext-parties-1-owner-delivery-2026-10-25/resume-recovery'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def load(name):
    return json.loads((RETAINED / name).read_text())

# The original checksum manifest binds reviewed sources, triage and all receipts.
checksums = {}
for line in (RETAINED / 'SHA256SUMS').read_text().splitlines():
    digest, name = line.split('  ', 1)
    assert sha(RETAINED / name) == digest, name
    checksums[name] = digest

triage = load('review-triage.json')
expected = [f'BH-{i:02}' for i in range(1, 11)] + ['EC-01', 'VG-01', 'VG-02']
assert sorted(row['id'] for row in triage['rows']) == sorted(expected)
assert len(triage['rows']) == len(expected)
assert triage['allLayersCompleted'] is True
assert all(row['verdict'] and row['route'] and row['evidence'] for row in triage['rows'])
assert triage['reviewedDiff'] in checksums
sources = load('local-source-and-artifact-hashes.json')['sources']
for row in sources:
    assert sha(RETAINED / row['retainedSnapshot']) == row['sha256'], row['path']

history = load('historical-preservation.json')
assert history['qualification'] is False and history['originalsModified'] is False
assert len(history['files']) == 17
for row in history['files']:
    path = ROOT / row['retained']
    assert sha(path) == row['sha256'] and path.stat().st_size == row['bytes'], row['retained']

result = load('resume-result.json')
assert all(result[key] is False for key in ['accepted', 'available', 'liveQualified', 'completeReplacementPacket'])
assert result['targetIntegrationDate'] is None and result['qualifyingLiveCommand'] is None
assert all(value is None for value in result['installedHashes'].values())
assert result['localRun']['exitCode'] == 1 and result['localRun']['qualification'] is False
assert load('recovery-local/local-result.json')['exitCode'] == 1
assembly = next(ET.parse(RETAINED / 'historical-local/Hexalith.EventStore.Server.Tests-tests.xml').iter('assembly'))
assert (assembly.get('total'), assembly.get('passed'), assembly.get('failed')) == ('450', '446', '4')
missing = (RETAINED / 'historical-live-missing/live-gate.txt').read_text()
present = (RETAINED / 'historical-live-present/live-gate.txt').read_text()
assert missing.startswith('Missing live inputs: ') and missing.count('EXT_PARTIES_') == 12
assert present.startswith('Live qualification remains blocked: ') and 'are not installed' in present

print(json.dumps({
    'review_preservation': {'passed': True, 'finding_ids': expected, 'source_snapshots': len(sources),
        'triage_sha256': sha(RETAINED / 'review-triage.json')},
    'local_live_history_preservation': {'passed': True, 'original_receipts': 17,
        'retained_manifest_files': len(checksums), 'local_exit_code': 1,
        'historical_server': {'total': 450, 'passed': 446, 'failed': 4},
        'live_refusals': ['missing twelve inputs', 'production custody and complete lanes not installed'],
        'accepted': False, 'available': False, 'liveQualified': False},
    'executed_ext_qualification': False,
    'historical_temporary_files_required': False
}, indent=2))
