#!/usr/bin/env python3
"""Preserve exact raw receipts in a deterministic archive and remove bulky working copies."""
import hashlib
import json
from pathlib import Path
import zipfile

HERE = Path(__file__).resolve().parent
ARCHIVE = HERE / 'raw-evidence.zip'

def sha(data):
    return hashlib.sha256(data).hexdigest()

entries = {}
if ARCHIVE.exists():
    with zipfile.ZipFile(ARCHIVE) as previous:
        entries = {name: previous.read(name) for name in previous.namelist() if name != 'entry-hashes.json'}
files = [path for path in HERE.rglob('*') if path.is_file()
         and (path.suffix in {'.xml', '.json', '.trace', '.log'} or 'provenance' in path.relative_to(HERE).parts
              or (path.suffix == '.zip' and path != ARCHIVE))
         and path.name not in {'final-verification.json', 'archive-entry-hashes.json'}]
for path in sorted(files):
    key = 'receipts/' + str(path.relative_to(HERE))
    data = path.read_bytes()
    if key in entries and entries[key] != data:
        entries['history/' + key + '.' + sha(entries[key])[:16]] = entries[key]
    entries[key] = data
    if path.parent.name in {'checks', 'historical'} and path.suffix == '.json':
        try:
            receipt = json.loads(data)
            log = Path(receipt.get('raw_log_local_path', '/nonexistent'))
            if log.is_file() and sha(log.read_bytes()) == receipt.get('raw_log_sha256'):
                entries['logs/' + log.name + '.' + receipt['raw_log_sha256'][:16]] = log.read_bytes()
        except (ValueError, OSError, AttributeError):
            pass
hashes = {name: sha(data) for name, data in sorted(entries.items())}
entries['entry-hashes.json'] = (json.dumps(hashes, indent=2) + '\n').encode()
temporary = ARCHIVE.with_suffix('.zip.tmp')
with zipfile.ZipFile(temporary, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as output:
    for name, data in sorted(entries.items()):
        info = zipfile.ZipInfo(name, (2026, 10, 27, 0, 0, 0))
        info.create_system = 3
        info.external_attr = 0o100644 << 16
        output.writestr(info, data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
temporary.replace(ARCHIVE)
manifest = {'archive': ARCHIVE.name, 'sha256': sha(ARCHIVE.read_bytes()), 'entries': hashes}
(HERE/'archive-entry-hashes.json').write_text(json.dumps(manifest,indent=2)+'\n')
# Only these task-created raw files are removed, after their exact bytes are sealed above.
for path in files:
    path.unlink()
print(f'Archived {len(hashes)} raw entries in {ARCHIVE.name} ({ARCHIVE.stat().st_size} bytes).')
