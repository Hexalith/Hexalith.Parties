import json,os,subprocess,sys
from pathlib import Path
private=Path(__file__).resolve().parent
lines=(private/'01-source-prepare.log').read_text().splitlines()
record=next(json.loads(line) for line in lines if line.startswith('{"status"'))
environment=os.environ.copy();environment.update(record['buildPreparation']['environment'])
environment.update({'DOTNET_CLI_USE_MSBUILD_SERVER':'0','MSBUILDDISABLENODEREUSE':'1'})
sys.exit(subprocess.run(['dotnet','build',sys.argv[1],'-c','Debug','-m:1','--disable-build-servers','-p:UseArtifactsOutput=true','-p:ArtifactsPath='+sys.argv[2]],env=environment).returncode)
