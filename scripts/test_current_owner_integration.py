"""Boundary fixtures for the mandatory current-workflow artifact gate."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("current_owner", Path(__file__).with_name("verify-current-owner-integration.py"))
verify = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(verify)


class CurrentOwnerEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.expected = dict(repository="Hexalith/Hexalith.Parties", run="123", attempt=2, source="a" * 40, platform="b" * 40, builds="c" * 40)
        self.context = dict(kind="ci", repository=self.expected["repository"], workflowRunId="123", workflowAttempt=2,
                            jobId="build-and-test", runnerKind="github-hosted", operatingSystem="Linux", disposable=True)
        self.owner = dict(runId="1" * 32, attemptId="2" * 32, environmentId="3" * 32, ownerRunId="1" * 32, role="owner")
        self.write("invocation.json", dict(classification="baseline-only", executionContext=self.context, profiles=[verify.PROFILE]))
        phase = dict(runId=self.owner["runId"], outcome=dict(exitCode="Success"), remainingResources=dict(unverified=False, isEmpty=True,
                     containerIds=[], processIds=[], workspaceIds=[], claimIds=[], discoveryFailures=[], resourceIds=[]))
        for name in ("execution.json", "cleanup.json"):
            self.write(name, phase)
        self.result = dict(schema="hexalith.test-result.v1", policyId="hexalith.platform.candidate-and-attachment-policy.v1",
            bindings=dict(run=self.owner, environment=dict(mode="package", executionContext=self.context), candidate=dict(classification="baseline-only")),
            terminal=dict(outcome="passed", cause="tests-completed"), termination=dict(state="not-yet-populated"),
            cleanup=dict(state="populated", disposition="complete", failures=[], leftoverResourceIds=[]), diagnostics=[])
        self.functional = dict(**{key: self.owner[key] for key in ("runId", "attemptId", "environmentId")}, profile=verify.PROFILE,
            revision=self.expected["source"], mappingHash="d" * 64, executionContext=self.context, status="functional-observations-passed", qualificationCredit=False,
            aggregate=dict(tenantId="g4t-fixture", domain="party", aggregateId="party1"), commandId="command1", eventCount=1, metadataSequence=1,
            projection=dict(id="party1", freshness="Current", createdAt="2026-11-05T10:00:00Z"), tenantsNativeQueryObserved=True, memoriesRegistryObserved=True)
        self.functional["events"] = [dict(identity=self.functional["aggregate"], sequenceNumber=1, correlationId="command1", causationId="command1", userId="g4-native-tests",
            serializationFormat="json", eventTypeName="Hexalith.Parties.Contracts.Events.PartyCreated", timestamp="2026-11-05T10:00:00Z")]
        self.functional["currentOwner"] = dict(**{key: self.functional[key] for key in ("runId", "attemptId", "environmentId", "revision", "mappingHash", "profile")},
            configuration="Release", qualificationCredit=False, committedCatalogValidated=True, sdkVersion="4.1.0", tenantsVersion="7.0.4",
            platform=dict(commit=self.expected["platform"], compositionCommit=self.expected["platform"], checkoutCommit=self.expected["platform"], catalogCommit=self.expected["builds"]),
            sdkBodies=verify.SDK.copy(), runtimeBodies={name: str(index) * 64 for index, name in enumerate(("Hexalith.Parties", "Hexalith.Parties.Contracts", "Hexalith.Parties.Projections", "Hexalith.Parties.Authentication", "Hexalith.Parties.Security"), 1)},
            catalogBodies={"Hexalith.Parties.Contracts": "2" * 64, "Hexalith.Parties.EventStoreExtensions": "6" * 64},
            observedExecutableOwners={"Hexalith.Parties, Version=1.0.0.0": "1" * 64}, routingCatalogRoot="e" * 64)
        self.loaded = dict(kind="real-native-sdk-assemblies", profile=verify.PROFILE, ownerRunId=self.owner["runId"], ownerAttemptId=self.owner["attemptId"],
            environmentId=self.owner["environmentId"], revision=self.expected["source"], mappingHash="d" * 64, executionContext=self.context)
        assemblies = [dict(identity=name + ", Version=4.1.0.0, Culture=neutral, PublicKeyToken=null", sha256=digest) for name, digest in verify.SDK.items()]
        self.loaded.update(loaded=assemblies, copied=copy.deepcopy(assemblies))
        self.write("loaded-assemblies-" + self.owner["runId"] + ".json", self.loaded)
        self.native = ('<TestRun><Results>' + ''.join('<UnitTestResult testName="Hexalith.Parties.Platform.IntegrationTests.' + name + '" outcome="Passed"/>' for name in sorted(verify.CASES))
                       + '</Results><ResultSummary><Counters total="2" executed="2" passed="2" failed="0"/></ResultSummary></TestRun>').encode()
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

    def test_changed_semantic_boundaries_are_refused_after_valid_content_hashes(self):
        for mutation in ("profile", "source", "run", "attempt", "sdk", "runtime", "public", "projection", "event", "memories", "platform", "builds", "catalog"):
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
