"""Release a runtime command only after the required focused controls have passed."""
import json,os,sys,time
from pathlib import Path
private=Path(__file__).resolve().parent
reported=False
while True:
    selection=private/'focused-release.json'
    required=json.loads(selection.read_text())['RequiredResults'] if selection.exists() else ['34-final-controls','35-final-consumer','35b-startup-ordering-controls','35c-driver-controls']
    results=[private/'queue'/(label+'.result') for label in required]
    if all(path.exists() and json.loads(path.read_text())['ExitCode']==0 for path in results):break
    if not reported:
        print('Holding runtime: required focused controls are incomplete or failed',flush=True);reported=True
    time.sleep(.1)
os.execvpe(sys.argv[1],sys.argv[1:],os.environ)
