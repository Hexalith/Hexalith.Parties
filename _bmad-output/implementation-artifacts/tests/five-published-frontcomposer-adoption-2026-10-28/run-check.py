"""Capture bounded command receipts; normalize local prefixes in public evidence."""
import argparse
import datetime
import hashlib
import json
import os
import shutil
from pathlib import Path
import subprocess
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
PRIVATE = Path(tempfile.gettempdir()) / 'five-published-frontcomposer-private-20261028'
PRIVATE.mkdir(exist_ok=True)


def normalized(value):
    return str(value).replace(str(ROOT), '${WORKSPACE}').replace(str(Path.home()), '${HOME}').replace('/tmp/', '${TMP}/')


parser = argparse.ArgumentParser()
parser.add_argument('name')
parser.add_argument('--cwd', default='.')
parser.add_argument('--source-environment')
parser.add_argument('--node24', action='store_true')
parser.add_argument('command', nargs=argparse.REMAINDER)
args = parser.parse_args()
command = args.command
if command and command[0] == '--':
    command = command[1:]
environment = os.environ.copy()
overrides = {}
if args.source_environment:
    overrides.update(json.loads(Path(args.source_environment).read_text()))
if args.node24:
    node24 = environment.get('PARTIES_NODE24_BIN', str(Path(tempfile.gettempdir()) / 'five-frontcomposer-node24' / 'node-v24.22.1-linux-x64' / 'bin'))
    overrides['PATH'] = node24 + os.pathsep + environment['PATH']
environment.update(overrides)
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
xml_outputs = [ROOT / args.cwd / command[index + 1] for index, value in enumerate(command)
               if value in ('-result-xml', '-xml')]
# A capture may only attach XML created by this execution. Preserve previous
# receipts and result bytes before vacating the requested output paths.
previous = [HERE / f'{args.name}.json', HERE / f'{args.name}.txt', *xml_outputs]
existing = list(dict.fromkeys(path for path in previous if path.exists()))
if existing:
    archive = HERE / 'historical-results' / (datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ-') + args.name)
    archive.mkdir(parents=True)
    for path in existing:
        destination = archive / path.name
        if path in xml_outputs:
            shutil.move(path, destination)
        else:
            shutil.copyfile(path, destination)
result = subprocess.run(command, cwd=ROOT / args.cwd, env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
raw = PRIVATE / f'{args.name}.txt'
raw.write_bytes(result.stdout)
public = HERE / f'{args.name}.txt'
public.write_text(normalized(result.stdout.decode(errors='replace')))
record = {
    'name': args.name, 'started_utc': started,
    'command': [normalized(item) for item in command],
    'cwd': '${WORKSPACE}' + (f'/{args.cwd}' if args.cwd != '.' else ''),
    'environment_overrides': {key: normalized(value) if key != 'PATH' else '${NODE24_BIN}:${INHERITED_PATH}' for key, value in overrides.items()},
    'exit_code': result.returncode,
    'log': public.name, 'log_sha256': hashlib.sha256(public.read_bytes()).hexdigest(),
    'private_original_sha256': hashlib.sha256(result.stdout).hexdigest(),
}
missing_xml = []
for artifact in xml_outputs:
    if artifact.exists():
        original = artifact.read_bytes()
        (PRIVATE / artifact.name).write_bytes(original)
        artifact.write_text(normalized(original.decode()))
        record['xunit_xml'] = {'path': str(artifact.relative_to(HERE)), 'sha256': hashlib.sha256(artifact.read_bytes()).hexdigest(),
                              'private_original_sha256': hashlib.sha256(original).hexdigest()}
    else:
        missing_xml.append(normalized(artifact))
if missing_xml:
    record['missing_result_xml'] = missing_xml
    record['capture_error'] = 'The command did not produce the requested fresh result XML.'
(HERE / f'{args.name}.json').write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({'name': args.name, 'exit_code': result.returncode, 'log': public.name}))
raise SystemExit(result.returncode or (1 if missing_xml else 0))
