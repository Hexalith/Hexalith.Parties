#!/usr/bin/env python3
"""Validate this run's ordinary public Parties native effects, measured image roles and exact cleanup."""
from __future__ import annotations

import argparse
from datetime import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import stat
import sys
import xml.etree.ElementTree as ET

# Reuse the existing safe bounded-file/JSON/identity readers. The private validator and every private predicate remain unchanged.
_spec = importlib.util.spec_from_file_location("private_current_readers", Path(__file__).with_name("verify-current-owner-integration.py"))
_readers = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_readers)
require, held_directory, read, decode, document = (_readers.require, _readers.held_directory, _readers.read, _readers.decode, _readers.document)
persisted_identity, creation_timestamp = _readers.persisted_identity, _readers.creation_timestamp
SDK, HEX, IDENTITY = _readers.SDK, _readers.HEX, _readers.IDENTITY
PROFILE = "platform-package"
PLATFORM = "0f4e27f1f78310bd731c10c19dc51bb1e0345d44"
BUILDS = "3c8096180ecf2aa539ac454815efbfceb8a92822"
IMAGE = "registry.hexalith.com/parties@sha256:48d0e8db506e4ce574e8c7bd97e528a302d3fbf97754b007176904cbc1f3abfb"
PACKAGE_READERS = {
    "Hexalith.Parties.Contracts": "99230b764de6769a803702170c174f9dbef9b0b5b0c2931b07e85045478cd654",
    "Hexalith.Parties.EventStoreExtensions": "a48612597603ce5b8d743e28e91236a51e561369c6bcd2d9fcca092dd78f1c06",
}
IMAGE_BODIES = {**SDK, "Hexalith.Parties.Contracts": "b2ddfb18a7719b171ab9cf8b846675c4556d4d683ba0b5ebe8a503a6ae73d2f6"}
IMAGE_SIZES = {"Hexalith.Parties.Contracts": 304640, "Hexalith.EventStore.Contracts": 953344,
               "Hexalith.EventStore.Client": 1288192, "Hexalith.EventStore.DomainService": 1247744,
               "Hexalith.EventStore.ServiceDefaults": 568320}
# Actual8f names are a measured name baseline only. Its old private result grants no new public native credit.
CASES = frozenset(['Hexalith.Parties.Platform.IntegrationTests.FunctionalObservationWriterTests.DuplicateCanonicalWriteRefusesWhileRecheckPreservesOriginalBytesAsync', 'Hexalith.Parties.Platform.IntegrationTests.FunctionalObservationWriterTests.RecheckRequiresOriginalAndPreservesOutputOwnershipBoundsAsync', 'Hexalith.Parties.Platform.IntegrationTests.MemoriesTenantObservationTests.ActiveCurrentTenantPasses', 'Hexalith.Parties.Platform.IntegrationTests.MemoriesTenantObservationTests.InactiveCurrentTenantFails(status: "deleting")', 'Hexalith.Parties.Platform.IntegrationTests.MemoriesTenantObservationTests.InactiveCurrentTenantFails(status: "failed")', 'Hexalith.Parties.Platform.IntegrationTests.MemoriesTenantObservationTests.InactiveCurrentTenantFails(status: "provisioning")', 'Hexalith.Parties.Platform.IntegrationTests.NativeAssemblyObservationTests.CopiedOutputRefusesLinkedAssembly', 'Hexalith.Parties.Platform.IntegrationTests.NativeAssemblyObservationTests.CopiedOutputReportsActualFileHashWithoutLoadingIt', 'Hexalith.Parties.Platform.IntegrationTests.NativeAssemblyObservationTests.PrivateDiagnosticRefusesSymbolicDirectory', 'Hexalith.Parties.Platform.IntegrationTests.NativeAssemblyObservationTests.PrivateDiagnosticRetainsFirstBytesAndRefusesOverwrite', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationPreservesOrdinaryBatchesAndRefusesMixedCurrentPurpose(profile: "platform-current-package", profiles: "platform-current-package", accepted: True)', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationPreservesOrdinaryBatchesAndRefusesMixedCurrentPurpose(profile: "platform-current-package", profiles: "platform-current-package,platform-current-package", accepted: False)', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationPreservesOrdinaryBatchesAndRefusesMixedCurrentPurpose(profile: "platform-current-package", profiles: "platform-current-package,platform-package", accepted: False)', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationPreservesOrdinaryBatchesAndRefusesMixedCurrentPurpose(profile: "platform-package", profiles: "lifecycle-package", accepted: False)', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationPreservesOrdinaryBatchesAndRefusesMixedCurrentPurpose(profile: "platform-package", profiles: "platform-package,lifecycle-package", accepted: True)', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.InvocationPreservesOrdinaryBatchesAndRefusesMixedCurrentPurpose(profile: "platform-package", profiles: "platform-package,platform-current-package", accepted: False)', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: "changed-context")', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: "changed-mapping")', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: "current-valid-local")', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: "foreign-attempt")', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: "foreign-model-output")', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: "foreign-run")', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: "lifecycle-foreign-receipt")', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: "lifecycle-foreign-source")', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: "lifecycle-local")', 'Hexalith.Parties.Platform.IntegrationTests.NativeDiagnosticBindingTests.PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(mutation: "valid-local")', 'Hexalith.Parties.Platform.IntegrationTests.PartyEnvironmentTests.GatewayRefusesAnonymousCommandStatusAsync', 'Hexalith.Parties.Platform.IntegrationTests.PartyEnvironmentTests.RealDependencies_CreateQueryAndPersistMatchingPartyEvents'])


