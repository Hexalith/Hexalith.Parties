import json,os,subprocess,sys
from pathlib import Path
private=Path(__file__).resolve().parent
record=next(json.loads(line) for line in (private/'01-source-prepare.log').read_text().splitlines() if line.startswith('{"status"'))
environment=os.environ.copy();environment.update(record['buildPreparation']['environment']);environment.update({'DOTNET_CLI_USE_MSBUILD_SERVER':'0','MSBUILDDISABLENODEREUSE':'1'})
sys.exit(subprocess.run(['bash','scripts/prepare-aspire-runtime-hosts.sh','--gateway-profile','native','--configuration','Debug','--artifacts-path',sys.argv[1],'--include-memory-qualification','true'],env=environment).returncode)
