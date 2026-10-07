from pathlib import Path
import subprocess,json,hashlib,datetime,xml.etree.ElementTree as E,tempfile
base=Path('/home/administrator/projects/hexalith')
packet=base/'parties/_bmad-output/implementation-artifacts/tests/http-qualification-2026-10-07/review-final'
packet.mkdir(parents=True,exist_ok=True)
(packet/'verification-driver.py').write_text(Path(__file__).read_text())
(base/'parties/artifacts').mkdir(exist_ok=True)
artifacts=Path(tempfile.mkdtemp(prefix='story54-http-review-final-',dir=base/'parties/artifacts'))
commands=[]
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def source_snapshot():
 result={}
 for repo in ['parties','eventstore','platform']:
  for folder in ['src','tests','eng']:
   for p in (base/repo/folder).rglob('*'):
    if p.is_file() and p.suffix in ['.cs','.csproj','.props','.targets','.json','.yaml','.yml','.ps1','.sh'] and not any(x in ['bin','obj','artifacts'] for x in p.relative_to(base/repo/folder).parts): result[str(p)]=sha(p)
  for name in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','.gitmodules','.editorconfig','.gitattributes']:
   p=base/repo/name
   if p.exists():result[str(p)]=sha(p)
 return result
initial=source_snapshot();(packet/'source-before.json').write_text(json.dumps(initial,indent=2)+'\n')
initial_heads={repo:subprocess.check_output(['git','rev-parse','HEAD'],cwd=base/repo,text=True).strip() for repo in ['agents','parties','eventstore','platform']}
for repo in initial_heads:
 (packet/f'{repo}-status-before.txt').write_text(subprocess.check_output(['git','status','--short','--branch'],cwd=base/repo,text=True))
 (packet/f'{repo}-diff-before.patch').write_text(subprocess.check_output(['git','diff','--ignore-space-at-eol'],cwd=base/repo,text=True))
def run(argv,cwd,log):
 print('Running:',log,flush=True)
 record={'argv':list(map(str,argv)),'workingDirectory':str(cwd),'combinedOutput':str(packet/log),'startedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
 with (packet/log).open('w') as stream:r=subprocess.run(record['argv'],cwd=cwd,stdout=stream,stderr=subprocess.STDOUT)
 record['exitCode']=r.returncode;record['outputSha256']=sha(packet/log);commands.append(record)
 (packet/'commands.json').write_text(json.dumps(commands,indent=2)+'\n')
 if r.returncode:
  print((packet/log).read_text()[-4000:],flush=True);raise SystemExit(r.returncode)
 print('Passed:',log,flush=True)
run(['pwsh','-NoProfile','-File','eng/verify-ext-parties-1.ps1','-Mode','Local','-EventStoreRoot',base/'eventstore','-PlatformRoot',base/'platform','-EvidenceDirectory',packet/'local-matrix','-ArtifactsDirectory',artifacts,'-MemoriesRoot','/tmp/parties-http-review-final-no-memories'],base/'parties','local-runner.log')
party_assembly=artifacts/'bin/Hexalith.Parties.Tests/debug/Hexalith.Parties.Tests.dll'
run(['dotnet',party_assembly,'-class','*DegradedResponseMiddlewareTests','-class','*HealthEndpointIntegrationTests','-class','*ServiceDefaultsCompatibilityTests','-result-xml',packet/'host-health.xml'],base/'parties','host-health.log')
run(['dotnet',party_assembly,'-method','*Program_SourceContainsOnlyActorHostMappingsAndDocumentedDaprInternalExceptions','-result-xml',packet/'architecture.xml'],base/'parties','architecture.log')
run(['dotnet',artifacts/'bin/Hexalith.EventStore.Client.Tests/debug/Hexalith.EventStore.Client.Tests.dll','-class','*LegacyCommandReplayInputTests','-class','*DomainProcessorStateRehydratorTests','-result-xml',packet/'shared-replay.xml'],base/'eventstore','shared-replay.log')
flags=['-c','Debug','--artifacts-path',artifacts,'-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false',f'-p:HexalithEventStoreRoot={base}/eventstore',f'-p:HexalithCommonsRoot={base}/parties/references/Hexalith.Commons','-p:HexalithMemoriesRoot=/tmp/parties-http-review-final-no-memories','-p:NuGetAudit=false','-m:1']
run(['dotnet','build','tests/Hexalith.Platform.Custody.Tests/Hexalith.Platform.Custody.Tests.csproj',*flags],base/'platform','custody-build.log')
run(['dotnet',artifacts/'bin/Hexalith.Platform.Custody.Tests/debug/Hexalith.Platform.Custody.Tests.dll','-result-xml',packet/'custody.xml'],base/'platform','custody.log')
final=source_snapshot();(packet/'source-after.json').write_text(json.dumps(final,indent=2)+'\n')
drift={p:{'before':initial.get(p),'after':final.get(p)} for p in initial.keys()|final.keys() if initial.get(p)!=final.get(p)}
artifact_hashes={str(p):sha(p) for p in artifacts.rglob('*') if p.is_file() and p.suffix in ['.dll','.pdb','.json']}
(packet/'artifact-manifest.json').write_text(json.dumps(artifact_hashes,indent=2)+'\n')
results={};distinct=set()
for xml in packet.rglob('*.xml'):
 root=E.parse(xml).getroot(); tests=root.findall('.//test'); assert tests and all(t.get('result')=='Pass' for t in tests),str(xml)
 assembly=root.find('.//assembly'); project=Path(assembly.get('name')).stem
 for t in tests:distinct.add((project,t.get('name')))
 results[str(xml.relative_to(packet))]={'passes':len(tests),'sha256':sha(xml),'assembly':assembly.get('name')}
builds={}
for log in list((packet/'local-matrix').glob('*-build.log'))+[packet/'custody-build.log']:
 text=log.read_text(); assert '0 Warning(s)' in text and '0 Error(s)' in text,str(log)
 builds[str(log.relative_to(packet))]={'warnings':0,'errors':0,'sha256':sha(log)}
summary={'builds':builds,'startedHeads':initial_heads,'finishedHeads':{repo:subprocess.check_output(['git','rev-parse','HEAD'],cwd=base/repo,text=True).strip() for repo in initial_heads},'artifactsDirectory':str(artifacts),'sourceFiles':len(final),'sourceDrift':drift,'xml':results,'distinctPasses':len(distinct),'sourcesSha256':sha(packet/'source-after.json'),'artifactManifestSha256':sha(packet/'artifact-manifest.json'),'commandsSha256':sha(packet/'commands.json')}
for repo in initial_heads:(packet/f'{repo}-status-after.txt').write_text(subprocess.check_output(['git','status','--short','--branch'],cwd=base/repo,text=True))
(packet/'evidence.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps({'distinctPasses':len(distinct),'sourceDrift':drift,'xmlCounts':{k:v['passes'] for k,v in results.items()}},indent=2),flush=True)
assert not drift,'Source changed during final verification; retain this attempt and reconcile.'
