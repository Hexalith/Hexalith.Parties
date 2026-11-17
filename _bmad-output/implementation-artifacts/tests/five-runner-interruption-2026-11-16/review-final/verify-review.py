import datetime,gzip,hashlib,json,sys,xml.etree.ElementTree as ET
from pathlib import Path
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parent

def read(path):
    p=root/path
    raw=p.read_bytes()
    return gzip.decompress(raw) if p.suffix=='.gz' else raw

def obj(path):return json.loads(read(path))
def require(value,message):
    if not value:raise AssertionError(message)
def counts(path):
    assemblies=ET.fromstring(read(path)).findall('.//assembly')
    result={k:sum(int(a.attrib.get(k,0)) for a in assemblies) for k in ['total','passed','failed','skipped','errors','not-run']}
    require(result['total']>0 and result['passed']==result['total'] and all(result[k]==0 for k in ['failed','skipped','errors','not-run']),f'Failed/empty XML: {path}')
    return result

index=obj('artifact-index.json.gz')
indexed={record['Path'] for record in index}
actual_paths={path.relative_to(root).as_posix() for path in root.rglob('*') if path.is_file() and path.relative_to(root).as_posix() not in ['artifact-index.json.gz','SHA256SUMS.gz']}
require(len(indexed)==len(index) and indexed==actual_paths,'Complete unique current artifact inventory')
sums={line.split('  ',1)[1]:line.split('  ',1)[0] for line in read('SHA256SUMS.gz').decode().splitlines()}
require(set(sums)==actual_paths|{'artifact-index.json.gz'},'Complete checksum inventory')
for path,digest in sums.items():require(hashlib.sha256((root/path).read_bytes()).hexdigest()==digest,'Checksum file: '+path)

parsed_json=parsed_xml=0
for record in index:
    p=root/record['Path'];raw=p.read_bytes()
    require(len(raw)==record['Bytes'],'Transport length: '+record['Path'])
    require(hashlib.sha256(raw).hexdigest()==record['TransportSha256'],'Transport hash: '+record['Path'])
    decoded=gzip.decompress(raw) if p.suffix=='.gz' else raw
    require(len(decoded)==record['DecodedBytes'],'Decoded length: '+record['Path'])
    require(hashlib.sha256(decoded).hexdigest()==record['DecodedSha256'],'Decoded hash: '+record['Path'])
    suffix=p.with_suffix('').suffix if p.suffix=='.gz' else p.suffix
    if suffix=='.json':json.loads(decoded);parsed_json+=1
    if suffix=='.xml':ET.fromstring(decoded);parsed_xml+=1
