import copy
import json
from pathlib import Path
import runpy
import subprocess
import sys
import tempfile
import unittest
from xml.etree import ElementTree as ET


SCRIPT = Path(__file__).with_name("verify-released-catalog-results.py")
verifier = runpy.run_path(str(SCRIPT))


def report_for(lane):
    project = verifier["ASSEMBLIES"][lane]
    root = ET.Element("assemblies")
    assembly = ET.SubElement(root, "assembly", {
        "name": f"/tests/{project}.dll", "errors": "0", "failed": "0", "skipped": "0", "not-run": "0",
    })
    collection = ET.SubElement(assembly, "collection", {"failed": "0", "skipped": "0", "not-run": "0"})
    for cases in verifier["REQUIRED"][lane].values():
        for case in cases:
            name = f"{project}.{case}"
            cls, method = name.partition("(")[0].rsplit(".", 1)
            ET.SubElement(collection, "test", {
                "type": cls, "method": method, "name": name, "result": "Pass",
            })
    for node in (assembly, collection):
        node.set("total", str(len(collection)))
        node.set("passed", str(len(collection)))
    return root


class ReleasedCatalogTests(unittest.TestCase):
    def setUp(self):
        scratch = tempfile.TemporaryDirectory()
        self.addCleanup(scratch.cleanup)
        self.results = Path(scratch.name)

    def verify(self, root, lane="core"):
        ET.ElementTree(root).write(self.results / f"{lane}.xml", encoding="utf-8")
        return verifier["verify_report"](self.results, lane)

    def test_unrelated_passing_case_cannot_replace_each_required_behavior(self):
        scenarios = [
            ("DW-50", "core", 'ProcessingBucketPagesProtectScopeAndCompatibilityReadsRemainCompleteAsync(scenario: "revision")'),
            ("DW-50", "projections", "RetainedProcessingHistoryUsesBoundedBucketsAndPureLargeRebuildAsync"),
            ("DW-58", "core", "ActorOnlyHistoryBackfillSurvivesRestartAndPreservesRollbackAndForeignTenantBytes"),
            ("DW-82", "core", "EmptyLocalStateRequiresExplicitCompleteRemoteCoverage(complete: False)"),
            ("DW-168", "core", "AcceptedDeleteRetainsObligationsUntilUnitIsObservablyAbsent"),
            ("DW-100", "ui", "RegisteredReconciliationActuallyExecutesTheSelectedSharedAssembly(shared: True)"),
        ]
        for item, lane, scenario in scenarios:
            with self.subTest(scenario=scenario):
                root = report_for(lane)
                self.assertIn(item, self.verify(root, lane)["deferredItems"])
                test = next(t for t in root.iter("test") if t.get("name").endswith("." + scenario))
                test.set("method", "AnotherPassingTest")
                test.set("name", test.get("type") + ".AnotherPassingTest")
                with self.assertRaisesRegex(ValueError, item):
                    self.verify(root, lane)

    def test_duplicate_theory_result_cannot_replace_the_other_variant(self):
        root = report_for("core")
        tests = [t for t in root.iter("test") if t.get("method") == "EmptyLocalStateRequiresExplicitCompleteRemoteCoverage"]
        self.assertEqual(len(tests), 2)
        tests[1].set("name", tests[0].get("name"))
        with self.assertRaisesRegex(ValueError, "DW-82"):
            self.verify(root)

    def test_reads_the_xunit_v3_display_name_of_a_string_theory(self):
        root = report_for("core")
        test = next(t for t in root.iter("test") if t.get("name").endswith('(scenario: "revision")'))
        test.set("name", test.get("name").replace('"', r'\"'))
        self.assertIn("DW-50", self.verify(root)["deferredItems"])

    def test_xml_result_and_totals_must_agree(self):
        for field in ("total", "passed", "errors", "failed", "skipped", "not-run"):
            with self.subTest(field=field):
                root = report_for("ui")
                root.find("assembly").set(field, "999")
                with self.assertRaisesRegex(ValueError, field):
                    self.verify(root, "ui")
        root = report_for("ui")
        next(root.iter("test")).set("result", "Fail")
        with self.assertRaisesRegex(ValueError, "did not pass"):
            self.verify(root, "ui")
        root = report_for("ui")
        root.find("assembly/collection").set("failed", "1")
        with self.assertRaisesRegex(ValueError, "collection failed"):
            self.verify(root, "ui")
        root = report_for("ui")
        ET.SubElement(ET.SubElement(root.find("assembly"), "errors"), "error")
        with self.assertRaisesRegex(ValueError, "assembly errors"):
            self.verify(root, "ui")

    def test_unsuccessful_revalidation_removes_previous_success(self):
        summary = self.results / "summary.json"
        summary.write_text('{"core": {"passed": 123}}')
        result = subprocess.run([sys.executable, SCRIPT, self.results], capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("catalog.json", result.stderr)
        self.assertFalse(summary.exists())

    def test_runtime_target_requires_the_selected_packages(self):
        catalog = {"HexalithEventStoreVersion": "5.4.0", "HexalithMemoriesVersion": "4.1.0"}
        libraries = {f"{name}/{catalog[prop]}": {"type": "package"} for name, prop in verifier["PACKAGES"]["core"].items()}
        project = "Hexalith.Parties.Tests/1.0.0"
        libraries[project] = {"type": "project"}
        target = ".NETCoreApp,Version=v11.0"
        assets = {key: {"runtime": {key.partition("/")[0] + ".dll": {}}} for key in libraries}
        deps = {"libraries": libraries, "runtimeTarget": {"name": target}, "targets": {target: assets}}
        path = self.results / "core-dependencies.json"
        path.write_text(json.dumps(deps))
        verify = verifier["verify_packages"]
        self.assertEqual(verify(self.results, "core", catalog)["Hexalith.Memories.Contracts"], "4.1.0")
        for change in ("wrong-version", "source-project", "inactive-target", "missing-dll"):
            with self.subTest(change=change):
                wrong = copy.deepcopy(deps)
                identity = "Hexalith.Memories.Contracts/4.1.0"
                if change == "source-project":
                    wrong["libraries"][identity]["type"] = "project"
                elif change == "wrong-version":
                    wrong["libraries"]["Hexalith.Memories.Contracts/4.0.0"] = wrong["libraries"].pop(identity)
                elif change == "missing-dll":
                    wrong["targets"][target][identity]["runtime"] = {"Other.dll": {}}
                else:
                    wrong["targets"][target].pop(identity)
                path.write_text(json.dumps(wrong))
                with self.assertRaisesRegex(ValueError, identity):
                    verify(self.results, "core", catalog)
        (self.results / "projections-dependencies.json").write_text(json.dumps(deps))
        with self.assertRaisesRegex(ValueError, "manifest for Hexalith.Parties.Projections.Tests"):
            verify(self.results, "projections", catalog)


if __name__ == "__main__":
    unittest.main()
