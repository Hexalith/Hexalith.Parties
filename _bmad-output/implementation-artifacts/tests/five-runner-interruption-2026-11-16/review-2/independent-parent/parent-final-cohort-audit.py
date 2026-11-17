"""Independently anchor and reread the current-layout executed/input bytes."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys
import zipfile

root = Path.cwd()
private = root / 'artifacts/runner-interruption-private-20261116'
active = private / 'review-2/current-layout'
phase = sys.argv[1]
assert phase in ('pre', 'post')
sha = lambda data: hashlib.sha256(data).hexdigest()
load = lambda path: json.loads(path.read_text())
clone = Path((active / 'private-root.txt').read_text().strip())
paths = load(active / 'cohort-paths.json')
expected = load(active / 'cohort-final.json')
assert set(paths) == set(expected) == {'WrapperSha256', 'PreparedSha256', 'ChildSha256'}
actual = {key: sha(Path(path).read_bytes()) for key, path in paths.items()}
assert actual == expected
assert Path(paths['ChildSha256']).name == 'Hexalith.Parties.Runtime.IntegrationTests.dll'
anchors = {name: sha((active / name).read_bytes()) for name in (
    'cohort-final.json', 'cohort-paths.json', 'source-final.json',
    'repository-freeze.json', 'reference-heads.json', 'prepared-closure.json')}
runtime_helpers = {str(path.relative_to(private)): sha(path.read_bytes())
                   for path in sorted(active.glob('*.py'))}
runtime_helpers['run-runtime.py'] = sha((private / 'run-runtime.py').read_bytes())
pre_path = private / 'parent-final-cohort-pre-audit.json'
if phase == 'post':
    pre = load(pre_path)
    assert pre['ManifestAnchors'] == anchors
    assert pre['ActualExecutedFiles'] == actual
    assert pre['RuntimeAndQueueHelpers'] == runtime_helpers

source = load(active / 'source-final.json')
for name, digest in source.items():
    assert sha((clone / name).read_bytes()) == digest, ('clone source', name)
    assert sha((root / name).read_bytes()) == digest, ('workspace source', name)
repository = load(active / 'repository-freeze.json')
assert repository['QualificationHead'] == '0f261ccd3b461102dcac35da2e074d15a195ebb4'
assert subprocess.check_output(['git', '-C', str(clone), 'rev-parse', 'HEAD'], text=True).strip() == repository['QualificationHead']
for name, digest in repository['Files'].items():
    assert sha((clone / name).read_bytes()) == digest, ('frozen input', name)
references = load(active / 'reference-heads.json')
assert len(references) == 10
for name, head in references.items():
    assert subprocess.check_output(['git', '-C', str(clone / name), 'rev-parse', 'HEAD'], text=True).strip() == head
    declaration = subprocess.check_output(['git', '-C', str(clone), 'ls-tree', 'HEAD', '--', name], text=True).split()[2]
    assert head == declaration, name

manifest = load(Path(paths['PreparedSha256']))
assert manifest['completed'] and manifest['configuration'] == 'Release'
assert manifest['sdkVersion'] == '5.1.0' and manifest['targetFramework'] == 'net11.0'
all_files = {}
archives = {}
binaries = {}
for project, entry in manifest['projects'].items():
    receipt_path = Path(entry['receipt'])
    assert sha(receipt_path.read_bytes()) == entry['sha256'], project
    receipt = load(receipt_path)
    assert receipt['completed'] and receipt['project'] == project
    assert receipt['configuration'] == manifest['configuration']
    assert receipt['targetFramework'] == manifest['targetFramework']
    assert receipt['familyVersions'] == manifest['familyVersions']
    assert set(receipt['runtimeInputs']).issubset(receipt['files'])
    for name, digest in receipt['files'].items():
        assert all_files.setdefault(name, digest) == digest
    for entry in receipt.get('sdkArchives', []):
        assert archives.setdefault(entry['path'], entry['sha256']) == entry['sha256']
    for entry in receipt.get('sdkBinaries', []):
        key = (entry['archive'], entry['entry'])
        assert binaries.setdefault(key, entry['sha256']) == entry['sha256']
        assert sha(Path(entry['path']).read_bytes()) == entry['sha256']
for name, digest in all_files.items():
    assert sha(Path(name).read_bytes()) == digest, ('prepared file', name)
for name, digest in archives.items():
    assert sha(Path(name).read_bytes()) == digest, ('package archive', name)
for (archive, entry), digest in binaries.items():
    with zipfile.ZipFile(archive) as package:
        assert sha(package.read(entry)) == digest, ('package binary', archive, entry)
closure = load(active / 'prepared-closure.json')
assert closure['Files'] == all_files
result = {
    'Phase': phase,
    'QualificationHead': repository['QualificationHead'],
    'ActualExecutedFiles': actual,
    'ManifestAnchors': anchors,
    'RuntimeAndQueueHelpers': runtime_helpers,
    'SourceFilesMatchCloneAndOriginal': len(source),
    'FrozenRepositoryFilesVerified': len(repository['Files']),
    'RootReferenceGitlinksVerified': len(references),
    'PreparedProjectReceiptsVerified': len(manifest['projects']),
    'PreparedFilesVerified': len(all_files),
    'PackageArchivesVerified': len(archives),
    'PackageBinaryEntriesVerified': len(binaries),
    'PublishedPackageAcceptanceClaimed': False,
}
(private / ('parent-final-cohort-' + phase + '-audit.json')).write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result, indent=2))
