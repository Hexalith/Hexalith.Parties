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
        methods = runner["BACKFILL_METHODS"] if lane == "backfill" else runner["MEMORIES_METHODS"][lane]
        root = ET.Element("assemblies")
        root.append(assembly)
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "results.xml"
            ET.ElementTree(root).write(report)
            return runner["check_report"](report, methods, project)

    def write_workloads(self, directory, *, empty_operation="operation-empty"):
        snapshots_by_phase = {
            "seed": ("survivors.json", "canonical.json"),
            "interrupt": ("survivors.json", "canonical.json", "generation.json"),
            "rebuild": ("survivors.json", "canonical.json", "generation.json"),
            "stage": ("survivors.json",),
            "commit": ("survivors.json",),
            "verify": ("survivors.json", "generation.json"),
        }
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
            snapshots = {}
            for name in ("survivors.json", "canonical.json", "generation.json"):
                snapshot = {"tenant": tenant, "snapshot": name}
                if name == "generation.json":
                    snapshot["interruptedProcess"] = 2
                data = json.dumps(snapshot).encode()
                (target / name).write_bytes(data)
                snapshots[name] = hashlib.sha256(data).hexdigest()
            seed_index_sha = hashlib.sha256(f"{tenant}:seed-index".encode()).hexdigest()
            commit_index_sha = hashlib.sha256(f"{tenant}:commit-index".encode()).hexdigest()
            if existing:
                preexisting = {"present": True, "sha256": seed_index_sha, "etag": f"etag-seed-{tenant}"}
                staged = {"present": True, "sha256": seed_index_sha, "etag": None}
            else:
                preexisting = {"present": False, "sha256": None, "etag": None}
                staged = {"present": False, "sha256": None, "etag": None}
            committed = {"present": True, "sha256": commit_index_sha, "etag": f"etag-commit-{tenant}"}
            index_by_phase = {
                "seed": preexisting,
                "interrupt": preexisting,
                "rebuild": preexisting,
                "stage": staged,
                "commit": committed,
                "verify": committed,
            }
            for pid, (phase, observations) in enumerate(snapshots_by_phase.items(), start=1):
                receipt = json.dumps({
                    "tenant": tenant, "operation": operation, "phase": phase, "processId": pid, "inputSha256": digest,
                    "snapshotSha256": {name: snapshots[name] for name in observations},
                    "index": index_by_phase[phase],
                }).encode("utf-8")
                encoded = base64.b64encode(receipt).decode("ascii")
                output.text = f"Synthetic diagnostic\n{runner['WORKLOAD_RECEIPT_PREFIX']}{encoded}\n"
                ET.ElementTree(root).write(target / f"{phase}.xml")
                (target / f"{phase}.json").write_bytes(receipt)
            outcomes[tenant] = {"tenant": tenant, "inputSha256": digest, "phases": list(snapshots_by_phase),
                                "snapshotSha256": snapshots, "index": index_by_phase}
        return outcomes

    def rewrite_workload_receipt(self, target, phase, content):
        (target / f"{phase}.json").write_bytes(content)
        report = target / f"{phase}.xml"
        root = ET.parse(report)
        encoded = base64.b64encode(content).decode("ascii")
        root.find("assembly/collection/test/output").text = runner["WORKLOAD_RECEIPT_PREFIX"] + encoded
        root.write(report)

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

    def test_workloads_reject_missing_or_changed_snapshots_even_with_matching_receipt_layers(self):
        for filename in ("survivors.json", "canonical.json", "generation.json"):
            for defect in ("missing", "changed"):
                with self.subTest(filename=filename, defect=defect), tempfile.TemporaryDirectory() as tmp:
                    directory = Path(tmp)
                    self.write_workloads(directory)
                    target = directory / "existing" / filename
                    if defect == "missing":
                        target.unlink()
                        message = "missing retained workload snapshot"
                    else:
                        target.write_bytes((directory / "empty" / filename).read_bytes())
                        message = "retained workload snapshot SHA-256 mismatch"
                    with self.assertRaisesRegex(ValueError, message):
                        runner["verify_workloads"](directory)

    def test_workloads_reject_inconsistent_snapshot_observations_even_with_matching_receipt_layers(self):
        for phase, name in (("stage", "survivors.json"), ("interrupt", "canonical.json"), ("verify", "generation.json")):
            for defect in ("missing", "different", "duplicate"):
                with self.subTest(phase=phase, name=name, defect=defect), tempfile.TemporaryDirectory() as tmp:
                    directory = Path(tmp)
                    self.write_workloads(directory)
                    target = directory / "existing"
                    receipt = json.loads((target / f"{phase}.json").read_text())
                    if defect == "missing":
                        del receipt["snapshotSha256"][name]
                        message = "missing or unexpected .* workload snapshot observations"
                    elif defect == "different":
                        receipt["snapshotSha256"][name] = "0" * 64
                        message = "inconsistent workload observations"
                    else:
                        message = "Duplicate workload receipt property"
                    content = json.dumps(receipt).encode("utf-8")
                    if defect == "duplicate":
                        key = json.dumps(name).encode("utf-8")
                        content = content.replace(key + b":", key + b': "duplicate", ' + key + b":", 1)
                    self.rewrite_workload_receipt(target, phase, content)
                    with self.assertRaisesRegex(ValueError, message):
                        runner["verify_workloads"](directory)

    def test_workloads_reject_unrelated_interruption_process_with_consistent_hashes_and_receipts(self):
        with tempfile.TemporaryDirectory() as tmp:
            directory = Path(tmp)
            target = directory / "existing"
            self.write_workloads(directory)
            generation = target / "generation.json"
            snapshot = json.loads(generation.read_bytes())
            snapshot["interruptedProcess"] = 999
            content = json.dumps(snapshot).encode()
            generation.write_bytes(content)
            digest = hashlib.sha256(content).hexdigest()
            for phase in ("interrupt", "rebuild", "verify"):
                receipt = json.loads((target / f"{phase}.json").read_bytes())
                receipt["snapshotSha256"]["generation.json"] = digest
                self.rewrite_workload_receipt(target, phase, json.dumps(receipt).encode())
            with self.assertRaisesRegex(ValueError, "generation snapshot process differs from interruption receipt"):
                runner["verify_workloads"](directory)

    def test_workloads_reject_missing_or_malformed_index_receipts(self):
        cases = (
            ("missing", "empty", "stage", "missing or malformed stage workload index receipt"),
            ("missing-field", "empty", "commit", "missing or malformed commit workload index receipt"),
            ("extra-field", "existing", "commit", "missing or malformed commit workload index receipt"),
            ("duplicate-field", "existing", "commit", "Duplicate workload receipt property"),
            ("non-bool-present", "empty", "commit", "invalid commit workload index presence"),
            ("uppercase-sha256", "empty", "commit", "missing or invalid commit workload index SHA-256"),
            ("short-sha256", "existing", "commit", "missing or invalid commit workload index SHA-256"),
            ("commit-null-etag", "empty", "commit", "missing or invalid commit workload index ETag"),
            ("blank-etag", "empty", "commit", "missing or invalid commit workload index ETag"),
            ("untrimmed-etag", "existing", "commit", "missing or invalid commit workload index ETag"),
        )
        for defect, target_name, phase, message in cases:
            with self.subTest(defect=defect), tempfile.TemporaryDirectory() as tmp:
                directory = Path(tmp)
                self.write_workloads(directory)
                target = directory / target_name
                receipt = json.loads((target / f"{phase}.json").read_bytes())
                if defect == "missing":
                    del receipt["index"]
                elif defect == "missing-field":
                    del receipt["index"]["etag"]
                elif defect == "extra-field":
                    receipt["index"]["extra"] = True
                elif defect == "non-bool-present":
                    receipt["index"]["present"] = 1
                elif defect == "uppercase-sha256":
                    receipt["index"]["sha256"] = receipt["index"]["sha256"].upper()
                elif defect == "short-sha256":
                    receipt["index"]["sha256"] = "abcd"
                elif defect == "commit-null-etag":
                    receipt["index"]["etag"] = None
                elif defect == "blank-etag":
                    receipt["index"]["etag"] = "   "
                elif defect == "untrimmed-etag":
                    receipt["index"]["etag"] = " etag-commit "
                content = json.dumps(receipt).encode("utf-8")
                if defect == "duplicate-field":
                    content = content.replace(b'"etag":', b'"etag": "dup", "etag":', 1)
                self.rewrite_workload_receipt(target, phase, content)
                with self.assertRaisesRegex(ValueError, message):
                    runner["verify_workloads"](directory)

    def test_workloads_reject_drifted_leaked_unchanged_or_mismatched_index_receipts(self):
        other_sha = hashlib.sha256(b"other-index-bytes").hexdigest()
        cases = (
            ("empty-seed-present", "empty", "seed",
             lambda r, _o: r["index"].update(present=True, sha256=other_sha, etag="etag-1"),
             "unexpected seed workload index presence"),
            ("empty-rebuild-leaked-hash", "empty", "rebuild",
             lambda r, _o: r["index"].update(sha256=other_sha),
             "unexpected rebuild workload index digest or ETag while absent"),
            ("empty-stage-leaked-etag", "empty", "stage",
             lambda r, _o: r["index"].update(etag="etag-envelope"),
             "unexpected stage workload index digest or ETag while absent"),
            ("empty-stage-leaked-candidate", "empty", "stage",
             lambda r, _o: r["index"].update(present=True, sha256=other_sha, etag=None),
             "unexpected stage workload index presence"),
            ("empty-commit-missing", "empty", "commit",
             lambda r, _o: r["index"].update(present=False, sha256=None, etag=None),
             "unexpected commit workload index presence"),
            ("empty-verify-mismatched-hash", "empty", "verify",
             lambda r, _o: r["index"].update(sha256=other_sha),
             "verify workload index mismatched commit index"),
            ("empty-verify-mismatched-etag", "empty", "verify",
             lambda r, _o: r["index"].update(etag="etag-other"),
             "verify workload index mismatched commit index"),
            ("existing-seed-missing", "existing", "seed",
             lambda r, _o: r["index"].update(present=False, sha256=None, etag=None),
             "unexpected seed workload index presence"),
            ("existing-interrupt-drifted-hash", "existing", "interrupt",
             lambda r, _o: r["index"].update(sha256=other_sha),
             "interrupt workload index drifted from seed index"),
            ("existing-rebuild-drifted-etag", "existing", "rebuild",
             lambda r, _o: r["index"].update(etag="etag-drifted"),
             "rebuild workload index drifted from seed index"),
            ("existing-stage-leaked-seed-etag", "existing", "stage",
             lambda r, o: r["index"].update(etag=o["existing"]["index"]["seed"]["etag"]),
             "stage workload index receipt must not expose an ETag"),
            ("existing-stage-leaked-envelope-etag", "existing", "stage",
             lambda r, _o: r["index"].update(etag="etag-envelope"),
             "stage workload index receipt must not expose an ETag"),
            ("existing-stage-drifted-hash", "existing", "stage",
             lambda r, o: r["index"].update(sha256=o["existing"]["index"]["commit"]["sha256"]),
             "stage workload index SHA-256 drifted from preexisting index"),
            ("existing-commit-unchanged-hash", "existing", "commit",
             lambda r, o: r["index"].update(sha256=o["existing"]["index"]["seed"]["sha256"]),
             "commit workload index did not change preexisting index bytes and ETag"),
            ("existing-commit-unchanged-etag", "existing", "commit",
             lambda r, o: r["index"].update(etag=o["existing"]["index"]["seed"]["etag"]),
             "commit workload index did not change preexisting index bytes and ETag"),
            ("existing-verify-mismatched-hash", "existing", "verify",
             lambda r, _o: r["index"].update(sha256=other_sha),
             "verify workload index mismatched commit index"),
            ("existing-verify-mismatched-etag", "existing", "verify",
             lambda r, _o: r["index"].update(etag="etag-other"),
             "verify workload index mismatched commit index"),
        )
        for defect, target_name, phase, mutate, message in cases:
            with self.subTest(defect=defect), tempfile.TemporaryDirectory() as tmp:
                directory = Path(tmp)
                outcomes = self.write_workloads(directory)
                target = directory / target_name
                receipt = json.loads((target / f"{phase}.json").read_bytes())
                mutate(receipt, outcomes)
                self.rewrite_workload_receipt(target, phase, json.dumps(receipt).encode("utf-8"))
                with self.assertRaisesRegex(ValueError, message):
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

    def test_backfill_requires_both_index_competitors_after_staging(self):
        method = f"{runner['BACKFILL_RUNTIME']}.ActualProviderIndexCompetitorAfterStagingSurvivesCommit"
        project = runner["BACKFILL_PROJECT"]
        for value in ("False", "True"):
            with self.subTest(existing=value):
                required_case = f"{method}(existing: {value})"
                self.assertIn(required_case, runner["REQUIRED_CASES"]["backfill"])
                assembly = self.report_for_methods(runner["BACKFILL_METHODS"], project)
                collection = assembly.find("collection")
                collection.remove(next(test for test in collection if test.get("name") == required_case))
                self.recount(assembly)
                with self.assertRaisesRegex(ValueError, "missing|incomplete|expected"):
                    self.verify(assembly, "backfill", project)

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

    def test_backfill_requires_every_workload_report_integrity_case(self):
        project = runner["BACKFILL_PROJECT"]
        method = ("Hexalith.Parties.Tests.Projections.PartyLegacyBackfillProcessRuntimeTests."
                  "WorkloadReportRefusesSubstitutedOrMalformedEvidence")
        for defect in ("phase", "target", "nested output", "identity", "counts"):
            with self.subTest(defect=defect):
                assembly = self.report_for_methods(runner["BACKFILL_METHODS"], project)
                complete = self.verify(assembly, "backfill", project)
                collection = assembly.find("collection")
                case = f'{method}(defect: "{defect}")'
                self.assertIn(case, complete["cases"])
                collection.remove(next(test for test in collection if test.get("name") == case))
                self.recount(assembly)
                with self.assertRaisesRegex(ValueError, "missing"):
                    self.verify(assembly, "backfill", project)

                checks = {lane: {"passed": len(cases), "cases": sorted(cases)}
                          for lane, cases in runner["REQUIRED_CASES"].items()}
                remaining = [name for name in complete["cases"] if name != case]
                checks["backfill"] = {"passed": len(remaining), "cases": remaining}
                with self.assertRaisesRegex(ValueError, "backfill: incomplete required case evidence"):
                    runner["complete_summary"]({"checks": checks})

    def test_backfill_requires_every_index_receipt_integrity_case(self):
        project = runner["BACKFILL_PROJECT"]
        method = ("Hexalith.Parties.Tests.Projections.PartyLegacyBackfillProcessRuntimeTests."
                  "IndexReceiptRefusesMissingDriftedLeakedUnchangedOrMismatchedState")
        cases = (
            ("False", "missing-object"),
            ("False", "duplicate-object"),
            ("False", "missing-field"),
            ("False", "extra-field"),
            ("False", "duplicate-field"),
            ("False", "absent-leaked-present"),
            ("False", "absent-leaked-etag"),
            ("False", "stage-leaked-candidate"),
            ("False", "commit-missing"),
            ("False", "commit-null-etag"),
            ("False", "invalid-sha256"),
            ("False", "blank-etag"),
            ("False", "untrimmed-etag"),
            ("False", "verify-mismatched-hash"),
            ("True", "existing-missing"),
            ("True", "interrupt-drifted-etag"),
            ("True", "rebuild-drifted-hash"),
            ("True", "stage-leaked-etag"),
            ("True", "stage-drifted-hash"),
            ("True", "commit-unchanged-hash"),
            ("True", "commit-unchanged-etag"),
            ("True", "verify-mismatched-etag"),
        )
        for existing, defect in cases:
            with self.subTest(existing=existing, defect=defect):
                assembly = self.report_for_methods(runner["BACKFILL_METHODS"], project)
                complete = self.verify(assembly, "backfill", project)
                collection = assembly.find("collection")
                case = f'{method}(existing: {existing}, defect: "{defect}")'
                self.assertIn(case, complete["cases"])
                collection.remove(next(test for test in collection if test.get("name") == case))
                self.recount(assembly)
                with self.assertRaisesRegex(ValueError, "missing"):
                    self.verify(assembly, "backfill", project)

                checks = {lane: {"passed": len(cases_for_lane), "cases": sorted(cases_for_lane)}
                          for lane, cases_for_lane in runner["REQUIRED_CASES"].items()}
                remaining = [name for name in complete["cases"] if name != case]
                checks["backfill"] = {"passed": len(remaining), "cases": remaining}
                with self.assertRaisesRegex(ValueError, "backfill: incomplete required case evidence"):
                    runner["complete_summary"]({"checks": checks})

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
