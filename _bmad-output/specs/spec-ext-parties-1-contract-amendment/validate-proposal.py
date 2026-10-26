#!/usr/bin/env python3
# /// script
# requires-python = ">=3.9"
# dependencies = ["PyYAML==6.0.3", "markdown-it-py==3.0.0"]
# ///
"""Read-only document checks. Emit JSON; no backend, qualification or Git mutations."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import re
import subprocess
import sys
import yaml
from markdown_it import MarkdownIt

folder = Path(__file__).resolve().parent
root = folder.parents[2]
manifest = json.loads((folder / 'proposal-input-manifest.json').read_text())
checks = []

def check(name, result, detail=None):
    checks.append({'name': name, 'passed': bool(result), 'detail': detail})

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

spec = (folder / 'SPEC.md').read_text()
fm = yaml.safe_load(spec.split('---', 2)[1])
new = manifest['newCompanions']
texts = {name: (folder / name).read_text() for name in ['SPEC.md', *new]}
all_text = '\n'.join(texts.values())
check('proposal remains unaccepted and not implementation-ready',
      fm['status'] == 'proposed-compatibility-contract' and fm['implementation_ready'] is False
      and fm['amendment_text_status'] == 'text-approved')
check('eight unique stable capability IDs',
      re.findall(r'\*\*(CAP-\d+)\*\*', spec) == [f'CAP-{n}' for n in range(1, 9)])
for n in range(1, 9):
    part = spec.split(f'**CAP-{n}**', 1)[1].split('**CAP-', 1)[0]
    check(f'CAP-{n} intent and success', '**intent:**' in part and '**success:**' in part)
check('five kernel fields present', all(f'## {name}' in spec for name in
      ['Why', 'Capabilities', 'Constraints', 'Non-goals', 'Success signal']))
check('diagrams absent from kernel', '```' not in spec)
check('original companions retained', set(manifest['priorCompanions']) <= set(fm['companions']))
check('six proposed companions discoverable', set(new) <= set(fm['companions']))
check('all companions exist', all((folder / p).is_file() for p in fm['companions']))
check('operational metadata excluded from companions',
      not any(p.endswith(('.json', '.py', '.zip')) or '.memlog' in p for p in fm['companions']))
links = []
for name, text in texts.items():
    for target in re.findall(r'\]\(([^)]+)\)', text):
        if ':' not in target and not (folder / target.split('#')[0]).exists():
            links.append({'file': name, 'target': target})
check('local Markdown links resolve', not links, links)

md = MarkdownIt('commonmark').enable('table')
tables = {}
for name in new:
    tokens = md.parse(texts[name])
    rows = []
    inside = False
    for token in tokens:
        if token.type == 'tr_open':
            inside = True
            rows.append([])
        elif token.type == 'tr_close':
            inside = False
        elif token.type == 'inline' and inside:
            rows[-1].append(token.content)
    tables[name] = rows
check('all six companions render tables', all(tables[name] for name in new))

test_rows = [r[0].split(' — ', 1)[0] for r in tables['acceptance-tests.md'] if r[0].startswith('AT-')]
check('20 unique amendment test rows', test_rows == [f'AT-{i:02}' for i in range(1, 21)])
identity_rows = [r[0] for r in tables['acceptance-tests.md'] if re.fullmatch(r'P-\d{2}', r[0])]
check('all 10 complete identity rows', identity_rows == [f'P-{i:02}' for i in range(1, 11)])
decision_rows = [r[0] for r in tables['readiness-decisions.md'] if re.fullmatch(r'R\d+', r[0])]
check('ten remaining owner decisions', decision_rows == [f'R{i}' for i in range(1, 11)])
inventory_rows = [r[0] for r in tables['compatibility-contract.md'] if re.fullmatch(r'C\d{2}', r[0])]
check('all 16 inventory classes', inventory_rows == [f'C{i:02}' for i in range(1, 17)])
plan_ids = [r[0] for r in tables['implementation-plan.md'] if re.fullmatch(r'(ES|PL|PA|AG|CV|BU|MC)-\d+', r[0])]
check('20 changes have owning repository rows', len(plan_ids) == 20 and len(set(plan_ids)) == 20, plan_ids)
check('approved clauses all referenced', all(f'A-{i:02}' in all_text for i in range(1, 7)))
check('required semantic states explicit', all(state in texts['compatibility-contract.md'] for state in
      ['ServiceExpired', 'PendingDestruction', 'DurableCopiesDestroyed', 'HolderResidualOpen', 'VerifiedCompleteWithinApprovedScope']))
check('legacy strict rejection and separate API/proof types explicit', all(term in all_text for term in
      ['IIdentityHistoryCustodyV2', 'IIdentityHistoryEventV2', 'ServiceExpiredHistorySegmentProofV2',
       'ProviderConfirmedDestroyed', 'AssuranceContractMismatch', 'POST api/v2/identity-history/read']))
check('approved original expiry and bounds retained', all(term in all_text for term in
      ['31,536,000', '2,592,000', '2,592,900', '100 ms', '1 second', '5 seconds', '900 seconds',
       '30 seconds', '120 seconds', '10 minutes', '60 minutes', 'party-actor-retention-v1', 'party-actor-history-v1']))
check('historical accepted target/date and qualification gates retained', all(term in all_text for term in
      ['56cb4401c4179d431385ad3d931fefa402e4fc5a', '2026-10-08', 'Level 4', 'Level 5', 'OQ-5.4-1',
       '192.168.1.30', '300/30/600/86400/604800', 'DependencyNotAvailable']))
check('scope excludes invented backend, bindings and date',
      'neither is selected or qualified' in texts['readiness-decisions.md'].casefold()
      and 'No replacement date is set' in texts['readiness-decisions.md']
      and 'actual accountable operators' in texts['readiness-decisions.md'].casefold())
check('approved clause bytes preserved', digest(folder / 'clause-diff.md') == manifest['approvedClauseSha256'])

drift = []
protected = 0
for rel, original_digest in manifest['inputHashes'].items():
    if rel in manifest['authorizedDerivedChanges']:
        continue
    protected += 1
    path = folder / rel
    if not path.is_file() or digest(path) != original_digest:
        drift.append(rel)
check('selected protected source and historical artifact bytes preserved', not drift,
      {'count': protected, 'drift': drift, 'scope': manifest['scope']})
body = (folder / '.memlog.md').read_text().split('---', 2)[2].strip()
prefix = body[:manifest['priorMemlogBodyCharacters']]
reinspected = json.loads((folder / 'proposal-source-drift.json').read_text())
check('concurrent drift explicitly disclosed and reinspection unchanged',
      drift == [reinspected['path']] and digest(folder / reinspected['path']) == reinspected['reinspectedSha256']
      and 'Concurrent source drift' in texts['brownfield.md'], reinspected)
check('prior canonical memlog entries retained in order',
      hashlib.sha256(prefix.encode()).hexdigest() == manifest['priorMemlogBodySha256'],
      'memlog.py updates descriptive updated metadata; canonical entry history is append-only')
check('new memlog records capability IDs and review choices',
      all(f'CAP-{i}:' in body for i in range(4, 9)) and 'User requests' in body)
whitespace = []
for name, text in texts.items():
    for i, line in enumerate(text.splitlines(), 1):
        if line != line.rstrip():
            whitespace.append([name, i])
    raw = (folder / name).read_bytes()
    if raw.replace(b'\r\n', b'').count(b'\n') or not raw.endswith(b'\r\n'):
        whitespace.append([name, 'CRLF/newline policy'])
check('new rendered documents obey EditorConfig whitespace and CRLF', not whitespace, whitespace)
cmd = ['git', '-c', 'core.whitespace=cr-at-eol', 'diff', '--check']
proc = subprocess.run(cmd, cwd=root, capture_output=True, text=True)
check('CRLF-aware tracked Git whitespace check', proc.returncode == 0,
      {'command': cmd, 'exitCode': proc.returncode, 'output': proc.stdout + proc.stderr})

written = ['SPEC.md', '.memlog.md', *new, 'proposal-input-manifest.json', 'validate-proposal.py', 'proposal-validation-initial.json', 'proposal-source-drift.json']
failed = [c['name'] for c in checks if not c['passed']]
status = 'passed' if not failed else ('review-ready-with-concurrent-source-drift' if failed == ['selected protected source and historical artifact bytes preserved'] else 'failed')
result = {
    'checkedAtUtc': datetime.now(timezone.utc).isoformat(),
    'status': status,
    'checkCount': len(checks),
    'failedChecks': [c['name'] for c in checks if not c['passed']],
    'checks': checks,
    'renderedTableRows': {name: len(rows) for name, rows in tables.items()},
    'outputHashes': {name: digest(folder / name) for name in written},
    'scope': manifest['scope'],
    'nonQualifying': {'runtimeTestsRun': False, 'installedOwnerCommandsRun': False,
                      'backendSelected': False, 'replacementAccepted': False, 'implementationReady': False},
    'diagnostics': ['Raw git diff --check exited 2 on repository-required CRLF; its CR-only trailing-whitespace diagnostics are resolved by the read-only core.whitespace=cr-at-eol override. No tracked configuration was changed.'],
    'reproduce': 'uv run _bmad-output/specs/spec-ext-parties-1-contract-amendment/validate-proposal.py'
}
print(json.dumps(result, indent=2))
sys.exit(0 if result['status'] == 'passed' else 1)  # Drift remains a failed byte-preservation gate, never silently waived.
