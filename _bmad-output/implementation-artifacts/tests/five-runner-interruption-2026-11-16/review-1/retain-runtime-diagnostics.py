import hashlib,json,os,runpy,shutil,sys
from pathlib import Path
private=Path(__file__).resolve().parent
base=private.parent/'run-runtime.py'
destination=Path(sys.argv[1])
scratch=Path(os.environ['TMPDIR'])
try:
    runpy.run_path(str(base),run_name='__main__')
finally:
    records=[];failures=[]
    destination.mkdir(parents=True,exist_ok=True)
    for directory in sorted(scratch.iterdir()):
        if not directory.is_dir() or directory.is_symlink() or not directory.name.startswith(('aspire-dcp','aspire-dapr','hexalith-route-catalog-')):continue
        target=destination/'private-sdk-diagnostics'/directory.name
        try:
            shutil.copytree(directory,target,symlinks=True,dirs_exist_ok=True)
            for path in target.rglob('*'):
                if path.is_file() and not path.is_symlink():records.append({'Path':str(path.relative_to(destination)),'Sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'Bytes':path.stat().st_size})
        except OSError as error:failures.append({'Directory':directory.name,'Error':str(error)})
    (destination/'sdk-diagnostic-index-private.json').write_text(json.dumps({'Files':records,'Failures':failures,'BaseHelperSha256':hashlib.sha256(base.read_bytes()).hexdigest(),'DiagnosticHelperSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()},indent=2))
