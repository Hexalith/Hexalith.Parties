#!/usr/bin/env python3
"""Product observations for public image bodies, package readers and persisted native effects.

The published compatibility cohort is measured independently of native readers.
No CI/controller receipt layout or fixed workflow/job identity is consumed.
"""
from __future__ import annotations

import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import stat
import xml.etree.ElementTree as ET

# Share the bounded-file, JSON and identity readers with the current-owner validator.
_spec = importlib.util.spec_from_file_location("private_current_readers", Path(__file__).with_name("runtime_observation_contract.py"))
_readers = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_readers)
require, held_directory, read, decode, document = (_readers.require, _readers.held_directory, _readers.read, _readers.decode, _readers.document)
complete_cleanup, expected_runtime_inputs = _readers.complete_cleanup, _readers.expected_runtime_inputs
persisted_identity, creation_timestamp = _readers.persisted_identity, _readers.creation_timestamp
issued_synthetic_tenant, result_contract = _readers.issued_synthetic_tenant, _readers.result_contract
native_execution, observation_timestamp = _readers.native_execution, _readers.observation_timestamp
SDK, SDK_VERSION, HEX, IDENTITY = _readers.SDK, _readers.SDK_VERSION, _readers.HEX, _readers.IDENTITY
PROFILE = "platform-package"
IMAGE = "registry.hexalith.com/parties@sha256:48d0e8db506e4ce574e8c7bd97e528a302d3fbf97754b007176904cbc1f3abfb"
PACKAGE_READERS = {
    "Hexalith.Parties.Contracts": "99230b764de6769a803702170c174f9dbef9b0b5b0c2931b07e85045478cd654",
    "Hexalith.Parties.EventStoreExtensions": "a48612597603ce5b8d743e28e91236a51e561369c6bcd2d9fcca092dd78f1c06",
}
# The released image retains its independently measured SDK 4.1 bodies. Native
# readers use SDK 4.2 and must never substitute their bytes for running image files.
IMAGE_SDK = {
    "Hexalith.EventStore.Contracts": "f3f2d7f6e7edf54e187af15edec8b4f13bf7e255b186b999efa0836a0c86a739",
    "Hexalith.EventStore.ServiceDefaults": "0e3f88ffefb12327d53f865b96bbb774beb71f7c78c9f20a3cbefa4901e9ba2d",
    "Hexalith.EventStore.Client": "702dfb7537e990a2065fe37c0b06301cdb70c26907af42faa6acdae331a7bce6",
    "Hexalith.EventStore.DomainService": "5d8068c8c116551efc3d545f85d41040023fa38217e5b4e73e4d225fe8e8bf78",
}
IMAGE_BODIES = {**IMAGE_SDK, "Hexalith.Parties.Contracts": "b2ddfb18a7719b171ab9cf8b846675c4556d4d683ba0b5ebe8a503a6ae73d2f6"}
IMAGE_SIZES = {"Hexalith.Parties.Contracts": 304640, "Hexalith.EventStore.Contracts": 953344,
               "Hexalith.EventStore.Client": 1288192, "Hexalith.EventStore.DomainService": 1247744,
               "Hexalith.EventStore.ServiceDefaults": 568320}
# Require persistence and anonymous access checks; other native controls run in their declared suites.
CASES = _readers.CASES


def public_reader_roles(observations: dict, *, source: str, native_output: Path) -> dict:
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
                    path = item.get("loadedPath", "")
                    expected_path = native_output / (contracts + ".dll")
                    require(native_output.is_absolute() and all(part not in (".", "..") for part in native_output.parts)
                            and not any(ord(char) < 32 for char in str(native_output)), "expected native output path differs")
                    require(isinstance(path, str) and path == str(expected_path), "caller-source DTO output path differs")
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


def validate(directory: Path, *, context: dict, source: str, platform: str, builds: str, native_output: Path) -> dict:
    expected_runtime_inputs(context, source, platform, builds)
    with held_directory(directory) as root:
        return validate_held(root, context=context, source=source, platform=platform, builds=builds, native_output=native_output)


