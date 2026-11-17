import gzip,hashlib,json,re,shutil
from pathlib import Path
private=Path(__file__).resolve().parent
root=private.parents[2]
public=root/'_bmad-output/implementation-artifacts/tests/five-runner-interruption-2026-11-16/review-2'
public.mkdir(parents=True,exist_ok=True)
active=private/'current-layout'
job=(private/'job-path.txt').read_text().strip()
current_job=(active/'job-path.txt').read_text().strip()
current_root=(active/'private-root.txt').read_text().strip()
secrets=set()
def discover(value):
    if isinstance(value,dict):
        for key,item in value.items():
            if isinstance(item,str) and item and (key.lower() in {'secret','producerrestartsecret','daprapitoken','appapitoken','jwtsigningkey','signingkey','privatekeypkcs8base64','proofprivatekey'}):secrets.add(item)
            else:discover(item)
    elif isinstance(value,list):
        for item in value:discover(item)
for path in private.rglob('*.json'):
    try:discover(json.loads(path.read_text()))
    except (ValueError,OSError):pass
def normalize(value):
    if isinstance(value,str):
        for secret in sorted(secrets,key=len,reverse=True):
            if len(secret)>10:value=value.replace(secret,'${PRIVATE_CREDENTIAL}')
        value=value.replace(current_root,'${CURRENT_REPOSITORY}').replace(current_job,'${JOB}').replace(str(private),'${PRIVATE_V3}').replace(job,'${HISTORICAL_JOB}').replace(str(root),'${REPOSITORY}').replace(str(Path.home()),'${HOME}').replace('/usr/share/dotnet','${DOTNET_ROOT}').replace('/var/lib/docker/volumes','${DOCKER_VOLUME_ROOT}')
        return re.sub(r'/tmp/hexalith-job\.[A-Za-z0-9]+','${RUNTIME}',value)
    if isinstance(value,list):return [normalize(v) for v in value]
    if isinstance(value,dict):return {normalize(k):normalize(v) for k,v in value.items()}
    return value
