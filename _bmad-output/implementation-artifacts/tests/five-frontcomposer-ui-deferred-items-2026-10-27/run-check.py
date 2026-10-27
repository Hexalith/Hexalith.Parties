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
import tempfile
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
        entry['configuration_sha256'] = {name:digest(cwd/name) for name in [
            'Directory.Build.props','Directory.Build.targets','Directory.Packages.props',
            'deps.local.props','deps.nuget.props','global.json','.editorconfig','.gitattributes',
            '.config/dotnet-tools.json','module/hexalith.module-manifest.v2.json'] if (cwd/name).is_file()}
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
    parser.add_argument('--host-configuration', choices=['Debug','Release'])
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
    def host_snapshot():
        if not args.host_configuration:
            return None
        directory = ROOT / 'src/Hexalith.Parties.UI/bin' / args.host_configuration / 'net10.0'
        binaries = {str(path.relative_to(ROOT)): digest(path) for path in directory.glob('*')
            if path.suffix in {'.dll','.pdb'} and path.stem in {'Hexalith.Parties.UI','Hexalith.Parties.AdminPortal',
                'Hexalith.Parties.ConsumerPortal','Hexalith.FrontComposer.Shell','Hexalith.FrontComposer.Contracts.UI'}}
        assets = {name:digest(ROOT/'references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/wwwroot'/name)
                  for name in ['js/fc-focus.js','js/fc-keyboard.js','js/fc-download.js','css/fc-shell.css']}
        return {'configuration':args.host_configuration,'binaries':binaries,'selected_source_assets':assets}
    host_before = host_snapshot()
    before = observed_inputs()
    executed_binaries = {}
    if len(command) > 1 and command[0] == 'dotnet' and command[1].endswith('.dll'):
        binary = (ROOT / args.cwd / command[1]).resolve()
        names = {binary.stem, 'Hexalith.FrontComposer.Shell', 'Hexalith.FrontComposer.Contracts.UI',
                 'Hexalith.Parties.UI', 'Hexalith.Parties.AdminPortal', 'Hexalith.Parties.ConsumerPortal'}
        for name in sorted(names):
            for suffix in ['.dll', '.pdb']:
                artifact = binary.parent / (name + suffix)
                if artifact.exists():
                    executed_binaries[str(artifact)] = digest(artifact)
    result = subprocess.run(command, cwd=ROOT / args.cwd, env=env, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, text=True)
    # Receipts outlive reruns: never overwrite a previous execution's raw log.
    with tempfile.NamedTemporaryFile(mode='w', prefix='five-frontcomposer-' + args.id + '-',
                                     suffix='.log', delete=False) as handle:
        handle.write(result.stdout)
        log = Path(handle.name)
    summary = result.stdout[-12000:]
    receipt = {'command': command, 'cwd': str(ROOT / args.cwd),
               'finished_utc': datetime.now(timezone.utc).isoformat(),
               'exit_code': result.returncode, 'raw_log_sha256': digest(log),
               'raw_log_local_path': str(log), 'source_mapping': provenance,
               'node_bin': str(args.node.resolve()) if args.node else None,
               'summary': summary, 'executed_binaries': executed_binaries,
               'browser_host_before': host_before, 'browser_host_after': host_snapshot(), 'observed_before': before, 'observed_after': observed_inputs()}
    if '-result-xml' in command:
        from xml.etree import ElementTree as ET
        xml_path = Path(command[command.index('-result-xml') + 1])
        if xml_path.exists():
            assemblies = ET.parse(xml_path).getroot().findall('assembly')
            receipt['tests'] = [{key: item.get(key) for key in ['total','passed','failed','skipped','not-run','errors']} for item in assemblies]
            receipt['result_xml_sha256'] = digest(xml_path)
            receipt['skipped_tests'] = [{'name': test.get('name'),
                'reason': ''.join(test.find('reason').itertext()).strip() if test.find('reason') is not None else None}
                for test in ET.parse(xml_path).getroot().iter('test') if test.get('result') == 'Skip']
    output = HERE / 'checks' / (args.id + '.json')
    output.parent.mkdir(exist_ok=True)
    if output.exists():
        previous = json.loads(output.read_text())
        history = HERE / 'historical'
        history.mkdir(exist_ok=True)
        (history / (args.id + '-' + previous['raw_log_sha256'][:12] + '.json')).write_bytes(output.read_bytes())
    output.write_text(json.dumps(receipt, indent=2) + '\n')
    print(result.stdout[-3000:])
    return result.returncode

if __name__ == '__main__':
    sys.exit(main())
