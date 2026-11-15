"""Executable controls for managed browser environment and owned command lifetime."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True

spec = importlib.util.spec_from_file_location("owned_browser", Path(__file__).with_name("run-browser.py"))
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)
ROOT = Path(__file__).resolve().parents[4]


class BrowserRunnerTests(unittest.TestCase):
    def test_external_host_overrides_are_removed_in_executed_child(self):
        with tempfile.TemporaryDirectory() as directory:
            scratch = Path(directory)
            output = scratch / "environment.json"
            environment = runner.managed_environment({**os.environ, "PLAYWRIGHT_SKIP_WEBSERVER": "1", "BASE_URL": "http://foreign.invalid"})
            runner.run_owned([sys.executable, "-c", "import json,os,pathlib,sys;pathlib.Path(sys.argv[1]).write_text(json.dumps(dict(os.environ)))", str(output)],
                             scratch, environment, scratch / "child.log", ROOT)
            observed = json.loads(output.read_text())
            self.assertNotIn("PLAYWRIGHT_SKIP_WEBSERVER", observed)
            self.assertNotIn("BASE_URL", observed)
            self.assertEqual(observed["CI"], "true")

    @unittest.skipUnless(sys.platform == "linux", "Owned publisher supervision requires Linux")
    def test_timeout_reaps_actual_child_and_grandchild_and_preserves_unrelated_worker(self):
        with tempfile.TemporaryDirectory() as directory:
            scratch = Path(directory)
            identities = scratch / "tree.json"
            ready = scratch / "ready"
            child = "import os,pathlib,sys,time;pathlib.Path(sys.argv[1]).write_text(str(os.getpid()));time.sleep(60)"
            parent = "import json,os,pathlib,subprocess,sys,time;p=subprocess.Popen([sys.executable,'-c',sys.argv[2],sys.argv[3]]);pathlib.Path(sys.argv[1]).write_text(json.dumps([os.getpid(),p.pid]));time.sleep(60)"
            unrelated = subprocess.Popen([sys.executable, "-c", "import time;time.sleep(60)"], start_new_session=True)
            try:
                with self.assertRaises(subprocess.TimeoutExpired):
                    runner.run_owned([sys.executable, "-c", parent, str(identities), child, str(ready)], scratch,
                                     os.environ.copy(), scratch / "tree.log", ROOT, timeout=1)
                self.assertTrue(ready.exists(), "The actual grandchild must have entered before the timeout")
                for identifier in json.loads(identities.read_text()):
                    self.assertFalse(Path(f"/proc/{identifier}").exists(), f"Owned process {identifier} was not reaped")
                self.assertIsNone(unrelated.poll())
            finally:
                unrelated.kill()
                unrelated.wait(timeout=5)


if __name__ == "__main__":
    unittest.main()
