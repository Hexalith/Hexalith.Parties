#!/usr/bin/env python3
"""Require this run's real current-owner native, persistence and cleanup evidence."""
from __future__ import annotations

import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
from decimal import Decimal
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import xml.etree.ElementTree as ET

PROFILE = "platform-current-package"
CASES = {
    "Hexalith.Parties.Platform.IntegrationTests.PartyEnvironmentTests.RealDependencies_CreateQueryAndPersistMatchingPartyEvents",
    "Hexalith.Parties.Platform.IntegrationTests.PartyEnvironmentTests.GatewayRefusesAnonymousCommandStatusAsync",
}
SDK_VERSION = "4.2.0"
SDK = {
    "Hexalith.EventStore.Contracts": "7e4c8823479a6c59456c5169c95d9082ccbb5d199f403f7cb7c3267c9cf9a44a",
    "Hexalith.EventStore.ServiceDefaults": "3b618387d1adc3b98cef9823d629edace174c2bc9a8272a501e979f972fcad12",
    "Hexalith.EventStore.Client": "5b184b6c7bc7e83251d4f48edfb6ee446a28ac4fae7c04c381c388ed8cb9cd92",
    "Hexalith.EventStore.DomainService": "d1593c5a2b93e0a6cb8d84d946a94223403bf38c02bb7e30987f1f188ff13646",
}
PUBLIC_PARTIES_CONTRACTS = "99230b764de6769a803702170c174f9dbef9b0b5b0c2931b07e85045478cd654"
HEX = re.compile(r"[a-f0-9]{64}\Z", re.ASCII)
IDENTITY = re.compile(r"[a-f0-9]{32}\Z", re.ASCII)
TRX_ID = re.compile(r"[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}\Z", re.ASCII)
PERSISTENCE_CASE = "Hexalith.Parties.Platform.IntegrationTests.PartyEnvironmentTests.RealDependencies_CreateQueryAndPersistMatchingPartyEvents"
# Required keys observed in the current-source hexalith.test-result.v1 schema (Builds 539b10ab, sha256 below). The producer, Builds
# TestResultWriter, validates the whole document against that schema before publishing it, so these readers need no
# schema validator: this stdlib guard only detects producer drift without adding a CI dependency. A test binds
# RESULT_SCHEMA_BUILDS_COMMIT to the committed Builds gitlink, so a Builds bump fails until these keys are re-derived.
RESULT_SCHEMA_BUILDS_COMMIT = "539b10ab0f884888ae4aeba0edb829dd59f707d6"
RESULT_SCHEMA_SHA256 = "9773678febac38025fae837154341d9036ce8aa94e66b0d30c7e15ee668d7e39"
RESULT_REQUIRED = frozenset({"schema", "policyId", "bindings", "diagnostics", "terminal", "termination", "retention", "cleanup"})
RESULT_BINDINGS_REQUIRED = frozenset({"run", "tool", "candidate", "decision", "reuseLineage", "environment", "artifacts", "suite",
                                      "attachments", "deadlines"})


def require(value: bool, message: str) -> None:
    if not value:
        raise ValueError(message)


def unique(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "duplicate JSON key")
        result[key] = value
    return result


def decode(body: bytes):
    return json.loads(body, object_pairs_hook=unique, parse_constant=lambda _: (_ for _ in ()).throw(ValueError("nonfinite JSON")))


@contextmanager
def held_directory(root: Path):
    path = root.absolute()
    require(".." not in path.parts, "evidence path cannot traverse parents")
    flags = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC
    descriptor = os.open(path.anchor, flags)
    try:
        for component in path.parts[1:]:
            child = os.open(component, flags, dir_fd=descriptor)
            os.close(descriptor)
            descriptor = child
        require(stat.S_ISDIR(os.fstat(descriptor).st_mode), "invalid evidence directory")
        yield descriptor
    except OSError as error:
        raise ValueError("evidence directory cannot follow links or unavailable paths") from error
    finally:
        os.close(descriptor)


