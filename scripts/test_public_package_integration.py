"""Exercise the hosted adapter against the product evidence fixture."""
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
        hold_cases = (
            "AfterPersistenceIsNoOpUnlessExplicitlyRequestedAsync",
            "IsRequestedWhenControllerWaitControlExists",
            "ObservePublishesMarkerThenCompletesOnPassAsync",
        )
        inventory = (*fixtures.XUNIT_403_TRX_CASE_NAMES,
                     *("Hexalith.Parties.Platform.IntegrationTests.CiLifecycleHoldTests." + name for name in hold_cases))
        fixture.native = fixtures.private_fixture.native_report(inventory)
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
        self.assertEqual(gate.validate(self.fixture.root, **self.expected)["nativeCases"], 31)

    def test_missing_lifecycle_controls_are_refused(self):
        self.fixture.native = fixtures.private_fixture.native_report(fixtures.XUNIT_403_TRX_CASE_NAMES)
        self.fixture.seal()
        with self.assertRaisesRegex(ValueError, "exact native case inventory differs"):
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
        self.assertEqual(gate.validate(self.fixture.root, **self.expected)["nativeCases"], 31)

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
