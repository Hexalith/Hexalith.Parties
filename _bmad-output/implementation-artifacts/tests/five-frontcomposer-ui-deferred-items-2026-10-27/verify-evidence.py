#!/usr/bin/env python3
"""Verify the sealed archive and final command/matrix receipts without raw working files."""
import hashlib
import json
import re
from pathlib import Path
import zipfile
from xml.etree import ElementTree as ET

HERE = Path(__file__).resolve().parent

def require(condition, message):
    if not condition:
        raise ValueError(message)

manifest = json.loads((HERE/'archive-entry-hashes.json').read_text())
archive = HERE / manifest['archive']
require(hashlib.sha256(archive.read_bytes()).hexdigest() == manifest['sha256'], 'archive checksum mismatch')
with zipfile.ZipFile(archive) as raw:
    hashes = json.loads(raw.read('entry-hashes.json'))
    require(hashes == manifest['entries'], 'entry manifests differ')
    require(len(raw.namelist()) == len(set(raw.namelist())), 'duplicate archive entries')
    require(set(raw.namelist()) == set(hashes) | {'entry-hashes.json'}, 'unmanifested archive entry')
    for name, expected in hashes.items():
        require(hashlib.sha256(raw.read(name)).hexdigest() == expected, name)
    final_path = HERE/'final-verification.json'
    require(final_path.is_file(), 'missing final-verification.json')
    final = json.loads(final_path.read_text())
    require(final['archive']['sha256'] == manifest['sha256'], 'final archive checksum')
    require(final['checks'], 'missing final checks')
    require(len({check['id'] for check in final['checks']}) == len(final['checks']), 'duplicate check IDs')
    require({row['scenario'] for row in final['matrix']} == {'Route navigation', 'Compact navigation', 'Freshness/reconcile', 'Export', 'Erasure confirmation', 'Package prerequisite'} and len(final['matrix']) == 6, 'missing or duplicate matrix rows')
    for check in final['checks']:
        require(check['receipt_sha256'] == hashes[check['receipt']], check['id'])
        receipt = json.loads(raw.read(check['receipt']))
        require(receipt['raw_log_sha256'] in {value for key, value in hashes.items() if key.startswith('logs/')}, check['id'] + ': missing raw log')
        require(receipt['exit_code'] == check['exit_code'], check['id'])
        browser_totals = {outcome: int(count) for count, outcome in re.findall(r'^\s*(\d+) (passed|failed|skipped|did not run)\b', receipt['summary'], re.MULTILINE)}
        for outcome, count in check.get('browser_counts', {}).items():
            require(browser_totals.get(outcome, 0) == count, check['id'] + ': ' + outcome)
        if check.get('required_pass'):
            require(receipt['exit_code'] == 0, check['id'])
            for result in receipt.get('tests', []):
                require(result['failed'] == '0', check['id'])
                require(result.get('errors', '0') == '0', check['id'])
                require(result.get('not-run', '0') == '0', check['id'])
                require(int(result['skipped']) == len(check.get('allowed_skips', [])), check['id'])
            if check.get('allowed_skips'):
                require(sorted(item['name'] for item in receipt['skipped_tests']) == sorted(check['allowed_skips']), 'receipt mismatch')
            if receipt.get('browser_host_before'):
                require(receipt['browser_host_before'] == receipt['browser_host_after'], check['id'])
    for row in final['matrix']:
        require(row['checks'], row['scenario'])
        require(all(any(check['id'] == selected and check.get('required_pass') for check in final['checks'])
                    for selected in row['checks']), row['scenario'])
        for test in row.get('named_tests', []):
            if 'xml_receipt' in test:
                matches = [item for item in ET.fromstring(raw.read(test['xml_receipt'])).iter('test')
                           if item.get('name') == test['name']]
                require(len(matches) == 1 and matches[0].get('result') == 'Pass', test['name'])
            else:
                check = next(item for item in final['checks'] if item['id'] == test['check'])
                receipt = json.loads(raw.read(check['receipt']))
                require(receipt['exit_code'] == 0 and test['name'] in receipt['summary'], test['name'])
print('Verified',len(manifest['entries']),'archived entries and final matrix receipts.')
