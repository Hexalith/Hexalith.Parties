import json,os,sys
from pathlib import Path
private=Path(__file__).resolve().parent
for label in sys.argv[1].split(','):
    path=private/'queue'/(label+'.result')
    if not path.exists() or json.loads(path.read_text())['ExitCode']!=0:
        print('Deferred before execution: required source cohort control did not pass: '+label,flush=True);sys.exit(3)
os.execvpe(sys.argv[2],sys.argv[2:],os.environ)