source=obj('source.json')
require(len(source['Files'])==39,'39 frozen implementation/current-layout source files')
require(source['ChildAssembly']=='Hexalith.Parties.Runtime.IntegrationTests.dll','Current-layout runtime assembly is required')
require(source['QualificationHead']=='d0a01fbadc5c6d9df56a39e6b1e4e831df9bdfd1','Isolated qualification source head')
prepared=obj('current-layout/prepared.json.gz')
require(prepared['schemaVersion']==1 and prepared['configuration']=='Release' and prepared['sdkVersion']=='5.1.0' and prepared['completed'] is True,'Completed explicit Release package preparation')
require(prepared['familyVersions']['HexalithMemoriesVersion']=='4.0.0' and prepared['familyVersions']['HexalithFrontComposerVersion']=='6.0.0','Explicit qualified consumer families')
require(prepared['familyVersions']['HexalithTenantsVersion']=='8.0.0' and prepared['familyVersions']['HexalithMcpCliVersion']=='3.0.0','Exact approved compatible fallback families')
require(len(obj('current-layout/reference-heads.json.gz'))==10,'Ten frozen root-declared source references')
require(obj('cohort.json')['Files']==source['Cohort'],'Readable source/cohort agreement')
for path,expected in source['Files'].items():require(hashlib.sha256(read('executed-source/'+path+'.gz')).hexdigest()==expected,'Executed source hash: '+path)
matrix=obj('matrix.json');rows=matrix['Cases']
selected_sessions=set()
require(matrix['SameUnchangedCohort']==source['Cohort'],'Matrix uses the frozen cohort')
suite=matrix['Suite'];require(counts(suite['Xml'])==suite['Counts']=={'total':6,'passed':6,'failed':0,'skipped':0,'errors':0,'not-run':0} and suite['PassedAsAggregate'] is True,'Complete passing current-layout suite')
phases={'admitted-empty-mappings','output-known','accepted-deletion','same-uri-refresh','cross-source-authority','bounded-legacy-discovery'}
require(len(rows)==6 and {r['Phase'] for r in rows}==phases,'Six distinct required phases')
for row in rows:
    result=obj(row['Result']);checkpoint=obj(row['Checkpoint']);custody=obj(row['ExternalCustody']);configuration=obj(row['Configuration'])
    selected_sessions.add(custody['Id'])
    phase=row['Phase'];observation=checkpoint['Observation'];topology=checkpoint['Topology'];persisted=observation['PersistedInterruption']
    require(result['Qualified'] and not result['EmergencyRecovery'] and not result['Failures'],'Unqualified main phase: '+phase)
    require(result['Signal']=='SIGINT via verified held pidfd' and result['InterruptedExecution'] and result['ExitCode']!=0,'Actual interrupted runner: '+phase)
    require(result['SentinelUnaffected'] and result['Closure']['Closed'],'Independent closure/sentinel: '+phase)
    require(result['Closure']['RemainingAnonymousVolumeNames']==[],'Anonymous volumes: '+phase)
    require(checkpoint['RunnerPid']==result['RunnerPid'] and checkpoint['RunnerStart']==result['RunnerStart'],'Runner binding: '+phase)
    require(checkpoint['Phase']==phase==observation['Phase']==configuration['Phase'],'Phase binding: '+phase)
    require(checkpoint['SecretSha256']==configuration['SecretSha256'] and checkpoint['Nonce']==configuration['Nonce'],'Authenticated issued configuration: '+phase)
    require(topology['Id']==custody['Id']==configuration['SessionId'],'Issued session: '+phase)
    for key,issued in [('Root','SessionRoot'),('Marker','WorkerMarker'),('LineageMarker','LineageMarker')]:require(topology[key]==custody[key]==configuration[issued],'Issued ownership '+key+': '+phase)
    hashes=checkpoint['ExecutedFiles']
    require([v for k,v in hashes.items() if k.endswith('/Hexalith.Parties.IntegrationTests.dll')]==[source['Cohort']['WrapperSha256']],'One final wrapper: '+phase)
    require([v for k,v in hashes.items() if k.endswith('/prepared.json')]==[source['Cohort']['PreparedSha256']],'One final preparation: '+phase)
    require([v for k,v in hashes.items() if k.endswith('/Hexalith.Parties.Runtime.IntegrationTests.dll')]==[source['Cohort']['ChildSha256']],'One final runtime child: '+phase)
    instant=lambda value:datetime.datetime.fromisoformat(value.replace('Z','+00:00'))
    observed=instant(observation['ObservedAtUtc']);created=instant(checkpoint['CreatedUtc']);expires=instant(observation['ExpiresAtUtc'])
    require(observed<=created<expires and (created-observed).total_seconds()<=15,'Fresh published frozen observation: '+phase)
    ownership=obj(row['RuntimePrefix']+'/ownership.json.gz')
    require(ownership['OwnedTopologyDisposed'] and ownership['CleanupFailure'] is None and not ownership['SessionDirectoryExists'],'Fixture disposal: '+phase)
    require('CanceledException' in ownership['PrimaryFailure'],'Interruption remains failure: '+phase)
    if phase=='admitted-empty-mappings':
        pending=observation['PendingReplacement'];quiescence=obj(row['RuntimePrefix']+'/incomplete-replacement-quiescence.json.gz')
        startup=observation['FrozenStartupProof']
        require(startup['KernelStopped'] and startup['OwnedListeningSocketsAbsent'] and (startup['Pid'],startup['Start'])==(pending['ReplacementPid'],pending['ReplacementStart']),'Conservative frozen startup proof')
        require(result['LiveAudit']['PendingReplacementVerified'] and pending is not None and pending['OriginalExited'] and pending['BeforeHealthyStartup'],'Mandatory pending replacement')
        require((pending['OriginalPid'],pending['OriginalStart'])!=(pending['ReplacementPid'],pending['ReplacementStart']),'Distinct replacement')
        require((quiescence['Pid'],quiescence['Start'])==(pending['ReplacementPid'],pending['ReplacementStart']) and quiescence['Exited'] and quiescence['BeforeBackingRemoval'],'Mandatory matching pre-backing quiescence')
    else:require(not result['LiveAudit']['PendingReplacementVerified'],'Unexpected replacement proof: '+phase)
    if phase=='cross-source-authority':
        baselines=persisted['OwnerSourceBaselines'];require(baselines['BothOwnedSourceSetsVerified'],'Both existing source sets')
        require(len(baselines['Baseline']['Metadata'])==6,'Both authority/provenance/inventory sets')
        for name in ['SourceAHashes','SourceBHashes']:
            expected={item['Key']:item['Sha256'] for item in baselines['Baseline'][name]}
            current={item['Key']:item['Sha256'] for item in baselines['Hashes'] if item['Source']==name}
            require(expected and expected==current,'Fresh full '+name)
    if phase=='bounded-legacy-discovery':require(persisted['UnknownLegacyAuthority'] and len(persisted['LegacyArtifacts'])>=2,'Unknown discovery retained')
    method={'admitted-empty-mappings':'SigintAtAdmittedEmptyMappings','output-known':'SigintAtOutputKnown','accepted-deletion':'SigintAtAcceptedDeletion','same-uri-refresh':'SigintAtSameUriRefresh','cross-source-authority':'SigintAtCrossSourceAuthority','bounded-legacy-discovery':'SigintAtBoundedLegacyDiscovery'}[phase]
    tests=ET.fromstring(read(row['Xml'])).findall('.//test')
    matching=[test for test in tests if test.attrib.get('name','').endswith('.'+method)]
    require(len(matching)==1 and matching[0].attrib['result']=='Pass','Selected executed XML row: '+phase)
closure=obj('closure.json.gz')
require(closure.get('Closed') is True,'Final independent closure must be true')
for name in ['LiveCapturedLifetimes','UnreapedCapturedZombies','LateMarkerBoundPids','RemainingContainerIds','RemainingNetworkIds','RemainingAnonymousVolumeNames','RemainingSessionDirectories']:
    require(closure.get(name)==[],'Final independent closure collection must be empty: '+name)
