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

    def duplicate_floor(tree):
        tests = tree.findall('.//test')
        old = next(t for t in tests if t.get('method') == 'EventStore_package_floor_does_not_override_nested_owner_catalogsAsync')
        entry = next(t for t in tests if t.get('method') == 'EventStorePackageFloorRunsThroughRestoreAndBuildAsync')
        identity = old.get('id')
        old.attrib.update(entry.attrib)
        old.set('id', identity)

    def rewrite_json(data, work, name, change):
        value = json.loads(gzip.decompress((work / data['archives'][name]['archive']).read_bytes()))
        change(value)
        rewrite(data, work, name, json.dumps(value).encode())

    def shared_skip(data, work):
        for name in ['parent-source-full-socket-enabled', final]:
            change_xml(data, work, name, swapped_skip)

    def changed_failure(tree):
        failure = tree.find(".//test[@result='Fail']/failure")
        failure.set('exception-type', 'UnexpectedApplicationRegression')
        failure.find('message').text = 'Unrelated application regression'

    def coordinated_floor(data, work):
        def replace(tree):
            tests = tree.findall('.//test')
            old = next(t for t in tests if t.get('method') == 'EventStore_package_floor_does_not_override_nested_owner_catalogsAsync')
            duplicate = next(t for t in tests if t.get('method') == 'EventStore_package_floor_preserves_newer_catalog_and_explicit_source_selectionsAsync')
            identity = old.get('id')
            old.attrib.update(duplicate.attrib)
            old.set('id', identity)
        for name in ['owner-contracts-clean-environment-baseline', 'owner-patched-floor', 'owner-patched-contracts-full']:
            change_xml(data, work, name, replace)

    def coordinated_source_mode(data, work):
        rewrite(data, work, 'source-environment.json', b'{}')
        for name in ['parent-source-fitness-build', 'projections-source-build', 'owner-patched-contracts-build']:
            run(data, name)['environment_overrides'] = {}

    def remove_runs(data, names):
        data['runs'] = [record for record in data['runs'] if record['name'] not in names]

    def change_source_test_environments(data, work):
        for name in ['parent-source-fitness-tests', 'parent-source-full-socket-enabled',
                     'projections-source-tests', 'owner-server-socket-enabled']:
            run(data, name)['environment_overrides'] = {}

    def change_hook_commands(data, work):
        for record in data['runs']:
            if record['name'].startswith('hook-qualified-'):
                record['command'] = ['dotnet', 'msbuild', 'unrelated.proj', '-t:ValidateEventStorePackageFloor']

    def remove_second_review(data, work):
        data.pop('followup_review')
        for name in list(data['archives']):
            if name.startswith('second-review-'):
                data['archives'].pop(name)

    def runner_error(tree):
        ET.SubElement(ET.SubElement(tree, 'errors'), 'error', {'type': 'assembly-cleanup'}).text = 'Runner failed'

    def coordinated_execution(data, work):
        run(data, final)['command'].extend(['-class', 'NoSuchClass'])
        rewrite_json(data, work, 'recorded-execution-inventory.json',
                     lambda value: next(r for r in value['executions'] if r['name'] == final)['command'].extend(['-class', 'NoSuchClass']))

    def sanitized_only(data, work, name, change):
        original_hash = data['archives'][name]['original_sha256']
        value = gzip.decompress((work / data['archives'][name]['archive']).read_bytes())
        rewrite(data, work, name, change(value))
        data['archives'][name]['original_sha256'] = original_hash

    def source_property(data, work, key, value):
        name = run(data, 'parent-source-fitness-selection')['log']
        def change(blob):
            payload = json.loads(blob)
            if value is None:
                payload['Properties'].pop(key)
            else:
                payload['Properties'][key] = value
            return json.dumps(payload).encode()
        sanitized_only(data, work, name, change)

    def synthetic_server_case(blob):
        tree = ET.fromstring(blob)
        test = tree.find('.//test')
        test.set('name', 'Synthetic.ControlThatDidNotRun')
        test.set('method', 'ControlThatDidNotRun')
        return ET.tostring(tree, encoding='utf-8')

    def replaced_review(data, work):
        name = 'blind-hunter-followup.md'
        rewrite(data, work, name, b'Unrelated text')

    def replaced_diff(data, work):
        rewrite(data, work, 'owner-patch.diff', b'not a patch')
        data['reviewed_diff_sha256']['owner-patch.diff'] = hashlib.sha256(b'not a patch').hexdigest()

    def companion_manifest(data, work):
        path = work / 'third-review-validation.json'
        value = json.loads(path.read_text())
        value['fresh_full_service']['passed'] = 0
        path.write_text(json.dumps(value))

    def replaced_recorded_input(data, work):
        fixture = work / 'root'
        fixture.mkdir()
        for path in args.root.resolve().iterdir():
            (fixture / path.name).symlink_to(path, target_is_directory=path.is_dir())
        (fixture / 'README.md').unlink()
        (fixture / 'README.md').write_bytes(b'Unrelated current content')
        data['post_verification_inputs']['README.md'] = hashlib.sha256(b'Unrelated current content').hexdigest()
        return fixture

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
        ('missing-owner-test-input', lambda d, w: d['post_verification_inputs'].pop('references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests/PackageGovernanceTests.cs'), 'input inventory'),
        ('assembly-snapshot-path', lambda d, w: rewrite_json(d, w, 'final-parent-package-assembly.json', lambda v: v.update(path='different.dll')), 'assembly snapshot selection'),
        ('assembly-snapshot-digest', lambda d, w: rewrite_json(d, w, 'final-parent-package-assembly.json', lambda v: v.update(sha256='0' * 64)), 'assembly snapshot digest'),
        ('build-project', lambda d, w: run(d, 'final-parent-package-build-corrected')['command'].__setitem__(2, 'different.csproj'), 'build invocation'),
        ('build-directory', lambda d, w: run(d, 'final-parent-package-build-corrected').update(cwd='unrelated'), 'build invocation'),
        ('build-mode', lambda d, w: run(d, 'final-parent-package-build-corrected').update(environment_overrides={'HexalithSourceMappingMode': 'source'}), 'build invocation'),
        ('failed-package-selection', lambda d, w: run(d, 'final-parent-package-selection').update(exit_code=1), 'package selection exit'),
        ('duplicate-floor-execution', xml_case('owner-patched-floor', duplicate_floor), 'floor inventory'),
        ('duplicate-full-floor-execution', xml_case('owner-patched-contracts-full', duplicate_floor), 'full floor inventory'),
        ('shared-unpermitted-skip', shared_skip, 'permitted parent skips'),
        ('missing-projection-skip-reason', xml_case('projections-source-tests', lambda tree: tree.find(".//test[@result='Skip']").remove(tree.find(".//test[@result='Skip']/reason"))), 'projection skip reason'),
        ('changed-owner-failure-cause', xml_case('owner-patched-contracts-full', changed_failure), 'owner failure diagnostics'),
        ('empty-review', lambda d, w: rewrite(d, w, 'blind-hunter-followup.md', b''), 'empty review report'),
        ('changed-revision-scope', lambda d, w: d['scope'].update(owner_selected_commit='0' * 40), 'revision scope'),
        ('invalid-observed-revision', lambda d, w: d.update(observed_head='0' * 40), 'observed revision'),
        ('coordinated-floor-drift', coordinated_floor, 'recorded floor inventory'),
        ('changed-floor-anchor', lambda d, w: rewrite_json(d, w, 'recorded-floor-inventory.json', lambda v: v.pop()), 'floor inventory anchor'),
        ('coordinated-source-mode', coordinated_source_mode, 'source environment anchor'),
        ('filtered-full-command', lambda d, w: run(d, final)['command'].extend(['-class', 'NoSuchClass']), 'recorded execution inventory'),
        ('source-test-environments', change_source_test_environments, 'recorded execution inventory'),
        ('source-test-directory', lambda d, w: run(d, 'parent-source-fitness-tests').update(cwd='unrelated'), 'recorded execution inventory'),
        ('missing-source-selections', lambda d, w: remove_runs(d, ['parent-source-fitness-selection', 'projections-source-selection', 'owner-contracts-selection', 'owner-server-selection']), 'recorded execution inventory'),
        ('missing-owner-builds', lambda d, w: remove_runs(d, ['owner-contracts-build', 'owner-server-build']), 'recorded execution inventory'),
        ('unrelated-source-blocker', lambda d, w: run(d, 'final-parent-source-build').update(command=['unrelated'], cwd='unrelated', environment_overrides={}), 'recorded execution inventory'),
        ('direct-hook-commands', change_hook_commands, 'recorded execution inventory'),
        ('failed-commitlint', lambda d, w: [run(d, name).update(exit_code=1) for name in ['owner-commitlint', 'root-commitlint']], 'recorded execution inventory'),
        ('missing-repository-safeguards', lambda d, w: remove_runs(d, ['warning-guard', 'gitlink-worktree', 'scope-whitespace']), 'recorded execution inventory'),
        ('missing-second-review', remove_second_review, 'second review metadata'),
        ('missing-fixture-input', lambda d, w: d['post_verification_inputs'].pop('_bmad-output/implementation-artifacts/tests/six-fitness-failures-resumed-2026-11-09/test-verify-receipt.py'), 'input inventory'),
        ('runner-error-element', xml_case(final, runner_error), 'runner error elements'),
        ('coordinated-execution-drift', coordinated_execution, 'execution inventory anchor'),
        ('changed-root-validated-message', lambda d, w: rewrite(d, w, 'root-message.txt', b'Invalid retained candidate\n'), 'validated commit message binding'),
        ('changed-owner-validated-message', lambda d, w: rewrite(d, w, 'owner-message.txt', b'Invalid retained candidate\n'), 'validated commit message binding'),
        ('sanitized-only-server-identity', lambda d, w: sanitized_only(d, w, run(d, 'owner-server-socket-enabled')['xml'], synthetic_server_case), 'sanitized payload inventory anchor'),
        ('evaluated-package-as-source', lambda d, w: source_property(d, w, 'HexalithEventStoreFromSource', 'false'), 'source selection: parent-source-fitness-selection'),
        ('missing-evaluated-mapping-hash', lambda d, w: source_property(d, w, 'HexalithSourceMappingHash', None), 'source selection mapping'),
        ('replaced-reviewed-diff', replaced_diff, 'sanitized payload inventory anchor'),
        ('replaced-nonempty-review', replaced_review, 'sanitized payload inventory anchor'),
        ('missing-derived-owner-failures', lambda d, w: run(d, 'owner-patched-contracts-full').update(failures=[]), 'derived failures'),
        ('missing-derived-parent-skips', lambda d, w: run(d, final).update(skips=[]), 'derived skips'),
        ('changed-third-review-manifest', companion_manifest, 'third review manifest anchor'),
        ('missing-third-review-manifest', lambda d, w: (w / 'third-review-validation.json').unlink(), 'third-review-validation.json'),
        ('changed-third-review-artifact', lambda d, w: (w / 'third-review-fresh-full-source.xml.gz').write_bytes(b'invalid'), 'third review compressed hash'),
        ('refreshed-historical-input-hash', replaced_recorded_input, 'recorded input inventory anchor'),
    ]
    for name, mutation, expected in cases:
        with tempfile.TemporaryDirectory(prefix='six-fitness-gate-') as temporary:
            work = Path(temporary)
            for item in original['archives'].values():
                shutil.copy2(folder / item['archive'], work / item['archive'])
            shutil.copy2(folder / 'third-review-validation.json', work / 'third-review-validation.json')
            companion = json.loads((folder / 'third-review-validation.json').read_text())
            for item in companion['archives'].values():
                shutil.copy2(folder / item['archive'], work / item['archive'])
            data = copy.deepcopy(original)
            alternate_root = mutation(data, work)
            try:
                gate.verify(work, alternate_root if isinstance(alternate_root, Path) else args.root, data)
            except (ValueError, KeyError, OSError, ET.ParseError) as error:
                if expected not in str(error):
                    raise RuntimeError(name + ' failed for the wrong reason: ' + str(error)) from error
                results.append({'case': name, 'result': 'rejected', 'reason': str(error)})
            else:
                raise RuntimeError('Accepted corrupt evidence: ' + name)
    print(json.dumps({'cases': len(results), 'results': results}, indent=2))


if __name__ == '__main__':
    main()
