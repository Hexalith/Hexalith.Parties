"""Exercise the exact pre-launch receipt entrypoint with real archive/file mutations."""
import importlib.util
import io
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
import unittest
import urllib.error
import zipfile
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts/validate-aspire-runtime-cohort.py"
spec = importlib.util.spec_from_file_location("runtime_cohort", SCRIPT)
cohort = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cohort)


class RuntimeReceiptCases(unittest.TestCase):
    def test_native_ui_wrapper_exception_requires_exact_project_type_and_bound_owner(self):
        reference = {"Hexalith.EventStore.Admin.UI/4.2.0.0": {"type": "reference"}}
        for project, bound, kind in [(cohort.MEMORY_QUALIFICATION_PROJECT, False, "reference"),
                                     (Path("unrelated.csproj"), True, "reference"),
                                     (cohort.MEMORY_QUALIFICATION_PROJECT, True, "package")]:
            with self.subTest(project=project, bound=bound, kind=kind):
                value = {"Hexalith.EventStore.Admin.UI/4.2.0.0": {"type": kind}}
                with self.assertRaisesRegex(ValueError, "Unqualified runtime SDK family"):
                    cohort.select_sdk(value, project, native_ui_bound=bound)
        self.assertEqual({}, cohort.select_sdk(reference, cohort.MEMORY_QUALIFICATION_PROJECT, native_ui_bound=True))

    def test_native_ui_wrapper_requires_a_completed_owner_receipt(self):
        self.deps["libraries"]["Hexalith.EventStore.Admin.UI/4.2.0.0"] = {"type": "reference"}
        self.target.with_suffix(".deps.json").write_text(json.dumps(self.deps))
        with self.assertRaisesRegex(ValueError, "requires the completed private Admin UI"):
            cohort.bind_native_ui(self.target, cohort.MEMORY_QUALIFICATION_PROJECT, "Release", "3.129.1", self.families, None)

    def test_native_ui_wrapper_binds_exact_owner_bytes_and_source_until_readback(self):
        self.deps["libraries"]["Hexalith.EventStore.Admin.UI/4.2.0.0"] = {"type": "reference"}
        self.target.with_suffix(".deps.json").write_text(json.dumps(self.deps))
        artifact = self.output / "Hexalith.EventStore.Admin.UI.dll"
        artifact.write_bytes(b"native UI artifact")
        source = self.root / "native-source.cs"
        source.write_text("qualified native source")
        (self.output / "native-admin-ui-artifact.tsv").write_text(str(source) + "|" + cohort.digest(source) + "\n")
        wrapper = self.root / "Qualification.csproj"
        wrapper.write_text("<Project />")
        with patch.object(cohort, "PRIVATE_ADMIN_UI_PROJECT", self.project), patch.object(cohort, "MEMORY_QUALIFICATION_PROJECT", wrapper):
            self.prepare()
            wrapper_output = self.root / "wrapper"
            shutil.copytree(self.output, wrapper_output)
            target = wrapper_output / self.target.name
            metadata = {"Properties": {**self.metadata["Properties"], "TargetPath": str(target)}}
            receipt = cohort.record(metadata, wrapper, "Release", "3.129.1", 0, "runtime", self.families, self.receipt_path)
            cohort.verify_receipt(receipt, wrapper, "Release", "3.129.1", self.families)
            for changed in (wrapper_output / artifact.name, source):
                with self.subTest(changed=changed):
                    original = changed.read_bytes()
                    try:
                        changed.write_bytes(b"unqualified replacement")
                        with self.assertRaisesRegex(ValueError, "differs|drifted"):
                            cohort.verify_receipt(receipt, wrapper, "Release", "3.129.1", self.families)
                    finally:
                        changed.write_bytes(original)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.project = self.root / "Host.csproj"
        self.project.write_text("<Project />")
        self.output = self.root / "bin/Release/net10.0"
        self.output.mkdir(parents=True)
        self.target = self.output / "Host.dll"
        self.target.write_bytes(b"prepared host entry")
        self.target.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {
            "tfm": "net10.0", "framework": {"name": "Microsoft.NETCore.App", "version": "10.0.0"}}}))
        self.assets_path = self.root / "obj/project.assets.json"
        self.assets_path.parent.mkdir()
        self.cache = self.root / "packages"
        self.official = self.root / "official"
        self.official.mkdir()
        self.families = {"HexalithFrontComposerVersion": "4.7.4", "HexalithMcpCliVersion": "2.0.1"}
        libraries, packages = {}, {}
        for package, version in [(name, "3.129.1") for name in sorted(cohort.CORE_IDS)] + [("Hexalith.FrontComposer.Contracts", "4.7.4"), ("Hexalith.McpCli.Abstractions", "2.0.1")]:
            self.add_package(package, version, libraries, packages)
        self.assets = {"project": {"frameworks": {"net10.0": {}}}, "libraries": libraries, "targets": {"net10.0": packages}, "packageFolders": {str(self.cache): {}}}
        self.assets_path.write_text(json.dumps(self.assets))
        self.deps = {"libraries": libraries, "targets": {"net10.0": packages}, "runtimeTarget": {"name": "net10.0"}}
        self.target.with_suffix(".deps.json").write_text(json.dumps(self.deps))
        self.metadata = {"Properties": {"TargetPath": str(self.target), "ProjectAssetsFile": str(self.assets_path),
                                       "Configuration": "Release", "HexalithEventStoreVersion": "3.129.1", "TargetFramework": "net10.0", "HexalithPartiesRuntimeTargetFramework": "net10.0",
                                       "CustomAfterMicrosoftCommonProps": str(cohort.RUNTIME_PROFILE)}}
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
        shutil.copyfile(archive, self.official / archive.name)
        identity = package + "/" + version
        libraries[identity] = {"type": "package", "path": relative}
        packages[identity] = {"runtime": {entry: {}}}

    def tearDown(self):
        self.temp.cleanup()

    def prepare(self):
        receipt = cohort.record(self.metadata, self.project, "Release", "3.129.1", 0, "runtime", self.families)
        cohort.write(self.receipt_path, receipt)
        def official(package, cache):
            package_id, version = package.split("=", 1)
            archive = self.official / (package_id.lower() + "." + version + ".nupkg")
            owner = next(name for name in ("Hexalith.EventStore", "Hexalith.FrontComposer", "Hexalith.McpCli") if package_id.startswith(name + "."))
            return cohort.archive_identity(archive, package_id, version, "https://github.com/Hexalith/" + owner)
        with patch.object(cohort, "public_archive", side_effect=official):
            cohort.write(self.manifest, cohort.complete([self.receipt_path], "Release", "3.129.1", [], self.cache, self.families))
        return receipt

    def test_evaluated_framework_must_match_the_explicit_preparation_profile(self):
        with self.assertRaisesRegex(ValueError, "Evaluated runtime target framework"):
            cohort.record(self.metadata, self.project, "Release", "3.129.1", 0, "runtime", self.families,
                          target_framework="net11.0")
        for value in (None, "", "net9.0", "net12.0", "net10.0;net11.0"):
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, "qualified target framework"):
                cohort.record({"Properties": {**self.metadata["Properties"], "TargetFramework": value}},
                              self.project, "Release", "3.129.1", 0, "runtime", self.families)

    def test_restore_dependency_and_runtime_framework_drift_refuse_preparation(self):
        for graph in ("restore-framework", "restore-target", "dependency-framework", "dependency-target", "runtime"):
            with self.subTest(graph=graph):
                self.tearDown(); self.setUp()
                if graph.startswith("restore"):
                    if graph == "restore-framework": self.assets["project"]["frameworks"] = {"net11.0": {}}
                    else: self.assets["targets"]["net11.0"] = self.assets["targets"].pop("net10.0")
                    self.assets_path.write_text(json.dumps(self.assets))
                elif graph.startswith("dependency"):
                    if graph == "dependency-framework": self.deps["runtimeTarget"]["name"] = ".NETCoreApp,Version=v11.0"
                    else: self.deps["targets"] = {"net11.0": self.deps["targets"]["net10.0"]}
                    self.target.with_suffix(".deps.json").write_text(json.dumps(self.deps))
                else:
                    self.target.with_suffix(".runtimeconfig.json").write_text('{"runtimeOptions":{"tfm":"net11.0"}}')
                with self.assertRaisesRegex(ValueError, "framework|profile"):
                    self.prepare()
                self.assertFalse(self.manifest.exists())

    def test_profile_selector_path_and_receipt_binding_refuse_substitution(self):
        for key, value in (("HexalithPartiesRuntimeTargetFramework", "net11.0"),
                           ("CustomAfterMicrosoftCommonProps", str(self.root / "foreign.props"))):
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, "runtime build profile"):
                cohort.record({"Properties": {**self.metadata["Properties"], key: value}}, self.project,
                              "Release", "3.129.1", 0, "runtime", self.families)
        receipt = self.prepare()
        del receipt["files"][str(cohort.RUNTIME_PROFILE)]
        cohort.write(self.receipt_path, receipt)
        manifest = cohort.load(self.manifest)
        manifest["projects"][str(self.project)]["sha256"] = cohort.digest(self.receipt_path)
        cohort.write(self.manifest, manifest)
        result, runner = self.launch()
        self.assertNotEqual(0, result.returncode)
        self.assertFalse(runner.exists())
        self.assertIn("build profile is not bound", result.stderr)

    def test_framework_mismatch_and_missing_selection_refuse_actual_launch(self):
        self.prepare()
        result, runner = self.launch(target_framework="net11.0")
        self.assertNotEqual(0, result.returncode)
        self.assertFalse(runner.exists())
        self.assertIn("target framework differs", result.stderr)
        result = subprocess.run([sys.executable, str(SCRIPT), "--manifest", str(self.manifest), "--project", str(self.project),
                                 "--configuration", "Release", "--version", "3.129.1"], capture_output=True, text=True)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("--target-framework", result.stderr)

    def use_framework(self, framework, aspnet=False):
        packages = next(iter(self.assets["targets"].values()))
        references = {"Microsoft.NETCore.App": {"privateAssets": "all"}}
        declarations = [{"name": "Microsoft.NETCore.App", "version": framework[3:] + ".0"}]
        if aspnet:
            references["Microsoft.AspNetCore.App"] = {"privateAssets": "none"}
            declarations.append({"name": "Microsoft.AspNetCore.App", "version": framework[3:] + ".0"})
        self.assets["project"]["frameworks"] = {framework: {"frameworkReferences": references}}
        self.assets["targets"] = {framework: packages}
        self.assets_path.write_text(json.dumps(self.assets))
        runtime_target = ".NETCoreApp,Version=v" + framework[3:]
        self.deps["targets"] = {runtime_target: packages}
        self.deps["runtimeTarget"]["name"] = runtime_target
        self.target.with_suffix(".deps.json").write_text(json.dumps(self.deps))
        options = {"tfm": framework}
        options.update({"frameworks": declarations} if aspnet else {"framework": declarations[0]})
        self.target.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": options}))
        self.metadata["Properties"]["TargetFramework"] = framework
        self.metadata["Properties"]["HexalithPartiesRuntimeTargetFramework"] = framework

    def test_real_shape_shared_framework_declarations_qualify_both_generations(self):
        for framework in ("net10.0", "net11.0"):
            for aspnet in (False, True):
                with self.subTest(framework=framework, aspnet=aspnet):
                    self.tearDown(); self.setUp(); self.use_framework(framework, aspnet)
                    receipt = self.prepare()
                    self.assertEqual(framework, receipt["targetFramework"])
                    result, runner = self.launch(target_framework=framework)
                    self.assertEqual(0, result.returncode, result.stderr)
                    self.assertTrue(runner.exists())

    def test_shared_framework_declarations_refuse_otherwise_coherent_missing_malformed_or_foreign_runtime(self):
        for framework in ("net10.0", "net11.0"):
            core = {"name": "Microsoft.NETCore.App", "version": framework[3:] + ".0"}
            aspnet = {"name": "Microsoft.AspNetCore.App", "version": framework[3:] + ".0"}
            other = "10.0.0" if framework == "net11.0" else "11.0.0"
            cases = [
                {}, {"framework": None}, {"frameworks": []}, {"frameworks": core}, {"frameworks": [None]},
                {"frameworks": [{"version": core["version"]}, aspnet]},
                {"frameworks": [{"name": core["name"]}, aspnet]},
                {"frameworks": [{"name": core["name"], "version": 11}, aspnet]},
                {"frameworks": [{**core, "version": "malformed"}, aspnet]},
                {"frameworks": [{**core, "version": framework[3:] + ".0-preview.1"}, aspnet]},
                {"frameworks": [{**core, "version": framework[3:].replace(".0", ".1") + ".0"}, aspnet]},
                {"frameworks": [{**core, "version": other}, aspnet], "rollForward": "Disable"},
                {"frameworks": [core, {**aspnet, "version": other}]},
                {"frameworks": [aspnet]}, {"framework": core},
                {"frameworks": [core, aspnet, core]},
                {"frameworks": [core, aspnet, {"name": "Foreign.App", "version": core["version"]}]},
                {"framework": core, "frameworks": [core, aspnet]},
                {"includedFrameworks": [core, aspnet]},
            ]
            for declarations in cases:
                with self.subTest(framework=framework, declarations=declarations):
                    self.tearDown(); self.setUp(); self.use_framework(framework, aspnet=True)
                    self.target.with_suffix(".runtimeconfig.json").write_text(json.dumps({
                        "runtimeOptions": {"tfm": framework, **declarations}}))
                    with self.assertRaisesRegex(ValueError, "shared framework"):
                        self.prepare()
                    self.assertFalse(self.manifest.exists())

    def test_rebound_hashes_cannot_hide_foreign_shared_framework_generation(self):
        self.use_framework("net11.0", aspnet=True)
        receipt = self.prepare()
        config = self.target.with_suffix(".runtimeconfig.json")
        document = cohort.load(config)
        document["runtimeOptions"]["frameworks"][0]["version"] = "10.0.0"
        document["runtimeOptions"]["rollForward"] = "Disable"
        config.write_text(json.dumps(document))
        receipt["files"][str(config)] = cohort.digest(config)
        cohort.write(self.receipt_path, receipt)
        manifest = cohort.load(self.manifest)
        manifest["projects"][str(self.project)]["sha256"] = cohort.digest(self.receipt_path)
        cohort.write(self.manifest, manifest)
        result, runner = self.launch(target_framework="net11.0")
        self.assertNotEqual(0, result.returncode)
        self.assertFalse(runner.exists())
        self.assertIn("shared framework", result.stderr)

    def test_net11_runtime_keeps_exact_older_published_package_bodies(self):
        self.use_framework("net11.0", aspnet=True)
        receipt = self.prepare()
        self.assertEqual("net11.0", receipt["targetFramework"])
        self.assertTrue(all(binary["entry"].startswith("lib/net10.0/") for binary in receipt["sdkBinaries"]))
        result, runner = self.launch(target_framework="net11.0")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue(runner.exists())

    def test_repacked_local_archive_cannot_authenticate_its_own_runtime(self):
        archive = next((self.cache / "hexalith.eventstore.client").rglob("*.nupkg"))
        with zipfile.ZipFile(archive, "a") as repacked:
            repacked.writestr("repacked.txt", "same declared identity and deployed DLL")
        with self.assertRaisesRegex(ValueError, "independently retrieved official"):
            self.prepare()
        self.assertFalse(self.manifest.exists())

    def test_apphost_and_native_libraries_are_bound_until_launch(self):
        for name in ("Host", "libnative.so", "libnative.so.1", "libnative.dylib"):
            for mutation in ("change", "remove", "add"):
                with self.subTest(name=name, mutation=mutation):
                    self.tearDown(); self.setUp()
                    selected = self.output / name
                    if mutation != "add": selected.write_bytes(b"actual prepared native body")
                    self.prepare()
                    if mutation == "remove": selected.unlink()
                    else: selected.write_bytes(b"unprepared native body")
                    result, runner = self.launch()
                    self.assertNotEqual(0, result.returncode)
                    self.assertFalse(runner.exists())

    def add_external_static_assets(self):
        roots = []
        for name in ("compressed", "jsmodules", "scopedcss", "source-wwwroot", "nuget-staticwebassets"):
            content = self.root / "external" / name
            content.mkdir(parents=True)
            (content / "asset.css").write_text("prepared " + name)
            roots.append(content)
        for index, selected in enumerate((roots[:3], roots[3:])):
            (self.output / ("Consumed" + str(index) + ".staticwebassets.runtime.json")).write_text(json.dumps({
                "ContentRoots": [str(path) for path in selected],
                "Root": {"Children": {str(i): {"Asset": {"ContentRootIndex": i, "SubPath": "asset.css"}}
                                      for i in range(len(selected))},
                         "Patterns": [{"ContentRootIndex": i, "Pattern": "**", "Depth": 0} for i in range(len(selected))]}}))
        return roots

    def test_every_consumed_static_manifest_binds_its_external_runtime_files(self):
        roots = self.add_external_static_assets()
        receipt = self.prepare()
        self.assertTrue(all(str(root / "asset.css") in receipt["files"] for root in roots))
        result, runner = self.launch()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue(runner.exists())

    def test_external_static_asset_addition_removal_and_mutation_refuse_launch(self):
        for index in range(5):
            for mutation in ("add", "remove", "change"):
                with self.subTest(content_root=index, mutation=mutation):
                    self.tearDown(); self.setUp()
                    roots = self.add_external_static_assets()
                    self.prepare()
                    asset = roots[index] / "asset.css"
                    if mutation == "add": (roots[index] / "new.js").write_text("not prepared")
                    elif mutation == "remove": asset.unlink()
                    else: asset.write_text("changed after preparation")
                    result, runner = self.launch()
                    self.assertNotEqual(0, result.returncode)
                    self.assertFalse(runner.exists())

    def test_linked_external_static_assets_refuse_preparation(self):
        roots = self.add_external_static_assets()
        (roots[0] / "linked.css").symlink_to(roots[1] / "asset.css")
        with self.assertRaisesRegex(ValueError, "Linked static web assets"):
            self.prepare()

    def test_real_downloader_replaces_stale_cache_from_independent_transport(self):
        name = "hexalith.eventstore.client.3.129.1.nupkg"
        official = (self.official / name).read_bytes()
        cache = self.root / "downloaded"
        cache.mkdir()
        (cache / name).write_bytes(b"untrusted cached bytes")
        with patch.object(cohort.urllib.request, "urlopen", return_value=io.BytesIO(official)) as download:
            proof = cohort.public_archive("Hexalith.EventStore.Client=3.129.1", cache)
        self.assertEqual(official, (cache / name).read_bytes())
        self.assertEqual(cohort.digest(cache / name), proof["sha256"])
        download.assert_called_once_with("https://api.nuget.org/v3-flatcontainer/hexalith.eventstore.client/3.129.1/" + name, timeout=30)

    def test_download_retries_transient_failure_without_accepting_cache(self):
        name = "hexalith.eventstore.client.3.129.1.nupkg"
        official = (self.official / name).read_bytes()
        with patch.object(cohort.urllib.request, "urlopen", side_effect=[urllib.error.URLError("transient"), io.BytesIO(official)]) as download, \
                patch.object(cohort.time, "sleep"):
            proof = cohort.public_archive("Hexalith.EventStore.Client=3.129.1", self.root / "downloaded")
        self.assertEqual(2, download.call_count)
        self.assertEqual("Hexalith.EventStore.Client", proof["id"])

    def test_download_failure_does_not_fall_back_to_existing_cache(self):
        cache = self.root / "downloaded"
        cache.mkdir()
        (cache / "hexalith.eventstore.client.3.129.1.nupkg").write_bytes(b"untrusted cached bytes")
        with patch.object(cohort.urllib.request, "urlopen", side_effect=urllib.error.URLError("unavailable")) as download, \
                patch.object(cohort.time, "sleep"):
            with self.assertRaises(urllib.error.URLError):
                cohort.public_archive("Hexalith.EventStore.Client=3.129.1", cache)
        self.assertEqual(3, download.call_count)

    def test_declared_portal_uses_its_actual_client_contracts_and_signalr_packages(self):
        self.project = cohort.PARTIES_UI_PROJECT
        removed = "Hexalith.EventStore.ServiceDefaults/3.129.1"
        self.assets["libraries"].pop(removed)
        self.assets["targets"]["net10.0"].pop(removed)
        (self.output / "Hexalith.EventStore.ServiceDefaults.dll").unlink()
        self.add_package("Hexalith.EventStore.SignalR", "3.129.1", self.assets["libraries"], self.assets["targets"]["net10.0"])
        self.assets_path.write_text(json.dumps(self.assets))
        self.target.with_suffix(".deps.json").write_text(json.dumps(self.deps))
        receipt = self.prepare()
        self.assertEqual(cohort.PARTIES_UI_IDS, {proof["id"] for proof in receipt["sdkArchives"] if proof["id"] in cohort.SDK_IDS})
        result, runner = self.launch()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue(runner.exists())

    def launch(self, configuration="Release", families=None, target_framework="net10.0"):
        runner = self.root / "runner-started"
        result = subprocess.run([sys.executable, str(SCRIPT), "--manifest", str(self.manifest), "--project", str(self.project),
                                 "--configuration", configuration, "--version", "3.129.1", "--families-json", json.dumps(families or self.families),
                                 "--target-framework", target_framework],
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

    def test_native_ui_provenance_requires_source_rows_at_record_and_readback(self):
        headers = ("sourceProject|/selected/UI.csproj\nsourceCommit|" + "a" * 40
                   + "\nsdkVersion|3.129.1\nproperties|Configuration=Release\n")
        source = self.root / "NativeUI.cs"
        source.write_text("actual selected source")
        proof = self.output / "native-admin-ui-artifact.tsv"
        for contents in ("", headers):
            with self.subTest(contents=bool(contents), phase="record"):
                proof.write_text(contents)
                with self.assertRaisesRegex(ValueError, "requires at least one absolute source/hash row"):
                    cohort.record(self.metadata, self.project, "Release", "3.129.1", 0, "runtime", self.families)
            with self.subTest(contents=bool(contents), phase="readback"):
                proof.write_text(headers + str(source) + "|" + cohort.digest(source) + "\n")
                receipt = cohort.record(self.metadata, self.project, "Release", "3.129.1", 0, "runtime", self.families)
                cohort.verify_receipt(receipt, self.project, "Release", "3.129.1", self.families)
                proof.write_text(contents)
                # Even a newly supplied matching file hash cannot bless source-free provenance.
                receipt["files"][str(proof)] = cohort.digest(proof)
                with self.assertRaisesRegex(ValueError, "requires at least one absolute source/hash row"):
                    cohort.verify_receipt(receipt, self.project, "Release", "3.129.1", self.families)

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
                added.write_bytes(b"{}" if "staticwebassets" in filename else b"unprepared runtime input")
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
