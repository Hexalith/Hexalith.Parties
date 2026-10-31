"""Validate this additive evidence packet offline; never import operational sources."""

import datetime
import hashlib
import json
from pathlib import Path
import re
import subprocess


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
DOCUMENT = ROOT / "docs/implementation/ext-parties-1-operational-continuation-2026-10-30.md"
checks = 0


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(name):
    return json.loads((HERE / name).read_text())


def check(condition, message):
    global checks
    checks += 1
    if not condition:
        raise AssertionError(message)


before = read("preservation-before.json")
document_baseline = next(row for row in before["files"] if Path(row["path"]) == DOCUMENT)
document_bytes = DOCUMENT.read_bytes()
prefix = document_bytes[:document_baseline["size"]]
check(hashlib.sha256(prefix).hexdigest() == document_baseline["sha256"], "Historical document prefix changed")
for row in before["files"]:
    if Path(row["path"]) != DOCUMENT:
        check(sha(row["path"]) == row["sha256"], "Historical/canonical file changed: " + row["path"])


def references(value):
    if isinstance(value, dict):
        if "path" in value and "sha256" in value:
            if Path(value["path"]) == DOCUMENT:
                check(value["sha256"] == document_baseline["sha256"], "Pre-append document reference mismatch")
            else:
                check(sha(value["path"]) == value["sha256"], "Captured reference changed: " + value["path"])
        for child in value.values():
            references(child)
    elif isinstance(value, list):
        for child in value:
            references(child)


findings = read("evidence-findings.json")
for filename in ["evidence-findings.json", "new-source-delta.json", "source-snapshot-integrity.json"]:
    references(read(filename))
prior_path = ROOT / "_bmad-output/implementation-artifacts/tests/ext-parties-1-sole-contributor-clarification-2026-10-31/proposal-addendum.json"
prior = json.loads(prior_path.read_text())
for key in ["confirmedCutoff", "historicalScope", "documentaryRoleDisposition", "constraints"]:
    check(findings[key] == prior[key], key + " changed")
check(findings["historicalScope"]["status"] == "ExpiredIneligibleUnderOriginalBindings", "Original scope revived")
check(findings["confirmedCutoff"]["exactExclusiveDeadlineUtc"] == "2026-11-15T23:00:00Z", "Confirmed cutoff changed")
check(not findings["documentaryRoleDisposition"]["independentHumanRecoveryAvailable"], "Independent Human invented")
check(findings["documentaryRoleDisposition"]["deputyNominationNoLongerOutstanding"], "Deputy nomination requested again")
check(not findings["newProposalOrRunCreated"], "Equivalent proposal/run generated")
history = findings["historicalSecretWrites"]
check(history["separateIntervals"] == prior["preservation"]["separatelyRequiredHistoricalSegments"], "Secret intervals changed")
check(not history["authenticatedHistoricalWriteRecordsCollected"] and history["archiveAvailability"] == "unknown", "History/absence fabricated")
check(history["actualCurrentIdentityAtAdmission"] is None and history["coverageThroughActualCurrentAtAdmissionRequired"], "Admission endpoint inferred")
check(not history["currentMetadataUsedAsHistoricalProof"] and not history["newSystemSecretGetPerformed"], "Metadata substituted for history")
dispositions = findings["separatePreservationDispositions"]
check(dispositions["memories"]["exactDifferences"] == prior["preservation"]["memoriesExactDifferencesInherited"], "Memories differences changed")
check(all(v["status"] == "unresolved" and v["authenticatedDisposition"] is None for v in dispositions.values()), "Preservation accepted without evidence")
check(findings["currentMetadataOnlyCredential"]["intendedCurrentCredentialRecord"] is None, "Current credential fabricated")
check(findings["snapshotAndKeyCopies"]["completeInventory"] is None, "Logs promoted to complete inventory")

source = read("source-snapshot-integrity.json")
check(all(x["matchesRecordedFingerprint"] and x["manifestEntryMatches"] for x in source["originalSnapshot"]), "Original snapshot fingerprint mismatch")
check(all(x["matchesOwnGenerationPin"] and x["manifestEntryMatches"] for x in source["laterTestedSnapshot"]), "Later tested generation mismatch")
check(all(x["matchesOwnGenerationPin"] for x in source["laterPreservedSnapshot"]), "Later preserved generation mismatch")
check(all(x["exitCode"] == 0 and x["matchesFinalRecordedFingerprint"] and x["differenceMatchesRecordedManifest"] for x in source["laterFinalReconstruction"]), "Final differential source integrity failed")
check(all(x["matchesHistoricalFinalReceipt"] and not x["admittedForExecution"] for x in source["currentSourceCandidates"]), "Current candidate/source admission conflated")
check(not source["currentSourceAdmission"] and not source["sourceImportedOrExecuted"] and not source["historicalTestsRerun"], "Operational source/test execution recorded")

