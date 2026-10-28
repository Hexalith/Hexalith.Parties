"""Retain package identities, native Playwright results, binaries and source hashes."""
import base64
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile

def require(condition, label='evidence condition failed'):
    if not condition:
        raise RuntimeError(label)
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
PRIVATE = Path(tempfile.gettempdir()) / 'five-published-frontcomposer-private-20261028'

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def normalized(value):
    return str(value).replace(str(ROOT), '${WORKSPACE}').replace(str(Path.home()), '${HOME}').replace('/tmp/', '${TMP}/')

def write(name, value):
    (HERE / name).write_text(normalized(json.dumps(value, indent=2)) + '\n')

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, text=True).strip()


def implementation_input_names():
    # HEAD includes both the index and working tree; explicit inputs survive a
    # fully staged tree and a later commit, while newly added helpers are included.
    required = [
        'Directory.Build.targets', 'Directory.Packages.props',
        'src/Hexalith.Parties.UI/Program.cs', 'src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj',
        'src/Hexalith.Parties.UI/Services/OptimisticReconcile.cs',
        'src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor.cs',
        'src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor',
        'tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs',
        'tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyPrivacyPageTests.cs',
        'tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyProfilePageTests.cs',
        'tests/Hexalith.Parties.UI.Tests/DataFreshnessIndicatorTests.cs',
        'tests/Hexalith.Parties.UI.Tests/StatusLiveRegionTests.cs',
        'tests/Hexalith.Parties.UI.Tests/OptimisticReconcileTests.cs',
        'tests/Hexalith.Parties.UI.Tests/MainLayoutAccessibilityTests.cs',
        'tests/e2e/helpers/frontcomposer-assets.ts', 'tests/e2e/helpers/consumer-download-cancellation.ts',
        'tests/e2e/specs/admin-parties-list.spec.ts', 'tests/e2e/specs/consumer-portal-routes.spec.ts',
        'tests/e2e/specs/parties-accessibility.spec.ts', 'tests/e2e/specs/shared-feedback-source.spec.ts',
        'tests/e2e/playwright.config.ts',
    ]
    for name in required:
        require((ROOT / name).is_file(), name + ': required implementation input is missing')
    changed = git('diff', '--name-only', 'HEAD').splitlines()
    new = git('ls-files', '--others', '--exclude-standard', '--', 'src', 'tests').splitlines()
    return sorted(set(required + changed + new))


