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
parsed_json=parsed_xml=0
for record in index:
    p=root/record['Path'];raw=p.read_bytes()
    require(hashlib.sha256(raw).hexdigest()==record['TransportSha256'],'Transport hash: '+record['Path'])
    decoded=gzip.decompress(raw) if p.suffix=='.gz' else raw
    require(hashlib.sha256(decoded).hexdigest()==record['DecodedSha256'],'Decoded hash: '+record['Path'])
    suffix=p.with_suffix('').suffix if p.suffix=='.gz' else p.suffix
    if suffix=='.json':json.loads(decoded);parsed_json+=1
    if suffix=='.xml':ET.fromstring(decoded);parsed_xml+=1
source=obj('source.json')
require(len(source['Files'])==39,'39 frozen implementation/current-layout source files')
require(source['ChildAssembly']=='Hexalith.Parties.Runtime.IntegrationTests.dll','Current-layout runtime assembly is required')
require(source['QualificationHead']=='0f261ccd3b461102dcac35da2e074d15a195ebb4','Isolated qualification source head')
prepared=obj('current-layout/prepared.json.gz')
require(prepared['schemaVersion']==1 and prepared['configuration']=='Release' and prepared['sdkVersion']=='5.1.0' and prepared['completed'] is True,'Completed explicit Release package preparation')
require(prepared['familyVersions']['HexalithMemoriesVersion']=='4.0.0' and prepared['familyVersions']['HexalithFrontComposerVersion']=='6.0.0','Explicit qualified consumer families')
require(len(obj('current-layout/reference-heads.json.gz'))==10,'Ten frozen root-declared source references')
require(obj('cohort.json')['Files']==source['Cohort'],'Readable source/cohort agreement')
for path,expected in source['Files'].items():require(hashlib.sha256(read('executed-source/'+path+'.gz')).hexdigest()==expected,'Executed source hash: '+path)
matrix=obj('matrix.json');rows=matrix['Cases']
selected_sessions=set()
require(matrix['SameUnchangedCohort']==source['Cohort'],'Matrix uses the frozen cohort')
suite=matrix['Suite'];require(counts(suite['Xml'])==suite['Counts']=={'total':6,'passed':6,'failed':0,'skipped':0,'errors':0,'not-run':0} and suite['PassedAsAggregate'] is True,'Complete passing current-layout suite')
original=matrix['HistoricalPreMigrationSuite'];original_assemblies=ET.fromstring(read(original['Xml'])).findall('.//assembly')
original_counts={key:sum(int(a.attrib.get(key,0)) for a in original_assemblies) for key in ['total','passed','failed','skipped','errors','not-run']}
require(original_counts==original['Counts']=={'total':6,'passed':5,'failed':1,'skipped':0,'errors':0,'not-run':0} and original['PassedAsAggregate'] is False,'Retain the original failed aggregate without forgiving arbitrary failures')
failed=[test for test in ET.fromstring(read(original['Xml'])).findall('.//test') if test.attrib.get('result')=='Fail']
require(len(failed)==1 and failed[0].attrib['name'].endswith('.SigintAtCrossSourceAuthority'),'Only the classified original cross-source refusal may be retried')
refusal=obj('final-matrix/cross-source-authority-result.json.gz')
require(refusal['Qualified'] is False and refusal['Signal'] is None and refusal['EmergencyRecovery'] and refusal['EmergencyClosure']['Closed'] and refusal['SentinelUnaffected'] and refusal['Failures'][0].startswith('System.IO.InvalidDataException: A captured runner descendant changed before interruption.'),'Preserve the exact original pre-signal refusal and completed owned emergency cleanup')
require(all(row['Xml']==suite['Xml'] for row in rows),'All six selected methods come from the passing current-layout suite')
require(original['CurrentAcceptance'] is False,'Historical suite cannot qualify current layout')
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
verification=obj('verification.json')
commands=obj('commands-current-layout.json')
selection=obj('current-controls-selection.json.gz')
def command_start(label):
    matches=[command for command in commands if command['Label']==label]
    require(len(matches)==1 and matches[0]['Result']['DescendantsReaped'],'Actual exclusive command anchor: '+label)
    return int(matches[0]['Result']['Supervisor']['Start'])
require(selection['Command']=='62-final-controls' and selection['NextCommand']=='63-consumer-unit' and not selection['FilesystemMtimeUsedAsAuthority'],'Current guard command anchors')
lower,upper=command_start('62-final-controls'),command_start('63-consumer-unit')
old_lower,old_upper=command_start('45-final-controls'),command_start('46-consumer-unit')
require((selection['LowerInclusiveStart'],selection['UpperExclusiveStart'],selection['HistoricalLowerInclusiveStart'],selection['HistoricalUpperExclusiveStart'])==(lower,upper,old_lower,old_upper) and old_lower<old_upper<lower<upper,'Exact retained kernel-birth command intervals')
all_guard_paths=[record['Path'] for record in index if record['Path'].startswith('current-layout/early-controls-package-final/consumer-gate-') and record['Path'].endswith('.json.gz') and '-custody.' not in record['Path']]
current_paths=set();historical_paths=set()
for path in all_guard_paths:
    guard=obj(path);custody=obj(path.removesuffix('.json.gz')+'-custody.json.gz')
    require(all(guard[key]==custody[key] for key in ['Gate','ConsumerPid','ConsumerStart']),'Exact marker-control outcome/custody binding')
    require(custody['Id'] in path and custody['Marker'] and custody['BackingId'] and custody['NetworkId'],'Issued marker-control session/backing custody')
    ticks=[custody['ConsumerStart'],custody['ControllerStart']]
    if all(lower<=tick<upper for tick in ticks):current_paths.add(path)
    else:
        require(all(old_lower<=tick<old_upper for tick in ticks),'Retained older guard belongs to actual45 interval')
        historical_paths.add(path)
