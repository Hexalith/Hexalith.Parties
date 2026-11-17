"""Keep the qualified semantic checks while selecting the fresh post-review packet."""
from pathlib import Path

base = Path(__file__).resolve().parent
text = (base / 'review-2/verify-review.py').read_text()
text = text.replace('0f261ccd3b461102dcac35da2e074d15a195ebb4', 'd0a01fbadc5c6d9df56a39e6b1e4e831df9bdfd1')
begin = text.index("original=matrix['HistoricalPreMigrationSuite']")
end = text.index("phases={'admitted-empty-mappings'", begin)
text = text[:begin] + text[end:]
begin = text.index("selection=obj('current-controls-selection.json.gz')")
end = text.index('guards=[obj(path) for path in current_paths]', begin)
text = text[:begin] + '''def command_start(label):
    matches=[command for command in commands if command['Label']==label]
    require(len(matches)==1 and matches[0]['Result']['ExitCode']==0 and matches[0]['Result']['DescendantsReaped'],'Actual exclusive command anchor: '+label)
    return int(matches[0]['Result']['Supervisor']['Start'])
lower,upper=command_start('62-final-controls'),command_start('63-consumer-unit')
current_paths={record['Path'] for record in index if record['Path'].startswith('current-layout/early-controls-package-final/consumer-gate-') and record['Path'].endswith('.json.gz') and '-custody.' not in record['Path']}
require(len(current_paths)==3,'Exactly three freshly executed marker guards')
guard_tests=[test for test in ET.fromstring(read('current-layout/62-final-controls.xml.gz')).findall('.//test') if '.FailedConsumerTerminationRetainsBothRemovalGatesUntilExactExit(' in test.attrib['name']]
require(len(guard_tests)==3,'Actual three passing gate XML rows')
gates=set()
for path in current_paths:
    guard=obj(path);custody=obj(path.removesuffix('.json.gz')+'-custody.json.gz')
    require(all(guard[key]==custody[key] for key in ['Gate','ConsumerPid','ConsumerStart']),'Exact marker-control outcome/custody binding')
    require(custody['Id'] in path and custody['Marker'] and custody['BackingId'] and custody['NetworkId'],'Issued marker-control session/backing custody')
    require(all(lower<=tick<upper for tick in [custody['ConsumerStart'],custody['ControllerStart']]),'Fresh command62-to63 kernel birth interval')
    matches=[test for test in guard_tests if ('gate: "'+guard['Gate']+'"') in test.attrib['name'].replace('\\\\"','"')]
    require(len(matches)==1 and matches[0].attrib['result']=='Pass','Actual matching passing gate XML row')
    gates.add(guard['Gate'])
require(gates=={'application','control-plane','stale-quiescence'},'All three intended guard kinds')
''' + text[end:]
text = text.replace("'50-final-consumer.xml.gz'", "'historical-consumer-selection.xml.gz'")
text = text.replace("['passed']==88", "['passed']==101")
text = text.replace("require(len(obj('current-layout/reference-heads.json.gz'))==10", "require(prepared['familyVersions']['HexalithTenantsVersion']=='8.0.0' and prepared['familyVersions']['HexalithMcpCliVersion']=='3.0.0','Exact approved compatible fallback families')\nrequire(len(obj('current-layout/reference-heads.json.gz'))==10")
text = text.replace("'current-layout/62-final-controls.xml.gz'", "obj('current-layout/current-cleanup-selection.json.gz')['Xml']")
text = text.replace("command_start('62-final-controls'),command_start('63-consumer-unit')", "command_start(obj('current-layout/current-cleanup-selection.json.gz')['Command']),command_start(obj('current-layout/current-cleanup-selection.json.gz')['NextCommand'])")
text = text.replace('Fresh command62-to63 kernel birth interval', 'Fresh selected-cleanup-to-next-runtime kernel birth interval')
text = text.replace('parent-final-cohort-', 'parent-post-review-cohort-')
text = text.replace("path.removeprefix('review-2/') if path.startswith('review-2/')", "path.removeprefix('review-final/') if path.startswith('review-final/')")
needle = "verification=obj('verification.json')"
extra = '''cleanup_rows=ET.fromstring(read(obj('current-layout/current-cleanup-selection.json.gz')['Xml'])).findall('.//test')
recovery_rows=[test for test in cleanup_rows if test.get('method')=='RecoveryReconcilesObjectsDisappearingAfterItsFinalInventory']
zombie_rows=[test for test in cleanup_rows if test.get('method')=='ClosureRejectsCapturedUnreapedZombieUntilActualReaping']
require(len(recovery_rows)==12 and len(zombie_rows)==1 and all(test.get('result')=='Pass' for test in recovery_rows+zombie_rows),'Twelve real recovery race/error rows and actual zombie/reaping regression')
'''
assert text.count(needle) == 1
text = text.replace(needle, extra + needle)
needle = "verification=obj('verification.json')"
extra = '''retired=obj('historical-prerequisites/unqualified-retirement-audit.json.gz')
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
'''
assert text.count(needle)==1
text=text.replace(needle,extra+needle)
needle = "index=obj('artifact-index.json.gz')"
extra = '''index=obj('artifact-index.json.gz')
indexed={record['Path'] for record in index}
actual_paths={path.relative_to(root).as_posix() for path in root.rglob('*') if path.is_file() and path.relative_to(root).as_posix() not in ['artifact-index.json.gz','SHA256SUMS.gz']}
require(len(indexed)==len(index) and indexed==actual_paths,'Complete unique current artifact inventory')
sums={line.split('  ',1)[1]:line.split('  ',1)[0] for line in read('SHA256SUMS.gz').decode().splitlines()}
require(set(sums)==actual_paths|{'artifact-index.json.gz'},'Complete checksum inventory')
for path,digest in sums.items():require(hashlib.sha256((root/path).read_bytes()).hexdigest()==digest,'Checksum file: '+path)
'''
assert text.count(needle) == 1
text = text.replace(needle, extra)
text = text.replace("require(hashlib.sha256(raw).hexdigest()==record['TransportSha256']", "require(len(raw)==record['Bytes'],'Transport length: '+record['Path'])\n    require(hashlib.sha256(raw).hexdigest()==record['TransportSha256']")
text = text.replace("require(hashlib.sha256(decoded).hexdigest()==record['DecodedSha256']", "require(len(decoded)==record['DecodedBytes'],'Decoded length: '+record['Path'])\n    require(hashlib.sha256(decoded).hexdigest()==record['DecodedSha256']")
needle = "print(json.dumps({'Files':len(index)"
position = text.index(needle)
extra = '''# Current qualification never adopts the externally advanced dependency defaults.
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
    match=re.search(r'kind: "([^"]+)", duringRemoval: (True|False), disappear: (True|False)',test.get('name').replace('\\\\"','"'))
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
'''
text = text[:position] + extra + text[position:]
(base / 'parent-post-review-verify.py').write_text(text)
compile(text, str(base / 'parent-post-review-verify.py'), 'exec')
print('Fresh semantic verifier generated and parsed')
