"""Verify this additive evidence packet offline; never call an operational service."""
import hashlib
import json
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
checks = 0


def check(condition, message):
    global checks
    checks += 1
    if not condition:
        raise AssertionError(message)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(name):
    return json.loads((HERE / name).read_text())


def write(name, value):
    (HERE / name).write_bytes(
        (json.dumps(value, indent=2, ensure_ascii=False) + "\n")
        .replace("\n", "\r\n")
        .encode()
    )


def verify_references(value):
    if isinstance(value, dict):
        if isinstance(value.get("path"), str) and "sha256" in value:
            path = Path(value["path"])
            check(path.is_absolute(), f"Nonabsolute reference: {path}")
            check(path.is_file() and digest(path) == value["sha256"], f"Source drift: {path}")
        for child in value.values():
            verify_references(child)
    elif isinstance(value, list):
        for child in value:
            verify_references(child)


record = "--record" in sys.argv
before = read("preservation-before.json")
for name, expected in before["files"].items():
    check(Path(name).is_file() and digest(name) == expected, f"Protected file changed: {name}")
doc_ref = before["additiveDocument"]
document = Path(doc_ref["path"]).read_bytes()
check(hashlib.sha256(document[:doc_ref["prefixBytes"]]).hexdigest() == doc_ref["prefixSha256"], "Original document prefix changed")
appended = document[doc_ref["prefixBytes"]:].decode()
check("Additive amended-window evidence continuation" in appended, "Missing additive section")
for target in re.findall(r"\]\(([^)]+)\)", appended):
    path = (Path(doc_ref["path"]).parent / target.split("#", 1)[0]).resolve()
    check(path.is_file() or (record and path == HERE / "validation.json"), f"Broken added link: {target}")

for path in HERE.glob("*.json"):
    if path.name not in {"preservation-before.json", "validation.json"}:
        verify_references(json.loads(path.read_text()))

continuation = read("continuation.json")
findings = read("evidence-findings.json")
check(continuation["amendedDeadline"]["date"] == "2026-11-15", "Amended date lost")
check(continuation["amendedDeadline"]["exactExclusiveDeadlineUtc"] is None, "Unconfirmed cutoff invented")
check(not continuation["amendedDeadline"]["cutoffTimeAndTimezoneConfirmed"], "Unconfirmed cutoff accepted")
check(not continuation["amendedDeadline"]["answerReceived"], "Owner answer fabricated")
check(continuation["amendedScopeProposal"] is None and continuation["newScopeDigestOrRunId"] is None, "Premature amended scope")
check(not list(HERE.glob("*proposal*.json")), "Premature proposal artifact")
check(not continuation["newOperationalBodiesPrepared"], "Operational bodies fabricated")
historical = continuation["historicalScope"]
check(historical["scopeSha256"] == "ef4a6896ed192137c4394cf5ef177ace1ce43ddf27a632731cf891f1c80cfbe3", "Original scope digest changed")
check(historical["runId"] == "9deb3d3afbbf42d39686abc30be4f64b", "Original run changed")
check(historical["exclusiveDeadlineUtc"] == "2026-10-30T16:53:50+00:00", "Original deadline changed")
check(historical["status"] == "ExpiredIneligibleUnderOriginalBindings" and historical["expiredAtRecording"], "Expired historical status lost")
original = json.loads(Path(historical["source"]["path"]).read_text())
check(original["scopeSha256"] == historical["scopeSha256"] and original["runId"] == historical["runId"], "Original source binding mismatch")
check(original["proposedWindow"]["exclusiveDeadline"] == historical["exclusiveDeadlineUtc"], "Original source window mismatch")

prior = json.loads(Path(continuation["priorProposal"]["path"]).read_text())
preservation = continuation["preservation"]
segments = preservation["separatelyRequiredHistoricalSegments"]
check([(s["fromResourceVersion"], s["throughResourceVersion"]) for s in segments] == [("36774577", "37223806"), ("37223806", "37671992"), ("37671992", "38120141")], "Separate transitions lost")
for segment in segments:
    check(all(segment[k] is None for k in ["authenticWriteSource", "authenticatedDisposition", "completeIntervalCovered"]), "Unproven interval admitted")
for key in ["authenticatedSecretDisposition", "authenticatedMemoriesDisposition", "authenticatedRegisterDisposition"]:
    check(preservation[key] is None, f"Unproven preservation acceptance: {key}")
check(preservation["memoriesExactDifferencesInherited"] == prior["preservation"]["memoriesExactDifferencesInherited"], "Memories differences changed")
check(preservation["coverageThroughActualCurrentAtAdmissionRequired"] and preservation["futureAdmissionRequiresFreshActualIdentityAndCompleteHistory"], "Actual admission history requirement lost")
check(not preservation["currentMetadataSubstitutesForHistoricalWrites"], "Metadata substituted for history")
check(preservation["currentIdentityObservation"]["observedAtUtc"] == prior["preservation"]["currentIdentityObservation"]["observedAtUtc"], "Historical GET restamped")
check(preservation["currentIdentityObservation"]["notRefreshedInThisContinuation"], "Fresh GET invented")
check(not preservation["historicalRegisterMismatchReconciled"], "Register mismatch silently reconciled")
check(all(v is None for v in continuation["appointments"].values()), "Appointment invented")
check(all(v is None for k, v in continuation["evidenceAvailability"].items() if k != "unavailabilityRule"), "Evidence or explicit unavailability invented")
for key in ["admissionAllowed", "executionAllowed", "backupCorrectionAllowed", "qualificationAllowed", "productionBackendAllowed"]:
    check(continuation[key] is False, f"Prohibited admission/effect: {key}")