def safe(value,key=''):
    if isinstance(value,dict):
        if key in ['Workflow','DeletionWorkflow']:
            selected={k:v for k,v in value.items() if k in ['instanceID','workflowName','runtimeStatus','createdAt','lastUpdatedAt']}
            selected['CompletePrivateStateSha256']=hashlib.sha256(json.dumps(value,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
            return safe(selected)
        if key in ['OwnedContainer','TestSentinel']:
            return safe({k:v for k,v in value.items() if k in ['Id','Name','Image','Created','Mounts','State']}|{'Labels':value.get('Config',{}).get('Labels',{})})
        return {(k+'Sha256' if k.lower()=='secret' else k):(hashlib.sha256(str(v).encode()).hexdigest() if k.lower()=='secret' else safe(v,k)) for k,v in value.items()}
    if isinstance(value,list):return [safe(v) for v in value]
    return value
def data(value):return (json.dumps(normalize(safe(value)),indent=2)+'\n').encode()
def detail(target,value):
    target.parent.mkdir(parents=True,exist_ok=True);target.with_name(target.name+'.gz').write_bytes(gzip.compress(data(value),mtime=0))
def readable(target,value):target.write_bytes(data(value))
commands=[]
roles={'45-contracts-build':'shared-helper-owner-build','46-rebind-live-reaping':'final-rebuild','47-freeze-cohort':'cohort-freeze','48-package-process-controls':'shared-process-regressions','49-final-controls':'focused-cleanup','50-final-consumer':'focused-consumer','51-startup-ordering-controls':'startup-ordering','52-driver-controls':'queue-containment','53-persisted-state-refusal':'real-persisted-state-refusal','54-final-matrix':'original-five-pass-one-refusal-matrix','59-isolated-cross-source':'isolated-sixth-selected-case','60-ordinary-restart':'ordinary-wrapper-and-child','61-controlled-cancellation':'coordination-cancellation','62-live-shutdown':'four-live-modes'}
for command in sorted(private.glob('*.command.json')):
    name=command.name.removesuffix('.command.json');value=json.loads(command.read_text());result=private/'queue'/(name+'.result')
    log=private/(name+'.log');deferred=log.exists() and 'Deferred before runtime execution:' in log.read_text()
    commands.append({'Label':name,'Kind':'DeferredBeforeRuntime' if deferred else 'ExecutedCommand','RuntimeLaneExecuted':not deferred and any('supervised-runtime.py' in argument or 'retain-runtime-diagnostics.py' in argument for argument in value['Args']),'HistoricalRole':roles.get(name),'CurrentAcceptanceRole':None,**value,'Result':json.loads(result.read_text()) if result.exists() else None})
readable(public/'commands.json',commands)
for path in sorted(private.glob('*.json')):
    if path.name.endswith(('.command.json','.supervisor.json','-private.json')) or path.name in ['source-final.json','cohort-final.json']:continue
    detail(public/path.name,json.loads(path.read_text()))
for path in sorted(private.glob('*.log')):
    (public/(path.name+'.gz')).write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
for path in sorted(private.glob('*.xml')):
    (public/(path.name+'.gz')).write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
for directory in [private/'new-controls',private/'early-controls-final',private/'outside-repository-attempt/early-controls-final',private/'pre-live-reaping/early-controls-final']:
    for path in sorted(directory.glob('*.json')):detail(public/directory.relative_to(private)/path.name,json.loads(path.read_text()))
for lane in ['persisted-state-refusal','final-matrix','final-cross-source','ordinary-restart','controlled-cancellation','live-shutdown'] + ['outside-repository-attempt/'+name for name in ['persisted-state-refusal','final-matrix','ordinary-restart','controlled-cancellation','live-shutdown']] + ['pre-live-reaping/'+name for name in ['persisted-state-refusal','final-matrix']]:
    source=private/lane
    if not source.exists():continue
    for path in source.glob('*.json'):detail(public/lane/path.name,json.loads(path.read_text()))
    for path in source.glob('*.xml'):
        target=public/lane/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
    for path in source.glob('*.log'):detail(public/lane/(path.name+'.sha256.json'),{'Sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'Bytes':path.stat().st_size})
    for folder in source.glob('runner-interruption-*'):
        if not (folder/'result.json').exists():continue
        phase=json.loads((folder/'result.json').read_text())['Phase']
        for name in ['result.json','checkpoint.json','custody.json','external-custody.json','configuration.json']:
            if (folder/name).exists():detail(public/lane/(phase+'-'+name),json.loads((folder/name).read_text()))
        for path in folder.glob('*.xml'):
            (public/lane/(phase+'-'+path.name+'.gz')).write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
    selected=['phase.json','startup-failure.json','ownership.json','executed-identities.json','source-authority-custody.json','endpoint-binding.json','runtime.owner-component.json','runtime.provisioning.json','incomplete-replacement-quiescence.json','producer-restarts.json','runtime.authority.json','runtime.provisioning.teardown.json','runtime.provisioning.survivor.teardown.json','authoritative-persistence.json','catalog-generation-controls.json','authentication-negative-controls.json','domain-service-acl-negative-controls.json']
    for folder in source.glob('parties-five-memory-runtime-*'):
        for name in selected:
            if (folder/name).exists():detail(public/lane/folder.name/name,json.loads((folder/name).read_text()))
        for path in folder.glob('*.xml'):
            target=public/lane/folder.name/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
        for path in (folder/'producer-restart').glob('*.ack.json'):detail(public/lane/folder.name/'coordination'/path.name,json.loads(path.read_text()))
for path in (private/'outside-repository-attempt').glob('*.json'):detail(public/'outside-repository-attempt'/path.name,json.loads(path.read_text()))
source=json.loads((private/'source-final.json').read_text())
(public/'pre-current-layout').mkdir(exist_ok=True)
readable(public/'pre-current-layout/source.json',{'Authority':'Historical pre-migration source27; does not qualify current layout','Files':source,'Cohort':json.loads((private/'cohort-final.json').read_text())})
readable(public/'pre-current-layout/cohort.json',{'Authority':'Historical f472/8ad/f3e4 cohort; full later lanes refused upstream input drift','Files':json.loads((private/'cohort-final.json').read_text())})
for name,expected in source.items():
    raw=(private/'source-final'/name).read_bytes();assert hashlib.sha256(raw).hexdigest()==expected
    target=public/'pre-current-layout/executed-source'/(name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(raw,mtime=0))
historical=json.loads((private/'outside-repository-source.json').read_text())
for name,expected in historical.items():
    raw=(private/'outside-repository-attempt/executed-source'/name).read_bytes();assert hashlib.sha256(raw).hexdigest()==expected
    target=public/'outside-repository-attempt/executed-source'/(name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(raw,mtime=0))
previous=json.loads((private/'source-before-live-reaping.json').read_text())
for name,expected in previous.items():
    raw=(private/'pre-live-reaping-source'/name).read_bytes();assert hashlib.sha256(raw).hexdigest()==expected
    target=public/'pre-live-reaping/executed-source'/(name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(raw,mtime=0))
for path in sorted((private/'pre-live-reaping-helpers').glob('*.py')):
    target=public/'pre-live-reaping/executed-helpers'/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(path.read_bytes(),mtime=0))
for name in ['driver.py','driver-initial.py','test-driver.py','verify-review.py','test-packet-verifier.py','index-packet.py','audit-final-closure.py','retain-runtime.py','retain-runtime-diagnostics.py','supervised-runtime.py','runtime-gate.py','build-final-indexes.py','export-review.py','audit-public.py','audit-native-attempt.py','commitlint-preflight.py','write-final-docs.py','select-current-controls.py']:
    path=private/name
    if path.exists():shutil.copyfile(path,public/name)
shutil.copyfile(root/'artifacts/runner-interruption-private-20261116/rebind.py',public/'rebuild-and-revalidate.py')

# Export current isolated-layout receipts with their own command custody. All original
# pre-migration lanes above retain their exact outcomes and carry no current role.
current_commands=[]
current_roles=json.loads((active/'current-command-roles.json').read_text())
current_roles.update({'66-driver-controls':'historical-unadmitted-timeout-control-4pass-1error','68-admitted-queue-controls':'5-final-queue-controls-with-actual-child-admission','71-ordinary-restart':'ordinary-six-restart-wrapper-and-actual-child','72-controlled-cancellation':'coordination-cancellation','73-live-shutdown':'four-actual-live-disposal-modes','74-final-post-runtime-validation':'full-post-runtime-prepared-input-and-cohort-readback'})
accepted_labels={'56-configuration-components-build','57-configuration-component-controls','58-final-release-runtime-preparation','59-freeze-final-cohort','60-validate-final-cohort','61-package-process-controls','62-final-controls','63-consumer-unit','64-consumer-runtime','65-startup-ordering-controls','67-configuration-controls','68-admitted-queue-controls','69-persisted-state-refusal','70-final-matrix','71-ordinary-restart','72-controlled-cancellation','73-live-shutdown','74-final-post-runtime-validation'}
for command in sorted(active.glob('*.command.json')):
    name=command.name.removesuffix('.command.json');value=json.loads(command.read_text());result=active/'queue'/(name+'.result')
    log=active/(name+'.log');superseded=log.exists() and 'Superseded before execution:' in log.read_text()
    current_commands.append({'Label':name,'Kind':'SupersededBeforeBuildOrTest' if superseded else 'ExecutedCommand','HistoricalNativeRole':current_roles.get(name) if int(name[:2])<38 else None,'HistoricalRole':current_roles.get(name) if int(name[:2])>=38 and name not in accepted_labels else None,'CurrentAcceptanceRole':current_roles.get(name) if name in accepted_labels else None,'RuntimeLaneExecuted':not superseded and any('supervised-runtime.py' in item for item in value['Args']),**value,'Result':json.loads(result.read_text()) if result.exists() else None})
readable(public/'commands-current-layout.json',current_commands)
for path in sorted(active.glob('*.json')):
    if path.name.endswith(('.command.json','.supervisor.json','-private.json')) or path.name in ['source-final.json','cohort-final.json']:continue
    detail(public/'current-layout'/path.name,json.loads(path.read_text()))
for path in sorted((active/'prepared-receipts').glob('*.json')):detail(public/'current-layout/prepared-receipts'/path.name,json.loads(path.read_text()))
for path in sorted(active.glob('*.log')):
    target=public/'current-layout'/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
for path in sorted(active.glob('*.xml')):
    target=public/'current-layout'/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
for evidence in ['early-controls-final','early-controls-package-final']:
    for path in (active/evidence).glob('*.json'):detail(public/'current-layout'/evidence/path.name,json.loads(path.read_text()))
for lane in ['persisted-state-refusal','final-matrix','ordinary-restart','controlled-cancellation','live-shutdown','package-persisted-state-refusal','package-final-matrix','package-ordinary-restart','package-controlled-cancellation','package-live-shutdown']:
    source_dir=active/lane
    if not source_dir.exists():continue
    for path in source_dir.glob('*.json'):detail(public/'current-layout'/lane/path.name,json.loads(path.read_text()))
    for path in source_dir.glob('*.xml'):
        target=public/'current-layout'/lane/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
    for path in source_dir.glob('*.log'):detail(public/'current-layout'/lane/(path.name+'.sha256.json'),{'Sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'Bytes':path.stat().st_size})
    for folder in source_dir.glob('runner-interruption-*'):
        if not (folder/'result.json').exists():continue
        phase=json.loads((folder/'result.json').read_text())['Phase']
        for name in ['result.json','checkpoint.json','custody.json','external-custody.json','configuration.json']:
            if (folder/name).exists():detail(public/'current-layout'/lane/(phase+'-'+name),json.loads((folder/name).read_text()))
        for path in folder.glob('*.xml'):
            target=public/'current-layout'/lane/(phase+'-'+path.name+'.gz');target.write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
    for folder in source_dir.glob('parties-five-memory-runtime-*'):
        for name in selected:
            if (folder/name).exists():detail(public/'current-layout'/lane/folder.name/name,json.loads((folder/name).read_text()))
        for path in folder.glob('*.xml'):
            target=public/'current-layout'/lane/folder.name/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
        for path in (folder/'producer-restart').glob('*.ack.json'):detail(public/'current-layout'/lane/folder.name/'coordination'/path.name,json.loads(path.read_text()))
source=json.loads((active/'source-final.json').read_text());cohort=json.loads((active/'cohort-final.json').read_text())
readable(public/'source.json',{'Authority':'Current-layout isolated executed source; all earlier cohorts remain historical','Files':source,'Cohort':cohort,'ChildAssembly':'Hexalith.Parties.Runtime.IntegrationTests.dll','QualificationHead':json.loads((active/'overlay-metadata.json').read_text())['QualificationHead']})
readable(public/'cohort.json',{'Authority':'Unchanged isolated current-layout wrapper/prepared/runtime-child cohort','Files':cohort})
for name,expected in source.items():
    raw=(active/'source-final'/name).read_bytes();assert hashlib.sha256(raw).hexdigest()==expected
    target=public/'executed-source'/(name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(raw,mtime=0))
archive=active/'pre-configured-control'
for path in sorted(archive.glob('*.json')):detail(public/'current-layout/pre-configured-control'/path.name,json.loads(path.read_text()))
for path in sorted(archive.glob('*.xml')):
    target=public/'current-layout/pre-configured-control'/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
for path in sorted(archive.glob('*.log')):
    target=public/'current-layout/pre-configured-control'/(path.name+'.gz');target.write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
previous_source=json.loads((archive/'source-final.json').read_text())
readable(public/'current-layout/pre-configured-control/source.json',{'Authority':'Pre-configuration-control correction source only; no current acceptance','Files':previous_source,'Cohort':json.loads((archive/'cohort-final.json').read_text())})
for name,expected in previous_source.items():
    raw=(archive/'executed-source'/name).read_bytes();assert hashlib.sha256(raw).hexdigest()==expected
    target=public/'current-layout/pre-configured-control/executed-source'/(name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(raw,mtime=0))
for path in sorted((archive/'executed-helpers').glob('*.py')):
    target=public/'current-layout/pre-configured-control/executed-helpers'/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(path.read_bytes(),mtime=0))
archive=active/'native-debug-attempt'
for path in sorted(archive.glob('*.json')):detail(public/'current-layout/native-debug-attempt'/path.name,json.loads(path.read_text()))
previous_source=json.loads((archive/'source-final.json').read_text())
readable(public/'current-layout/native-debug-attempt/source.json',{'Authority':'Failed native Debug runtime; no current acceptance','Files':previous_source,'Cohort':json.loads((archive/'cohort-final.json').read_text())})
for name,expected in previous_source.items():
    raw=(archive/'executed-source'/name).read_bytes();assert hashlib.sha256(raw).hexdigest()==expected
    target=public/'current-layout/native-debug-attempt/executed-source'/(name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(raw,mtime=0))
for path in sorted((archive/'executed-helpers').glob('*.py')):
    target=public/'current-layout/native-debug-attempt/executed-helpers'/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(path.read_bytes(),mtime=0))
for archive_name in ['pre-direct-configuration-model','direct-model-initial','direct-model-diagnostics','direct-model-missing-components','queue-control-before-admission']:
    archive=active/archive_name
    if not archive.exists():continue
    for path in sorted(archive.glob('*.json')):detail(public/'current-layout'/archive_name/path.name,json.loads(path.read_text()))
    for path in sorted(archive.glob('*.xml')):
        target=public/'current-layout'/archive_name/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
    for path in sorted(archive.glob('*.log')):
        target=public/'current-layout'/archive_name/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(normalize(path.read_text()).encode(),mtime=0))
    for path in sorted(archive.glob('*.py')):
        target=public/'current-layout'/archive_name/'executed-helpers'/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(path.read_bytes(),mtime=0))
    if (archive/'source-final.json').exists():
        previous_source=json.loads((archive/'source-final.json').read_text())
        readable(public/'current-layout'/archive_name/'source.json',{'Authority':'Retained unqualified configuration-control attempt; no current acceptance','Files':previous_source})
        for name,expected in previous_source.items():
            raw=(archive/'executed-source'/name).read_bytes();assert hashlib.sha256(raw).hexdigest()==expected
            target=public/'current-layout'/archive_name/'executed-source'/(name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(raw,mtime=0))
    for path in sorted((archive/'executed-helpers').glob('*.py')):
        target=public/'current-layout'/archive_name/'executed-helpers'/(path.name+'.gz');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(gzip.compress(path.read_bytes(),mtime=0))
shutil.copyfile(private.parent/'run-runtime.py',public/'current-layout/run-runtime.py')
for path in sorted(active.glob('*.py')):
    target=public/'current-layout'/path.name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,target)
