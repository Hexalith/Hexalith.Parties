#!/usr/bin/env python3
"""Regenerate producer-owned source hooks with pinned tool 4.34.0 and run one command."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
CONFIG = HERE / 'review1/source-build/configuration.json'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, default=HERE.parents[3])
    parser.add_argument('--workspace', type=Path, required=True)
    parser.add_argument('--cwd', default='.')
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--regenerate', action='store_true')
    mode.add_argument('--replay-retained', action='store_true')
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    root, workspace = args.root.resolve(), args.workspace.resolve()
    config = json.loads(CONFIG.read_text())
    installed = json.loads((root / '.config/dotnet-tools.json').read_text())['tools'][config['package']]['version']
    if installed != config['version']:
        raise ValueError('repository tool pin differs from retained configuration')
    cache = Path(os.environ.get('NUGET_PACKAGES', Path.home() / '.nuget/packages'))
    tool = cache / config['package'] / config['version'] / 'tools/net10.0/any'
    if sha(tool / 'Hexalith.Builds.Tooling.dll') != config['tooling_sha256']:
        raise ValueError('pinned Tooling assembly hash differs')
    workspace.mkdir(parents=True, exist_ok=True)
    if args.regenerate or args.replay_retained:
        # The producer can initialize missing direct references. Require all of them first,
        # so this read/build helper cannot initialize either direct or nested submodules.
        refs = subprocess.check_output(['git', 'config', '--file', str(root / '.gitmodules'), '--get-regexp', r'^submodule\..*\.path$'], text=True)
        if any(not (root / row.split(None, 1)[1] / '.git').exists() for row in refs.splitlines()):
            raise ValueError('all root-declared checkouts must already be initialized')
        helper = workspace / 'RegenerateSourceMapping.cs'
        shutil.copyfile(HERE / config['generator'], helper)
        command = ['dotnet', 'run', '--file', str(helper), '--', str(tool), str(root / config['manifest']), str(workspace)]
        if args.replay_retained:
            original_receipt = HERE / 'implementation-verification.json'
            if sha(original_receipt) != config['original_receipt_sha256']:
                raise ValueError('original mapping receipt changed')
            if json.loads(original_receipt.read_text())['source_mapping_tool']['mapping_json_sha256'] != config['original_mapping_json_sha256']:
                raise ValueError('retained replay mapping is not bound to the original receipt')
            template = HERE / config['historical_input']
            if sha(template) != config['historical_input_sha256'] or json.loads(template.read_text())['ContentHash'] != config['original_mapping_content_hash']:
                raise ValueError('retained producer-selected mapping input changed')
            def relocate(value):
                if isinstance(value, dict):
                    return {key: relocate(item) for key, item in value.items()}
                if isinstance(value, list):
                    return [relocate(item) for item in value]
                return value.replace('${PARTIES_ROOT}', str(root)) if isinstance(value, str) else value
            retained = workspace / 'retained-mapping-input.json'
            retained.write_text(json.dumps(relocate(json.loads(template.read_text())), indent=2))
            command.append(str(retained))
        subprocess.run(command, cwd=root, check=True)
        generated = {str(p.relative_to(workspace)): sha(p) for p in (workspace / 'source-mapping').rglob('*') if p.is_file()}
        (workspace / 'generation-receipt.json').write_text(json.dumps({'command': command, 'selection': 'retained producer input' if args.replay_retained else 'fresh producer resolver', 'tool_version': config['version'],
            'tooling_sha256': config['tooling_sha256'], 'configuration_sha256': sha(CONFIG), 'generator_sha256': sha(helper),
            'generated_sha256': generated}, indent=2) + '\n')
    generation = json.loads((workspace / 'generation-receipt.json').read_text())
    for relative, digest in generation['generated_sha256'].items():
        if sha(workspace / relative) != digest:
            raise ValueError('generated mapping changed: ' + relative)
    env = dict(os.environ, **json.loads((workspace / 'source-environment.json').read_text()))
    command = args.command[1:] if args.command[:1] == ['--'] else args.command
    if not command:
        return 0
    return subprocess.run(command, cwd=root / args.cwd, env=env).returncode


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print('Source command failed: ' + str(error), file=sys.stderr)
        sys.exit(1)
