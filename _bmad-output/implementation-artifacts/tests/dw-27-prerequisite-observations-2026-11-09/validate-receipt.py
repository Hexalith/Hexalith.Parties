"""Validate this receipt's artifacts and current inputs without optimization-sensitive checks."""
from pathlib import Path
import argparse
import gzip
import hashlib
import json
import sys
import xml.etree.ElementTree as ET

INPUTS = (
    'Directory.Build.targets',
    'tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs',
    '_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md',
)
REQUIRED = [f'final-{mode}-{stage}' for mode in ('source', 'package')
            for stage in ('build', 'selection', 'prerequisites', 'full')]
REQUIRED += ['final-warning-guard', 'final-whitespace-crlf']


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def validate(receipt_path, root):
    receipt = json.loads(receipt_path.read_text())
    require(receipt['required_checks'] == REQUIRED, 'required check inventory')
    require(receipt['required_checks_passed'] is True, 'required checks not passed')
    checks = {check['name']: check for check in receipt['checks']}
    require(len(checks) == len(receipt['checks']), 'duplicate check names')
    require(all(name in checks and checks[name]['exit_code'] == 0 for name in REQUIRED),
            'missing or failing required check')
    raw = {}
    for entry in receipt['archives']:
        compressed = (receipt_path.parent / entry['file']).read_bytes()
        require(digest(compressed) == entry['compressed_sha256'], 'compressed hash: ' + entry['file'])
        data = gzip.decompress(compressed)
        require(len(data) == entry['raw_bytes'] and digest(data) == entry['raw_sha256'],
                'raw hash: ' + entry['file'])
        name = entry['file'].removesuffix('.gz')
        require(name not in raw, 'duplicate archive: ' + name)
        raw[name] = data
    historical = json.loads((root / receipt['skip_policy']['source_receipt']).read_text())
    old_skips = next(check['skips'] for check in historical['checks'] if check['name'] == 'source-full')
    allowed = {skip['name'] for skip in old_skips}
    require(len(allowed) == 13 and set(receipt['skip_policy']['allowed_full_suite_names']) == allowed,
            'runtime skip policy differs from historical inventory')
    for name, check in checks.items():
        require(check['log'] in raw and digest(raw[check['log']]) == check['sha256'],
                'check log hash: ' + name)
        if not name.endswith(('-prerequisites', '-full')):
            continue
        xml_name = name + '.xml'
        require(xml_name in raw and check.get('xml_sha256') == digest(raw[xml_name]),
                'required XML missing or changed: ' + name)
        tree = ET.fromstring(raw[xml_name])
        assemblies = tree.findall('.//assembly')
        tests = tree.findall('.//test')
        require(bool(assemblies) and bool(tests), 'empty XML report: ' + name)
        require([a.attrib for a in assemblies] == check['assemblies'], 'XML summary mismatch: ' + name)
        total = sum(int(a.get('total', '0')) for a in assemblies)
        require(total == len(tests), 'XML test inventory mismatch: ' + name)
        skips = [t for t in tests if t.get('result') == 'Skip']
        recorded_skips = [{'name': t.get('name'), 'reason': t.findtext('reason/message') or t.findtext('reason')}
                          for t in skips]
        require(recorded_skips == check['skips'], 'XML skip summary mismatch: ' + name)
        if name.startswith('final-'):
            require(all(int(a.get('failed', '-1')) == 0 and int(a.get('errors', '-1')) == 0
                        and int(a.get('not-run', '-1')) == 0 for a in assemblies), 'XML failures/errors: ' + name)
            require(all(t.get('result') in ('Pass', 'Skip') for t in tests), 'nonpassing XML test: ' + name)
            if name.endswith('-prerequisites'):
                require(not skips and all(int(a.get('skipped', '-1')) == 0 for a in assemblies),
                        'prerequisite skips: ' + name)
            else:
                require({t.get('name') for t in skips} == allowed and len(skips) == 13,
                        'unexpected full-suite skips: ' + name)
                require(all(skip['reason'] for skip in recorded_skips), 'missing runtime skip reason: ' + name)
    preparation = json.loads(raw['prepare-source.stdout.json'])
    require(preparation == receipt['source_preparation'], 'preparation record differs from raw output')
    mapping = preparation['buildPreparation']
    for mode in ('source', 'package'):
        selected = json.loads(raw[f'final-{mode}-selection.log'])['Properties']
        require(selected['Configuration'] == 'Debug' and selected['TreatWarningsAsErrors'] == 'true'
                and selected['HexalithEventStoreVersion'] == '4.1.0', 'selected build properties: ' + mode)
        if mode == 'source':
            require(selected['HexalithSourceMappingMode'] == 'source'
                    and selected['HexalithSourceMappingHash'] == mapping['mappingHash']
                    and selected['HexalithEventStoreFromSource'] == 'true', 'source selection semantics')
        else:
            require(not selected['HexalithSourceMappingMode'] and not selected['HexalithSourceMappingHash']
                    and selected['HexalithEventStoreFromSource'] in ('', 'false'), 'package selection semantics')
        for stage in ('build', 'selection', 'prerequisites', 'full'):
            expected_environment = mapping['environment'] if mode == 'source' else {}
            require(checks[f'final-{mode}-{stage}']['environment_overrides'] == expected_environment,
                    'emitted environment mismatch: ' + mode + '-' + stage)
        for stage in ('prerequisites', 'full'):
            executed = Path(checks[f'final-{mode}-{stage}']['command'][1])
            if not executed.is_absolute():
                executed = root / executed
            require(executed.resolve() == Path(selected['TargetPath']).resolve(), 'executed TargetPath: ' + mode)
        audit_name = 'post-review-' + mode + '-mode'
        require(checks[audit_name]['exit_code'] == 0, 'failed mode audit: ' + mode)
        audit = json.loads(raw[audit_name + '.log'])['Properties']
        require(audit['HexalithSourceMappingMode'] == selected['HexalithSourceMappingMode']
                and audit['HexalithSourceMappingHash'] == selected['HexalithSourceMappingHash'], 'mode audit drift')
        if mode == 'package':
            require(all(value in ('', 'false') for key, value in audit.items() if key.endswith('FromSource'))
                    and audit['UseHexalithProjectReferences'] in ('', 'false'), 'mixed package selectors')
    require(json.loads(raw['post-review-inherited-selectors.json']) == {}, 'inherited source selectors observed')
    bindings = receipt.get('post_verification_input_hashes', {})
    require(set(bindings) == set(INPUTS), 'final input binding inventory')
    for path, expected in bindings.items():
        require(digest((root / path).read_bytes()) == expected, 'final input hash: ' + path)
    initial = json.loads(raw['initial-tracked-hashes.json'])
    permitted = set(INPUTS) | {'_bmad-output/implementation-artifacts/spec-dw-27-invalid-page-failure.md'}
    for path, expected in initial.items():
        if path not in permitted:
            require(digest((root / path).read_bytes()) == expected, 'preservation failure: ' + path)
    return {'result': 'PASS', 'archives': len(raw), 'required_checks': len(REQUIRED),
            'input_bindings': len(bindings), 'historical_files_checked': len(initial) - len(permitted)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--receipt', type=Path, required=True)
    parser.add_argument('--root', type=Path, required=True)
    arguments = parser.parse_args()
    try:
        print(json.dumps(validate(arguments.receipt.resolve(), arguments.root.resolve()), sort_keys=True))
    except (OSError, ValueError, KeyError, TypeError, StopIteration, ET.ParseError) as error:
        print('FAIL: ' + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
