import json, re, subprocess
from pathlib import Path
root=Path('/home/administrator/projects/hexalith/parties')
evidence=Path('/tmp/ext-parties-1-branch-b-policy-20261008')
version=(evidence/'final-local/eventstore-version.log').read_text().strip()
flags=['-c','Debug','--artifacts-path',str(evidence/'final-artifacts'),'-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-p:HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore','-p:HexalithCommonsRoot=/home/administrator/projects/hexalith/parties/references/Hexalith.Commons','-p:HexalithMemoriesRoot='+str(evidence/'optional-memories-package-mode'),'-p:NuGetAudit=false','-m:1','-p:HexalithEventStoreVersion='+version]
text=(root/'eng/verify-ext-parties-1.ps1').read_text()
lanes=re.findall(r"Root=\$PartiesRoot; Project='([^']+)'; Classes=@\(([^)]+)\); Matrix='([^']+)'",text)
assert len(lanes)==6, lanes
import os
env=os.environ.copy(); env['HEXALITH_PARTIES_SOURCE_ROOT']=str(root)
results=[]
manifest=json.loads((evidence/'final-local/local-evidence.json').read_text())
for project,patterns,matrix in lanes:
    classes=re.findall(r"'([^']+)'",patterns)
    build=['dotnet','build',str(root/'tests'/project/f'{project}.csproj')]+flags
    build_log=evidence/'final-local'/f'{project}-build.log'
    with build_log.open('w') as output: complete=subprocess.run(build,cwd=root,stdout=output,stderr=subprocess.STDOUT)
    entry={'project':project,'buildCommand':build,'buildExitCode':complete.returncode,'buildLog':str(build_log)}
    if complete.returncode:
        results.append(entry); print(json.dumps(entry),flush=True); continue
    assembly=evidence/'final-artifacts/bin'/project/'debug'/f'{project}.dll'
    test=['dotnet',str(assembly)]
    for name in classes: test+=['-class',name]
    xml_log=evidence/'final-local'/f'{project}-tests.xml'
    test+=['-result-xml',str(xml_log)]
    test_log=evidence/'final-local'/f'{project}-tests.log'
    with test_log.open('w') as output: complete=subprocess.run(test,cwd=root,env=env,stdout=output,stderr=subprocess.STDOUT)
    summary=re.findall(r'.*Total: \d+, Errors: \d+, Failed: \d+, Skipped: \d+, Not Run: \d+.*',test_log.read_text())
    entry.update({'testCommand':test,'testExitCode':complete.returncode,'xmlLog':str(xml_log),'testLog':str(test_log),'summary':summary[-1] if summary else None,'testEnvironment':{'HEXALITH_PARTIES_SOURCE_ROOT':str(root)}})
    results.append(entry); print(json.dumps(entry),flush=True)
    if complete.returncode==0:
        manifest.append({'Project':project,'Matrix':matrix,'Classes':classes,'BuildLog':str(build_log),'TestLog':str(test_log),'XmlLog':str(xml_log),'Summary':entry['summary']})
        (evidence/'final-local/selected-local-evidence.json').write_text(json.dumps(manifest,indent=2)+'\n')
    (evidence/'remaining-lane-commands.json').write_text(json.dumps(results,indent=2)+'\n')
if len(results)!=6 or any(entry['buildExitCode'] or entry.get('testExitCode',1) for entry in results): raise SystemExit(1)
