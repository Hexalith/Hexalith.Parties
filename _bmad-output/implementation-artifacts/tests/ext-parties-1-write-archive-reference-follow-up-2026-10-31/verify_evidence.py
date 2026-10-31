"""Validate this reference follow-up offline; never open credentials or payloads."""

import hashlib
import json
import re
import subprocess
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
checks = 0


def read(path):
    return json.loads(Path(path).read_text())


def check(condition, message):
    global checks
    if not condition:
        raise SystemExit(message)
    checks += 1


def digest(data):
    return hashlib.sha256(data).hexdigest()


def pointer(value, path):
    for part in path.strip("/").split("/"):
        value = value[part]
    return value


before = read(HERE / "preservation-before.json")
findings = read(HERE / "evidence-findings.json")
document = before["documentPrefix"]
document_path = Path(document["path"])
document_bytes = document_path.read_bytes()
check(digest(document_bytes[:document["size"]]) == document["sha256"], "Earlier document prefix changed")
for ref in before["protectedFiles"] + list(before["sources"].values()):
    data = Path(ref["path"]).read_bytes()
    check(len(data) == ref["size"] and digest(data) == ref["sha256"], "Protected reference changed: " + ref["path"])
check(findings["sources"] == before["sources"], "Reference set changed")

sources = {key: read(ref["path"]) for key, ref in findings["sources"].items() if ref["path"].endswith(".json")}
owner = sources["canonicalOwnerForm"]
for key, value in findings["canonicalReferenceChain"]["observedPointers"].items():
    check(value is None and pointer(owner, key) is None, "Canonical pending field changed: " + key)
for key in ["confirmedCutoff", "historicalScope", "documentaryRoleDisposition", "constraints", "historicalSecretWrites"]:
    check(findings[key] == sources["priorFindings"][key], "Inherited binding changed: " + key)
check(sources["priorValidation"]["passed"], "Missing inherited successful validation receipt")
check(findings["confirmedCutoff"]["exactExclusiveDeadlineUtc"] == "2026-11-15T23:00:00Z", "Confirmed cutoff changed")
check(findings["historicalScope"]["status"] == "ExpiredIneligibleUnderOriginalBindings", "Historical scope revived")
check(findings["documentaryRoleDisposition"]["deputyNominationNoLongerOutstanding"], "Deputy nomination reopened")

history = findings["historicalSecretWrites"]
intervals = history["separateIntervals"]
check([(row["fromResourceVersion"], row["throughResourceVersion"]) for row in intervals] == [
    ("36774577", "37223806"), ("37223806", "37671992"), ("37671992", "38120141")
], "Historical intervals merged or changed")
for row in intervals:
    check(all(row[key] is None for key in ["authenticWriteSource", "authenticatedDisposition", "completeIntervalCovered"]), "Unsupported history claim")
check(history["archiveAvailability"] == "unknown" and history["actualCurrentIdentityAtAdmission"] is None, "Archive availability or current admission identity invented")
check(history["coverageThroughActualCurrentAtAdmissionRequired"] and not history["currentMetadataUsedAsHistoricalProof"], "Admission continuity weakened")

snapshot, archive, proposed = findings["concreteArchiveCandidates"]
etcd = sources["historicalEtcdSnapshotReceipt"]
node = sources["historicalNodeArchiveReceipt"]
bindings = sources["recoveryBindings"]
check(snapshot["snapshotRevision"] == etcd["status"]["revision"] == 25283476, "Snapshot revision mismatch")
check(snapshot["snapshotRevision"] < int(intervals[0]["fromResourceVersion"]), "Old snapshot unexpectedly covers required revisions")
check(snapshot["compactRevision"] == etcd["sourceHashKv"][0]["HashKV"]["compact_revision"], "Compaction receipt mismatch")
check(snapshot["snapshotSha256"] == etcd["snapshotSha256"], "Snapshot receipt fingerprint miscopied")
check(bindings["historicalObservations"]["etcdSource"]["sha256"] == findings["sources"]["historicalEtcdSnapshotReceipt"]["sha256"], "Snapshot chain mismatch")
check(bindings["historicalObservations"]["nodeBundle"]["sha256"] == findings["sources"]["historicalNodeArchiveReceipt"]["sha256"], "Node archive chain mismatch")
for key in ["key", "versionId"]:
    check(archive[key] == node["immutableReadback"]["readback"][key], "Archive object reference mismatch")
