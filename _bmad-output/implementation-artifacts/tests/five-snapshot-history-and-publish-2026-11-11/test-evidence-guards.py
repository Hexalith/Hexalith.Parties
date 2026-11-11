#!/usr/bin/env python3
"""Focused refusal and owned-cleanup controls for this batch's evidence tools."""

import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch
import xml.etree.ElementTree as ET

BATCH = Path(__file__).resolve().parent


def load(name, filename):
    specification = importlib.util.spec_from_file_location(name, BATCH / filename)
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


GUARDS = load("snapshot_evidence_guards", "retain-evidence.py")
OWNED = load("snapshot_backfill_driver", "run-owned-backfill.py")
PUBLISHER = load("snapshot_publisher", "verify-sidecar-publication.py")
CASES = ("Owned.Control", "Owned.Other")


def result_xml(results=None):
    results = results if results is not None else dict.fromkeys(CASES, "Pass")
    root = ET.Element("assemblies")
    assembly = ET.SubElement(root, "assembly", total=str(len(results)), passed=str(sum(value == "Pass" for value in results.values())),
                             failed=str(sum(value == "Fail" for value in results.values())), skipped=str(sum(value == "Skip" for value in results.values())), time="0.01")
    collection = ET.SubElement(assembly, "collection")
    for name, result in results.items():
        owner, method = name.rsplit(".", 1)
        test = ET.SubElement(collection, "test", name=name, type=owner, method=method, result=result)
        if result == "Fail":
            ET.SubElement(test, "failure")
    return root


def xml_bytes(tree):
    return ET.tostring(tree)


class EvidenceRefusalTests(unittest.TestCase):
    def test_exact_executed_selection_and_reversal_are_accepted(self):
        self.assertEqual(GUARDS.validate_test_evidence(xml_bytes(result_xml()), CASES)["passed"], "2")
        red = result_xml({CASES[0]: "Fail", CASES[1]: "Pass"})
        self.assertEqual(GUARDS.validate_test_evidence(xml_bytes(red), CASES, {CASES[0]})["failed"], "1")

    def test_zero_discovery_skips_missing_duplicate_and_wrong_cases_are_refused(self):
        invalid = [result_xml({}), result_xml({CASES[0]: "Skip", CASES[1]: "Pass"}), result_xml({CASES[0]: "Pass"}),
                   result_xml({CASES[0]: "Pass", "Owned.Unrelated": "Pass"})]
        duplicate = result_xml()
        duplicate.find(".//collection").append(copy.deepcopy(duplicate.find(".//test")))
        invalid.append(duplicate)
        incorrect_identity = result_xml()
        incorrect_identity.find(".//test").set("method", "Unexecuted")
        invalid.append(incorrect_identity)
        for tree in invalid:
            with self.subTest(xml=xml_bytes(tree)), self.assertRaises(ValueError):
                GUARDS.validate_test_evidence(xml_bytes(tree), CASES)

    def test_counter_disagreement_and_assembly_errors_are_refused(self):
        for key, value in (("total", "0"), ("passed", "1"), ("failed", "1"), ("skipped", "1"), ("not-run", "1"), ("errors", "1")):
            tree = result_xml()
            tree.find("assembly").set(key, value)
            with self.subTest(key=key), self.assertRaises(ValueError):
                GUARDS.validate_test_evidence(xml_bytes(tree), CASES)
        with self.assertRaises(ValueError):
            GUARDS.validate_test_evidence(xml_bytes(result_xml()), [])

    def test_unexpected_or_missing_reversal_failures_are_refused(self):
        for results in (dict.fromkeys(CASES, "Pass"), dict.fromkeys(CASES, "Fail"), {CASES[0]: "Pass", CASES[1]: "Fail"}):
            with self.subTest(results=results), self.assertRaises(ValueError):
                GUARDS.validate_test_evidence(xml_bytes(result_xml(results)), CASES, {CASES[0]})

    def test_publisher_receipt_uses_actual_sidecar_environment_references(self):
        expected = {"APP_API_TOKEN": "{memories-app-api-token.value}", "DAPR_API_TOKEN": "{memories-dapr-api-token.value}"}
        self.assertEqual(PUBLISHER.token_references({"type": "dapr.v0"}), dict.fromkeys(expected))
        self.assertEqual(PUBLISHER.token_references({"env": expected}), expected)
        wrong = {**expected, "APP_API_TOKEN": "actual-wrong-binding"}
        self.assertEqual(PUBLISHER.token_references({"env": wrong}), wrong)
        receipt = json.loads((BATCH / "sdk-sidecar-emission.json").read_text())
        for row in receipt["records"]:
            row.update(pairedSecretEmissionQualified=True, blocker=None, sidecarKeys=["dapr", "env", "type"],
                       sidecarSecretReferences=PUBLISHER.token_references({"env": expected}))
        self.assertTrue(GUARDS.validate_sidecar_receipt(receipt))
        receipt["records"][0]["sidecarSecretReferences"] = PUBLISHER.token_references({"env": wrong})
        with self.assertRaises(ValueError):
            GUARDS.validate_sidecar_receipt(receipt)

    def test_observed_sidecar_refusal_and_fully_evidenced_pairing_are_accepted(self):
        receipt = json.loads((BATCH / "sdk-sidecar-emission.json").read_text())
        self.assertIs(GUARDS.validate_sidecar_receipt(receipt), False)
        for row in receipt["records"]:
            row.update(pairedSecretEmissionQualified=True, blocker=None, sidecarKeys=["dapr", "env", "type"],
                       sidecarSecretReferences=row["appSecretReferences"])
        self.assertIs(GUARDS.validate_sidecar_receipt(receipt), True)

    def test_invalid_or_duplicate_sidecar_execution_receipts_are_refused(self):
        original = json.loads((BATCH / "sdk-sidecar-emission.json").read_text())
        mutations = [lambda value: value.update(executed=0), lambda value: value.update(passed=False),
                     lambda value: value["records"].pop(),
                     lambda value: value["records"][1].update(target=value["records"][0]["target"]),
                     lambda value: value["records"][0].update(publisherExitCode=1),
                     lambda value: value["records"][0].update(publisherExitCode=False),
                     lambda value: value["records"][0].update(conformance="failed"),
                     lambda value: value["records"][0].update(appSecretReferences={}),
                     lambda value: value["records"][0].update(actualManifestSha256=""),
                     lambda value: value["records"][0].update(privateRawArtifactsRemoved=False),
                     lambda value: value["records"][0].update(pairedSecretEmissionQualified=True, blocker=None),
                     lambda value: value["records"][0].update(blocker="some-other-error"),
                     lambda value: value.update(effectivePackages=[])]
        for index, mutate in enumerate(mutations):
            value = copy.deepcopy(original)
            mutate(value)
            with self.subTest(mutation=index), self.assertRaises(ValueError):
                GUARDS.validate_sidecar_receipt(value)