def read(root: Path | int, name: str, limit: int = 4194304) -> bytes:
    require(re.fullmatch(r"[A-Za-z0-9_.-]+", name, re.ASCII) is not None and name not in (".", ".."), "unsafe evidence filename")
    if isinstance(root, Path):
        with held_directory(root) as descriptor:
            return read(descriptor, name, limit)
    descriptor = None
    try:
        descriptor = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC, dir_fd=root)
        before = os.fstat(descriptor)
        require(stat.S_ISREG(before.st_mode) and 0 < before.st_size <= limit, "invalid evidence file")
        body = bytearray()
        while len(body) <= limit:
            chunk = os.read(descriptor, min(65536, limit + 1 - len(body)))
            if not chunk:
                break
            body.extend(chunk)
        after = os.fstat(descriptor)
        require(all(getattr(before, key) == getattr(after, key) for key in ("st_dev", "st_ino", "st_mode", "st_size", "st_mtime_ns", "st_ctime_ns"))
                and len(body) == before.st_size, "evidence changed while reading")
        return bytes(body)
    except OSError as error:
        raise ValueError("evidence must be a bounded regular file without links") from error
    finally:
        if descriptor is not None:
            os.close(descriptor)


def creation_timestamp(value):
    require(isinstance(value, str) and re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})", value) is not None,
            "creation timestamp must be a present offset timestamp")
    require(datetime.fromisoformat(value).utcoffset() is not None, "creation timestamp requires an offset")
    return value


def observation_timestamp(value):
    """Compare offset timestamps exactly, retaining .NET ticks and Docker nanoseconds."""
    match = re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.(\d{1,9}))?(?:Z|[+-]\d{2}:\d{2})", value) if isinstance(value, str) else None
    require(match is not None, "observation timestamp requires an offset")
    instant = datetime.fromisoformat(value)
    seconds = ((instant.toordinal() * 24 + instant.hour) * 60 + instant.minute) * 60 + instant.second
    return (seconds - int(instant.utcoffset().total_seconds())) * 1_000_000_000 + int((match[1] or "0").ljust(9, "0"))


def native_execution(report, cases, result, functional):
    """Bind real reporter identities and observations to the runner's issued execution interval."""
    deadlines = result["bindings"].get("deadlines")
    require(isinstance(deadlines, dict), "issued execution timing missing")
    execution = deadlines.get("execution")
    require(isinstance(execution, dict), "issued execution timing missing")
    require(execution.get("state") in ("populated", "not-declared")
            and execution.get("origin") == "test-execution-started", "issued execution timing missing")
    issued = observation_timestamp(execution.get("originAt"))
    terminal = observation_timestamp(result["terminal"].get("recordedAt"))
    require(issued <= terminal <= observation_timestamp(datetime.now(timezone.utc).isoformat()), "invalid issued execution interval")
    deadline = terminal
    if execution["state"] == "populated":
        deadline = observation_timestamp(execution.get("expiresAt"))
        seconds = execution.get("effectiveSeconds")
        require(type(seconds) in (int, float) and Decimal(str(seconds)).is_finite()
                and Decimal(str(seconds)) > 0 and Decimal(str(seconds)) * 1_000_000_000 == deadline - issued,
                "issued execution deadline is inconsistent")
    else:
        require(not {"expiresAt", "effectiveSeconds", "configurationContentIdentity"} & execution.keys(),
                "undeclared execution has deadline fields")
    require(report.tag.rsplit("}", 1)[-1] == "TestRun" and TRX_ID.fullmatch(report.get("id", "")) is not None,
            "native report execution identity missing")
    times = [item for item in report if item.tag.rsplit("}", 1)[-1] == "Times"]
    require(len(times) == 1, "native report timing missing or ambiguous")
    creation, queuing, start, finish = (observation_timestamp(times[0].get(key)) for key in ("creation", "queuing", "start", "finish"))
    require(issued <= creation <= queuing <= start <= finish <= terminal, "native report lies outside issued execution")
    require(finish <= deadline, "native execution exceeded its issued deadline")
    definitions = [item for item in report.iter() if item.tag.rsplit("}", 1)[-1] == "UnitTest"]
    require(len(definitions) == len(cases), "native execution definitions differ")
    bindings = {}
    for definition in definitions:
        identifier = definition.get("id", "")
        executions = [item for item in definition if item.tag.rsplit("}", 1)[-1] == "Execution"]
        require(TRX_ID.fullmatch(identifier) is not None and identifier not in bindings and len(executions) == 1,
                "native execution definitions missing or duplicated")
        bindings[identifier] = (definition.get("name"), executions[0].get("id"))
    seen = set()
    persistence = []
    for case in cases:
        identifier, execution_id = case.get("testId", ""), case.get("executionId", "")
        require(TRX_ID.fullmatch(execution_id) is not None and execution_id not in seen
                and bindings.pop(identifier, None) == (case.get("testName"), execution_id), "native case execution identity differs")
        seen.add(execution_id)
        case_start, case_end = (observation_timestamp(case.get(key)) for key in ("startTime", "endTime"))
        require(start <= case_start <= case_end <= finish, "native case lies outside report execution")
        if case.get("testName") == PERSISTENCE_CASE:
            persistence.append((case_start, case_end))
    require(len(persistence) == 1, "native persistence execution missing or duplicated")
    observed = observation_timestamp(functional.get("observedAtUtc"))
    require(persistence[0][0] <= observed <= persistence[0][1], "functional observation lies outside native persistence execution")
    return persistence[0][0], observed


