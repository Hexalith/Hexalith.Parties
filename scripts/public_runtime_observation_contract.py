#!/usr/bin/env python3
"""Product observations for the platform-package Release owner, its launched images and native effects.

Platform builds the mapped Parties owner in Release and launches dependency images by the
digest it resolved from their catalog tags. Owner, SDK and image identities are read from
that evidence and cross-checked; no image or package body is pinned here. No CI/controller
receipt layout or fixed workflow/job identity is consumed.
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

# Share the bounded-file, JSON, identity and Release-owner readers with the helper-less validator.
_spec = importlib.util.spec_from_file_location("private_current_readers", Path(__file__).with_name("runtime_observation_contract.py"))
_readers = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_readers)
require, held_directory, read, decode, document = (_readers.require, _readers.held_directory, _readers.read, _readers.decode, _readers.document)
complete_cleanup, expected_runtime_inputs = _readers.complete_cleanup, _readers.expected_runtime_inputs
persisted_identity, creation_timestamp = _readers.persisted_identity, _readers.creation_timestamp
issued_synthetic_tenant, result_contract = _readers.issued_synthetic_tenant, _readers.result_contract
native_execution, observation_timestamp = _readers.native_execution, _readers.observation_timestamp
current_owner, native_inventory, hex_digest = _readers.current_owner, _readers.native_inventory, _readers.hex_digest
HEX, IDENTITY, PROFILE = _readers.HEX, _readers.IDENTITY, _readers.PROFILE
# Require persistence and anonymous access checks; other native controls run in their declared suites.
CASES = _readers.CASES


def public_reader_roles(observations: dict, *, source: str, native_output: Path) -> dict:
    """Keep the native test's caller-source DTO separate from the Contracts the composition consumed."""
    contracts, test_name = "Hexalith.Parties.Contracts", "Hexalith.Parties.Platform.IntegrationTests"

    def named(field, name):
        return [item for item in observations[field] if item["identity"].split(",", 1)[0] == name]

    copied = named("copied", contracts)
    # The published composition consumes Contracts, not EventStoreExtensions; its bytes are measured, not pinned.
    require(len(copied) == 1 and hex_digest(copied[0].get("sha256")), "composition Contracts inventory missing")
    loaded = named("loaded", contracts)
    require(len(loaded) == 1, "caller-source DTO inventory missing")
    item = loaded[0]
    tests = named("loaded", test_name)
    path = item.get("loadedPath", "")
    expected_path = native_output / (contracts + ".dll")
    require(native_output.is_absolute() and all(part not in (".", "..") for part in native_output.parts)
            and not any(ord(char) < 32 for char in str(native_output)), "expected native output path differs")
    require(isinstance(path, str) and path == str(expected_path), "caller-source DTO output path differs")
    require(item["identity"] == contracts + ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
            and item.get("informationalVersion") == "1.0.0+" + source
            and hex_digest(item.get("sha256")), "caller-source DTO provenance differs")
    require(len(tests) == 1 and tests[0].get("informationalVersion") == item["informationalVersion"]
            and tests[0].get("loadedPath") == path.removesuffix(contracts + ".dll") + test_name + ".dll",
            "caller-source DTO and native test source/output differ")
    return {"loadedContracts": "caller-source-test-dto", "copiedContracts": "composition-output"}


def validate(directory: Path, *, context: dict, source: str, platform: str, builds: str, release_tag: str, native_output: Path) -> dict:
    expected_runtime_inputs(context, source, platform, builds)
    with held_directory(directory) as root:
        return validate_held(root, context=context, source=source, platform=platform, builds=builds, release_tag=release_tag, native_output=native_output)


def verify_cleanup_termination(result, owner_id, diagnostic):
    """Require exact post-test stops of the native work, the environment and its access."""
    termination = result["termination"]
    require(termination.get("state") == "populated" and termination.get("leftoverWorkIds") == []
            and termination.get("failures") == [] and diagnostic.get("runId") == owner_id
            and diagnostic.get("leftoverWorkIds") == [], "incomplete or foreign post-test termination")
    passed_at = observation_timestamp(result["terminal"]["recordedAt"])
    cleaned_at = observation_timestamp(result["cleanup"]["observedAt"])
    for group, prefixes in (("ownedWork", {"native", "environment"}), ("environmentAccess", {"access"})):
        work, evidence = termination.get(group), diagnostic.get(group)
        require(isinstance(work, list) and isinstance(evidence, list) and len(work) == len(evidence) == len(prefixes),
                "incomplete post-test termination inventory")
        expected_ids = {prefix + ":" + owner_id for prefix in prefixes}
        required = {(entry["id"], entry["status"], observation_timestamp(entry["observedAt"])) for entry in work}
        actual = {(entry["Id"], entry["Status"], observation_timestamp(entry["ObservedAtUtc"])) for entry in evidence}
        require({entry[0] for entry in required} == expected_ids and required == actual
                and all(status == "terminated" and passed_at <= time <= cleaned_at for _, status, time in required)
                and all(entry.get("diagnosticIds") == ["termination"] for entry in work),
                "post-test termination is out of order or differs from the owned diagnostic")


