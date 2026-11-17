"""Bind one immutable current-layout execution cohort and its full prepared closure."""
import hashlib,importlib.util,json,subprocess,sys
from pathlib import Path
private=Path(__file__).resolve().parent;root=Path.cwd();a=Path(sys.argv[1]);receipts=a/'runtime-profile/Release'
for label in ['38-release-contracts','39-release-unit','56-configuration-components-build','57-configuration-component-controls','58-final-release-runtime-preparation']:
    assert json.loads((private/'queue'/(label+'.result')).read_text())['ExitCode']==0,label
source=json.loads((private/'source-final.json').read_text())
for name,expected in source.items():assert hashlib.sha256((root/name).read_bytes()).hexdigest()==expected,name
freeze=json.loads((private/'repository-freeze.json').read_text())
for name,expected in freeze['Files'].items():assert hashlib.sha256((root/name).read_bytes()).hexdigest()==expected,name
for name,head in freeze['ReferenceHeads'].items():
    assert subprocess.check_output(['git','-C',str(root/name),'rev-parse','HEAD'],text=True).strip()==head
spec=importlib.util.spec_from_file_location('cohort_validator',root/'scripts/validate-aspire-runtime-cohort.py');validator=importlib.util.module_from_spec(spec);spec.loader.exec_module(validator)
manifest=json.loads((receipts/'prepared.json').read_text());records={};checked={}
for project,bound in manifest['projects'].items():
    receipt=validator.verify_manifest(receipts/'prepared.json',Path(project),'Release','5.1.0',manifest['familyVersions'],'net11.0');records[project]=receipt
    target=private/'prepared-receipts'/Path(bound['receipt']).name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(Path(bound['receipt']).read_bytes())
    def paths(value):
        if isinstance(value,dict):
            for key,item in value.items():
                if isinstance(item,str) and Path(key).is_absolute() and len(item)==64:
                    if Path(key).is_file():
                        assert hashlib.sha256(Path(key).read_bytes()).hexdigest()==item,key
                        checked[key]=item
                else:paths(item)
        elif isinstance(value,list):
            for item in value:paths(item)
    paths(receipt)
paths={'PreparedSha256':receipts/'prepared.json','WrapperSha256':a/'bin/Hexalith.Parties.IntegrationTests/release/Hexalith.Parties.IntegrationTests.dll','ChildSha256':a/'bin/Hexalith.Parties.Runtime.IntegrationTests/release/Hexalith.Parties.Runtime.IntegrationTests.dll'}
cohort={key:hashlib.sha256(path.read_bytes()).hexdigest() for key,path in paths.items()}
(private/'cohort-final.json').write_text(json.dumps(cohort,indent=2)+'\n');(private/'prepared.json').write_bytes((receipts/'prepared.json').read_bytes())
(private/'cohort-paths.json').write_text(json.dumps({key:str(path) for key,path in paths.items()},indent=2)+'\n')
(private/'prepared-closure.json').write_text(json.dumps({'AllProjectReceiptsVerified':True,'Projects':len(records),'FrozenRepositoryFiles':len(freeze['Files']),'SourceFiles':len(source),'ReferenceHeadsVerified':True,'UniquePreparedFilesChecked':len(checked),'Files':dict(sorted(checked.items()))},indent=2)+'\n')
for project,receipt in records.items():
    if project.endswith('/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj'):(private/'current-source-host-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
print(json.dumps(cohort))
