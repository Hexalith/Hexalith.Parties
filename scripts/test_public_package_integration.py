"""Synthetic artifact/byte controls only; no GitHub identity, Docker, native run or qualification credit."""
import copy
from datetime import datetime, timedelta, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(filename))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


public = load("public_package_controls", "verify-public-package-integration.py")
private_fixture = load("unchanged_private_controls", "test_current_owner_integration.py")


class PublicPackageEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.fixture = private_fixture.CurrentOwnerEvidenceTests("test_current_same_run_evidence_passes_without_qualification_credit")
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        f = self.fixture
        self.expected = {**f.expected, "platform": public.PLATFORM, "builds": public.BUILDS}
        self.invocation = dict(classification="baseline-only", executionContext=f.context, profiles=[public.PROFILE])
        f.write("invocation.json", self.invocation)
        f.functional.update(profile=public.PROFILE, currentOwner=None, selectorCompatibilityObserved=True)
        platform = dict(commit=public.PLATFORM, compositionCommit=public.PLATFORM, checkoutCommit=public.PLATFORM,
                        catalogCommit=public.BUILDS, releaseTag="v1.8.0")
        now = datetime.now(timezone.utc)
        f.functional["publicRuntime"] = dict(kind="running-owned-public-parties-image", profile=public.PROFILE,
            ownerRunId=f.owner["runId"], ownerAttemptId=f.owner["attemptId"], environmentId=f.owner["environmentId"],
            revision=self.expected["source"], mappingHash=f.functional["mappingHash"], executionContext=f.context,
            platform=platform, image=dict(containerId="e" * 64, runId=f.owner["runId"], imageReference=public.IMAGE,
            imageContentId="sha256:" + "f" * 64, running=True, startedAt=(now - timedelta(seconds=2)).isoformat().replace("+00:00", "Z")),
            bodies={name: dict(role="running-public-image-file", path="/app/" + name + ".dll", bytes=public.IMAGE_SIZES[name], sha256=digest)
                    for name, digest in public.IMAGE_BODIES.items()}, measuredAtUtc=now.isoformat(), qualificationCredit=False)
        f.loaded["profile"] = public.PROFILE
        for name, digest in public.PACKAGE_READERS.items():
            item = dict(identity=name + ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", sha256=digest,
                        informationalVersion="1.0.0+50904cc4ba53c9b0b2c082d5872a0d276be8f5c3")
            f.loaded["copied"].append(item)
            if name == "Hexalith.Parties.Contracts": f.loaded["loaded"].append(item.copy())
        for field in ("loaded", "copied"):
            for item in f.loaded[field]: item["scope"] = "loaded-native-test-process" if field == "loaded" else "copied-native-composition-output"
        f.write("loaded-assemblies-" + f.owner["runId"] + ".json", f.loaded)
        report = ET.Element("TestRun")
        results = ET.SubElement(report, "Results")
        for name in sorted(public.CASES): ET.SubElement(results, "UnitTestResult", testName=name, outcome="Passed")
        ET.SubElement(ET.SubElement(report, "ResultSummary"), "Counters", total="28", executed="28", passed="28", failed="0")
        f.native = ET.tostring(report)
        f.seal()

    def check(self):
        return public.validate(self.fixture.root, **self.expected)

    def test_separate_public_roles_and_all_original28_names_pass_as_a_unit_fixture_only(self):
        result = self.check()
        self.assertEqual(result["nativeCases"], 28)
        self.assertEqual(result["profile"], "platform-package")
        self.assertFalse(result["qualificationCredit"])

    def test_historical_tuple_cannot_qualify_selected_sources(self):
        with self.assertRaises(ValueError):
            public.validate(self.fixture.root, **{**self.expected,
                "platform": "0f4e27f1f78310bd731c10c19dc51bb1e0345d44",
                "builds": "3c8096180ecf2aa539ac454815efbfceb8a92822"})

    def test_private_current_owner_proof_cannot_be_promoted(self):
        self.fixture.functional["currentOwner"] = {"profile": "platform-current-package"}
        self.fixture.seal()
        with self.assertRaisesRegex(ValueError, "private current-owner"): self.check()

    def test_missing_or_false_selector_compatibility_observation_refuses(self):
        for value in (None, False, "true", 1):
            with self.subTest(value=value):
                if value is None:
                    self.fixture.functional.pop("selectorCompatibilityObserved", None)
                else:
                    self.fixture.functional["selectorCompatibilityObserved"] = value
                self.fixture.seal()
                with self.assertRaisesRegex(ValueError, "selector compatibility"): self.check()

    def test_role_substitution_and_actual_body_tampering_refuse_after_resealing(self):
        f = self.fixture
        baseline = copy.deepcopy(f.functional)
        for name in public.IMAGE_BODIES:
            with self.subTest(name=name):
                f.functional = copy.deepcopy(baseline)
                f.functional["publicRuntime"]["bodies"][name]["sha256"] = public.PACKAGE_READERS.get(name, "a" * 64)
                f.seal()
                with self.assertRaisesRegex(ValueError, "running image body"): self.check()
        f.functional = baseline
        f.seal()
        for field in ("loaded", "copied"):
            for item in f.loaded[field]:
                name = item["identity"].split(",", 1)[0]
                if name != "Hexalith.Parties.Contracts": continue
                with self.subTest(field=field):
                    saved = item["sha256"]
                    item["sha256"] = public.IMAGE_BODIES[name]
                    f.write("loaded-assemblies-" + f.owner["runId"] + ".json", f.loaded)
                    with self.assertRaisesRegex(ValueError, "public NuGet reader"): self.check()
                    item["sha256"] = saved
        f.write("loaded-assemblies-" + f.owner["runId"] + ".json", f.loaded)

    def test_foreign_runtime_source_owner_job_attempt_mapping_or_public_source_refuse(self):
        f = self.fixture
        baseline = copy.deepcopy(f.functional)
        changes = [(name, "x") for name in ("profile", "ownerRunId", "ownerAttemptId", "environmentId", "revision", "mappingHash")]
        changes += [("executionContext", {**f.context, "jobId": "foreign"}), ("qualificationCredit", True), ("kind", "copied-static-image"), ("unexpected", True)]
        for field, value in changes:
            with self.subTest(field=field):
                f.functional = copy.deepcopy(baseline)
                f.functional["publicRuntime"][field] = value
                f.seal()
                with self.assertRaises(ValueError): self.check()
        for field in ("commit", "compositionCommit", "checkoutCommit", "catalogCommit", "releaseTag"):
            with self.subTest(platformField=field):
                f.functional = copy.deepcopy(baseline)
                f.functional["publicRuntime"]["platform"][field] = "foreign"
                f.seal()
                with self.assertRaises(ValueError): self.check()

    def test_unknown_unpinned_stopped_or_foreign_image_and_body_shapes_refuse(self):
        f = self.fixture
        baseline = copy.deepcopy(f.functional)
        changes = [("containerId", "--option"), ("runId", "f" * 32), ("imageReference", "registry.hexalith.com/parties:3.1.0"),
                   ("imageContentId", "latest"), ("running", False), ("startedAt", "unknown"), ("unexpected", True)]
        for field, value in changes:
            with self.subTest(field=field):
                f.functional = copy.deepcopy(baseline)
                f.functional["publicRuntime"]["image"][field] = value
                f.seal()
                with self.assertRaises(ValueError): self.check()
        for mutation in ("missing-body", "unknown-body", "copied-role", "wrong-size", "foreign-path"):
            with self.subTest(body=mutation):
                f.functional = copy.deepcopy(baseline)
                bodies = f.functional["publicRuntime"]["bodies"]
                name = "Hexalith.Parties.Contracts"
                if mutation == "missing-body": del bodies[name]
                if mutation == "unknown-body": bodies["foreign"] = bodies[name].copy()
                if mutation == "copied-role": bodies[name]["role"] = "copied-native-composition-output"
                if mutation == "wrong-size": bodies[name]["bytes"] += 1
                if mutation == "foreign-path": bodies[name]["path"] = "/foreign/" + name + ".dll"
                f.seal()
                with self.assertRaises(ValueError): self.check()

    def test_missing_renamed_duplicate_or_skipped_original_native_cases_refuse(self):
        f = self.fixture
        original = f.native
        tree = ET.fromstring(original)
        names = [element.get("testName") for element in tree.iter("UnitTestResult")]
        for mutation in ("missing", "renamed", "duplicate", "skip"):
            with self.subTest(mutation=mutation):
                tree = ET.fromstring(original)
                results = tree.find("Results")
                if mutation == "missing": results.remove(results[0])
                if mutation == "renamed": results[0].set("testName", "renamed")
                if mutation == "duplicate": results[0].set("testName", names[1])
                if mutation == "skip": results[0].set("outcome", "NotExecuted")
                f.native = ET.tostring(tree)
                f.seal()
                with self.assertRaises(ValueError): self.check()

    def test_persistence_projection_anonymous_case_and_reference_owner_assertions_remain_required(self):
        f = self.fixture
        baseline = copy.deepcopy(f.functional)
        for mutation in ("projection", "event", "timestamp", "sequence", "tenants", "memories"):
            with self.subTest(mutation=mutation):
                f.functional = copy.deepcopy(baseline)
                if mutation == "projection": f.functional["projection"]["id"] = "other"
                if mutation == "event": f.functional["events"][0]["identity"]["metadataKey"] = "other"
                if mutation == "timestamp": f.functional["projection"]["createdAt"] = "2026-11-01T00:00:00Z"
                if mutation == "sequence": f.functional["metadataSequence"] = 2
                if mutation == "tenants": f.functional["tenantsNativeQueryObserved"] = False
                if mutation == "memories": f.functional["memoriesRegistryObserved"] = False
                f.seal()
                with self.assertRaises(ValueError): self.check()

    def test_unknown_or_leftover_cleanup_and_changed_first_outcome_refuse(self):
        f = self.fixture
        phase = json.loads((f.root / "cleanup.json").read_text())
        for mutation in ("unknown", "leftover"):
            changed = copy.deepcopy(phase)
            if mutation == "unknown": changed["remainingResources"]["unverified"] = True
            if mutation == "leftover": changed["remainingResources"]["containerIds"] = ["e" * 64]
            f.write("cleanup.json", changed)
            with self.subTest(mutation=mutation), self.assertRaises(ValueError): self.check()
        f.write("cleanup.json", phase)
        f.result["terminal"]["outcome"] = "cancelled"
        f.seal()
        with self.assertRaises(ValueError): self.check()

    def test_per_attempt_helper_layout_is_accepted_only_when_complete_and_verified(self):
        f = self.fixture
        execution = json.loads((f.root / "execution.json").read_text())
        cleanup = json.loads((f.root / "cleanup.json").read_text())
        (f.root / "cleanup.json").unlink()
        observed = dict(schema="hexalith.module.ci-integration-execution.v1", executionContext=f.context, processExitCode=0,
                        launched=True, cancelled=False, timedOut=False, signals=[], outputParsed=True, result=execution,
                        stderr=dict(bytes=0, sha256="0" * 64, excerpt="", truncated=False, redacted=True))
        attempt = dict(schema="hexalith.module.ci-integration-cleanup.v1", status="completed", runId=f.owner["runId"],
                       executionContext=f.context, jobStatus="", processExitCode=0, outputParsed=True, result=cleanup, verified=True)
        f.write("invocation.json", dict(self.invocation, timeoutSeconds=2100))
        f.write("execution.json", observed)
        f.write("cleanup-1.json", attempt)
        f.write("cleanup-2.json", dict(attempt, jobStatus="success"))
        self.assertEqual(self.check()["runId"], f.owner["runId"])
        refusals = (
            ("cleanup-2.json", dict(attempt, verified=False)),
            ("cleanup-2.json", dict(attempt, status="timed-out")),
            ("cleanup-2.json", dict(attempt, result=dict(cleanup, remainingResources=dict(cleanup["remainingResources"], unverified=True)))),
            ("execution.json", dict(observed, cancelled=True)),
            ("execution.json", dict(observed, outputParsed=False, result=None)),
        )
        for number, (name, changed) in enumerate(refusals):
            original = json.loads((f.root / name).read_text())
            f.write(name, changed)
            with self.subTest(refusal=number, name=name), self.assertRaises(ValueError):
                self.check()
            f.write(name, original)
        f.write("cleanup.json", cleanup)
        with self.subTest(layout="mixed"), self.assertRaises(ValueError):
            self.check()
        (f.root / "cleanup.json").unlink()
        (f.root / "cleanup-1.json").unlink()
        with self.subTest(layout="gap"), self.assertRaises(ValueError):
            self.check()

    def new_helper_layout(self):
        """Write the exact record shapes the per-attempt Builds helper emits for a verified run."""
        f = self.fixture
        execution = json.loads((f.root / "execution.json").read_text())
        cleanup = json.loads((f.root / "cleanup.json").read_text())
        (f.root / "cleanup.json").unlink()
        stderr = dict(bytes=0, sha256=hashlib.sha256(b"").hexdigest(), excerpt="", truncated=False, redacted=True)
        invocation = dict(executionContext=f.context, profiles=[public.PROFILE], classification="baseline-only", timeoutSeconds=2045)
        observed = dict(schema="hexalith.module.ci-integration-execution.v1", executionContext=f.context, processExitCode=0,
                        launched=True, cancelled=False, timedOut=False, signals=[], outputParsed=True, result=execution, stderr=stderr)
        attempt = dict(schema="hexalith.module.ci-integration-cleanup.v1", status="completed", runId=f.owner["runId"],
                       executionContext=f.context, jobStatus="", processExitCode=0, outputParsed=True, verified=True,
                       stderr=stderr, result=cleanup)
        f.write("invocation.json", invocation)
        f.write("execution.json", observed)
        f.write("cleanup-1.json", attempt)
        return invocation, observed, attempt, execution, cleanup

    def test_new_helper_timeout_is_the_only_accepted_invocation_addition(self):
        f = self.fixture
        invocation, *_ = self.new_helper_layout()
        self.assertEqual(self.check()["runId"], f.owner["runId"])
        for name, changed in {
            "bool-timeout": dict(invocation, timeoutSeconds=True),
            "float-timeout": dict(invocation, timeoutSeconds=2045.0),
            "string-timeout": dict(invocation, timeoutSeconds="2045"),
            "below-range": dict(invocation, timeoutSeconds=59),
            "above-range": dict(invocation, timeoutSeconds=86401),
            "other-new-field": dict(invocation, qualificationOwners=["eventstore"]),
            "foreign-profile": dict(invocation, profiles=["lifecycle-package"]),
        }.items():
            f.write("invocation.json", changed)
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.check()
        f.write("invocation.json", dict(invocation, timeoutSeconds=60))
        self.assertEqual(self.check()["runId"], f.owner["runId"])

    def test_mixed_layouts_foreign_records_and_non_integer_exit_codes_refuse(self):
        f = self.fixture
        invocation, observed, attempt, execution, cleanup = self.new_helper_layout()
        foreign = dict(f.context, workflowRunId="999")
        refusals = {
            "timeout-with-raw-execution": [("execution.json", execution)],
            "timeout-with-whole-raw-layout": [("execution.json", execution), ("cleanup.json", cleanup), ("cleanup-1.json", None)],
            "raw-invocation-with-new-records": [("invocation.json", {k: v for k, v in invocation.items() if k != "timeoutSeconds"})],
            "wrapped-execution-with-raw-cleanup": [("cleanup.json", cleanup), ("cleanup-1.json", None)],
            "raw-and-per-attempt-cleanup": [("cleanup.json", cleanup)],
            "foreign-wrapper-context": [("execution.json", dict(observed, executionContext=foreign))],
            "missing-wrapper-context": [("execution.json", {k: v for k, v in observed.items() if k != "executionContext"})],
            "foreign-cleanup-context": [("cleanup-1.json", dict(attempt, status="completed", verified=False, executionContext=foreign)),
                                        ("cleanup-2.json", attempt)],
            "foreign-cleanup-owner": [("cleanup-1.json", dict(attempt, runId="f" * 32))],
            "earlier-record-foreign-owner": [("cleanup-1.json", dict(attempt, runId="f" * 32, verified=False)), ("cleanup-2.json", attempt)],
            "false-exit-code": [("execution.json", dict(observed, processExitCode=False))],
            "float-exit-code": [("execution.json", dict(observed, processExitCode=0.0))],
            "final-cleanup-false-exit": [("cleanup-1.json", dict(attempt, processExitCode=False))],
        }
        for name, writes in refusals.items():
            originals = {file: (f.root / file).read_text() if (f.root / file).exists() else None for file, _ in writes}
            for file, value in writes:
                if value is None:
                    (f.root / file).unlink()
                else:
                    f.write(file, value)
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.check()
            for file, body in originals.items():
                if body is None:
                    (f.root / file).unlink(missing_ok=True)
                else:
                    (f.root / file).write_text(body)
        self.assertEqual(self.check()["runId"], f.owner["runId"])
        f.write("cleanup-1.json", dict(attempt, status="completed", verified=False, processExitCode=3))
        f.write("cleanup-2.json", dict(attempt, jobStatus="success"))
        self.assertEqual(self.check()["runId"], f.owner["runId"])

    def test_hash_tampering_and_unreferenced_substitute_refuse(self):
        f = self.fixture
        selected = next(item for item in f.result["diagnostics"] if item["id"] == "functional-observations")
        path = f.root / selected["reference"]
        path.write_bytes(path.read_bytes() + b" ")
        with self.assertRaisesRegex(ValueError, "changed diagnostic"): self.check()
        f.seal()
        f.write("unreferenced-runtime.json", f.functional["publicRuntime"])
        with self.assertRaisesRegex(ValueError, "unreferenced"): self.check()

    def test_noninteger_byte_counts_and_relabelled_loaded_or_copied_scopes_refuse(self):
        f = self.fixture
        body = f.functional["publicRuntime"]["bodies"]["Hexalith.Parties.Contracts"]
        body["bytes"] = float(body["bytes"])
        f.seal()
        with self.assertRaisesRegex(ValueError, "integer"): self.check()
        body["bytes"] = int(body["bytes"])
        f.seal()
        for field in ("loaded", "copied"):
            item = f.loaded[field][0]
            saved = item["scope"]
            item["scope"] = "copied-static-source"
            f.write("loaded-assemblies-" + f.owner["runId"] + ".json", f.loaded)
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "role differs"): self.check()
            item["scope"] = saved
            f.write("loaded-assemblies-" + f.owner["runId"] + ".json", f.loaded)

    def test_foreign_argument_tuple_and_private_invocation_are_not_an_adoption_path(self):
        for field in ("platform", "builds"):
            with self.subTest(field=field), self.assertRaises(ValueError):
                public.validate(self.fixture.root, **{**self.expected, field: "f" * 40})
        self.fixture.write("invocation.json", {**self.invocation, "profiles": ["platform-current-package"]})
        with self.assertRaises(ValueError): self.check()


if __name__ == "__main__":
    unittest.main()
