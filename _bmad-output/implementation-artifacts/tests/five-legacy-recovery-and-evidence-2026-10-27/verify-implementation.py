#!/usr/bin/env python3
"""Verify canonical source text, exact executed DLLs, and the original retry body."""
import argparse
import hashlib
import json
from pathlib import Path
import sys


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(data):
    return data.replace(b'\r\n', b'\n')


def verify(root, evidence, receipt_path, artifacts):
    receipt = json.loads(receipt_path.read_text())
    sources = receipt['source_files']
    require(sources and len({row['path'] for row in sources}) == len(sources), 'source inventory is empty or duplicated')
    for row in sources:
        require(digest(canonical((root / row['path']).read_bytes())) == row['lf_normalized_sha256'], 'source text differs: ' + row['path'])
    if artifacts:
        rows = receipt['executed_artifacts']
        require(rows and len({row['path'] for row in rows}) == len(rows), 'artifact inventory is empty or duplicated')
        for row in rows:
            require(digest((root / row['path']).read_bytes()) == row['sha256'], 'artifact bytes differ: ' + row['path'])
    proof = json.loads((evidence / 'historical-protocol-proof.json').read_text())
    original = (evidence / 'original-retry-workflow.cs.txt').read_bytes()
    require(digest(original) == proof['original_workflow_sha256'], 'retained original workflow differs')
    marker = b'    public override async Task<NaturalLanguageEmbeddingRetryResult> RunAsync('
    original = canonical(original)
    helper = canonical((root / proof['historical_helper']).read_bytes())
    require(original.count(marker) == 1 and helper.count(marker) == 1, 'historical RunAsync body is missing or duplicated')
    body = original[original.index(marker):]
    require(helper[helper.index(marker):] == body, 'historical helper RunAsync differs from retained original')
    require(digest(body) == proof['run_body_sha256'], 'historical RunAsync proof hash differs')
    return {'verified_source_files': len(sources), 'verified_executed_artifacts': len(receipt['executed_artifacts']) if artifacts else 0,
        'historical_run_body_recomputed': True, 'result': 'pass'}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--artifacts', action='store_true')
    parser.add_argument('--root', type=Path)
    parser.add_argument('--receipt', type=Path, default=Path(__file__).parent / 'implementation-verification.json')
    args = parser.parse_args()
    try:
        print(json.dumps(verify(args.root or Path(__file__).resolve().parents[4], Path(__file__).parent, args.receipt, args.artifacts), indent=2))
        return 0
    except (OSError, ValueError, KeyError, TypeError) as error:
        print('Implementation verification failed: ' + str(error), file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
