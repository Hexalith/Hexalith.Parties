#!/usr/bin/env python3
"""Retain receipt bytes with path-only substitution and deterministic gzip."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path


def digest(value):
    return hashlib.sha256(value).hexdigest()


def retain(original, parties_root, source_workspace):
    """Apply these two byte substitutions, in order; preserve every other byte."""
    sanitized = original.replace(parties_root.encode(), b'${PARTIES_ROOT}')
    sanitized = sanitized.replace(source_workspace.encode(), b'${SOURCE_WORKSPACE}')
    return sanitized, gzip.compress(sanitized, mtime=0)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--parties-root', required=True)
    parser.add_argument('--source-workspace', required=True)
    args = parser.parse_args()
    original = args.input.read_bytes()
    sanitized, compressed = retain(original, args.parties_root, args.source_workspace)
    args.output.write_bytes(compressed)
    print(json.dumps({'retained': args.output.name, 'sha256': digest(compressed),
        'sanitized_sha256': digest(sanitized), 'original_sha256': digest(original),
        'compressed_bytes': len(compressed), 'sanitized_bytes': len(sanitized)}, indent=2))


if __name__ == '__main__':
    main()
