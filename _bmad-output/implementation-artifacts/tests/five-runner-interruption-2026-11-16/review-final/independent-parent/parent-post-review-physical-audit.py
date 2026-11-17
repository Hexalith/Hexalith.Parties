from pathlib import Path
import datetime
import json
import subprocess

base = Path(__file__).resolve().parent
packet = base / 'review-final/current-layout/package-final-matrix'
records = [json.loads(path.read_text()) for path in packet.glob('runner-interruption-*/external-custody.json')]
assert len(records) == 6
lifetimes = {(p['Pid'], p['Start']) for record in records for p in record['Processes']}
containers = {p['Id'] for record in records for p in record['Containers']}
networks = {p['Id'] for record in records for p in record['Networks']}
volumes = {p['Name'] for record in records for p in record['AnonymousVolumes']}
markers = {record['Marker'] for record in records}
lineages = {record['LineageMarker'] for record in records}

def kernel(pid):
    try:
        raw = Path('/proc', str(pid), 'stat').read_text()
        fields = raw[raw.rfind(')') + 2:].split()
        return int(fields[19]), fields[0]
    except (FileNotFoundError, ProcessLookupError):
        return None

remaining = []
for pid, birth in sorted(lifetimes):
    observed = kernel(pid)
    if observed is not None and observed[0] == birth:
        remaining.append({'Pid': pid, 'Birth': birth, 'State': observed[1]})
late = []
for path in Path('/proc').iterdir():
    if not path.name.isdigit():
        continue
    try:
        before = kernel(int(path.name))
        env = (path / 'environ').read_bytes().split(b'\0')
        match = any(('PARTIES_TEST_TOPOLOGY_OWNER=' + value).encode() in env for value in markers)
        match |= any(('PARTIES_TEST_RUNNER_OWNER=' + value).encode() in env for value in lineages)
        if match and before is not None and kernel(int(path.name)) == before:
            late.append({'Pid': int(path.name), 'Birth': before[0], 'State': before[1]})
    except (FileNotFoundError, ProcessLookupError, PermissionError):
        pass

def docker(*args):
    result = subprocess.run(['docker', *args], capture_output=True, text=True, timeout=20, check=True)
    return set(result.stdout.split())

remaining_containers = sorted(containers & docker('ps', '-aq', '--no-trunc'))
remaining_networks = sorted(networks & docker('network', 'ls', '-q', '--no-trunc'))
remaining_volumes = sorted(volumes & docker('volume', 'ls', '-q'))
remaining_roots = [record['Id'] for record in records if Path(record['Root']).exists()]
report = {
    'ObservedAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'MainSessions': sorted(record['Id'] for record in records),
    'CapturedLifetimes': len(lifetimes), 'CapturedContainerIds': len(containers),
    'CapturedNetworkIds': len(networks), 'CapturedAnonymousVolumeNames': len(volumes),
    'RemainingExactLifetimesIncludingZombies': remaining, 'LateMarkerBoundLifetimes': late,
    'RemainingContainerIds': remaining_containers, 'RemainingNetworkIds': remaining_networks,
    'RemainingAnonymousVolumeNames': remaining_volumes, 'RemainingSessionDirectories': remaining_roots,
}
report['Passed'] = not any((remaining, late, remaining_containers, remaining_networks, remaining_volumes, remaining_roots))
(base / 'parent-post-review-physical-audit.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
assert report['Passed']