check(continuation["liveStopped"] and continuation["fullCustodyStopped"], "Stop lost")
check(continuation["allInheritedBlockersRetained"] and findings["allInheritedBlockersRetained"], "Inherited blockers lost")
check(continuation["constraints"] == {"capEngineerDays": 2, "newInfrastructureSpend": 0, "newCapOrEffortAllocationInferred": False, "spentEffortInferred": False}, "Constraints changed")
check(all(v == 0 for v in continuation["effects"].values()), "Unpermitted effect recorded")
check(findings["effects"] == continuation["effects"], "Inconsistent effects")

windows = read("technical-window-analysis.json")
base = json.loads(Path(continuation["inheritsEntireHistoricalProposalAsEvidence"]["path"]).read_text())
for retained, proposed in zip(windows["keyMappings"], base["keyPlan"], strict=True):
    check(retained["notBefore"] == proposed["mapping"]["notBefore"] and retained["verifyUntil"] == proposed["mapping"]["verifyUntil"], "Key technical window changed")
    check(datetime.fromisoformat(retained["verifyUntil"]) < datetime(2026, 11, 15, tzinfo=timezone.utc), "Window comparison incorrect")
check(not windows["credentialOrKeyWindowsExtended"] and not windows["freshProviderObservation"], "Technical extension/freshness invented")
obs = read("source-observations.json")
check(obs["ownerRecordsAdmitted"] == [], "Unsubstantiated owner record admitted")
check(obs["agentsRegister"]["source"]["sha256"] == preservation["registerObservedSha256"], "Register changed")
check(obs["agentsRegister"]["originalSha256"] == preservation["registerOriginalSha256"], "Original register binding changed")
follow = read("newer-source-follow-up.json")
check(follow["memories"]["catalogToSetupFingerprintMatches"] and follow["memories"]["setupToApprovalFingerprintMatches"] and follow["memories"]["adoptionReceiptFingerprintMatches"], "New source chain mismatch")
check(not follow["memories"]["qualificationCredit"], "Other-scope qualification invented")
check(follow["protection"]["selectedFields"]["acceptedProfile"] is None, "New accepted profile invented")

argv = ["git", "-c", "core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol", "diff", "--check", "--", "docs/implementation/ext-parties-1-operational-continuation-2026-10-30.md"]
whitespace = subprocess.run(argv, cwd=ROOT, text=True, capture_output=True)
check(whitespace.returncode == 0, f"Whitespace check failed: {whitespace.stderr}{whitespace.stdout}")
for path in HERE.rglob("*"):
    if path.is_file() and path.parent.name != "source-snapshots":
        data = path.read_bytes()
        check(b"\n" not in data.replace(b"\r\n", b""), f"Expected CRLF: {path.name}")

report = {"schema": "EXT-PARTIES-1.offline-continuation-validation.v1", "verifiedAtUtc": datetime.now(timezone.utc).isoformat(), "passed": True, "checksPassed": checks, "protectedFilesUnchangedDuringThisContinuation": len(before["files"]), "preexistingHistoricalFingerprintDifferencesRetained": before["inheritedFingerprintDifferencesBeforeWork"], "earlierDocumentPrefixPreserved": True, "documentSha256": digest(doc_ref["path"]), "historicalScopeExpiredUnderOriginalBindings": True, "exactCutoffPending": True, "amendedProposalPrepared": False, "allInheritedBlockersRetained": True, "agentsRegisterUnchanged": True, "canonicalOwnerFormUnchanged": True, "effects": continuation["effects"], "constraints": continuation["constraints"], "whitespaceCommand": {"argv": argv, "exitCode": whitespace.returncode, "stdout": whitespace.stdout, "stderr": whitespace.stderr}, "command": "python3 -B " + str(Path(__file__).relative_to(ROOT)) + " --record", "limit": "Offline integrity, references, history/requirement preservation and whitespace only. No old verifier/tests/signatures, service checks, recovery or qualification rerun. Source hashes supply no new owner authentication, historical completeness, current grant or technical extension."}
if record:
    write("validation.json", report)
    manifest = "".join(f"{digest(p)}  {p.relative_to(HERE).as_posix()}\n" for p in sorted(HERE.rglob("*")) if p.is_file() and p.name != "SHA256SUMS")
    (HERE / "SHA256SUMS").write_bytes(manifest.replace("\n", "\r\n").encode())
manifest_count = 0
for line in (HERE / "SHA256SUMS").read_text().splitlines():
    expected, name = line.split("  ", 1)
    check(digest(HERE / name) == expected, f"Packet manifest mismatch: {name}")
    manifest_count += 1
print(json.dumps({"passed": True, "checksPassedIncludingManifest": checks, "protectedFiles": len(before["files"]), "packetManifestEntries": manifest_count, "recordedReceipt": record, "exactCutoffPending": True, "admissionAllowed": False, "executionAllowed": False}))
