"""Retain the freshly executed post-review cohort in a private publication candidate."""
from pathlib import Path
import ast
import argparse
import atexit
import os
import subprocess
import sys
import tempfile
import gzip
import hashlib
import json
import re
import shutil
import xml.etree.ElementTree as ET

root = Path.cwd()
base = root / 'artifacts/runner-interruption-private-20261116'
private = base / 'review-final'
active = private / 'current-layout'
candidate = private / 'public-candidate'
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--completion-authorized', action='store_true', help='Use only after the parent explicitly signals completed commands, post-readback and scratch closure')
parser.add_argument('--check-ready', action='store_true', help='Read prerequisites without creating a candidate or completion claims')
arguments = parser.parse_args()
current_root = (active / 'private-root.txt').read_text().strip()
current_job = (active / 'job-path.txt').read_text().strip()

def readiness():
    missing = []
    def require(value, reason):
        if not value: missing.append(reason)
    def read_json(path):
        if not path.exists():
            missing.append('Missing '+str(path.relative_to(base)))
            return {}
        return json.loads(path.read_text())
    completion = read_json(active/'handoff-completion.json')
    for key in ['AllFinalCommandsCompleted','AllFinalChildrenAndHelpersAwaited','ParentPostReadbackCompleted','ScratchRemoved']:
        require(completion.get(key) is True, 'Completion '+key+' is not true')
    require(completion.get('OuterDriverExitCode') == 0, 'Owning driver has not exited0')
    require(completion.get('OwningWorkspaceExistsAfterExit') is False, 'Owned workspace absence is not recorded')
    require(not Path(current_root).parent.exists(), 'Owning workspace still exists')
    require(completion.get('DriverCompletion',{}).get('AllJobsAwaited') is True and completion.get('DriverCompletion',{}).get('ContainmentFailures') is False, 'Driver completion is not certified')
    selected = read_json(active/'current-cleanup-selection.json').get('Command')
    labels = ['38-release-contracts','39-release-unit','56-configuration-components-build','57-configuration-component-controls','58-final-release-runtime-preparation','59-freeze-final-cohort','60-validate-final-cohort','61-package-process-controls',selected,'63-consumer-unit','64-consumer-runtime','65-startup-ordering-controls','67-configuration-controls','68-admitted-queue-controls','69-persisted-state-refusal','70-final-matrix','71-ordinary-restart','72-controlled-cancellation','73-live-shutdown','74-final-post-runtime-validation']
    for label in labels:
        if label is None:continue
        value = read_json(active/'queue'/(label+'.result'))
        require(value.get('ExitCode') == 0 and value.get('DescendantsReaped') is True, 'Incomplete/unqualified command '+label)
    pre = read_json(base/'parent-post-review-cohort-pre-audit.json')
    post = read_json(base/'parent-post-review-cohort-post-audit.json')
    require(pre.get('Phase') == 'pre' and post.get('Phase') == 'post','Actual parent pre/post readbacks are missing')
    for key in ['ActualExecutedFiles','ManifestAnchors','RuntimeAndQueueHelpers','QualificationHead','SourceFilesMatchCloneAndOriginal','FrozenRepositoryFilesVerified','RootReferenceGitlinksVerified','PreparedProjectReceiptsVerified','PreparedFilesVerified','PackageArchivesVerified','PackageBinaryEntriesVerified']:
        require(key in pre and pre.get(key) == post.get(key), 'Parent pre/post mismatch '+key)
    require(post.get('ActualExecutedFiles') == read_json(active/'cohort-final.json') == completion.get('FinalCohort'), 'Completion/readback/cohort disagree')
    require(read_json(base/'parent-post-review-physical-audit.json').get('Passed') is True, 'Final independent physical closure is missing')
    for name in ['ordinary','cancellation','live']:
        require(read_json(base/('parent-post-review-'+name+'-audit.json')).get('Passed') is True,'Parent continuation readback is missing: '+name)
    historical=read_json(base/'parent-pre-final-historical-packet-freeze.json')
    require(set(historical.get('OnlyRootMetadataMayBeVersioned',[]))=={'README.md','artifact-index.json.gz','SHA256SUMS.gz'},'Historical root metadata versioning policy is missing')
    require(len(historical.get('ExistingPublicFiles',{}))==3409,'Expected complete historical public freeze is missing')
    older=root/'_bmad-output/implementation-artifacts/tests/five-runner-interruption-2026-11-16'
    for name,digest in historical.get('ExistingPublicFiles',{}).items():
        path=older/name
        require(path.is_file() and hashlib.sha256(path.read_bytes()).hexdigest()==digest,'Existing public bytes changed before candidate export: '+name)
    return missing

