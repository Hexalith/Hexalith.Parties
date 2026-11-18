#!/usr/bin/env python3
"""Bind a hosted package integration artifact to its exact CI invocation and owner cleanup."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys

import public_runtime_observation_contract as public
from runtime_observation_contract import IDENTITY, document, held_directory, require


EXECUTION_SCHEMA = "hexalith.module.ci-integration-execution.v1"
CLEANUP_SCHEMA = "hexalith.module.ci-integration-cleanup.v1"
CLEANUP_RECORD = re.compile(r"cleanup-([1-9][0-9]?)\.json\Z", re.ASCII)

# Exact xUnit TRX inventory includes the original cases and the attached lifecycle hold controls.
NATIVE_CASES = frozenset("Hexalith.Parties.Platform.IntegrationTests." + suffix for suffix in (
    'CiLifecycleHoldTests.AfterPersistenceIsNoOpUnlessExplicitlyRequestedAsync',
    'CiLifecycleHoldTests.IsRequestedWhenControllerWaitControlExists',
    'CiLifecycleHoldTests.ObservePublishesMarkerThenCompletesOnPassAsync',
    'FunctionalObservationWriterTests.DuplicateCanonicalWriteRefusesWhileRecheckPreservesOriginalBytesAsync',
    'FunctionalObservationWriterTests.RecheckRequiresOriginalAndPreservesOutputOwnershipBoundsAsync',
    'MemoriesTenantObservationTests.ActiveCurrentTenantPasses',
    'MemoriesTenantObservationTests.InactiveCurrentTenantFails(status: \\"deleting\\")',
    'MemoriesTenantObservationTests.InactiveCurrentTenantFails(status: \\"failed\\")',
    'MemoriesTenantObservationTests.InactiveCurrentTenantFails(status: \\"provisioning\\")',
    'NativeAssemblyObservationTests.CopiedOutputRefusesLinkedAssembly',
    'NativeAssemblyObservationTests.CopiedOutputReportsActualFileHashWithoutLoadingIt',
    'NativeAssemblyObservationTests.PrivateDiagnosticRefusesSymbolicDirectory',
    'NativeAssemblyObservationTests.PrivateDiagnosticRetainsFirstBytesAndRefusesOverwrite',
    'NativeDiagnosticBindingTests.InvocationRequiresTheDeclaredIntegrationProfileOnce(profile: \\"platform-package\\", profiles: \\"platform-package,lifecycle-package\\", accepted: True)',
    'NativeDiagnosticBindingTests.InvocationRequiresTheDeclaredIntegrationProfileOnce(profile: \\"platform-package\\", profiles: \\"lifecycle-package\\", accepted: False)',
    'NativeDiagnosticBindingTests.InvocationRequiresTheDeclaredIntegrationProfileOnce(profile: \\"platform-package\\", profiles: \\"platform-package\\", accepted: True)',
    'NativeDiagnosticBindingTests.InvocationRequiresTheDeclaredIntegrationProfileOnce(profile: \\"platform-package\\", profiles: \\"unknown-package,platform-package\\", accepted: False)',
    'NativeDiagnosticBindingTests.InvocationRequiresTheDeclaredIntegrationProfileOnce(profile: \\"platform-package\\", profiles: \\"platform-package,platform-source\\", accepted: False)',
    'NativeDiagnosticBindingTests.InvocationRequiresTheDeclaredIntegrationProfileOnce(profile: \\"platform-package\\", profiles: \\"platform-package,platform-package\\", accepted: False)',
    'NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \\"changed-context\\")',
    'NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \\"changed-mapping\\")',
    'NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \\"foreign-attempt\\")',
    'NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \\"foreign-model-output\\")',
    'NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \\"foreign-run\\")',
    'NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \\"lifecycle-foreign-receipt\\")',
    'NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \\"lifecycle-foreign-source\\")',
    'NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \\"lifecycle-local\\")',
    'NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: \\"valid-local\\")',
    'PartyEnvironmentTests.GatewayRefusesAnonymousCommandStatusAsync',
    'PartyEnvironmentTests.RealDependencies_CreateQueryAndPersistMatchingPartyEvents',
))


def invocation_accepted(invocation, context, profile) -> bool:
    """The pinned helper records three fields; later helpers add only the integer timeout they enforce."""
    if not isinstance(invocation, dict):
        return False
    expected = {"classification": "baseline-only", "executionContext": context, "profiles": [profile]}
    if "timeoutSeconds" not in invocation:
        return invocation == expected
    timeout = invocation["timeoutSeconds"]
    rest = {key: value for key, value in invocation.items() if key != "timeoutSeconds"}
    return type(timeout) is int and 60 <= timeout <= 86400 and rest == expected


def integer_zero(value) -> bool:
    """A process exit code counts as success only as the integer 0, never False or 0.0."""
    return type(value) is int and value == 0


def integration_phases(root, context, run_id, invocation):
    """Return the helper's execution result, its final exact-down result and the evidence names carrying them.

    The pinned Builds helper writes a three-field `invocation.json`, raw `execution.json` and `cleanup.json`.
    Later helpers add the integer `timeoutSeconds`, wrap execution with its process observation and keep one
    `cleanup-<n>.json` record per exact down. Each layout is accepted only whole: a mixed, non-contiguous,
    foreign-job, foreign-owner or unverified layout is refused.
    """
    names = set(os.listdir(root))
    execution = document(root, "execution.json")
    numbers = sorted(int(match.group(1)) for match in map(CLEANUP_RECORD.fullmatch, names) if match)
    wrapped = isinstance(execution, dict) and execution.get("schema") == EXECUTION_SCHEMA
    timed = isinstance(invocation, dict) and "timeoutSeconds" in invocation
    if not wrapped:
        require(not numbers and not timed, "mixed helper evidence layouts")
        return execution, document(root, "cleanup.json"), {"execution.json", "cleanup.json"}
    require(timed and numbers and "cleanup.json" not in names, "mixed helper evidence layouts")
    require(numbers == list(range(1, len(numbers) + 1)), "non-contiguous cleanup records")
    require(execution.get("executionContext") == context and integer_zero(execution.get("processExitCode"))
            and execution.get("launched") is True and execution.get("signals") == []
            and execution.get("outputParsed") is True and execution.get("cancelled") is False
            and execution.get("timedOut") is False and isinstance(execution.get("result"), dict),
            "integration execution was not a verified completion of this job")
    records = [document(root, f"cleanup-{number}.json") for number in numbers]
    require(all(isinstance(record, dict) and record.get("schema") == CLEANUP_SCHEMA for record in records), "unsupported cleanup record")
    require(all(record.get("executionContext") == context and record.get("runId") == run_id for record in records),
            "cleanup record belongs to another job or owner")
    final = records[-1]
    require(final.get("status") == "completed" and final.get("verified") is True and integer_zero(final.get("processExitCode"))
            and isinstance(final.get("result"), dict), "final exact down was not verified")
    return execution["result"], final["result"], {"execution.json", *(f"cleanup-{number}.json" for number in numbers)}

def platform_release(commit):
    """Use the release recorded in the committed direct Platform signoff."""
    ledger = subprocess.check_output(["git", "show", "HEAD:.gitlink-signoff.tsv"], text=True, timeout=15)
    matches = {
        fields[3].split("@")[0]
        for line in ledger.splitlines()
        if (fields := line.split("|"))[:3] == ["references/Hexalith.Platform", commit, "validated-advance"]
        and len(fields) == 6 and re.fullmatch(r"v[0-9]+\.[0-9]+\.[0-9]+@" + commit, fields[3], re.ASCII)
    }
    require(len(matches) == 1, "the selected Platform commit requires one exact released signoff")
    return matches.pop()


def validate(directory, *, context, source, platform, builds, release_tag, workspaces):
    with held_directory(directory) as root:
        invocation = document(root, "invocation.json")
        require(invocation_accepted(invocation, context, public.PROFILE), "foreign invocation/profile")
        result = document(root, "test-result.json")
        run_id = public.result_contract(result)["run"]["runId"]
        require(isinstance(run_id, str) and IDENTITY.fullmatch(run_id) is not None, "invalid owner identity")
        execution, cleanup, phase_files = integration_phases(root, context, run_id, invocation)
        for phase in (execution, cleanup):
            remaining = phase.get("remainingResources", {})
            require(phase.get("runId") == run_id and phase.get("outcome", {}).get("exitCode") == "Success",
                    "execution or cleanup failed or belongs to a foreign owner")
            require(remaining.get("unverified") is False and remaining.get("isEmpty") is True
                    and all(remaining.get(key) == [] for key in
                            ("containerIds", "processIds", "workspaceIds", "claimIds", "discoveryFailures", "resourceIds")),
                    "owned resources remain")
        native_output = workspaces / run_id / "test-builds/bin/Hexalith.Parties.Platform.IntegrationTests/release"
        return public.validate_held(root, context=context, source=source, platform=platform, builds=builds,
                                    release_tag=release_tag, native_output=native_output,
                                    helper_files=frozenset({"invocation.json", *phase_files}), required_cases=NATIVE_CASES, current_owner=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    for field in ("source", "platform", "builds"):
        parser.add_argument("--" + field, required=True)
    args = parser.parse_args()
    context = {"kind": "ci", "repository": os.environ["GITHUB_REPOSITORY"], "workflowRunId": os.environ["GITHUB_RUN_ID"],
               "workflowAttempt": int(os.environ["GITHUB_RUN_ATTEMPT"]), "jobId": "build-and-test",
               "runnerKind": "github-hosted", "operatingSystem": "Linux", "disposable": True}
    public.expected_runtime_inputs(context, args.source, args.platform, args.builds)
    receipt = validate(args.directory, context=context, source=args.source, platform=args.platform, builds=args.builds,
                       release_tag=platform_release(args.platform),
                       workspaces=Path.home() / ".local/share/hexalith-builds/g4-workspaces")
    print(json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        print("Public integration evidence refused (" + type(error).__name__ + "): " + str(error), file=sys.stderr)
        raise SystemExit(1) from None
