from pathlib import Path
import datetime,hashlib,json,subprocess,xml.etree.ElementTree as ET
root=Path.cwd(); p=root/'artifacts/runner-interruption-private-20261116/review-final/current-layout'
load=lambda f:json.loads(f.read_text())
cohort=load(p/'cohort-final.json')
sources=load(p/'source-final.json')
assert len(sources)==39 and all(hashlib.sha256((root/f).read_bytes()).hexdigest()==h for f,h in sources.items())
expected={'admitted-empty-mappings','output-known','accepted-deletion','same-uri-refresh','cross-source-authority','bounded-legacy-discovery'}
rows=[]; ids=set();networks=set();volumes=set();births=set();seen=set()
for lane in ['package-final-matrix']:
 for file in (p/lane).rglob('result.json'):
  result=load(file)
  if not result['Qualified']:continue
  phase=result['Phase']; assert phase not in seen;seen.add(phase)
  cp=load(file.with_name('checkpoint.json')); obs=cp['Observation']; topology=cp['Topology']; closure=result['Closure']
  cfg=load(file.with_name('configuration.json'))
  assert cfg['Phase']==phase and cfg['Secret']==cp['Secret'] and cfg['Nonce']==cp['Nonce'],phase
  assert cfg['SessionId']==topology['Id'] and cfg['SessionRoot']==topology['Root'] and cfg['WorkerMarker']==topology['Marker'] and cfg['LineageMarker']==topology['LineageMarker'],phase
  assert cp['Phase']==obs['Phase']==phase
  assert result['InterruptedExecution'] and result['SentinelUnaffected'] and not result['EmergencyRecovery'] and result['EmergencyClosure'] is None and result['Failures']==[]
  assert result['Signal']=='SIGINT via verified held pidfd' and result['ExitCode']!=0
  assert (result['RunnerPid'],result['RunnerStart'])==(cp['RunnerPid'],cp['RunnerStart'])
  assert closure['Closed'] and all(x['Exited'] for x in closure['Processes'])
  assert not any(closure[x] for x in ['RemainingContainerIds','RemainingNetworkIds','RemainingAnonymousVolumeNames','LateOwnedWorkers','SessionDirectoryExists'])
  assert result['LiveAudit']['LiveCustodyVerified'] and result['LiveAudit']['ExecutedBytesVerified']
  assert topology['HostLifetime']=='Hexalith.Parties.IntegrationTests.HealthChecks.PartiesTopologyLifetime'
  files=cp['ExecutedFiles']
  assert [h for f,h in files.items() if f.endswith('/Hexalith.Parties.IntegrationTests.dll')]==[cohort['WrapperSha256']]
  assert [h for f,h in files.items() if f.endswith('/prepared.json')]==[cohort['PreparedSha256']]
  assert [h for f,h in files.items() if f.endswith('/Hexalith.Parties.Runtime.IntegrationTests.dll')]==[cohort['ChildSha256']]
  persisted=obs['PersistedInterruption']
  if phase=='admitted-empty-mappings':
   pending=obs['PendingReplacement'];proof=obs['FrozenStartupProof'];assert proof['Pid']==pending['ReplacementPid'] and proof['Start']==pending['ReplacementStart'] and proof['KernelStopped'] and proof['OwnedListeningSocketsAbsent'];assert result['LiveAudit']['PendingReplacementVerified'] and pending['OriginalExited'] and pending['BeforeHealthyStartup'] and pending['UnchangedBackingCustody']
   assert (pending['OriginalPid'],pending['OriginalStart'])!=(pending['ReplacementPid'],pending['ReplacementStart'])
   private=p/lane/Path(cp['PrivateRoot']).name
   q=load(private/'incomplete-replacement-quiescence.json')
   assert q['Pid']==pending['ReplacementPid'] and q['Start']==pending['ReplacementStart'] and q['Exited'] and q['BeforeBackingRemoval']
   readiness=pending['ReplacementReadiness'];assert readiness.get('ConnectionRefused') or readiness.get('Status') in [502,503]
  if phase=='cross-source-authority':
   baseline=persisted['OwnerSourceBaselines'];assert baseline['BothOwnedSourceSetsVerified'] and {x['Source'] for x in baseline['Hashes']}=={'SourceAHashes','SourceBHashes'}
   assert all(len(x['Sha256'])==64 for x in baseline['Hashes']) and len(baseline['Baseline']['Metadata'])==6
   for name in ['SourceAHashes','SourceBHashes']:
    expected_hashes={v['Key']:v['Sha256'] for v in baseline['Baseline'][name]};assert expected_hashes and expected_hashes=={v['Key']:v['Sha256'] for v in baseline['Hashes'] if v['Source']==name}
  if phase=='same-uri-refresh':
   inv=persisted['Inventory'];assert any(e.get('sourceRevisionNumber')==1 for e in inv['entries'])
  if phase=='bounded-legacy-discovery':
   assert persisted['UnknownLegacyAuthority'] and persisted['Authority']==persisted['Provenance']==''
   assert len(persisted['LegacyArtifacts'])>=2 and all(x['Fields']>0 and len(x['CompleteFieldSha256'])==64 for x in persisted['LegacyArtifacts'])
  custody=load(file.with_name('external-custody.json'))
  ids.update(x['Id'] for x in custody['Containers']);networks.update(x['Id'] for x in custody['Networks']);volumes.update(x['Name'] for x in custody['AnonymousVolumes']);births.update((x['Pid'],x['Start']) for x in custody['Processes'])
  rows.append({'Phase':phase,'Receipt':str(file.relative_to(p)),'Qualified':True,'EmergencyRecovery':False})
