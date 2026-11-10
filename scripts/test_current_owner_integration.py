"""Boundary fixtures for the mandatory current-workflow artifact gate."""
import copy
from datetime import datetime, timedelta
import gzip
import hashlib
import importlib.util
import json
import os
import subprocess
import time
from unittest import mock
from pathlib import Path
import tempfile
import unittest
import uuid
import xml.etree.ElementTree as ET

SPEC = importlib.util.spec_from_file_location("current_owner", Path(__file__).with_name("verify-current-owner-integration.py"))
verify = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(verify)

# Literal released hexalith.test-result.v1 required keys (Builds 5ec17790, schema sha256 9773678f...7e39),
# written independently of the reader's constants so a dropped constant cannot hide its own refusal.
RELEASED_RESULT_REQUIRED = ("schema", "policyId", "bindings", "diagnostics", "terminal", "termination", "retention", "cleanup")
RELEASED_BINDINGS_REQUIRED = ("run", "tool", "candidate", "decision", "reuseLineage", "environment", "artifacts", "suite",
                              "attachments", "deadlines")
RELEASED_RESULT_SCHEMA_SHA256 = "9773678febac38025fae837154341d9036ce8aa94e66b0d30c7e15ee668d7e39"
BUILDS_RESULT_SCHEMA = Path(__file__).resolve().parents[1] / "references/Hexalith.Builds/schemas/hexalith.test-result.v1.json"


def released_result_schema():
    """Return the released schema bytes only when the Builds checkout provides them; CI runs without submodules."""
    try:
        return BUILDS_RESULT_SCHEMA.read_bytes()
    except (FileNotFoundError, NotADirectoryError):
        return None


def native_report(names):
    """Use the real MTP TRX ownership/timing shape with deterministic synthetic IDs."""
    report = ET.Element("TestRun", id=str(uuid.UUID(int=1000)), name="native")
    ET.SubElement(report, "Times", creation="2026-11-05T10:00:01Z", queuing="2026-11-05T10:00:01Z",
                  start="2026-11-05T10:00:01Z", finish="2026-11-05T10:00:08Z")
    results = ET.SubElement(report, "Results")
    definitions = ET.SubElement(report, "TestDefinitions")
    for index, name in enumerate(sorted(names), 1):
        identity = str(uuid.UUID(int=index))
        ET.SubElement(results, "UnitTestResult", testName=name, outcome="Passed", testId=identity, executionId=identity,
                      startTime="2026-11-05T10:00:02Z", endTime="2026-11-05T10:00:07Z")
        definition = ET.SubElement(definitions, "UnitTest", name=name, id=identity)
        ET.SubElement(definition, "Execution", id=identity)
    ET.SubElement(ET.SubElement(report, "ResultSummary"), "Counters", total=str(len(names)),
                  executed=str(len(names)), passed=str(len(names)), failed="0")
    return ET.tostring(report)


def check_native_refusals(test, fixture, check):
    """Reseal independently invalid report timing and execution references through each full reader."""
    original = fixture.native
    for mutation in ("old-times", "missing-times", "duplicate-times", "missing-run-id", "missing-execution-id",
                     "foreign-execution-id", "duplicate-execution-id", "foreign-test-id", "foreign-definition-name",
                     "missing-definition", "outside-case", "reversed-case", "naive-times", "future-times", "outside-persistence"):
        with test.subTest(mutation=mutation):
            tree = ET.fromstring(original)
            times = tree.find("Times")
            results = tree.find("Results")
            definitions = tree.find("TestDefinitions")
            if mutation in ("old-times", "future-times"):
                for node in tree.iter():
                    for key, value in list(node.attrib.items()):
                        if key in ("creation", "queuing", "start", "finish", "startTime", "endTime"):
                            node.set(key, value.replace("2026", "1900" if mutation == "old-times" else "2999"))
            if mutation == "missing-times": tree.remove(times)
            if mutation == "duplicate-times": tree.append(copy.deepcopy(times))
            if mutation == "missing-run-id": del tree.attrib["id"]
            if mutation == "missing-execution-id": del results[0].attrib["executionId"]
            if mutation == "foreign-execution-id": results[0].set("executionId", str(uuid.UUID(int=999)))
            if mutation == "duplicate-execution-id":
                results[1].set("executionId", results[0].get("executionId"))
                definitions[1].find("Execution").set("id", results[0].get("executionId"))
            if mutation == "foreign-test-id": results[0].set("testId", str(uuid.UUID(int=999)))
            if mutation == "foreign-definition-name": definitions[0].set("name", "Foreign.Test")
            if mutation == "missing-definition": definitions.remove(definitions[0])
            if mutation == "outside-case": results[0].set("startTime", "2026-11-05T09:59:59Z")
            if mutation == "reversed-case": results[0].set("endTime", "2026-11-05T10:00:01Z")
            if mutation == "naive-times": times.set("start", "2026-11-05T10:00:01")
            if mutation == "outside-persistence":
                next(case for case in results if case.get("testName") == verify.PERSISTENCE_CASE).set("endTime", "2026-11-05T10:00:04Z")
            fixture.native = ET.tostring(tree)
            fixture.seal()
            with test.assertRaises(ValueError): check()
    fixture.native = original
    fixture.seal()
    check()


