#!/usr/bin/env python3
"""Verify the implementation snapshot and, optionally, the executed local DLLs."""
import argparse
import hashlib
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--artifacts', action='store_true', help='Require the exact executed local assemblies.')
args = parser.parse_args()
root = Path(__file__).resolve().parents[4]
receipt = json.loads((Path(__file__).parent / 'implementation-verification.json').read_text())
rows = receipt['source_files'] + (receipt['executed_artifacts'] if args.artifacts else [])
for row in rows:
    path = root / row['path']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == row['sha256'], row['path']
print(json.dumps({'verified_source_files': len(receipt['source_files']),
    'verified_executed_artifacts': len(receipt['executed_artifacts']) if args.artifacts else 0,
    'result': 'pass'}, indent=2))
