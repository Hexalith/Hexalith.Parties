"""Check retained public results, operative inputs and the five bounded dispositions."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
FINAL_SUITES = [('ui', 445), ('admin', 327), ('consumer', 85), ('primary-client', 345)]


def require(condition, label='evidence condition failed'):
    if not condition:
        raise RuntimeError(label)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def validate_final_commands(commands):
    builds = ['ui-build', 'admin-build', 'consumer-build', 'primary-client-build']
    tests = ['ui-full', 'admin-full', 'consumer-full', 'primary-client-tests']
    controls = ['browser-ui', 'browser-config', 'browser-typecheck', 'warning-guard', 'whitespace']
    for name in builds + tests + controls:
        command = commands[name + '.json']
        require(command['exit_code'] == 0, name + ': command failed')
        require(not command.get('capture_error') and not command.get('missing_result_xml'), name + ': capture failed')
        if name in tests:
            require(command.get('xunit_xml', {}).get('path') == name + '.xml', name + ': missing fresh result XML')


def validate_xunit(path, expected):
    root = ET.parse(path).getroot()
    assemblies = root.findall('assembly')
    require(len(assemblies) == 1, str(path) + ': expected one assembly')
    assembly = assemblies[0]
    for key in ['total', 'passed']:
        require(int(assembly.attrib[key]) == expected, str(path) + ': incorrect ' + key)
    for node in [root, assembly, *assembly.findall('collection')]:
        for key in ['errors', 'failed', 'skipped', 'not-run']:
            require(int(node.get(key, '0')) == 0, str(path) + ': nonzero ' + key)
    require(not root.findall('.//error') and not root.findall('.//failure'), str(path) + ': runner errors')
    cases = assembly.findall('.//test')
    require(len(cases) == expected, str(path) + ': missing actual cases')
    identities = [case.get('id') for case in cases]
    require(all(identities), str(path) + ': missing case identities')
    require(len(set(identities)) == expected, str(path) + ': duplicate case identities')
    require(all(case.get('result') == 'Pass' for case in cases), str(path) + ': nonpassing actual cases')


def validate_browser_xml(path, browser):
    expected = len(browser['cases'])
    root = ET.parse(path).getroot()
    require(int(root.get('tests', '-1')) == expected, 'native browser total mismatch')
    for node in [root, *root.findall('testsuite')]:
        for key in ['errors', 'failures', 'skipped']:
            require(int(node.get(key, '0')) == 0, 'native browser ' + key)
    for tag in ['failure', 'error', 'skipped']:
        require(not root.findall('.//' + tag), 'native browser ' + tag)
    require(len(root.findall('.//testcase')) == expected, 'native browser actual count mismatch')
    native = []
    for suite in root.findall('testsuite'):
        cases = suite.findall('testcase')
        require(int(suite.get('tests', '-1')) == len(cases), 'native suite count mismatch')
        for case in cases:
            native.append((case.get('classname'), case.get('name', '').split(' › ')[-1], suite.get('hostname')))
    retained = [(case['location']['file'], case['title'], case['projectName']) for case in browser['cases']]
    require(len(native) == expected and Counter(native) == Counter(retained), 'native browser case identities mismatch')


def verify():
    receipt = json.loads((HERE / 'implementation-verification.json').read_text())
    validate_final_commands(receipt['commands'])
    for filename, command in receipt['commands'].items():
        require(digest(HERE / command['log']) == command['log_sha256'], filename)
        if 'xunit_xml' in command:
            xml = command['xunit_xml']
            require(digest(HERE / xml['path']) == xml['sha256'], filename)
    for name, expected in FINAL_SUITES:
        filename = f'{name}-full.xml' if name != 'primary-client' else 'primary-client-tests.xml'
        validate_xunit(HERE / filename, expected)
    for name in ['ui', 'admin', 'consumer', 'primary-client']:
        command = receipt['commands'][name + '-build.json']
        log = (HERE / command['log']).read_text()
        require('0 Warning(s)' in log and '0 Error(s)' in log, name + ': build diagnostics')
    require(receipt['commands']['source-launch-blocker.json']['exit_code'] == 2)
    require('HXR015' in (HERE / 'source-launch-blocker.txt').read_text())
    identity = json.loads((HERE / 'package-source-binary-identity.json').read_text())
    for filename, expected in identity['inputs_sha256'].items():
        require(digest(ROOT / filename) == expected, filename)
    assets = json.loads((HERE / 'browser-assets.json').read_text())
    expected_assets = identity['packages']['hexalith.frontcomposer.shell']['assets']
    require(len(assets['observations']) == 6)
    for observation in assets['observations']:
        require(observation['observation'] == {'mode': 'package', 'version': '4.7.1', 'sourceRoot': None, 'assets': expected_assets})
    for package in identity['packages'].values():
        require(package['version'] == '4.7.1')
        require(package['repository']['commit'] == '5688c3eba98567133285b0f51843d9daa8f24f0b')
        require(len(package['copied_assemblies']) == 4)
        require(all(copy['sha256'] == package['dll_sha256'] for copy in package['copied_assemblies']))
    require(set(identity['implementation_assemblies']) == {'UI', 'AdminPortal', 'ConsumerPortal'})
    for name, implementation in identity['implementation_assemblies'].items():
        require(len(implementation['copies']) == (2 if name == 'UI' else 3), name + ': missing implementation copies')
        require(all(copy['sha256'] == implementation['sha256'] for copy in implementation['copies']), name + ': implementation DLL mismatch')
    browser = json.loads((HERE / 'browser-cases.json').read_text())
    require(browser['stats'] == {'total': 68, 'expected': 68, 'unexpected': 0, 'flaky': 0, 'skipped': 0, 'ok': True})
    require(len(browser['cases']) == 68 and len({case['testId'] for case in browser['cases']}) == 68)
    require(all(case['ok'] and len(case['results']) == 1 and case['results'][0]['status'] == 'passed'
                and case['results'][0]['retry'] == 0 for case in browser['cases']))
    validate_browser_xml(HERE / 'browser-ui.xml', browser)
    ledger = (ROOT / '_bmad-output/implementation-artifacts/deferred-work.md').read_text()
    for number in [91, 100, 101, 102, 112]:
        section = ledger.split(f'### DW-{number}:', 1)[1].split('\n### DW-', 1)[0]
        require(('status: open' if number == 100 else 'status: done 2026-10-28') in section)
        require('adoption_2026_10_28:' in section and 'five-published-frontcomposer-adoption-2026-10-28/README.md' in section)
    require(receipt['remaining_gates'] and receipt['source_qualification'].startswith('blocked: HXR015'))
    print('Verified 1,202 passing .NET cases, 68 package browser passes, six package asset comparisons, operative hashes and five bounded dispositions.')


if __name__ == '__main__':
    verify()
