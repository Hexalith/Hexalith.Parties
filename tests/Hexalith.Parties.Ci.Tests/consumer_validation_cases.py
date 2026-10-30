"""Offline executable regressions for package consumer validation."""

from __future__ import annotations

import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch
from xml.etree import ElementTree

SCRIPTS = Path(__file__).resolve().parents[2] / 'scripts'
sys.path.insert(0, str(SCRIPTS))
SPEC = importlib.util.spec_from_file_location('consumer_validation', SCRIPTS / 'validate-consumer-package-references.py')
CONSUMER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CONSUMER)


def write_package(feed: Path, package_id: str, filename_version: str, metadata_version: str) -> Path:
    """Write the minimal local package used by real NuGet restores."""
    feed.mkdir(parents=True, exist_ok=True)
    path = feed / f'{package_id}.{filename_version}.nupkg'
    with zipfile.ZipFile(path, 'w') as package:
        package.writestr(f'{package_id}.nuspec', f'''<package><metadata>
<id>{package_id}</id><version>{metadata_version}</version><authors>Tests</authors>
<description>Offline consumer regression fixture</description></metadata></package>''')
    return path


class SupportVersions(unittest.TestCase):
    """Require NuGet-equivalent support versions without admitting stale archives."""

    def test_normalized_versions_across_every_support_identity(self):
        package_ids = sorted(CONSUMER.COMMONS_SUPPORT_PACKAGE_IDS | CONSUMER.EVENT_STORE_SUPPORT_PACKAGE_IDS
                             | CONSUMER.FRONT_COMPOSER_SUPPORT_PACKAGE_IDS | CONSUMER.TENANTS_SUPPORT_PACKAGE_IDS)
        cases = [('6.5.4.0', '6.5.4', '6.5.4'), ('6.5.4+build.9', '6.5.4', '6.5.4+build.9'),
                 ('6.5', '6.5.0', '6.5.0'), ('6.5.4.2', '6.5.4.2', '6.5.4.2'),
                 ('6.5.4-preview.2+build.9', '6.5.4-preview.2', '6.5.4-preview.2+build.9')]
        for package_id in package_ids:
            for expected, filename, metadata in cases:
                with self.subTest(package_id=package_id, expected=expected), tempfile.TemporaryDirectory() as temp:
                    feed = Path(temp)
                    write_package(feed, package_id, filename, metadata)
                    CONSUMER.validate_support_packages(feed, frozenset({package_id}), expected)
                    with self.assertRaises(ValueError):
                        CONSUMER.validate_support_packages(feed, frozenset({package_id}), '6.5.3')
                    with self.assertRaises(ValueError):
                        CONSUMER.validate_support_packages(feed, frozenset({package_id}), '6.5.4-preview.3')
                    with self.assertRaises(ValueError):
                        CONSUMER.validate_support_packages(feed, frozenset({package_id}), '6.5.4.1')
                    original = next(feed.glob('*.nupkg'))
                    original.rename(feed / 'wrong-filename.nupkg')
                    with self.assertRaises(ValueError):
                        CONSUMER.validate_support_packages(feed, frozenset({package_id}), expected)

    def test_sdk_produced_normalized_archives(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            shutil.copyfile(SCRIPTS.parent / 'global.json', root / 'global.json')
            selected_sdk = subprocess.check_output(['dotnet', '--version'], cwd=root, text=True).strip()
            repository_sdk = subprocess.check_output(['dotnet', '--version'], cwd=SCRIPTS.parent, text=True).strip()
            self.assertEqual(selected_sdk, repository_sdk)
            print(f'Normalized package fixture SDK: {selected_sdk}')
            project = root / 'Support.csproj'
            # No compilation: NuGet's real Pack task supplies filenames and nuspec versions.
            project.write_text('''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>net10.0</TargetFramework><IncludeBuildOutput>false</IncludeBuildOutput>
<NoPackageAnalysis>true</NoPackageAnalysis><Authors>Tests</Authors>
</PropertyGroup><ItemGroup><None Include="payload.txt" Pack="true" PackagePath="content" /></ItemGroup></Project>''')
            (root / 'payload.txt').write_text('fixture')
            (root / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
            # Independent package fixtures retain Release and must not inherit sealed Debug source imports.
            environment = os.environ.copy()
            for key in ('CustomBeforeDirectoryBuildProps', 'CustomAfterMicrosoftCommonProps',
                        'CustomBeforeDirectoryBuildTargets', 'CustomAfterDirectoryBuildTargets',
                        'AfterMicrosoftNETSdkTargets', 'HEXALITH_SOURCE_MAPPING_HASH', 'Configuration'):
                environment.pop(key, None)
            package_id = 'Hexalith.FrontComposer.Contracts'
            for index, version in enumerate(('6.5.4.0', '6.5.4+build.9', '6.5', '6.5.4.2', '6.5.4-preview.2+build.9')):
                feed = root / f'feed-{index}'
                command = ['dotnet', 'pack', str(project), '-c', 'Release', '--output', str(feed),
                           '-p:NoBuild=true', '-p:NuGetAudit=false', f'-p:PackageId={package_id}', f'-p:PackageVersion={version}']
                result = subprocess.run(command, cwd=root, env=environment, capture_output=True, text=True, timeout=30)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                CONSUMER.validate_support_packages(feed, frozenset({package_id}), version)
                consumer = root / f'consumer-{index}'
                consumer.mkdir()
                CONSUMER.write_nuget_config(consumer, [feed], [])
                restored_project = consumer / 'Restored.csproj'
                restored_project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>net10.0</TargetFramework><RestorePackagesPath>{consumer / 'cache'}</RestorePackagesPath>
</PropertyGroup><ItemGroup><PackageReference Include="{package_id}" Version="[{version}]" /></ItemGroup></Project>''')
                result = subprocess.run(['dotnet', 'restore', str(restored_project), '-p:NuGetAudit=false'],
                                        cwd=consumer, env=environment, capture_output=True, text=True, timeout=30)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                CONSUMER.validate_family_assets(consumer / 'obj' / 'project.assets.json', version,
                                                frozenset({package_id}), 'FrontComposer')


class RestoredVersions(unittest.TestCase):
    """Qualify family selections before building either generated consumer."""

    def test_consumer_checks_each_selected_family_and_required_portal_packages(self):
        families = {'Hexalith.Commons': CONSUMER.COMMONS_SUPPORT_PACKAGE_IDS,
                    'Hexalith.EventStore': CONSUMER.PORTAL_REQUIRED_EVENT_STORE_PACKAGE_IDS,
                    'Hexalith.FrontComposer': CONSUMER.FRONT_COMPOSER_SUPPORT_PACKAGE_IDS,
                    'Hexalith.Tenants': CONSUMER.TENANTS_SUPPORT_PACKAGE_IDS}
        versions = {'Hexalith.Commons': '9.8.7', 'Hexalith.EventStore': '7.6.5', 'Hexalith.FrontComposer': '6.5.4', 'Hexalith.Tenants': '5.4.3'}
        with tempfile.TemporaryDirectory() as temp:
            project = Path(temp) / 'PortalConsumer.csproj'
            assets = project.parent / 'obj' / 'project.assets.json'
            assets.parent.mkdir()
            good = {f'{package_id}/{versions[family]}': {'type': 'package'}
                    for family, required in families.items() for package_id in required}
            for family in families:
                good[f'{family}.Transitive/{versions[family]}'] = {'type': 'package'}

            def validate(libraries, portal=True):
                assets.write_text(json.dumps({'libraries': libraries}))
                calls = []
                with patch.object(CONSUMER, 'run_dotnet', side_effect=lambda args, cwd: calls.append(args[0])):
                    try:
                        CONSUMER.validate_consumer(project, '7.6.5.0', families['Hexalith.EventStore'],
                                                  '6.5.4+build.9', '5.4.3', require_portal_packages=portal, commons_version='9.8.7')
                    except ValueError:
                        self.assertEqual(calls, ['restore'], 'Invalid selections must fail before build')
                        raise
                self.assertEqual(calls, ['restore', 'build'])

            validate(good)
            validate({key: value for key, value in good.items() if key.startswith(('Hexalith.EventStore.', 'Hexalith.Commons.'))}, portal=False)
            for family, required in families.items():
                for key in [key for key in good if key.startswith(family + '.')]:
                    for version in ('0.0.1', versions[family] + '.1', versions[family] + '-preview.1'):
                        bad = dict(good)
                        del bad[key]
                        bad[key.rsplit('/', 1)[0].lower() + '/' + version] = {'type': 'package'}
                        with self.subTest(identity=key, version=version), self.assertRaises(ValueError):
                            validate(bad)
                    bad = dict(good)
                    bad[key] = {'type': 'project'}
                    with self.subTest(identity=key, type='project'), self.assertRaises(ValueError):
                        validate(bad)
                for package_id in required:
                    bad = dict(good)
                    del bad[f'{package_id}/{versions[family]}']
                    with self.subTest(missing=package_id), self.assertRaises(ValueError):
                        validate(bad)
            # Client selection still rejects a mismatched optional family.
            for family in ('Hexalith.FrontComposer', 'Hexalith.Tenants'):
                bad = {key: value for key, value in good.items()
                       if key.startswith(('Hexalith.EventStore.', 'Hexalith.Commons.'))}
                bad[f'{family}.Transitive/0.0.1'] = {'type': 'package'}
                with self.subTest(client=family), self.assertRaisesRegex(ValueError, family):
                    validate(bad, portal=False)


class RestoreIsolation(unittest.TestCase):
    """Run NuGet against poisoned parent settings and fresh private caches."""

    def test_inherited_fallback_and_disabled_sources(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            shutil.copyfile(SCRIPTS.parent / 'global.json', root / 'global.json')
            feed = root / 'feed & packages'
            package_id = 'Deferred.Consumer.Isolation'
            write_package(feed, package_id, '1.0.0', '1.0.0')

            def restore(label, parent_config, explicit, inherited_fallback=None):
                (root / 'NuGet.Config').write_text(parent_config)
                work = root / label
                work.mkdir()
                local = work / 'local'
                local.mkdir()
                CONSUMER.write_nuget_config(work, [local], [str(feed)] if explicit else [])
                project_dir = work / 'consumer'
                project_dir.mkdir()
                project = project_dir / 'Consumer.csproj'
                project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup>
<PackageReference Include="{package_id}" Version="[1.0.0]" /></ItemGroup></Project>''')
                environment = os.environ.copy()
                environment.pop('NUGET_FALLBACK_PACKAGES', None)
                if inherited_fallback is not None:
                    environment['NUGET_FALLBACK_PACKAGES'] = str(inherited_fallback)
                environment['NUGET_PACKAGES'] = str(root / 'unrelated-inherited-cache')
                invoke = subprocess.run
                def bounded_run(*args, **kwargs):
                    return invoke(*args, **kwargs, capture_output=True, text=True, timeout=30)
                with patch.dict(os.environ, environment, clear=True), patch.object(CONSUMER.subprocess, 'run', side_effect=bounded_run):
                    try:
                        CONSUMER.run_dotnet(['restore', str(project), '-p:NuGetAudit=false'], project_dir)
                        exit_code = 0
                    except subprocess.CalledProcessError as error:
                        exit_code = error.returncode
                assets = json.loads((project_dir / 'obj' / 'project.assets.json').read_text())
                return work, exit_code, assets

            empty_parent = '<configuration><packageSources><clear /></packageSources></configuration>'
            seed, result, _ = restore('seed', empty_parent, True)
            self.assertEqual(result, 0)
            fallback = seed / '.nuget' / 'packages'
            config = ElementTree.Element('configuration')
            folders = ElementTree.SubElement(config, 'fallbackPackageFolders')
            ElementTree.SubElement(folders, 'add', {'key': 'parent-cache', 'value': str(fallback)})
            poisoned = ElementTree.tostring(config, encoding='unicode')
            _, result, assets = restore('fallback-denied', poisoned, False)
            self.assertNotEqual(result, 0, 'Inherited fallback satisfied a missing consumer package')
            self.assertNotIn(str(fallback), {str(Path(folder)) for folder in assets['packageFolders']})
            self.assertTrue(any(entry.get('code') == 'NU1101' for entry in assets.get('logs', [])), assets.get('logs'))
            _, result, _ = restore('fallback-explicit', poisoned, True)
            self.assertEqual(result, 0)
            _, result, assets = restore('environment-fallback-denied', empty_parent, False, fallback)
            self.assertNotEqual(result, 0, 'Inherited environment fallback satisfied a missing consumer package')
            self.assertNotIn(str(fallback), {str(Path(folder)) for folder in assets['packageFolders']})
            self.assertTrue(any(entry.get('code') == 'NU1101' for entry in assets.get('logs', [])), assets.get('logs'))
            _, result, _ = restore('environment-fallback-explicit', empty_parent, True, fallback)
            self.assertEqual(result, 0)
            disabled = '''<configuration><disabledPackageSources>
<add key="additional-source-1" value="true" /><add key="local-package-feed-1" value="true" />
</disabledPackageSources></configuration>'''
            _, result, assets = restore('disabled-explicit', disabled, True)
            self.assertEqual(result, 0)
            self.assertIn(f'{package_id}/1.0.0', assets['libraries'])
            self.assertEqual(set(assets['project']['restore']['sources']),
                             {str((root / 'disabled-explicit' / 'local').resolve()), str(feed.resolve())})


if __name__ == '__main__':
    unittest.main(verbosity=2)
