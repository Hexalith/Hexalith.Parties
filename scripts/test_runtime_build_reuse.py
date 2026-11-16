"""Exercise the production marker gate before any SDK command can run."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest

PREPARE = Path(__file__).with_name('prepare-aspire-runtime-hosts.sh')


class RuntimeBuildReuseTests(unittest.TestCase):
    def test_invalid_completion_cannot_reach_the_sdk_or_reuse_an_old_prepared_manifest(self):
        with tempfile.TemporaryDirectory(prefix='parties-reuse-marker-') as temporary:
            root = Path(temporary)
            tools = root/'tools'
            tools.mkdir()
            invoked = root/'dotnet-invoked'
            dotnet = tools/'dotnet'
            dotnet.write_text(f'#!{sys.executable}\nfrom pathlib import Path\nPath({str(invoked)!r}).write_text("unexpected")\nraise SystemExit(91)\n')
            dotnet.chmod(0o755)
            artifacts = root/'artifacts'
            profile = artifacts/'runtime-profile/Release'
            profile.mkdir(parents=True)
            started = profile/'solution-build-started-ns'
            completed = profile/'solution-build-completed.json'
            prepared = profile/'prepared.json'
            now = time.time_ns()
            valid = dict(configuration='Release', buildStartedNs=now-1000, completedNs=now-1)
            cases = {
                'missing-completion': None,
                'malformed-json': '{',
                'non-object': '[]',
                'wrong-configuration': json.dumps(dict(valid,configuration='Debug')),
                'missing-start-time': json.dumps({k:v for k,v in valid.items() if k!='buildStartedNs'}),
                'boolean-start': json.dumps(dict(valid,buildStartedNs=True)),
                'boolean-completion': json.dumps(dict(valid,completedNs=True)),
                'zero-start': json.dumps(dict(valid,buildStartedNs=0)),
                'string-time': json.dumps(dict(valid,completedNs=str(now-1))),
                'completion-before-start': json.dumps(dict(valid,completedNs=now-2000)),
                'future-completion': json.dumps(dict(valid,completedNs=now+3600_000_000_000)),
                'different-begin': json.dumps(dict(valid,buildStartedNs=now-2000)),
                'unknown-field': json.dumps(dict(valid,unverified=True)),
                'duplicate-field': json.dumps(valid)[:-1]+',"configuration":"Release"}',
                'missing-begin': json.dumps(valid),
                'linked-completion': json.dumps(valid),
                'linked-begin': json.dumps(valid),
            }
            for name,body in cases.items():
                with self.subTest(name=name):
                    completed.unlink(missing_ok=True)
                    started.unlink(missing_ok=True)
                    started.write_text(str(valid['buildStartedNs'])+'\n')
                    if body is not None:
                        completed.write_text(body)
                    if name=='missing-begin':
                        started.unlink()
                    if name=='linked-completion':
                        target=root/'complete.json';target.write_text(body)
                        completed.unlink();completed.symlink_to(target)
                    if name=='linked-begin':
                        target=root/'begin.txt';target.write_text(started.read_text())
                        started.unlink();started.symlink_to(target)
                    prepared.write_text('{"old":"cannot authorize reuse"}')
                    result=subprocess.run(['bash',str(PREPARE),'--reuse-build','true','--artifacts-path',str(artifacts)],
                        env=dict(os.environ,PATH=str(tools)+os.pathsep+os.environ['PATH']),capture_output=True,text=True,timeout=10)
                    self.assertNotEqual(0,result.returncode,result.stdout+result.stderr)
                    self.assertIn('build',result.stderr.lower())
                    self.assertFalse(invoked.exists(),result.stdout+result.stderr)
                    self.assertFalse(prepared.exists())


if __name__=='__main__':
    unittest.main()
