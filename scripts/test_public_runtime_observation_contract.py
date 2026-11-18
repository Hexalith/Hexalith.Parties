"""Synthetic artifact/byte controls only; no GitHub identity, Docker, native run or qualification credit."""
import copy
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


# Literal xUnit v3 MTP TRX names, written independently of the validator's name sets with the quoting
# captured from xUnit 4.0.3 reports; the hosted adapter test adds the attached lifecycle hold controls.
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
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.HeldIntegrationInvocationRoutesToTheLifecycleJournal(held: False)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.HeldIntegrationInvocationRoutesToTheLifecycleJournal(held: True)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationRequiresTheDeclaredIntegrationProfileOnce(profile: \"platform-package\", profiles: \"lifecycle-package\", accepted: False)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationRequiresTheDeclaredIntegrationProfileOnce(profile: \"platform-package\", profiles: \"platform-package,lifecycle-package\", accepted: True)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationRequiresTheDeclaredIntegrationProfileOnce(profile: \"platform-package\", profiles: \"platform-package,platform-package\", accepted: False)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationRequiresTheDeclaredIntegrationProfileOnce(profile: \"platform-package\", profiles: \"platform-package,platform-source\", accepted: False)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationRequiresTheDeclaredIntegrationProfileOnce(profile: \"platform-package\", profiles: \"platform-package\", accepted: True)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationRequiresTheDeclaredIntegrationProfileOnce(profile: \"platform-package\", profiles: \"unknown-package,platform-package\", accepted: False)',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"changed-context\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"changed-mapping\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"foreign-attempt\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"foreign-model-output\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"foreign-run\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"held-platform-package\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"lifecycle-foreign-receipt\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"lifecycle-foreign-source\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"lifecycle-local\")',
    r'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \"valid-local\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PartyEnvironmentTests.GatewayRefusesAnonymousCommandStatusAsync',
    r'Hexalith.Parties.Platform.IntegrationTests.PartyEnvironmentTests.RealDependencies_CreateQueryAndPersistMatchingPartyEvents',
    r'Hexalith.Parties.Platform.IntegrationTests.PublicRuntimeImageObservationsTests.ReadBindsResolvedDependencyImagesToThisOwner(mutation: \"accepted-immutable-config\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PublicRuntimeImageObservationsTests.ReadBindsResolvedDependencyImagesToThisOwner(mutation: \"accepted-tagged-manifest\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PublicRuntimeImageObservationsTests.ReadBindsResolvedDependencyImagesToThisOwner(mutation: \"duplicate-resource\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PublicRuntimeImageObservationsTests.ReadBindsResolvedDependencyImagesToThisOwner(mutation: \"empty-images\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PublicRuntimeImageObservationsTests.ReadBindsResolvedDependencyImagesToThisOwner(mutation: \"foreign-attempt\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PublicRuntimeImageObservationsTests.ReadBindsResolvedDependencyImagesToThisOwner(mutation: \"foreign-environment\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PublicRuntimeImageObservationsTests.ReadBindsResolvedDependencyImagesToThisOwner(mutation: \"foreign-mapping\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PublicRuntimeImageObservationsTests.ReadBindsResolvedDependencyImagesToThisOwner(mutation: \"foreign-repository\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PublicRuntimeImageObservationsTests.ReadBindsResolvedDependencyImagesToThisOwner(mutation: \"foreign-run\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PublicRuntimeImageObservationsTests.ReadBindsResolvedDependencyImagesToThisOwner(mutation: \"platform-source\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PublicRuntimeImageObservationsTests.ReadBindsResolvedDependencyImagesToThisOwner(mutation: \"qualification-credit\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PublicRuntimeImageObservationsTests.ReadBindsResolvedDependencyImagesToThisOwner(mutation: \"substituted-image-id\")',
    r'Hexalith.Parties.Platform.IntegrationTests.PublicRuntimeImageObservationsTests.ReadBindsResolvedDependencyImagesToThisOwner(mutation: \"substituted-reference\")',
)


class PublicPackageEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.fixture = private_fixture.CurrentOwnerEvidenceTests("test_current_same_run_evidence_passes_without_qualification_credit")
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        f = self.fixture
        self.expected = {**f.expected, "release_tag": "v1.9.0", "native_output": Path("/product/Release/native-tests")}
        f.functional.update(profile=public.PROFILE, selectorCompatibilityObserved=True)
        f.loaded["profile"] = public.PROFILE
        # The native test process loads its caller-source DTO; the composition output carries its consumed Contracts.
        directory = str(self.expected["native_output"]) + "/"
        dto = dict(identity="Hexalith.Parties.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", sha256="4" * 64,
                   informationalVersion="1.0.0+" + self.expected["source"], loadedPath=directory + "Hexalith.Parties.Contracts.dll")
        f.loaded["loaded"] += [dto, dict(identity="Hexalith.Parties.Platform.IntegrationTests, Version=1.0.0.0", sha256="5" * 64,
                                         informationalVersion=dto["informationalVersion"], loadedPath=directory + "Hexalith.Parties.Platform.IntegrationTests.dll")]
        f.loaded["copied"].append(dict(identity="Hexalith.Parties.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", sha256="6" * 64,
                                       informationalVersion="1.0.0+" + "9" * 40, copiedPath="/product/workspaces/run/hosts/platform-model/Hexalith.Parties.Contracts.dll"))
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
        self.assertEqual(result["nativeCases"], 43)
        self.assertEqual(result["profile"], "platform-package")
        self.assertEqual(result["readerRoles"], dict(loadedContracts="caller-source-test-dto", copiedContracts="composition-output"))
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
        self.assertEqual(self.check()["nativeCases"], 43)

    def test_native_times_and_execution_identities_are_bound_after_resealing(self):
        private_fixture.check_native_refusals(self, self.fixture, self.check)

    def test_precise_execution_boundaries_deadline_shapes_and_report_processing(self):
        private_fixture.check_execution_boundaries(self, self.fixture, self.check)

    def test_functional_observation_is_inside_the_issued_execution(self):
        f = self.fixture
        original = copy.deepcopy(f.functional)
        for value in (None, "1900-01-01T00:00:00Z", "2999-01-01T00:00:00Z", "2026-11-05T10:00:10Z",
                      "2026-11-05T10:00:01Z", "2026-11-05T10:00:05", "not-a-time"):
            with self.subTest(value=value):
                f.functional = copy.deepcopy(original)
                f.functional["observedAtUtc"] = value
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

    def test_ordinary_evidence_requires_the_measured_release_owner(self):
        for value in (None, {"profile": "platform-current-package"}):
            with self.subTest(currentOwner=value):
                original = copy.deepcopy(self.fixture.functional)
                self.fixture.functional["currentOwner"] = value
                self.fixture.seal()
                with self.assertRaisesRegex(ValueError, "Release owner observation is absent|current owner lineage"): self.check()
                self.fixture.functional = original
        self.fixture.seal()
        self.check()

    def test_missing_or_false_selector_compatibility_observation_refuses(self):
        for value in (None, False, "true", 1):
            with self.subTest(value=value):
                if value is None:
                    self.fixture.functional.pop("selectorCompatibilityObserved", None)
                else:
                    self.fixture.functional["selectorCompatibilityObserved"] = value
                self.fixture.seal()
                with self.assertRaisesRegex(ValueError, "selector compatibility"): self.check()

    def test_substituted_owner_bodies_refuse_after_resealing(self):
        f = self.fixture
        baseline = copy.deepcopy(f.functional)
        for group, name, message in (("runtimeBodies", "Hexalith.Parties.Contracts", "stale or swapped"),
                                     ("catalogBodies", "Hexalith.Parties.Contracts", "stale or swapped"),
                                     ("runtimeBodies", "Hexalith.Parties", "executable/route proof"),
                                     ("runtimeBodies", "Hexalith.Parties.Security", "owner bodies"),
                                     ("sdkBodies", "Hexalith.EventStore.Client", "SDK substitution"),
                                     ("sdkBodies", "Hexalith.EventStore.Contracts", "SDK substitution")):
            with self.subTest(group=group, name=name):
                f.functional = copy.deepcopy(baseline)
                f.functional["currentOwner"][group][name] = "a" * 64 if group != "runtimeBodies" or name != "Hexalith.Parties.Security" else "substituted"
                f.seal()
                with self.assertRaisesRegex(ValueError, message): self.check()
        f.functional = baseline
        f.seal()
        # The native inventory carries the owner's SDK bodies: a coherent substitution in both still refuses at the other reader.
        for field in ("loaded", "copied"):
            item = next(item for item in f.loaded[field] if item["identity"].startswith("Hexalith.EventStore.Contracts,"))
            with self.subTest(inventory=field):
                saved = item["sha256"]
                item["sha256"] = "a" * 64
                f.write("loaded-assemblies-" + f.owner["runId"] + ".json", f.loaded)
                with self.assertRaisesRegex(ValueError, "SDK substitution"): self.check()
                item["sha256"] = saved
        f.write("loaded-assemblies-" + f.owner["runId"] + ".json", f.loaded)
        self.check()

    def test_foreign_owner_run_source_mapping_or_platform_refuse(self):
        f = self.fixture
        baseline = copy.deepcopy(f.functional)
        for field in ("profile", "runId", "attemptId", "environmentId", "revision", "mappingHash"):
            with self.subTest(field=field):
                f.functional = copy.deepcopy(baseline)
                f.functional["currentOwner"][field] = "x"
                f.seal()
                with self.assertRaisesRegex(ValueError, "lineage"): self.check()
        for field in ("commit", "compositionCommit", "checkoutCommit", "catalogCommit", "releaseTag"):
            with self.subTest(platformField=field):
                f.functional = copy.deepcopy(baseline)
                f.functional["currentOwner"]["platform"][field] = "f" * 40 if field != "releaseTag" else "v1.8.0"
                f.seal()
                with self.assertRaisesRegex(ValueError, "foreign Platform/catalog"): self.check()
        # A mapping without a local release tag still binds the selected Platform and catalog commits.
        f.functional = copy.deepcopy(baseline)
        f.functional["currentOwner"]["platform"]["releaseTag"] = None
        f.seal()
        self.check()
        f.functional = baseline
        f.seal()

    def test_tampered_dependency_images_refuse(self):
        private_fixture.check_dependency_image_refusals(self, self.fixture, self.check)

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
        f.write("unreferenced-runtime.json", f.functional["currentOwner"])
        with self.assertRaisesRegex(ValueError, "unreferenced"): self.check()

    def test_relabelled_loaded_or_copied_scopes_refuse(self):
        f = self.fixture
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
