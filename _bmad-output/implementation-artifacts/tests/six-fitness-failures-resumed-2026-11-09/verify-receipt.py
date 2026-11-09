"""Validate retained evidence without promoting any blocked qualification gate."""

import argparse
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
        assemblies = tree.findall('assembly')
        require(assemblies, 'empty report: ' + name)
        require([a.attrib for a in assemblies] == run['assemblies'], 'assembly attributes: ' + name)
        identities = []
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
        require(all(identities) and len(set(identities)) == len(identities), 'test identities: ' + name)
        reports[name] = tree

    mandatory = ['parent-source-fitness-tests', 'parent-source-full-socket-enabled',
                 'projections-source-tests', 'owner-server-socket-enabled', 'owner-patched-floor',
                 'final-parent-package-build-corrected', 'final-documentation-package-corrected', 'final-parent-package-full']
    require(data['required_successful_runs'] == mandatory, 'required run inventory')
    for name in mandatory:
        require(name in runs and runs[name]['exit_code'] == 0, 'required successful run: ' + name)
        if name != 'final-parent-package-build-corrected':
            require(name in reports, 'required test report: ' + name)
            require(not reports[name].findall(".//test[@result='Fail']"), 'required test failure: ' + name)
    for name in ['parent-source-fitness-tests', 'owner-patched-floor', 'final-documentation-package-corrected']:
        require(not reports[name].findall(".//test[@result='Skip']"), 'focused skip: ' + name)
    require(len(reports['owner-patched-floor'].findall('.//test')) == 48, 'floor case count')
    for entry in ['CollectPackageReferences', 'PrepareForBuild']:
        cases = [t for t in reports['owner-patched-floor'].findall('.//test')
                 if '.EventStorePackageFloorRunsThroughRestoreAndBuildAsync(' in t.get('name', '')
                 and entry in t.get('name', '')]
        require(len(cases) == 5 and all(t.get('result') == 'Pass' for t in cases), 'entry target cases: ' + entry)

    current = reports['final-parent-package-full'].findall('.//test')
    expected = {
        'ExternalRuntimeGraphsMatchTheIndependentlyEvaluatedPartiesPackageSelection': (2, ['Debug', 'Release']),
        'CommittedSelectionObservations_PreservePackageGraphAndImmutableConsumptionRefusals': (1, []),
        'CommittedSourceSelections_RecordOverridesAndRefuseInvalidImmutableConsumption': (2, ['McpCli', 'Platform']),
        'MaintainedTechnologyTablesMatchCentralPackageCatalog': (1, []),
    }
    for method, (count, values) in expected.items():
        cases = [t for t in current if re.search(r'\.' + re.escape(method) + r'(?:\(|$)', t.get('name', ''))]
        require(len(cases) == count and all(t.get('result') == 'Pass' for t in cases), 'six controls: ' + method)
        require(all(sum(v in t.get('name', '') for t in cases) == 1 for v in values), 'case parameters: ' + method)

    baseline = reports['owner-contracts-clean-environment-baseline']
    patched = reports['owner-patched-contracts-full']
    failures = lambda tree: {t.get('name') for t in tree.findall(".//test[@result='Fail']")}
    require(failures(baseline) == failures(patched) and len(failures(patched)) == 205, 'owner failure baseline')
    require(runs['owner-contracts-clean-environment-baseline']['exit_code'] == 1
            and runs['owner-patched-contracts-full']['exit_code'] == 1, 'owner blocked exits')
    require(runs['final-parent-source-build']['exit_code'] == 1
            and b'HXW005' in payloads[runs['final-parent-source-build']['log']], 'source blocker')
    for name in ['parent-source-fitness-build', 'projections-source-build', 'owner-patched-contracts-build',
                 'final-parent-package-build-corrected']:
        text = payloads[runs[name]['log']].decode()
        require(runs[name]['exit_code'] == 0 and re.search(r'\b0 Warning\(s\)', text)
                and re.search(r'\b0 Error\(s\)', text), 'build summary: ' + name)

    props = json.loads(payloads[runs['final-parent-package-selection']['log']])['Properties']
    require(props['Configuration'] == 'Debug' and props['TreatWarningsAsErrors'].lower() == 'true', 'build policy')
    require(props['HexalithEventStoreFromSource'] in ['', 'false'] and not props['HexalithSourceMappingMode'], 'package mode')
    require(props['HexalithEventStoreVersion'] == '4.1.0', 'package selection')

    for stem in ['blind-hunter', 'edge-case', 'verification-gap']:
        for suffix in ['original', 'followup']:
            require(stem + '-' + suffix + '.md' in payloads, 'review report: ' + stem)
    for name in ['delivery.diff', 'owner-patch.diff', 'root-patch.diff']:
        require(name in payloads and digest(payloads[name]) == data['reviewed_diff_sha256'][name], 'review diff: ' + name)
    mutation = json.loads(payloads['hook-mutation.json'])
    require(len(mutation['controls']) == 6 and all(c['valid'] for c in mutation['controls']), 'hook mutation')
    for control in mutation['controls']:
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
    for name, expected_hash in data['post_verification_inputs'].items():
        require(digest((root / name).read_bytes()) == expected_hash, 'current input: ' + name)
    return {'evidence': 'verified', 'completion': 'in-review', 'preserved_files': len(preserved), 'owner_failures': 205}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--receipt', required=True, type=Path)
    parser.add_argument('--root', required=True, type=Path)
    args = parser.parse_args()
    try:
        print(json.dumps(verify(args.receipt.parent, args.root), sort_keys=True))
    except (ValueError, KeyError, OSError, ET.ParseError, subprocess.CalledProcessError) as error:
        parser.exit(1, str(error) + '\n')
