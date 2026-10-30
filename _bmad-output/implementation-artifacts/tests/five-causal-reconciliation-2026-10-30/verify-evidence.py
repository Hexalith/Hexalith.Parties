"""Read-only source/receipt and executed-matrix audit; supports python -O."""
import gzip
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

folder = Path(__file__).resolve().parent
root = folder.parents[3]
manifest = json.loads((folder / 'qualification.json').read_text())

def require(condition, message):
    if not condition:
        raise SystemExit(message)

for name, expected in manifest['sources'].items():
    path = root / name
    require(path.is_file(), f'missing source: {name}')
    require(hashlib.sha256(path.read_bytes().replace(b'\r\n', b'\n')).hexdigest() == expected, f'source changed: {name}')
for name, expected in manifest['artifacts'].items():
    path = folder / name
    require(path.is_file(), f'missing artifact: {name}')
    require(hashlib.sha256(path.read_bytes()).hexdigest() == expected, f'artifact changed: {name}')

for name, expected in manifest['xml_uncompressed_sha256'].items():
    require(hashlib.sha256(gzip.decompress((folder / name).read_bytes())).hexdigest() == expected, f'XML bytes changed: {name}')

for lane in manifest['final_lanes']:
    receipt = json.loads((folder / f'accepted-{lane}-tests.json').read_text())
    require(receipt['exit_code'] == 0, f'{lane} did not pass')
    cases = list(ET.parse(gzip.open(folder / f'accepted-{lane}-tests.xml.gz', 'rb')).iter('test'))
    require(len(cases) == manifest['case_count'][lane], f'{lane} case count changed')
    require(all(test.get('result') == 'Pass' for test in cases), f'{lane} has non-passing cases')
    for row, methods in manifest['matrix'].items():
        for method, count in methods.items():
            found = [test for test in cases if test.get('method') == method]
            require(len(found) == count, f'{lane}/{row}/{method}: expected {count}, found {len(found)}')
    project = ET.parse(folder / f'{lane}.Focused.csproj')
    constants = project.findtext('.//DefineConstants')
    require(('HFC_SHARED_RECONCILE_READINESS' in constants) == (lane in ('shared', 'shared-legacy-transport')), f'{lane} compilation fence wrong')

    require(('HES_CANCELLABLE_PROJECTIONS' in constants) == (lane in ('shared', 'retained-cancellable-transport')), f'{lane} transport compilation fence wrong')

negative = list(ET.parse(gzip.open(folder / 'callback-mutation-tests-clean.xml.gz', 'rb')).iter('test'))
require(len(negative) == 2 and all(test.get('result') == 'Fail' for test in negative), 'snapshot mutation must fail both cases')
require(all(test.get('method') == 'RemovalRevokesCallbackInAlreadyCapturedDispatchSnapshot' for test in negative), 'wrong mutation cases')
require(all('butwas1' in ''.join(test.findtext('failure/message').split()) for test in negative), 'mutation did not expose a late callback')
require(json.loads((folder / 'accepted-runner-tests.json').read_text())['exit_code'] == 0, 'runner controls did not pass')
print('PASS: source hashes, archived bytes, all four capability lanes and revocation mutation control')