def collect():
    PRIVATE.mkdir(exist_ok=True)
    for file in HERE.glob('*.txt'):
        content = file.read_bytes()
        if str(ROOT).encode() in content or str(Path.home()).encode() in content or b'/tmp/' in content:
            (PRIVATE / ('original-' + file.name)).write_bytes(content)
            file.write_text(normalized(content.decode()))
    report = ROOT / 'tests/e2e/playwright-report/index.html'
    shutil.copyfile(report, PRIVATE / 'browser-report.html')
    encoded = re.search('id="playwrightReportBase64">data:application/zip;base64,([^<]+)', report.read_text()).group(1)
    archive = zipfile.ZipFile(io.BytesIO(base64.b64decode(encoded)))
    summary = json.loads(archive.read('report.json'))
    cases = []
    assets = []
    identities = []
    for name in archive.namelist():
        if name == 'report.json':
            continue
        for test in json.loads(archive.read(name))['tests']:
            cases.append({key: test[key] for key in ('testId', 'title', 'projectName', 'location', 'outcome', 'ok')} | {'results': [{key: result[key] for key in ('status', 'retry', 'duration')} for result in test['results']]})
            for result in test['results']:
                for attachment in result.get('attachments', []):
                    if attachment['name'] == 'frontcomposer-assets.json':
                        assets.append({'test': test['title'], 'observation': json.loads(attachment['body'])})
                    if attachment['name'] == 'browser-identity.json':
                        identities.append(json.loads((report.parent / attachment['path']).read_text()))
    require(summary['stats'] == {'total': 68, 'expected': 68, 'unexpected': 0, 'flaky': 0, 'skipped': 0, 'ok': True}, 'evidence condition failed')
    require(len(cases) == 68 and len(assets) == 6, 'evidence condition failed')
    require(all((case['ok'] and case['results'] == [{**case['results'][0], 'status': 'passed', 'retry': 0}] for case in cases)), 'evidence condition failed')
    require(identities and identities[0]['server']['mode'] == 'managed', 'evidence condition failed')
    require(identities[0]['server']['environment']['Parties__FrontComposer__UseSharedUiPrimitives'] == 'true', 'evidence condition failed')
    write('browser-cases.json', {'stats': summary['stats'], 'native_html_sha256': digest(report), 'cases': cases})
    write('browser-assets.json', {'observations': assets, 'runtime': identities})
    xml = ROOT / 'tests/e2e/test-results/junit.xml'
    shutil.copyfile(xml, PRIVATE / 'browser-junit.xml')
    (HERE / 'browser-ui.xml').write_text(normalized(xml.read_text()))
    metadata = (ROOT / 'src/Hexalith.Parties.UI/bin/Debug/net10.0/Hexalith.Parties.UI.package-metadata.txt').read_text().splitlines()
    require(metadata[0] == '4.7.1', 'evidence condition failed')
    packages = {}
    for suffix, assembly in [('shell', 'Shell'), ('contracts.ui', 'Contracts.UI')]:
        package = 'hexalith.frontcomposer.' + suffix
        folder = next((Path(item) / package / '4.7.1' for item in metadata[1:] if (Path(item) / package / '4.7.1').exists()))
        nuspec = folder / f'{package}.nuspec'
        ns = {'n': 'http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd'}
        repository = ET.parse(nuspec).find('.//n:repository', ns).attrib
        require(repository['commit'] == '5688c3eba98567133285b0f51843d9daa8f24f0b', 'evidence condition failed')
        library = folder / f'lib/net10.0/Hexalith.FrontComposer.{assembly}.dll'
        copies = []
        for project in ['src/Hexalith.Parties.UI', 'tests/Hexalith.Parties.UI.Tests', 'tests/Hexalith.Parties.AdminPortal.Tests', 'tests/Hexalith.Parties.ConsumerPortal.Tests']:
            copy = ROOT / project / f'bin/Debug/net10.0/{library.name}'
            require(copy.exists() and digest(copy) == digest(library), 'evidence condition failed')
            copies.append({'path': str(copy.relative_to(ROOT)), 'sha256': digest(copy)})
        packages[package] = {'version': '4.7.1', 'repository': repository, 'nupkg_sha256': digest(folder / f'{package}.4.7.1.nupkg'), 'nuspec_sha256': digest(nuspec), 'dll_sha256': digest(library), 'copied_assemblies': copies}
        if suffix == 'shell':
            for observation in assets:
                require(observation['observation']['mode'] == 'package', 'evidence condition failed')
                for asset in observation['observation']['assets']:
                    installed = folder / asset['selectedPath'].split('/4.7.1/', 1)[1]
                    require(digest(installed) == asset['sha256'] and installed.stat().st_size == asset['bytes'], 'evidence condition failed')
            packages[package]['assets'] = assets[0]['observation']['assets']
    sources = implementation_input_names()
    inputs = {name: digest(ROOT / name) for name in sources if (ROOT / name).is_file() and (name.startswith(('src/', 'tests/')) or name in ('Directory.Build.targets', 'Directory.Packages.props'))}
    inputs.update({name: digest(ROOT / name) for name in ['.gitattributes', 'Directory.Packages.props', 'references/Hexalith.Builds/Props/Directory.Packages.props', 'references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/wwwroot/js/fc-focus.js', 'references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/wwwroot/js/fc-keyboard.js', 'references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/wwwroot/js/fc-download.js', 'references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts.UI/Freshness/FcOptimisticReconcileRequest.cs']})
    identities = {'parties': git('rev-parse', 'HEAD')}
    for module in ['Builds', 'FrontComposer', 'EventStore', 'Tenants']:
        identities[module] = {'checkout': git('-C', f'references/Hexalith.{module}', 'rev-parse', 'HEAD'), 'root_gitlink': git('rev-parse', f'HEAD:references/Hexalith.{module}')}
    implementation_assemblies = {}
    for name, test_project in [('UI', 'UI'), ('AdminPortal', 'AdminPortal'), ('ConsumerPortal', 'ConsumerPortal')]:
        library = ROOT / f'src/Hexalith.Parties.{name}/bin/Debug/net10.0/Hexalith.Parties.{name}.dll'
        outputs = ['src/Hexalith.Parties.UI', 'tests/Hexalith.Parties.UI.Tests']
        if name != 'UI':
            outputs.append(f'tests/Hexalith.Parties.{test_project}.Tests')
        copies = []
        for output in outputs:
            copy = ROOT / output / f'bin/Debug/net10.0/{library.name}'
            require(copy.exists() and digest(copy) == digest(library), name + ': exercised implementation mismatch')
            copies.append({'path': str(copy.relative_to(ROOT)), 'sha256': digest(copy)})
        implementation_assemblies[name] = {'source_output': str(library.relative_to(ROOT)), 'sha256': digest(library), 'copies': copies}
    write('package-source-binary-identity.json', {'repositories': identities, 'packages': packages, 'implementation_assemblies': implementation_assemblies, 'inputs_sha256': inputs, 'test_assemblies': {name: digest(ROOT / f'tests/Hexalith.Parties.{name}.Tests/bin/Debug/net10.0/Hexalith.Parties.{name}.Tests.dll') for name in ['UI', 'AdminPortal', 'ConsumerPortal']}, 'project_assets_sha256': {name: digest(ROOT / f'tests/Hexalith.Parties.{name}.Tests/obj/project.assets.json') for name in ['UI', 'AdminPortal', 'ConsumerPortal']}})
    receipts = {}
    for file in HERE.glob('*.json'):
        value = json.loads(file.read_text())
        if 'command' in value and 'exit_code' in value:
            receipts[file.name] = value
    write('implementation-verification.json', {'commands': receipts, 'browser_stats': summary['stats'], 'final_suites': {name: ET.parse(HERE / f'{name}-full.xml').getroot().find('assembly').attrib for name in ['ui', 'admin', 'consumer']}, 'source_qualification': 'blocked: HXR015; no mapped source build/browser pass is claimed', 'remaining_gates': ['DW-100 fixed-reconciler package publication/adoption', 'whole G4', 'G4-A picker', 'Story 8.9', 'fresh source launch prerequisite']})
    print('Collected 68 passing browser cases, six installed/served asset comparisons, both 4.7.1 packages and all final test receipts.')


if __name__ == '__main__':
    collect()