def check_execution_boundaries(test, fixture, check):
    """Exercise exact clock boundaries and the runner's separate report-processing lifetime."""
    original_result = copy.deepcopy(fixture.result)
    original_native = fixture.native
    original_observation = fixture.functional["observedAtUtc"]
    for field in ("deadlines", "execution"):
        for value in (None, [], "invalid", True, 1):
            with test.subTest(field=field, value=value):
                fixture.result = copy.deepcopy(original_result)
                target = fixture.result["bindings"] if field == "deadlines" else fixture.result["bindings"]["deadlines"]
                target[field] = value
                fixture.seal()
                with test.assertRaisesRegex(ValueError, "issued execution timing"): check()
    for seconds in (None, True, "120", 0, -1, 1, 120.0000001):
        with test.subTest(effectiveSeconds=seconds):
            fixture.result = copy.deepcopy(original_result)
            fixture.result["bindings"]["deadlines"]["execution"]["effectiveSeconds"] = seconds
            fixture.seal()
            with test.assertRaisesRegex(ValueError, "deadline is inconsistent"): check()
    for field, value in (("expiresAt", "2026-11-05T09:59:59Z"), ("effectiveSeconds", 120),
                         ("configurationContentIdentity", "sha256:" + "0" * 64)):
        with test.subTest(undeclaredField=field):
            fixture.result = copy.deepcopy(original_result)
            fixture.result["bindings"]["deadlines"]["execution"] = dict(state="not-declared", origin="test-execution-started",
                originAt="2026-11-05T10:00:00Z", **{field: value})
            fixture.seal()
            with test.assertRaisesRegex(ValueError, "undeclared execution"): check()
    fixture.result = copy.deepcopy(original_result)
    timing = fixture.result["bindings"]["deadlines"]["execution"]
    timing.update(effectiveSeconds=8, expiresAt="2026-11-05T10:00:08Z")
    fixture.seal()
    check()  # Child/report finished by expiry; terminal recording legitimately follows at :09.
    timing.update(effectiveSeconds=7, expiresAt="2026-11-05T10:00:07Z")
    fixture.seal()
    with test.assertRaisesRegex(ValueError, "native execution exceeded"): check()
    fixture.result = original_result
    tree = ET.fromstring(original_native)
    case = next(item for item in tree.iter("UnitTestResult") if item.get("testName") == verify.PERSISTENCE_CASE)
    case.set("endTime", "2026-11-05T10:00:06.0000001Z")
    fixture.native = ET.tostring(tree)
    fixture.functional["observedAtUtc"] = "2026-11-05T10:00:06.0000002Z"
    fixture.seal()
    with test.assertRaisesRegex(ValueError, "outside native persistence"): check()
    fixture.functional["observedAtUtc"] = "2026-11-05T11:00:06.0000001+01:00"
    fixture.seal()
    check()
    fixture.native = original_native
    fixture.functional["observedAtUtc"] = original_observation
    fixture.seal()


def check_synthetic_tenant_boundaries(test, fixture, check):
    """Reseal coherent foreign effects and malformed issued tenant bindings through each reader."""
    original_environment = copy.deepcopy(fixture.result["bindings"]["environment"])
    original_functional = copy.deepcopy(fixture.functional)
    fixture.functional["aggregate"]["tenantId"] = "g4t-foreign"
    for event in fixture.functional["events"]:
        event["identity"] = {key: value.replace("g4t-fixture", "g4t-foreign") for key, value in event["identity"].items()}
    fixture.seal()
    with test.assertRaisesRegex(ValueError, "issued synthetic tenant"): check()
    fixture.functional = copy.deepcopy(original_functional)
    for field, values in (("environment", (None, [], "invalid", True, 1, "missing")),
                          ("isolation", (None, [], "invalid", True, 1, {}, "missing")),
                          ("state", (None, "not-yet-populated", "missing")),
                          ("syntheticTenantId", (None, [], {}, True, 1, "", "g4t-", "G4T-fixture", "g4t-foreign",
                                                 "g4t-space tenant", "g4t-trailing-", "g4t-" + "a" * 61, "missing"))):
        for value in values:
            with test.subTest(field=field, value=value):
                fixture.result["bindings"]["environment"] = copy.deepcopy(original_environment)
                target = (fixture.result["bindings"] if field == "environment" else
                          fixture.result["bindings"]["environment"] if field == "isolation" else
                          fixture.result["bindings"]["environment"]["isolation"])
                if value == "missing":
                    del target[field]
                else:
                    target[field] = value
                fixture.seal()
                # The released contract requires bindings.environment, so its absence refuses at that guard first.
                expected = "released contract field" if (field, value) == ("environment", "missing") else "issued synthetic tenant"
                with test.assertRaisesRegex(ValueError, expected): check()
    fixture.result["bindings"]["environment"] = original_environment
    fixture.seal()
    check()


