import gzip,hashlib,json,sys
from pathlib import Path
review=Path(sys.argv[1])
def digest(raw):return hashlib.sha256(raw).hexdigest()
def index(base,legacy):
 records=[]
 for path in sorted(base.rglob('*')):
  if not path.is_file() or path.relative_to(base).as_posix() in ['artifact-index.json.gz','SHA256SUMS.gz']:continue
  raw=path.read_bytes();decoded=gzip.decompress(raw) if path.suffix=='.gz' else raw
  if legacy:record={'path':path.relative_to(base).as_posix(),'bytes':len(raw),'sha256':digest(raw),'decodedBytes':len(decoded),'decodedSha256':digest(decoded)}
  else:record={'Path':path.relative_to(base).as_posix(),'Bytes':len(raw),'TransportSha256':digest(raw),'DecodedBytes':len(decoded),'DecodedSha256':digest(decoded)}
  records.append(record)
 value={'schemaVersion':1,'authority':'Integrity only; root v1 evidence is historical; current acceptance is in review-final indexes','excludes':['artifact-index.json.gz','SHA256SUMS.gz'],'entries':records} if legacy else records
 (base/'artifact-index.json.gz').write_bytes(gzip.compress((json.dumps(value,indent=2)+'\n').encode(),mtime=0))
 lines=[digest(p.read_bytes())+'  '+p.relative_to(base).as_posix() for p in sorted(base.rglob('*')) if p.is_file() and p.relative_to(base).as_posix()!='SHA256SUMS.gz']
 (base/'SHA256SUMS.gz').write_bytes(gzip.compress(('\n'.join(lines)+'\n').encode(),mtime=0))
 return len(records)
result={'ReviewFiles':index(review,False)}
if '--include-root' in sys.argv:result['ActiveRootFiles']=index(review.parent,True)
print(json.dumps(result))