require(len(selected_sessions)==6 and len(closure.get('MainSessions',[]))==6 and set(closure['MainSessions'])==selected_sessions,'Final closure must bind exactly the six selected sessions')
cleanup_rows=ET.fromstring(read(obj('current-layout/current-cleanup-selection.json.gz')['Xml'])).findall('.//test')
recovery_rows=[test for test in cleanup_rows if test.get('method')=='RecoveryReconcilesObjectsDisappearingAfterItsFinalInventory']
zombie_rows=[test for test in cleanup_rows if test.get('method')=='ClosureRejectsCapturedUnreapedZombieUntilActualReaping']
require(len(recovery_rows)==12 and len(zombie_rows)==1 and all(test.get('result')=='Pass' for test in recovery_rows+zombie_rows),'Twelve real recovery race/error rows and actual zombie/reaping regression')
retired=obj('historical-prerequisites/unqualified-retirement-audit.json.gz')
require(retired['Qualified'] is False and retired['LongRuntimeCommandsExecuted']==[] and retired['RuntimeAndQueueHelpersUnchanged'],'Earlier prerequisites stay unqualified and never admit runtime')
completion=obj('historical-prerequisites/retired-job-completion.json.gz')
require(completion['Qualified'] is False and completion['AllCommandsAwaited'] and completion['ScratchRemoved'] and completion['ExitCode']==0 and not completion['DriverCompletion']['ContainmentFailures'],'Retired failed-prerequisite job was awaited and removed')
historical_source=obj('historical-prerequisites/source.json')
require(historical_source['Qualified'] is False and set(historical_source['Files'])==set(source['Files']),'Historical prerequisite source identity')
changed={path for path,digest in historical_source['Files'].items() if source['Files'][path]!=digest}
require(changed=={'tests/Hexalith.Parties.IntegrationTests/Topology/PartyMemoryProducerRestartSignalTests.cs'},'Fresh cohort includes the necessary bounded stopped-state verification repair')
for path,digest in historical_source['Files'].items():require(hashlib.sha256(read('historical-prerequisites/executed-source/'+path+'.gz')).hexdigest()==digest,'Retained failed-prerequisite source: '+path)
for label in ['62-final-controls','68b-final-controls-retry']:
    prior=ET.fromstring(read('historical-prerequisites/'+label+'.xml.gz'))
    assemblies=prior.findall('.//assembly')
    require(sum(int(item.get('total',0)) for item in assemblies)==101 and sum(int(item.get('passed',0)) for item in assemblies)==100 and sum(int(item.get('failed',0)) for item in assemblies)==1,'Retain failed100/101 prerequisite aggregate: '+label)
    failures=[test for test in prior.findall('.//test') if test.get('result')!='Pass']
    require(len(failures)==1 and failures[0].get('method')=='HungSignalHelperAfterAppliedStopIsReapedAndOriginalProcessResumes','Retain the exact original stopped-state assertion failure: '+label)
verification=obj('verification.json')
commands=obj('commands-current-layout.json')
def command_start(label):
    matches=[command for command in commands if command['Label']==label]
    require(len(matches)==1 and matches[0]['Result']['ExitCode']==0 and matches[0]['Result']['DescendantsReaped'],'Actual exclusive command anchor: '+label)
    return int(matches[0]['Result']['Supervisor']['Start'])
lower,upper=command_start(obj('current-layout/current-cleanup-selection.json.gz')['Command']),command_start(obj('current-layout/current-cleanup-selection.json.gz')['NextCommand'])
current_paths={record['Path'] for record in index if record['Path'].startswith('current-layout/early-controls-package-final/consumer-gate-') and record['Path'].endswith('.json.gz') and '-custody.' not in record['Path']}
require(len(current_paths)==3,'Exactly three freshly executed marker guards')
guard_tests=[test for test in ET.fromstring(read(obj('current-layout/current-cleanup-selection.json.gz')['Xml'])).findall('.//test') if '.FailedConsumerTerminationRetainsBothRemovalGatesUntilExactExit(' in test.attrib['name']]
require(len(guard_tests)==3,'Actual three passing gate XML rows')
gates=set()
for path in current_paths:
    guard=obj(path);custody=obj(path.removesuffix('.json.gz')+'-custody.json.gz')
    require(all(guard[key]==custody[key] for key in ['Gate','ConsumerPid','ConsumerStart']),'Exact marker-control outcome/custody binding')
    require(custody['Id'] in path and custody['Marker'] and custody['BackingId'] and custody['NetworkId'],'Issued marker-control session/backing custody')
    require(all(lower<=tick<upper for tick in [custody['ConsumerStart'],custody['ControllerStart']]),'Fresh selected-cleanup-to-next-runtime kernel birth interval')
    matches=[test for test in guard_tests if ('gate: "'+guard['Gate']+'"') in test.attrib['name'].replace('\\"','"')]
    require(len(matches)==1 and matches[0].attrib['result']=='Pass','Actual matching passing gate XML row')
    gates.add(guard['Gate'])
