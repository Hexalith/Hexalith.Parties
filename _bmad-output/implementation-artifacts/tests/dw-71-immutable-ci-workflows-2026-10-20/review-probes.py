from pathlib import Path
import json, os, shutil, subprocess, tempfile
import xml.etree.ElementTree as ET
repo=Path('/home/administrator/projects/hexalith/parties')
out=Path('/tmp/parties-dw71-validation')
results=[]
with tempfile.TemporaryDirectory(prefix='parties-dw71-probe-') as directory:
 root=Path(directory)
 relative=Path('tests/Hexalith.Parties.Ci.Tests/bin/Debug/net10.0')
 shutil.copytree(repo/relative,root/relative,copy_function=os.link)
 shutil.copy2(repo/'global.json',root/'global.json')
 (root/'.github/workflows').mkdir(parents=True)
 original={name:(repo/'.github/workflows'/name).read_bytes() for name in ['ci.yml','codeql.yml','release.yml']}
 for name,b in original.items(): (root/'.github/workflows'/name).write_bytes(b)
 def run(label,method,expected):
  xml=out/(label+'.xml')
  cmd=['dotnet',str(root/relative/'Hexalith.Parties.Ci.Tests.dll'),'-method','*'+method,'-result-xml',str(xml)]
  with (out/(label+'.txt')).open('w') as log: p=subprocess.run(cmd,stdout=log,stderr=subprocess.STDOUT,timeout=30)
  a=ET.parse(xml).getroot().find('assembly')
  counts={k:v for k,v in a.attrib.items() if k in ['total','passed','failed','errors','skipped','not-run']}
  expected_ok=(p.returncode==0 and counts['passed']=='1') if expected=='pass' else (p.returncode!=0 and counts['failed']=='1' and counts['errors']=='0')
  if not expected_ok: raise RuntimeError((label,p.returncode,counts))
  results.append({'label':label,'command':cmd,'exit_code':p.returncode,'expected':expected,'counts':counts,'retained_log':label+'.txt'})
  print(label,p.returncode,expected,flush=True)
 run('pin-baseline','MainPushRunsCiAndCodeQlWhileReleaseRequiresManualDispatch','pass')
 for name in ['ci.yml','codeql.yml']:
  b=original[name]
  lines=b.decode().splitlines(keepends=True)
  pinned=next(line for line in lines if '    uses: Hexalith/Hexalith.Builds/' in line)
  floating=pinned.split('@')[0]+'@main\n'
  mutated=b.decode().replace(pinned,'#'+pinned+floating).encode()
  (root/'.github/workflows'/name).write_bytes(mutated)
  run('commented-pin-'+name.split('.')[0],'MainPushRunsCiAndCodeQlWhileReleaseRequiresManualDispatch','fail')
  (root/'.github/workflows'/name).write_bytes(b)
 builds=root/'references/Hexalith.Builds'
 builds.parent.mkdir(parents=True)
 subprocess.run(['git','clone','--quiet','--shared','--no-checkout',str(repo/'references/Hexalith.Builds'),str(builds)],check=True)
 (builds/'.github/workflows').mkdir(parents=True)
 (builds/'.github/workflows/domain-ci.yml').write_text('deliberately unrelated checkout contents\n')
 run('divergent-builds-checkout','CiWorkflowDelegatesToSharedDomainCiWithPartiesTestLanes','pass')
 shutil.rmtree(builds/'.git')
 subprocess.run(['git','init','--quiet',str(builds)],check=True)
 run('missing-pinned-object','CiWorkflowDelegatesToSharedDomainCiWithPartiesTestLanes','fail')
(out/'review-probes.json').write_text(json.dumps(results,indent=2)+'\n')
