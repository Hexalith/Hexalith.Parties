import os,socket,subprocess,tempfile,pathlib,urllib.request,time,json,sys,signal,argparse,hashlib
parser=argparse.ArgumentParser(description='Run selected Memories controls on disposable owned loopback infrastructure.')
parser.add_argument('--runtime-root', type=pathlib.Path, required=True)
parser.add_argument('--owner-root', type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[4]/'references/Hexalith.Memories')
parser.add_argument('--output', type=pathlib.Path, required=True)
parser.add_argument('--scope', choices=['focused','affected'], default='affected')
options=parser.parse_args()
root=options.owner_root.resolve()
work=options.output.resolve()
work.mkdir(parents=True, exist_ok=False)
runtime=options.runtime_root.resolve()
def port():
 with socket.socket() as s:s.bind(('127.0.0.1',0));return s.getsockname()[1]
pp,pr,ph,sp,se,sc,sh=[port() for _ in range(7)]
commands=[[str(runtime/'dapr/placement'),'--port',str(pp),'--listen-address','127.0.0.1','--initial-cluster',f'dapr-placement-0=127.0.0.1:{pr}','--healthz-listen-address','127.0.0.1','--healthz-port',str(ph),'--enable-metrics=false'],[str(runtime/'dapr/scheduler'),'--port',str(sp),'--listen-address','127.0.0.1','--override-broadcast-host-port',f'127.0.0.1:{sp}','--etcd-initial-cluster',f'dapr-scheduler-server-0=http://127.0.0.1:{se}','--etcd-client-port',str(sc),'--etcd-data-dir',str(work/'scheduler-data'),'--healthz-listen-address','127.0.0.1','--healthz-port',str(sh),'--enable-metrics=false']]
procs=[];receipts=[]
try:
 for i,args in enumerate(commands):
  log=open(work/f'control-{i}.log','w'); p=subprocess.Popen(args,stdout=log,stderr=subprocess.STDOUT,start_new_session=True);procs.append(p)
  deadline=time.monotonic()+45
  while True:
   if p.poll() is not None:raise RuntimeError('Control plane failed; see private log')
   try:
    with urllib.request.urlopen(f'http://127.0.0.1:{[ph,sh][i]}/healthz',timeout=1) as r:
     if r.status==200:break
   except Exception:pass
   if time.monotonic()>deadline:raise RuntimeError('Control readiness timeout')
   time.sleep(.1)
 rp,fp=port(),port()
 for label,binary,module,listen in [('redis',str(runtime/'native-redis/redis-server'),str(runtime/'native-redis/redisearch.so'),rp),('falkor',str(runtime/'native-falkor/redis-server'),str(runtime/'native-falkor/falkordb.so'),fp)]:
  data=work/label;data.mkdir()
  args=[binary,'--port',str(listen),'--bind','127.0.0.1','--dir',str(data),'--save','','--appendonly','yes','--loadmodule',module]
  log=open(work/(label+'.log'),'w');p=subprocess.Popen(args,stdout=log,stderr=subprocess.STDOUT,start_new_session=True);procs.append(p)
  deadline=time.monotonic()+30
  while True:
   if p.poll() is not None:raise RuntimeError('Native backend failed; see private log')
   try:
    with socket.create_connection(('127.0.0.1',listen),timeout=1) as sock:
     sock.sendall(b'*1\r\n$4\r\nPING\r\n')
     if sock.recv(50).startswith(b'+PONG'):break
   except OSError:pass
   if time.monotonic()>deadline:raise RuntimeError('Native backend timeout')
   time.sleep(.1)
 env=dict(os.environ,MEMORIES_TEST_OWNED_NATIVE='1',MEMORIES_TEST_REDISSTACK_CONNECTION=f'127.0.0.1:{rp}',MEMORIES_TEST_FALKORDB_CONNECTION=f'127.0.0.1:{fp}',MEMORIES_DAPR_PLACEMENT_HOST_ADDRESS=f'127.0.0.1:{pp}',MEMORIES_DAPR_SCHEDULER_HOST_ADDRESS=f'127.0.0.1:{sp}')
 args=['dotnet','tests/Hexalith.Memories.IntegrationTests/bin/Debug/net10.0/Hexalith.Memories.IntegrationTests.dll','-class','Hexalith.Memories.IntegrationTests.Ingestion.NaturalLanguageErasureIntegrationTests','-class','Hexalith.Memories.IntegrationTests.Ingestion.RetryWorkflowErasureIntegrationTests','-class','Hexalith.Memories.IntegrationTests.Ingestion.TenantIngestionLifecycleIntegrationTests']
 if options.scope=='focused':args=args[:2]+['-method','*NamespaceMigration*','-method','*OriginalSameNameRawRetry*']
 args+=['-xml',str(work/'results.xml')]
 with open(work/'tests.log','w') as log:
  p=subprocess.Popen(args,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True);procs.append(p)
  code=p.wait(timeout=1200)
 receipts.append({'command':args,'exit_code':code,'environment':{k:v for k,v in env.items() if k.startswith('MEMORIES_DAPR_')}})
 print(json.dumps(receipts),flush=True)
 sys.exit(code)
finally:
 for p in reversed(procs):
  try:os.killpg(p.pid,signal.SIGTERM)
  except ProcessLookupError:pass
 for p in reversed(procs):
  try:p.wait(timeout=5)
  except subprocess.TimeoutExpired:
   try:os.killpg(p.pid,signal.SIGKILL)
   except ProcessLookupError:pass
   p.wait(timeout=5)
 (work/'receipt.json').write_text(json.dumps({'results':receipts,
  'runner_sha256':hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),
  'binary_sha256':{str(p.relative_to(runtime)):hashlib.sha256(p.read_bytes()).hexdigest() for p in
    [runtime/'dapr/placement',runtime/'dapr/scheduler',runtime/'native-redis/redis-server',runtime/'native-redis/redisearch.so',runtime/'native-falkor/redis-server',runtime/'native-falkor/falkordb.so']},
  'cleanup':[{'pid':p.pid,'exit_code':p.poll()} for p in procs]},indent=2))
