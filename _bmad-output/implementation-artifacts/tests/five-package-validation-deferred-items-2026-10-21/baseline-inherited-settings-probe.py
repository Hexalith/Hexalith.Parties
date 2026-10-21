"""Reproduce baseline NuGet settings limitations retained outside DW-69."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import zipfile

scripts = Path('scripts').resolve()
sys.path.insert(0, str(scripts))
spec = importlib.util.spec_from_file_location('dw69_review_limits', scripts / 'validate-consumer-package-references.py')
module = importlib.util.module_from_spec(spec)
baseline = subprocess.check_output(['git', 'show', '1bed6c139f7699bc717182763c04099ea75abc5b:scripts/validate-consumer-package-references.py'], text=True)
exec(compile(baseline, str(scripts / 'validate-consumer-package-references.py'), 'exec'), module.__dict__)

with tempfile.TemporaryDirectory(prefix='dw69-review-') as directory:
    root = Path(directory)
    feed = root / 'feed'
    feed.mkdir()
    package_id = 'DW69.Review.InheritedOnly'
    with zipfile.ZipFile(feed / f'{package_id}.1.0.0.nupkg', 'w') as package:
        package.writestr(f'{package_id}.nuspec', f'<package><metadata><id>{package_id}</id><version>1.0.0</version><authors>Tests</authors><description>Offline review probe</description></metadata></package>')

    def restore(case, configuration, sources):
        (root / 'NuGet.Config').write_text(configuration)
        work = root / case
        work.mkdir()
        local = work / 'local'
        local.mkdir()
        module.write_nuget_config(work, [local], sources)
        consumer = work / 'consumer'
        consumer.mkdir()
        project = consumer / 'Consumer.csproj'
        project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include="{package_id}" Version="[1.0.0]" /></ItemGroup></Project>')
        environment = os.environ.copy()
        environment['NUGET_PACKAGES'] = str(work / 'cache')
        command = ['dotnet', 'restore', str(project), '-p:NuGetAudit=false', '--verbosity', 'minimal']
        result = subprocess.run(command, cwd=consumer, env=environment, capture_output=True, text=True, timeout=30)
        assets = json.loads((consumer / 'obj/project.assets.json').read_text())
        print(json.dumps({'case': case, 'command': command, 'exit_code': result.returncode, 'sources': list(assets['project']['restore']['sources']), 'packageFolders': list(assets['packageFolders']), 'log_codes': [entry.get('code') for entry in assets.get('logs', [])]}))
        return work, result.returncode, assets

    populated, exit_code, _ = restore('populate', '<configuration><packageSources><clear /></packageSources></configuration>', [str(feed)])
    if exit_code != 0:
        raise RuntimeError('Failed to seed the synthetic fallback cache')
    fallback = populated / 'cache'
    _, exit_code, assets = restore('fallback', f'<configuration><fallbackPackageFolders><add key="inherited" value="{fallback}" /></fallbackPackageFolders></configuration>', [])
    if exit_code != 0 or not any(Path(folder) == fallback for folder in assets['packageFolders']):
        raise RuntimeError('Fallback-folder limitation was not reproduced')
    _, exit_code, assets = restore('disabled', '<configuration><disabledPackageSources><add key="additional-source-1" value="true" /></disabledPackageSources></configuration>', [str(feed)])
    if exit_code != 1 or not any(entry.get('code') == 'NU1101' for entry in assets.get('logs', [])):
        raise RuntimeError('Disabled-source limitation was not reproduced')
