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
for controller in [1588010,1589905]:
    start=birth(controller)
    args=pathlib.Path('/proc',str(controller),'cmdline').read_bytes().split(b'\0')
    args=[x.decode() for x in args if x]
    assert args[1]=='-c' and "prctl(36,1,0,0,0)" in args[2]
    script,root,marker,session,named,mode,lineage=args[3:]
    root=pathlib.Path(root)
    assert root.parent==job and root.name=='hexalith-parties-topology-'+session and pathlib.Path(script)==root/'startup.py'
    assert named=='parties-interruption-named-'+session
    created=json.loads((root/'created.json').read_text())
    cid=created['Container']; child=created['Child']; child_start=birth(child)
    env=pathlib.Path('/proc',str(child),'environ').read_bytes().split(b'\0')
    assert ('PARTIES_TEST_TOPOLOGY_OWNER='+marker).encode() in env and ('PARTIES_TEST_RUNNER_OWNER='+lineage).encode() in env
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
    fd=os.pidfd_open(child)
    try:
        assert birth(child)==child_start
        signal.pidfd_send_signal(fd,signal.SIGKILL)
    finally:os.close(fd)
    deadline=time.monotonic()+5
    while birth(controller)==start and time.monotonic()<deadline:time.sleep(.02)
    assert birth(controller)!=start and birth(child)!=child_start
    docker('rm','-f','-v',cid)
    docker('rm','-f','-v',foreign[0]['Id'])
    docker('volume','rm',named)
    assert not root.is_symlink()
    shutil.rmtree(root)
    record={'Qualified':False,'EmergencyReconciliation':True,'ControllerExited':True,'WorkerExited':True,'OwnedContainerAbsent':cid not in docker('ps','-aq','--no-trunc').split(),'SentinelAndNamedVolumeRemovedByTestOwner':True,'SessionRootRemoved':not root.exists()}
    (private/('failed-early-'+session+'-after.json')).write_text(json.dumps(record,indent=2))
    records.append(record)
print(json.dumps(records))