def validate_held(root: int, *, context: dict, source: str, platform: str, builds: str, release_tag: str, native_output: Path, helper_files: frozenset[str] | None = None,
                  required_cases: frozenset[str] | None = None) -> dict:
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
    cleanup = result["cleanup"]
    require(cleanup.get("state") == "populated" and cleanup.get("disposition") == "complete" and cleanup.get("failures") == [] and cleanup.get("leftoverResourceIds") == [], "owned result cleanup incomplete")
    if helper_files is None:
        complete_cleanup(root, owner["runId"])
    diagnostics = result.get("diagnostics")
    require(isinstance(diagnostics, list) and 2 <= len(diagnostics) <= 5, "mandatory native/functional evidence missing")
    selected = {}
    names = set()
    for reference in diagnostics:
        require(set(reference) == {"id", "classification", "redacted", "reference", "contentIdentity"}, "invalid diagnostic shape")
        identity = reference["id"]
        require(identity in ("native-report", "functional-observations", "lifecycle", "executing-tool", "termination") and identity not in selected, "duplicate or unsupported diagnostic")
        prefix, suffix = {"native-report": ("native", "trx"), "functional-observations": ("functional-observations", "json"), "lifecycle": ("lifecycle", "json"),
                          "executing-tool": ("executing-tool", "json"), "termination": ("termination", "json")}[identity]
        filename = reference["reference"]
        match = re.fullmatch(prefix + r"-([a-f0-9]{64})\." + suffix, filename, re.ASCII) if isinstance(filename, str) else None
        require(match is not None and reference["classification"] == "internal" and reference["redacted"] is True
                and reference["contentIdentity"] == "sha256:" + match[1] and filename not in names, "invalid diagnostic reference")
        body = read(root, filename, 4194304 if identity == "native-report" else 1048576)
        require(hashlib.sha256(body).hexdigest() == match[1], "changed diagnostic content")
        if identity == "executing-tool":
            tool = bindings["tool"]
            require(tool.get("state") == "populated" and tool.get("contentIdentity") == reference["contentIdentity"],
                    "executing tool binding differs")
        if identity in ("lifecycle", "termination") and helper_files is not None:
            require(decode(body).get("runId") == owner["runId"], "foreign runner diagnostic")
        selected[identity] = body
        names.add(filename)
    require({"native-report", "functional-observations"} <= selected.keys(), "mandatory native/functional evidence absent")
    if result["termination"].get("state") == "not-yet-populated":
        require("termination" not in selected, "unexpected termination diagnostic")
    else:
        require(helper_files is not None and "termination" in selected, "unverified native termination")
        verify_cleanup_termination(result, owner["runId"], decode(selected["termination"]))
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
    if required_cases is not None:
        case_names = {item.get("testName") for item in cases}
        require(case_names == required_cases and len(cases) == len(required_cases), "exact native case inventory differs")
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
    native_execution(report, cases, result, functional)
    current = current_owner(functional, platform=platform, builds=builds, release_tag=release_tag)
    loaded_name = "loaded-assemblies-" + owner["runId"] + ".json"
    loaded = document(root, loaded_name)
    native_inventory(loaded, owner=owner, source=source, context=context, mapping_hash=functional["mappingHash"], current=current)
    reader_roles = public_reader_roles(loaded, source=source, native_output=native_output)
    allowed = names | {loaded_name, "test-result.json", "attachments.json", "attachment-withdrawals.json"}
    allowed |= {"cleanup-observations.json"} if helper_files is None else helper_files
    require(all(name in allowed and stat.S_ISREG(os.stat(name, dir_fd=root, follow_symlinks=False).st_mode) for name in os.listdir(root)), "unreferenced or duplicate artifact substitute")
    return {"profile": PROFILE, "source": source, "runId": owner["runId"], "nativeCases": len(cases), "readerRoles": reader_roles, "qualificationCredit": False}
