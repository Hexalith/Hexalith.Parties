import argparse,json,pathlib,socket,subprocess,tempfile,time,urllib.request,uuid,threading,http.server,os

def run(*args):
 p=subprocess.run(args,text=True,capture_output=True)
 if p.returncode:raise RuntimeError('Owned runtime command failed: '+args[0])
 return p.stdout.strip()
def port():
 with socket.socket() as s:s.bind(('127.0.0.1',0));return s.getsockname()[1]
name='parties-owner-inventory-'+uuid.uuid4().hex[:12];username='parties-probe-'+uuid.uuid4().hex[:8];process=None;controller=None;receipt={}
parser=argparse.ArgumentParser(description='Qualify the actual SDK/health inventory probe against owned Redis ACLs.')
parser.add_argument('--workspace',type=pathlib.Path,required=True)
parser.add_argument('--output-directory',type=pathlib.Path,required=True)
parser.add_argument('--daprd',type=pathlib.Path,default=pathlib.Path.home()/'.dapr/bin/daprd')
args=parser.parse_args();root=str(args.workspace.resolve());args.output_directory.mkdir(parents=True,exist_ok=True)

with tempfile.TemporaryDirectory(prefix='parties-owner-inventory-') as tmp:
 d=pathlib.Path(tmp)
 try:
  run('docker','run','--rm','-d','--name',name,'-p','127.0.0.1::6379','redis:7.4-alpine','redis-server','--save','','--appendonly','no')
  rp=json.loads(run('docker','inspect',name))[0]['NetworkSettings']['Ports']['6379/tcp'][0]['HostPort']
  for i in range(100):
   try:
    if run('docker','exec',name,'redis-cli','ping')=='PONG':break
   except Exception:pass
   time.sleep(.1)
  password='synthetic-'+uuid.uuid4().hex
  run('docker','exec',name,'redis-cli','ACL','SETUSER',username,'on','>'+password,'~*parties:memory-mapping:*','+@all')
  (d/'components').mkdir();(d/'components'/'state.yaml').write_text('apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: statestore\nspec:\n  type: state.redis\n  version: v1\n  metadata:\n  - name: redisHost\n    value: "127.0.0.1:'+rp+'"\n  - name: redisUsername\n    value: "'+username+'"\n  - name: redisPassword\n    value: "'+password+'"\n  - name: enableTLS\n    value: "false"\n')
  hp,gp,ip=port(),port(),port();api_token='synthetic-'+uuid.uuid4().hex
  with (d/'daprd.log').open('w') as log:
   process=subprocess.Popen([str(args.daprd),'--app-id',name,'--dapr-http-port',str(hp),'--dapr-grpc-port',str(gp),'--dapr-internal-grpc-port',str(ip),'--resources-path',str(d/'components'),'--enable-metrics=false'],env=dict(os.environ,DAPR_API_TOKEN=api_token),stdout=log,stderr=log)
   for i in range(150):
    try:
     req=urllib.request.Request(f'http://127.0.0.1:{hp}/v1.0/healthz/outbound',headers={'dapr-api-token':api_token})
     with urllib.request.urlopen(req,timeout=.5) as r:
      if r.status==204:break
    except Exception:pass
    if process.poll() is not None:raise RuntimeError('Owned Dapr exited before readiness')
    time.sleep(.1)
   else:raise RuntimeError('Owned Dapr readiness timed out')
   receipt['authenticatedNonDefaultAclUser']=any('user='+username in line for line in run('docker','exec',name,'redis-cli','CLIENT','LIST').splitlines());assert receipt['authenticatedNonDefaultAclUser']
   class Repair(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
     if self.path!='/grant-inventory':self.send_error(404);return
     run('docker','exec',name,'redis-cli','ACL','SETUSER',username,'~*parties:memory-ingestion:*');receipt['inventoryPrefixGrantApplied']=True
     self.send_response(204);self.end_headers()
    def log_message(self,*a):pass
   controller=http.server.ThreadingHTTPServer(('127.0.0.1',0),Repair);threading.Thread(target=controller.serve_forever,daemon=True).start()
   config={'daprHttpEndpoint':f'http://127.0.0.1:{hp}','daprGrpcEndpoint':f'http://127.0.0.1:{gp}','daprApiToken':api_token,'stateStoreName':'statestore','inventoryAclRepairEndpoint':f'http://127.0.0.1:{controller.server_port}/grant-inventory'}
   cfg=d/'private.json';cfg.write_text(json.dumps(config));cfg.chmod(0o600)
   with open(args.output_directory/'inventory-runtime-tests.log','w') as output:
    result=subprocess.run(['dotnet','tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll','-method','*ActualSdkHealthDetectsInventoryPrefixAclDenialAndRepairInBothModes'],cwd=root,env=dict(os.environ,PARTIES_MEMORIES_INVENTORY_ACL_CONFIG=str(cfg)),stdout=output,stderr=subprocess.STDOUT)
   receipt['sdkHealthTestExitCode']=result.returncode
   keys=run('docker','exec',name,'redis-cli','KEYS','*parties:memory-*');receipt['allReservedSentinelsAbsent']=not keys;assert not keys
   receipt['daprdVersion']=run(str(args.daprd),'--version');receipt['redisVersion']=run('docker','exec',name,'redis-cli','INFO','server').split('redis_version:')[1].splitlines()[0]
 finally:
  if controller:controller.shutdown();controller.server_close()
  if process:
   process.terminate()
   try:process.wait(timeout=10)
   except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
   receipt['ownedDaprStopped']=process.poll() is not None
  subprocess.run(['docker','rm','-f',name],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
  receipt['ownedRedisRemoved']=subprocess.run(['docker','inspect',name],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode!=0
(args.output_directory/'inventory-runtime.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt,indent=2))