logs = read("snapshot-log-observations.json")
check(logs["podListReceipt"]["exitCode"] == 0 and len(logs["logObservations"]) == 2, "Snapshot observations missing")
for observation in logs["logObservations"]:
    receipt = observation["receipt"]
    check(receipt["exitCode"] == 0, "Snapshot log read failed")
    check(receipt["argv"][receipt["argv"].index("--server") + 1] == "https://192.168.1.30:6443" and "--insecure-skip-tls-verify=false" in receipt["argv"], "Log transport binding missing")
    reconstructed = "".join(x["loggedAt"] + " " + x["checksumResult"] + "\n" for x in observation["publicChecksumResults"]).encode()
    check(len(reconstructed) == receipt["stdoutBytes"] and hashlib.sha256(reconstructed).hexdigest() == receipt["stdoutSha256"], "Public lines do not account for the captured log bytes")
binding = read("snapshot-pvc-binding.json")
check(binding["exitCode"] == 0 and binding["uidMatches"], "Snapshot pod UID binding failed")
check("snapshots\topenbao-snapshots\n" in binding["stdout"] and "snapshots\t/snapshots\n" in binding["stdout"], "Snapshot PVC/mount binding missing")
failed = read("snapshot-log-attempt-01.json")["podListReceipt"]
check(failed["exitCode"] == 1 and failed["failureClassification"] == "LocalJsonpathParsingError", "Initial failure misclassified")
check(len(failed["stderr"].encode()) == failed["stderrBytes"] and hashlib.sha256(failed["stderr"].encode()).hexdigest() == failed["stderrSha256"], "Recovered parser error does not match original bytes")
check(not findings["admissionAllowed"] and not findings["recoveryAllowed"] and findings["liveStopped"] and findings["fullCustodyStopped"], "Required stop lost")
check(not findings["agentsRegisterChanged"] and not findings["technicalWindowsExtended"], "Register/window changed")
for key, value in findings["effects"].items():
    if key not in {"kubernetesGetCommandAttempts", "successfulSelectedMetadataGets", "localJsonpathFormattingFailures", "snapshotPodLogReads"}:
        check(value == 0, "Forbidden effect: " + key)
check(findings["effects"]["kubernetesGetCommandAttempts"] == 3 and findings["effects"]["snapshotPodLogReads"] == 2, "Read attempts not accounted for")

appended = document_bytes[document_baseline["size"]:].decode()
for target in re.findall(r"\]\(([^)]+)\)", appended):
    path = (DOCUMENT.parent / target).resolve()
    check(path.is_file() or path == HERE / "validation.json", "New document link missing: " + target)
for path in HERE.iterdir():
    if path.is_file():
        data = path.read_bytes()
        check(b"\n" not in data.replace(b"\r\n", b""), "Expected CRLF: " + path.name)
        check(all(line == line.rstrip() for line in data.decode().splitlines()), "Trailing whitespace: " + path.name)
argv = ["git", "-c", "core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol", "diff", "--check", "--", str(DOCUMENT.relative_to(ROOT))]
result = subprocess.run(argv, cwd=ROOT, capture_output=True, text=True)
check(result.returncode == 0, "Whitespace command failed: " + result.stdout + result.stderr)
receipt = {
    "schema": "EXT-PARTIES-1.technical-evidence-offline-validation.v1",
    "verifiedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "passed": True, "checksPassed": checks,
    "protectedFilesPreserved": len(before["files"]) - 1,
    "earlierDocumentPrefixPreserved": True,
    "sourceIntegrityAndCurrentAdmissionSeparated": True,
    "rawSnapshotLogBytesAccountedForByPublicLines": True,
    "archiveAndCredentialAndCompleteCopyInventoryAvailability": "unknown",
    "separatePreservationDispositions": "unresolved",
    "agentsRegisterAndCanonicalOwnerFormUnchanged": True,
    "admissionAllowed": False, "recoveryAllowed": False,
    "effects": findings["effects"], "constraints": findings["constraints"],
    "command": "python3 -B " + str(Path(__file__).relative_to(ROOT)),
    "whitespaceCommand": {"argv": argv, "exitCode": result.returncode, "stdout": result.stdout, "stderr": result.stderr},
    "limit": "Offline integrity, byte preservation and observation consistency only. No operational source imported, historical test rerun, qualification, current grant, complete history or factual preservation acceptance established.",
}
(HERE / "validation.json").write_bytes((json.dumps(receipt, indent=4) + "\n").replace("\n", "\r\n").encode())
manifest = "".join(sha(p) + "  " + p.name + "\n" for p in sorted(HERE.iterdir()) if p.is_file() and p.name != "SHA256SUMS")
(HERE / "SHA256SUMS").write_bytes(manifest.replace("\n", "\r\n").encode())
print(json.dumps({"passed": True, "checksPassed": checks, "protectedFilesPreserved": len(before["files"]) - 1, "documentPrefixPreserved": True, "recoveryStopped": True}))