def public_reader_roles(observations: dict, *, source: str, run_id: str) -> dict:
    """Keep the owner test DTO separate from the model's consumed public package."""
    contracts = "Hexalith.Parties.Contracts"
    loaded_origin = "public-nuget"
    for field in ("loaded", "copied"):
        for name, digest in PACKAGE_READERS.items():
            observed = [item for item in observations[field] if item["identity"].split(",", 1)[0] == name]
            # The published composition consumes Contracts, not EventStoreExtensions.
            require(len(observed) == 1 if name == contracts else len(observed) <= 1, "public package reader inventory missing")
            for item in observed:
                if name == contracts and field == "loaded" and item.get("sha256") != digest:
                    # Genuine module preparation always builds its active owner from source.
                    # This is test-side DTO provenance; it never admits a public package body.
                    require(item.get("sha256") not in set(IMAGE_BODIES.values()) | set(PACKAGE_READERS.values()),
                            "image/private bytes cannot substitute for a public NuGet reader")
                    test_name = "Hexalith.Parties.Platform.IntegrationTests"
                    tests = [entry for entry in observations[field] if entry["identity"].split(",", 1)[0] == test_name]
                    suffix = "/g4-workspaces/" + run_id + "/test-builds/bin/" + test_name + "/release/"
                    path = item.get("loadedPath", "")
                    require(isinstance(path, str) and path.startswith("/") and suffix in path
                            and path.endswith(suffix + contracts + ".dll")
                            and all(part not in (".", "..") for part in path.split("/"))
                            and not any(ord(char) < 32 for char in path), "caller-source DTO output path differs")
                    require(item["identity"] == contracts + ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
                            and item.get("informationalVersion") == "1.0.0+" + source
                            and HEX.fullmatch(item.get("sha256", "")) is not None,
                            "caller-source DTO provenance differs; it is not a public NuGet reader")
                    require(len(tests) == 1 and tests[0].get("informationalVersion") == item["informationalVersion"]
                            and tests[0].get("loadedPath") == path.removesuffix(contracts + ".dll") + test_name + ".dll",
                            "caller-source DTO and native test source/output differ")
                    loaded_origin = "caller-source-test-dto"
                else:
                    require(item.get("sha256") == digest, "image/private bytes cannot substitute for a public NuGet reader")
                    if name == contracts:
                        require(item["identity"] == contracts + ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
                                and item.get("informationalVersion") == "1.0.0+50904cc4ba53c9b0b2c082d5872a0d276be8f5c3",
                                "public NuGet Contracts assembly metadata differs")
    return {"loadedContracts": loaded_origin, "copiedContracts": "public-nuget"}