def validate_held(root: int, *, context: dict, source: str, platform: str, builds: str, native_output: Path) -> dict:
    expected_runtime_inputs(context, source, platform, builds)
    result = document(root, "test-result.json")
    bindings = result_contract(result)
    synthetic_tenant = issued_synthetic_tenant(bindings)
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
    complete_cleanup(root, owner["runId"])
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
    require(isinstance(events, list) and len(events) > 0
            and type(functional.get("eventCount")) is int and type(functional.get("metadataSequence")) is int
            and functional["eventCount"] == len(events) == functional["metadataSequence"], "persisted sequence mismatch")
    require(aggregate["tenantId"] == synthetic_tenant and aggregate["domain"] == "party", "persisted aggregate differs from issued synthetic tenant")
    require(functional["projection"].get("id") == aggregate["aggregateId"] and functional["projection"].get("freshness") in ("current", "Current"), "projection differs from persisted aggregate")
    for sequence, event in enumerate(events, 1):
        require(event.get("identity") == expected_identity and type(event.get("sequenceNumber")) is int and event["sequenceNumber"] == sequence
                and event.get("correlationId") == functional["commandId"] == event.get("causationId")
                and event.get("userId") == "g4-native-tests" and event.get("serializationFormat") == "json", "persisted event identity mismatch")
    created = [event for event in events if event.get("eventTypeName") == "Hexalith.Parties.Contracts.Events.PartyCreated"]
    require(len(created) == 1 and creation_timestamp(created[0].get("timestamp")) == creation_timestamp(functional["projection"].get("createdAt")), "PartyCreated/projection timestamp mismatch")
    require(functional.get("tenantsNativeQueryObserved") is True and functional.get("memoriesRegistryObserved") is True, "real reference-owner assertions absent")
    require(functional.get("selectorCompatibilityObserved") is True, "SDK selector compatibility assertions absent")
    persistence_start, observed = native_execution(report, cases, result, functional)
    require("currentOwner" in functional and functional["currentOwner"] is None, "ordinary evidence cannot promote a private current-owner proof")
    runtime = functional.get("publicRuntime")
    require(isinstance(runtime, dict) and set(runtime) == {"kind", "profile", "ownerRunId", "ownerAttemptId", "environmentId", "revision", "mappingHash", "executionContext", "platform", "image", "bodies", "measuredAtUtc", "qualificationCredit"}, "measured ordinary runtime evidence missing or unknown")
    require(runtime["kind"] == "running-owned-public-parties-image" and runtime["qualificationCredit"] is False, "wrong runtime observation role")
    for field, expected in (("profile", PROFILE), ("ownerRunId", owner["runId"]), ("ownerAttemptId", owner["attemptId"]),
                            ("environmentId", owner["environmentId"]), ("revision", source), ("mappingHash", functional["mappingHash"]), ("executionContext", context)):
        require(runtime.get(field) == expected, "running image owner/source/job differs")
    require(runtime["platform"] == {"commit": platform, "compositionCommit": platform, "checkoutCommit": platform,
                                   "catalogCommit": builds, "releaseTag": "v1.9.0"}, "running image public Platform/catalog differs")
    image = runtime["image"]
    require(isinstance(image, dict) and set(image) == {"containerId", "runId", "imageReference", "imageContentId", "running", "startedAt"}, "unknown running image identity")
    require(HEX.fullmatch(image.get("containerId", "")) is not None and image.get("runId") == owner["runId"]
            and image.get("imageReference") == IMAGE and image.get("running") is True
            and re.fullmatch(r"sha256:[a-f0-9]{64}", image.get("imageContentId", ""), re.ASCII) is not None, "foreign, mutable or stopped public image")
    require(isinstance(image.get("startedAt"), str) and re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,9})?Z", image["startedAt"], re.ASCII) is not None,
            "running image start timestamp is unavailable")
    measured = observation_timestamp(runtime["measuredAtUtc"])
    require(persistence_start <= measured <= observed, "running image measurement lies outside observed execution")
    require(observation_timestamp(image["startedAt"]) <= measured, "running image started after its measurement")
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
            require(all(item.get("sha256") == digest and f", Version={SDK_VERSION}.0," in item["identity"] for item in observed), "native/composition SDK substitution")
    reader_roles = public_reader_roles(loaded, source=source, native_output=native_output)
    allowed = names | {loaded_name, "cleanup-observations.json", "test-result.json", "attachments.json", "attachment-withdrawals.json"}
    require(all(name in allowed and stat.S_ISREG(os.stat(name, dir_fd=root, follow_symlinks=False).st_mode) for name in os.listdir(root)), "unreferenced or duplicate artifact substitute")
    return {"profile": PROFILE, "source": source, "runId": owner["runId"], "nativeCases": len(cases), "readerRoles": reader_roles, "qualificationCredit": False}
