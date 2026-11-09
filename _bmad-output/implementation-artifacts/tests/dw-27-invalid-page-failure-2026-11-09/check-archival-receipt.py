"""Exercise the archival verifier on isolated pre-adoption fixture bytes."""
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile

root = Path.cwd()
relative = Path('_bmad-output/implementation-artifacts/tests/dw-27-invalid-page-failure-2026-10-17')
source = root / relative
module_spec = importlib.util.spec_from_file_location('dw27_verifier', source / 'verify-compatibility-and-restoration.py')
module = importlib.util.module_from_spec(module_spec)
module_spec.loader.exec_module(module)
validation = json.loads((source / 'validation.json').read_text())
manifest = json.loads((source / 'consumer-patch-manifest.json').read_text())
protected = [source / name for name in ('validation.json', 'consumer-patch-manifest.json', 'parties-consumer-adoption.patch', 'compatibility-and-restoration-check.json')]
before = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in protected}
with tempfile.TemporaryDirectory(prefix='dw27-historical-fixture-') as temporary:
    fixture = Path(temporary)
    evidence = fixture / relative
    evidence.mkdir(parents=True)
    for name in ('validation.json', 'consumer-patch-manifest.json', 'parties-consumer-adoption.patch'):
        (evidence / name).write_bytes((source / name).read_bytes())
    original_baseline_bytes = module.baseline_bytes
    def archived_bytes(repository, revision, path):
        owner = root / 'references/Hexalith.EventStore' if repository.name == 'Hexalith.EventStore' else root
        return original_baseline_bytes(owner, revision, path)
    module.baseline_bytes = archived_bytes
    module.__file__ = str(evidence / 'verify-compatibility-and-restoration.py')
    for path in manifest['files']:
        target = fixture / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(original_baseline_bytes(root, validation['parties_baseline'], path))
    for path in ('tests/Hexalith.EventStore.Server.Tests/ErrorHandling/QueryProblemDetailsTests.cs',
                 'src/Hexalith.EventStore.Server/Queries/QueryExecutionFailedException.cs'):
        target = fixture / 'references/Hexalith.EventStore' / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(original_baseline_bytes(root / 'references/Hexalith.EventStore', validation['eventstore_baseline'], path))
    receipt = fixture / 'custom-receipt.json'
    module.verify((evidence / 'consumer-patch-manifest.json').resolve(), receipt.resolve())
    result = json.loads(receipt.read_text())
    if result['command'][-2:] != ['--receipt', str(receipt.resolve())]:
        raise RuntimeError('Custom receipt destination was not recorded')
    print('PASS: custom receipt destination recorded; historical reconstruction checks passed')
after = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in protected}
if before != after:
    raise RuntimeError('Historical patch, manifest or receipt changed')
print('PASS: retained patch, manifest and historical receipts unchanged')
