"""One-off DW-105 qualification using Builds' existing bounded smoke helpers."""
import hashlib
import importlib.util
import json
import pathlib
import re
import subprocess
import sys
import time
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[4]
EVIDENCE = pathlib.Path(__file__).resolve().parent
ARCHIVE = pathlib.Path('/tmp/parties-dw105-20261022-ui.tar.gz')
# Immutable identities observed for this batch's SDK-published archive.
ARCHIVE_SHA256 = 'd478e22e1cdf206054ae747d25048995884825ce0984fea05420343ea8e93119'
IMAGE_ID = 'sha256:e36065ef6aeb2f8d8fa1f8244f1d6eebc06f04f9c837dc9b4eb077b14b7cbf80'
IMAGE = 'parties-dw105-20261022-local:0.0.0-local'
CONTAINER = f'parties-dw105-20261022-owned-ui-{uuid.uuid4().hex}'
HELPER = ROOT / 'references/Hexalith.Builds/Github/publish-containers/smoke_container_platforms.py'
commands = []
summary = {'attempt_id': uuid.uuid4().hex, 'phase': 'initialization', 'result': 'failure',
           'scope': 'Fresh local SDK Release/package image; linux/amd64 Development startup only. No registry publication, historical release repair, arm64, production authentication or GitHub approval execution.',
           'archive': str(ARCHIVE), 'image': IMAGE, 'container': CONTAINER, 'http': [],
           'load_attempted': False, 'startup_attempted': False}
load_attempted = False
startup_attempted = False
accepted = False
smoke = None


def write_receipt():
    summary['commands'] = commands
    temporary = EVIDENCE / f"ui-container-qualification-{summary['attempt_id']}.tmp"
    temporary.write_text(json.dumps(summary, indent=2) + '\n')
    temporary.replace(EVIDENCE / 'ui-container-qualification.json')


def phase(name):
    summary['phase'] = name
    write_receipt()


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def run(command, timeout=120):
    started = time.monotonic()
    result = original_run(command, timeout=timeout)
    commands.append({'command': command, 'timeout_seconds': timeout,
                     'exit_code': result.returncode if result is not None else None,
                     'stdout': result.stdout if result is not None else '',
                     'stderr': result.stderr if result is not None else '',
                     'elapsed_seconds': round(time.monotonic() - started, 3)})
    return result


def inspect(kind, name):
    result = run(['docker', kind, 'inspect', name])
    require(result is not None, f'{kind} inspection did not complete; ownership is unknown.')
    if result.returncode == 0:
        items = json.loads(result.stdout)
        require(isinstance(items, list) and len(items) == 1 and isinstance(items[0], dict),
                f'{kind} inspection returned invalid evidence.')
        return items[0]
    missing = {f'Error response from daemon: No such {kind}: {name}', f'Error: No such {kind}: {name}'}
    require(result.returncode == 1 and result.stdout.strip() == '[]' and result.stderr.strip() in missing,
            f'{kind} absence could not be proved; refusing mutation.')
    return None


def cleanup_container():
    observed = inspect('container', CONTAINER)
    if observed is not None:
        require(observed.get('Image') == IMAGE_ID, 'The container is no longer bound to the owned image.')
        require(smoke._remove_container(CONTAINER) == 'pass', 'Owned container removal failed.')
    summary['container_cleanup'] = 'pass'


def cleanup_image():
    tagged = inspect('image', IMAGE)
    require(tagged is None or tagged.get('Id') == IMAGE_ID, 'The batch tag was rebound; refusing removal.')
    observed = inspect('image', IMAGE_ID)
    if observed is not None:
        require(observed.get('Id') == IMAGE_ID
                and set(observed.get('RepoDigests') or []) <= {f"{IMAGE.rsplit(':', 1)[0]}@{IMAGE_ID}"}
                and set(observed.get('RepoTags') or []) <= {IMAGE},
                'Owned image has unexpected bindings; refusing removal.')
        # Unforced removal by immutable ID cannot delete an image substituted at the tag.
        # --no-prune also preserves unrelated dangling parent images.
        result = run(['docker', 'image', 'rm', '--no-prune', IMAGE_ID])
        require(result is not None and result.returncode == 0, 'Owned image removal failed.')
    summary['image_cleanup'] = 'pass'