def check_required_contract_keys(test, fixture, check):
    """Refuse a sealed result lacking any key the released schema requires at the top level or in bindings."""
    fixture.seal()
    original = copy.deepcopy(fixture.result)
    for scope, keys in (("result", RELEASED_RESULT_REQUIRED), ("bindings", RELEASED_BINDINGS_REQUIRED)):
        for key in keys:
            with test.subTest(scope=scope, key=key):
                changed = copy.deepcopy(original)
                del (changed if scope == "result" else changed["bindings"])[key]
                fixture.write("test-result.json", changed)  # Written directly: seal() would restore diagnostics.
                with test.assertRaisesRegex(ValueError, "unsupported result contract|released contract field"): check()
    fixture.write("test-result.json", original)
    check()


def check_released_schema(test, fixture, check):
    """Validate the sealed fixture with the full released schema only when jsonschema and the Builds schema both exist."""
    body = released_result_schema()
    if body is None or importlib.util.find_spec("jsonschema") is None:
        test.skipTest("the full released-schema check needs jsonschema and the Builds schema checkout")
    import jsonschema
    test.assertEqual(hashlib.sha256(body).hexdigest(), RELEASED_RESULT_SCHEMA_SHA256)
    # FormatChecker asserts date-time/uri only when jsonschema's optional format extras are installed; the schema's
    # own utc/endpoint patterns are enforced either way.
    fixture.seal()
    validator = jsonschema.Draft202012Validator(json.loads(body), format_checker=jsonschema.FormatChecker())
    sealed = json.loads((fixture.root / "test-result.json").read_text(encoding="utf-8"))
    test.assertEqual(sorted(error.message for error in validator.iter_errors(sealed)), [])
    check()


class CurrentOwnerEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.expected = dict(repository="Hexalith/Hexalith.Parties", run="123", attempt=2, source="a" * 40, platform="b" * 40, builds="c" * 40)
        self.context = dict(kind="ci", repository=self.expected["repository"], workflowRunId="123", workflowAttempt=2,
                            jobId="build-and-test", runnerKind="github-hosted", operatingSystem="Linux", disposable=True)
        self.owner = dict(runId="1" * 32, attemptId="2" * 32, attemptSequence=1, environmentId="3" * 32, ownerRunId="1" * 32, role="owner")
        self.write("invocation.json", dict(classification="baseline-only", executionContext=self.context, profiles=[verify.PROFILE]))
        phase = dict(runId=self.owner["runId"], outcome=dict(exitCode="Success"), remainingResources=dict(unverified=False, isEmpty=True,
                     containerIds=[], processIds=[], workspaceIds=[], claimIds=[], discoveryFailures=[], resourceIds=[]))
        for name in ("execution.json", "cleanup.json"):
            self.write(name, phase)
        # Shaped like the genuine hosted 42690523916 result: valid under the released hexalith.test-result.v1 schema.
        self.result = dict(schema="hexalith.test-result.v1", policyId="hexalith.platform.candidate-and-attachment-policy.v1",
            bindings=dict(run=self.owner, tool=dict(state="not-yet-populated"),
                          candidate=dict(state="populated", classification="baseline-only", reason="No authenticated candidate request was selected."),
                          decision=dict(state="not-yet-populated"), reuseLineage=dict(state="not-yet-populated"),
                          environment=dict(composition=dict(id="parties", contentIdentity="sha256:" + "5" * 64), mode="package", executionContext=self.context,
                                           catalog=dict(commit=self.expected["builds"], instanceId=self.owner["environmentId"] + "-catalog"),
                                           isolation=dict(state="populated", syntheticTenantId="g4t-fixture", dataIsolationId="g4r-fixture",
                                                          resourceScopeId=self.owner["environmentId"], catalogScopeId="fixture-catalog"),
                                           resources=[dict(resourceId="parties", moduleId="parties",
                                                           endpoints=[dict(name="http", protocol="http", uri="http://localhost:5000/")])],
                                           readiness=dict(state="populated", status="ready", observedAt="2026-11-05T09:59:59Z",
                                                          resources=[dict(resourceId="parties", status="ready", observedAt="2026-11-05T09:59:59Z", diagnosticIds=[])]),
                                           startupTasks=dict(state="populated", tasks=[])),
                          artifacts=dict(expected=dict(state="not-yet-populated"), loaded=dict(state="not-yet-populated")),
                          suite=dict(declared=dict(state="not-yet-populated"), completion=dict(state="not-yet-populated")),
                          attachments=dict(state="populated", holds=[]),
                          deadlines=dict(startup=dict(state="populated", origin="environment-requested", originAt="2026-11-05T09:58:00Z", effectiveSeconds=600,
                                                      expiresAt="2026-11-05T10:08:00Z", override=dict(state="not-overridden")),
                                         execution=dict(state="populated", origin="test-execution-started", originAt="2026-11-05T10:00:00Z",
                                                        effectiveSeconds=120, expiresAt="2026-11-05T10:02:00Z", configurationContentIdentity="sha256:" + "4" * 64))),
            terminal=dict(outcome="passed", cause="tests-completed", recordedAt="2026-11-05T10:00:09Z", diagnosticIds=[]), termination=dict(state="not-yet-populated"),
            retention=dict(state="populated", disposition="not-retained", failureRunIds=[], retainedResourceIds=[],
                           reason="local-failure-retention-not-established", recordedAt="2026-11-05T10:00:10Z"),
            cleanup=dict(state="populated", disposition="complete", attemptId="4" * 32, observedAt="2026-11-05T10:00:10Z", ownedResourceIds=["resource:parties"],
                         failures=[], leftoverResourceIds=[], diagnosticIds=[]), diagnostics=[])
        self.functional = dict(**{key: self.owner[key] for key in ("runId", "attemptId", "environmentId")}, profile=verify.PROFILE,
            revision=self.expected["source"], mappingHash="d" * 64, executionContext=self.context, status="functional-observations-passed", qualificationCredit=False,
            aggregate=dict(tenantId="g4t-fixture", domain="party", aggregateId="party1"), commandId="command1", eventCount=1, metadataSequence=1,
            projection=dict(id="party1", freshness="Current", createdAt="2026-11-05T10:00:00Z"), tenantsNativeQueryObserved=True, memoriesRegistryObserved=True,
            selectorCompatibilityObserved=True, observedAtUtc="2026-11-05T10:00:06Z")
        self.functional["events"] = [dict(identity=dict(
            tenantId="g4t-fixture", domain="party", aggregateId="party1", actorId="g4t-fixture:party:party1",
            eventStreamKeyPrefix="g4t-fixture:party:party1:events:", metadataKey="g4t-fixture:party:party1:metadata",
            snapshotKey="g4t-fixture:party:party1:snapshot", pipelineKeyPrefix="g4t-fixture:party:party1:pipeline:",
            pubSubTopic="g4t-fixture.party.events", queueSession="g4t-fixture:party:party1"), sequenceNumber=1, correlationId="command1", causationId="command1", userId="g4-native-tests",
            serializationFormat="json", eventTypeName="Hexalith.Parties.Contracts.Events.PartyCreated", timestamp="2026-11-05T10:00:00Z")]
        self.functional["currentOwner"] = dict(**{key: self.functional[key] for key in ("runId", "attemptId", "environmentId", "revision", "mappingHash", "profile")},
            configuration="Release", qualificationCredit=False, committedCatalogValidated=True, sdkVersion=verify.SDK_VERSION, tenantsVersion="7.0.5",
            platform=dict(commit=self.expected["platform"], compositionCommit=self.expected["platform"], checkoutCommit=self.expected["platform"], catalogCommit=self.expected["builds"]),
            sdkBodies=verify.SDK.copy(), runtimeBodies={name: str(index) * 64 for index, name in enumerate(("Hexalith.Parties", "Hexalith.Parties.Contracts", "Hexalith.Parties.Projections", "Hexalith.Parties.Authentication", "Hexalith.Parties.Security"), 1)},
            packageArchives={"Hexalith.Parties.Contracts.3.1.0.nupkg": "7" * 64, "Hexalith.Parties.EventStoreExtensions.3.1.0.nupkg": "8" * 64},
            catalogBodies={"Hexalith.Parties.Contracts": "2" * 64, "Hexalith.Parties.EventStoreExtensions": "6" * 64},
            observedExecutableOwners={"Hexalith.Parties, Version=1.0.0.0": "1" * 64}, routingCatalogRoot="e" * 64)
        self.loaded = dict(kind="real-native-sdk-assemblies", profile=verify.PROFILE, ownerRunId=self.owner["runId"], ownerAttemptId=self.owner["attemptId"],
            environmentId=self.owner["environmentId"], revision=self.expected["source"], mappingHash="d" * 64, executionContext=self.context)
        assemblies = [dict(identity=name + f", Version={verify.SDK_VERSION}.0, Culture=neutral, PublicKeyToken=null", sha256=digest) for name, digest in verify.SDK.items()]
        self.loaded.update(loaded=[dict(item, scope="loaded-native-test-process") for item in assemblies],
                           copied=[dict(item, scope="copied-native-composition-output") for item in assemblies])
        self.write("loaded-assemblies-" + self.owner["runId"] + ".json", self.loaded)
        self.native = native_report(verify.CASES)
        self.seal()

    def write(self, name, value):
        (self.root / name).write_text(json.dumps(value), encoding="utf-8")

    def seal(self):
        for path in self.root.glob("native-*.trx"):
            path.unlink()
        for path in self.root.glob("functional-observations-*.json"):
            path.unlink()
        self.result["diagnostics"] = []
        for identity, prefix, suffix, body in (("native-report", "native", "trx", self.native),
                ("functional-observations", "functional-observations", "json", json.dumps(self.functional).encode())):
            digest = hashlib.sha256(body).hexdigest()
            name = prefix + "-" + digest + "." + suffix
            (self.root / name).write_bytes(body)
            self.result["diagnostics"].append(dict(id=identity, classification="internal", redacted=True, reference=name, contentIdentity="sha256:" + digest))
        self.write("test-result.json", self.result)

    def check(self):
        return verify.validate(self.root, **self.expected)

    def test_current_same_run_evidence_passes_without_qualification_credit(self):
        self.assertEqual(self.check()["nativeCases"], 2)
        self.assertFalse(self.check()["qualificationCredit"])

    def test_results_missing_any_released_required_key_are_refused(self):
        check_required_contract_keys(self, self, self.check)

    def test_rebuilt_fixture_satisfies_the_full_released_schema_when_available(self):
        check_released_schema(self, self, self.check)

    def test_reader_required_keys_match_the_literal_released_key_sets(self):
        self.assertEqual(verify.RESULT_SCHEMA_SHA256, RELEASED_RESULT_SCHEMA_SHA256)
        self.assertEqual(verify.RESULT_REQUIRED, frozenset(RELEASED_RESULT_REQUIRED))
        self.assertEqual(verify.RESULT_BINDINGS_REQUIRED, frozenset(RELEASED_BINDINGS_REQUIRED))

    def test_reader_schema_constants_are_bound_to_the_committed_builds_gitlink(self):
        """Runs without the submodule checkout (CI): a Builds gitlink bump fails until the reader keys are re-derived."""
        repository = Path(__file__).resolve().parents[1]
        try:
            listing = subprocess.run(["git", "ls-tree", "HEAD", "references/Hexalith.Builds"], cwd=repository,
                                     capture_output=True, text=True, check=True, timeout=30).stdout.split()
        except (FileNotFoundError, subprocess.CalledProcessError):
            self.skipTest("not a git checkout of the Parties repository")
        self.assertEqual(listing[:2], ["160000", "commit"])
        self.assertEqual(listing[2], verify.RESULT_SCHEMA_BUILDS_COMMIT,
                         "Builds moved: re-derive RESULT_SCHEMA_SHA256 and the required key sets from its test-result schema")

    def test_reader_required_keys_are_bound_to_the_released_schema_hash(self):
        body = released_result_schema()
        if body is None:
            self.skipTest("the Builds schema checkout is absent (CI runs with submodules: false)")
        schema = json.loads(body)
        self.assertEqual(hashlib.sha256(body).hexdigest(), verify.RESULT_SCHEMA_SHA256)
        self.assertEqual(frozenset(schema["required"]), verify.RESULT_REQUIRED)
        self.assertEqual(frozenset(schema["$defs"]["bindings"]["required"]), verify.RESULT_BINDINGS_REQUIRED)

    def test_selector_proof_requires_the_boolean_true(self):
        for value in (None, False, "true", 1):
            with self.subTest(value=value):
                self.functional["selectorCompatibilityObserved"] = value
                self.seal()
                with self.assertRaisesRegex(ValueError, "selector compatibility"): self.check()
        del self.functional["selectorCompatibilityObserved"]
        self.seal()
        with self.assertRaisesRegex(ValueError, "selector compatibility"): self.check()

    def test_native_times_and_execution_identities_are_bound_after_resealing(self):
        check_native_refusals(self, self, self.check)

    def test_precise_execution_boundaries_deadline_shapes_and_report_processing(self):
        check_execution_boundaries(self, self, self.check)

    def test_execution_interval_requires_issued_offset_times_and_completed_order(self):
        original = copy.deepcopy(self.result)
        for mutation in ("missing", "wrong-origin", "unpopulated", "missing-origin", "naive-origin", "reversed", "future-terminal"):
            with self.subTest(mutation=mutation):
                self.result = copy.deepcopy(original)
                timing = self.result["bindings"]["deadlines"]["execution"]
                if mutation == "missing": del self.result["bindings"]["deadlines"]
                if mutation == "wrong-origin": timing["origin"] = "environment-requested"
                if mutation == "unpopulated": timing["state"] = "not-yet-populated"
                if mutation == "missing-origin": timing.pop("originAt")
                if mutation == "naive-origin": timing["originAt"] = "2026-11-05T10:00:00"
                if mutation == "reversed": self.result["terminal"]["recordedAt"] = "2026-11-05T09:59:59Z"
                if mutation == "future-terminal": self.result["terminal"]["recordedAt"] = "2999-11-05T10:00:00Z"
                self.seal()
                with self.assertRaises(ValueError): self.check()
        self.result = original
        self.seal()
        self.check()

    def test_retained_real_namespaced_xunit403_report_passes_timing_and_identity_checks(self):
        path = Path(__file__).resolve().parents[1] / ("_bmad-output/implementation-artifacts/tests/"
            "published-catalog-sdk-adoption-2026-11-09/completed-sdk420-v3.1.1/ordinary-native.trx.gz")
        report = ET.fromstring(gzip.decompress(path.read_bytes()))
        cases = [item for item in report.iter() if item.tag.rsplit("}", 1)[-1] == "UnitTestResult"]
        times = next(item for item in report if item.tag.rsplit("}", 1)[-1] == "Times")
        start = datetime.fromisoformat(times.get("start")) - timedelta(seconds=1)
        terminal = datetime.fromisoformat(times.get("finish")) + timedelta(seconds=1)
        persistence = next(case for case in cases if case.get("testName") == verify.PERSISTENCE_CASE)
        observed = persistence.get("startTime")
        result = dict(bindings=dict(deadlines=dict(execution=dict(state="populated", origin="test-execution-started",
            originAt=start.isoformat(), effectiveSeconds=300, expiresAt=(start + timedelta(seconds=300)).isoformat()))),
            terminal=dict(recordedAt=terminal.isoformat()))
        self.assertEqual(verify.native_execution(report, cases, result, dict(observedAtUtc=observed)),
                         (verify.observation_timestamp(observed), verify.observation_timestamp(observed)))
        # This is reporter compatibility only, not a claim of fresh runtime execution.

    def test_equivalent_offset_and_unbounded_declared_execution_are_supported(self):
        timing = self.result["bindings"]["deadlines"]["execution"]
        timing.update(state="not-declared", originAt="2026-11-05T11:00:00+01:00")
        timing.pop("expiresAt")
        timing.pop("effectiveSeconds")
        timing.pop("configurationContentIdentity")
        self.functional["observedAtUtc"] = "2026-11-05T11:00:06.0000000+01:00"
        self.seal()
        self.check()

    def test_counts_and_event_sequences_require_json_integers(self):
        for field in ("eventCount", "metadataSequence", "sequenceNumber"):
            target = self.functional["events"][0] if field == "sequenceNumber" else self.functional
            for value in (True, False, 1.0, "1", None, 0, -1, 2):
                with self.subTest(field=field, value=value):
                    target[field] = value
                    self.seal()
                    with self.assertRaisesRegex(ValueError, "persisted sequence|event identity"): self.check()
            target[field] = 1
        self.seal()
        self.assertEqual(self.check()["nativeCases"], 2)

    def test_private_assembly_scopes_and_bounded_inventory_are_required(self):
        for field in ("loaded", "copied"):
            original = copy.deepcopy(self.loaded[field])
            for value in ([], None, {}, original * 17,
                          [dict(item, scope="copied-static-image") for item in original],
                          [{key: value for key, value in item.items() if key != "scope"} for item in original],
                          [None]):
                with self.subTest(field=field, value=value):
                    self.loaded[field] = value
                    self.write("loaded-assemblies-" + self.owner["runId"] + ".json", self.loaded)
                    with self.assertRaisesRegex(ValueError, "inventory|role differs"): self.check()
            self.loaded[field] = original
        self.write("loaded-assemblies-" + self.owner["runId"] + ".json", self.loaded)
        self.check()

    def test_historical_private_package_version_cannot_claim_the_current_sdk_cohort(self):
        archives = self.functional["currentOwner"]["packageArchives"]
        self.functional["currentOwner"]["packageArchives"] = {
            name.replace("3.1.0", "3.0.0"): digest for name, digest in archives.items()
        }
        self.seal()
        with self.assertRaisesRegex(ValueError, "private source packages were not measured"):
            self.check()

    def test_historical_sdk_version_cannot_claim_the_current_owner_cohort(self):
        for version in ("4.1.0", "4.2.1", "4.2.0-preview.1"):
            with self.subTest(version=version):
                self.functional["currentOwner"]["sdkVersion"] = version
                self.seal()
                with self.assertRaisesRegex(ValueError, "wrong current owner cohort"):
                    self.check()

    def test_new_helper_invocation_timeout_and_per_attempt_records_are_bound_to_this_job(self):
        phase = json.loads((self.root / "execution.json").read_text())
        (self.root / "cleanup.json").unlink()
        stderr = dict(bytes=0, sha256=hashlib.sha256(b"").hexdigest(), excerpt="", truncated=False, redacted=True)
        self.write("invocation.json", dict(executionContext=self.context, profiles=[verify.PROFILE], classification="baseline-only",
                                           timeoutSeconds=2045))
        self.write("execution.json", dict(schema=verify.EXECUTION_SCHEMA, executionContext=self.context, processExitCode=0, launched=True,
                                          cancelled=False, timedOut=False, signals=[], outputParsed=True, result=phase, stderr=stderr))
        attempt = dict(schema=verify.CLEANUP_SCHEMA, status="completed", runId=self.owner["runId"], executionContext=self.context,
                       jobStatus="", processExitCode=0, outputParsed=True, verified=True, stderr=stderr, result=phase)
        self.write("cleanup-1.json", attempt)
        self.assertEqual(self.check()["nativeCases"], 2)
        for name, value in {"foreign-owner": dict(attempt, runId="f" * 32), "bool-exit": dict(attempt, processExitCode=False),
                            "foreign-job": dict(attempt, executionContext=dict(self.context, jobId="other"))}.items():
            self.write("cleanup-1.json", value)
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.check()
        self.write("cleanup-1.json", attempt)
        self.write("invocation.json", dict(executionContext=self.context, profiles=[verify.PROFILE], classification="baseline-only",
                                           timeoutSeconds=True))
        with self.assertRaises(ValueError):
            self.check()

    def test_real_captured_sdk_identity_shape_matches_the_three_key_aggregate(self):
        # Exact identity shape captured from genuine 42288812736 attempt 2; no event/body evidence is substituted.
        actor = "g4t-b155e4bb4ff9:party:01M6SA5BHM3EH0HBBN0RJ6D487"
        captured = {"tenantId": "g4t-b155e4bb4ff9", "domain": "party", "aggregateId": "01M6SA5BHM3EH0HBBN0RJ6D487",
                    "actorId": actor, "eventStreamKeyPrefix": actor + ":events:", "metadataKey": actor + ":metadata",
                    "snapshotKey": actor + ":snapshot", "pipelineKeyPrefix": actor + ":pipeline:",
                    "pubSubTopic": "g4t-b155e4bb4ff9.party.events", "queueSession": actor}
        self.functional["aggregate"] = {key: captured[key] for key in ("tenantId", "domain", "aggregateId")}
        self.result["bindings"]["environment"]["isolation"]["syntheticTenantId"] = captured["tenantId"]
        self.functional["events"][0]["identity"] = captured
        self.functional["projection"]["id"] = captured["aggregateId"]
        self.seal()
        self.assertEqual(self.check()["nativeCases"], 2)

    def test_persisted_effects_require_the_exact_issued_synthetic_tenant(self):
        check_synthetic_tenant_boundaries(self, self, self.check)

    def test_every_core_and_computed_persisted_identity_field_is_required_and_exact(self):
        original = copy.deepcopy(self.functional["events"][0]["identity"])
        self.assertEqual(len(original), 10)
        for field in original:
            for mutation in ("missing", "foreign", "null"):
                with self.subTest(field=field, mutation=mutation):
                    identity = copy.deepcopy(original)
                    if mutation == "missing": del identity[field]
                    if mutation == "foreign": identity[field] = "foreign-value"
                    if mutation == "null": identity[field] = None
                    self.functional["events"][0]["identity"] = identity
                    self.seal()
                    with self.assertRaises(ValueError): self.check()
        self.functional["events"][0]["identity"] = original
        self.seal()

    def test_core_only_extra_and_coherent_foreign_sdk_identities_are_refused(self):
        original = self.functional["events"][0]["identity"]
        foreign = {key: value.replace("g4t-fixture", "g4t-foreign") for key, value in original.items()}
        for identity in (copy.deepcopy(self.functional["aggregate"]), {**original, "unexpected": "value"}, foreign, None, []):
            with self.subTest(identity=identity):
                self.functional["events"][0]["identity"] = identity
                self.seal()
                with self.assertRaises(ValueError): self.check()
        self.functional["events"][0]["identity"] = original
        self.seal()

    def test_identity_core_requires_actual_sdk_canonical_component_grammar(self):
        original = self.functional["aggregate"]
        for field, value in (("tenantId", "G4T-fixture"), ("tenantId", "g4t-" + "a" * 61),
                             ("tenantId", "g4t-other:tenant"), ("domain", "Party"), ("domain", "party_other"),
                             ("aggregateId", "other:aggregate"), ("aggregateId", "-party"),
                             ("aggregateId", "a" * 257), ("aggregateId", True)):
            with self.subTest(field=field, value=value):
                self.functional["aggregate"] = {**original, field: value}
                self.seal()
                with self.assertRaises(ValueError): self.check()
        self.functional["aggregate"] = original
        self.seal()

    def test_sdk_system_topic_derivation_does_not_widen_current_run_tenant_scope(self):
        core = {"tenantId": "system", "domain": "party", "aggregateId": "Party_1.A"}
        identity = verify.persisted_identity(core)
        self.assertEqual(identity["pubSubTopic"], "party.events")
        self.assertEqual(identity["actorId"], "system:party:Party_1.A")
        self.functional["aggregate"] = core
        self.functional["events"][0]["identity"] = identity
        self.functional["projection"]["id"] = core["aggregateId"]
        self.seal()
        with self.assertRaises(ValueError): self.check()

    def test_changed_semantic_boundaries_are_refused_after_valid_content_hashes(self):
        for mutation in ("profile", "source", "run", "attempt", "sdk", "runtime", "public", "projection", "event", "memories", "platform", "builds", "catalog", "archives"):
            with self.subTest(mutation=mutation):
                original = copy.deepcopy(self.functional)
                if mutation == "profile": self.functional["profile"] = "platform-package"
                if mutation == "source": self.functional["revision"] = "f" * 40
                if mutation == "run": self.functional["runId"] = "4" * 32
                if mutation == "attempt": self.functional["attemptId"] = "4" * 32
                if mutation == "sdk": self.functional["currentOwner"]["sdkBodies"]["Hexalith.EventStore.Contracts"] = "f" * 64
                if mutation == "runtime": self.functional["currentOwner"]["runtimeBodies"]["Hexalith.Parties.Contracts"] = "f" * 64
                if mutation == "public":
                    self.functional["currentOwner"]["runtimeBodies"]["Hexalith.Parties.Contracts"] = verify.PUBLIC_PARTIES_CONTRACTS
                    self.functional["currentOwner"]["catalogBodies"]["Hexalith.Parties.Contracts"] = verify.PUBLIC_PARTIES_CONTRACTS
                if mutation == "projection": self.functional["projection"]["id"] = "other"
                if mutation == "event": self.functional["events"][0]["causationId"] = "other"
                if mutation == "memories": self.functional["memoriesRegistryObserved"] = False
                if mutation in ("platform", "builds"):
                    self.functional["currentOwner"]["platform"]["commit" if mutation == "platform" else "catalogCommit"] = "f" * 40
                if mutation == "archives": self.functional["currentOwner"]["packageArchives"] = {}
                if mutation == "catalog": self.functional["currentOwner"]["committedCatalogValidated"] = False
                self.seal()
                with self.assertRaises(ValueError): self.check()
                self.functional = original
                self.seal()

    def test_missing_skipped_duplicate_or_renamed_native_cases_are_refused(self):
        original = self.native
        for body in (b'<TestRun/>', original.replace(b'Passed', b'NotExecuted', 1), original.replace(b'GatewayRefusesAnonymousCommandStatusAsync', b'Renamed'),
                     original.replace(b'</Results>', b'<UnitTestResult testName="PartyEnvironmentTests.GatewayRefusesAnonymousCommandStatusAsync" outcome="Passed"/></Results>')):
            with self.subTest(body=body):
                self.native = body
                self.seal()
                with self.assertRaises(ValueError): self.check()
        self.native = original

    def test_counterfeit_fully_qualified_case_identities_are_refused_with_valid_hashes(self):
        original = self.native
        for fake in ("Counterfeit.Hexalith.Parties.Platform.IntegrationTests.PartyEnvironmentTests", "Hexalith.Parties.Platform.IntegrationTests.CounterfeitPartyEnvironmentTests"):
            with self.subTest(fake=fake):
                self.native = original.replace(b"Hexalith.Parties.Platform.IntegrationTests.PartyEnvironmentTests", fake.encode())
                self.seal()
                with self.assertRaises(ValueError): self.check()

    def test_missing_null_untyped_or_invalid_creation_timestamps_are_refused_with_valid_hashes(self):
        for value in (None, 0, "not-a-date", "2026-11-05T10:00:00", "2026-13-05T10:00:00Z", "missing"):
            with self.subTest(value=value):
                self.functional["events"][0]["timestamp"] = value
                self.functional["projection"]["createdAt"] = value
                if value == "missing":
                    del self.functional["events"][0]["timestamp"]
                    del self.functional["projection"]["createdAt"]
                self.seal()
                with self.assertRaises(ValueError): self.check()

    def test_fifo_and_raced_file_or_directory_links_are_refused_without_blocking(self):
        filename = self.result["diagnostics"][0]["reference"]
        path = self.root / filename
        body = path.read_bytes()
        path.unlink()
        os.mkfifo(path)
        started = time.monotonic()
        with self.assertRaises(ValueError): self.check()
        self.assertLess(time.monotonic() - started, 1)
        path.unlink()
        path.write_bytes(body)
        outside = Path(self.temp.name).with_name(Path(self.temp.name).name + "-outside")
        outside.mkdir()
        self.addCleanup(lambda: __import__("shutil").rmtree(outside))
        (outside / filename).write_bytes(body)
        original_open = os.open
        def race_file(name, flags, *args, **kwargs):
            if name == filename:
                path.unlink()
                path.symlink_to(outside / filename)
            return original_open(name, flags, *args, **kwargs)
        with mock.patch.object(verify.os, "open", side_effect=race_file), self.assertRaises(ValueError): self.check()
        path.unlink()
        path.write_bytes(body)
        original_root = self.root.with_name(self.root.name + "-held")
        def race_directory(name, flags, *args, **kwargs):
            if name == self.root.name:
                self.root.rename(original_root)
                self.root.symlink_to(outside, target_is_directory=True)
            return original_open(name, flags, *args, **kwargs)
        try:
            with mock.patch.object(verify.os, "open", side_effect=race_directory), self.assertRaises(ValueError): self.check()
        finally:
            self.root.unlink()
            original_root.rename(self.root)

    def test_missing_mandatory_diagnostic_or_changed_bytes_are_refused(self):
        self.result["diagnostics"] = self.result["diagnostics"][:1]
        self.write("test-result.json", self.result)
        with self.assertRaises(ValueError): self.check()
        self.seal()
        path = self.root / self.result["diagnostics"][0]["reference"]
        path.write_bytes(path.read_bytes() + b" ")
        with self.assertRaises(ValueError): self.check()

    def test_foreign_invocation_incomplete_cleanup_and_unreferenced_substitutes_are_refused(self):
        self.write("invocation.json", dict(classification="baseline-only", executionContext=self.context, profiles=["platform-package"]))
        with self.assertRaises(ValueError): self.check()
        self.write("invocation.json", dict(classification="baseline-only", executionContext=self.context, profiles=[verify.PROFILE]))
        self.result["cleanup"]["leftoverResourceIds"] = ["parties"]
        self.write("test-result.json", self.result)
        with self.assertRaises(ValueError): self.check()
        self.result["cleanup"]["leftoverResourceIds"] = []
        self.write("test-result.json", self.result)
        self.write("functional-observations-substitute.json", self.functional)
        with self.assertRaises(ValueError): self.check()

    def test_links_duplicate_json_and_foreign_native_sdk_are_refused(self):
        self.loaded["loaded"][0]["sha256"] = "f" * 64
        self.write("loaded-assemblies-" + self.owner["runId"] + ".json", self.loaded)
        with self.assertRaises(ValueError): self.check()
        with self.assertRaises(ValueError): verify.decode(b'{"a":1,"a":2}')
        target = self.root / "outside.json"
        target.write_text("{}")
        (self.root / "linked.json").symlink_to(target)
        with self.assertRaises(ValueError): verify.read(self.root, "linked.json")


if __name__ == "__main__":
    unittest.main()