def document(root: Path, name: str):
    return decode(read(root, name))


EXECUTION_SCHEMA = "hexalith.module.ci-integration-execution.v1"
CLEANUP_SCHEMA = "hexalith.module.ci-integration-cleanup.v1"
CLEANUP_RECORD = re.compile(r"cleanup-([1-9][0-9]?)\.json\Z", re.ASCII)


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


def result_contract(result):
    """Return the bindings of a supported result that carries every released top-level and bindings key."""
    require(isinstance(result, dict) and result.get("schema") == "hexalith.test-result.v1"
            and result.get("policyId") == "hexalith.platform.candidate-and-attachment-policy.v1", "unsupported result contract")
    require(RESULT_REQUIRED <= result.keys(), "result lacks a released contract field")
    bindings = result["bindings"]
    require(isinstance(bindings, dict) and RESULT_BINDINGS_REQUIRED <= bindings.keys(), "result bindings lack a released contract field")
    return bindings


def issued_synthetic_tenant(bindings):
    """Read the canonical synthetic tenant issued by the owning environment."""
    require(isinstance(bindings, dict), "issued synthetic tenant bindings missing")
    environment = bindings.get("environment")
    require(isinstance(environment, dict), "issued synthetic tenant environment missing")
    isolation = environment.get("isolation")
    require(isinstance(isolation, dict) and isolation.get("state") == "populated", "issued synthetic tenant isolation missing")
    tenant = isolation.get("syntheticTenantId")
    require(isinstance(tenant, str) and len(tenant) <= 64
            and re.fullmatch(r"g4t-[a-z0-9](?:[a-z0-9-]*[a-z0-9])?", tenant, re.ASCII) is not None,
            "invalid issued synthetic tenant")
    return tenant


def persisted_identity(aggregate):
    """Derive the complete current SDK AggregateIdentity JSON contract from its canonical core tuple."""
    require(isinstance(aggregate, dict) and set(aggregate) == {"tenantId", "domain", "aggregateId"}, "invalid aggregate identity tuple")
    tenant, domain, identifier = (aggregate[key] for key in ("tenantId", "domain", "aggregateId"))
    require(all(isinstance(value, str) and len(value) <= 64
                and re.fullmatch(r"[a-z0-9](?:[a-z0-9-]*[a-z0-9])?", value, re.ASCII) is not None
                for value in (tenant, domain)), "invalid canonical tenant/domain")
    require(isinstance(identifier, str) and len(identifier) <= 256
            and re.fullmatch(r"[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?", identifier, re.ASCII) is not None,
            "invalid canonical aggregate identifier")
    actor = f"{tenant}:{domain}:{identifier}"
    return {**aggregate, "actorId": actor, "eventStreamKeyPrefix": actor + ":events:",
            "metadataKey": actor + ":metadata", "snapshotKey": actor + ":snapshot",
            "pipelineKeyPrefix": actor + ":pipeline:",
            "pubSubTopic": (domain if tenant == "system" else tenant + "." + domain) + ".events",
            "queueSession": actor}


def validate(directory: Path, *, repository: str, run: str, attempt: int, source: str, platform: str, builds: str) -> dict:
    with held_directory(directory) as root:
        return validate_held(root, repository=repository, run=run, attempt=attempt, source=source, platform=platform, builds=builds)