require(len(current_paths)==len(historical_paths)==3 and current_paths=={item['Outcome'] for item in selection['SelectedGuards']} and historical_paths=={item['Outcome'] for item in selection['ExcludedHistoricalGuards']},'Exactly three actual62 controls and three retained historical45 controls')
for entries,xml_path in [(selection['SelectedGuards'],'current-layout/62-final-controls.xml.gz'),(selection['ExcludedHistoricalGuards'],'current-layout/45-final-controls.xml.gz')]:
    tests=[test for test in ET.fromstring(read(xml_path)).findall('.//test') if '.FailedConsumerTerminationRetainsBothRemovalGatesUntilExactExit(' in test.attrib['name']]
    require(len(tests)==3 and {entry['Gate'] for entry in entries}=={'application','control-plane','stale-quiescence'},'Actual three gate rows per retained command')
    for entry in entries:
        custody=obj(entry['Custody']);guard=obj(entry['Outcome'])
        require(all(entry[key]==custody[key] for key in ['Gate','ConsumerPid','ConsumerStart','ControllerPid','ControllerStart']) and entry['SessionId']==custody['Id'],'Selection rederives exact retained births/session')
        matches=[test for test in tests if test.attrib['name']==entry['XmlMethod']]
        require(len(matches)==1 and matches[0].attrib['result']=='Pass' and ('gate: "'+entry['Gate']+'"') in matches[0].attrib['name'].replace('\\"','"'),'Actual matching passing gate XML row')
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
historical_methods=[t.attrib['name'] for t in ET.fromstring(read('50-final-consumer.xml.gz')).findall('.//test')]
require(sorted(current_methods)==sorted(historical_methods) and len(current_methods)==60,'Reexecuted exact historical60 method rows in current assemblies')
require(obj('current-layout/consumer-method-set.json.gz')['Methods']==sorted(current_methods),'Readable focused selection agrees with actual XML')
require(verification['Lanes']['focused-consumer-unit']['Counts']['passed']==38 and verification['Lanes']['focused-consumer-runtime']['Counts']['passed']==22,'Exact current unit/runtime consumer split')
require(verification['Lanes']['focused-cleanup']['Counts']['passed']==88,'Preserved cleanup controls plus discovery/startup regressions')
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
pre=obj('independent-parent/parent-final-cohort-pre-audit.json.gz')
post=obj('independent-parent/parent-final-cohort-post-audit.json.gz')
require(pre['Phase']=='pre' and post['Phase']=='post','Actual independent pre/post execution readbacks')
for key in ['ActualExecutedFiles','ManifestAnchors','RuntimeAndQueueHelpers','QualificationHead','SourceFilesMatchCloneAndOriginal','FrozenRepositoryFilesVerified','RootReferenceGitlinksVerified','PreparedProjectReceiptsVerified','PreparedFilesVerified','PackageArchivesVerified','PackageBinaryEntriesVerified']:
    require(pre[key]==post[key],'Independent pre/post input/cohort agreement: '+key)
for path,digest in post['RuntimeAndQueueHelpers'].items():
    exported=path.removeprefix('review-2/') if path.startswith('review-2/') else 'current-layout/'+path
    require(hashlib.sha256(read(exported)).hexdigest()==digest,'Exported actual anchored runtime/queue helper: '+path)
require(post['ActualExecutedFiles']==source['Cohort'] and post['QualificationHead']==source['QualificationHead'],'Independent actual executed cohort/source head agreement')
require(post['SourceFilesMatchCloneAndOriginal']==39 and post['RootReferenceGitlinksVerified']==10 and post['PreparedProjectReceiptsVerified']==len(prepared['projects']),'Independent complete source/reference/receipt readback')
require(post['PreparedFilesVerified']==len(obj('current-layout/prepared-closure.json.gz')['Files']) and post['FrozenRepositoryFilesVerified']>39 and post['PackageArchivesVerified']>0 and post['PackageBinaryEntriesVerified']>0,'Independent complete prepared/repository/archive/binary closure')
if verification['FalseClosureMutationRejected']:
    controls=obj(verification['FalseClosureAndWrongSessionControls'])
    require(controls['PositiveBaselinePassed'] and controls['Passed']==2 and controls['FalseClosureMutationRejected'] and {item['Mutation'] for item in controls['Controls']}=={'false-closure','wrong-session'} and all(item['TransportAndDecodedHashesUpdated'] and item['Rejected'] for item in controls['Controls']),'Actual positive-baseline and two rehashed semantic mutation receipts')
print(json.dumps({'Files':len(index),'ParsedJson':parsed_json,'ParsedXml':parsed_xml,'CurrentMainSigintCases':6,'FocusedControls':verification['Lanes']['focused-cleanup']['Counts']['passed']+60,'LiveModes':4,'OrdinaryAndCoordinationLanes':True,'Verified':True}))
