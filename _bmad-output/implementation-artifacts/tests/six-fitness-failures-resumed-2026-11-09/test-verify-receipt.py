"""Exercise the receipt gate against valid evidence and independent corruptions."""

import argparse
import copy
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--receipt', required=True, type=Path)
    parser.add_argument('--root', required=True, type=Path)
    args = parser.parse_args()
    folder = args.receipt.resolve().parent
    module = importlib.util.spec_from_file_location('receipt_gate', folder / 'verify-receipt.py')
    gate = importlib.util.module_from_spec(module)
    module.loader.exec_module(gate)
    original = json.loads(args.receipt.read_text())
    gate.verify(folder, args.root, copy.deepcopy(original))
    results = [{'case': 'valid', 'result': 'accepted'}]

    def run(data, name):
        return next(r for r in data['runs'] if r['name'] == name)

    def rewrite(data, work, name, value):
        item = data['archives'][name]
        blob = gzip.compress(value, mtime=0)
        (work / item['archive']).write_bytes(blob)
        item.update(original_sha256=hashlib.sha256(value).hexdigest(),
                    sanitized_sha256=hashlib.sha256(value).hexdigest(),
                    compressed_sha256=hashlib.sha256(blob).hexdigest())

    def change_xml(data, work, name, change):
        record = run(data, name)
        tree = ET.fromstring(gzip.decompress((work / data['archives'][record['xml']]['archive']).read_bytes()))
        change(tree)
        value = ET.tostring(tree, encoding='utf-8')
        rewrite(data, work, record['xml'], value)
        record['xml_sha256'] = hashlib.sha256(value).hexdigest()
        record['assemblies'] = [a.attrib for a in tree.findall('assembly')]

    def recount(tree):
        for assembly in tree.findall('assembly'):
            tests = assembly.findall('.//test')
            assembly.set('total', str(len(tests)))
            for outcome, field in [('Pass', 'passed'), ('Fail', 'failed'), ('Skip', 'skipped')]:
                assembly.set(field, str(sum(t.get('result') == outcome for t in tests)))

    def reduced(tree):
        keep = ['ExternalRuntimeGraphsMatchTheIndependentlyEvaluatedPartiesPackageSelection',
                'CommittedSelectionObservations_PreservePackageGraphAndImmutableConsumptionRefusals',
                'CommittedSourceSelections_RecordOverridesAndRefuseInvalidImmutableConsumption',
                'MaintainedTechnologyTablesMatchCentralPackageCatalog']
        for collection in tree.findall('.//collection'):
            for test in list(collection.findall('test')):
                if not any(m in test.get('method', '') for m in keep):
                    collection.remove(test)
        recount(tree)

    def all_skipped(tree):
        for test in tree.findall('.//test'):
            test.set('result', 'Skip')
            ET.SubElement(test, 'reason').text = 'Unexpected skip'
        recount(tree)

    def swapped_skip(tree):
        old = tree.find(".//test[@result='Skip']")
        new = tree.find(".//test[@result='Pass']")
        reason = old.findtext('reason')
        old.set('result', 'Pass')
        old.remove(old.find('reason'))
        new.set('result', 'Skip')
        ET.SubElement(new, 'reason').text = reason
        recount(tree)

    def floor_argument(tree):
        for test in tree.findall('.//test'):
            if test.get('method') == 'EventStorePackageFloorRunsThroughRestoreAndBuildAsync' and '3.128.3' in test.get('name'):
                test.set('name', test.get('name').replace('3.128.3', 'synthetic-version'))
                return
        raise RuntimeError('No floor argument fixture')

    def duplicate_id(tree):
        tests = tree.findall('.//test')
        tests[1].set('id', tests[0].get('id'))

    final = 'final-parent-package-full-corrected'
    xml_case = lambda name, change: lambda data, work: change_xml(data, work, name, change)

    def duplicate_mutation(data, work):
        value = json.loads(gzip.decompress((work / data['archives']['hook-mutation.json']['archive']).read_bytes()))
        control = next(c for c in value['controls'] if c['name'] == 'hook-qualified-detached-CollectPackageReferences')
        value['controls'] = [control] * 6
        rewrite(data, work, 'hook-mutation.json', json.dumps(value).encode())

    def hidden_owner_failure(tree):
        test = tree.find(".//test[@result='Fail']")
        test.set('result', 'Pass')
        test.remove(test.find('failure'))
        recount(tree)

    cases = [
        ('missing-XML', lambda d, w: run(d, final).pop('xml'), 'missing XML'),
        ('reduced-full-suite', xml_case(final, reduced), 'suite summary'),
        ('all-skipped-server', xml_case('owner-server-socket-enabled', all_skipped), 'suite summary'),
        ('changed-skip-identity', xml_case(final, swapped_skip), 'parent skip inventory'),
        ('floor-argument-drift', xml_case('owner-patched-floor', floor_argument), 'entry target cases'),
        ('duplicate-hook-controls', duplicate_mutation, 'mutation inventory'),
        ('empty-input-inventory', lambda d, w: d['post_verification_inputs'].clear(), 'input inventory'),
        ('missing-input', lambda d, w: d['post_verification_inputs'].pop('README.md'), 'input inventory'),
        ('missing-review', lambda d, w: d['archives'].pop('blind-hunter-followup.md'), 'review report'),
        ('compressed-hash', lambda d, w: d['archives']['delivery.diff'].update(compressed_sha256='0' * 64), 'compressed hash'),
        ('missing-full-run', lambda d, w: d['runs'].remove(run(d, final)), 'required successful run'),
        ('reduced-required-runs', lambda d, w: d['required_successful_runs'].pop(), 'required run inventory'),
        ('reduced-preservation-inventory', lambda d, w: d['preserved_files'].pop(next(iter(d['preserved_files']))), 'preservation inventory'),
        ('duplicate-test-ID', xml_case(final, duplicate_id), 'test identities'),
        ('empty-test-report', xml_case(final, lambda tree: tree.clear()), 'empty report'),
        ('nonzero-successful-exit', lambda d, w: run(d, final).update(exit_code=1), 'required successful run'),
        ('hidden-owner-failure', xml_case('owner-patched-contracts-full', hidden_owner_failure), 'suite summary'),
        ('empty-DW-inventory', lambda d, w: d['protected_dw_entry_sha256'].clear(), 'protected DW inventory'),
        ('changed-frozen-intent-binding', lambda d, w: d.update(frozen_intent_sha256='0' * 64), 'frozen intent binding'),
    ]
    for name, mutation, expected in cases:
        with tempfile.TemporaryDirectory(prefix='six-fitness-gate-') as temporary:
            work = Path(temporary)
            for item in original['archives'].values():
                shutil.copy2(folder / item['archive'], work / item['archive'])
            data = copy.deepcopy(original)
            mutation(data, work)
            try:
                gate.verify(work, args.root, data)
            except (ValueError, KeyError, OSError, ET.ParseError) as error:
                if expected not in str(error):
                    raise RuntimeError(name + ' failed for the wrong reason: ' + str(error)) from error
                results.append({'case': name, 'result': 'rejected', 'reason': str(error)})
            else:
                raise RuntimeError('Accepted corrupt evidence: ' + name)
    print(json.dumps({'cases': len(results), 'results': results}, indent=2))


if __name__ == '__main__':
    main()
