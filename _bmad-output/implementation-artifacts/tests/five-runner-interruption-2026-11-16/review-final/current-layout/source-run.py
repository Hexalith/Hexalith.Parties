import json,os,sys
from pathlib import Path
private=Path(__file__).resolve().parent
record=next(json.loads(line) for line in (private/'01-source-prepare.log').read_text().splitlines() if line.startswith('{"status"'))
environment=os.environ.copy();environment.update(record['buildPreparation']['environment'])
environment.update({'UseArtifactsOutput':'true','ArtifactsPath':str(Path.cwd()/'artifacts/verification/source-output'),'DOTNET_CLI_USE_MSBUILD_SERVER':'0','MSBUILDDISABLENODEREUSE':'1'})
os.execvpe(sys.argv[1],sys.argv[1:],environment)
