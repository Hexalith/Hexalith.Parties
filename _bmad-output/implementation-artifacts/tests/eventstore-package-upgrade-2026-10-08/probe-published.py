"""Inspect one actually published EventStore release from the public NuGet feed."""

import concurrent.futures
import datetime
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

version, expected_commit, output_directory, owner_directory, verifier_dll = sys.argv[1:]
output = Path(output_directory)
output.mkdir(parents=True, exist_ok=True)
for result_name in ['published-package-proof.json', 'partial-publication-observation.json']:
    (output / result_name).unlink(missing_ok=True)
owner = Path(owner_directory)
manifest = subprocess.check_output(['git', 'show', expected_commit + ':tools/release-packages.json'], cwd=owner)
packages = json.loads(manifest)['packages']


def inspect_package(package):
    package_id = package['id']
    identity = package_id.lower()
    url = f'https://api.nuget.org/v3-flatcontainer/{identity}/{version}/{identity}.{version}.nupkg'
    with urllib.request.urlopen(url, timeout=60) as response:
        content = response.read()
    with zipfile.ZipFile(io.BytesIO(content)) as archive:
        nuspec = ET.fromstring(archive.read(package_id + '.nuspec'))
        repository = nuspec.find('.//{*}repository')
        if repository is None or repository.get('commit') != expected_commit:
            raise ValueError(f'{package_id}: repository commit mismatch')
        if nuspec.find('.//{*}version').text != version:
            raise ValueError(f'{package_id}: release version mismatch')
        dependencies = [dict(dependency.attrib) for dependency in nuspec.findall('.//{*}dependency')]
        if package_id == 'Hexalith.EventStore.DomainService':
            dll = archive.read('lib/net10.0/Hexalith.EventStore.DomainService.dll')
            (output / 'Hexalith.EventStore.DomainService.dll').write_bytes(dll)
            matching = [dependency for dependency in dependencies if dependency['id'] == 'Hexalith.EventStore.ServiceDefaults']
            if len(matching) != 1 or matching[0]['version'] != version:
                raise ValueError(f'{package_id}: ServiceDefaults dependency mismatch')
            dll_hash = hashlib.sha256(dll).hexdigest()
        else:
            dll_hash = None
        signed = '.signature.p7s' in archive.namelist()
    return {
        'id': package_id,
        'version': version,
        'url': url,
        'nugetPage': f'https://www.nuget.org/packages/{package_id}/{version}',
        'repositoryCommit': expected_commit,
        'nupkgSha256': hashlib.sha256(content).hexdigest(),
        'hasNuGetSignature': signed,
        'dependencies': dependencies,
        'domainServiceDllSha256': dll_hash,
    }


with concurrent.futures.ThreadPoolExecutor(max_workers=7) as pool:
    futures = {pool.submit(inspect_package, package): package['id'] for package in packages}
    observations = []
    errors = []
    for future in concurrent.futures.as_completed(futures):
        try:
            observations.append(future.result())
        except Exception as error:
            errors.append({'id': futures[future], 'error': str(error)})
    if errors:
        (output / 'partial-publication-observation.json').write_text(json.dumps({'observedAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'packages': observations, 'errors': errors}, indent=2) + '\n')
        raise RuntimeError(json.dumps(errors))
    observations.sort(key=lambda package: package['id'])
if len(observations) != len(packages):
    raise ValueError('Release manifest package count mismatch')
for package in observations:
    for dependency in package['dependencies']:
        if dependency['id'].startswith('Hexalith.EventStore.'):
            if dependency['version'] != version:
                raise ValueError(f'{package["id"]}: family dependency mismatch: {dependency}')
subprocess.run(['git', 'merge-base', '--is-ancestor', 'c4d5455a3b79ca1ba0113a286cf2432b2cace7fb', expected_commit], cwd=owner, check=True)
api = json.loads(subprocess.check_output([
    'dotnet', verifier_dll,
    str(output / 'Hexalith.EventStore.DomainService.dll'),
], text=True))
result = {
    'observedAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'version': version,
    'repositoryCommit': expected_commit,
    'descendsFromApiIntroducingCommit': True,
    'releaseManifestSha256': hashlib.sha256(manifest).hexdigest(),
    'packages': observations,
    'publicApi': api,
}
(output / 'published-package-proof.json').write_text(json.dumps(result, indent=2) + '\n')
print(f'Verified {len(observations)} published packages at {version}, exact source {expected_commit}, and public generic sidecar API.')
