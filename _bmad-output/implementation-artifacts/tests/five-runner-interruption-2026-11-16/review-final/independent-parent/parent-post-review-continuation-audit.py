"""Read actual final restart/cancellation/live receipts independently of export."""
from pathlib import Path
import hashlib
import json
import sys
import xml.etree.ElementTree as ET

root = Path.cwd()
private = root / 'artifacts/runner-interruption-private-20261116'
active = private / 'review-final/current-layout'
load = lambda p: json.loads(p.read_text())
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
cohort = load(active / 'cohort-final.json')
phases = {'admitted-empty-mappings', 'output-known', 'accepted-deletion',
          'same-uri-refresh', 'cross-source-authority', 'bounded-legacy-discovery'}

def result(label):
    value = load(active / 'queue' / (label + '.result'))
    assert value['ExitCode'] == value['CommandExitCode'] == 0, label
    assert value['DescendantsReaped'] and not value['TimedOut'], label
    return value

def xml(path, count, method):
    document = ET.parse(path)
    assemblies = document.findall('assembly')
    assert len(assemblies) == 1
    a = assemblies[0]
    for name, expected in {'total':count, 'passed':count, 'failed':0,
                           'skipped':0, 'errors':0, 'not-run':0}.items():
        assert int(a.get(name, '0')) == expected, (path, name, a.get(name))
    tests = document.findall('.//test')
    assert len(tests) == count
    assert all(t.get('result') == 'Pass' and t.get('method') == method for t in tests)
    return {'Method':method, 'PassingRows':count, 'XmlSha256':sha(path)}

def owned(directory, controlled):
    owners = list(directory.glob('parties-five-memory-runtime-*'))
    assert len(owners) == 1
    d = owners[0]
    o = load(d / 'ownership.json')
    assert o['OwnedTopologyDisposed'] and not o['SessionDirectoryExists']
    assert o['ControlledCancellation'] is controlled
    assert o['CleanupFailure'] is None
    assert not Path(o['Root']).exists()
    if not controlled:
        assert o['PrimaryFailure'] is None
    identities = load(d / 'executed-identities.json')
    for suffix, key in [('/Hexalith.Parties.IntegrationTests.dll','WrapperSha256'),
                        ('/Hexalith.Parties.Runtime.IntegrationTests.dll','ChildSha256')]:
        assert [h for f,h in identities.items() if f.endswith(suffix)] == [cohort[key]]
    # Ordinary/cancellation provenance records wrapper+child; only interruption
    # provenance adds the manifest. Bind its actual command path and live bytes.
    manifest_path = load(directory / 'command.json')['Prepared']
    assert manifest_path == load(active / 'cohort-paths.json')['PreparedSha256']
    assert sha(Path(manifest_path)) == cohort['PreparedSha256']
    return d, o

ordinary = active / 'package-ordinary-restart'
r = result('71-ordinary-restart')
assert load(ordinary / 'summary.json')['ExitCode'] == 0
wrapper = xml(ordinary / 'tests.xml', 1,
              'ActualCurrentSourceMemoriesRestartsAcrossFiveRecoveryBoundaries')
d, ownership = owned(ordinary, False)
child = xml(d / 'runtime.xml', 1,
            'SelectedMemoriesRecoverAcrossProducerWorkloadRestarts')
restarts = load(d / 'producer-restarts.json')
assert len(restarts) == 6 and {x['Phase'] for x in restarts} == phases
for x in restarts:
    assert x['OriginalExited'] and x['Healthy']
    assert (x['OriginalPid'],x['OriginalStart']) != (x['ReplacementPid'],x['ReplacementStart'])
    assert x['PreparedManifestSha256'] == cohort['PreparedSha256']
    for prefix in ['Original','Replacement']:
        p = Path('/proc') / str(x[prefix+'Pid']) / 'stat'
        try:
            raw = p.read_text(); fields = raw[raw.rfind(')')+2:].split()
        except (FileNotFoundError, ProcessLookupError):
            continue
        assert fields[19] != str(x[prefix+'Start']), (prefix, x['Phase'], fields[0])
    ack = load(d / 'producer-restart' / (x['Phase'] + '.ack.json'))
    assert ack['phase'] == x['Phase'] and ack['session'] == ownership['Id'] and ack['replaced']
ordinary_report = {'Passed':True, 'SixActualReplacementsAndAcknowledgements':sorted(phases),
                   'Wrapper':wrapper, 'ActualChild':child, 'OwnedTopologyDisposed':True,
                   'ExactProducerBirthsAbsentIncludingZombies':True,
                   'DescendantsReaped':True, 'Seconds':r['Seconds']}
(private / 'parent-post-review-ordinary-audit.json').write_text(json.dumps(ordinary_report,indent=2)+'\n')
if sys.argv[1:] == ['ordinary']:
    print(json.dumps(ordinary_report,indent=2))
    sys.exit(0)

canceled = active / 'package-controlled-cancellation'
r = result('72-controlled-cancellation')
assert load(canceled / 'summary.json')['ExitCode'] == 0
wrapper = xml(canceled / 'tests.xml', 1,
              'ActualCurrentSourceCanceledRestartCompletesOwnedCleanup')
d, ownership = owned(canceled, True)
assert 'OperationCanceledException' in ownership['PrimaryFailure']
cancel_report = {'Passed':True, 'Wrapper':wrapper, 'ControlledCancellationObserved':True,
                 'IncompleteReplacementCleanupAndOwnedDisposalPassed':True,
                 'DescendantsReaped':True, 'Seconds':r['Seconds']}
(private / 'parent-post-review-cancellation-audit.json').write_text(json.dumps(cancel_report,indent=2)+'\n')
if sys.argv[1:] == ['cancellation']:
    print(json.dumps(cancel_report,indent=2))
    sys.exit(0)

live = active / 'package-live-shutdown'
r = result('73-live-shutdown')
assert load(live / 'summary.json')['ExitCode'] == 0
rows = xml(live / 'tests.xml', 4,
           'LiveShutdown_NormalCanceledAndFailedStartupStopsOwnedWorkersWithinObservationDeadlineAsync')
names = [t.get('name').replace('\\', '') for t in ET.parse(live / 'tests.xml').findall('.//test')]
assert all(sum('"'+mode+'"' in n for n in names) == 1
           for mode in ['normal','canceled','failed','cleanup-failed'])
live_report = {'Passed':True, **rows, 'ActualLiveModes':['canceled','cleanup-failed','failed','normal'],
               'DescendantsReaped':True, 'Seconds':r['Seconds']}
(private / 'parent-post-review-live-audit.json').write_text(json.dumps(live_report,indent=2)+'\n')
post = result('74-final-post-runtime-validation')
print(json.dumps({'Ordinary':ordinary_report,'Cancellation':cancel_report,'Live':live_report,
                  'FinalValidation':post},indent=2))
