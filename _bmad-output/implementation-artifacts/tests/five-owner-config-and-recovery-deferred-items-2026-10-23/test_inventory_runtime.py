"""Focused runner failure/deadline tests; never contact Docker or expose credentials."""
import argparse
import importlib.util
import io
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import time
import unittest
from contextlib import redirect_stdout
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("inventory_runtime", pathlib.Path(__file__).with_name("inventory-runtime.py"))
RUNNER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUNNER)


class InventoryRuntimeTests(unittest.TestCase):
    def invoke(self, qualify, finalize=lambda *args: True):
        with tempfile.TemporaryDirectory() as directory:
            output = io.StringIO()
            def cleanup(args, receipt, owned):
                try:
                    return finalize(args, receipt, owned)
                finally:
                    owned["temporary"].cleanup()
            with patch.object(RUNNER, "qualify", qualify), patch.object(RUNNER, "finalize", cleanup), redirect_stdout(output):
                code = RUNNER.main(["--workspace", directory, "--output-directory", directory, "--configuration", "Release"])
            receipt = json.loads((pathlib.Path(directory) / "inventory-runtime.json").read_text())
            self.assertNotIn("private-password", output.getvalue())
            return code, receipt

    def test_nonzero_csharp_exit_is_preserved_after_receipt_and_cleanup(self):
        cleaned = []
        def qualify(args, receipt, owned):
            with patch.object(RUNNER, "run_owned", return_value=subprocess.CompletedProcess([], 7)) as command:
                try:
                    RUNNER.execute_tests(args, pathlib.Path("private.json"), receipt)
                finally:
                    self.assertIn("/Release/", command.call_args.args[0][1])
        code, receipt = self.invoke(qualify, lambda *args: cleaned.append(True) or True)
        self.assertEqual((code, receipt["sdkHealthTestExitCode"], receipt["status"]), (7, 7, "failed"))
        self.assertTrue(cleaned)

    def test_exception_writes_sanitized_failure_and_cleanup_receipt(self):
        def qualify(*args):
            raise RuntimeError("private-password")
        def finalize(args, receipt, owned):
            receipt["ownedRedisRemoved"] = True
            owned["temporary"].cleanup()
            return True
        code, receipt = self.invoke(qualify, finalize)
        self.assertEqual(code, 1)
        self.assertTrue(receipt["ownedRedisRemoved"])
        self.assertEqual(receipt["failureType"], "RuntimeError")

    def test_cleanup_failure_turns_success_into_failure(self):
        code, receipt = self.invoke(lambda *args: None, lambda *args: False)
        self.assertEqual((code, receipt["status"]), (1, "failed"))

    def test_cleanup_exception_does_not_replace_primary_exit(self):
        def qualify(*args):
            raise RUNNER.CommandFailure(9)
        def finalize(*args):
            raise RuntimeError("private-password")
        code, receipt = self.invoke(qualify, finalize)
        self.assertEqual(code, 9)
        self.assertEqual(receipt["cleanupErrors"], ["RuntimeError"])

    def test_finalize_accepts_case_insensitive_object_absence(self):
        for diagnostic in ("error: no such object: owned-container", "Error: No such object: owned-container"):
            with self.subTest(diagnostic=diagnostic):
                receipt = {}
                results = [subprocess.CompletedProcess([], 0, "", ""),
                           subprocess.CompletedProcess([], 1, "", diagnostic)]
                with patch.object(RUNNER, "run", side_effect=results):
                    cleaned = RUNNER.finalize(argparse.Namespace(command_timeout=1), receipt, {"name": "owned-container"})
                self.assertTrue(cleaned)
                self.assertTrue(receipt["ownedRedisRemoved"])
                self.assertEqual(receipt["cleanupErrors"], [])

    def test_finalize_keeps_unrelated_inspect_errors_unverified(self):
        for code, diagnostic in ((1, "permission denied"), (1, "Cannot connect to the Docker daemon"),
                                 (1, "error: no such image: owned-container"), (1, None),
                                 (0, "error: no such object: owned-container")):
            with self.subTest(code=code, diagnostic=diagnostic):
                receipt = {}
                results = [subprocess.CompletedProcess([], 0, "", ""),
                           subprocess.CompletedProcess([], code, "", diagnostic)]
                with patch.object(RUNNER, "run", side_effect=results):
                    cleaned = RUNNER.finalize(argparse.Namespace(command_timeout=1), receipt, {"name": "owned-container"})
                self.assertFalse(cleaned)
                self.assertFalse(receipt["ownedRedisRemoved"])
                self.assertEqual(receipt["cleanupErrors"], ["ContainerCleanupUnverified"])

    @unittest.skipUnless(os.name == "posix", "Owned process groups require POSIX")
    def test_timeout_terminates_owned_child_group(self):
        with tempfile.TemporaryDirectory() as directory:
            pidfile = pathlib.Path(directory) / "child.pid"
            script = "import subprocess,time,pathlib,sys; p=subprocess.Popen([sys.executable,'-c','import time;time.sleep(60)']);pathlib.Path(sys.argv[1]).write_text(str(p.pid));time.sleep(60)"
            with self.assertRaises(subprocess.TimeoutExpired):
                RUNNER.run_owned([sys.executable, "-c", script, str(pidfile)], timeout=.3,
                                 stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            child = int(pidfile.read_text())
            deadline = time.monotonic() + 2
            while time.monotonic() < deadline:
                stat = pathlib.Path(f"/proc/{child}/stat")
                if not stat.exists() or stat.read_text().split(") ", 1)[1].startswith("Z"):
                    break
                time.sleep(.05)
            else:
                self.fail("Owned child survived command deadline")


if __name__ == "__main__":
    unittest.main()
