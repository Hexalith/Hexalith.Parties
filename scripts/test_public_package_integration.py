"""Exercise the hosted adapter against current owner evidence."""
import copy
import hashlib
import json
from pathlib import Path
import unittest

import test_public_runtime_observation_contract as fixtures


gate = fixtures.load("public_integration_gate", "verify-public-package-integration.py")


class PublicPackageIntegrationTests(unittest.TestCase):
    def setUp(self):
        product = fixtures.PublicPackageEvidenceTests("test_separate_public_roles_accept_a_complete_native_report_as_a_unit_fixture_only")
        product.setUp()
        self.addCleanup(product.doCleanups)
        self.fixture = product.fixture
        self.expected = product.expected.copy()
        self.expected.pop("native_output")
        self.expected["workspaces"] = Path("/product/workspaces")
        fixture = self.fixture
        fixture.root.joinpath("cleanup-observations.json").unlink()
        owner_fixture = fixtures.private_fixture.CurrentOwnerEvidenceTests("test_current_same_run_evidence_passes_without_qualification_credit")
        self.addCleanup(owner_fixture.doCleanups)
        owner_fixture.setUp()
        owner = owner_fixture.functional["currentOwner"]
        historical = fixture.functional.pop("publicRuntime")
        owner.update(profile=gate.public.PROFILE, tenantsVersion="7.0.7", memoriesVersion="3.0.27",
                     platform={**historical["platform"], "isPlatformRepository": False})
        images = []
        for index, resource in enumerate(("tenants", "tenants-api", "memories", "memories-domain-service",
                                          "memories-access-telemetry-clock", "memories-access-telemetry"), 1):
            version = "7.0.7" if resource.startswith("tenants") else "3.0.27"
            repository = f"registry.hexalith.com/{resource}"
            manifest, config = "sha256:" + "b" * 64, "sha256:" + "c" * 64
            images.append(dict(resourceId=resource, catalogImage=f"{repository}:{version}",
                               expectedReference=f"{repository}@{manifest}", expectedManifestDigest=manifest,
                               expectedConfigDigest=config, observedReference=f"{repository}@{manifest}",
                               observedImageId=config, containerId=f"{index:064x}"))
        owner["dependencyImages"] = copy.deepcopy(images)
        fixture.functional.update(currentOwner=owner, dependencyImages=images)
        hold_cases = (
            "AfterPersistenceIsNoOpUnlessExplicitlyRequestedAsync",
            "IsRequestedWhenControllerWaitControlExists",
            "ObservePublishesMarkerThenCompletesOnPassAsync",
        )
        suite = "Hexalith.Parties.Platform.IntegrationTests."
        inventory = [name for name in fixtures.XUNIT_403_TRX_CASE_NAMES
                     if "InvocationPreservesOrdinaryBatches" not in name and "current-valid-local" not in name]
        inventory.extend(suite + "CiLifecycleHoldTests." + name for name in hold_cases)
        for profiles, accepted in (("platform-package,lifecycle-package", True), ("lifecycle-package", False),
                                   ("platform-package", True), ("unknown-package,platform-package", False),
                                   ("platform-package,platform-source", False), ("platform-package,platform-package", False)):
            inventory.append(suite + r'NativeDiagnosticBindingTests.InvocationRequiresTheDeclaredIntegrationProfileOnce(profile: \"platform-package\", '
                             + rf'profiles: \"{profiles}\", accepted: {accepted})')
        self.inventory = inventory
        fixture.native = fixtures.private_fixture.native_report(self.inventory)
        fixture.seal()
        self.context = fixture.context
        self.run_id = fixture.owner["runId"]
        remaining = dict(unverified=False, isEmpty=True, containerIds=[], processIds=[], workspaceIds=[],
                         claimIds=[], discoveryFailures=[], resourceIds=[])
        self.result = dict(runId=self.run_id, outcome={"exitCode": "Success"}, remainingResources=remaining)
        fixture.write("invocation.json", dict(classification="baseline-only", executionContext=self.context,
                                             profiles=[gate.public.PROFILE], timeoutSeconds=300))
        self.execution = dict(schema=gate.EXECUTION_SCHEMA, executionContext=self.context, processExitCode=0,
                              outputParsed=True, launched=True, signals=[], cancelled=False, timedOut=False,
                              result=self.result)
        fixture.write("execution.json", self.execution)
        self.cleanup = dict(schema=gate.CLEANUP_SCHEMA, executionContext=self.context, runId=self.run_id,
                            status="completed", verified=True, processExitCode=0, result=self.result)
        fixture.write("cleanup-1.json", self.cleanup)

    def test_owner_and_current_job_pass_through_the_real_public_validator(self):
        self.assertEqual(gate.validate(self.fixture.root, **self.expected)["nativeCases"], 30)

    def test_missing_lifecycle_controls_are_refused(self):
        self.fixture.native = fixtures.private_fixture.native_report([name for name in self.inventory if "CiLifecycleHoldTests" not in name])
        self.fixture.seal()
        with self.assertRaisesRegex(ValueError, "exact native case inventory differs"):
            gate.validate(self.fixture.root, **self.expected)

    def test_foreign_owner_revision_is_refused(self):
        self.fixture.functional["currentOwner"]["revision"] = "9" * 40
        self.fixture.seal()
        with self.assertRaisesRegex(ValueError, "owner lineage differs"):
            gate.validate(self.fixture.root, **self.expected)

    def test_historical_runtime_cannot_replace_current_owner(self):
        self.fixture.functional["publicRuntime"] = {}
        self.fixture.seal()
        with self.assertRaisesRegex(ValueError, "historical image evidence"):
            gate.validate(self.fixture.root, **self.expected)

    def test_substituted_image_is_refused_even_in_both_receipts(self):
        functional = self.fixture.functional
        for images in (functional["dependencyImages"], functional["currentOwner"]["dependencyImages"]):
            images[0]["observedImageId"] = "sha256:" + "0" * 64
        self.fixture.seal()
        with self.assertRaisesRegex(ValueError, "dependency image identity differs"):
            gate.validate(self.fixture.root, **self.expected)

    def test_process_that_never_launched_is_refused(self):
        self.fixture.write("execution.json", {**self.execution, "launched": False})
        with self.assertRaisesRegex(ValueError, "execution was not a verified completion"):
            gate.validate(self.fixture.root, **self.expected)

    def add_post_test_termination(self, observed_at):
        results = self.fixture.result
        def entry(prefix):
            return dict(id=prefix + ":" + self.run_id, status="terminated", observedAt=observed_at,
                        diagnosticIds=["termination"])
        work, access = [entry("native"), entry("environment")], [entry("access")]
        results["termination"] = dict(state="populated", ownedWork=work, environmentAccess=access,
                                      leftoverWorkIds=[], failures=[])
        def recorded(rows):
            return [dict(Id=row["id"], Status=row["status"], ObservedAtUtc=row["observedAt"]) for row in rows]
        diagnostic = json.dumps(dict(runId=self.run_id, ownedWork=recorded(work), environmentAccess=recorded(access),
                                     leftoverWorkIds=[])).encode()
        digest = hashlib.sha256(diagnostic).hexdigest()
        name = f"termination-{digest}.json"
        (self.fixture.root / name).write_bytes(diagnostic)
        results["diagnostics"].append(dict(id="termination", classification="internal", redacted=True,
                                           reference=name, contentIdentity="sha256:" + digest))
        self.fixture.write("test-result.json", results)

    def test_termination_after_native_success_is_bound_to_the_final_down(self):
        self.add_post_test_termination("2026-11-05T10:00:09.5Z")
        self.assertEqual(gate.validate(self.fixture.root, **self.expected)["nativeCases"], 30)

    def test_termination_before_native_success_is_refused(self):
        self.add_post_test_termination("2026-11-05T10:00:08Z")
        with self.assertRaisesRegex(ValueError, "post-test termination is out of order"):
            gate.validate(self.fixture.root, **self.expected)

    def test_foreign_final_cleanup_is_refused(self):
        self.fixture.write("cleanup-2.json", {**self.cleanup, "runId": "d" * 32})
        with self.assertRaisesRegex(ValueError, "another job or owner"):
            gate.validate(self.fixture.root, **self.expected)

    def test_unverified_final_cleanup_is_refused(self):
        self.fixture.write("cleanup-2.json", {**self.cleanup, "verified": False})
        with self.assertRaisesRegex(ValueError, "exact down was not verified"):
            gate.validate(self.fixture.root, **self.expected)


def load_tests(loader, tests, pattern):
    if pattern == "test_*_integration.py":
        import test_public_reader_roles
        import test_runtime_observation_contract

        for module in (fixtures, test_public_reader_roles, test_runtime_observation_contract):
            tests.addTests(loader.loadTestsFromModule(module))
    return tests


if __name__ == "__main__":
    unittest.main()