missing = readiness()
if arguments.check_ready:
    print(json.dumps({'ToolingParsed':True,'CompletionReady':not missing,'CandidateWritten':False,'Pending':missing}))
    sys.exit(0)
if not arguments.completion_authorized:
    raise SystemExit('HOLD: explicit parent completion authorization is required; no candidate was written')
if missing:
    raise SystemExit('HOLD: final completion/readback/closure prerequisites are incomplete; no candidate was written: '+json.dumps(missing))
assert not candidate.exists(), 'Retain an existing candidate rather than overwriting it'
assert 'TMPDIR' in os.environ, 'Run this disposable packaging job with run-with-temp'
temporary = tempfile.TemporaryDirectory(prefix='post-review-packet-', dir=os.environ['TMPDIR'])
atexit.register(temporary.cleanup)
public = Path(temporary.name)
job = (base / 'review-2/job-path.txt').read_text().strip()
secrets = set()

# Reuse the already qualified exporter transformations without running its
# historical top-level export. The retained candidate includes this dependency.
previous = base / 'review-2/export-review.py'
tree = ast.parse(previous.read_text())
definitions = [node for node in tree.body if isinstance(node, ast.FunctionDef)]
exec(compile(ast.Module(body=definitions, type_ignores=[]), str(previous), 'exec'))
for path in base.rglob('*.json'):
    try:
        discover(json.loads(path.read_text()))
    except (OSError, ValueError):
        pass

load = lambda path: json.loads(path.read_text())
for path in sorted(active.glob('*.json')):
    if path.name.endswith(('.command.json', '.supervisor.json', '-private.json')):
        continue
    detail(public / 'current-layout' / path.name, load(path))
for path in sorted((active / 'prepared-receipts').glob('*.json')):
    detail(public / 'current-layout/prepared-receipts' / path.name, load(path))
for path in sorted(active.glob('*.xml')):
    target = public / 'current-layout' / (path.name + '.gz')
    target.write_bytes(gzip.compress(normalize(path.read_text()).encode(), mtime=0))
for path in sorted(active.glob('*.log')):
    target = public / 'current-layout' / (path.name + '.gz')
    target.write_bytes(gzip.compress(normalize(path.read_text()).encode(), mtime=0))
for path in sorted(active.glob('*.py')):
    shutil.copyfile(path, public / 'current-layout' / path.name)
shutil.copyfile(base/'run-runtime.py',public/'current-layout/run-runtime.py')
for path in sorted((active / 'early-controls-package-final').glob('*.json')):
    detail(public / 'current-layout/early-controls-package-final' / path.name, load(path))
for path in sorted((active / 'pre-signal-control-retry/early-controls-package-final').glob('*.json')):
    detail(public / 'current-layout/pre-signal-control-retry/early-controls-package-final' / path.name, load(path))

selected = ['phase.json', 'startup-failure.json', 'ownership.json', 'executed-identities.json',
            'source-authority-custody.json', 'endpoint-binding.json', 'runtime.owner-component.json',
            'runtime.provisioning.json', 'incomplete-replacement-quiescence.json', 'producer-restarts.json',
            'runtime.authority.json', 'runtime.provisioning.teardown.json',
            'runtime.provisioning.survivor.teardown.json', 'authoritative-persistence.json',
            'catalog-generation-controls.json', 'authentication-negative-controls.json',
            'domain-service-acl-negative-controls.json']
lanes = ['package-persisted-state-refusal', 'package-final-matrix', 'package-ordinary-restart',
         'package-controlled-cancellation', 'package-live-shutdown']
