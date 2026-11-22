"""Check how the Aspire preparation hook handles an incomplete restored cache."""

import os
from pathlib import Path
import subprocess
import tempfile
import unittest


HOOK = Path(__file__).with_name("prepare-ci-package-dependencies.sh")


class PrepareCiPackageDependenciesTests(unittest.TestCase):
    def test_export_requires_both_restored_tools(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            # Simulate NuGet treating an already extracted package as up to date.
            dotnet = root / "dotnet"
            dotnet.write_text(
                '#!/bin/sh\ncase "$1" in\n'
                '  msbuild) echo 17.0.0 ;;\n  restore) exit 0 ;;\n  *) exit 2 ;;\nesac\n'
            )
            dotnet.chmod(0o755)
            packages = root / "packages"
            binaries = [
                packages / "aspire.hosting.orchestration.linux-x64/17.0.0/tools/dcp",
                packages / "aspire.dashboard.sdk.linux-x64/17.0.0/tools/Aspire.Dashboard",
            ]
            for binary in binaries:
                binary.parent.mkdir(parents=True)
                binary.write_bytes(b"fixture")
            exported = root / "github-env"
            env = dict(os.environ, PATH=f"{root}:{os.environ['PATH']}", NUGET_PACKAGES=str(packages),
                       GITHUB_ENV=str(exported), RUNNER_TEMP=directory)
            for binary in binaries:
                with self.subTest(binary=binary.name):
                    binary.write_bytes(b"")
                    result = subprocess.run(["bash", HOOK], env=env, capture_output=True, text=True, check=False)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertIn(str(binary), result.stderr)
                    self.assertFalse(exported.exists())
                    binary.write_bytes(b"fixture")

            for binary in binaries:
                binary.chmod(0o600)
            exported.write_text("EXISTING=value\n")
            subprocess.run(["bash", HOOK], env=env, check=True)
            for binary in binaries:
                self.assertTrue(os.access(binary, os.X_OK))
            self.assertEqual(exported.read_text(), "EXISTING=value\nHexalithPartiesAppHostSdkVersion=17.0.0\n")
