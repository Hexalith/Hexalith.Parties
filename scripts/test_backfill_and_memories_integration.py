import base64
import hashlib
import json
from pathlib import Path
import runpy
import tempfile
import unittest
from xml.etree import ElementTree as ET


runner = runpy.run_path(str(Path(__file__).with_name("verify-backfill-and-memories-deferrals.py")))


class BackfillMemoriesReportTests(unittest.TestCase):
    def report(self, lane, project):
        return self.report_for_methods(runner["MEMORIES_METHODS"][lane], project)

    def report_for_methods(self, expected, project):
        assembly = ET.Element("assembly", name=project + ".dll", errors="0")
        collection = ET.SubElement(assembly, "collection")
        for method, arguments in expected.items():
            cls, name = method.rsplit(".", 1)
            for args in arguments:
                ET.SubElement(collection, "test", type=cls, method=name, name=method + args, result="Pass")
        self.recount(assembly)
        return assembly

    def recount(self, assembly):
        for node in (assembly, *assembly.findall("collection")):
            total = str(len(node.findall(".//test")))
            node.attrib.update(total=total, passed=total, skipped="0", failed="0", **{"not-run": "0"})

    def verify(self, assembly, lane, project):
        root = ET.Element("assemblies")
        root.append(assembly)
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "results.xml"
            ET.ElementTree(root).write(report)
            return runner["check_report"](report, runner["MEMORIES_METHODS"][lane], project)

    def write_workloads(self, directory, *, empty_operation="operation-empty"):
        phases = ("seed", "interrupt", "rebuild", "stage", "commit", "verify")
        assembly = self.report_for_methods({runner["WORKLOAD_METHOD"]: runner["FACT"]}, runner["BACKFILL_PROJECT"])
        root = ET.Element("assemblies")
        root.append(assembly)
        output = ET.SubElement(assembly.find("collection/test"), "output")
        outcomes = {}
        for tenant, existing in (("empty", False), ("existing", True)):
            target = directory / tenant
            target.mkdir()
            operation = "operation-existing" if existing else empty_operation
            manifest = json.dumps({"tenant": tenant, "existing": existing, "indexOperation": operation}, indent=2).encode()
            (target / "input.json").write_bytes(manifest)
            digest = hashlib.sha256(manifest).hexdigest()
            for pid, phase in enumerate(phases, start=1):
                receipt = json.dumps({
                    "tenant": tenant, "operation": operation, "phase": phase, "processId": pid, "inputSha256": digest,
                }).encode("utf-8")
                encoded = base64.b64encode(receipt).decode("ascii")
                output.text = f"Synthetic diagnostic\n{runner['WORKLOAD_RECEIPT_PREFIX']}{encoded}\n"
                ET.ElementTree(root).write(target / f"{phase}.xml")
                (target / f"{phase}.json").write_bytes(receipt)
            outcomes[tenant] = {"tenant": tenant, "inputSha256": digest, "phases": list(phases)}
        return outcomes

    def test_every_phase_binds_the_retained_manifest_for_both_starting_states(self):
        with tempfile.TemporaryDirectory() as tmp:
            directory = Path(tmp)
            expected = self.write_workloads(directory)
            self.assertEqual(runner["verify_workloads"](directory), expected)

    def test_workloads_reject_xml_from_another_phase_or_target(self):
        for source in (Path("empty/seed.xml"), Path("existing/stage.xml")):
            with self.subTest(source=source), tempfile.TemporaryDirectory() as tmp:
                directory = Path(tmp)
                self.write_workloads(directory)
                (directory / "empty/stage.xml").write_bytes((directory / source).read_bytes())
                with self.assertRaisesRegex(ValueError, "passing workload test must contain one matching XML receipt"):
                    runner["verify_workloads"](directory)

    def test_workloads_require_exactly_one_verbatim_receipt_on_the_passing_test(self):
        for defect in ("misplaced", "duplicate", "nested", "reformatted"):
            with self.subTest(defect=defect), tempfile.TemporaryDirectory() as tmp:
                directory = Path(tmp)
                self.write_workloads(directory)
                report = directory / "empty/stage.xml"
                root = ET.parse(report)
                test = root.find("assembly/collection/test")
                output = test.find("output")
                if defect == "misplaced":
                    test.remove(output)
                    root.find("assembly").append(output)
                elif defect == "duplicate":
                    output.text += output.text
                elif defect == "nested":
                    ET.SubElement(output, "nested").text = output.text
                else:
                    receipt_path = directory / "empty/stage.json"
                    receipt_path.write_text(json.dumps(json.loads(receipt_path.read_text()), sort_keys=True))
                root.write(report)
                with self.assertRaisesRegex(ValueError, "passing workload test must contain one matching XML receipt"):
                    runner["verify_workloads"](directory)

    def test_workloads_reject_changed_or_missing_input_digests(self):
        for defect in ("changed receipt", "missing receipt digest", "changed manifest bytes"):
            with self.subTest(defect=defect), tempfile.TemporaryDirectory() as tmp:
                directory = Path(tmp)
                self.write_workloads(directory)
                if defect == "changed manifest bytes":
                    manifest = directory / "existing" / "input.json"
                    manifest.write_bytes(manifest.read_bytes() + b"\n")
                else:
                    receipt_path = directory / "empty" / "stage.json"
                    receipt = json.loads(receipt_path.read_text())
                    if defect == "changed receipt":
                        receipt["inputSha256"] = "0" * 64
                    else:
                        del receipt["inputSha256"]
                    receipt_path.write_text(json.dumps(receipt))
                with self.assertRaisesRegex(ValueError, "missing or mismatched .* workload input SHA-256"):
                    runner["verify_workloads"](directory)

    def test_workloads_reject_invalid_operations_with_matching_receipts_and_digests(self):
        for operation in ("", "  ", None, 42, True):
            with self.subTest(operation=operation), tempfile.TemporaryDirectory() as tmp:
                directory = Path(tmp)
                self.write_workloads(directory, empty_operation=operation)
                with self.assertRaisesRegex(ValueError, "invalid backfill workload operation"):
                    runner["verify_workloads"](directory)

    def test_misplaced_test_is_rejected_even_when_reported_counts_match(self):
        project = runner["MEMORIES_PROJECT"]
        lane = "same-uri-refresh"
        for path in (".", "collection"):
            with self.subTest(path=path):
                assembly = self.report(lane, project)
                ET.SubElement(ET.SubElement(assembly.find(path), "unexpected"), "test", result="Fail")
                with self.assertRaisesRegex(ValueError, "outside assembly/collection"):
                    self.verify(assembly, lane, project)

    def test_success_requires_every_lane_and_each_independent_producer_control(self):
        required_lanes = {"backfill", "same-uri-refresh", "bounded-discovery", "cross-source-authority",
                          "purged-identity-reuse", "concurrent-older-admission"}
        controls = {
            "cross-source-authority": ["UnitOwnershipAuthorityIntegrationTests.UnconfiguredProducerCannotBorrowIssuedAuthorityForUnscheduledCleanup"],
            "concurrent-older-admission": [
                f"{method}(annotation: {value})" for method in (
                    "RetryWorkflowErasureIntegrationTests.ActualOlderProducerBetweenAbsentReadAndAdmissionCannotGainModernAuthority",
                    "MemoryErasureProcessRestartTests.ActualOlderProducerCompletesBetweenAbsentReadAndAdmissionAcrossReplacement",
                ) for value in ("True", "False")],
        }
        self.assertEqual(set(runner["REQUIRED_CASES"]), required_lanes)
        checks = {lane: {"passed": len(cases), "cases": sorted(cases)} for lane, cases in runner["REQUIRED_CASES"].items()}
        summary = {"status": "incomplete", "checks": checks}
        runner["complete_summary"](summary)
        self.assertEqual(summary["status"], "passed")
        for lane in required_lanes:
            with self.subTest(missing_lane=lane):
                with self.assertRaises(ValueError):
                    runner["complete_summary"]({"checks": {name: value for name, value in checks.items() if name != lane}})
        for lane, suffixes in controls.items():
            for suffix in suffixes:
                with self.subTest(suffix=suffix):
                    omitted = f"Hexalith.Memories.IntegrationTests.Ingestion.{suffix}"
                    self.assertIn(omitted, checks[lane]["cases"])
                    cases = [case for case in checks[lane]["cases"] if case != omitted]
                    shortened = checks | {lane: {"passed": len(cases), "cases": cases}}
                    with self.assertRaises(ValueError):
                        runner["complete_summary"]({"checks": shortened})

    def test_substituted_theory_argument_cannot_replace_required_variant(self):
        for lane, project, suffix in (
            ("same-uri-refresh", runner["MEMORIES_PROJECT"], "(newerFirst: False)"),
            ("purged-identity-reuse", runner["NIGHTLY_PROJECT"], "(crossSource: True)"),
            ("cross-source-authority", runner["MEMORIES_PROJECT"], '(bareMarker: "modern")'),
        ):
            with self.subTest(lane=lane):
                assembly = self.report(lane, project)
                test = next(test for test in assembly.iter("test") if test.get("name").endswith(suffix))
                self.verify(assembly, lane, project)
                test.set("name", test.get("name").removesuffix(suffix) + "(substituted: True)")
                with self.assertRaisesRegex(ValueError, "missing"):
                    self.verify(assembly, lane, project)

    def test_both_competing_producer_scenarios_and_both_admissions_are_required(self):
        lane, project = "concurrent-older-admission", runner["NIGHTLY_PROJECT"]
        for method in ("ActualOlderProducerBetweenAbsentReadAndAdmissionCannotGainModernAuthority",
                       "ActualOlderProducerCompletesBetweenAbsentReadAndAdmissionAcrossReplacement"):
            for annotation in ("True", "False"):
                with self.subTest(method=method, annotation=annotation):
                    assembly = self.report(lane, project)
                    collection = assembly.find("collection")
                    suffix = f".{method}(annotation: {annotation})"
                    collection.remove(next(test for test in collection if test.get("name").endswith(suffix)))
                    self.recount(assembly)
                    with self.assertRaisesRegex(ValueError, method):
                        self.verify(assembly, lane, project)

    def test_assembly_identity_error_and_collection_results_must_agree(self):
        lane, project = "cross-source-authority", runner["MEMORIES_PROJECT"]
        for node, field in ((".", "name"), (".", "passed"), ("collection", "total"), ("collection", "failed"),
                            ("collection/test", "result")):
            with self.subTest(node=node, field=field):
                assembly = self.report(lane, project)
                assembly.find(node).set(field, "unexpected")
                with self.assertRaises(ValueError):
                    self.verify(assembly, lane, project)
        assembly = self.report(lane, project)
        ET.SubElement(ET.SubElement(assembly, "errors"), "error")
        with self.assertRaisesRegex(ValueError, "assembly errors"):
            self.verify(assembly, lane, project)


if __name__ == "__main__":
    unittest.main()