require(gates=={'application','control-plane','stale-quiescence'},'All three intended guard kinds')
guards=[obj(path) for path in current_paths]
for guard in guards:
    require(all(guard[k] for k in ['ParentExitedBeforeDiscovery','FailedTerminationPreserved','BackingAndControlPlaneRetained','BuilderRetainedUntilExactExit','ExactConsumerExitedBeforeRemoval','SuccessfulRetry','ForeignSurvived']),'Marker retention/exit/survivor proof: '+guard['Gate'])
    require(guard['ActualFixtureDisposalExercised']==(guard['Gate']!='control-plane'),'Actual application/builder disposal caller')
configuration_xml=verification['ConfigurationOnlyControls']['Xml']
require(counts(configuration_xml)['passed']==7,'Seven configuration-only and startup-ordering controls')
configuration_rows=ET.fromstring(read(configuration_xml)).findall('.//test')
configuration_only=[t for t in configuration_rows if 'ConfiguredDaprPath_' in t.attrib['name']]
require(len(configuration_only)==3 and all('startupScheduled=False' in (t.findtext('output') or '') and 'startupScheduled=True' not in (t.findtext('output') or '') for t in configuration_only),'Passive configuration trace shows no startup admission')
startup_xml=verification['StartupAdmissionControls']['Xml']
require(counts(startup_xml)['passed']==5,'Exact reproduction and both admission orderings')
startup_tests=ET.fromstring(read(startup_xml)).findall('.//test')
require(len([t for t in startup_tests if 'ConfiguredDaprPath_' in t.attrib['name']])==3 and len([t for t in startup_tests if 'StartupAdmissionAndCleanupPreserveBothOrderings' in t.attrib['name']])==2,'Both startup-admission orderings and exact three-row reproduction')
for name,lane in verification['Lanes'].items():
    actual=counts(lane['Xml']);require(actual==lane['Counts'],'Lane counts: '+name)
consumer_names=['focused-consumer-unit','focused-consumer-runtime']
require(sum(verification['Lanes'][name]['Counts']['passed'] for name in consumer_names)==60,'60 consumer controls across current assembly split')
current_methods=[]
for name in consumer_names:current_methods.extend(t.attrib['name'] for t in ET.fromstring(read(verification['Lanes'][name]['Xml'])).findall('.//test'))
historical_methods=[t.attrib['name'] for t in ET.fromstring(read('historical-consumer-selection.xml.gz')).findall('.//test')]
require(sorted(current_methods)==sorted(historical_methods) and len(current_methods)==60,'Reexecuted exact historical60 method rows in current assemblies')
require(obj('current-layout/consumer-method-set.json.gz')['Methods']==sorted(current_methods),'Readable focused selection agrees with actual XML')
require(verification['Lanes']['focused-consumer-unit']['Counts']['passed']==38 and verification['Lanes']['focused-consumer-runtime']['Counts']['passed']==22,'Exact current unit/runtime consumer split')
require(verification['Lanes']['focused-cleanup']['Counts']['passed']==101,'Preserved cleanup controls plus discovery/startup regressions')
require(verification['Lanes']['ordinary-wrapper']['Counts']['passed']==1 and verification['Lanes']['ordinary-child']['Counts']['passed']==1,'Ordinary wrapper/actual child')
require(verification['Lanes']['coordination-cancellation']['Counts']['passed']==1,'Coordination cancellation')
live=ET.fromstring(read(verification['Lanes']['live-shutdown']['Xml']))
import re
display_names=[re.sub(r'\\+(?=")','',t.attrib['name']) for t in live.findall('.//test') if 'mode: ' in t.attrib.get('name','')]
modes={re.search(r'mode: "([^"]+)"',name).group(1) for name in display_names}
require(modes=={'normal','canceled','failed','cleanup-failed'},'All four intended live modes')
require(verification['IsolatedLocalClone'] and verification['RootReferenceGitlinksFrozen'] and verification['QualificationHead']==source['QualificationHead'] and verification['CanonicalBaseline']=='a575021fc97a8eda664711089270b60f5e570ded','Current-layout isolation and canonical baseline')
require(verification['ImplementationComplete'] and verification['AllJobsAwaited'] and verification['ScratchRemoved'],'Completed handoff')
completion=obj(verification['CompletionReceipt'])
require(completion['AllFinalCommandsCompleted'] and completion['AllFinalChildrenAndHelpersAwaited'] and completion['OuterDriverExitCode']==0 and completion['DriverCompletion']['AllJobsAwaited'] and not completion['DriverCompletion']['ContainmentFailures'] and completion['ParentPostReadbackCompleted'] and completion['ScratchRemoved'] and not completion['OwningWorkspaceExistsAfterExit'] and completion['FinalCohort']==source['Cohort'],'Awaited final job and independently rehashed cohort before owned scratch release')
require(len(completion['FinalCommandResults'])==6 and all(value['ExitCode']==0 and value['DescendantsReaped'] for value in completion['FinalCommandResults'].values()),'All six final runtime/validation commands completed and reaped')

