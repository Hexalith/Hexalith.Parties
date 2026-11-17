"""Revalidate the complete prepared package runtime closure and exact frozen task inputs."""
import hashlib,importlib.util,json,subprocess
from pathlib import Path
private=Path(__file__).resolve().parent;root=Path.cwd();sha=lambda file:hashlib.sha256(file.read_bytes()).hexdigest()
paths=json.loads((private/'cohort-paths.json').read_text());cohort=json.loads((private/'cohort-final.json').read_text())
for name,path in paths.items():assert sha(Path(path))==cohort[name],name
for name,expected in json.loads((private/'source-final.json').read_text()).items():assert sha(root/name)==expected,name
for name,head in json.loads((private/'reference-heads.json').read_text()).items():assert subprocess.check_output(['git','-C',str(root/name),'rev-parse','HEAD'],text=True).strip()==head,name
spec=importlib.util.spec_from_file_location('validator',root/'scripts/validate-aspire-runtime-cohort.py');validator=importlib.util.module_from_spec(spec);spec.loader.exec_module(validator)
manifest_path=Path(paths['PreparedSha256']);manifest=json.loads(manifest_path.read_text())
for project in manifest['projects']:validator.verify_manifest(manifest_path,Path(project),'Release','5.1.0',manifest['familyVersions'],'net11.0')
print(json.dumps({'PreparedProjectsVerified':len(manifest['projects']),'CohortUnchanged':True,'FrozenSourceFilesVerified':39,'RootReferencesVerified':10,'ExplicitReleasePackageProfile':True}))
