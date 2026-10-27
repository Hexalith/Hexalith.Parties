#!/usr/bin/env python3
"""Poison evidence in disposable copies and check ordinary/optimized fail-closed validation."""
import copy
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

HERE = Path(__file__).resolve().parent

def require(value, message):
    if not value:
        raise ValueError(message)

with tempfile.TemporaryDirectory(prefix='five-evidence-controls-') as temporary:
    target = Path(temporary)
    for name in ['verify-evidence.py', 'archive-entry-hashes.json', 'final-verification.json']:
        shutil.copy2(HERE / name, target / name)
    (target / 'raw-evidence.zip').symlink_to(HERE / 'raw-evidence.zip')
    final = json.loads((target / 'final-verification.json').read_text())
    cases = [('valid', final, True)]
    cases.append(('missing-final', None, False))
    poisoned = copy.deepcopy(final); poisoned['matrix'] = []; cases.append(('missing-matrix', poisoned, False))
    poisoned = copy.deepcopy(final); poisoned['checks'] *= 2; cases.append(('duplicate-check', poisoned, False))
    poisoned = copy.deepcopy(final); poisoned['archive']['sha256'] = '0' * 64; cases.append(('archive-mismatch', poisoned, False))
    poisoned = copy.deepcopy(final)
    check = next(item for item in poisoned['checks'] if item.get('browser_counts', {}).get('passed', 0) > 0)
    check['browser_counts']['passed'] = 0
    cases.append(('zero-browser-count', poisoned, False))
    poisoned = copy.deepcopy(final); poisoned['checks'][0]['exit_code'] = 17; cases.append(('exit-mismatch', poisoned, False))
    for optimization in [[], ['-O']]:
        for name, document, expected in cases:
            path = target / 'final-verification.json'
            if document is None:
                path.unlink(missing_ok=True)
            else:
                path.write_text(json.dumps(document))
            result = subprocess.run([sys.executable, *optimization, str(target / 'verify-evidence.py')], capture_output=True, text=True)
            require((result.returncode == 0) == expected, name + ': ' + result.stdout + result.stderr)
    print('14 normal/optimized evidence controls passed (valid archive and six fail-closed mutations).')