require(verification['Lanes']['package-process-controls']['Counts']['passed']==32,'Existing shared process regression class and direct-status controls')
require(verification['Lanes']['persisted-state-refusal']['Counts']['passed']==1,'Real persisted checkpoint mutation control')
mutation_prefix='current-layout/package-persisted-state-refusal/same-uri-refresh-'
mutation=obj(mutation_prefix+'result.json.gz');mutation_checkpoint=obj(mutation_prefix+'checkpoint.json.gz');mutation_configuration=obj(mutation_prefix+'configuration.json.gz')
require(mutation['ChangedPersistedStateRefusedRunnerSignal'] and mutation['Qualified'] and mutation['InterruptedExecution'] and mutation['Signal']=='SIGINT via verified held pidfd' and mutation['SentinelUnaffected'] and not mutation['EmergencyRecovery'] and not mutation['Failures'] and mutation['Closure']['Closed'],'Actual changed-state refusal, restored-state signal and normal closure')
require(mutation_checkpoint['RunnerPid']==mutation['RunnerPid'] and mutation_checkpoint['RunnerStart']==mutation['RunnerStart'] and mutation_checkpoint['Nonce']==mutation_configuration['Nonce'] and mutation_checkpoint['SecretSha256']==mutation_configuration['SecretSha256'],'Actual mutation runner and issued configuration binding')
for suffix,hash_key in [('/Hexalith.Parties.IntegrationTests.dll','WrapperSha256'),('/prepared.json','PreparedSha256'),('/Hexalith.Parties.Runtime.IntegrationTests.dll','ChildSha256')]:
    require([value for path,value in mutation_checkpoint['ExecutedFiles'].items() if path.endswith(suffix)]==[source['Cohort'][hash_key]],'Actual mutation unchanged cohort: '+hash_key)

require(verification['QueueControls']['Passed']==5,'Five contained queue controls')
admission=obj(verification['QueueControls']['AdmissionReceipt'])
require(admission['AdmissionObservedBeforeTimeoutWait'] and admission['ChildExitedBeforeReturn'] and admission['ForeignSurvived'] and admission['ProductionDriverUnchanged'],'Actual admitted detached child timeout/closure/survivor control')
require(admission['ActualOutcome']['ExitCode']==255 and admission['ActualOutcome']['DescendantsReaped'] and admission['ActualWaitTimeoutSeconds']==0.3 and admission['AdmissionBudgetSeconds']==5,'Finite admitted timeout exercised actual unchanged driver cleanup')
commands=obj('commands-current-layout.json')
post_label=verification['PostRuntimeInputValidation']['CommandLabel']
post_commands=[command for command in commands if command['Label']==post_label]
require(len(post_commands)==1 and post_commands[0]['Result']['ExitCode']==0 and post_commands[0]['Result']['DescendantsReaped'] and post_commands[0]['Args'][-1].endswith('/validate-release-cohort.py'),'Actual completed post-runtime full input validation')
pre=obj('independent-parent/parent-post-review-cohort-pre-audit.json.gz')
post=obj('independent-parent/parent-post-review-cohort-post-audit.json.gz')
require(pre['Phase']=='pre' and post['Phase']=='post','Actual independent pre/post execution readbacks')
for key in ['ActualExecutedFiles','ManifestAnchors','RuntimeAndQueueHelpers','QualificationHead','SourceFilesMatchCloneAndOriginal','FrozenRepositoryFilesVerified','RootReferenceGitlinksVerified','PreparedProjectReceiptsVerified','PreparedFilesVerified','PackageArchivesVerified','PackageBinaryEntriesVerified']:
    require(pre[key]==post[key],'Independent pre/post input/cohort agreement: '+key)
for path,digest in post['RuntimeAndQueueHelpers'].items():
    exported=path.removeprefix('review-final/') if path.startswith('review-final/') else 'current-layout/'+path
    require(hashlib.sha256(read(exported)).hexdigest()==digest,'Exported actual anchored runtime/queue helper: '+path)
require(post['ActualExecutedFiles']==source['Cohort'] and post['QualificationHead']==source['QualificationHead'],'Independent actual executed cohort/source head agreement')
require(post['SourceFilesMatchCloneAndOriginal']==39 and post['RootReferenceGitlinksVerified']==10 and post['PreparedProjectReceiptsVerified']==len(prepared['projects']),'Independent complete source/reference/receipt readback')
require(post['PreparedFilesVerified']==len(obj('current-layout/prepared-closure.json.gz')['Files']) and post['FrozenRepositoryFilesVerified']>39 and post['PackageArchivesVerified']>0 and post['PackageBinaryEntriesVerified']>0,'Independent complete prepared/repository/archive/binary closure')
if verification['FalseClosureMutationRejected']:
    controls=obj(verification['FalseClosureAndWrongSessionControls'])
    require(controls['PositiveBaselinePassed'] and controls['Passed']==2 and controls['FalseClosureMutationRejected'] and {item['Mutation'] for item in controls['Controls']}=={'false-closure','wrong-session'} and all(item['TransportAndDecodedHashesUpdated'] and item['Rejected'] for item in controls['Controls']),'Actual positive-baseline and two rehashed semantic mutation receipts')
# Current qualification never adopts the externally advanced dependency defaults.
profile=verification['ExplicitFallbackProfile']
require(profile==obj('current-layout/explicit-fallback-profile.json.gz'),'Readable invocation profile agrees with retained profile')
for family,version in {'EventStore':'5.1.0','Tenants':'8.0.0','Memories':'4.0.0','FrontComposer':'6.0.0','McpCli':'3.0.0'}.items():
    require(profile[family]==version,'Exact approved invocation profile: '+family)
