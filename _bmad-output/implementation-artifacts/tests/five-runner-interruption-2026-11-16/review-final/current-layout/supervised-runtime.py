"""Keep short runtime scratch alive until the exclusive descendant reaper has completed."""
import json,os,sys,time
from pathlib import Path
from driver import ContainmentFailure,run_job

private=Path(__file__).resolve().parent
if (private/'defer-runtime').exists():
    print('Deferred before runtime execution: prior cohort is unqualified and retained',flush=True)
    sys.exit(3)
destination=Path(sys.argv[1]);scratch=Path(os.environ['TMPDIR'])
log=destination.with_name(destination.name+'-supervisor.log')
try:
    outcome=run_job([sys.executable,str(private/'retain-runtime-diagnostics.py'),*sys.argv[1:]],os.environ,log,int(os.environ['PARTIES_RUNTIME_JOB_TIMEOUT']),scratch,Path.cwd())
except ContainmentFailure as error:
    destination.with_name(destination.name+'-containment-failure.json').write_text(json.dumps({'Failure':str(error),'ScratchReleaseRefused':True}))
    while True:time.sleep(1)
destination.with_name(destination.name+'-supervisor-result.json').write_text(json.dumps(outcome,indent=2))
print(destination.name,outcome['ExitCode'],flush=True)
sys.exit(outcome['ExitCode'])
