"""Fail if any frozen local source, mapping, prepared output or executed cohort changes."""
import hashlib,json,subprocess
from pathlib import Path
private=Path(__file__).resolve().parent;root=Path.cwd();sha=lambda file:hashlib.sha256(file.read_bytes()).hexdigest()
paths=json.loads((private/'cohort-paths.json').read_text());cohort=json.loads((private/'cohort-final.json').read_text())
for name,path in paths.items():assert sha(Path(path))==cohort[name],name
manifest=json.loads(Path(paths['PreparedSha256']).read_text());assert manifest['completed'] and manifest['kind']=='mapped-native-source-build'
for name,expected in manifest['files'].items():assert sha(Path(name))==expected,name
for name,head in manifest['referenceHeads'].items():assert subprocess.check_output(['git','-C',str(root/name),'rev-parse','HEAD'],text=True).strip()==head,name
print(json.dumps({'PreparedFilesVerified':len(manifest['files']),'CohortUnchanged':True,'MappedNativeSourceBuild':True}))
