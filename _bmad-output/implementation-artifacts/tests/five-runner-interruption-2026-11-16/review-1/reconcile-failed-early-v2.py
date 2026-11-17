import json,os,pathlib,signal,subprocess,time,shutil
private=pathlib.Path(__file__).resolve().parent
job=pathlib.Path(os.environ['TMPDIR'])
records=[]
def docker(*args):return subprocess.check_output(['docker',*args],text=True,timeout=15)
def birth(pid):
    try:
        s=pathlib.Path('/proc',str(pid),'stat').read_text(); f=s[s.rfind(')')+2:].split()
        return None if f[0] in ('Z','X') else int(f[19])
    except FileNotFoundError:return None
# These exact externally issued arguments were independently read from the two
# still-live scoped controller command lines after the failed test. Both controllers
# subsequently completed their natural child wait; no signal is sent here.
for controller,session,marker,lineage,mode in [
    (1588010,'f6b8a60f59e0','81f10c4ea65d4072ba7b7ecec03f0cea','edf95e38626f4e10a668fa2287e999b2','crash'),
    (1589905,'7f264a5b156f','62db61b763cd47638c0eb77352007e42','a90588d7aea54deaa66bd27807ffdbfe','cancel')]:
    assert birth(controller) is None
    start=None
    root=job/('hexalith-parties-topology-'+session)
    named='parties-interruption-named-'+session
    assert not root.is_symlink()
    created=json.loads((root/'created.json').read_text())
    cid=created['Container']; child=created['Child']; child_start=birth(child)
    assert child_start is None
    for candidate in pathlib.Path('/proc').iterdir():
        if not candidate.name.isdecimal():continue
        try:env=(candidate/'environ').read_bytes().split(b'\0')
        except (FileNotFoundError,ProcessLookupError,PermissionError):continue
        assert ('PARTIES_TEST_TOPOLOGY_OWNER='+marker).encode() not in env
        assert ('PARTIES_TEST_RUNNER_OWNER='+lineage).encode() not in env
    info=json.loads(docker('inspect',cid))[0]
    assert info['Id']==cid and info['Config']['Labels']['hexalith.parties.test.owner']==marker and info['Config']['Labels']['hexalith.parties.test.session']==session
    allids=docker('ps','-aq','--no-trunc').split()
    foreign=[]
    for other in allids:
        if other==cid:continue
        value=json.loads(docker('inspect',other))[0]
        if any(x.get('Name')==named and x['Destination']=='/foreign' for x in value['Mounts']):foreign.append(value)
    assert len(foreign)==1
    assert foreign[0]['Config']['Image']==info['Config']['Image']=='redis:7.4-alpine'
    assert not foreign[0]['Config'].get('Labels',{}).get('hexalith.parties.test.owner')
    record={'Qualified':False,'OriginalFailure':'volume JSON whitespace projection mismatch','Controller':controller,'ControllerBirthObservedAfterFailure':start,'OriginalControllerBirthAtLaunchAvailable':False,'Child':child,'ChildStartObservedUnderExactMarker':child_start,'Session':session,'Root':str(root),'Marker':marker,'Lineage':lineage,'OwnedContainer':info,'TestSentinel':foreign[0],'NamedVolume':json.loads(docker('volume','inspect',named))[0]}
    (private/('failed-early-'+session+'-before.json')).write_text(json.dumps(record,indent=2))
    anonymous=[m['Name'] for value in [info,*foreign] for m in value['Mounts'] if m['Type']=='volume' and m['Name']!=named]
    docker('rm','-f','-v',cid)
    docker('rm','-f','-v',foreign[0]['Id'])
    docker('volume','rm',named)
    assert not any(name in docker('volume','ls','-q').split() for name in anonymous)
    assert not root.is_symlink()
    shutil.rmtree(root)
    record={'Qualified':False,'EmergencyReconciliation':True,'ControllerExitedNaturallyAfterReapingWorker':True,'WorkerExitedNaturally':True,'SignalSent':False,'OriginalWorkerBirthAtLaunchUnavailable':True,'AnonymousVolumesAbsent':anonymous,'OwnedContainerAbsent':cid not in docker('ps','-aq','--no-trunc').split(),'SentinelAndNamedVolumeRemovedByTestOwner':True,'SessionRootRemoved':not root.exists()}
    (private/('failed-early-'+session+'-after.json')).write_text(json.dumps(record,indent=2))
    records.append(record)
print(json.dumps(records))
