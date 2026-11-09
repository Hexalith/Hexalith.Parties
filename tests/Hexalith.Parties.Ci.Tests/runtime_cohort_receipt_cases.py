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
        self.families = {"HexalithFrontComposerVersion": "4.7.4", "HexalithMcpCliVersion": "2.0.1"}
        libraries, packages = {}, {}
        for package, version in [(name, "3.129.1") for name in sorted(cohort.CORE_IDS)] + [("Hexalith.FrontComposer.Contracts", "4.7.4"), ("Hexalith.McpCli.Abstractions", "2.0.1")]:
            self.add_package(package, version, libraries, packages)
        self.assets = {"libraries": libraries, "targets": {"net10.0": packages}, "packageFolders": {str(self.cache): {}}}
        self.assets_path.write_text(json.dumps(self.assets))
        self.deps = {"libraries": libraries, "targets": {"net10.0": packages}, "runtimeTarget": {"name": "net10.0"}}
        self.target.with_suffix(".deps.json").write_text(json.dumps(self.deps))
        self.metadata = {"Properties": {"TargetPath": str(self.target), "ProjectAssetsFile": str(self.assets_path),
                                       "Configuration": "Release", "HexalithEventStoreVersion": "3.129.1"}}
        self.receipt_path = self.root / "receipt.json"
        self.manifest = self.root / "prepared.json"

    def add_package(self, package, version, libraries, packages):
        relative = package.lower() + "/" + version
        folder = self.cache / relative
        folder.mkdir(parents=True)
        entry = "lib/net10.0/" + package + ".dll"
        contents = ("actual archive runtime:" + package + "/" + version).encode()
        (self.output / (package + ".dll")).write_bytes(contents)
        archive = folder / (package.lower() + "." + version + ".nupkg")
        owner = next(name for name in ("Hexalith.EventStore", "Hexalith.FrontComposer", "Hexalith.McpCli") if package.startswith(name + "."))
        nuspec = ('<package><metadata><id>' + package + '</id><version>' + version + '</version>'
                  '<repository type="git" url="https://github.com/Hexalith/' + owner + '" commit="' + 'a' * 40 + '" />'
                  '</metadata></package>')
        with zipfile.ZipFile(archive, "w") as zipped:
            zipped.writestr(package + ".nuspec", nuspec)
            zipped.writestr(entry, contents)
        identity = package + "/" + version
        libraries[identity] = {"type": "package", "path": relative}
        packages[identity] = {"runtime": {entry: {}}}

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

    def use_admin_graph(self):
        self.project = cohort.ADMIN_SERVER_PROJECT
        client = "Hexalith.EventStore.Client/3.129.1"
        self.assets["libraries"].pop(client)
        self.assets["targets"]["net10.0"].pop(client)
        (self.output / "Hexalith.EventStore.Client.dll").unlink()
        for package in sorted(cohort.ADMIN_SERVER_IDS - cohort.CORE_IDS):
            self.add_package(package, "3.129.1", self.assets["libraries"], self.assets["targets"]["net10.0"])
        self.assets_path.write_text(json.dumps(self.assets))
        self.target.with_suffix(".deps.json").write_text(json.dumps(self.deps))

    def test_declared_admin_server_qualifies_its_actual_transport_without_sdk_client(self):
        self.use_admin_graph()
        receipt = self.prepare()
        self.assertEqual(cohort.ADMIN_SERVER_IDS, {p["id"] for p in receipt["sdkArchives"] if p["id"] in cohort.SDK_IDS})
        result, runner = self.launch()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue(runner.exists())

    def test_admin_server_requires_each_owned_dependency_in_restore_and_deployment(self):
        for package in sorted(cohort.ADMIN_SERVER_IDS):
            for graph in ("assets", "deps"):
                with self.subTest(package=package, graph=graph):
                    self.tearDown(); self.setUp(); self.use_admin_graph()
                    identity = package + "/3.129.1"
                    if graph == "assets":
                        self.assets["libraries"].pop(identity)
                        self.assets_path.write_text(json.dumps(self.assets))
                    else:
                        deps = json.loads(self.target.with_suffix(".deps.json").read_text())
                        deps["libraries"].pop(identity)
                        self.target.with_suffix(".deps.json").write_text(json.dumps(deps))
                    with self.assertRaisesRegex(ValueError, "incomplete|dependency manifest"):
                        self.prepare()
                    self.assertFalse(self.manifest.exists())

    def test_admin_readback_cannot_rebind_hashes_to_hide_missing_owned_dependencies(self):
        for package in sorted(cohort.ADMIN_SERVER_IDS):
            for graph in ("assets", "deps"):
                with self.subTest(package=package, graph=graph):
                    self.tearDown(); self.setUp(); self.use_admin_graph()
                    receipt = self.prepare()
                    path = self.assets_path if graph == "assets" else self.target.with_suffix(".deps.json")
                    value = json.loads(path.read_text())
                    value["libraries"].pop(package + "/3.129.1")
                    path.write_text(json.dumps(value))
                    receipt["files"][str(path)] = cohort.digest(path)
                    cohort.write(self.receipt_path, receipt)
                    manifest = cohort.load(self.manifest)
                    manifest["projects"][str(self.project)]["sha256"] = cohort.digest(self.receipt_path)
                    cohort.write(self.manifest, manifest)
                    result, runner = self.launch()
                    self.assertNotEqual(0, result.returncode)
                    self.assertFalse(runner.exists())
                    self.assertRegex(result.stderr, "incomplete|dependency manifest")

    def test_admin_server_refuses_foreign_versions_source_edges_and_stale_binary(self):
        for mutation in ("version", "source", "binary"):
            with self.subTest(mutation=mutation):
                self.tearDown(); self.setUp(); self.use_admin_graph()
                package = "Hexalith.EventStore.Admin.Server"
                if mutation == "version":
                    self.assets["libraries"][package + "/3.128.4"] = self.assets["libraries"].pop(package + "/3.129.1")
                elif mutation == "source":
                    self.assets["libraries"][package + "/3.129.1"]["type"] = "project"
                else:
                    (self.output / (package + ".dll")).write_bytes(b"older Admin runtime")
                self.assets_path.write_text(json.dumps(self.assets))
                with self.assertRaisesRegex(ValueError, "cohort differs|differs from its actual archive"):
                    self.prepare()
                self.assertFalse(self.manifest.exists())

    def test_admin_graph_cannot_exempt_an_ordinary_or_same_named_foreign_host(self):
        for name in ("Host.csproj", cohort.ADMIN_SERVER_PROJECT.name):
            with self.subTest(project=name):
                self.tearDown(); self.setUp(); self.use_admin_graph()
                project = self.root / name
                self.project = project
                project.write_text("<Project />")
                with self.assertRaisesRegex(ValueError, "incomplete: Hexalith.EventStore.Client"):
                    self.prepare()
                self.assertFalse(self.manifest.exists())

    def test_completed_exact_build_qualifies_the_same_entrypoint_used_before_start(self):
        receipt = self.prepare()
        self.assertEqual(cohort.CORE_IDS | {"Hexalith.FrontComposer.Contracts", "Hexalith.McpCli.Abstractions"},
                         {Path(binary["path"]).stem for binary in receipt["sdkBinaries"]})
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

    def test_native_ui_assets_and_application_source_are_bound_until_launch(self):
        for changed in ("output-asset", "source-asset", "native-app", "runtime-assets", "provenance"):
            with self.subTest(changed=changed):
                self.tearDown(); self.setUp()
                output_asset = self.output / "wwwroot/css/app.css"
                output_asset.parent.mkdir(parents=True)
                output_asset.write_bytes(b"actual native CSS")
                source_asset = self.root / "selected-ui/wwwroot/css/app.css"
                source_asset.parent.mkdir(parents=True)
                source_asset.write_bytes(output_asset.read_bytes())
                native_app = self.root / "selected-ui/bin/NativeUI.dll"
                native_app.parent.mkdir(parents=True)
                native_app.write_bytes(b"actual selected native application")
                runtime = self.output / "Host.staticwebassets.runtime.json"
                runtime.write_text(json.dumps({"ContentRoots": [str(source_asset.parent)]}))
                provenance = self.output / "native-admin-ui-artifact.tsv"
                provenance.write_text("sourceCommit|" + "a" * 40 + "\n" + "\n".join(
                    str(path) + "|" + cohort.digest(path) for path in (source_asset, native_app)) + "\n")
                receipt = self.prepare()
                for bound in (output_asset, source_asset, native_app, runtime, provenance):
                    self.assertIn(str(bound), receipt["files"])
                result, runner = self.launch()
                self.assertEqual(0, result.returncode, result.stderr)
                runner.unlink()
                selected = {"output-asset": output_asset, "source-asset": source_asset,
                            "native-app": native_app, "runtime-assets": runtime, "provenance": provenance}[changed]
                selected.write_bytes(b"mutated after the completed build")
                result, runner = self.launch()
                self.assertNotEqual(0, result.returncode)
                self.assertFalse(runner.exists())
                self.assertIn("drifted", result.stderr)

    def test_native_ui_provenance_refuses_an_unmatched_initial_file_hash(self):
        native_app = self.root / "NativeUI.dll"
        native_app.write_bytes(b"actual native application")
        (self.output / "native-admin-ui-artifact.tsv").write_text(str(native_app) + "|" + "0" * 64 + "\n")
        with self.assertRaisesRegex(ValueError, "Native UI artifact provenance"):
            self.prepare()
        self.assertFalse(self.manifest.exists())

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
        for mutation in ("wrong-version", "project", "missing-core", "wrong-frontend", "project-frontend", "wrong-mcpcli", "project-mcpcli", "deps-skew"):
            with self.subTest(mutation=mutation):
                self.tearDown(); self.setUp()
                libraries = self.assets["libraries"]
                if mutation == "wrong-version": libraries["Hexalith.EventStore.Client/3.128.4"] = libraries.pop("Hexalith.EventStore.Client/3.129.1")
                elif mutation == "project": libraries["Hexalith.EventStore.Client/3.129.1"]["type"] = "project"
                elif mutation == "missing-core": libraries.pop("Hexalith.EventStore.Contracts/3.129.1")
                elif mutation == "wrong-frontend": libraries["Hexalith.FrontComposer.Contracts/4.7.2"] = libraries.pop("Hexalith.FrontComposer.Contracts/4.7.4")
                elif mutation == "project-frontend": libraries["Hexalith.FrontComposer.Contracts/4.7.4"]["type"] = "project"
                elif mutation == "wrong-mcpcli": libraries["Hexalith.McpCli.Abstractions/2.0.0"] = libraries.pop("Hexalith.McpCli.Abstractions/2.0.1")
                elif mutation == "project-mcpcli": libraries["Hexalith.McpCli.Abstractions/2.0.1"]["type"] = "project"
                else:
                    self.deps["libraries"] = dict(self.deps["libraries"])
                    self.deps["libraries"]["Hexalith.EventStore.Client/3.128.4"] = self.deps["libraries"].pop("Hexalith.EventStore.Client/3.129.1")
                    self.target.with_suffix(".deps.json").write_text(json.dumps(self.deps))
                self.assets_path.write_text(json.dumps(self.assets))
                with self.assertRaises(ValueError): self.prepare()
                self.assertFalse(self.manifest.exists())

    def test_unqualified_family_members_refuse_in_restore_and_deployed_graph(self):
        for package in ("Hexalith.EventStore.Unqualified", "Hexalith.EventStore.Admin.Cli", "hexalith.eventstore.client"):
            for graph in ("assets", "deps"):
                with self.subTest(package=package, graph=graph):
                    self.tearDown(); self.setUp()
                    path = self.assets_path if graph == "assets" else self.target.with_suffix(".deps.json")
                    data = json.loads(path.read_text())
                    data["libraries"][package + "/9.9.9"] = {"type": "project"}
                    path.write_text(json.dumps(data))
                    with self.assertRaises(ValueError): self.prepare()
                    self.assertFalse(self.manifest.exists())

    def test_only_actual_declared_sample_application_project_is_exempt_from_sdk_versions(self):
        identity = "Hexalith.EventStore.Sample.Contracts/1.0.0"
        for path in (str(cohort.SAMPLE_CONTRACTS_PROJECT), str(cohort.NATIVE_SAMPLE_CONTRACTS_PROJECT), "foreign/Contracts.csproj"):
            with self.subTest(path=path):
                self.tearDown(); self.setUp()
                metadata = {"type": "project", "msbuildProject": path}
                self.assets["libraries"][identity] = metadata
                self.assets_path.write_text(json.dumps(self.assets))
                self.target.with_suffix(".deps.json").write_text(json.dumps(self.deps))
                if path != "foreign/Contracts.csproj":
                    self.deps["libraries"]["Hexalith.EventStore.Sample.Contracts.Reference/1.0.0.0"] = {"type": "reference"}
                    self.target.with_suffix(".deps.json").write_text(json.dumps(self.deps))
                    self.prepare()
                    result, runner = self.launch()
                    self.assertEqual(0, result.returncode, result.stderr)
                    self.assertTrue(runner.exists())
                else:
                    with self.assertRaisesRegex(ValueError, "Unqualified"): self.prepare()

    def test_added_configuration_binaries_and_assets_refuse_receipt_readback(self):
        for filename in ("appsettings.Development.json", "Unrecorded.dll", "wwwroot/new.js", "native-ui-artifact/new.json", "Host.staticwebassets.new.json"):
            with self.subTest(filename=filename):
                self.tearDown(); self.setUp(); self.prepare()
                added = self.output / filename
                added.parent.mkdir(parents=True, exist_ok=True)
                added.write_bytes(b"unprepared runtime input")
                result, runner = self.launch()
                self.assertNotEqual(0, result.returncode)
                self.assertFalse(runner.exists())
                self.assertIn("inventory drifted", result.stderr)

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
