import datetime, fnmatch, hashlib, json, re, subprocess
from pathlib import Path
from xml.etree import ElementTree

root=Path('/home/administrator/projects/hexalith/parties')
evidence=Path('/tmp/ext-parties-1-branch-b-policy-20261008')
source_before=json.loads((evidence/'source-before.json').read_text())
source_after={'capturedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'owners':{}}
drift=[]
for owner,original in source_before['owners'].items():
    directory=Path(original['root'])
    files=[]
    for part in ['src','tests','samples','eng']:
        if not (directory/part).exists(): continue
        for file in sorted((directory/part).rglob('*')):
            if not file.is_file() or file.suffix not in ['.cs','.csproj','.props','.targets','.ps1','.json'] or set(file.relative_to(directory).parts)&{'bin','obj','node_modules'}: continue
            files.append({'path':str(file),'sha256':hashlib.sha256(file.read_bytes()).hexdigest()})
    for filename in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','.editorconfig']:
        file=directory/filename
        if file.is_file(): files.append({'path':str(file),'sha256':hashlib.sha256(file.read_bytes()).hexdigest()})
    source_after['owners'][owner]={'root':str(directory),'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=directory,text=True).strip(),'status':subprocess.check_output(['git','status','--short'],cwd=directory,text=True).splitlines(),'files':files}
    earlier={item['path']:item['sha256'] for item in original['files']}
    later={item['path']:item['sha256'] for item in files}
    for path in sorted(earlier.keys()|later.keys()):
        if earlier.get(path)!=later.get(path): drift.append({'owner':owner,'path':path,'beforeSha256':earlier.get(path),'afterSha256':later.get(path)})
(evidence/'source-after.json').write_text(json.dumps(source_after,indent=2)+'\n')
manifest=json.loads((evidence/'local/local-evidence.json').read_text())
assert len(manifest)==11, len(manifest)
version=(evidence/'local/eventstore-version.log').read_text().strip()
base_flags=['-c','Debug','--artifacts-path',str(evidence/'artifacts'),'-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-p:HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore','-p:HexalithCommonsRoot=/home/administrator/projects/hexalith/parties/references/Hexalith.Commons','-p:HexalithMemoriesRoot='+str(evidence/'optional-memories-package-mode'),'-p:NuGetAudit=false','-m:1','-p:HexalithEventStoreVersion='+version]
lanes=[]
for item in manifest:
    project=item['Project']
    owner='EventStore' if project.startswith('Hexalith.EventStore.') else 'Platform' if project.startswith('Hexalith.Platform.') else 'Parties'
    owner_root=Path(source_before['owners'][owner]['root'])
    xml=Path(item['XmlLog'])
    tests=ElementTree.parse(xml).getroot().findall('.//test')
    assert tests and all(test.get('result')=='Pass' for test in tests)
    counts={pattern:sum(fnmatch.fnmatchcase(test.get('type',''),pattern) for test in tests) for pattern in item['Classes']}
    assert all(counts.values()), counts
    summary=re.search(r'Total: (\d+), Errors: (\d+), Failed: (\d+), Skipped: (\d+), Not Run: (\d+)',item['Summary'])
    assert summary and int(summary[1])==len(tests) and all(int(summary[group])==0 for group in range(2,6))
    build_text=Path(item['BuildLog']).read_text()
    assert 'Build succeeded.' in build_text and re.search(r'0 Warning\(s\)',build_text) and re.search(r'0 Error\(s\)',build_text)
    assembly=evidence/'artifacts/bin'/project/'debug'/f'{project}.dll'
    command=['dotnet',str(assembly)]
    for pattern in item['Classes']: command+=['-class',pattern]
    command+=['-result-xml',str(xml)]
    lanes.append({'project':project,'matrix':item['Matrix'],'passed':len(tests),'requiredClasses':counts,'buildCommand':['dotnet','build',str(owner_root/'tests'/project/f'{project}.csproj')]+base_flags,'testCommand':command,'testEnvironment':{'HEXALITH_PARTIES_SOURCE_ROOT':str(root)},'buildLogSha256':hashlib.sha256(Path(item['BuildLog']).read_bytes()).hexdigest(),'testLogSha256':hashlib.sha256(Path(item['TestLog']).read_bytes()).hexdigest(),'xmlSha256':hashlib.sha256(xml.read_bytes()).hexdigest(),'assemblySha256':hashlib.sha256(assembly.read_bytes()).hexdigest(),'pdbSha256':hashlib.sha256(assembly.with_suffix('.pdb').read_bytes()).hexdigest()})
required=sum(len(lane['requiredClasses']) for lane in lanes)
assert required==41,required
assert lanes[4]['requiredClasses']['*IdentityHistoryCustodyPolicyTests']==56
result={'qualification':'LocalOnly','completeLocalExitCode':0,'localCommand':['pwsh','-NoProfile','-File','eng/verify-ext-parties-1.ps1','-Mode','Local','-EventStoreRoot','/home/administrator/projects/hexalith/eventstore','-PlatformRoot','/home/administrator/projects/hexalith/platform','-EvidenceDirectory',str(evidence/'local'),'-ArtifactsDirectory',str(evidence/'artifacts'),'-MemoriesRoot',str(evidence/'optional-memories-package-mode')],'sourceVersion':version,'ownerLaneCount':len(lanes),'requiredClassCount':required,'passed':sum(lane['passed'] for lane in lanes),'lanes':lanes,'sourceDrift':drift,'sourceFingerprintScope':'4020 source/build/config files captured before and rescanned after; source drift is not compiled-source proof','incomplete':['Production IIdentityHistoryCustody provider and installed independent history/event/snapshot custody','Irreversible all-copy destruction receipts and nonrollback restore qualification','Approved successor continuation after predecessor expiry/destruction','Complete installed P-01-P-10 persisted-state/restart/restore/failure-injection probes and exact accepted target/command']}
(evidence/'verification-manifest.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'lanes':len(lanes),'classes':required,'passed':result['passed'],'sourceDriftCount':len(drift),'sourceDrift':drift}))