require(profile['Configuration']=='Release' and profile['TrackedDependencyFilesChanged'] is False and verification['ExplicitProfileInvocationOnly'] and verification['CurrentSourceHostSeparate'] and verification['CurrentDefaultDependencyAdoptionQualified'] is False and verification['PublishedPackageQualification'] is False,'Invocation-only compatibility qualification preserves dependency files and adoption limits')
references=obj('current-layout/reference-heads.json.gz')
require(references['references/Hexalith.Memories']=='68a78d475c7ae8b77193e308dfb8c8f5e4cc0826','Separately identified current source host revision')
host=obj('current-layout/current-source-host-receipt.json.gz')
require(host['completed'] and host['configuration']=='Release' and host['sdkVersion']=='5.1.0' and host['familyVersions']==prepared['familyVersions'] and host['project'].endswith('/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj'),'Completed separate current-source host preparation')
for row in rows:
    checkpoint=obj(row['Checkpoint']);result=obj(row['Result']);topology=checkpoint['Topology'];closed=result['Closure']
    require(checkpoint['ExecutedFiles'][host['targetPath']]==host['files'][host['targetPath']],'Executed source host binds its completed receipt: '+row['Phase'])
    require(len(topology['Containers'])==6 and len(topology['Networks'])==1 and len(topology['AnonymousVolumes'])==2,'Complete checkpoint backing inventory: '+row['Phase'])
    require(all(item['Labels']['hexalith.parties.test.owner']==topology['Marker'] and item['Labels']['hexalith.parties.test.session']==topology['Id'] for item in topology['Containers']+topology['Networks']),'Exact externally issued backing labels: '+row['Phase'])
    require(all(item['Exited'] for item in closed['Processes']) and all(closed[key]==[] for key in ['RemainingContainerIds','RemainingNetworkIds','RemainingAnonymousVolumeNames']) and closed['LateOwnedWorkers']==0 and closed['SessionDirectoryExists'] is False,'Per-case closure must retain no captured kernel lifetime or backing: '+row['Phase'])
combinations=set()
for test in recovery_rows:
    match=re.search(r'kind: "([^"]+)", duringRemoval: (True|False), disappear: (True|False)',test.get('name').replace('\\"','"'))
    require(match is not None,'Named real recovery regression parameters')
    combinations.add(match.groups())
require(combinations=={(kind,stage,disappear) for kind in ['container','volume','network'] for stage in ['True','False'] for disappear in ['True','False']},'All twelve exact disappearance/present-error combinations execute')
signal_rows=[test for test in cleanup_rows if test.get('method')=='HungSignalHelperAfterAppliedStopIsReapedAndOriginalProcessResumes']
require(len(signal_rows)==1 and signal_rows[0].get('result')=='Pass','Actual repaired stopped-state prerequisite reaches the hung helper and resume checks')
require(set(completion['FinalCommandResults'])=={'69-persisted-state-refusal','70-final-matrix','71-ordinary-restart','72-controlled-cancellation','73-live-shutdown','74-final-post-runtime-validation'},'Completion binds exactly the six final runtime/validation jobs')
require(set(verification['ExternalGatesRemainOpen'])=={'publication/adoption','historical production inventory','older-writer exclusion','deployment/workload/key/trust custody','owner acceptance'},'All existing external gates remain open')
legacy=obj(verification['HistoricalContext'])
require(legacy['AuthenticatedClosureEstablishedForTheseRuns'] is False and legacy['RecoveryPerformed'] is False and legacy['OriginalPerRunDirectoryBindingUnavailable'] is True and {run['Command'].split('-')[0] for run in legacy['Runs']}=={'47','49','51'},'Original failed SDK custody remains unestablished')
present=obj('historical-context/original-sdk-present-state.json.gz')
require(present['OriginalFailedFocusedCustodyReconstructed'] is False and present['AuthenticatedClosureEstablishedForFailedFocusedRuns'] is False and present['RecoveryAuthorizedByTheseObservations'] is False,'Present-state observations cannot reconstruct original custody')

# The actual current-head native runtime failure is historical and supplies no final acceptance.
native=obj('native-d0-attempt/status.json.gz')
native_post=obj('native-d0-attempt/parent-current-native-post-audit.json.gz')
native_pre=obj('native-d0-attempt/parent-current-native-pre-audit.json.gz')
native_cohort=obj('native-d0-attempt/records/cohort-final.json.gz')
require(native['Qualified'] is False and native['QualificationHead']==source['QualificationHead'] and native['PublishedCheckpoint'] is False and native['PersistedStateMutationPerformed'] is False and native['ActualSigintPerformed'] is False,'Retain current native pre-checkpoint refusal without mutation or SIGINT acceptance')
require(native_post['Phase']=='post' and native_pre['Phase']=='pre' and native_post['ActualExecutedFiles']==native_post['ExpectedExecutedFiles']==native_cohort and native_post['CohortThreeFilesUnchanged'] and native_post['NativePreparedFilesUnchanged'] is False,'Native three executed files match while its complete input/output closure drifted')
require(native_post['NativePreparedFilesRead']==native['FrozenNativeFiles']==105880 and len(native_post['NativePreparedFileDrifts'])==native['DriftedNativeFiles']==41,'Retain every exact native input/output drift')
require(all(item['ExpectedSha256']!=item['ActualSha256'] for item in native_post['NativePreparedFileDrifts']),'Actual native expected/current hashes differ')
require(native_pre['NativePreparedFilesUnchanged'] and native_pre['NativePreparedFileDrifts']==[] and native_pre['NativePreparedFilesRead']==105880 and native_pre['ActualExecutedFiles']==native_cohort,'Native preparation was complete before its actual runtime attempt')
for key in ['QualificationHead','ManifestAnchors','RuntimeAndQueueHelpers','SourceFilesMatchCloneAndOriginal','FrozenRepositoryFilesVerified','RootReferenceGitlinksVerified']:
    require(native_pre[key]==native_post[key],'Native pre/post preserved source/helper anchors: '+key)