for lane in lanes:
    folder = active / lane
    for path in sorted(folder.glob('*.json')):
        if not path.name.endswith('-private.json'):
            detail(public / 'current-layout' / lane / path.name, load(path))
    for path in sorted(folder.glob('*.xml')):
        target = public / 'current-layout' / lane / (path.name + '.gz')
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(gzip.compress(normalize(path.read_text()).encode(), mtime=0))
    for path in sorted(folder.glob('*.log')):
        detail(public / 'current-layout' / lane / (path.name + '.sha256.json'),
               {'Sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'Bytes': path.stat().st_size})
    for run in sorted(folder.glob('runner-interruption-*')):
        if not (run / 'result.json').exists():
            continue
        phase = load(run / 'result.json')['Phase']
        for name in ['result.json', 'checkpoint.json', 'custody.json', 'external-custody.json', 'configuration.json']:
            detail(public / 'current-layout' / lane / (phase + '-' + name), load(run / name))
    for run in sorted(folder.glob('parties-five-memory-runtime-*')):
        for name in selected:
            if (run / name).exists():
                detail(public / 'current-layout' / lane / run.name / name, load(run / name))
        for path in sorted(run.glob('*.xml')):
            target = public / 'current-layout' / lane / run.name / (path.name + '.gz')
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(gzip.compress(normalize(path.read_text()).encode(), mtime=0))
        for path in sorted((run / 'producer-restart').glob('*.ack.json')):
            detail(public / 'current-layout' / lane / run.name / 'coordination' / path.name, load(path))

source = load(active / 'source-final.json')
cohort = load(active / 'cohort-final.json')
qualification_head = load(active / 'overlay-metadata.json')['QualificationHead']
readable(public / 'source.json', {'Authority': 'Fresh post-review execution; earlier review-2 remains historical',
         'Files': source, 'Cohort': cohort, 'ChildAssembly': 'Hexalith.Parties.Runtime.IntegrationTests.dll',
         'QualificationHead': qualification_head})
readable(public / 'cohort.json', {'Files': cohort})
for name, expected in source.items():
    raw = (active / 'source-final' / name).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == expected
    target = public / 'executed-source' / (name + '.gz')
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(gzip.compress(raw, mtime=0))

commands = []
accepted_labels = {'38-release-contracts','39-release-unit','56-configuration-components-build','57-configuration-component-controls','58-final-release-runtime-preparation','59-freeze-final-cohort','60-validate-final-cohort','61-package-process-controls','62-final-controls','63-consumer-unit','64-consumer-runtime','65-startup-ordering-controls','67-configuration-controls','68-admitted-queue-controls','69-persisted-state-refusal','70-final-matrix','71-ordinary-restart','72-controlled-cancellation','73-live-shutdown','74-final-post-runtime-validation'}
for path in sorted(active.glob('*.command.json')):
    label = path.name.removesuffix('.command.json')
    commands.append({'Label': label, **load(path), 'Result': load(active / 'queue' / (label + '.result')),
                     'CurrentAcceptanceRole': 'Final compatible Release prerequisite/runtime' if label in accepted_labels else None,
                     'HistoricalOrBuildOnly': label not in accepted_labels})
readable(public / 'commands-current-layout.json', commands)

def counts(path):
    assemblies = ET.parse(path).findall('.//assembly')
    result = {key: sum(int(item.get(key, 0)) for item in assemblies)
              for key in ['total', 'passed', 'failed', 'skipped', 'errors', 'not-run']}
    assert result['total'] and result['total'] == result['passed']
    assert not any(result[key] for key in ['failed', 'skipped', 'errors', 'not-run'])
    return result

rows = []
prefix = 'current-layout/package-final-matrix/'
for path in sorted((active / 'package-final-matrix').glob('runner-interruption-*/result.json')):
    result = load(path)
    checkpoint = load(path.with_name('checkpoint.json'))
    phase = result['Phase']
    rows.append({'Phase': phase, 'Xml': prefix + 'tests.xml.gz',
                 'Result': prefix + phase + '-result.json.gz',
                 'Checkpoint': prefix + phase + '-checkpoint.json.gz',
                 'Configuration': prefix + phase + '-configuration.json.gz',
                 'ExternalCustody': prefix + phase + '-external-custody.json.gz',
                 'RuntimePrefix': prefix + 'parties-five-memory-runtime-' + checkpoint['Topology']['Id']})
assert len(rows) == 6
readable(public / 'matrix.json', {'Cases': rows, 'SameUnchangedCohort': cohort,
         'Suite': {'Xml': prefix + 'tests.xml.gz', 'Counts': counts(active / 'package-final-matrix/tests.xml'), 'PassedAsAggregate': True}})
verification = {'CanonicalBaseline': 'a575021fc97a8eda664711089270b60f5e570ded',
                'QualificationHead': qualification_head,
                'Configuration': 'Release', 'PublishedPackageQualification': False,
                'ReviewFindingsFixed': ['R3-E1', 'R3-E2', 'bounded-stopped-state-verification-prerequisite'], 'Lanes': {}}
cleanup_selection = load(active / 'current-cleanup-selection.json')
for name, label in [('package-process-controls', '61-package-process-controls'), ('focused-cleanup', cleanup_selection['Command']),
                    ('focused-consumer-unit', '63-consumer-unit'), ('focused-consumer-runtime', '64-consumer-runtime'),
                    ('startup-ordering', '65-startup-ordering-controls'), ('configuration', '67-configuration-controls')]:
    verification['Lanes'][name] = {'Xml': 'current-layout/' + label + '.xml.gz',
                                 'Counts': counts(active / (label + '.xml'))}
for name, lane in [('persisted-state-refusal', lanes[0]), ('ordinary-wrapper', lanes[2]),
                   ('coordination-cancellation', lanes[3]), ('live-shutdown', lanes[4])]:
    verification['Lanes'][name] = {'Xml': 'current-layout/' + lane + '/tests.xml.gz',
                                 'Counts': counts(active / lane / 'tests.xml')}
children = list((active / lanes[2]).glob('parties-five-memory-runtime-*/runtime.xml'))
assert len(children) == 1
verification['Lanes']['ordinary-child'] = {'Xml': 'current-layout/' + children[0].relative_to(active).as_posix() + '.gz',
                                           'Counts': counts(children[0])}
verification['QueueControlCommand'] = '68-admitted-queue-controls'
verification['CleanupSelection'] = 'current-layout/current-cleanup-selection.json.gz'
verification['RetainedFocusedFailure'] = 'historical-prerequisites/signal-control-retry-diagnosis.json.gz'
verification['QueueAdmission'] = 'current-layout/timeout-admission-control.json.gz'
verification['QueueControls'] = {'Passed': 5, 'AdmissionReceipt': verification['QueueAdmission']}
verification['ConfigurationOnlyControls'] = {'Xml': 'current-layout/67-configuration-controls.xml.gz'}
verification['StartupAdmissionControls'] = {'Xml': 'current-layout/65-startup-ordering-controls.xml.gz'}
verification['IsolatedLocalClone'] = verification['RootReferenceGitlinksFrozen'] = True
completion = load(active / 'handoff-completion.json')
assert completion['AllFinalCommandsCompleted'] and completion['AllFinalChildrenAndHelpersAwaited']
assert completion['ScratchRemoved'] and completion['OuterDriverExitCode'] == 0
verification['ImplementationComplete'] = verification['AllJobsAwaited'] = verification['ScratchRemoved'] = True
verification['CompletionReceipt'] = 'current-layout/handoff-completion.json.gz'
verification['PostRuntimeInputValidation'] = {'CommandLabel': '74-final-post-runtime-validation'}
verification['FalseClosureMutationRejected'] = False
verification['FalseClosureAndWrongSessionControls'] = 'packet-mutation-controls.json.gz'
current_methods = []
for label in ['63-consumer-unit', '64-consumer-runtime']:
    current_methods.extend(test.attrib['name'] for test in ET.parse(active / (label + '.xml')).findall('.//test'))
assert len(current_methods) == 60
detail(public / 'current-layout/consumer-method-set.json', {'Methods': sorted(current_methods)})
shutil.copyfile(root / '_bmad-output/implementation-artifacts/tests/five-runner-interruption-2026-11-16/review-2/50-final-consumer.xml.gz',
                public / 'historical-consumer-selection.xml.gz')
verification['ExternalGatesRemainOpen'] = ['publication/adoption', 'historical production inventory',
    'older-writer exclusion', 'deployment/workload/key/trust custody', 'owner acceptance']
verification['ExplicitFallbackProfile'] = load(active / 'explicit-fallback-profile.json')
verification['CurrentDefaultDependencyAdoptionQualified'] = False
verification['CurrentSourceHostSeparate'] = True
verification['ExplicitProfileInvocationOnly'] = True
verification['HistoricalContext'] = 'historical-context/original-sdk-custody-limit.json.gz'
readable(public / 'verification.json', verification)
physical = load(base / 'parent-post-review-physical-audit.json')
assert physical['Passed']
physical['Closed'] = physical['Passed']
physical['LiveCapturedLifetimes'] = [value for value in physical['RemainingExactLifetimesIncludingZombies'] if value['State'] not in ['Z', 'X']]
physical['UnreapedCapturedZombies'] = [value for value in physical['RemainingExactLifetimesIncludingZombies'] if value['State'] in ['Z', 'X']]
physical['LateMarkerBoundPids'] = physical['LateMarkerBoundLifetimes']
readable(public / 'closure.json', physical)
(public / 'closure.json.gz').write_bytes(gzip.compress((public / 'closure.json').read_bytes(), mtime=0))
(public / 'closure.json').unlink()
for name in ['parent-post-review-cohort-pre-audit.json', 'parent-post-review-cohort-post-audit.json',
             'parent-post-review-physical-audit.json', 'parent-post-review-matrix-audit.json',
             'parent-post-review-state-refusal-audit.json', 'parent-final-patch-focused-audit.json',
             'parent-post-review-ordinary-audit.json','parent-post-review-cancellation-audit.json',
             'parent-post-review-live-audit.json','parent-post-review-intent-preservation.json']:
    detail(public / 'independent-parent' / name, load(base / name))
if (base/'parent-post-review-continuation-audit.json').exists():
    detail(public/'independent-parent/parent-post-review-continuation-audit.json',load(base/'parent-post-review-continuation-audit.json'))
for name in ['parent-post-review-cohort-audit.py', 'parent-post-review-physical-audit.py',
             'parent-post-review-continuation-audit.py', 'parent-post-review-matrix-audit.py']:
    target = public / 'independent-parent' / name
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(base / name, target)
shutil.copyfile(previous, public / 'historical-export-transforms.py')
retired_parent = base / 'review-final-before-signal-control-fix'
retired = retired_parent / 'current-layout'
for name in ['unqualified-retirement-audit.json', 'signal-control-retry-diagnosis.json', 'independent-parent-pre-audit.json']:
    detail(public / 'historical-prerequisites' / name, load(retired / name))
detail(public / 'historical-prerequisites/retired-job-completion.json', load(retired_parent / 'retired-job-completion.json'))
prior_source = load(retired / 'source-final.json')
readable(public / 'historical-prerequisites/source.json', {'Qualified': False, 'Files': prior_source, 'Cohort': load(retired / 'cohort-final.json')})
for name, digest in prior_source.items():
    raw = (retired / 'source-final' / name).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == digest
    target = public / 'historical-prerequisites/executed-source' / (name + '.gz')
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(gzip.compress(raw, mtime=0))
for label in ['62-final-controls', '68b-final-controls-retry']:
    path = retired / (label + '.xml')
    (public / 'historical-prerequisites' / (path.name + '.gz')).write_bytes(gzip.compress(normalize(path.read_text()).encode(), mtime=0))
    detail(public / 'historical-prerequisites' / (label + '.result.json'), load(retired / 'queue' / (label + '.result')))
native = active / 'native-d0-attempt'
for path in sorted((native / 'records').glob('*.json')):
    detail(public / 'native-d0-attempt/records' / path.name, load(path))
for path in sorted(native.glob('*.json')):
    detail(public / 'native-d0-attempt' / path.name, load(path))
for path in sorted((native/'records').glob('*.log')):
    detail(public/'native-d0-attempt/records'/(path.name+'.sha256.json'), {'Sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'Bytes':path.stat().st_size})
for path in sorted((native/'queue').glob('*')):
    if path.suffix in ['.json','.result']:
        detail(public/'native-d0-attempt/queue'/path.name,load(path))
for path in sorted((native/'executed-helpers').glob('*.py')):
    target=public/'native-d0-attempt/executed-helpers'/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(path.read_bytes(),mtime=0))
raw_native_runner=(base/'run-runtime.py').read_bytes()
assert hashlib.sha256(raw_native_runner).hexdigest()==load(native/'parent-current-native-post-audit.json')['RuntimeAndQueueHelpers']['run-runtime.py']
(public/'native-d0-attempt/executed-helpers/run-runtime.py.gz').write_bytes(gzip.compress(raw_native_runner,mtime=0))
for path in sorted((native/'early-controls-native-final').glob('*.json')):
    detail(public/'native-d0-attempt/early-controls-native-final'/path.name,load(path))
for path in sorted((native / 'records').glob('*.xml')):
    target = public / 'native-d0-attempt/records' / (path.name + '.gz')
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(gzip.compress(normalize(path.read_text()).encode(), mtime=0))
native_source = load(native / 'records/source-final.json')
for name, digest in native_source.items():
    raw = (native / 'executed-source' / name).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == digest
    target = public / 'native-d0-attempt/executed-source' / (name + '.gz')
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(gzip.compress(raw, mtime=0))
native_lane = native / 'native-persisted-state-refusal'
for path in sorted(native_lane.glob('*.json')):
    if not path.name.endswith('-private.json'):
        detail(public / 'native-d0-attempt/runtime' / path.name, load(path))
for path in sorted(native_lane.glob('*.xml')):
    target = public / 'native-d0-attempt/runtime' / (path.name + '.gz')
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(gzip.compress(normalize(path.read_text()).encode(), mtime=0))
for path in sorted(native_lane.glob('*.log')):
    detail(public/'native-d0-attempt/runtime'/(path.name+'.sha256.json'),{'Sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'Bytes':path.stat().st_size})
for run in native_lane.glob('runner-interruption-*'):
    for path in run.glob('*.json'):
        detail(public / 'native-d0-attempt/runtime' / run.name / path.name, load(path))
for run in native_lane.glob('parties-five-memory-runtime-*'):
    for name in selected:
        if (run / name).exists():
            detail(public / 'native-d0-attempt/runtime' / run.name / name, load(run / name))
for archive_name in ['release-default-tenants-attempt', 'pre-manifest-gated-controls']:
    for path in sorted((active / archive_name).rglob('*')):
        if not path.is_file():
            continue
        target = public / archive_name / path.relative_to(active / archive_name)
        if path.suffix in ['.json', '.result']:
            detail(target, load(path))
        elif path.suffix in ['.log', '.xml']:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.with_name(target.name + '.gz').write_bytes(gzip.compress(normalize(path.read_text()).encode(), mtime=0))
shutil.copyfile(Path(__file__), public / Path(__file__).name)
indexer = (base / 'review-2/index-packet.py').read_text().replace('corrected acceptance is in review-2 indexes', 'current acceptance is in review-final indexes')
(public / 'index-packet.py').write_text(indexer)
shutil.copyfile(base / 'review-2/test-packet-verifier.py', public / 'test-packet-verifier.py')
shutil.copyfile(base / 'parent-post-review-verify.py', public / 'verify-review.py')
shutil.copyfile(base/'parent-generate-post-review-verifier.py',public/'generate-verifier.py')
history = public/'historical-context'
history.mkdir()
previous_public=root/'_bmad-output/implementation-artifacts/tests/five-runner-interruption-2026-11-16/review-1'
for original,target in [('failed-focused-closure-accounting.json.gz','original-sdk-custody-limit.json.gz'),('present-state.json.gz','original-sdk-present-state.json.gz')]:
    shutil.copyfile(previous_public/original,history/target)
historical_freeze=load(base/'parent-pre-final-historical-packet-freeze.json')
detail(public/'independent-parent/parent-pre-final-historical-packet-freeze.json',historical_freeze)
older_root=previous_public.parent
archive=public/'historical-root'
archive.mkdir()
for name in historical_freeze['OnlyRootMetadataMayBeVersioned']:
    raw=(older_root/name).read_bytes()
    assert hashlib.sha256(raw).hexdigest()==historical_freeze['ExistingPublicFiles'][name]
    (archive/name).write_bytes(raw)
readable(public/'historical-preservation.json',{'ExistingPublicFiles':historical_freeze['ExistingPublicFiles'],'OnlyRootMetadataMayBeVersioned':historical_freeze['OnlyRootMetadataMayBeVersioned'],'NonMetadataFiles':3406,'OriginalRootMetadataArchive':'historical-root/','AllExistingFilesMatchedBeforeExport':True})
detail(public/'packaging-diagnostics/first-positive-baseline-refusal.json',load(private/'packaging-first-refusal/refusal.json'))
for path in sorted((private/'packaging-first-refusal').glob('*.py')):
    target=public/'packaging-diagnostics/before-volume-schema-fix'/(path.name+'.gz')
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_bytes(gzip.compress(path.read_bytes(),mtime=0))
readable(public/'history.json', {
    'CurrentAuthority':'Only source.json/cohort.json/matrix.json/verification.json select this final compatible cohort',
    'EarlierPublicEvidenceBytesModified':False,
    'ActiveRootMetadataMayBeVersionedOnlyWithExactHistoricalCopies':historical_freeze['OnlyRootMetadataMayBeVersioned'],
    'EarlierPublicPackets':['../review-2/README.md','../review-1/README.md','../README.md'],
    'UnqualifiedAttempts':[
        {'Path':'historical-prerequisites/','Outcome':'Both complete101-row suites fail the original pre-helper STOP assertion (100pass/1fail); no runtime admitted; original source/cohort and retired job closure retained'},
        {'Path':'native-d0-attempt/','Outcome':'Current-d0 source/Debug builds and focused controls pass, then actual native runtime fails before checkpoint/mutation/SIGINT; writer-lease cancellation and IServiceProvider disposal failure; three executed hashes match but41/105880 inputs/outputs drift; exact emergency closure is unqualified'},
        {'Path':'release-default-tenants-attempt/','Outcome':'NU1109 for current default Tenants8.0.1 requiring SDK5.1.1 against the compatible comparison SDK5.1.0; no current-default adoption'},
        {'Path':'pre-manifest-gated-controls/','Outcome':'Code3 before corrected compatible preparation completed; no tests/runtime executed'},
        {'Path':'../review-2/','Outcome':'All older0f/source variants, native MSB4018/readiness attempts, ancestry/adopted-zombie/configuration/proxy failures and earlier selected outcomes remain historical for these corrected d0 bytes'},
        {'Path':'historical-context/original-sdk-custody-limit.json.gz','Outcome':'Original47/49/51 SDK worker custody/closure remains unestablished; present-state observations grant no recovery authority'},
        {'Path':'packaging-diagnostics/first-positive-baseline-refusal.json.gz','Outcome':'First private semantic baseline refused a native physical-volume field-name mismatch; exact refusal/source retained, corrected to actual schema without runtime/source changes'}],
    'ExternalGatesRemainOpen':verification['ExternalGatesRemainOpen']})

def tool(label, args):
    result=subprocess.run(args,capture_output=True,text=True,timeout=90)
    if result.returncode:
        record={'Tool':label,'ExitCode':result.returncode,'Stdout':normalize(result.stdout),'Stderr':normalize(result.stderr),'RuntimeOrSourceChanged':False}
        target=private/('packaging-refusal-'+label+'.json')
        target.write_text(json.dumps(record,indent=2)+'\n')
        raise RuntimeError('Private packaging '+label+' refused; exact diagnostic retained at '+str(target))
    return result.stdout

tool('initial-integrity',[sys.executable,str(public/'index-packet.py'),str(public)])
positive=json.loads(tool('positive-baseline',[sys.executable,str(public/'verify-review.py'),str(public)]))
controls=json.loads(tool('semantic-mutations',[sys.executable,str(public/'test-packet-verifier.py'),str(public)]))
assert controls['PositiveBaselinePassed'] and controls['Passed']==2 and controls['FalseClosureMutationRejected']
(private/'packet-mutation-controls.json').write_text(json.dumps(controls,indent=2)+'\n')
detail(public/'packet-mutation-controls.json',controls)
verification['FalseClosureMutationRejected']=True
verification['IndependentFinalArtifactReviewPending']=True
verification['PublicationPerformedByImplementation']=False
readable(public/'verification.json',verification)

# Final prose is emitted only after actual completion plus a positive baseline
# and both rehashed semantic tamper controls, never from a running runtime job.
physical_counts=(physical['CapturedLifetimes'],physical['CapturedContainerIds'],physical['CapturedNetworkIds'],physical['CapturedAnonymousVolumeNames'])
(public/'README.md').write_text(f'''# Final runner interruption qualification

This packet selects the isolated d0a01fba/source 39 compatible Release cohort in [source.json](source.json), [cohort.json](cohort.json), [matrix.json](matrix.json) and [verification.json](verification.json). The actual child is `Hexalith.Parties.Runtime.IntegrationTests.dll`. Canonical baseline a575 and the approved frozen intent are unchanged.

All six actual held-pidfd SIGINT cases pass together with zero skips and no main emergency recovery. The real owned persisted-state mutation refuses signaling, preserves the runner, restores exact bytes, then completes SIGINT and normal cleanup. Ordinary six-replacement wrapper/actual-child recovery, coordination cancellation and all four live disposal modes pass. Replay guards remain enabled. Focused rows pass: 32 process, 101 cleanup, 38 unit  + 22 runtime consumer, 5 startup, 7 configuration and 5 queue controls. The 101 rows include all 12 recovery inspect/removal disappearance/present-error combinations, the real captured zombie followed by parent reaping, and the bounded birth-bound T/t prerequisite before the actual hung signal helper.

The invocation explicitly selects SDK 5.1.0, Tenants 8.0.0, Memories 4.0.0, FrontComposer 6.0.0 and McpCli 3.0.0. No dependency file was changed and current-default adoption is not qualified. The actual current-source Memories host at 68a78d4 is separately bound by its completed receipt and every checkpoint's executed hash.

[Independent closure](closure.json.gz) finds {physical_counts[0]} captured kernel lifetimes, {physical_counts[1]} containers, {physical_counts[2]} networks and {physical_counts[3]} anonymous volumes absent, including no selected zombies, late markers or session roots. Foreign sentinels survive. Parent pre/post readbacks bind all 39 source files, 14,209 frozen repository files, 10 declared references, 13 receipts, 2,424 prepared files, 28 archives/27 binary entries and 17 runtime/queue helpers. All final jobs were awaited and their owned scratch was removed before this candidate was exported.

[History](history.json) preserves every earlier outcome separately. Both original 100/101 prerequisite suites remain failed and admitted no runtime; only the SignalTests source differs from that retired source 39. The actual current-d0 native Debug runtime failed before checkpoint/mutation/SIGINT with writer-lease cancellation and a service-provider disposal diagnostic. Its three executed files matched while 41 of 105,880 inputs/outputs drifted; exact 82/6/1/2 emergency closure remains unqualified. The default-Tenants NU1109 refusal and preparation-gated code3 controls are separate. Older 0f MSB4018/readiness/ancestry/configuration/proxy attempts remain historical. Original 47/49/51 authenticated SDK custody remains unestablished; present-state observations cannot reconstruct it.

Detailed receipts use deterministic gzip (`mtime=0`); decode with `gzip -cd FILE.gz`. [artifact-index.json.gz](artifact-index.json.gz) and [SHA256SUMS.gz](SHA256SUMS.gz) cover this candidate. `verify-review.py` validates complete inventories, actual passing method rows, the unchanged cohort, both owner baseline sets, mandatory frozen replacement/socket/quiescence evidence and exact six-session closure. A positive semantic baseline passes; both false-closure and wrong-session mutations are rejected after all transport/decoded/checksum hashes are updated.

All3406 older public non-metadata files remain unchanged. The exact previous dated-root README/index/checksum bytes are preserved under [historical-root](historical-root/) before the parent versions active root metadata. Archived indexes describe their original snapshot. This is a private candidate pending independent parent byte/credential/claim review and authorized publication. Interrupted business work remains incomplete. Disposable backing removal supplies no physical business-erasure or historical-completeness claim. Publication/adoption, historical production inventory, older-writer exclusion, deployment/workload/key/trust custody and owner acceptance remain OPEN.
''')
readable(public/'documentation-updates.json',{
    'ApplyOnlyAfterParentCandidateAudit':True,
    'ChangeFrozenIntent':False,
    'HistoricalPublicBytesMustRemainUnchanged':True,
    'TestsReadmeAddition':'Latest qualification is tests/five-runner-interruption-2026-11-16/review-final/README.md: current d0/source 39, one compatible Release cohort, six actual SIGINT cases,32 process/101 cleanup/38 + 22 consumer/5 startup/7 configuration/5 queue rows, real state refusal/restoration, ordinary wrapper/actual child, cancellation and four live modes pass. All prior outcomes remain historical; native d0 writer-lease/disposal failure and41/105880 drifts are unqualified. Compatible invocation pins do not adopt current defaults. Final jobs/scratch are closed; external business/owner gates remain open.',
    'FiveExistingLedgerMappings':'Append the final review-final packet link and same qualified scope to the existing five mappings; retain every original production, historical-inventory, writer-exclusion, deployment and owner-acceptance gate as OPEN. Do not create replacement deferred entries or remove historical receipts.',
    'SpecUnfrozenNote':'Final current d0/source 39 qualification uses unchanged explicit compatible Release inputs and the separately identified68a current-source host. Both101-row pre-signal-repair suites and the actual native d0 pre-checkpoint failure stay unqualified. All6 current SIGINT cases and required supporting lanes pass; pre/post full readback, exact closure and actual driver/scratch completion are retained. The approved frozen intent and canonical a575 baseline remain unchanged; parent owns final review/publication.'})
tool('final-integrity',[sys.executable,str(public/'index-packet.py'),str(public)])
final=json.loads(tool('final-semantic',[sys.executable,str(public/'verify-review.py'),str(public)]))
try:
    shutil.copytree(public,candidate)
except BaseException:
    if candidate.exists():shutil.rmtree(candidate)
    raise
temporary.cleanup()
(private/'packaging-result.json').write_text(json.dumps({'PrivateCandidate':str(candidate),'PositiveBaseline':positive,'SemanticTamperControls':controls,'FinalSemantic':final,'ParentIndependentReviewPending':True,'Published':False},indent=2)+'\n')
print(json.dumps({'PrivateCandidate':str(candidate),'FinalSemantic':final,'FalseClosureAndWrongSessionRejected':True,'Published':False}))
