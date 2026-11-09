"""Replay the historical receipt checks against their exact recorded Git blobs."""

import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile


def main():
    packet = Path(__file__).resolve().parent
    root = packet.parents[3]
    receipt_folder = packet.parent / 'six-fitness-failures-resumed-2026-11-09'
    receipt = receipt_folder / 'validation.json'
    data = json.loads(receipt.read_text())
    bindings = json.loads((packet / 'input-bindings.json').read_text())
    if len(bindings) != len(data['post_verification_inputs']) or {item['path'] for item in bindings} != set(data['post_verification_inputs']):
        raise ValueError('Recorded input inventory differs')
    with tempfile.TemporaryDirectory(prefix='six-fitness-historical-') as temporary:
        snapshot = Path(temporary)
        git_dir = subprocess.check_output(['git', 'rev-parse', '--absolute-git-dir'], cwd=root, text=True).strip()
        (snapshot / '.git').write_text('gitdir: ' + git_dir + '\n')
        # Check current preservation promises; only the tested input files are replayed.
        for name in list(data['preserved_files']) + ['_bmad-output/implementation-artifacts/deferred-work.md']:
            target = snapshot / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(root / name, target)
        for item in bindings:
            name = item['path']
            owner, relative = root, name
            prefix = 'references/Hexalith.Tenants/'
            if name.startswith(prefix):
                owner, relative = root / 'references/Hexalith.Tenants', name.removeprefix(prefix)
            blob = subprocess.check_output(['git', 'show', item['revision'] + ':' + relative], cwd=owner)
            if hashlib.sha256(blob).hexdigest() != data['post_verification_inputs'][name]:
                raise ValueError('Recorded input bytes differ: ' + name)
            target = snapshot / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(blob)
        current_spec = (root / '_bmad-output/implementation-artifacts/spec-six-fitness-failures.md').read_bytes()
        recorded_spec = (snapshot / '_bmad-output/implementation-artifacts/spec-six-fitness-failures.md').read_bytes()
        import re
        frozen = lambda blob: re.search(rb'<frozen-after-approval.*?</frozen-after-approval>', blob, re.S).group()
        if frozen(current_spec) != frozen(recorded_spec):
            raise ValueError('Current frozen intent differs')
        for flags in [[], ['-O']]:
            for script in ['verify-receipt.py', 'test-verify-receipt.py']:
                subprocess.run([sys.executable, *flags, str(receipt_folder / script), '--receipt', str(receipt),
                                '--root', str(snapshot)], cwd=root, check=True)


if __name__ == '__main__':
    main()
