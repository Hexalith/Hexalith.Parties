"""Synthetic artifact/byte controls only; no GitHub identity, Docker, native run or qualification credit."""
import copy
from datetime import datetime, timedelta
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


public = load("public_package_controls", "public_runtime_observation_contract.py")
private_fixture = load("unchanged_private_controls", "test_runtime_observation_contract.py")


# Literal xUnit 4.0.3 MTP TRX snapshot, independent of the validator's name sets.
XUNIT_403_TRX_CASE_NAMES = (
    r'Hexalith.Parties.Platform.IntegrationTests.FunctionalObservationWriterTests.DuplicateCanonicalWriteRefusesWhileRecheckPreservesOriginalBytesAsync',
    r'Hexalith.Parties.Platform.IntegrationTests.FunctionalObservationWriterTests.RecheckRequiresOriginalAndPreservesOutputOwnershipBoundsAsync',
    r'Hexalith.Parties.Platform.IntegrationTests.MemoriesTenantObservationTests.ActiveCurrentTenantPasses',
    r'Hexalith.Parties.Platform.IntegrationTests.MemoriesTenantObservationTests.InactiveCurrentTenantFails(status: \"deleting\")',
    r'Hexalith.Parties.Platform.IntegrationTests.MemoriesTenantObservationTests.InactiveCurrentTenantFails(status: \"failed\")',
    r'Hexalith.Parties.Platform.IntegrationTests.MemoriesTenantObservationTests.InactiveCurrentTenantFails(status: \"provisioning\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeAssemblyObservationTests.CopiedOutputRefusesLinkedAssembly',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeAssemblyObservationTests.CopiedOutputReportsActualFileHashWithoutLoadingIt',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeAssemblyObservationTests.PrivateDiagnosticRefusesSymbolicDirectory',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeAssemblyObservationTests.PrivateDiagnosticRetainsFirstBytesAndRefusesOverwrite',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationPreservesOrdinaryBatchesAndRefusesMixedCurrentPurpose(profile: \"platform-current-package\", profiles: \"platform-current-package,platform-current-package\", accepted: False)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationPreservesOrdinaryBatchesAndRefusesMixedCurrentPurpose(profile: \"platform-current-package\", profiles: \"platform-current-package,platform-package\", accepted: False)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationPreservesOrdinaryBatchesAndRefusesMixedCurrentPurpose(profile: \"platform-current-package\", profiles: \"platform-current-package\", accepted: True)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationPreservesOrdinaryBatchesAndRefusesMixedCurrentPurpose(profile: \"platform-package\", profiles: \"lifecycle-package\", accepted: False)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationPreservesOrdinaryBatchesAndRefusesMixedCurrentPurpose(profile: \"platform-package\", profiles: \"platform-package,lifecycle-package\", accepted: True)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationPreservesOrdinaryBatchesAndRefusesMixedCurrentPurpose(profile: \"platform-package\", profiles: \"platform-package,platform-current-package\", accepted: False)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"changed-context\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"changed-mapping\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"current-valid-local\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"foreign-attempt\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"foreign-model-output\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"foreign-run\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"lifecycle-foreign-receipt\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"lifecycle-foreign-source\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"lifecycle-local\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"valid-local\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PartyEnvironmentTests.GatewayRefusesAnonymousCommandStatusAsync',
    r'Hexalith.Parties.Platform.IntegrationTests.PartyEnvironmentTests.RealDependencies_CreateQueryAndPersistMatchingPartyEvents',
)


class PublicPackageEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.fixture = private_fixture.CurrentOwnerEvidenceTests("test_current_same_run_evidence_passes_without_qualification_credit")
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        f = self.fixture
        self.expected = {**f.expected, "platform": "081178aa5cc01f5dc736ba0d1980aa80fedc6fe1", "builds": "5ec1779040606be89306129a7d2b245258c9acd2"}
        self.expected["native_output"] = Path("/product/Release/native-tests")
        f.functional.update(profile=public.PROFILE, currentOwner=None, selectorCompatibilityObserved=True)
        platform = dict(commit="081178aa5cc01f5dc736ba0d1980aa80fedc6fe1", compositionCommit="081178aa5cc01f5dc736ba0d1980aa80fedc6fe1", checkoutCommit="081178aa5cc01f5dc736ba0d1980aa80fedc6fe1",
                        catalogCommit="5ec1779040606be89306129a7d2b245258c9acd2", releaseTag="v1.9.0")
        now = datetime.fromisoformat("2026-11-05T10:00:05+00:00")
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
        f.native = private_fixture.native_report(XUNIT_403_TRX_CASE_NAMES)
        f.seal()

    def test_expected_sources_require_full_canonical_commit_identities_at_both_entry_points(self):
        for field in ("source", "platform", "builds"):
            for value in (None, "", "a" * 7, "a" * 39, "a" * 41, "A" * 40, "g" * 40, " " + "a" * 40):
                with self.subTest(field=field, value=value):
                    expected = dict(self.expected, **{field: value})
                    with self.assertRaisesRegex(ValueError, "full canonical commit"):
                        public.validate(self.fixture.root, **expected)
                    with public.held_directory(self.fixture.root) as directory:
                        with self.assertRaisesRegex(ValueError, "full canonical commit"):
                            public.validate_held(directory, **expected)

    def check(self):
        return public.validate(self.fixture.root, **self.expected)

    def test_separate_public_roles_accept_a_complete_native_report_as_a_unit_fixture_only(self):
        result = self.check()
        self.assertEqual(result["nativeCases"], 28)
        self.assertEqual(result["profile"], "platform-package")
        self.assertFalse(result["qualificationCredit"])

    def test_results_missing_any_released_required_key_are_refused(self):
        private_fixture.check_required_contract_keys(self, self.fixture, self.check)


    def test_persisted_effects_require_the_exact_issued_synthetic_tenant(self):
        private_fixture.check_synthetic_tenant_boundaries(self, self.fixture, self.check)

    def test_counts_and_event_sequences_require_json_integers(self):
        f = self.fixture
        for field in ("eventCount", "metadataSequence", "sequenceNumber"):
            target = f.functional["events"][0] if field == "sequenceNumber" else f.functional
            for value in (True, False, 1.0, "1", None, 0, -1, 2):
                with self.subTest(field=field, value=value):
                    target[field] = value
                    f.seal()
                    with self.assertRaisesRegex(ValueError, "persisted sequence|event identity"): self.check()
            target[field] = 1
        f.seal()
        self.assertEqual(self.check()["nativeCases"], 28)

    def test_native_times_and_execution_identities_are_bound_after_resealing(self):
        private_fixture.check_native_refusals(self, self.fixture, self.check)

    def test_precise_execution_boundaries_deadline_shapes_and_report_processing(self):
        private_fixture.check_execution_boundaries(self, self.fixture, self.check)

    def test_image_measurement_occurs_inside_persistence_even_for_an_earlier_image_start(self):
        f = self.fixture
        runtime = f.functional["publicRuntime"]
        runtime["image"]["startedAt"] = "2026-11-05T10:00:00Z"
        runtime["measuredAtUtc"] = "2026-11-05T10:00:01Z"
        f.seal()
        with self.assertRaisesRegex(ValueError, "measurement lies outside"): self.check()
        runtime["measuredAtUtc"] = "2026-11-05T10:00:06.0000001Z"
        f.seal()
        with self.assertRaisesRegex(ValueError, "measurement lies outside"): self.check()
        runtime["measuredAtUtc"] = "2026-11-05T10:00:05.0000000Z"
        runtime["image"]["startedAt"] = "2026-11-05T10:00:05.000000001Z"
        f.seal()
        with self.assertRaisesRegex(ValueError, "started after its measurement"): self.check()

    def test_public_observations_are_inside_the_issued_execution_and_ordered(self):
        f = self.fixture
        original = copy.deepcopy(f.functional)
        for field in ("observedAtUtc", "measuredAtUtc"):
            for value in (None, "1900-01-01T00:00:00Z", "2999-01-01T00:00:00Z", "2026-11-05T10:00:10Z",
                          "2026-11-05T10:00:01Z", "2026-11-05T10:00:05", "not-a-time"):
                with self.subTest(field=field, value=value):
                    f.functional = copy.deepcopy(original)
                    target = f.functional if field == "observedAtUtc" else f.functional["publicRuntime"]
                    target[field] = value
                    f.seal()
                    with self.assertRaises(ValueError): self.check()
        f.functional = copy.deepcopy(original)
        f.functional["publicRuntime"]["measuredAtUtc"] = "2026-11-05T10:00:06.000001Z"
        f.seal()
        with self.assertRaises(ValueError): self.check()
        f.functional = original
        f.seal()
        self.check()


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

    def test_native_sdk_bytes_cannot_replace_running_image_bytes(self):
        f = self.fixture
        for name, digest in public.SDK.items():
            with self.subTest(name=name):
                self.assertNotEqual(digest, public.IMAGE_SDK[name])
                original = f.functional["publicRuntime"]["bodies"][name]["sha256"]
                f.functional["publicRuntime"]["bodies"][name]["sha256"] = digest
                f.seal()
                with self.assertRaises(ValueError): self.check()
                f.functional["publicRuntime"]["bodies"][name]["sha256"] = original

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
        phase = json.loads((f.root / "cleanup-observations.json").read_text())
        for mutation in ("unknown", "leftover"):
            changed = copy.deepcopy(phase)
            if mutation == "unknown": changed["remainingResources"]["unverified"] = True
            if mutation == "leftover": changed["remainingResources"]["containerIds"] = ["e" * 64]
            f.write("cleanup-observations.json", changed)
            with self.subTest(mutation=mutation), self.assertRaises(ValueError): self.check()
        f.write("cleanup-observations.json", phase)
        f.result["terminal"]["outcome"] = "cancelled"
        f.seal()
        with self.assertRaises(ValueError): self.check()





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

    def test_foreign_argument_tuple_and_runtime_profile_are_refused(self):
        for field in ("platform", "builds"):
            with self.subTest(field=field), self.assertRaises(ValueError):
                public.validate(self.fixture.root, **{**self.expected, field: "f" * 40})
        self.fixture.functional["profile"] = "platform-current-package"
        self.fixture.seal()
        with self.assertRaises(ValueError): self.check()


if __name__ == "__main__":
    unittest.main()
