"""Offline verification of the confirmed owner amendment and separate proposal."""
import hashlib
import json
import re
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from zoneinfo import ZoneInfo

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
checks = 0
historical_source_drift = []


def check(value, message):
    global checks
    checks += 1
    if not value:
        raise AssertionError(message)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(path):
    return json.loads(Path(path).read_text())


def verify_references(value, historical=False):
    if isinstance(value, dict):
        if isinstance(value.get("path"), str) and "sha256" in value:
            path = Path(value["path"])
            current = digest(path) if path.is_file() else None
            if historical and current != value["sha256"]:
                historical_source_drift.append({"path": str(path), "historicalSha256": value["sha256"], "currentSha256": current, "admissionAllowed": False})
            else:
                check(current == value["sha256"], f"Reference changed: {path}")
        for key, child in value.items():
            verify_references(child, historical or key == "historicalBindings")
    elif isinstance(value, list):
        for child in value:
            verify_references(child, historical)


before = read(HERE / "preservation-before.json")
for name, expected in before["files"].items():
    check(Path(name).is_file() and digest(name) == expected, f"Protected file changed: {name}")
doc = before["additiveDocument"]
doc_bytes = Path(doc["path"]).read_bytes()
check(hashlib.sha256(doc_bytes[:doc["prefixBytes"]]).hexdigest() == doc["prefixSha256"], "Earlier document bytes changed")
new_text = doc_bytes[doc["prefixBytes"]:].decode()
check("Owner recommendations applied" in new_text, "Missing additive continuation")
for target in re.findall(r"\]\(([^)]+)\)", new_text):
    path = (Path(doc["path"]).parent / target).resolve()
    check(path.is_file() or path == HERE / "validation.json", f"Missing new link: {target}")

owner = read(HERE / "owner-recommendation-acceptance.json")
proposal = read(HERE / "amended-recovery-proposal.json")
findings = read(HERE / "evidence-findings.json")
for value in [owner, proposal, findings]:
    verify_references(value)
check(owner["instructionSource"]["verbatim"] == "apply recommended", "Owner instruction changed")
check(owner["instructionSource"]["sourceMessageTimestamp"] is None, "Message timestamp invented")
cutoff = owner["acceptedCutoff"]
local = datetime(2026, 11, 16, tzinfo=ZoneInfo("Europe/Paris"))
check(cutoff["amendedDeadlineDate"] == "2026-11-15", "Owner date changed")
check(cutoff["exclusiveDeadlineLocal"] == local.isoformat() == "2026-11-16T00:00:00+01:00", "Wrong local cutoff")
check(cutoff["exactExclusiveDeadlineUtc"] == local.astimezone(timezone.utc).isoformat().replace("+00:00", "Z") == "2026-11-15T23:00:00Z", "Wrong UTC conversion")
check(cutoff["cutoffTimeAndTimezoneConfirmed"] and not cutoff["technicalOrCredentialWindowExtended"], "Cutoff confirmation or technical-window rule lost")
check(datetime(2026, 11, 15, 23, 59, 59, tzinfo=ZoneInfo("Europe/Paris")) < local, "Final day excluded")
check(not local < local, "Exclusive boundary admitted")

