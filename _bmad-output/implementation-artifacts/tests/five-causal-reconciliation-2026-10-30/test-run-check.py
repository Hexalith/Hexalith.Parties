"""Short ordinary-process-group timeout proof; no escaped-session claim."""
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest


class RunCheckTests(unittest.TestCase):
 def test_timeout_drains_descendant_before_hash(self):
  with tempfile.TemporaryDirectory() as directory:
   root=Path(directory)
   checker=root/'run-check.py'
   checker.write_bytes(Path(__file__).with_name('run-check.py').read_bytes())
   # Parent exits on TERM; child ignores TERM and keeps stdout open and writing.
   child="import signal,time;signal.signal(signal.SIGTERM,signal.SIG_IGN);print('child ready',flush=True);\nwhile True: print('child tick',flush=True);time.sleep(.02)"
   parent="import subprocess,sys,time;subprocess.Popen([sys.executable,'-u','-c',sys.argv[1]]);print('parent ready',flush=True);time.sleep(30)"
   command=[sys.executable,'-u','-c',parent,child]
   spec=importlib.util.spec_from_file_location('checker',checker)
   module=importlib.util.module_from_spec(spec)
   spec.loader.exec_module(module)
   started=time.monotonic()
   self.assertEqual(module.run('timeout',directory,command,timeout=.3),124)
   self.assertLess(time.monotonic()-started,4)
   log=root/'timeout.txt'
   original=log.read_bytes()
   receipt=json.loads((root/'timeout.json').read_text())
   self.assertIn(b'child ready',original)
   self.assertIn(b'parent ready',original)
   self.assertIn(b'VERIFICATION TIMEOUT: 0.3 seconds',original)
   self.assertEqual(receipt['command'],command)
   self.assertEqual(receipt['cwd'],directory)
   self.assertEqual(receipt['sha256'],hashlib.sha256(original).hexdigest())
   time.sleep(.1)
   self.assertEqual(log.read_bytes(),original)

 def test_success_preserves_exact_output(self):
  with tempfile.TemporaryDirectory() as directory:
   checker=Path(directory)/'run-check.py'
   checker.write_bytes(Path(__file__).with_name('run-check.py').read_bytes())
   command=[sys.executable,'-c',"import sys;sys.stdout.buffer.write(b'out\\r\\n');sys.stdout.flush();sys.stderr.buffer.write(b'err\\n')"]
   result=subprocess.run([sys.executable,str(checker),'success',directory,*command],capture_output=True,timeout=4)
   self.assertEqual(result.returncode,0)
   self.assertEqual((Path(directory)/'success.txt').read_bytes(),b'out\r\nerr\n')


if __name__=='__main__':
 unittest.main()
