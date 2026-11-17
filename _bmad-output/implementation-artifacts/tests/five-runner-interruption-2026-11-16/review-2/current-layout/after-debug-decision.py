import os,sys,time
from pathlib import Path
private=Path(__file__).resolve().parent
while not (private/'after-debug-decision.json').exists():time.sleep(.1)
import json
record=json.loads((private/'after-debug-decision.json').read_text());environment=os.environ.copy();environment.update(record.get('Env',{}))
os.execvpe(record['Args'][0],record['Args'],environment)
