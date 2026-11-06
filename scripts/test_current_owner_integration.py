"""Boundary fixtures for the mandatory current-workflow artifact gate."""
import copy
import hashlib
import importlib.util
import json
import os
import time
from unittest import mock
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
        self.functional["events"] = [dict(identity=dict(
            tenantId="g4t-fixture", domain="party", aggregateId="party1", actorId="g4t-fixture:party:party1",
            eventStreamKeyPrefix="g4t-fixture:party:party1:events:", metadataKey="g4t-fixture:party:party1:metadata",
            snapshotKey="g4t-fixture:party:party1:snapshot", pipelineKeyPrefix="g4t-fixture:party:party1:pipeline:",
            pubSubTopic="g4t-fixture.party.events", queueSession="g4t-fixture:party:party1"), sequenceNumber=1, correlationId="command1", causationId="command1", userId="g4-native-tests",
            serializationFormat="json", eventTypeName="Hexalith.Parties.Contracts.Events.PartyCreated", timestamp="2026-11-05T10:00:00Z")]
        self.functional["currentOwner"] = dict(**{key: self.functional[key] for key in ("runId", "attemptId", "environmentId", "revision", "mappingHash", "profile")},
            configuration="Release", qualificationCredit=False, committedCatalogValidated=True, sdkVersion="4.1.0", tenantsVersion="7.0.5",
            platform=dict(commit=self.expected["platform"], compositionCommit=self.expected["platform"], checkoutCommit=self.expected["platform"], catalogCommit=self.expected["builds"]),
            sdkBodies=verify.SDK.copy(), runtimeBodies={name: str(index) * 64 for index, name in enumerate(("Hexalith.Parties", "Hexalith.Parties.Contracts", "Hexalith.Parties.Projections", "Hexalith.Parties.Authentication", "Hexalith.Parties.Security"), 1)},
            packageArchives={"Hexalith.Parties.Contracts.3.0.0.nupkg": "7" * 64, "Hexalith.Parties.EventStoreExtensions.3.0.0.nupkg": "8" * 64},
            catalogBodies={"Hexalith.Parties.Contracts": "2" * 64, "Hexalith.Parties.EventStoreExtensions": "6" * 64},
            observedExecutableOwners={"Hexalith.Parties, Version=1.0.0.0": "1" * 64}, routingCatalogRoot="e" * 64)
        self.loaded = dict(kind="real-native-sdk-assemblies", profile=verify.PROFILE, ownerRunId=self.owner["runId"], ownerAttemptId=self.owner["attemptId"],
            environmentId=self.owner["environmentId"], revision=self.expected["source"], mappingHash="d" * 64, executionContext=self.context)
        assemblies = [dict(identity=name + ", Version=4.1.0.0, Culture=neutral, PublicKeyToken=null", sha256=digest) for name, digest in verify.SDK.items()]
        self.loaded.update(loaded=assemblies, copied=copy.deepcopy(assemblies))
        self.write("loaded-assemblies-" + self.owner["runId"] + ".json", self.loaded)
        self.native = ('<TestRun><Results>' + ''.join('<UnitTestResult testName="' + name + '" outcome="Passed"/>' for name in sorted(verify.CASES))
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

    def test_real_captured_sdk_identity_shape_matches_the_three_key_aggregate(self):
        # Exact identity shape captured from genuine 42288812736 attempt 2; no event/body evidence is substituted.
        actor = "g4t-b155e4bb4ff9:party:01M6SA5BHM3EH0HBBN0RJ6D487"
        captured = {"tenantId": "g4t-b155e4bb4ff9", "domain": "party", "aggregateId": "01M6SA5BHM3EH0HBBN0RJ6D487",
                    "actorId": actor, "eventStreamKeyPrefix": actor + ":events:", "metadataKey": actor + ":metadata",
                    "snapshotKey": actor + ":snapshot", "pipelineKeyPrefix": actor + ":pipeline:",
                    "pubSubTopic": "g4t-b155e4bb4ff9.party.events", "queueSession": actor}
        self.functional["aggregate"] = {key: captured[key] for key in ("tenantId", "domain", "aggregateId")}
        self.functional["events"][0]["identity"] = captured
        self.functional["projection"]["id"] = captured["aggregateId"]
        self.seal()
        self.assertEqual(self.check()["nativeCases"], 2)

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
