#!/usr/bin/env python3
"""Bind build-reusing runtime preparation to a successful complete solution build."""
import argparse
import json
from pathlib import Path
import time

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('phase', choices=['begin', 'complete'])
parser.add_argument('--configuration', required=True)
parser.add_argument('--artifacts-path', default='')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
artifacts = Path(args.artifacts_path) if args.artifacts_path else root/'artifacts'
if not artifacts.is_absolute():
    raise ValueError('The runtime artifact root must be absolute')
directory = artifacts/'runtime-profile'/args.configuration
directory.mkdir(parents=True, exist_ok=True)
started = directory/'solution-build-started-ns'
completed = directory/'solution-build-completed.json'
if args.phase == 'begin':
    completed.unlink(missing_ok=True)
    started.write_text(str(time.time_ns())+'\n')
else:
    value = int(started.read_text())
    completed.write_text(json.dumps({'configuration':args.configuration,
                                    'buildStartedNs':value,'completedNs':time.time_ns()})+'\n')
