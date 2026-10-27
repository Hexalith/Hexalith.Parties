#!/usr/bin/env python3
"""Run one check, validating pinned-tool mapping provenance when requested."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from datetime import datetime, timezone

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def observed_inputs():
    baselines = {'.': '6ba02654ca390b3333183962355bd5c5857b8224',
                 'references/Hexalith.FrontComposer': '8d2b47bd206ffd5c5e77a5ac0461b9cfe52ea4fd',
                 'references/Hexalith.Tenants': '2cf3a9660f71f6877923772cd145aa6856907945'}
    result = {}
    for relative in ['.', 'references/Hexalith.FrontComposer', 'references/Hexalith.Tenants',
                     'references/Hexalith.Builds', 'references/Hexalith.EventStore',
                     'references/Hexalith.Memories', 'references/Hexalith.Platform']:
        cwd = ROOT / relative
        head = subprocess.check_output(['git','rev-parse','HEAD'],cwd=cwd,text=True).strip()
        entry = {'head':head}
        baseline = baselines.get(relative)
        # A drifted checkout can contain already-committed task edits; hash from the approved baseline.
        if baseline and subprocess.run(['git','cat-file','-e',baseline],cwd=cwd,stderr=subprocess.DEVNULL).returncode == 0:
            tracked = subprocess.check_output(['git','diff','--name-only',baseline],cwd=cwd,text=True).splitlines()
            untracked = subprocess.check_output(['git','ls-files','--others','--exclude-standard'],cwd=cwd,text=True).splitlines()
            entry['source_files'] = {name:digest(cwd/name) for name in sorted(set(tracked+untracked))
                if name.startswith(('src/','tests/')) and (cwd/name).is_file() and (cwd/name).suffix in {'.cs','.razor','.js','.ts','.csproj','.props','.targets','.txt'}}
        result[relative] = entry
    return result

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--id', required=True)
    parser.add_argument('--cwd', default='.')
    parser.add_argument('--source', type=Path)
    parser.add_argument('--node', type=Path)
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ['--'] else args.command
    env = dict(os.environ)
    if args.node:
        env['PATH'] = str(args.node.resolve()) + os.pathsep + env['PATH']
    provenance = None
    if args.source:
        workspace = args.source.resolve()
        generation_file = workspace / 'generation-receipt.json'
        generation = json.loads(generation_file.read_text())
        for relative, expected in generation['generated_sha256'].items():
            if digest(workspace / relative) != expected:
                raise ValueError('pinned-tool mapping changed: ' + relative)
        selected = json.loads((workspace / 'source-environment.json').read_text())
        env.update(selected)
        provenance = {'generation_receipt_sha256': digest(generation_file),
                      'tool_version': generation['tool_version'],
                      'tooling_sha256': generation['tooling_sha256'],
                      'selection': generation['selection'], 'environment': selected,
                      'generated_sha256': generation['generated_sha256']}
    before = observed_inputs()
    result = subprocess.run(command, cwd=ROOT / args.cwd, env=env, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, text=True)
    log = Path('/tmp') / ('five-frontcomposer-' + args.id + '.log')
    log.write_text(result.stdout)
    summary = result.stdout[-12000:]
    receipt = {'command': command, 'cwd': str(ROOT / args.cwd),
               'finished_utc': datetime.now(timezone.utc).isoformat(),
               'exit_code': result.returncode, 'raw_log_sha256': digest(log),
               'raw_log_local_path': str(log), 'source_mapping': provenance,
               'node_bin': str(args.node.resolve()) if args.node else None,
               'summary': summary, 'observed_before': before, 'observed_after': observed_inputs()}
    if '-result-xml' in command:
        from xml.etree import ElementTree as ET
        xml_path = Path(command[command.index('-result-xml') + 1])
        if xml_path.exists():
            assemblies = ET.parse(xml_path).getroot().findall('assembly')
            receipt['tests'] = [{key: item.get(key) for key in ['total','passed','failed','skipped','not-run','errors']} for item in assemblies]
            receipt['result_xml_sha256'] = digest(xml_path)
    output = HERE / 'checks' / (args.id + '.json')
    output.parent.mkdir(exist_ok=True)
    output.write_text(json.dumps(receipt, indent=2) + '\n')
    print(result.stdout[-3000:])
    return result.returncode

if __name__ == '__main__':
    sys.exit(main())
