"""Freeze an owned local qualification repository; never mutate its source workspace."""
import hashlib,json,os,subprocess,sys
from pathlib import Path
private=Path(__file__).resolve().parent
origin=Path.cwd()
outer=Path(os.environ['TMPDIR'])
root=outer/'repo'
metadata=json.loads((private/'overlay-metadata.json').read_text())
subprocess.run(['git','clone','--local','--no-hardlinks','--no-checkout',str(origin),str(root)],check=True)
subprocess.run(['git','-C',str(root),'checkout','--detach',metadata['QualificationHead']],check=True)
references=json.loads((private/'reference-heads.json').read_text())
for relative,head in references.items():
    target=root/relative
    subprocess.run(['git','clone','--local','--no-hardlinks','--no-checkout',str(origin/relative),str(target)],check=True)
    subprocess.run(['git','-C',str(target),'checkout','--detach',head],check=True)
    nested=subprocess.check_output(['git','-C',str(target),'submodule','status'],text=True)
    if any(line and not line.startswith('-') for line in nested.splitlines()):
        raise RuntimeError('Nested reference unexpectedly initialized: '+relative)
for relative,expected in json.loads((private/'source-final.json').read_text()).items():
    raw=(private/'source-final'/relative).read_bytes()
    if hashlib.sha256(raw).hexdigest()!=expected:raise RuntimeError('Overlay changed: '+relative)
    target=root/relative;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(raw)
records={}
for relative in subprocess.check_output(['git','-C',str(root),'ls-files'],text=True).splitlines():
    path=root/relative
    if path.is_file() and not path.is_symlink():records[relative]=hashlib.sha256(path.read_bytes()).hexdigest()
(private/'repository-freeze.json').write_text(json.dumps({'QualificationHead':metadata['QualificationHead'],'Files':records,'ReferenceHeads':references,'Root':str(root),'OuterScratch':str(outer)},indent=2))
scratch=root/'artifacts/verification';scratch.mkdir(parents=True)
(private/'private-root.txt').write_text(str(root))
for key in ('TMPDIR','TMP','TEMP'):os.environ[key]=str(scratch)
os.chdir(root)
os.execv(sys.executable,[sys.executable,str(private/'driver.py')])