for path in sorted((private/'pre-current-layout').glob('*.json')):detail(public/'pre-current-layout'/path.name,json.loads(path.read_text()))
archive=private/'packet-before-marker-selection'
for path in sorted(archive.glob('*')):
    target=public/'packet-before-marker-selection'/(path.name+'.gz' if path.suffix=='.py' else path.name)
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_bytes(gzip.compress(path.read_bytes(),mtime=0) if path.suffix=='.py' else path.read_bytes())
for name in ['parent-final-cohort-pre-audit.json','parent-final-cohort-post-audit.json','parent-final-focused-controls-audit.json','parent-current-state-refusal-audit.json','parent-current-six-case-matrix-audit.json','parent-current-physical-audit.json','parent-current-acceptance-audit.json','parent-current-ordinary-audit.json','parent-current-cancellation-audit.json','parent-current-live-shutdown-audit.json','parent-current-live-audit.json','parent-final-staged-source-audit.json','parent-final-marker-receipt-selection-audit.json']:
    path=private.parent/name
    if path.exists():detail(public/'independent-parent'/name,json.loads(path.read_text()))
parent_script=private.parent/'parent-final-cohort-audit.py'
if parent_script.exists():
    target=public/'independent-parent'/parent_script.name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(parent_script,target)
print(public)
