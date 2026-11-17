import datetime,json,subprocess
from pathlib import Path
private=Path(__file__).resolve().parent/'current-layout'
processes=set();containers=set();networks=set();volumes=set();markers=set();lineages=set();directories=set();sessions=[]
for lane in ['package-final-matrix']:
 for directory in (private/lane).glob('runner-interruption-*'):
  result=json.loads((directory/'result.json').read_text())
  if not result['Qualified']:raise AssertionError('Unqualified current-layout main row')
  topology=json.loads((directory/'external-custody.json').read_text())
  sessions.append(topology['Id']);markers.add(topology['Marker']);lineages.add(topology['LineageMarker']);directories.add(topology['Root'])
  processes.update((item['Pid'],item['Start']) for item in topology['Processes'])
  containers.update(item['Id'] for item in topology['Containers']);networks.update(item['Id'] for item in topology['Networks']);volumes.update(item['Name'] for item in topology['AnonymousVolumes'])
def birth(pid):
 try:
  raw=Path('/proc',str(pid),'stat').read_text();fields=raw[raw.rfind(')')+2:].split()
  return None if fields[0] in ['Z','X'] else int(fields[19])
 except (FileNotFoundError,ProcessLookupError):return None
def docker(*args):return set(subprocess.check_output(['docker',*args],text=True,timeout=10).splitlines())
live=[{'Pid':pid,'Start':start} for pid,start in processes if birth(pid)==start]
zombies=[]
for pid,start in processes:
 try:
  raw=Path('/proc',str(pid),'stat').read_text();fields=raw[raw.rfind(')')+2:].split()
  if int(fields[19])==start and fields[0] in ['Z','X']:zombies.append({'Pid':pid,'Start':start,'State':fields[0]})
 except (FileNotFoundError,ProcessLookupError):pass
late=[]
for directory in Path('/proc').iterdir():
 if not directory.name.isdecimal():continue
 try:env=(directory/'environ').read_bytes().split(b'\0')
 except (FileNotFoundError,ProcessLookupError,PermissionError):continue
 if any(('PARTIES_TEST_TOPOLOGY_OWNER='+marker).encode() in env for marker in markers) or any(('PARTIES_TEST_RUNNER_OWNER='+marker).encode() in env for marker in lineages):late.append(int(directory.name))
remaining_containers=sorted(containers & docker('ps','-aq','--no-trunc'))
remaining_networks=sorted(networks & docker('network','ls','-q','--no-trunc'))
remaining_volumes=sorted(volumes & docker('volume','ls','-q'))
remaining_directories=[path for path in directories if Path(path).exists()]
result={'ObservedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
 'Scope':'Independent exact kernel/Docker readback of only the six selected main cases; no signals or removals',
 'MainSessions':sorted(sessions),'CapturedLifetimes':len(processes),'CapturedContainerIds':len(containers),'CapturedNetworkIds':len(networks),'CapturedAnonymousVolumeNames':len(volumes),
 'LiveCapturedLifetimes':live,'UnreapedCapturedZombies':zombies,'LateMarkerBoundPids':late,'RemainingContainerIds':remaining_containers,'RemainingNetworkIds':remaining_networks,
 'RemainingAnonymousVolumeNames':remaining_volumes,'RemainingSessionDirectories':remaining_directories,
 'Closed':not any([live,zombies,late,remaining_containers,remaining_networks,remaining_volumes,remaining_directories])}
(private.parent/'closure.json').write_text(json.dumps(result,indent=2))
assert len(sessions)==6 and len(set(sessions))==6 and result['Closed'],result
print(json.dumps({'Closed':result['Closed'],'CapturedLifetimes':len(processes),'CapturedContainerIds':len(containers),'CapturedNetworkIds':len(networks),'CapturedAnonymousVolumeNames':len(volumes)}))
