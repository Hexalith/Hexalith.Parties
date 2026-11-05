#!/usr/bin/env python3
"""Require this run's real current-owner native, persistence and cleanup evidence."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import xml.etree.ElementTree as ET

PROFILE = "platform-current-package"
CASES = {
    "PartyEnvironmentTests.RealDependencies_CreateQueryAndPersistMatchingPartyEvents",
    "PartyEnvironmentTests.GatewayRefusesAnonymousCommandStatusAsync",
}
SDK = {
    "Hexalith.EventStore.Contracts": "f3f2d7f6e7edf54e187af15edec8b4f13bf7e255b186b999efa0836a0c86a739",
    "Hexalith.EventStore.ServiceDefaults": "0e3f88ffefb12327d53f865b96bbb774beb71f7c78c9f20a3cbefa4901e9ba2d",
    "Hexalith.EventStore.Client": "702dfb7537e990a2065fe37c0b06301cdb70c26907af42faa6acdae331a7bce6",
    "Hexalith.EventStore.DomainService": "5d8068c8c116551efc3d545f85d41040023fa38217e5b4e73e4d225fe8e8bf78",
}
PUBLIC_PARTIES_CONTRACTS = "b10c65c9b15aaf2b07ed1cd3eececb5d8685a62d87efa1465a777adba335eff9"
HEX = re.compile(r"[a-f0-9]{64}\Z", re.ASCII)
IDENTITY = re.compile(r"[a-f0-9]{32}\Z", re.ASCII)


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


def read(root: Path, name: str, limit: int = 4194304) -> bytes:
    require(re.fullmatch(r"[A-Za-z0-9_.-]+", name, re.ASCII) is not None, "unsafe evidence filename")
    path = root / name
    require(all(not part.is_symlink() for part in (path, *path.parents)), "linked evidence path")
    with path.open("rb") as stream:
        before = os.fstat(stream.fileno())
        require(stat.S_ISREG(before.st_mode) and 0 < before.st_size <= limit, "invalid evidence file")
        body = stream.read(limit + 1)
        after = os.fstat(stream.fileno())
    require(all(getattr(before, key) == getattr(after, key) for key in ("st_dev", "st_ino", "st_mode", "st_size", "st_mtime_ns", "st_ctime_ns"))
            and len(body) == before.st_size, "evidence changed while reading")
    return body


def document(root: Path, name: str):
    return decode(read(root, name))


def validate(directory: Path, *, repository: str, run: str, attempt: int, source: str, platform: str, builds: str) -> dict:
    root = directory.absolute()
    require(root.is_dir() and not root.is_symlink(), "missing current workflow artifact")
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
    for required in CASES:
        require(sum(item.get("testName", "").endswith(required) for item in cases) == 1, "required unchanged native case missing or duplicated")
    functional = decode(selected["functional-observations"])
    require(functional.get("status") == "functional-observations-passed" and functional.get("qualificationCredit") is False, "functional evidence did not pass")
    for key in ("runId", "attemptId", "environmentId"):
        require(functional.get(key) == owner[key], "functional owner mismatch")
    require(functional.get("profile") == PROFILE and functional.get("revision") == source and HEX.fullmatch(functional.get("mappingHash", "")) is not None
            and functional.get("executionContext") == context, "functional source/profile/context mismatch")
    aggregate = functional["aggregate"]
    events = functional["events"]
    require(isinstance(events, list) and len(events) > 0 and functional.get("eventCount") == len(events) == functional.get("metadataSequence"), "persisted sequence mismatch")
    require(aggregate.get("tenantId", "").startswith("g4t-") and aggregate.get("domain") == "party" and aggregate.get("aggregateId"), "foreign persisted aggregate")
    require(functional["projection"].get("id") == aggregate["aggregateId"] and functional["projection"].get("freshness") in ("current", "Current"), "projection differs from persisted aggregate")
    for sequence, event in enumerate(events, 1):
        require(event.get("identity") == aggregate and event.get("sequenceNumber") == sequence
                and event.get("correlationId") == functional["commandId"] == event.get("causationId")
                and event.get("userId") == "g4-native-tests" and event.get("serializationFormat") == "json", "persisted event identity mismatch")
    created = [event for event in events if event.get("eventTypeName") == "Hexalith.Parties.Contracts.Events.PartyCreated"]
    require(len(created) == 1 and created[0].get("timestamp") == functional["projection"].get("createdAt"), "PartyCreated/projection timestamp mismatch")
    require(functional.get("tenantsNativeQueryObserved") is True and functional.get("memoriesRegistryObserved") is True, "real reference-owner assertions absent")
    current = functional["currentOwner"]
    for key in ("runId", "attemptId", "environmentId", "revision", "mappingHash", "profile"):
        require(current.get(key) == functional[key], "current owner lineage mismatch")
    require(current.get("configuration") == "Release" and current.get("qualificationCredit") is False and current.get("committedCatalogValidated") is True
            and current.get("sdkVersion") == "4.1.0" and current.get("tenantsVersion") == "7.0.4", "wrong current owner cohort")
    identity = current["platform"]
    require(all(identity.get(key) == platform for key in ("commit", "compositionCommit", "checkoutCommit")) and identity.get("catalogCommit") == builds, "foreign Platform/catalog identity")
    require(current.get("sdkBodies") == SDK, "actual runtime SDK bodies differ")
    archives = current.get("packageArchives")
    require(isinstance(archives, dict) and set(archives) == {"Hexalith.Parties.Contracts.3.0.0.nupkg", "Hexalith.Parties.EventStoreExtensions.3.0.0.nupkg"}
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
        identities = [item["identity"].split(",", 1)[0] for item in loaded[field]]
        require(len(identities) == len(set(identities)), "duplicate observed assemblies")
        for name, expected in SDK.items():
            observed = [item for item in loaded[field] if item["identity"].split(",", 1)[0] == name]
            require(len(observed) == 1 if field == "copied" or name == "Hexalith.EventStore.Contracts" else len(observed) <= 1, "native/composition SDK inventory missing")
            require(all(item.get("sha256") == expected and ", Version=4.1.0.0," in item["identity"] for item in observed), "native/composition SDK substitution")
    allowed = names | {loaded_name, "invocation.json", "execution.json", "cleanup.json", "test-result.json", "attachments.json", "attachment-withdrawals.json"}
    require(all(path.is_file() and not path.is_symlink() and path.name in allowed for path in root.iterdir()), "unreferenced or duplicate artifact substitute")
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
