"""Validate retained evidence without promoting any blocked qualification gate."""

import argparse
from collections import Counter
import gzip
import hashlib
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def inventory_digest(value):
    return digest(json.dumps(value, sort_keys=True, separators=(',', ':')).encode())


def verify_companion(folder, root):
    """Check the separately retained third-review packet without restamping it."""
    blob = (folder / 'third-review-validation.json').read_bytes()
    require(digest(blob) == '570e08c0ccf92465f93df1e40593e7e1b4cfbf8f199d0140983e0f06ac572c12',
            'third review manifest anchor')
    data = json.loads(blob)
    for name, item in data['archives'].items():
        require(Path(item['archive']).name == item['archive'], 'third review archive path')
        archive = (folder / item['archive']).read_bytes()
        require(digest(archive) == item['compressed_sha256'], 'third review compressed hash: ' + name)
        require(digest(gzip.decompress(archive)) == item['sanitized_sha256'], 'third review payload hash: ' + name)
    for name, expected in data['current_input_sha256'].items():
        require(digest((root / name).read_bytes()) == expected, 'third review recorded input: ' + name)


def verify(folder, root, manifest=None):
    folder, root = Path(folder), Path(root)
    data = manifest if manifest is not None else json.loads((folder / 'validation.json').read_text())
    require(data['completion_status'] == 'in-review', 'blocked completion status')
    payloads = {}
    for name, item in data['archives'].items():
        require(Path(name).name == name, 'archive name')
        archive = item['archive']
        require(Path(archive).name == archive, 'archive path')
        blob = (folder / archive).read_bytes()
        require(digest(blob) == item['compressed_sha256'], 'compressed hash: ' + name)
        plain = gzip.decompress(blob)
        require(digest(plain) == item['sanitized_sha256'], 'payload hash: ' + name)
        payloads[name] = plain

    runs = {}
    reports = {}
    for run in data['runs']:
        name = run['name']
        require(name not in runs, 'duplicate run: ' + name)
        runs[name] = run
        log = data['archives'][run['log']]
        require(log['original_sha256'] == run['log_sha256'], 'run log binding: ' + name)
        require(isinstance(run['command'], list) and run['command'], 'command: ' + name)
        require('cwd' in run and 'environment_overrides' in run and 'cleared_environment_keys' in run,
                'execution environment: ' + name)
        if run.get('assemblies'):
            require('xml' in run, 'missing XML: ' + name)
        if 'xml' not in run:
            continue
        require(data['archives'][run['xml']]['original_sha256'] == run['xml_sha256'], 'XML binding: ' + name)
        tree = ET.fromstring(payloads[run['xml']])
        require(not tree.findall('.//errors/error'), 'runner error elements: ' + name)
        assemblies = tree.findall('assembly')
        require(assemblies, 'empty report: ' + name)
        require([a.attrib for a in assemblies] == run['assemblies'], 'assembly attributes: ' + name)
        identities = []
        names = []
        for assembly in assemblies:
            tests = assembly.findall('.//test')
            require(tests and int(assembly.get('errors')) == 0, 'empty tests or runner errors: ' + name)
            require(int(assembly.get('not-run')) == 0, 'unexecuted tests: ' + name)
            require(int(assembly.get('total')) == len(tests), 'test total: ' + name)
            for outcome, attr in [('Pass', 'passed'), ('Fail', 'failed'), ('Skip', 'skipped')]:
                require(sum(t.get('result') == outcome for t in tests) == int(assembly.get(attr)),
                        'outcome count: ' + name)
            require(all(t.get('result') in ['Pass', 'Fail', 'Skip'] for t in tests), 'unknown outcome: ' + name)
            identities.extend(t.get('id') for t in tests)
            names.extend(t.get('name') for t in tests)
        require(all(identities) and len(set(identities)) == len(identities), 'test identities: ' + name)
        # xUnit truncates long InlineData values, so distinct cases can have equal display names.
        require(all(names), 'test names: ' + name)
        reports[name] = tree

    mandatory = ['parent-source-fitness-tests', 'parent-source-full-socket-enabled',
                 'projections-source-tests', 'owner-server-socket-enabled', 'owner-patched-floor',
                 'final-parent-package-build-corrected', 'final-documentation-package-corrected', 'final-parent-package-full-corrected']
    require(data['required_successful_runs'] == mandatory, 'required run inventory')
    for name in mandatory:
        require(name in runs and runs[name]['exit_code'] == 0, 'required successful run: ' + name)
        if name != 'final-parent-package-build-corrected':
            require(name in reports, 'required test report: ' + name)
            require(not reports[name].findall(".//test[@result='Fail']"), 'required test failure: ' + name)
    for name in ['parent-source-fitness-tests', 'owner-patched-floor', 'final-documentation-package-corrected']:
        require(not reports[name].findall(".//test[@result='Skip']"), 'focused skip: ' + name)
    summaries = {
        'parent-source-fitness-tests': (304, 304, 0, 0),
        'parent-source-full-socket-enabled': (2865, 2852, 0, 13),
        'final-parent-package-full-corrected': (2865, 2852, 0, 13),
        'projections-source-tests': (467, 466, 0, 1),
        'owner-server-socket-enabled': (1875, 1875, 0, 0),
        'owner-patched-floor': (48, 48, 0, 0),
        'final-documentation-package-corrected': (10, 10, 0, 0),
        'owner-contracts-clean-environment-baseline': (1018, 813, 205, 0),
        'owner-patched-contracts-full': (1028, 823, 205, 0),
    }
    for name, expected_summary in summaries.items():
        require(name in reports, 'required report: ' + name)
        actual = tuple(sum(int(a.get(field)) for a in reports[name].findall('assembly'))
                       for field in ['total', 'passed', 'failed', 'skipped'])
        require(actual == expected_summary, 'suite summary: ' + name)
    full_before = reports['parent-source-full-socket-enabled'].findall('.//test')
    full_after = reports['final-parent-package-full-corrected'].findall('.//test')
    require(Counter(t.get('name') for t in full_before) == Counter(t.get('name') for t in full_after), 'full parent inventory')
    skip_inventory = lambda tests: sorted((t.get('name'), t.findtext('reason')) for t in tests if t.get('result') == 'Skip')
    require(skip_inventory(full_before) == skip_inventory(full_after), 'parent skip inventory')
    require(digest(payloads['permitted-parent-skips.json']) == '6efa46b21ad98d0842fcfb0e9635f10f8d76e8e87e4e67419235f205eeef4c84', 'permitted skip anchor')
    require(skip_inventory(full_after) == [tuple(item) for item in json.loads(payloads['permitted-parent-skips.json'])], 'permitted parent skips')
    projection_skips = reports['projections-source-tests'].findall(".//test[@result='Skip']")
    require(projection_skips[0].get('name') == 'Hexalith.Parties.Projections.Tests.Handlers.PartySdkProjectionHandlerTests.PublishedSdkPreservesLargeLegacyLiveAndRebuildCompatibilityAsync', 'projection skip identity')
    require(projection_skips[0].findtext('reason') == 'Runs against the unchanged published SDK package; source generation behavior has its separate control.', 'projection skip reason')
    require(len(reports['owner-patched-floor'].findall('.//test')) == 48, 'floor case count')
    prefix = 'Hexalith.Tenants.Contracts.Tests.PackageGovernanceTests.EventStorePackageFloorRunsThroughRestoreAndBuildAsync'
    expected_floor = set()
    for entry in ['CollectPackageReferences', 'PrepareForBuild']:
        for version, source, accepted in [('3.128.3', False, False), ('3.128.4-preview.1', False, False),
                                          ('3.128.4+metadata-with-dash', False, True), ('4.1.0', False, True),
                                          ('test-source-version', True, True)]:
            expected_floor.add(f'{prefix}(entryTarget: "{entry}", selectedVersion: "{version}", useSource: {source}, accepted: {accepted})')
    floor_cases = [t for t in reports['owner-patched-floor'].findall('.//test') if t.get('method') == prefix.rsplit('.', 1)[1]]
    require({t.get('name').replace('\\', '') for t in floor_cases} == expected_floor
            and all(t.get('result') == 'Pass' for t in floor_cases), 'entry target cases')
    baseline_floor_methods = {
        'EventStore_package_floor_does_not_override_nested_owner_catalogsAsync',
        'EventStore_package_floor_preserves_newer_catalog_and_explicit_source_selectionsAsync',
        'EventStore_package_floor_rejects_older_global_package_overridesAsync',
    }
    baseline_floor = [t for t in reports['owner-contracts-clean-environment-baseline'].findall('.//test')
                      if t.get('method') in baseline_floor_methods]
    expected_inventory = Counter(t.get('name').replace('\\', '') for t in baseline_floor)
    expected_inventory.update(expected_floor)
    require(digest(payloads['recorded-floor-inventory.json']) == '598ae7b0a34a92423d58a8e8be9c2110eef9096953c94211a035f80b62d12a9f', 'floor inventory anchor')
    require(expected_inventory == Counter(json.loads(payloads['recorded-floor-inventory.json'])), 'recorded floor inventory')
    require(Counter(t.get('name').replace('\\', '') for t in reports['owner-patched-floor'].findall('.//test'))
            == expected_inventory, 'floor inventory')
    full_floor = [t for t in reports['owner-patched-contracts-full'].findall('.//test')
                  if t.get('method') in baseline_floor_methods | {prefix.rsplit('.', 1)[1]}]
    require(Counter(t.get('name').replace('\\', '') for t in full_floor) == expected_inventory, 'full floor inventory')

    current = full_after
    expected = {
        'ExternalRuntimeGraphsMatchTheIndependentlyEvaluatedPartiesPackageSelection': (2, ['Debug', 'Release']),
        'CommittedSelectionObservations_PreservePackageGraphAndImmutableConsumptionRefusals': (1, []),
        'CommittedSourceSelections_RecordOverridesAndRefuseInvalidImmutableConsumption': (2, ['McpCli', 'Platform']),
        'MaintainedTechnologyTablesMatchCentralPackageCatalog': (1, []),
    }
    for method, (count, values) in expected.items():
        cases = [t for t in current if re.search(r'\.' + re.escape(method) + r'(?:\(|$)', t.get('name', ''))]
        require(len(cases) == count and all(t.get('result') == 'Pass' for t in cases), 'six controls: ' + method)
        if values:
            parameter = 'configuration' if method.startswith('ExternalRuntimeGraphs') else 'dependency'
            expected_arguments = {f'{parameter}: "{value}")' for value in values}
            actual_arguments = {t.get('name').replace('\\', '').split('(', 1)[1] for t in cases}
            require(actual_arguments == expected_arguments, 'case parameters: ' + method)

    baseline = reports['owner-contracts-clean-environment-baseline']
    patched = reports['owner-patched-contracts-full']
    failures = lambda tree: {t.get('name') for t in tree.findall(".//test[@result='Fail']")}
    require(failures(baseline) == failures(patched) and len(failures(patched)) == 205, 'owner failure baseline')
    # Python fixture durations are the only differences in the retained failure messages.
    failure_diagnostics = lambda tree: Counter(
        (t.get('name'), t.find('failure').get('exception-type'),
         re.sub(r'(?m)^(Ran [0-9]+ tests in )[0-9.]+(s)$', r'\1<elapsed>\2', t.findtext('failure/message', '')))
        for t in tree.findall(".//test[@result='Fail']"))
    require(failure_diagnostics(baseline) == failure_diagnostics(patched), 'owner failure diagnostics')
    require(runs['owner-contracts-clean-environment-baseline']['exit_code'] == 1
            and runs['owner-patched-contracts-full']['exit_code'] == 1, 'owner blocked exits')
    require(runs['final-parent-source-build']['exit_code'] == 1
            and b'HXW005' in payloads[runs['final-parent-source-build']['log']], 'source blocker')
    require(digest(payloads['source-environment.json']) == 'dc28e0f5c71d50c964bd3ac825b64635bb3f2bf5dd3673f00b5be069df0105f2', 'source environment anchor')
    source_environment = json.loads(payloads['source-environment.json'])
    builds = {
        'parent-source-fitness-build': ('Hexalith.Parties.Tests', '${PARTIES_ROOT}', source_environment),
        'projections-source-build': ('Hexalith.Parties.Projections.Tests', '${PARTIES_ROOT}', source_environment),
        'owner-patched-contracts-build': ('Hexalith.Tenants.Contracts.Tests', '${PARTIES_ROOT}/references/Hexalith.Tenants', source_environment),
        'final-parent-package-build-corrected': ('Hexalith.Parties.Tests', '${PARTIES_ROOT}', {}),
    }
    for name, (project, cwd, environment) in builds.items():
        require(runs[name]['command'] == ['dotnet', 'build', f'tests/{project}/{project}.csproj',
                                         '--configuration', 'Debug', '--disable-build-servers', '-m:1']
                and runs[name]['cwd'] == cwd and runs[name]['environment_overrides'] == environment
                and runs[name]['cleared_environment_keys'] == [], 'build invocation: ' + name)
        text = payloads[runs[name]['log']].decode()
        require(runs[name]['exit_code'] == 0 and re.search(r'\b0 Warning\(s\)', text)
                and re.search(r'\b0 Error\(s\)', text), 'build summary: ' + name)

    selection = runs['final-parent-package-selection']
    require(selection['exit_code'] == 0, 'package selection exit')
    require(selection['command'] == ['dotnet', 'msbuild', 'tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj', '-nologo',
                                     '-getProperty:TargetPath,Configuration,TreatWarningsAsErrors,HexalithEventStoreVersion,HexalithEventStoreFromSource,HexalithSourceMappingMode,HexalithSourceMappingHash']
            and selection['cwd'] == '${PARTIES_ROOT}' and selection['environment_overrides'] == {}
            and selection['cleared_environment_keys'] == [], 'package selection invocation')
    props = json.loads(payloads[selection['log']])['Properties']
    require(props['Configuration'] == 'Debug' and props['TreatWarningsAsErrors'].lower() == 'true', 'build policy')
    require(props['HexalithEventStoreFromSource'] in ['', 'false'] and not props['HexalithSourceMappingMode'], 'package mode')
    require(props['HexalithEventStoreVersion'] == '4.1.0', 'package selection')
    # This snapshot predates the corrected build; its digest is not final-binary provenance.
    snapshot = json.loads(payloads['final-parent-package-assembly.json'])
    require(snapshot['path'] == props['TargetPath'] and snapshot['properties'] == props, 'assembly snapshot selection')
    require(re.fullmatch(r'[0-9a-f]{64}', snapshot['sha256']) and snapshot['sha256'] != '0' * 64, 'assembly snapshot digest')
    final_run = runs['final-parent-package-full-corrected']
    require(final_run['command'][:2] == ['dotnet', props['TargetPath']], 'evaluated test target')
    require(final_run['environment_overrides'] == {}, 'package test environment')
    require(all(a.get('name') == props['TargetPath'] for a in reports['final-parent-package-full-corrected'].findall('assembly')),
            'test assembly binding')

    for stem in ['blind-hunter', 'edge-case', 'verification-gap']:
        for suffix in ['original', 'followup']:
            require(stem + '-' + suffix + '.md' in payloads, 'review report: ' + stem)
            require(payloads[stem + '-' + suffix + '.md'].strip(), 'empty review report: ' + stem)
    for name in ['delivery.diff', 'owner-patch.diff', 'root-patch.diff']:
        require(name in payloads and digest(payloads[name]) == data['reviewed_diff_sha256'][name], 'review diff: ' + name)
    mutation = json.loads(payloads['hook-mutation.json'])
    require(len(mutation['controls']) == 6 and all(c['valid'] for c in mutation['controls']), 'hook mutation')
    expected_mutations = {'hook-qualified-' + variant + '-' + target:
                          variant == 'connected' or target == 'ValidateEventStorePackageFloor'
                          for variant in ['connected', 'detached']
                          for target in ['CollectPackageReferences', 'PrepareForBuild', 'ValidateEventStorePackageFloor']}
    require({c['name'] for c in mutation['controls']} == set(expected_mutations), 'mutation inventory')
    for control in mutation['controls']:
        require(control['expected_rejection'] is expected_mutations[control['name']], 'mutation expectation')
        result = runs[control['name']]
        if control['expected_rejection']:
            require(result['exit_code'] != 0 and b'must be at least 3.128.4' in payloads[result['log']], 'floor diagnostic')
        else:
            require(result['exit_code'] == 0, 'detached mutation control')

    packets = [
        ('207aaf51f63448e51acf83b4c46fdb456df6526c', 'five-data-lifecycle-qualification-2026-10-28'),
        ('2c08ae879008381948761db90ced5cd7eac091db', 'six-fitness-failures-2026-10-28'),
    ]
    preserved = {}
    for revision, leaf in packets:
        path = '_bmad-output/implementation-artifacts/tests/' + leaf
        files = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', revision, '--', path], cwd=root, text=True).splitlines()
        require(files, 'missing historical packet')
        for name in files:
            original = subprocess.check_output(['git', 'show', revision + ':' + name], cwd=root)
            require((root / name).read_bytes() == original, 'historical bytes: ' + name)
            preserved[name] = digest(original)
    require(preserved == data['preserved_files'], 'preservation inventory')
    resume = '806ed7b83b35fcc24996e58affb78ea51a0c8aa7'
    scope = json.loads(payloads['scope.json'])
    require(data['scope'] == scope and data['resume_revision'] == scope['resume_revision'] == resume,
            'revision scope')
    require(scope['baseline'] == packets[0][0] and scope['delivered'] == packets[1][0], 'revision anchors')
    require(scope['owner_local_commit'] == '60c1bd50c63513a466be2c301b48d3c4f1d3cf72', 'recorded owner patch revision')
    observed = data['observed_head']
    require(observed == 'd3d5e7d4de132c3e8b81d1ab04fb8c3541b8aaa4', 'observed revision')
    require(subprocess.check_output(['git', 'rev-parse', '--verify', observed + '^{commit}'], cwd=root, text=True).strip()
            == observed, 'observed revision')
    selected = subprocess.check_output(['git', 'ls-tree', observed, '--', 'references/Hexalith.Tenants'], cwd=root, text=True)
    require(selected.split()[2] == scope['owner_selected_commit'], 'selected owner revision')
    ledger_path = '_bmad-output/implementation-artifacts/deferred-work.md'
    old_ledger = subprocess.check_output(['git', 'show', resume + ':' + ledger_path], cwd=root, text=True)
    new_ledger = (root / ledger_path).read_text()
    expected_dw = {}
    for number in [50, 82, 168, 130, 149]:
        pattern = r'^### DW-' + str(number) + r':.*?(?=^### DW-|\Z)'
        before = re.search(pattern, old_ledger, re.M | re.S)
        after = re.search(pattern, new_ledger, re.M | re.S)
        require(before is not None and after is not None, 'missing protected DW entry')
        require(before.group() == after.group() and 'status: open' in after.group(), 'protected DW entry')
        expected_dw[str(number)] = digest(after.group().encode())
    require(expected_dw == data['protected_dw_entry_sha256'], 'protected DW inventory')
    spec_path = '_bmad-output/implementation-artifacts/spec-six-fitness-failures.md'
    before = subprocess.check_output(['git', 'show', resume + ':' + spec_path], cwd=root)
    frozen = lambda value: re.search(rb'<frozen-after-approval.*?</frozen-after-approval>', value, re.S)
    old_intent, new_intent = frozen(before), frozen((root / spec_path).read_bytes())
    require(old_intent is not None and new_intent is not None and old_intent.group() == new_intent.group(), 'frozen intent')
    require(digest(old_intent.group()) == data['frozen_intent_sha256'], 'frozen intent binding')
    expected_inputs = {
        '.config/dotnet-tools.json', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props',
        'global.json', 'module/hexalith.module-manifest.v2.json', 'scripts/test.ps1', 'README.md',
        'docs/architecture.md', 'docs/project-overview.md', 'docs/getting-started.md', 'docs/development-guide.md',
        'tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs',
        'tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs',
        '_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md',
        '_bmad-output/implementation-artifacts/spec-six-fitness-failures.md',
        '_bmad-output/implementation-artifacts/tests/six-fitness-failures-resumed-2026-11-09/verify-receipt.py',
        '_bmad-output/implementation-artifacts/tests/six-fitness-failures-resumed-2026-11-09/test-verify-receipt.py',
        'references/Hexalith.Tenants/Directory.Build.props', 'references/Hexalith.Tenants/Directory.Build.targets',
        'references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests/PackageGovernanceTests.cs',
    }
    require(set(data['post_verification_inputs']) == expected_inputs, 'input inventory')
    for name, expected_hash in data['post_verification_inputs'].items():
        require(digest((root / name).read_bytes()) == expected_hash, 'current input: ' + name)

    # This is a stamped historical packet. Preserve complete execution metadata,
    # including unsuccessful and intermediate runs, without inventing missing provenance.
    inventory_bytes = payloads['recorded-execution-inventory.json']
    require(digest(inventory_bytes) == 'f001c3d899291be52256d4b3cc43d4b5ca6d097760e52b4a2003dace1345dfef',
            'execution inventory anchor')
    inventory = json.loads(inventory_bytes)
    execution_keys = ['name', 'command', 'cwd', 'environment_overrides', 'cleared_environment_keys',
                      'exit_code', 'log', 'log_sha256', 'xml', 'xml_sha256']
    executions = [{key: run[key] for key in execution_keys if key in run} for run in data['runs']]
    require(executions == inventory['executions'], 'recorded execution inventory')
    for name, expected_hash in {
        'owner-message.txt': '39faedef758d411a012f7cc94cea7119a94d9d9697cf99dbef4b515c7cc9ae2c',
        'root-message.txt': '4e9a05bac6a50962f12c2019fbf77b90d151910d04bc22bd0f457db97ff24a2b',
    }.items():
        require(digest(payloads[name]) == expected_hash, 'validated commit message binding: ' + name)

    for name in ['parent-source-fitness-selection', 'projections-source-selection',
                 'owner-contracts-selection', 'owner-server-selection']:
        selection_run = runs[name]
        selected_props = json.loads(payloads[selection_run['log']])['Properties']
        require(selection_run['exit_code'] == 0 and selected_props['Configuration'] == 'Debug'
                and selected_props['TreatWarningsAsErrors'].lower() == 'true'
                and selected_props['HexalithEventStoreFromSource'] == 'true'
                and selected_props['HexalithSourceMappingMode'] == 'source', 'source selection: ' + name)
        project = Path(selection_run['command'][2]).stem
        expected_path = selection_run['cwd'] + f'/tests/{project}/bin/Debug/net10.0/{project}.dll'
        require(selected_props['TargetPath'] == expected_path, 'source selection target: ' + name)
        if 'HexalithSourceMappingHash' in selection_run['command'][4].split(':', 1)[1].split(','):
            require(selected_props.get('HexalithSourceMappingHash') == source_environment['HEXALITH_SOURCE_MAPPING_HASH'],
                    'source selection mapping: ' + name)

    require(data.get('followup_review') == inventory['followup_review'], 'second review metadata')
    require({name: data['archives'][name] for name in inventory['review_archives'] if name in data['archives']}
            == inventory['review_archives'], 'second review archive inventory')
    for name in data['followup_review']['layers']:
        require(payloads[name].strip(), 'empty second review report: ' + name)
    require(digest(payloads['second-review.diff']) == data['followup_review']['reviewed_diff_sha256']
            and digest(payloads['second-review-correction.diff']) == data['followup_review']['correction_diff_sha256'],
            'second review diff bindings')
    checks = json.loads(payloads[data['followup_review']['checks']])
    require([check['name'] for check in checks] == ['verify-normal', 'negative-normal', 'verify-optimized', 'negative-optimized']
            and all(check['exit_code'] == 0 for check in checks), 'second review checks')
    for mode in ['normal', 'optimized']:
        fixture_result = json.loads(payloads[f'second-review-negative-{mode}.log'])
        require(fixture_result['cases'] == len(fixture_result['results']) == data['followup_review']['fixture_cases_per_mode']
                and fixture_result['results'][0] == {'case': 'valid', 'result': 'accepted'}
                and all(result['result'] == 'rejected' for result in fixture_result['results'][1:]),
                'second review fixtures: ' + mode)
    # Raw execution hashes do not bind sanitized bytes. Anchor the exact retained
    # payloads as well, after semantic checks so negative fixtures keep their diagnostics.
    for name, tree in reports.items():
        failures = [{'name': test.get('name'), 'message': test.findtext('failure/message', '')}
                    for test in tree.findall(".//test[@result='Fail']")]
        skips = [{'name': test.get('name'), 'reason': test.findtext('reason')}
                 for test in tree.findall(".//test[@result='Skip']")]
        require(runs[name].get('failures') == failures, 'derived failures: ' + name)
        require(runs[name].get('skips') == skips, 'derived skips: ' + name)
    require(inventory_digest({name: item['sanitized_sha256'] for name, item in data['archives'].items()})
            == '6db5d3a7909039bb67de13b87b39cd0765fc08d7662e2fd0d5ca9cda8dc94636',
            'sanitized payload inventory anchor')
    require(inventory_digest(data['post_verification_inputs'])
            == '30b582fa16ae8dde7bdd320a4d00b4959051411c9fb7377c881f90b1ff99c100',
            'recorded input inventory anchor')
    verify_companion(folder, root)
    return {'evidence': 'verified', 'completion': 'in-review', 'preserved_files': len(preserved), 'owner_failures': 205}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--receipt', required=True, type=Path)
    parser.add_argument('--root', required=True, type=Path)
    args = parser.parse_args()
    try:
        print(json.dumps(verify(args.receipt.parent, args.root, json.loads(args.receipt.read_text())), sort_keys=True))
    except (ValueError, KeyError, OSError, ET.ParseError, subprocess.CalledProcessError) as error:
        parser.exit(1, str(error) + '\n')
