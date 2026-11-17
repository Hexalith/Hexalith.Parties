"""Queue evidence commands under the repository's exclusive Linux descendant supervisor."""
import json
import os
from pathlib import Path
import subprocess
import sys
import textwrap
import time

class ContainmentFailure(RuntimeError):
    """The queue must retain scratch and refuse subsequent commands."""

def birth(pid):
    raw=Path('/proc',str(pid),'stat').read_text()
    return raw[raw.rfind(')')+2:].split()[19]

def run_job(args,environment,log,timeout,scratch,root):
    state=scratch/('job-supervisor-'+os.urandom(8).hex())
    state.mkdir()
    shared=root/'tests/PackageTests.Shared/PackageTestProcessSupervisor.cs'
    text=shared.read_text()
    script=textwrap.dedent(text.split('private const string Script = """',1)[1].split('""";',1)[0]).strip()
    environment=environment.copy()
    environment['PARTIES_JOB_SUPERVISOR_STATE']=str(state)
    command=[sys.executable,'-c',script,str(state),'4900',str(os.getpid()),birth(os.getpid()),*args]
    began=time.monotonic()
    timed_out=False
    with log.open('w') as stream:
        child=subprocess.Popen(command,env=environment,stdout=stream,stderr=subprocess.STDOUT)
        identity={'Pid':child.pid,'Start':birth(child.pid),'State':str(state),'ExclusiveSubreaper':True}
        log.with_suffix('.supervisor.json').write_text(json.dumps(identity,indent=2))
        try:
            code=child.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            timed_out=True
            (state/'stop').touch()
            try: code=child.wait(timeout=6)
            except subprocess.TimeoutExpired as error:
                # Do not kill the reaper, start another command or release its scratch.
                raise ContainmentFailure('The exclusive supervisor did not finish bounded cleanup: '+str(identity)) from error
    failure=state/'failure'
    if failure.exists():
        raise ContainmentFailure('Descendant cleanup failed: '+failure.read_text())
    for path in state.iterdir(): path.unlink()
    state.rmdir()
    return {'ExitCode':255 if timed_out else code,'CommandExitCode':code,'TimedOut':timed_out,
        'Seconds':time.monotonic()-began,'DescendantsReaped':True,'Supervisor':identity}

def main():
    private=Path(__file__).resolve().parent
    root=Path.cwd();scratch=Path(os.environ['TMPDIR'])
    (private/'job-path.txt').write_text(str(scratch))
    print('Owned scratch ready; exclusive command supervisors enabled',flush=True)
    while not (private/'finish').exists():
        for path in sorted((private/'queue').glob('*.json')):
            result=path.with_suffix('.result')
            if result.exists():continue
            task=json.loads(path.read_text())
            def expand(value):return value.replace('{JOB}',str(scratch)).replace('{ROOT}',str(root)).replace('{PRIVATE}',str(private))
            args=[expand(value) for value in task['Args']]
            environment=os.environ.copy();environment.update({key:expand(value) for key,value in task.get('Env',{}).items()})
            (private/(path.stem+'.command.json')).write_text(json.dumps({'Args':args,'Env':task.get('Env',{})},indent=2))
            try:
                outcome=run_job(args,environment,private/(path.stem+'.log'),task.get('Timeout',2400),scratch,root)
            except ContainmentFailure as error:
                (private/'containment-failure.json').write_text(json.dumps({'Job':path.stem,'Failure':str(error),'LaterJobsRefused':True,'ScratchReleaseRefused':True},indent=2))
                print('Containment failed; queue stopped and scratch retained',flush=True)
                # An exact externally audited recovery is required. No finish file or later job may bypass containment.
                while True: time.sleep(1)
            result.write_text(json.dumps(outcome,indent=2))
            print(path.stem,outcome['ExitCode'],flush=True)
        time.sleep(.1)
    (private/'driver-completed.json').write_text(json.dumps({'AllJobsAwaited':True,'ContainmentFailures':False}))

if __name__=='__main__':main()