def validate_held(root: int, *, repository: str, run: str, attempt: int, source: str, platform: str, builds: str) -> dict:
    context = {"kind": "ci", "repository": repository, "workflowRunId": run, "workflowAttempt": attempt,
               "jobId": "build-and-test", "runnerKind": "github-hosted", "operatingSystem": "Linux", "disposable": True}
    invocation = document(root, "invocation.json")
    require(invocation_accepted(invocation, context, PROFILE), "foreign invocation/profile")
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
    execution, final_cleanup, phase_names = integration_phases(root, context, owner["runId"], invocation)
    for phase in (execution, final_cleanup):
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
    current = functional["currentOwner"]
    for key in ("runId", "attemptId", "environmentId", "revision", "mappingHash", "profile"):
        require(current.get(key) == functional[key], "current owner lineage mismatch")
    require(current.get("configuration") == "Release" and current.get("qualificationCredit") is False and current.get("committedCatalogValidated") is True
            and current.get("sdkVersion") == SDK_VERSION and current.get("tenantsVersion") == "7.0.5", "wrong current owner cohort")
    identity = current["platform"]
    require(all(identity.get(key) == platform for key in ("commit", "compositionCommit", "checkoutCommit")) and identity.get("catalogCommit") == builds, "foreign Platform/catalog identity")
    require(current.get("sdkBodies") == SDK, "actual runtime SDK bodies differ")
    archives = current.get("packageArchives")
    require(isinstance(archives, dict) and set(archives) == {"Hexalith.Parties.Contracts.3.1.0.nupkg", "Hexalith.Parties.EventStoreExtensions.3.1.0.nupkg"}
            and all(isinstance(value, str) and HEX.fullmatch(value) for value in archives.values()), "private source packages were not measured")
    runtime, catalog = current["runtimeBodies"], current["catalogBodies"]
    require(set(catalog) == {"Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions"}
            and all(isinstance(body, str) and HEX.fullmatch(body) for body in (*runtime.values(), *catalog.values())), "incomplete measured owner bodies")
    require(runtime.get("Hexalith.Parties.Contracts") == catalog["Hexalith.Parties.Contracts"] != PUBLIC_PARTIES_CONTRACTS, "stale or swapped current catalog/runtime body")
    require(set(runtime) == {"Hexalith.Parties", "Hexalith.Parties.Contracts", "Hexalith.Parties.Projections", "Hexalith.Parties.Authentication", "Hexalith.Parties.Security"}, "incomplete running owner closure")
    executable = [value for key, value in current["observedExecutableOwners"].items() if key.split(",", 1)[0] == "Hexalith.Parties"]
    require(executable == [runtime["Hexalith.Parties"]] and HEX.fullmatch(current.get("routingCatalogRoot", "")) is not None, "actual committed executable/route proof absent")
    loaded_name = "loaded-assemblies-" + owner["runId"] + ".json"
    loaded = document(root, loaded_name)
    require(loaded.get("kind") == "real-native-sdk-assemblies" and loaded.get("profile") == PROFILE
            and loaded.get("ownerRunId") == owner["runId"] and loaded.get("ownerAttemptId") == owner["attemptId"]
            and loaded.get("environmentId") == owner["environmentId"] and loaded.get("revision") == source
            and loaded.get("mappingHash") == functional["mappingHash"] and loaded.get("executionContext") == context, "native source observation differs")
    for field in ("loaded", "copied"):
        require(isinstance(loaded.get(field), list) and 0 < len(loaded[field]) <= 64, "native assembly inventory is empty or oversized")
        require(all(isinstance(item, dict) and item.get("scope") == ("loaded-native-test-process" if field == "loaded" else "copied-native-composition-output")
                    for item in loaded[field]), "native assembly role differs")
        identities = [item["identity"].split(",", 1)[0] for item in loaded[field]]
        require(len(identities) == len(set(identities)), "duplicate observed assemblies")
        for name, expected in SDK.items():
            observed = [item for item in loaded[field] if item["identity"].split(",", 1)[0] == name]
            require(len(observed) == 1 if field == "copied" or name == "Hexalith.EventStore.Contracts" else len(observed) <= 1, "native/composition SDK inventory missing")
            require(all(item.get("sha256") == expected and f", Version={SDK_VERSION}.0," in item["identity"] for item in observed), "native/composition SDK substitution")
    allowed = names | phase_names | {loaded_name, "invocation.json", "test-result.json", "attachments.json", "attachment-withdrawals.json"}
    require(all(name in allowed and stat.S_ISREG(os.stat(name, dir_fd=root, follow_symlinks=False).st_mode) for name in os.listdir(root)), "unreferenced or duplicate artifact substitute")
    return {"profile": PROFILE, "source": source, "runId": owner["runId"], "nativeCases": len(cases), "qualificationCredit": False}


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
    main()