assert seen==expected
methods={'admitted-empty-mappings':'SigintAtAdmittedEmptyMappings','output-known':'SigintAtOutputKnown','accepted-deletion':'SigintAtAcceptedDeletion','same-uri-refresh':'SigintAtSameUriRefresh','cross-source-authority':'SigintAtCrossSourceAuthority','bounded-legacy-discovery':'SigintAtBoundedLegacyDiscovery'}
for row in rows:
 lane=Path(row['Receipt']).parts[0];tests=list(ET.parse(p/lane/'tests.xml').iter('test'));matching=[t for t in tests if t.attrib.get('method')==methods[row['Phase']]];assert len(matching)==1 and matching[0].attrib['result']=='Pass'
a=next(ET.parse(p/'package-final-matrix/tests.xml').iter('assembly'));assert [a.attrib[x] for x in ['total','passed','failed','skipped','errors','not-run']]==['6','6','0','0','0','0']
zombies=[]
for pid,start in births:
 try:
  raw=Path(f'/proc/{pid}/stat').read_text();f=raw[raw.rfind(')')+2:].split();assert f[0] in ['Z','X'] or int(f[19])!=start,(pid,start)
  if f[0] in ['Z','X'] and int(f[19])==start:zombies.append((pid,start))
 except FileNotFoundError:pass
assert not zombies,zombies
for args,captured in [(['ps','-aq','--no-trunc'],ids),(['network','ls','-q','--no-trunc'],networks),(['volume','ls','-q'],volumes)]:
 assert not captured.intersection(subprocess.check_output(['docker',*args],text=True).split())
summary={'SourceHashes':len(sources),'Matrix':rows,'UniquePassedCases':len(rows),'FrozenCohort':cohort,'CapturedLifetimesAbsent':len(births),'CapturedContainerIdsAbsent':len(ids),'CapturedNetworkIdsAbsent':len(networks),'CapturedAnonymousVolumesAbsent':len(volumes),'SelectedCaseFailures':0,'CurrentSuiteCounts':{'Total':6,'Passed':6,'Failed':0},'UnreapedSelectedLifetimes':zombies,'HistoricalFailuresRemainUnqualified':True}
(root/'artifacts/runner-interruption-private-20261116/parent-post-review-matrix-audit.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps(summary,indent=2))