class OwnedCleanupTests(unittest.TestCase):
    def test_absence_requires_a_successful_exact_missing_resource_query(self):
        identifier = "a" * 64
        with patch.object(OWNED.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, stdout="")) as query:
            self.assertTrue(OWNED.confirmed_container_absent(identifier))
            self.assertTrue(query.call_args.kwargs["check"])
            self.assertIn("id=" + identifier, query.call_args.args[0])
        with patch.object(OWNED.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, stdout=identifier + "\n")):
            self.assertFalse(OWNED.confirmed_container_absent(identifier))
        with patch.object(OWNED.subprocess, "run", side_effect=subprocess.CalledProcessError(1, "docker")):
            with self.assertRaises(subprocess.CalledProcessError):
                OWNED.confirmed_container_absent(identifier)
        with patch.object(OWNED.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, stdout="b" * 64 + "\n")):
            with self.assertRaises(ValueError):
                OWNED.confirmed_container_absent(identifier)

    def test_cleanup_attempts_every_resource_after_termination_reap_and_daemon_failures(self):
        first = Mock(pid=101)
        first.poll.return_value = None
        first.wait.side_effect = subprocess.TimeoutExpired("worker", 10)
        second = Mock(pid=102)
        second.poll.side_effect = [None, 0]
        result = {}
        with patch.object(OWNED.os, "killpg", side_effect=[PermissionError(), None]) as kill, \
             patch.object(OWNED.subprocess, "run", side_effect=[subprocess.TimeoutExpired("docker", 30), subprocess.CalledProcessError(1, "docker")]):
            OWNED.cleanup_owned(first, second, "a" * 64, result)
        self.assertEqual(kill.call_count, 2)
        first.wait.assert_called_once()
        second.wait.assert_called_once()
        self.assertFalse(result["cleanupSucceeded"])
        self.assertFalse(result["ownedContainerAbsent"])
        self.assertTrue(result["processCleanup"]["sidecar"])
        self.assertEqual({failure["operation"] for failure in result["cleanupErrors"]}, {"terminate", "reap", "remove", "confirm-absence"})

    def test_exit_and_auto_removal_races_are_confirmed_without_false_failure(self):
        process = Mock(pid=103)
        process.poll.side_effect = [None, 0]
        result = {}
        with patch.object(OWNED.os, "killpg", side_effect=ProcessLookupError()), \
             patch.object(OWNED.subprocess, "run", side_effect=[subprocess.CompletedProcess([], 1), subprocess.CompletedProcess([], 0, stdout="")]):
            OWNED.cleanup_owned(process, None, "a" * 64, result)
        self.assertTrue(result["cleanupSucceeded"])
        self.assertTrue(result["containerRemovalExitRaceConfirmedAbsent"])
        self.assertEqual(result["cleanupErrors"], [])

    def test_failed_cleanup_still_writes_receipt_and_preserves_primary_failure(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "result"
            with patch.object(sys, "argv", ["run-owned-backfill.py", "--assembly", "unused.dll", "--output", str(output)]), \
                 patch.object(OWNED, "command", side_effect=["a" * 64, RuntimeError("controlled primary failure")]), \
                 patch.object(OWNED.subprocess, "run", side_effect=[subprocess.TimeoutExpired("docker", 30), subprocess.CalledProcessError(1, "docker")]):
                with self.assertRaisesRegex(RuntimeError, "controlled primary failure"):
                    OWNED.main()
            receipt = json.loads((output / "receipt.json").read_text())
            self.assertFalse(receipt["cleanupSucceeded"])
            self.assertEqual(len(receipt["cleanupErrors"]), 2)
            self.assertFalse(receipt["ownedContainerAbsent"])

    def test_real_owned_worker_is_terminated_and_reaped_after_sibling_exit(self):
        exited = subprocess.Popen([sys.executable, "-c", "pass"], start_new_session=True)
        live = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"], start_new_session=True)
        try:
            exited.wait(timeout=5)
            result = {}
            OWNED.cleanup_owned(exited, live, None, result)
            self.assertTrue(result["cleanupSucceeded"])
            self.assertTrue(result["ownedProcessesExited"])
            self.assertIsNotNone(live.poll())
        finally:
            for process in (exited, live):
                if process.poll() is None:
                    process.kill()
                process.wait(timeout=5)


if __name__ == "__main__":
    unittest.main(verbosity=2)