for label in ['01-source-prepare','03-debug-unit','04-debug-runtime','05-debug-wrapper','06-native-runtime-preparation','07-debug-contracts','08-freeze-native-cohort','09-validate-native-cohort','10-native-process','11-native-cleanup','12-native-consumer-unit','13-native-consumer-runtime','14-native-startup','15-native-configuration','16-native-queue']:
    record=obj('native-d0-attempt/queue/'+label+'.result.gz')
    require(record['ExitCode']==0 and record['DescendantsReaped'],'Actual current-head source/Debug prerequisite completed: '+label)
for label,expected_count in [('10-native-process',32),('11-native-cleanup',101),('12-native-consumer-unit',38),('13-native-consumer-runtime',22),('14-native-startup',5),('15-native-configuration',7)]:
    require(counts('native-d0-attempt/records/'+label+'.xml.gz')['passed']==expected_count,'Actual native control rows before runtime refusal: '+label)
native_command=obj('native-d0-attempt/queue/20-native-persisted-state-refusal.result.gz')
require(native_command['ExitCode']!=0 and native_command['DescendantsReaped'],'Actual native runtime failed and its supervisor completed')
require(obj('native-d0-attempt/records/source-final.json.gz')==source['Files'] and native_cohort!=source['Cohort'],'Same task source uses separately qualified native and Release cohorts')
for path,digest in obj('native-d0-attempt/records/source-final.json.gz').items():
    require(hashlib.sha256(read('native-d0-attempt/executed-source/'+path+'.gz')).hexdigest()==digest,'Retained native source: '+path)
for path,digest in native_post['RuntimeAndQueueHelpers'].items():
    require(hashlib.sha256(read('native-d0-attempt/executed-helpers/'+Path(path).name+'.gz')).hexdigest()==digest,'Retained actual native helper: '+path)
native_results=[record['Path'] for record in index if record['Path'].startswith('native-d0-attempt/runtime/runner-interruption-') and record['Path'].endswith('/result.json.gz')]
require(len(native_results)==1,'Exactly one retained native runtime refusal')
failed_native=obj(native_results[0])
require(failed_native['Qualified'] is False and failed_native['Signal'] is None and failed_native['ChangedPersistedStateRefusedRunnerSignal'] is False and failed_native['EmergencyRecovery'] and failed_native['EmergencyClosure']['Closed'] and failed_native['SentinelUnaffected'] and failed_native['Failures'],'Native emergency closure remains unqualified')
native_ownership=[record['Path'] for record in index if record['Path'].startswith('native-d0-attempt/runtime/parties-five-memory-runtime-') and record['Path'].endswith('/ownership.json.gz')]
require(len(native_ownership)==1,'Retained actual native disposal diagnostic')
diagnostic=obj(native_ownership[0]);combined=str(diagnostic['PrimaryFailure'])+str(diagnostic['CleanupFailure'])
require('ProjectionDeliveryWriterProtocolTestLease' in combined and 'cancel' in combined.lower() and 'ObjectDisposedException' in combined and 'IServiceProvider' in combined,'Retain observed writer-lease cancellation and service-provider disposal diagnostics without asserting every readiness cause')
native_physical=obj('native-d0-attempt/parent-current-native-physical-audit.json.gz')
require(native_physical['Passed'] and (native_physical['CapturedLifetimes'],native_physical['CapturedContainerIds'],native_physical['CapturedNetworkIds'],native_physical['CapturedAnonymousVolumeNames'])==(82,6,1,2) and all(native_physical[key]==[] for key in ['RemainingExactLifetimesIncludingZombies','LateMarkerBoundLifetimes','RemainingContainerIds','RemainingNetworkIds','RemainingAnonymousVolumeNames','RemainingSessionDirectories']),'Independent exact native emergency closure is separate from acceptance')
default=obj('release-default-tenants-attempt/status.json.gz')
require(default['Qualified'] is False and default['TestAssembliesQualified'] is False and default['LongRuntimeExecuted'] is False and all(value in default['Blocker'] for value in ['NU1109','Tenants8.0.1','EventStore5.1.1','5.1.0']),'Retain current-default dependency refusal; no digest or dependency blessing')
transition=obj('pre-manifest-gated-controls/status.json.gz')
require(transition['ExitCode']==3 and transition['RuntimeExecuted'] is False and transition['TestsExecuted'] is False,'Retain preparation-gated commands as unexecuted')

