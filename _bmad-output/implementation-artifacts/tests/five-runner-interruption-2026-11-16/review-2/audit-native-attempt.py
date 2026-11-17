import datetime,hashlib,json,subprocess
from pathlib import Path
base=Path(__file__).resolve().parent;private=base/'current-layout';sha=lambda file:hashlib.sha256(file.read_bytes()).hexdigest()
manifest=json.loads((private/'prepared.json').read_text());drift=[]
for name,expected in manifest['files'].items():
    path=Path(name);actual=sha(path) if path.is_file() else None
    if actual!=expected:drift.append({'Path':name,'Expected':expected,'Actual':actual})
cohort=json.loads((private/'cohort-final.json').read_text());actual_cohort={key:sha(Path(path)) for key,path in json.loads((private/'cohort-paths.json').read_text()).items()}
case=next((private/'persisted-state-refusal').glob('runner-interruption-*/result.json'));result=json.loads(case.read_text());custody=json.loads(case.with_name('custody.json').read_text())['Topology'];closure=result['EmergencyClosure'];remaining=[];zombies=[]
for item in closure['Processes']:
    stat=Path('/proc',str(item['Pid']),'stat')
    try:
        raw=stat.read_text();fields=raw[raw.rfind(')')+2:].split()
        if int(fields[19])==item['Start']:(zombies if fields[0] in ['Z','X'] else remaining).append(item)
    except (FileNotFoundError,ProcessLookupError):pass
containers=set(subprocess.check_output(['docker','ps','-aq','--no-trunc'],text=True,timeout=10).splitlines());networks=set(subprocess.check_output(['docker','network','ls','-q','--no-trunc'],text=True,timeout=10).splitlines());volumes=set(subprocess.check_output(['docker','volume','ls','-q'],text=True,timeout=10).splitlines())
value={'ObservedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'Qualified':False,'NativeRuntimeBlocker':'Actual runtime reached sidecar-eventstore startup timeout before checkpoint; retained log also records MSB4018 concurrent GenerateDepsFile shared-output contention','AllPreparedFilesChecked':len(manifest['files']),'PreparedFileDrift':drift,'ExpectedCohort':cohort,'ActualCohort':actual_cohort,'ThreeExecutedFilesUnchanged':cohort==actual_cohort,'MainCheckpointReached':False,'MutationExecuted':False,'SignalDelivered':result['Signal'],'ResultQualified':result['Qualified'],'EmergencyRecovery':result['EmergencyRecovery'],'EmergencyClosed':closure['Closed'],'SentinelUnaffected':result['SentinelUnaffected'],'LiveCapturedLifetimes':remaining,'UnreapedCapturedZombies':zombies,'RemainingContainerIds':[x['Id'] for x in custody['Containers'] if x['Id'] in containers],'RemainingNetworkIds':[x['Id'] for x in custody['Networks'] if x['Id'] in networks],'RemainingAnonymousVolumeNames':[x['Name'] for x in custody['AnonymousVolumes'] if x['Name'] in volumes],'SessionDirectoryExists':Path(custody['Root']).exists(),'DescendantSupervisorReaped':json.loads((private/'queue/36-persisted-state-refusal.result').read_text())['DescendantsReaped']}
(private/'native-runtime-blocker.json').write_text(json.dumps(value,indent=2)+'\n')
assert not any(value[key] for key in ['LiveCapturedLifetimes','UnreapedCapturedZombies','RemainingContainerIds','RemainingNetworkIds','RemainingAnonymousVolumeNames','SessionDirectoryExists'])
assert result['Signal'] is None and not result['Qualified'] and closure['Closed']
print(json.dumps({'PreparedFileDrifts':len(drift),'ThreeExecutedFilesUnchanged':value['ThreeExecutedFilesUnchanged'],'EmergencyClosed':value['EmergencyClosed'],'Qualified':False}))
