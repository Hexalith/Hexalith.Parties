"""Exercise the exact pre-launch receipt entrypoint with real archive/file mutations."""
import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import time
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts/validate-aspire-runtime-cohort.py"
spec = importlib.util.spec_from_file_location("runtime_cohort", SCRIPT)
cohort = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cohort)


class RuntimeReceiptCases(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.project = self.root / "Host.csproj"
        self.project.write_text("<Project />")
        self.output = self.root / "bin/Release/net10.0"
        self.output.mkdir(parents=True)
        self.target = self.output / "Host.dll"
        self.target.write_bytes(b"prepared host entry")
        self.target.with_suffix(".runtimeconfig.json").write_text('{"runtimeOptions":{"tfm":"net10.0"}}')
        self.assets_path = self.root / "obj/project.assets.json"
        self.assets_path.parent.mkdir()
        self.cache = self.root / "packages"
        self.families = {"HexalithFrontComposerVersion": "4.7.4"}
        libraries, packages = {}, {}
        for package, version in [(name, "3.129.1") for name in sorted(cohort.CORE_IDS)] + [("Hexalith.FrontComposer.Contracts", "4.7.4")]:
            relative = package.lower() + "/" + version
            folder = self.cache / relative
            folder.mkdir(parents=True)
            entry = "lib/net10.0/" + package + ".dll"
            contents = ("actual archive runtime:" + package + "/" + version).encode()
            (self.output / (package + ".dll")).write_bytes(contents)
            archive = folder / (package.lower() + "." + version + ".nupkg")
            owner = "Hexalith.EventStore" if package in cohort.SDK_IDS else "Hexalith.FrontComposer"
            nuspec = ('<package><metadata><id>' + package + '</id><version>' + version + '</version>'
                      '<repository type="git" url="https://github.com/Hexalith/' + owner + '" commit="' + 'a' * 40 + '" />'
                      '</metadata></package>')
            with zipfile.ZipFile(archive, "w") as zipped:
                zipped.writestr(package + ".nuspec", nuspec)
                zipped.writestr(entry, contents)
            identity = package + "/" + version
            libraries[identity] = {"type": "package", "path": relative}
            packages[identity] = {"runtime": {entry: {}}}
        self.assets = {"libraries": libraries, "targets": {"net10.0": packages}, "packageFolders": {str(self.cache): {}}}
        self.assets_path.write_text(json.dumps(self.assets))
        self.deps = {"libraries": libraries, "targets": {"net10.0": packages}, "runtimeTarget": {"name": "net10.0"}}
        self.target.with_suffix(".deps.json").write_text(json.dumps(self.deps))
        self.metadata = {"Properties": {"TargetPath": str(self.target), "ProjectAssetsFile": str(self.assets_path),
                                       "Configuration": "Release", "HexalithEventStoreVersion": "3.129.1"}}
        self.receipt_path = self.root / "receipt.json"
        self.manifest = self.root / "prepared.json"

    def tearDown(self):
        self.temp.cleanup()

    def prepare(self):
        receipt = cohort.record(self.metadata, self.project, "Release", "3.129.1", 0, "runtime", self.families)
        cohort.write(self.receipt_path, receipt)
        cohort.write(self.manifest, cohort.complete([self.receipt_path], "Release", "3.129.1", [], self.cache, self.families))
        return receipt

    def launch(self, configuration="Release", families=None):
        runner = self.root / "runner-started"
        result = subprocess.run([sys.executable, str(SCRIPT), "--manifest", str(self.manifest), "--project", str(self.project),
                                 "--configuration", configuration, "--version", "3.129.1", "--families-json", json.dumps(families or self.families)],
                                capture_output=True, text=True)
        if result.returncode == 0:
            runner.write_text("started after the exact entrypoint")
        return result, runner

    def test_completed_exact_build_qualifies_the_same_entrypoint_used_before_start(self):
        receipt = self.prepare()
        self.assertEqual(4, len(receipt["sdkBinaries"]))
        result, runner = self.launch()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue(runner.exists())

    def test_missing_and_mutated_prepared_files_refuse_before_runner(self):
        for mutation in ("missing-entry", "entry", "sdk", "deps", "assets", "config", "peer-version", "receipt", "incomplete"):
            with self.subTest(mutation=mutation):
                self.tearDown(); self.setUp(); self.prepare()
                if mutation == "missing-entry": self.target.unlink()
                elif mutation == "entry": self.target.write_bytes(b"stale host entry")
                elif mutation == "sdk": (self.output / "Hexalith.EventStore.Client.dll").write_bytes(b"older SDK")
                elif mutation == "deps": self.target.with_suffix(".deps.json").write_text('{}')
                elif mutation == "assets": self.assets_path.write_text('{}')
                elif mutation == "config": self.target.with_suffix(".runtimeconfig.json").write_text('{}')
                elif mutation == "peer-version": self.families["HexalithFrontComposerVersion"] = "4.7.2"
                elif mutation == "receipt": self.receipt_path.write_text('{}')
                else: self.manifest.write_text('{"completed":false}')
                result, runner = self.launch()
                self.assertNotEqual(0, result.returncode)
                self.assertFalse(runner.exists())
                self.assertIn("Runtime SDK qualification failed", result.stderr)

    def test_cross_configuration_receipt_refuses_before_runner(self):
        self.prepare()
        result, runner = self.launch("Debug")
        self.assertNotEqual(0, result.returncode)
        self.assertFalse(runner.exists())
        self.assertIn("configuration", result.stderr)

    def test_rebinding_receipt_hashes_cannot_hide_wrong_sdk_archive_binary(self):
        receipt = self.prepare()
        binary = self.output / "Hexalith.EventStore.Client.dll"
        binary.write_bytes(b"stale actual SDK despite correct assets/property")
        receipt["files"][str(binary)] = cohort.digest(binary)
        cohort.write(self.receipt_path, receipt)
        manifest = cohort.load(self.manifest)
        manifest["projects"][str(self.project)]["sha256"] = cohort.digest(self.receipt_path)
        cohort.write(self.manifest, manifest)
        result, runner = self.launch()
        self.assertNotEqual(0, result.returncode)
        self.assertFalse(runner.exists())
        self.assertIn("differs from its actual archive", result.stderr)

    def test_record_rejects_old_entry_output_and_missing_dependencies(self):
        self.target.unlink()
        with self.assertRaisesRegex(ValueError, "entry PE"):
            self.prepare()
        self.target.write_bytes(b"entry")
        os.utime(self.target, ns=(1, 1))
        with self.assertRaisesRegex(ValueError, "entry PE"):
            cohort.record(self.metadata, self.project, "Release", "3.129.1", time.time_ns(), "runtime", self.families)
        self.target.with_suffix(".deps.json").unlink()
        with self.assertRaisesRegex(ValueError, "output is missing"):
            self.prepare()

    def test_record_rejects_actual_sdk_and_frontend_graph_drift(self):
        for mutation in ("wrong-version", "project", "missing-core", "wrong-frontend", "project-frontend", "deps-skew"):
            with self.subTest(mutation=mutation):
                self.tearDown(); self.setUp()
                libraries = self.assets["libraries"]
                if mutation == "wrong-version": libraries["Hexalith.EventStore.Client/3.128.4"] = libraries.pop("Hexalith.EventStore.Client/3.129.1")
                elif mutation == "project": libraries["Hexalith.EventStore.Client/3.129.1"]["type"] = "project"
                elif mutation == "missing-core": libraries.pop("Hexalith.EventStore.Contracts/3.129.1")
                elif mutation == "wrong-frontend": libraries["Hexalith.FrontComposer.Contracts/4.7.2"] = libraries.pop("Hexalith.FrontComposer.Contracts/4.7.4")
                elif mutation == "project-frontend": libraries["Hexalith.FrontComposer.Contracts/4.7.4"]["type"] = "project"
                else:
                    self.deps["libraries"] = dict(self.deps["libraries"])
                    self.deps["libraries"]["Hexalith.EventStore.Client/3.128.4"] = self.deps["libraries"].pop("Hexalith.EventStore.Client/3.129.1")
                    self.target.with_suffix(".deps.json").write_text(json.dumps(self.deps))
                self.assets_path.write_text(json.dumps(self.assets))
                with self.assertRaises(ValueError): self.prepare()
                self.assertFalse(self.manifest.exists())

    def test_archive_nuspec_and_binary_are_read_from_the_same_zip(self):
        self.prepare()
        archive = self.cache / "hexalith.eventstore.client/3.129.1/hexalith.eventstore.client.3.129.1.nupkg"
        with zipfile.ZipFile(archive) as old:
            entries = {name: old.read(name) for name in old.namelist()}
        nuspec = next(name for name in entries if name.endswith(".nuspec"))
        entries[nuspec] = entries[nuspec].replace(b"3.129.1", b"3.128.4")
        with zipfile.ZipFile(archive, "w") as new:
            for name, content in entries.items(): new.writestr(name, content)
        with self.assertRaisesRegex(ValueError, "Archive identity"):
            self.prepare()


if __name__ == "__main__":
    unittest.main(verbosity=2)
