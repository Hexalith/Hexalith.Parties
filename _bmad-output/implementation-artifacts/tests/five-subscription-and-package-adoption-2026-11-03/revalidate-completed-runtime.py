import importlib.util,json
from pathlib import Path
root=Path(__file__).resolve().parents[4]
module_path=root/'scripts/validate-aspire-runtime-cohort.py'
spec=importlib.util.spec_from_file_location('runtime_validator',module_path)
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
manifest_path=root/'artifacts/runtime-profile/Debug/prepared.json'
manifest=module.load(manifest_path)
paths=[]
for project,binding in manifest['projects'].items():
    path=Path(binding['receipt']); previous=module.load(path)
    metadata={'Configuration':previous['configuration'],'HexalithEventStoreVersion':previous['sdkVersion'],
              'TargetPath':previous['targetPath'],'ProjectAssetsFile':previous['assetsPath']}
    refreshed=module.record(metadata,Path(project),previous['configuration'],previous['sdkVersion'],
                            previous['buildStartedNs'],previous['role'],previous['familyVersions'])
    module.write(path,refreshed);paths.append(path)
packages=[entry['id']+'='+entry['version'] for entry in manifest['publicArchives']]
completed=module.complete(paths,manifest['configuration'],manifest['sdkVersion'],packages,
                          manifest_path.parent/'public-archives',manifest['familyVersions'])
module.write(manifest_path,completed)
print(json.dumps({'completed':True,'configuration':completed['configuration'],'sdkVersion':completed['sdkVersion'],'projectCount':len(completed['projects'])}))