check(archive["recordedRetention"] == node["immutableReadback"]["readback"]["retentionApi"], "Retention record mismatch")
check(archive["currentObjectExistenceAndAccess"] == "unknown", "Storage-lock expiry treated as deletion or current access")
check(proposed["preparationOnly"] and not proposed["mutationAuthorized"] and proposed["operationalGate"] == "closed" and proposed["scopeApproval"] is None, "Proposed archive admitted")
check(proposed["accessReference"]["writerApplication"] == bindings["trust"]["writerIdentity"], "Writer reference mismatch")
check(proposed["accessReference"]["validatorApplication"] == bindings["trust"]["validatorIdentity"], "Validator reference mismatch")
check(findings["authenticatedAccessLimits"]["currentAuthenticatedWriteArchiveTransportReference"] is None, "Archive transport invented")
check(all(value == 0 for value in findings["effects"].values()), "Operational effects recorded")
check(not findings["admissionAllowed"] and not findings["recoveryAllowed"] and findings["liveStopped"] and findings["fullCustodyStopped"] and not findings["agentsRegisterChanged"], "Stop/register disposition changed")

added = document_bytes[document["size"]:].decode()
for target in re.findall(r"\]\(([^)]+)\)", added):
    path = Path(target) if target.startswith("/") else document_path.parent / target
    check(path.is_file() or path.resolve() == HERE / "validation.json", "New document link missing: " + target)
for path in [HERE / "evidence-findings.json", HERE / "preservation-before.json", HERE / "verify_evidence.py"]:
    data = path.read_bytes()
    check(b"\n" not in data.replace(b"\r\n", b""), "Non-CRLF new file: " + str(path))
whitespace_argv = ["git", "-c", "core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol", "diff", "--check", "--", str(document_path.relative_to(ROOT))]
whitespace = subprocess.run(whitespace_argv, cwd=ROOT, capture_output=True, text=True, check=False)
check(whitespace.returncode == 0, "Document whitespace check failed: " + whitespace.stderr + whitespace.stdout)
receipt = {
    "schema": "EXT-PARTIES-1.write-archive-reference-offline-validation.v1",
    "verifiedAtUtc": datetime.now(timezone.utc).isoformat(),
    "passed": True,
    "checksPassed": checks,
    "command": "python3 -B " + str(Path(__file__).relative_to(ROOT)),
    "documentPrefixPreserved": True,
    "priorPacketAndCanonicalOwnerFormAndAgentsRegisterPreserved": True,
    "sourceReferencesVerified": len(before["sources"]),
    "archiveAvailabilityAndAllIntervals": "unknown",
    "writeArchiveAuthenticated": False,
    "operationalCalls": 0,
    "priorChecksOrObservationsRerun": False,
    "admissionAllowed": False,
    "recoveryAllowed": False,
    "whitespaceCommand": {"argv": whitespace_argv, "exitCode": whitespace.returncode, "stdout": whitespace.stdout, "stderr": whitespace.stderr},
    "limit": "Local reference-chain consistency and byte preservation only; no archive payload, historical write completeness, current access, preservation acceptance, runtime admission or operational qualification is established.",
}
(HERE / "validation.json").write_bytes((json.dumps(receipt, indent=4) + "\n").replace("\n", "\r\n").encode())
print(json.dumps({"passed": True, "checksPassed": checks, "sourcesFollowed": len(before["sources"]), "operationalCalls": 0}))
