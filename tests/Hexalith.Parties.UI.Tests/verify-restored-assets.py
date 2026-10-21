"""Compile the actual UI asset resolver against isolated, locally restored fixture packages."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
TESTS = ROOT / 'tests/Hexalith.Parties.UI.Tests'
SOURCE = ET.parse(TESTS / 'Hexalith.Parties.UI.Tests.csproj')
METADATA = [item for item in SOURCE.findall('.//AssemblyMetadata')
            if item.get('Include') in {'NuGetPackageFolders', 'HexalithFrontComposerVersion'}]
assert len(METADATA) == 2
ASSET = Path('hexalith.frontcomposer.shell/1.0.0/staticwebassets/css/fc-shell.css')
CONTENT = '/* restored fixture stylesheet */'
CONFIGURATION = 'Release' if os.environ.get('CI') else 'Debug'


def invoke(directory, env, *args):
    result = subprocess.run(['dotnet', *args], cwd=directory, env=env,
                            capture_output=True, text=True, timeout=30)
    assert result.returncode == 0, result.stdout + result.stderr
    return result.stdout


with tempfile.TemporaryDirectory(prefix='parties-restored-assets-') as temporary:
    workspace = Path(temporary)
    feed = workspace / 'feed'
    feed.mkdir()
    with zipfile.ZipFile(feed / 'Hexalith.FrontComposer.Shell.1.0.0.nupkg', 'w') as package:
        package.writestr('Hexalith.FrontComposer.Shell.nuspec', '''<package>
          <metadata><id>Hexalith.FrontComposer.Shell</id><version>1.0.0</version>
          <authors>fixture</authors><description>Isolated restored-asset probe</description></metadata>
        </package>''')
        package.writestr('staticwebassets/css/fc-shell.css', CONTENT)

    for scenario in ['configuration', 'restore-property', 'environment', 'empty-environment', 'fallback']:
        directory = workspace / scenario
        directory.mkdir()
        (directory / 'global.json').write_bytes((ROOT / 'global.json').read_bytes())
        config = ET.Element('configuration')
        sources = ET.SubElement(config, 'packageSources')
        ET.SubElement(sources, 'clear')
        ET.SubElement(sources, 'add', key='local', value=str(feed))
        settings = ET.SubElement(config, 'config')
        ET.SubElement(settings, 'add', key='globalPackagesFolder', value='config-cache')
        fallback = workspace / 'configuration/config-cache'
        if scenario == 'fallback':
            folders = ET.SubElement(config, 'fallbackPackageFolders')
            ET.SubElement(folders, 'clear')
            ET.SubElement(folders, 'add', key='fixture', value=str(fallback))
        ET.ElementTree(config).write(directory / 'NuGet.Config', encoding='unicode')

        project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
        properties = ET.SubElement(project, 'PropertyGroup')
        for name, value in [('TargetFramework', 'net10.0'), ('OutputType', 'Exe'),
                            ('ImplicitUsings', 'enable'), ('Nullable', 'enable'),
                            ('HexalithFrontComposerVersion', '1.0.0')]:
            ET.SubElement(properties, name).text = value
        group = ET.SubElement(project, 'ItemGroup')
        for item in METADATA:
            group.append(item)
        ET.SubElement(group, 'Compile', Include=str(TESTS / 'RestoredFrontComposerAssets.cs'))
        ET.SubElement(group, 'PackageReference', Include='Hexalith.FrontComposer.Shell', Version='1.0.0')
        project_file = directory / 'probe.csproj'
        ET.ElementTree(project).write(project_file, encoding='unicode')
        (directory / 'Program.cs').write_text('''using System.Reflection;
using System.Text.Json;
using Hexalith.Parties.UI.Tests;
var assembly = Assembly.GetExecutingAssembly();
string asset = RestoredFrontComposerAssets.ResolveShellStylesheet(assembly);
Console.WriteLine(JsonSerializer.Serialize(new {
    Asset = asset, Content = File.ReadAllText(asset),
    Folders = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(item => item.Key == "NuGetPackageFolders").Value
}));
''')
        env = dict(os.environ)
        env.pop('NUGET_PACKAGES', None)
        expected = directory / 'config-cache'
        arguments = []
        if scenario == 'restore-property':
            expected = directory / 'property-cache'
            arguments.append(f'-p:RestorePackagesPath={expected}')
        elif scenario == 'environment':
            expected = directory / 'environment-cache'
            env['NUGET_PACKAGES'] = str(expected)
        elif scenario == 'empty-environment':
            env['NUGET_PACKAGES'] = ''
        elif scenario == 'fallback':
            expected = fallback

        invoke(directory, env, 'restore', str(project_file), '--configfile', str(directory / 'NuGet.Config'),
               '-p:NuGetAudit=false', *arguments)
        invoke(directory, env, 'build', str(project_file), '--no-restore', '-c', CONFIGURATION, *arguments)
        result = json.loads(invoke(directory, env, str(directory / f'bin/{CONFIGURATION}/net10.0/probe.dll')))
        assert Path(result['Asset']).resolve() == (expected / ASSET).resolve(), result
        assert result['Content'] == CONTENT, result
        assert expected.resolve() in {Path(folder).resolve() for folder in result['Folders'].split(';') if folder}, result
        if scenario == 'fallback':
            assert not (directory / 'config-cache' / ASSET).exists()
        print(f'PASS {scenario}: compiled metadata and actual stylesheet lookup match the restored package')
