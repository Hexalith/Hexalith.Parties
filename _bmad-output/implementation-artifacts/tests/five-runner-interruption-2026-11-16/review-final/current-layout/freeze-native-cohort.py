"""Freeze completed mapped source builds without asserting public-package qualification."""
import hashlib,json,os,subprocess,sys,time
from pathlib import Path
private=Path(__file__).resolve().parent;root=Path.cwd();a=Path(sys.argv[1]);sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
required=json.loads((private/'native-completed-build-commands.json').read_text()) if (private/'native-completed-build-commands.json').exists() else ['03-debug-unit','04-debug-runtime','05-debug-wrapper','06-release-contracts','16-debug-contracts']
for label in required:assert json.loads((private/'queue'/(label+'.result')).read_text())['ExitCode']==0,label
source=json.loads((private/'source-final.json').read_text())
for name,expected in source.items():assert sha(root/name)==expected,name
freeze=json.loads((private/'repository-freeze.json').read_text());inputs={}
for name,expected in freeze['Files'].items():
    assert sha(root/name)==expected,name;inputs[str(root/name)]=expected
for name,head in freeze['ReferenceHeads'].items():
    reference=root/name
    assert subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD'],text=True).strip()==head
    for relative in subprocess.check_output(['git','-C',str(reference),'ls-files'],text=True).splitlines():
        file=reference/relative
        if file.is_file() and not file.is_symlink():inputs[str(file)]=sha(file)
for name in source:inputs[str(root/name)]=sha(root/name)
preparation=next(json.loads(line) for line in (private/'01-source-prepare.log').read_text().splitlines() if line.startswith('{"status"'))
for value in preparation['buildPreparation']['environment'].values():
    path=Path(value)
    if path.is_file():inputs[str(path)]=sha(path)
environment=os.environ.copy();environment.update(preparation['buildPreparation']['environment']);environment.update({'UseArtifactsOutput':'true','ArtifactsPath':str(a),'DOTNET_CLI_USE_MSBUILD_SERVER':'0','MSBUILDDISABLENODEREUSE':'1'})
properties=json.loads(subprocess.check_output(['dotnet','msbuild','tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj','-nologo','-getProperty:Configuration,TargetFramework,HexalithSourceMappingMode,HexalithSourceMappingHash,HexalithEventStoreVersion,HexalithMemoriesVersion,HexalithFrontComposerVersion,TargetPath'],env=environment,text=True))
assert properties['Properties']['Configuration']=='Debug' and properties['Properties']['HexalithSourceMappingMode']=='source',properties
assert properties['Properties']['HexalithSourceMappingHash']==preparation['buildPreparation']['mappingHash'],properties
(private/'native-evaluated-properties.json').write_text(json.dumps(properties,indent=2)+'\n')
outputs={str(file):sha(file) for file in (a/'bin').rglob('*') if file.is_file() and not file.is_symlink()}
for file in (a/'obj').glob('*/project.assets.json'):outputs[str(file)]=sha(file)
manifest={'schemaVersion':1,'completed':True,'kind':'mapped-native-source-build','configuration':'Debug','Authority':'Completed local source build closure only; no published-package qualification','completedNs':time.time_ns(),'qualificationHead':freeze['QualificationHead'],'referenceHeads':freeze['ReferenceHeads'],'evaluatedProperties':properties,'mappingHash':preparation['buildPreparation']['mappingHash'],'mappingEnvironment':preparation['buildPreparation']['environment'],'completedBuildCommands':required,'files':inputs|outputs,'SourceFiles':len(inputs),'OutputFiles':len(outputs)}
prepared=a/'runtime-profile/Debug/prepared.json';prepared.parent.mkdir(parents=True,exist_ok=True);prepared.write_text(json.dumps(manifest,indent=2)+'\n')
paths={'PreparedSha256':prepared,'WrapperSha256':a/'bin/Hexalith.Parties.IntegrationTests/debug/Hexalith.Parties.IntegrationTests.dll','ChildSha256':a/'bin/Hexalith.Parties.Runtime.IntegrationTests/debug/Hexalith.Parties.Runtime.IntegrationTests.dll'}
cohort={key:sha(path) for key,path in paths.items()}
(private/'cohort-final.json').write_text(json.dumps(cohort,indent=2)+'\n');(private/'cohort-paths.json').write_text(json.dumps({key:str(path) for key,path in paths.items()},indent=2)+'\n');(private/'prepared.json').write_bytes(prepared.read_bytes())
(private/'prepared-closure.json').write_text(json.dumps({'AllCompletedSourceBuildsVerified':True,'MappingHash':manifest['mappingHash'],'SourceFiles':len(source),'RootReferenceHeadsVerified':True,'UniquePreparedFilesChecked':len(manifest['files']),'Files':manifest['files']},indent=2)+'\n')
(private/'current-source-host-receipt.json').write_text(json.dumps({'Authority':'Mapped native source Debug build only','ReferenceHead':freeze['ReferenceHeads']['references/Hexalith.Memories'],'TargetPath':str(a/'bin/Hexalith.Memories.Server/debug/Hexalith.Memories.Server.dll'),'Sha256':sha(a/'bin/Hexalith.Memories.Server/debug/Hexalith.Memories.Server.dll'),'PublicPackageQualification':False},indent=2)+'\n')
(private/'helper-sha256.json').write_text(json.dumps({file.name:sha(file) for file in sorted(private.glob('*.py'))},indent=2)+'\n')
print(json.dumps(cohort))
