#!/usr/bin/env python3
"""Validate the exact retained EXT inventories without historical temporary files."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

MANIFEST_SHA256 = 'a9c375e78ba95f6c4be3927bd954d9c92ee5fb2fca726f2ef9227cef0724083d'
SOURCE_INVENTORY_SHA256 = '580f85657ac8fb43cfb201cc58ce81c297118010dec3d0989618373b851a83ff'
RETAINED_PATH = '_bmad-output/implementation-artifacts/tests/ext-parties-1-owner-delivery-2026-10-25/resume-recovery'


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def unique(rows, key, count, label):
    require(len(rows) == count and len({row[key] for row in rows}) == count, label + ' inventory is incomplete or duplicated')


def verify(root):
    retained = root / RETAINED_PATH
    def load(name):
        return json.loads((retained / name).read_text())
    require(sha(retained / 'SHA256SUMS') == MANIFEST_SHA256, 'retained checksum manifest differs from pinned original')
    checksums = {}
    for line in (retained / 'SHA256SUMS').read_text().splitlines():
        digest, name = line.split('  ', 1)
        require(name not in checksums, 'duplicate retained checksum entry: ' + name)
        require(sha(retained / name) == digest, 'retained bytes differ: ' + name)
        checksums[name] = digest
    require(len(checksums) == 71, 'retained checksum inventory is incomplete')

    triage = load('review-triage.json')
    expected = [f'BH-{i:02}' for i in range(1, 11)] + ['EC-01', 'VG-01', 'VG-02']
    unique(triage['rows'], 'id', 13, 'finding')
    require(sorted(row['id'] for row in triage['rows']) == sorted(expected), 'finding IDs differ')
    require(triage['allLayersCompleted'] is True, 'review layers are incomplete')
    require(all(row['verdict'] and row['route'] and row['evidence'] for row in triage['rows']), 'finding disposition is incomplete')
    require(triage['reviewedDiff'] in checksums, 'reviewed diff is not retained')
    sources = load('local-source-and-artifact-hashes.json')['sources']
    unique(sources, 'path', 14, 'reviewed source')
    unique(sources, 'retainedSnapshot', 14, 'source snapshot')
    inventory = json.dumps(sorted(sources, key=lambda row: row['path']), sort_keys=True, separators=(',', ':')).encode()
    require(hashlib.sha256(inventory).hexdigest() == SOURCE_INVENTORY_SHA256, 'reviewed source inventory differs from pinned original')
    for row in sources:
        require(sha(retained / row['retainedSnapshot']) == row['sha256'], 'reviewed source snapshot differs: ' + row['path'])

    history = load('historical-preservation.json')
    require(history['qualification'] is False and history['originalsModified'] is False, 'historical qualification status differs')
    unique(history['files'], 'retained', 17, 'historical receipt')
    for row in history['files']:
        path = root / row['retained']
        require(sha(path) == row['sha256'] and path.stat().st_size == row['bytes'], 'historical receipt differs: ' + row['retained'])

    result = load('resume-result.json')
    require(all(result[key] is False for key in ['accepted', 'available', 'liveQualified', 'completeReplacementPacket']), 'owner delivery status differs')
    require(result['targetIntegrationDate'] is None and result['qualifyingLiveCommand'] is None, 'unqualified owner date or command was supplied')
    installed = {'configurationSha256', 'planSha256', 'actorAuthorityAnchorSha256', 'custodyAnchorSha256', 'sourceAdmissionAnchorSha256',
        'observerAnchorSha256', 'restoreTargetSha256', 'failureInjectionTargetSha256', 'successorProofProfileSha256'}
    require(set(result['installedHashes']) == installed and all(value is None for value in result['installedHashes'].values()), 'installed hash inventory differs')
    require(result['localRun']['exitCode'] == 1 and result['localRun']['qualification'] is False, 'historical Local outcome differs')
    require(load('recovery-local/local-result.json')['exitCode'] == 1, 'recovery Local exit differs')
    assemblies = list(ET.parse(retained / 'historical-local/Hexalith.EventStore.Server.Tests-tests.xml').iter('assembly'))
    require(len(assemblies) == 1, 'historical Server XML inventory differs')
    assembly = assemblies[0]
    require((assembly.get('total'), assembly.get('passed'), assembly.get('failed')) == ('450', '446', '4'), 'historical Server outcome differs')
    missing = (retained / 'historical-live-missing/live-gate.txt').read_text()
    present = (retained / 'historical-live-present/live-gate.txt').read_text()
    require(missing.startswith('Missing live inputs: ') and missing.count('EXT_PARTIES_') == 12, 'missing-input Live refusal differs')
    require(present.startswith('Live qualification remains blocked: ') and 'are not installed' in present, 'uninstalled Live refusal differs')
    return {'review_preservation': {'passed': True, 'finding_ids': expected, 'source_snapshots': len(sources),
        'triage_sha256': sha(retained / 'review-triage.json')},
        'local_live_history_preservation': {'passed': True, 'original_receipts': 17, 'retained_manifest_files': len(checksums),
            'local_exit_code': 1, 'historical_server': {'total': 450, 'passed': 446, 'failed': 4},
            'live_refusals': ['missing twelve inputs', 'production custody and complete lanes not installed'],
            'accepted': False, 'available': False, 'liveQualified': False},
        'executed_ext_qualification': False, 'historical_temporary_files_required': False}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[4])
    args = parser.parse_args()
    try:
        print(json.dumps(verify(args.root), indent=2))
        return 0
    except (OSError, ValueError, KeyError, TypeError, ET.ParseError) as error:
        print('Preservation verification failed: ' + str(error), file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