# Replace any earlier receipt with this attempt's failure state before fallible setup.
write_receipt()
try:
    sys.path.insert(0, str(HELPER.parent))
    spec = importlib.util.spec_from_file_location('shared_smoke', HELPER)
    smoke = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(smoke)
    original_run = smoke._run_or_none
    smoke._run_or_none = run
    summary['source_sha'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True, timeout=30).strip()
    summary['shared_helper_sha256'] = hashlib.sha256(HELPER.read_bytes()).hexdigest()
    with ARCHIVE.open('rb') as archive:
        summary['archive_sha256'] = hashlib.file_digest(archive, 'sha256').hexdigest()
    require(summary['archive_sha256'] == ARCHIVE_SHA256, 'The batch archive identity changed.')
    phase('preflight')
    # Both the tag and immutable image must be explicitly absent before import.
    for kind, name in [('container', CONTAINER), ('image', IMAGE), ('image', IMAGE_ID)]:
        require(inspect(kind, name) is None, f'Refusing to reuse or remove a pre-existing {kind}.')
    load_attempted = summary['load_attempted'] = True
    phase('load')
    result = run(['docker', 'load', '--input', str(ARCHIVE)])
    require(result is not None and result.returncode == 0, 'SDK archive load did not complete successfully.')
    phase('image-validation')
    observed = inspect('image', IMAGE)
    require(observed is not None and observed.get('Id') == IMAGE_ID, 'Imported image identity does not match the batch.')
    summary['image_id'] = observed['Id']
    summary['architecture'] = observed.get('Architecture')
    summary['os'] = observed.get('Os')
    summary['labels'] = observed.get('Config', {}).get('Labels', {})
    require(observed.get('Architecture') == 'amd64' and observed.get('Os') == 'linux', 'Imported image platform is incorrect.')
    require(summary['labels'].get('org.opencontainers.image.revision') == summary['source_sha'], 'Imported image source is incorrect.')
    startup_attempted = summary['startup_attempted'] = True
    phase('startup')
    require(smoke._start_container('linux/amd64', CONTAINER, summary['image_id']) == 'pass', 'Container startup failed.')
    logs = []
    outcome, attempts = smoke._poll_liveness(CONTAINER, 60, 1, logs)
    summary['startup'] = {'outcome': outcome, 'attempts': attempts, 'log': logs}
    require(outcome == 'pass', 'Container liveness failed.')
    port = run(['docker', 'port', CONTAINER, '8080/tcp'])
    require(port is not None and port.returncode == 0, 'Published container port is unavailable.')
    address = re.search(r'127\.0\.0\.1:(\d+)', port.stdout)
    require(address is not None, 'Published container port is invalid.')
    for route in ['/alive', '/health', '/ready', '/_content/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.bundle.scp.css']:
        phase(f'http:{route}')
        result = smoke._liveness_result(f'http://127.0.0.1:{address.group(1)}{route}')
        status = result.stdout.strip() if result is not None else ''
        summary['http'].append({'route': route, 'status': status})
        require(result is not None and result.returncode == 0 and status == '200', f'HTTP qualification failed for {route}.')
    phase('final-state')
    summary['final_state'] = smoke._container_state(CONTAINER)
    require(summary['final_state'] == 'running|0', 'Container did not remain running.')
    accepted = True
except Exception as error:
    summary['failure_phase'] = summary['phase']
    summary['error'] = str(error).replace(str(ROOT), '<workspace>')
finally:
    for attempted, name, cleanup in [(startup_attempted, 'container', cleanup_container), (load_attempted, 'image', cleanup_image)]:
        if attempted:
            try:
                if 'failure_phase' not in summary:
                    phase(f'cleanup-{name}')
                cleanup()
            except Exception as error:
                summary.setdefault('failure_phase', f'cleanup-{name}')
                summary[f'{name}_cleanup'] = 'failure'
                summary.setdefault('cleanup_errors', []).append(str(error).replace(str(ROOT), '<workspace>'))
    summary['result'] = 'pass' if accepted and not summary.get('cleanup_errors') else 'failure'
    summary['phase'] = 'complete' if summary['result'] == 'pass' else summary.get('failure_phase', summary['phase'])
    write_receipt()
    print(json.dumps({key: value for key, value in summary.items() if key != 'commands'}, indent=2))
if summary['result'] != 'pass':
    raise SystemExit(1)
