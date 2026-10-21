"""Reproduce stale restored-family acceptance in the exact starting revision."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
from unittest.mock import patch

root = Path(__file__).resolve().parents[4]
source = subprocess.check_output(['git', 'show', '1bed6c139f7699bc717182763c04099ea75abc5b:scripts/validate-consumer-package-references.py'], cwd=root, text=True)
sys.path.insert(0, str(root / 'scripts'))
with tempfile.TemporaryDirectory() as temp:
    folder = Path(temp)
    script = folder / 'consumer.py'
    script.write_text(source)
    spec = importlib.util.spec_from_file_location('baseline_consumer', script)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    project = folder / 'PortalConsumer.csproj'
    assets = folder / 'obj/project.assets.json'
    assets.parent.mkdir()
    libraries = {f'{package_id}/1.0.0': {'type': 'package'} for package_id in module.PORTAL_REQUIRED_EVENT_STORE_PACKAGE_IDS}
    libraries['Hexalith.FrontComposer.Contracts/0.9.0'] = {'type': 'package'}
    libraries['Hexalith.Tenants.Client/0.9.0'] = {'type': 'package'}
    assets.write_text(json.dumps({'libraries': libraries}))
    calls = []
    with patch.object(module, 'run_dotnet', side_effect=lambda args, cwd: calls.append(args[0])):
        module.validate_consumer(project, '1.0.0', module.PORTAL_REQUIRED_EVENT_STORE_PACKAGE_IDS)
    if calls != ['restore', 'build']:
        raise RuntimeError(f'Baseline reproduction changed: {calls}')
    print(json.dumps({'baseline_accepts_stale_frontcomposer_and_tenants': True, 'calls': calls, 'libraries': libraries}, indent=2))
