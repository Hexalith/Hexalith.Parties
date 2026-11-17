import json,os,subprocess,time
from pathlib import Path
private=Path(__file__).resolve().parent
job=Path(os.environ['TMPDIR']); root=Path.cwd()
(private/'job-path.txt').write_text(str(job))
print('Owned scratch ready',flush=True)
while not (private/'finish').exists():
    for path in sorted((private/'queue').glob('*.json')):
        result=path.with_suffix('.result')
        if result.exists():continue
        task=json.loads(path.read_text())
        def expand(x):return x.replace('{JOB}',str(job)).replace('{ROOT}',str(root)).replace('{PRIVATE}',str(private))
        args=[expand(a) for a in task['Args']]; env=os.environ.copy();env.update({k:expand(v) for k,v in task.get('Env',{}).items()})
        started=time.monotonic()
        (private/(path.stem+'.command.json')).write_text(json.dumps({'Args':args,'Env':task.get('Env',{})},indent=2))
        with (private/(path.stem+'.log')).open('w') as log:
            try:code=subprocess.run(args,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=task.get('Timeout',2400)).returncode
            except BaseException as e:log.write(str(e));code=255
        result.write_text(json.dumps({'ExitCode':code,'Seconds':time.monotonic()-started}))
        print(path.stem,code,flush=True)
    time.sleep(.5)