def validate(directory: Path, *, repository: str, run: str, attempt: int, source: str, platform: str, builds: str) -> dict:
    require(platform == PLATFORM and builds == BUILDS, "ordinary public gate requires the actual published Platform and Builds sources")
    with held_directory(directory) as root:
        return validate_held(root, repository=repository, run=run, attempt=attempt, source=source, platform=platform, builds=builds)


def validate_held(root: int, *, repository: str, run: str, attempt: int, source: str, platform: str, builds: str) -> dict:
    context = {"kind": "ci", "repository": repository, "workflowRunId": run, "workflowAttempt": attempt,
               "jobId": "build-and-test", "runnerKind": "github-hosted", "operatingSystem": "Linux", "disposable": True}
    invocation = document(root, "invocation.json")
    require(invocation == {"classification": "baseline-only", "executionContext": context, "profiles": [PROFILE]}, "foreign invocation/profile")
    result = document(root, "test-result.json")
    require(result.get("schema") == "hexalith.test-result.v1" and result.get("policyId") == "hexalith.platform.candidate-and-attachment-policy.v1", "unsupported result contract")
    bindings = result["bindings"]
    owner = bindings["run"]
    for key in ("runId", "attemptId", "environmentId"):
        require(isinstance(owner.get(key), str) and IDENTITY.fullmatch(owner[key]) is not None, "invalid owner identity")
    require(len({owner[key] for key in ("runId", "attemptId", "environmentId")}) == 3 and owner.get("ownerRunId") == owner["runId"] and owner.get("role") == "owner", "foreign owner role")
    require(bindings["environment"].get("mode") == "package" and bindings["environment"].get("executionContext") == context, "foreign result context")
    require(bindings["candidate"].get("classification") == "baseline-only", "fabricated candidate credit")
    require(result["terminal"].get("outcome") == "passed" and result["terminal"].get("cause") == "tests-completed", "native execution did not pass")
    require(result.get("termination", {}).get("state") == "not-yet-populated", "interrupted native execution")
    cleanup = result["cleanup"]
    require(cleanup.get("state") == "populated" and cleanup.get("disposition") == "complete" and cleanup.get("failures") == [] and cleanup.get("leftoverResourceIds") == [], "owned result cleanup incomplete")
    for filename in ("execution.json", "cleanup.json"):
        phase = document(root, filename)
        remaining = phase.get("remainingResources", {})
        require(phase.get("runId") == owner["runId"] and phase.get("outcome", {}).get("exitCode") == "Success", "execution/cleanup failed or foreign")
        require(remaining.get("unverified") is False and remaining.get("isEmpty") is True
                and all(remaining.get(key) == [] for key in ("containerIds", "processIds", "workspaceIds", "claimIds", "discoveryFailures", "resourceIds")), "owned resources remain")
    diagnostics = result.get("diagnostics")
    require(isinstance(diagnostics, list) and 2 <= len(diagnostics) <= 3, "mandatory native/functional evidence missing")
    selected = {}
    names = set()
    for reference in diagnostics:
        require(set(reference) == {"id", "classification", "redacted", "reference", "contentIdentity"}, "invalid diagnostic shape")
        identity = reference["id"]
        require(identity in ("native-report", "functional-observations", "lifecycle") and identity not in selected, "duplicate or unsupported diagnostic")
        prefix, suffix = {"native-report": ("native", "trx"), "functional-observations": ("functional-observations", "json"), "lifecycle": ("lifecycle", "json")}[identity]
        filename = reference["reference"]
        match = re.fullmatch(prefix + r"-([a-f0-9]{64})\." + suffix, filename, re.ASCII) if isinstance(filename, str) else None
        require(match is not None and reference["classification"] == "internal" and reference["redacted"] is True
                and reference["contentIdentity"] == "sha256:" + match[1] and filename not in names, "invalid diagnostic reference")
        body = read(root, filename, 4194304 if identity == "native-report" else 1048576)
        require(hashlib.sha256(body).hexdigest() == match[1], "changed diagnostic content")
        selected[identity] = body
        names.add(filename)
    require({"native-report", "functional-observations"} <= selected.keys(), "mandatory native/functional evidence absent")
    native = selected["native-report"]
    require(b"<!DOCTYPE" not in native.upper() and b"<!ENTITY" not in native.upper(), "unsafe native XML")
    report = ET.fromstring(native)
    cases = [item for item in report.iter() if item.tag.rsplit("}", 1)[-1] == "UnitTestResult"]
    counters = [item for item in report.iter() if item.tag.rsplit("}", 1)[-1] == "Counters"]
    require(len(counters) == 1 and len(cases) > 0, "empty or ambiguous native report")
    count = counters[0].attrib
    require(all(item.get("outcome") == "Passed" for item in cases), "failed or skipped native case")
    require(all(int(count.get(key, "-1")) == len(cases) for key in ("total", "executed", "passed"))
            and all(int(count.get(key, "0")) == 0 for key in ("failed", "error", "timeout", "aborted", "notExecuted", "notRunnable", "disconnected", "inconclusive")), "inconsistent native counters")
    require(len(cases) == len(CASES) and len({item.get("testName") for item in cases}) == len(CASES), "original28 native set differs")
    for required in CASES:
        require(sum(item.get("testName") == required for item in cases) == 1, "required unchanged native case missing or duplicated")
    functional = decode(selected["functional-observations"])
    require(functional.get("status") == "functional-observations-passed" and functional.get("qualificationCredit") is False, "functional evidence did not pass")
    for key in ("runId", "attemptId", "environmentId"):
        require(functional.get(key) == owner[key], "functional owner mismatch")
    require(functional.get("profile") == PROFILE and functional.get("revision") == source and HEX.fullmatch(functional.get("mappingHash", "")) is not None
            and functional.get("executionContext") == context, "functional source/profile/context mismatch")
    aggregate = functional["aggregate"]
    expected_identity = persisted_identity(aggregate)
    events = functional["events"]
    require(isinstance(events, list) and len(events) > 0 and functional.get("eventCount") == len(events) == functional.get("metadataSequence"), "persisted sequence mismatch")
    require(aggregate.get("tenantId", "").startswith("g4t-") and aggregate.get("domain") == "party" and aggregate.get("aggregateId"), "foreign persisted aggregate")
    require(functional["projection"].get("id") == aggregate["aggregateId"] and functional["projection"].get("freshness") in ("current", "Current"), "projection differs from persisted aggregate")
    for sequence, event in enumerate(events, 1):
        require(event.get("identity") == expected_identity and event.get("sequenceNumber") == sequence
                and event.get("correlationId") == functional["commandId"] == event.get("causationId")
                and event.get("userId") == "g4-native-tests" and event.get("serializationFormat") == "json", "persisted event identity mismatch")
    created = [event for event in events if event.get("eventTypeName") == "Hexalith.Parties.Contracts.Events.PartyCreated"]
    require(len(created) == 1 and creation_timestamp(created[0].get("timestamp")) == creation_timestamp(functional["projection"].get("createdAt")), "PartyCreated/projection timestamp mismatch")
    require(functional.get("tenantsNativeQueryObserved") is True and functional.get("memoriesRegistryObserved") is True, "real reference-owner assertions absent")
    require("currentOwner" in functional and functional["currentOwner"] is None, "ordinary evidence cannot promote a private current-owner proof")
    runtime = functional.get("publicRuntime")
    require(isinstance(runtime, dict) and set(runtime) == {"kind", "profile", "ownerRunId", "ownerAttemptId", "environmentId", "revision", "mappingHash", "executionContext", "platform", "image", "bodies", "measuredAtUtc", "qualificationCredit"}, "measured ordinary runtime evidence missing or unknown")
    require(runtime["kind"] == "running-owned-public-parties-image" and runtime["qualificationCredit"] is False, "wrong runtime observation role")
    for field, expected in (("profile", PROFILE), ("ownerRunId", owner["runId"]), ("ownerAttemptId", owner["attemptId"]),
                            ("environmentId", owner["environmentId"]), ("revision", source), ("mappingHash", functional["mappingHash"]), ("executionContext", context)):
        require(runtime.get(field) == expected, "running image owner/source/job differs")
    require(runtime["platform"] == {"commit": platform, "compositionCommit": platform, "checkoutCommit": platform,
                                   "catalogCommit": builds, "releaseTag": "v1.7.1"}, "running image public Platform/catalog differs")
    image = runtime["image"]
    require(isinstance(image, dict) and set(image) == {"containerId", "runId", "imageReference", "imageContentId", "running", "startedAt"}, "unknown running image identity")
    require(HEX.fullmatch(image.get("containerId", "")) is not None and image.get("runId") == owner["runId"]
            and image.get("imageReference") == IMAGE and image.get("running") is True
            and re.fullmatch(r"sha256:[a-f0-9]{64}", image.get("imageContentId", ""), re.ASCII) is not None, "foreign, mutable or stopped public image")
    require(isinstance(image.get("startedAt"), str) and re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,9})?Z", image["startedAt"], re.ASCII) is not None,
            "running image start timestamp is unavailable")
    measured = datetime.fromisoformat(creation_timestamp(runtime["measuredAtUtc"]))
    require(datetime.fromisoformat(image["startedAt"]) <= measured, "running image started after its measurement")
    bodies = runtime["bodies"]
    require(isinstance(bodies, dict) and set(bodies) == set(IMAGE_BODIES), "running image body set is incomplete or unknown")
    for name, digest in IMAGE_BODIES.items():
        require(isinstance(bodies[name], dict) and type(bodies[name].get("bytes")) is int, "running image byte count must be an integer")
        require(bodies[name] == {"role": "running-public-image-file", "path": "/app/" + name + ".dll", "bytes": IMAGE_SIZES[name], "sha256": digest},
                "actual running image body differs from its measured public role")
    loaded_name = "loaded-assemblies-" + owner["runId"] + ".json"
    loaded = document(root, loaded_name)
    require(loaded.get("kind") == "real-native-sdk-assemblies" and loaded.get("profile") == PROFILE
            and loaded.get("ownerRunId") == owner["runId"] and loaded.get("ownerAttemptId") == owner["attemptId"]
            and loaded.get("environmentId") == owner["environmentId"] and loaded.get("revision") == source
            and loaded.get("mappingHash") == functional["mappingHash"] and loaded.get("executionContext") == context, "native source observation differs")
    for field in ("loaded", "copied"):
        require(isinstance(loaded.get(field), list) and 0 < len(loaded[field]) <= 64, "native assembly inventory is empty or oversized")
        require(all(item.get("scope") == ("loaded-native-test-process" if field == "loaded" else "copied-native-composition-output") for item in loaded[field]), "native assembly role differs")
        identities = [item["identity"].split(",", 1)[0] for item in loaded[field]]
        require(len(identities) == len(set(identities)), "duplicate observed assemblies")
        for name, digest in SDK.items():
            observed = [item for item in loaded[field] if item["identity"].split(",", 1)[0] == name]
            require(len(observed) == 1 if field == "copied" or name == "Hexalith.EventStore.Contracts" else len(observed) <= 1, "native/composition SDK inventory missing")
            require(all(item.get("sha256") == digest and ", Version=4.1.0.0," in item["identity"] for item in observed), "native/composition SDK substitution")
    reader_roles = public_reader_roles(loaded, source=source, run_id=owner["runId"])
    allowed = names | {loaded_name, "invocation.json", "execution.json", "cleanup.json", "test-result.json", "attachments.json", "attachment-withdrawals.json"}
    require(all(name in allowed and stat.S_ISREG(os.stat(name, dir_fd=root, follow_symlinks=False).st_mode) for name in os.listdir(root)), "unreferenced or duplicate artifact substitute")
    return {"profile": PROFILE, "source": source, "runId": owner["runId"], "nativeCases": len(cases), "readerRoles": reader_roles, "qualificationCredit": False}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    for field in ("source", "platform", "builds"):
        parser.add_argument("--" + field, required=True)
    args = parser.parse_args()
    for value in (args.source, args.platform, args.builds):
        require(re.fullmatch(r"[a-f0-9]{40}", value, re.ASCII) is not None, "require exact committed identities")
    receipt = validate(args.directory, repository=os.environ["GITHUB_REPOSITORY"], run=os.environ["GITHUB_RUN_ID"],
                       attempt=int(os.environ["GITHUB_RUN_ATTEMPT"]), source=args.source, platform=args.platform, builds=args.builds)
    print(json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, TypeError) as error:
        print("Public integration evidence refused: " + type(error).__name__, file=sys.stderr)
        raise SystemExit(1)
