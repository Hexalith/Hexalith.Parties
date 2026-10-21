"""Reproduce the existing registry-floor prerelease grammar gap without a network."""
import json
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[4]
workflow = (root / '.github/workflows/release.yml').read_text()
start = workflow.index('              def parts:')
end = workflow.index("' \"$published_response\")", start)
program = workflow[start:end]
for version in ('3.16.0-a..b', '3.16.0-.'):
    result = subprocess.run(['jq', '-er', '--arg', 'floor', '3.15.1', program],
                            input=json.dumps({'versions': [version]}), capture_output=True, text=True, check=True)
    if result.stdout.strip():
        raise RuntimeError('Expected unchanged malformed-prerelease exclusion')
    print(f'{version}: existing expression exits {result.returncode} and excludes the malformed version')
