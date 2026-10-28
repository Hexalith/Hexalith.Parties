"""Negative controls for lane-specific acceptance and checkout-complete evidence."""

import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET


HERE = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location("matrix_audit", HERE / "audit-matrix.py")
AUDIT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(AUDIT)


class MatrixAuditTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.evidence = self.root / HERE.relative_to(AUDIT.ROOT)
        self.evidence.mkdir(parents=True)
        self.receipt = json.loads((HERE / "implementation-verification.json").read_text())
        dispositions = json.loads((HERE / "disposition-verification.json").read_text())
        generation = json.loads((HERE / "retained-generation-receipt.json").read_text())
        inputs = {"implementation-verification.json", "disposition-verification.json", "helper-verification.json",
                  "helper-tests.xml", "retained-generation-receipt.json"}
        for command in self.receipt["commands"]:
            inputs.add(command["log"])
            if "xml" in command:
                inputs.add(command["xml"])
        inputs.update(item["snapshot"] for item in dispositions["historical_inputs"].values())
        inputs.add(self.receipt["source_resolver_blocker"]["log"])
        for command in json.loads((HERE / "helper-verification.json").read_text()):
            inputs.add(command["log"])
        inputs.update(item["path"] for item in generation["archived_inputs"].values())
        inputs.update(name.replace("source-mapping/", "retained-source-mapping/", 1)
                      for name in generation["generated_sha256"])
        for name in inputs:
            target = self.evidence / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(HERE / name, target)
        for name in self.receipt["source_sha256"] | dispositions["input_sha256"]:
            target = self.root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            if name == "references/Hexalith.Builds/Props/Directory.Packages.props":
                # Model a checkout of the owning index, including its checkout filters.
                self.index_catalog = subprocess.check_output(
                    ["git", "cat-file", "--filters", ":Props/Directory.Packages.props"],
                    cwd=AUDIT.ROOT / "references/Hexalith.Builds")
                target.write_bytes(self.index_catalog)
            else:
                shutil.copyfile(AUDIT.ROOT / name, target)

    def write_receipt(self):
        (self.evidence / "implementation-verification.json").write_text(json.dumps(self.receipt))

    def test_checkout_shaped_audit_has_all_required_inputs_without_ignored_logs(self):
        self.assertFalse(list(self.root.rglob("*.log")))
        catalog = self.root / "references/Hexalith.Builds/Props/Directory.Packages.props"
        self.assertEqual(catalog.read_bytes(), self.index_catalog)
        self.assertEqual(AUDIT.audit(self.evidence, self.root)["source_and_receipt_hashes"], "Pass")

    def test_missing_mapped_lane_is_rejected_even_with_retained_cases(self):
        self.receipt["commands"] = [command for command in self.receipt["commands"]
                                    if command["name"] != "final-mapped-ui-tests"]
        self.write_receipt()
        with self.assertRaisesRegex(ValueError, "Missing required lane: final-mapped-ui-tests"):
            AUDIT.audit(self.evidence, self.root)

    def test_missing_theory_variant_is_rejected_after_matching_receipt_and_xml(self):
        command = next(item for item in self.receipt["commands"] if item["name"] == "final-mapped-ui-tests")
        name = AUDIT.UI + "EarlyConfirmationCannotReconcileRejectedOrCancelledCommand(cancel: True)"
        command["test_results"] = [case for case in command["test_results"] if case["name"] != name]
        document = ET.parse(self.evidence / command["xml"])
        for parent in document.iter():
            for child in list(parent):
                if child.tag == "test" and child.attrib["name"] == name:
                    parent.remove(child)
        document.write(self.evidence / command["xml"], encoding="utf-8")
        command["xml_sha256"] = hashlib.sha256((self.evidence / command["xml"]).read_bytes()).hexdigest()
        self.write_receipt()
        with self.assertRaisesRegex(ValueError, "Missing passing variant in final-mapped-ui-tests"):
            AUDIT.audit(self.evidence, self.root)

    def test_mutated_historical_catalog_snapshot_is_rejected(self):
        dispositions = json.loads((self.evidence / "disposition-verification.json").read_text())
        snapshot = next(iter(dispositions["historical_inputs"].values()))["snapshot"]
        target = self.evidence / snapshot
        target.write_bytes(target.read_bytes() + b"\n")
        with self.assertRaisesRegex(ValueError, "SHA-256 mismatch: historical-inputs/"):
            AUDIT.audit(self.evidence, self.root)

    def test_mutated_disposition_input_is_rejected_despite_pass_values(self):
        target = self.root / ".config/dotnet-tools.json"
        target.write_bytes(target.read_bytes() + b"\n")
        with self.assertRaisesRegex(ValueError, r"SHA-256 mismatch: \.config/dotnet-tools.json"):
            AUDIT.audit(self.evidence, self.root)


if __name__ == "__main__":
    unittest.main(verbosity=2)
