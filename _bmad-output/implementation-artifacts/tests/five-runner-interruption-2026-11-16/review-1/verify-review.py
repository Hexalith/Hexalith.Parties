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
def counts(path,allow_failure=False):
    assemblies=ET.fromstring(read(path)).findall('.//assembly')
    result={k:sum(int(a.attrib.get(k,0)) for a in assemblies) for k in ['total','passed','failed','skipped','errors','not-run']}
    require(result['total']>0 and (result['passed']==result['total'] or allow_failure and result['passed']==4 and result['failed']==1) and all(result[k]==0 for k in ['skipped','errors','not-run']),f'Failed/empty XML: {path}')
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
require(len(source['Files'])==24,'24 frozen implementation/context source files')
require(obj('cohort.json')['Files']==source['Cohort'],'Readable source/cohort agreement')
for path,expected in source['Files'].items():require(hashlib.sha256(read('executed-source/'+path+'.gz')).hexdigest()==expected,'Executed source hash: '+path)
matrix=obj('matrix.json');rows=matrix['Cases']
require(matrix['SameUnchangedCohort']==source['Cohort'],'Matrix uses the frozen cohort')
phases={'admitted-empty-mappings','output-known','accepted-deletion','same-uri-refresh','cross-source-authority','bounded-legacy-discovery'}
require(len(rows)==6 and {r['Phase'] for r in rows}==phases,'Six distinct required phases')
for row in rows:
    result=obj(row['Result']);checkpoint=obj(row['Checkpoint']);custody=obj(row['ExternalCustody']);configuration=obj(row['Configuration'])
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
    require([v for k,v in hashes.items() if k.endswith('/Hexalith.Parties.Tests.dll')]==[source['Cohort']['ChildSha256']],'One final runtime child: '+phase)
    instant=lambda value:datetime.datetime.fromisoformat(value.replace('Z','+00:00'))
    observed=instant(observation['ObservedAtUtc']);created=instant(checkpoint['CreatedUtc']);expires=instant(observation['ExpiresAtUtc'])
    require(observed<=created<expires and (created-observed).total_seconds()<=15,'Fresh published frozen observation: '+phase)
    ownership=obj(row['RuntimePrefix']+'/ownership.json.gz')
    require(ownership['OwnedTopologyDisposed'] and ownership['CleanupFailure'] is None and not ownership['SessionDirectoryExists'],'Fixture disposal: '+phase)
    require('CanceledException' in ownership['PrimaryFailure'],'Interruption remains failure: '+phase)
    if phase=='admitted-empty-mappings':
        pending=observation['PendingReplacement'];quiescence=obj(row['RuntimePrefix']+'/incomplete-replacement-quiescence.json.gz')
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
verification=obj('verification.json')
guard_paths=[record['Path'] for record in index if record['Path'].startswith('early-controls-final-admission/consumer-gate-') and record['Path'].endswith('.json.gz') and '-custody.' not in record['Path']]
guards=[obj(path) for path in guard_paths]
require(len(guards)==3 and {g['Gate'] for g in guards}=={'application','control-plane','stale-quiescence'},'Three actual marker-guard controls')
for guard in guards:
    require(all(guard[k] for k in ['ParentExitedBeforeDiscovery','FailedTerminationPreserved','BackingAndControlPlaneRetained','BuilderRetainedUntilExactExit','ExactConsumerExitedBeforeRemoval','SuccessfulRetry','ForeignSurvived']),'Marker retention/exit/survivor proof: '+guard['Gate'])
    require(guard['ActualFixtureDisposalExercised']==(guard['Gate']!='control-plane'),'Actual application/builder disposal caller')
startup_xml=verification['StartupAdmissionControls']['Xml']
require(counts(startup_xml)['passed']==5,'Exact reproduction and both admission orderings')
startup_tests=ET.fromstring(read(startup_xml)).findall('.//test')
require(len([t for t in startup_tests if 'ConfiguredDaprPath_' in t.attrib['name']])==3 and len([t for t in startup_tests if 'StartupAdmissionAndCleanupPreserveBothOrderings' in t.attrib['name']])==2,'Both startup-admission orderings and exact three-row reproduction')
for name,lane in verification['Lanes'].items():
    actual=counts(lane['Xml'],lane.get('AllowRetainedFailure',False));require(actual==lane['Counts'],'Lane counts: '+name)
require(verification['Lanes']['focused-consumer']['Counts']['passed']==60,'60 consumer controls')
require(verification['Lanes']['focused-cleanup']['Counts']['passed']==80,'80 cleanup/signal/fixture controls')
require(verification['Lanes']['ordinary-wrapper']['Counts']['passed']==1 and verification['Lanes']['ordinary-child']['Counts']['passed']==1,'Ordinary wrapper/actual child')
require(verification['Lanes']['coordination-cancellation']['Counts']['passed']==1,'Coordination cancellation')
live=ET.fromstring(read(verification['Lanes']['live-shutdown']['Xml']))
import re
display_names=[re.sub(r'\\+(?=")','',t.attrib['name']) for t in live.findall('.//test') if 'mode: ' in t.attrib.get('name','')]
modes={re.search(r'mode: "([^"]+)"',name).group(1) for name in display_names}
require(modes=={'normal','canceled','failed','cleanup-failed'},'All four intended live modes')
require(verification['ImplementationComplete'] and verification['AllJobsAwaited'] and verification['ScratchRemoved'],'Completed handoff')
print(json.dumps({'Files':len(index),'ParsedJson':parsed_json,'ParsedXml':parsed_xml,'CurrentMainSigintCases':6,'FocusedControls':140,'LiveModes':4,'OrdinaryAndCoordinationLanes':True,'Verified':True}))
