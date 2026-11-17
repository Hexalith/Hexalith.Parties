"""False semantic closure must fail after transport and decoded hashes are updated."""
import gzip,hashlib,json,os,shutil,subprocess,sys,tempfile
from pathlib import Path

packet=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parent
baseline=subprocess.run([sys.executable,str(packet/'verify-review.py'),str(packet)],capture_output=True,text=True,timeout=60)
assert baseline.returncode==0,baseline.stdout+baseline.stderr
results=[]
for mutation in ['false-closure','wrong-session']:
    with tempfile.TemporaryDirectory(dir=os.environ['TMPDIR']) as temporary:
        changed=Path(temporary)/'packet';shutil.copytree(packet,changed)
        closure=changed/'closure.json.gz';value=json.loads(gzip.decompress(closure.read_bytes()))
        if mutation=='false-closure':value['Closed']=False
        else:value['MainSessions'][0]='unselected-session'
        raw=(json.dumps(value,indent=2)+'\n').encode();closure.write_bytes(gzip.compress(raw,mtime=0))
        index=changed/'artifact-index.json.gz';records=json.loads(gzip.decompress(index.read_bytes()))
        entry=next(record for record in records if record['Path']=='closure.json.gz')
        entry.update({'Bytes':closure.stat().st_size,'TransportSha256':hashlib.sha256(closure.read_bytes()).hexdigest(),'DecodedBytes':len(raw),'DecodedSha256':hashlib.sha256(raw).hexdigest()})
        index.write_bytes(gzip.compress((json.dumps(records,indent=2)+'\n').encode(),mtime=0))
        sums=changed/'SHA256SUMS.gz'
        sums.write_bytes(gzip.compress(('\n'.join(hashlib.sha256(path.read_bytes()).hexdigest()+'  '+path.relative_to(changed).as_posix() for path in sorted(changed.rglob('*')) if path.is_file() and path!=sums)+'\n').encode(),mtime=0))
        run=subprocess.run([sys.executable,str(changed/'verify-review.py'),str(changed)],capture_output=True,text=True,timeout=60)
        expected='Final independent closure must be true' if mutation=='false-closure' else 'Final closure must bind exactly the six selected sessions'
        assert run.returncode!=0 and expected in run.stderr,run.stdout+run.stderr
        results.append({'Mutation':mutation,'TransportAndDecodedHashesUpdated':True,'Rejected':True,'ExpectedFailure':expected})
print(json.dumps({'PositiveBaselinePassed':True,'Passed':len(results),'Controls':results,'FalseClosureMutationRejected':True}))
