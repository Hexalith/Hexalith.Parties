#!/usr/bin/env python3
"""Execute the maintained current-package browser controls in an owned local snapshot.

Invoke through run-with-temp with the repository-pinned Node/npm on PATH.
Only local object stores are cloned; source branches, dependencies and nested
submodules are unchanged. Results must be outside the disposable TMPDIR.
"""
import argparse,os,pathlib,subprocess,json,shutil,hashlib,sys
import importlib.util

sys.dont_write_bytecode=True


def managed_environment(environment=None):
 """Always use the runner-owned Test host and its fixed local URL."""
 selected=dict(os.environ if environment is None else environment)
 selected.pop('PLAYWRIGHT_SKIP_WEBSERVER',None)
 selected.pop('BASE_URL',None)
 selected['CI']='true'
 return selected


def run_owned(args,cwd,environment,log,source,timeout=600):
 """Reuse the maintained Linux process-group/subreaper custody, including timeout cleanup."""
 spec=importlib.util.spec_from_file_location('parties_owned_publisher',source/'scripts/verify-eventstore-publish.py')
 supervisor=importlib.util.module_from_spec(spec)
 spec.loader.exec_module(supervisor)
 with log.open('a') as output, supervisor.owned_child_reaper():
  process=None
  primary=None
  try:
   process=subprocess.Popen(args,cwd=cwd,env=environment,stdout=output,stderr=subprocess.STDOUT,start_new_session=True)
   process.wait(timeout=timeout)
   if process.returncode:
    raise RuntimeError(str(args[:3])+' failed; '+log.read_text()[-5000:])
  except BaseException as failure:
   primary=failure
   raise
  finally:
   if process is not None:
    try:supervisor.stop_owned_command(process)
    except Exception as cleanup:
     if primary is not None:raise BaseExceptionGroup('Browser command and descendant cleanup failed',[primary,cleanup]) from None
     raise


def main():
 parser=argparse.ArgumentParser()
 parser.add_argument('--source',type=pathlib.Path,required=True)
 parser.add_argument('--output',type=pathlib.Path,required=True)
 args=parser.parse_args()
 root=args.source.resolve();private=args.output.resolve();scratch=pathlib.Path(os.environ['TMPDIR']).resolve();copy=scratch/'browser'
 if private.is_relative_to(scratch):raise ValueError('Retain results outside the disposable TMPDIR')
 private.mkdir(parents=True,exist_ok=False)
 def run(args,cwd=root,env=None,log=None):
  run_owned(args,cwd,env,private/(log or 'browser-setup.log'),root)
 run(['git','clone','--local','--shared','--no-checkout',str(root),str(copy)])
 run(['git','sparse-checkout','set','.config','src','tests','scripts','module'],cwd=copy)
 run(['git','checkout','--detach','HEAD'],cwd=copy)
 producer=root/'references/Hexalith.Builds';target=copy/'references/Hexalith.Builds'
 run(['git','clone','--local','--shared','--no-checkout',str(producer),str(target)])
 run(['git','sparse-checkout','set','.config','Props','Targets','Sdk','Tools'],cwd=target)
 revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=producer,text=True).strip()
 run(['git','checkout','--detach',revision],cwd=target)
 overlays=['tests/e2e/helpers/frontcomposer-assets.ts','tests/e2e/config-tests/frontcomposer-assets.test.mjs','tests/e2e/package.json']
 for relative in overlays:
  dest=copy/relative;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(root/relative,dest)
 (private/'browser-source-identities.json').write_text(json.dumps({'baseline':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'Builds':revision,'overlays':{p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in overlays}},indent=2))
 args=['dotnet','build','src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj','-c','Release','-m:1','--disable-build-servers','-p:NuGetAudit=false','-p:MinVerVersionOverride=1.0.0','-p:HexalithEventStoreVersion=5.1.0','-p:HexalithMemoriesVersion=4.0.0','-p:HexalithFrontComposerVersion=6.0.0']
 run(args,cwd=copy,log='browser-build.log');print('browser UI build exit=0',flush=True)
 run(['npm','ci','--ignore-scripts'],cwd=copy/'tests/e2e',log='browser-npm-ci.log')
 run(['npm','run','typecheck'],cwd=copy/'tests/e2e',log='browser-typecheck.log')
 run(['npm','run','test:config'],cwd=copy/'tests/e2e',log='browser-config.log')
 env=managed_environment()
 args=['npm','run','test','--','specs/consumer-portal-routes.spec.ts','specs/admin-parties-list.spec.ts','--project=chromium','--grep','exports own data|navigation cancels an active shared download|direct GDPR route for an erased party','--retries=0']
 try:run(args,cwd=copy/'tests/e2e',env=env,log='browser-behavior.log');print('real browser behavior exit=0',flush=True)
 finally:
  for relative in ['tests/e2e/test-results','tests/e2e/playwright-report']:
   if (copy/relative).exists():shutil.copytree(copy/relative,private/('browser-'+pathlib.Path(relative).name),dirs_exist_ok=True)
  build=copy/'src/Hexalith.Parties.UI/bin/Release/net11.0'
  (private/'browser-assemblies.json').write_text(json.dumps({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in build.glob('*.dll') if p.name.startswith('Hexalith')},indent=2))
  for name in ['Hexalith.Parties.UI.package-metadata.txt','Hexalith.Parties.UI.source-metadata.txt']:shutil.copyfile(build/name,private/('browser-'+name))


if __name__=='__main__':
 main()
