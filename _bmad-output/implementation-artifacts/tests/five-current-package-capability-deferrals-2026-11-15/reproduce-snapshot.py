#!/usr/bin/env python3
"""Recreate the historical host snapshot from local Git objects and retained overlays."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import tarfile


def run(*arguments, cwd=None, **options):
    subprocess.run(arguments, cwd=cwd, check=True, **options)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True, help='Existing local Parties repository with root-declared repositories')
    parser.add_argument('--results', type=Path, required=True, help='Retained private output outside the disposable TMPDIR')
    parser.add_argument('--prepare', action='store_true', help='Also run the original strict package-host preparation')
    args = parser.parse_args()
    source = args.source.resolve()
    scratch = Path(os.environ['TMPDIR']).resolve()
    results = args.results.resolve()
    if results.is_relative_to(scratch):
        raise ValueError('Retained results must be outside the disposable TMPDIR')
    results.mkdir(parents=True, exist_ok=True)
    receipt = Path(__file__).resolve().parent
    overlays = json.loads((receipt / 'overlay-identities.json').read_text())
    owners = json.loads((receipt / 'snapshot-identities.json').read_text())
    baseline = overlays['baseline']
    snapshot = scratch / 'package-runtime'
    run('git', '-C', str(source), 'cat-file', '-e', baseline + '^{commit}')
    run('git', 'clone', '--local', '--shared', '--no-checkout', str(source), str(snapshot))
    run('git', 'sparse-checkout', 'set', '--no-cone', '/*', '!/_bmad-output/', '!/docs/', '!/references/', cwd=snapshot)
    run('git', 'checkout', '--detach', baseline, cwd=snapshot)
    expected = {row['path']: row['snapshotSha256'] for row in overlays['files']}
    with tarfile.open(receipt / 'runtime-overlays.tar.gz', 'r:gz') as archive:
        if {member.name for member in archive.getmembers()} != set(expected):
            raise ValueError('Historical overlay member inventory differs')
        for member in archive.getmembers():
            relative = PurePosixPath(member.name)
            if not member.isfile() or relative.is_absolute() or '..' in relative.parts:
                raise ValueError('Historical overlay must be a regular relative file')
            content = archive.extractfile(member).read()
            if hashlib.sha256(content).hexdigest() != expected[member.name]:
                raise ValueError('Historical overlay hash differs: ' + member.name)
            target = snapshot / member.name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(content)
    declared = subprocess.check_output(['git', 'config', '--file', str(snapshot / '.gitmodules'),
        '--get-regexp', r'^submodule\..*\.path$'], text=True)
    if {line.split(None, 1)[1] for line in declared.splitlines()} != {'references/' + row['owner'] for row in owners}:
        raise ValueError('Immutable baseline root-declared repository inventory differs')
    for row in owners:
        local = source / 'references' / row['owner']
        target = snapshot / 'references' / row['owner']
        run('git', '-C', str(local), 'cat-file', '-e', row['commit'] + '^{commit}')
        run('git', 'clone', '--local', '--shared', '--no-checkout', str(local), str(target))
        run('git', 'sparse-checkout', 'set', '--no-cone', '/*', '!/_bmad-output/', '!/docs/', '!/references/', '!/node_modules/', cwd=target)
        run('git', 'checkout', '--detach', row['commit'], cwd=target)
    # Each commit comes only from the existing local object stores. No fetch,
    # remote clone, submodule update or nested initialization is performed.
    if args.prepare:
        artifacts = snapshot / 'artifacts/package'
        environment = dict(os.environ, HexalithEventStoreVersion='5.1.0',
            HexalithMemoriesVersion='4.0.0', HexalithFrontComposerVersion='6.0.0',
            MSBUILDDISABLENODEREUSE='1', DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER='1')
        with (results / 'snapshot-preparation.log').open('w') as output:
            run('bash', 'scripts/prepare-aspire-runtime-hosts.sh', '--gateway-profile', 'packaged',
                '--configuration', 'Release', '--artifacts-path', str(artifacts),
                '--include-memory-qualification', 'true', cwd=snapshot, env=environment,
                stdout=output, stderr=subprocess.STDOUT)
        retained = results / 'new-preparation-receipts'
        retained.mkdir()
        for path in (artifacts / 'runtime-profile/Release').glob('*.json'):
            shutil.copy2(path, retained / path.name)
        print('New preparation receipts retained separately; they do not replace unavailable original receipts.')
    print('Historical snapshot setup completed; run-with-temp removes its local clones and outputs on exit.')


if __name__ == '__main__':
    main()