# Read the final continuation receipts themselves as well as their independent audits.
expected_methods={'ordinary-wrapper':'ActualCurrentSourceMemoriesRestartsAcrossFiveRecoveryBoundaries','ordinary-child':'SelectedMemoriesRecoverAcrossProducerWorkloadRestarts','coordination-cancellation':'ActualCurrentSourceCanceledRestartCompletesOwnedCleanup','live-shutdown':'LiveShutdown_NormalCanceledAndFailedStartupStopsOwnedWorkersWithinObservationDeadlineAsync'}
for name,method in expected_methods.items():
    tests=ET.fromstring(read(verification['Lanes'][name]['Xml'])).findall('.//test')
    require(all(test.get('method')==method and test.get('result')=='Pass' for test in tests),'Actual intended continuation method: '+name)
def continuation_owner(folder,controlled):
    paths=[record['Path'] for record in index if record['Path'].startswith('current-layout/'+folder+'/parties-five-memory-runtime-') and record['Path'].endswith('/ownership.json.gz')]
    require(len(paths)==1,'One owned continuation session: '+folder)
    ownership=obj(paths[0]);prefix=paths[0].removesuffix('ownership.json.gz')
    require(ownership['ControlledCancellation'] is controlled and ownership['OwnedTopologyDisposed'] and ownership['CleanupFailure'] is None and ownership['SessionDirectoryExists'] is False,'Actual normal owned continuation disposal: '+folder)
    if controlled:require('OperationCanceledException' in ownership['PrimaryFailure'],'Original intended cancellation remains visible')
    else:require(ownership['PrimaryFailure'] is None,'Ordinary recovery has no primary failure')
    identities=obj(prefix+'executed-identities.json.gz')
    for suffix,key in [('/Hexalith.Parties.IntegrationTests.dll','WrapperSha256'),('/Hexalith.Parties.Runtime.IntegrationTests.dll','ChildSha256')]:
        require([value for name,value in identities.items() if name.endswith(suffix)]==[source['Cohort'][key]],'Continuation uses the unchanged cohort: '+key)
    command=obj('current-layout/'+folder+'/command.json.gz')
    require(command['Prepared']==obj('current-layout/cohort-paths.json.gz')['PreparedSha256'],'Continuation command uses the exact prepared path independently hashed before/after execution')
    return prefix,ownership
ordinary_prefix,ordinary_owner=continuation_owner('package-ordinary-restart',False)
restarts=obj(ordinary_prefix+'producer-restarts.json.gz')
require(len(restarts)==6 and {item['Phase'] for item in restarts}==phases,'All six actual ordinary replacements')
for item in restarts:
    require(item['OriginalExited'] and item['Healthy'] and (item['OriginalPid'],item['OriginalStart'])!=(item['ReplacementPid'],item['ReplacementStart']) and item['PreparedManifestSha256']==source['Cohort']['PreparedSha256'],'Actual distinct healthy ordinary replacement: '+item['Phase'])
    ack=obj(ordinary_prefix+'coordination/'+item['Phase']+'.ack.json.gz')
    require(ack['phase']==item['Phase'] and ack['session']==ordinary_owner['Id'] and ack['replaced'],'Actual replacement acknowledgement: '+item['Phase'])
continuation_owner('package-controlled-cancellation',True)
ordinary_audit=obj('independent-parent/parent-post-review-ordinary-audit.json.gz')
cancel_audit=obj('independent-parent/parent-post-review-cancellation-audit.json.gz')
live_audit=obj('independent-parent/parent-post-review-live-audit.json.gz')
require(all(report['Passed'] and report['DescendantsReaped'] for report in [ordinary_audit,cancel_audit,live_audit]),'Independent continuation audits passed and command descendants were reaped')
require(set(ordinary_audit['SixActualReplacementsAndAcknowledgements'])==phases and ordinary_audit['ExactProducerBirthsAbsentIncludingZombies'],'Independent actual ordinary replacement/closure audit')
require(cancel_audit['ControlledCancellationObserved'] and cancel_audit['IncompleteReplacementCleanupAndOwnedDisposalPassed'],'Independent intended cancellation audit')
require(set(live_audit['ActualLiveModes'])=={'normal','canceled','failed','cleanup-failed'} and live_audit['PassingRows']==4,'Independent all-four-live audit')
preservation=obj('historical-preservation.json')
freeze=obj('independent-parent/parent-pre-final-historical-packet-freeze.json.gz')
require(preservation['ExistingPublicFiles']==freeze['ExistingPublicFiles'] and len(preservation['ExistingPublicFiles'])==3409 and preservation['NonMetadataFiles']==3406 and preservation['AllExistingFilesMatchedBeforeExport'],'Complete unchanged older public evidence snapshot')
require(set(preservation['OnlyRootMetadataMayBeVersioned'])==set(freeze['OnlyRootMetadataMayBeVersioned'])=={'README.md','artifact-index.json.gz','SHA256SUMS.gz'},'Only three active root metadata files may be versioned')
for name in preservation['OnlyRootMetadataMayBeVersioned']:
    require(hashlib.sha256((root/'historical-root'/name).read_bytes()).hexdigest()==preservation['ExistingPublicFiles'][name],'Exact prior root metadata archived: '+name)
print(json.dumps({'Files':len(index),'ParsedJson':parsed_json,'ParsedXml':parsed_xml,'CurrentMainSigintCases':6,'FocusedControls':verification['Lanes']['focused-cleanup']['Counts']['passed']+60,'LiveModes':4,'OrdinaryAndCoordinationLanes':True,'Verified':True}))
