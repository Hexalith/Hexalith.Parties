"""Exercise receipt rejection using isolated temporary copies, normally and under -O."""
from pathlib import Path
import copy
import gzip
import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

packet = Path(__file__).resolve().parent
root = packet.parents[3]
original = json.loads((packet / 'validation.json').read_text())
results = []


def replace_raw(directory, receipt, name, data, bind_check=True):
    compressed = gzip.compress(data, mtime=0)
    (directory / (name + '.gz')).write_bytes(compressed)
    entry = next(a for a in receipt['archives'] if a['file'] == name + '.gz')
    entry.update(raw_bytes=len(data), raw_sha256=hashlib.sha256(data).hexdigest(),
                 compressed_sha256=hashlib.sha256(compressed).hexdigest())
    if bind_check:
        for check in receipt['checks']:
            if check['log'] == name:
                check['sha256'] = hashlib.sha256(data).hexdigest()
            if name == check['name'] + '.xml':
                check['xml_sha256'] = hashlib.sha256(data).hexdigest()
                tree = ET.fromstring(data)
                check['assemblies'] = [a.attrib for a in tree.findall('.//assembly')]
                check['skips'] = [{'name': t.get('name'), 'reason': t.findtext('reason')}
                                  for t in tree.findall('.//test') if t.get('result') == 'Skip']


def mutate(directory, receipt, scenario):
    check = next(c for c in receipt['checks'] if c['name'] == 'final-source-prerequisites')
    if scenario == 'required-exit':
        check['exit_code'] = 1
    elif scenario == 'missing-xml':
        receipt['archives'] = [a for a in receipt['archives'] if a['file'] != check['name'] + '.xml.gz']
    elif scenario == 'empty-xml':
        replace_raw(directory, receipt, check['name'] + '.xml', b'<assemblies/>')
    elif scenario in ('prerequisite-skip', 'unexpected-runtime-skip'):
        name = check['name'] if scenario == 'prerequisite-skip' else 'final-source-full'
        tree = ET.fromstring(gzip.decompress((directory / (name + '.xml.gz')).read_bytes()))
        if scenario == 'prerequisite-skip':
            test = tree.find('.//test')
            test.set('result', 'Skip')
            ET.SubElement(test, 'reason').text = 'unexpected opt-out'
            assembly = tree.find('.//assembly')
            assembly.set('passed', str(int(assembly.get('passed')) - 1))
            assembly.set('skipped', '1')
        else:
            test = next(t for t in tree.findall('.//test') if t.get('result') == 'Skip')
            test.set('name', 'unexpected-runtime-case')
        replace_raw(directory, receipt, name + '.xml', ET.tostring(tree))
    elif scenario == 'stale-check-hash':
        name = check['log']
        raw = gzip.decompress((directory / (name + '.gz')).read_bytes()) + b'changed\n'
        replace_raw(directory, receipt, name, raw, bind_check=False)
    elif scenario == 'missing-inputs':
        receipt.pop('post_verification_input_hashes')
    elif scenario == 'wrong-mode':
        name = 'final-package-selection.log'
        selected = json.loads(gzip.decompress((directory / (name + '.gz')).read_bytes()))
        selected['Properties']['HexalithSourceMappingMode'] = 'source'
        replace_raw(directory, receipt, name, json.dumps(selected).encode())
    elif scenario == 'wrong-target':
        check['command'][1] = '/tmp/unrelated-stale.dll'
    elif scenario == 'mixed-selectors':
        name = 'post-review-package-mode.log'
        selected = json.loads(gzip.decompress((directory / (name + '.gz')).read_bytes()))
        selected['Properties']['HexalithCommonsHttpFromSource'] = 'true'
        replace_raw(directory, receipt, name, json.dumps(selected).encode())
    else:
        raise ValueError(scenario)


for optimized in (False, True):
    for scenario in ('valid', 'required-exit', 'missing-xml', 'empty-xml', 'prerequisite-skip',
                     'unexpected-runtime-skip', 'stale-check-hash', 'missing-inputs',
                     'wrong-mode', 'wrong-target', 'mixed-selectors'):
        with tempfile.TemporaryDirectory(prefix='dw27-receipt-fixture-') as temporary:
            directory = Path(temporary)
            for entry in original['archives']:
                shutil.copyfile(packet / entry['file'], directory / entry['file'])
            receipt = copy.deepcopy(original)
            if scenario != 'valid':
                mutate(directory, receipt, scenario)
            path = directory / 'validation.json'
            path.write_text(json.dumps(receipt))
            command = [sys.executable] + (['-O'] if optimized else []) + [str(packet / 'validate-receipt.py'),
                       '--receipt', str(path), '--root', str(root)]
            run = subprocess.run(command, capture_output=True, text=True, timeout=30)
            expected = 0 if scenario == 'valid' else 1
            if run.returncode != expected:
                raise RuntimeError(f'{scenario} optimized={optimized}: {run.returncode}: {run.stdout}{run.stderr}')
            results.append(dict(scenario=scenario, optimized=optimized, exit_code=run.returncode,
                                output=(run.stdout + run.stderr).strip()))
print(json.dumps(dict(result='PASS', cases=len(results), checks=results), indent=2))