scope_bytes = json.dumps(proposal["scope"], ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()
check(hashlib.sha256(scope_bytes).hexdigest() == proposal["scopeSha256"], "Separate scope fingerprint mismatch")
check(proposal["scope"]["calendarWindow"] == cutoff, "Proposal cutoff differs from acceptance")
check(proposal["proposalId"] == proposal["scope"]["proposalId"], "Proposal identity mismatch")
historical = proposal["historicalScope"]
check(historical == owner["historicalScopePreserved"], "Historical scope changed")
check(historical["scopeSha256"] == "ef4a6896ed192137c4394cf5ef177ace1ce43ddf27a632731cf891f1c80cfbe3", "Original digest lost")
check(proposal["scopeSha256"] != historical["scopeSha256"], "Original fingerprint reused")
check(historical["runId"] == "9deb3d3afbbf42d39686abc30be4f64b", "Original run changed")
check(historical["exclusiveDeadlineUtc"] == "2026-10-30T16:53:50+00:00", "Original deadline changed")
check(historical["expiredAtRecording"] and historical["status"] == "ExpiredIneligibleUnderOriginalBindings", "Historical expiry lost")
check(not historical["originalBytesOrDigestChanged"] and not historical["revivedOrExecuted"], "Original scope revived")
check(proposal["scope"]["newOperationalRunId"] is None and proposal["operationalPlan"]["newRunId"] is None, "Runtime run created")
check(proposal["proposalId"] != historical["runId"], "Historical run adopted")
check(proposal["operationalPlan"]["nativeRequests"] == [] and proposal["operationalPlan"]["newOperationIds"] == [], "Native operations fabricated")
check(proposal["operationalPlan"]["liveMutationCommand"] is None, "Executable command created")
check(proposal["operationalPlan"]["selectedCredentialReference"] is None, "Credential selected")

prior = read(proposal["scope"]["requirementsFromLatestContinuation"]["path"])
base = read(proposal["scope"]["historicalRecoveryPlan"]["path"])
check(proposal["preservation"] == prior["preservation"], "Historical preservation requirements changed")
check(proposal["evidenceAvailability"] == prior["evidenceAvailability"], "Evidence or unavailability invented")
check(proposal["historicalBindings"]["resources"] == base["resources"], "Resource bindings changed")
check(proposal["historicalBindings"]["installedWorkloadImages"] == base["installedWorkloadImages"], "Image bindings changed")
check(proposal["scope"]["clusterBinding"] == base["cluster"], "Cluster binding changed")
segments = proposal["preservation"]["separatelyRequiredHistoricalSegments"]
check([(s["fromResourceVersion"], s["throughResourceVersion"]) for s in segments] == [("36774577", "37223806"), ("37223806", "37671992"), ("37671992", "38120141")], "Separate Secret intervals lost")
check(all(all(s[k] is None for k in ["authenticWriteSource", "authenticatedDisposition", "completeIntervalCovered"]) for s in segments), "Unproven interval admitted")
for key in ["authenticatedSecretDisposition", "authenticatedMemoriesDisposition", "authenticatedRegisterDisposition"]:
    check(proposal["preservation"][key] is None, f"Preservation acceptance invented: {key}")
check(proposal["preservation"]["coverageThroughActualCurrentAtAdmissionRequired"], "Actual admission revision coverage lost")
check(not proposal["preservation"]["currentMetadataSubstitutesForHistoricalWrites"], "Metadata substituted for history")
check(not owner["preservationDirection"]["factualPreservationAccepted"] and not owner["preservationDirection"]["historicalEvidenceWaived"], "Owner unknowns turned into waiver")
appointment = proposal["appointments"]
check(appointment["humanRecoveryLeadAppointmentAccepted"] and appointment["assessmentResponsibilityAccepted"], "Accepted owner designation omitted")
for key in ["namedHumanIdentityRecord", "humanSubjectAndIssuer", "assessmentExecutionAllocationReference", "differentQualifiedHumanDeputy", "deputyAppointmentAndAccessReference"]:
    check(appointment[key] is None, f"Unprovided appointment/access fact invented: {key}")
check(not appointment["operationalAppointmentGateSatisfied"], "Documentary appointment substituted for operational facts")
windows = read(prior["technicalWindows"]["path"])
check(proposal["technicalWindows"]["keyMappings"] == windows["keyMappings"], "Key technical windows changed")
check(proposal["technicalWindows"]["historicalAuthorityPlanWindows"] == windows["authorityPlanValidUntil"], "Authority technical windows changed")
check(not proposal["technicalWindows"]["credentialAndKeyWindowExtended"] and not proposal["technicalWindows"]["runtimeProfileChanged"], "Technical change invented")
for key in ["admissionAllowed", "executionAllowed", "backupCorrectionAllowed", "qualificationAllowed", "productionBackendAllowed", "agentsRegisterWritesAllowed"]:
    check(proposal[key] is False, f"Prohibited admission/effect: {key}")
check(proposal["liveStopped"] and proposal["fullCustodyStopped"], "Stop lost")
check(proposal["allInheritedBlockersRetained"] and findings["allInheritedBlockersRetained"], "Inherited blockers lost")
check(proposal["constraints"] == prior["constraints"], "Engineering/spend constraints changed")
check(all(v == 0 for v in proposal["effects"].values()), "Operational effect recorded")
check(proposal["effects"] == findings["effects"], "Inconsistent effects")

argv = ["git", "-c", "core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol", "diff", "--check", "--", "docs/implementation/ext-parties-1-operational-continuation-2026-10-30.md"]
whitespace = subprocess.run(argv, cwd=ROOT, text=True, capture_output=True)
check(whitespace.returncode == 0, f"Whitespace check failed: {whitespace.stdout}{whitespace.stderr}")
for path in HERE.iterdir():
    if path.is_file():
        check(b"\n" not in path.read_bytes().replace(b"\r\n", b""), f"Expected CRLF: {path.name}")

receipt = {"schema": "EXT-PARTIES-1.confirmed-proposal-offline-validation.v1", "verifiedAtUtc": datetime.now(timezone.utc).isoformat(), "passed": True, "checksPassed": checks, "protectedFilesPreserved": len(before["files"]), "earlierDocumentPrefixPreserved": True, "exactExclusiveDeadlineLocal": cutoff["exclusiveDeadlineLocal"], "timezone": "Europe/Paris", "exactExclusiveDeadlineUtc": cutoff["exactExclusiveDeadlineUtc"], "scopeSha256": proposal["scopeSha256"], "amendedProposalFileSha256": digest(HERE / "amended-recovery-proposal.json"), "historicalScopeExpiredAndUnchanged": True, "ownerDocumentaryDesignationRecorded": True, "operationalAppointmentGateSatisfied": False, "distinctQualifiedDeputyFound": False, "allInheritedBlockersRetained": True, "credentialAndKeyTechnicalWindowsUnchanged": True, "agentsRegisterAndCanonicalOwnerFormUnchanged": True, "preexistingHistoricalDifferencesRetained": before["olderHistoricalDifferencesRetained"], "historicalSourceDriftObserved": historical_source_drift, "admissionAllowed": False, "executionAllowed": False, "effects": proposal["effects"], "constraints": proposal["constraints"], "whitespaceCommand": {"argv": argv, "exitCode": whitespace.returncode, "stdout": whitespace.stdout, "stderr": whitespace.stderr}, "command": "python3 -B " + str(Path(__file__).relative_to(ROOT)), "limit": "Offline integrity, cutoff/fingerprint and requirement checks only. No old verifier, discovery, service/provider/SSH probe, credential operation, recovery, qualification or external message. Documentary owner designation supplies no missing operational identity/access/allocation or factual preservation proof."}
(HERE / "validation.json").write_bytes((json.dumps(receipt, ensure_ascii=False, indent=4) + "\n").replace("\n", "\r\n").encode())
manifest = "".join(f"{digest(p)}  {p.name}\n" for p in sorted(HERE.iterdir()) if p.is_file() and p.name != "SHA256SUMS")
(HERE / "SHA256SUMS").write_bytes(manifest.replace("\n", "\r\n").encode())
for line in manifest.splitlines():
    expected, name = line.split("  ", 1)
    check(digest(HERE / name) == expected, f"Manifest mismatch: {name}")
print(json.dumps({"passed": True, "checksPassedIncludingManifest": checks, "protectedFilesPreserved": len(before["files"]), "scopeSha256": proposal["scopeSha256"], "exactExclusiveDeadlineUtc": cutoff["exactExclusiveDeadlineUtc"], "historicalSourceDriftCount": len(historical_source_drift), "admissionAllowed": False, "executionAllowed": False}))
