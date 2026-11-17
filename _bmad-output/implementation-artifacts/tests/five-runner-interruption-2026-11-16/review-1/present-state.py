import datetime,json,os,subprocess
from pathlib import Path
private=Path(__file__).resolve().parent
scratch=(private/'job-path.txt').read_text().strip()
errors=[]
def docker(*args):
 try:return subprocess.check_output(['docker',*args],text=True,stderr=subprocess.PIPE,timeout=10)
 except (OSError,subprocess.SubprocessError) as failure:
  errors.append({'Operation':list(args),'Error':str(failure)[:8192]});return None
containers=[];networks=[]
ids=docker('ps','-aq','--no-trunc','--filter','label=hexalith.parties.test.owner')
if ids and ids.strip():
 raw=docker('inspect',*ids.split())
 if raw:
  for item in json.loads(raw):
   labels=item.get('Config',{}).get('Labels') or {}
   containers.append({'Id':item['Id'],'Created':item['Created'],'Running':item.get('State',{}).get('Running'),'Labels':{k:v for k,v in labels.items() if k in ['hexalith.parties.test.owner','hexalith.parties.test.session']}})
ids=docker('network','ls','-q','--no-trunc','--filter','label=hexalith.parties.test.owner')
if ids and ids.strip():
 raw=docker('network','inspect',*ids.split())
 if raw:
  for item in json.loads(raw):
   labels=item.get('Labels') or {}
   networks.append({'Id':item['Id'],'Created':item['Created'],'AttachedContainerIds':sorted((item.get('Containers') or {}).keys()),'Labels':{k:v for k,v in labels.items() if k in ['hexalith.parties.test.owner','hexalith.parties.test.session']}})
def identity(directory):
 raw=(directory/'stat').read_text();parts=raw[raw.rfind(')')+2:].split()
 return None if parts[0] in ['Z','X'] else (int(parts[19]),int(parts[1]))
markers=[];tied=[];uninspectable=changed=0
for directory in Path('/proc').iterdir():
 if not directory.name.isdecimal() or int(directory.name)==os.getpid():continue
 try:
  before=identity(directory)
  if before is None:continue
  environment=(directory/'environ').read_bytes().split(b'\0')
  names=['PARTIES_TEST_TOPOLOGY_OWNER','PARTIES_TEST_RUNNER_OWNER'];owned={}
  for name in names:
   prefix=(name+'=').encode()
   for entry in environment:
    if entry.startswith(prefix):owned[name]=entry[len(prefix):].decode(errors='replace')
  relations=[]
  try:
   cwd=os.readlink(directory/'cwd')
   if cwd==scratch or cwd.startswith(scratch+'/'):relations.append('exact-driver-scratch-cwd')
  except (FileNotFoundError,PermissionError):pass
  cmd=(directory/'cmdline').read_bytes()
  if scratch.encode() in cmd:relations.append('exact-driver-scratch-in-command')
  for name in ['TMPDIR','TMP','TEMP']:
   prefix=(name+'=').encode()
   if any(entry.startswith(prefix) and (entry[len(prefix):]==scratch.encode() or entry[len(prefix):].startswith((scratch+'/').encode())) for entry in environment):relations.append(name+'-under-exact-driver-scratch')
  if before!=identity(directory):changed+=1;continue
  if owned or relations:
   try:executable=Path(os.readlink(directory/'exe')).name
   except (FileNotFoundError,PermissionError):executable=None
   record={'Pid':int(directory.name),'Start':before[0],'Parent':before[1],'Executable':executable}
   if owned:markers.append(record|{'Markers':owned})
   if relations:tied.append(record|{'Relations':relations})
 except (FileNotFoundError,ProcessLookupError):pass
 except PermissionError:uninspectable+=1
value={'ObservedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'Scope':'Read-only current Parties ownership-label/worker-marker inventory and inspectable processes tied to the exact driver TMPDIR; may include other sessions or owners','OwnershipLabeledContainers':containers,'OwnershipLabeledNetworks':networks,'InheritedPartiesMarkerProcesses':markers,'ExactDriverScratchProcesses':tied,'UninspectableProcesses':uninspectable,'IdentityChangedDuringRead':changed,'ReadErrors':errors,'ScratchExists':Path(scratch).exists(),'OriginalFailedFocusedCustodyReconstructed':False,'AuthenticatedClosureEstablishedForFailedFocusedRuns':False,'RecoveryAuthorizedByTheseObservations':False,'NoSignalsOrMutationsPerformed':True}
(private/'present-state.json').write_text(json.dumps(value,indent=2))
print(json.dumps({k:len(value[k]) for k in ['OwnershipLabeledContainers','OwnershipLabeledNetworks','InheritedPartiesMarkerProcesses','ExactDriverScratchProcesses']}|{'ReadErrors':len(errors),'ScratchExists':value['ScratchExists']}))
