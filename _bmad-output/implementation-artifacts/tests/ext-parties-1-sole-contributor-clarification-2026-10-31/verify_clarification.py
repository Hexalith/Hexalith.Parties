"""Validate the owner clarification offline without changing historical packets."""
import hashlib
import json
import re
import subprocess
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
checks = 0


def read(name):
    return json.loads((HERE / name).read_text())


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def check(condition, message):
    global checks
    checks += 1
    if not condition:
        raise AssertionError(message)


def references(value):
    if isinstance(value, dict):
        if "path" in value and "sha256" in value:
            check(Path(value["path"]).is_file() and sha(value["path"]) == value["sha256"], f"Reference drift: {value['path']}")
        for child in value.values():
            references(child)
    elif isinstance(value, list):
        for child in value:
            references(child)


before = read("preservation-before.json")
for name, expected in before["files"].items():
    check(Path(name).is_file() and sha(name) == expected, f"Historical file changed: {name}")
doc = before["additiveDocument"]
data = Path(doc["path"]).read_bytes()
check(hashlib.sha256(data[:doc["prefixBytes"]]).hexdigest() == doc["prefixSha256"], "Document prefix changed")
appended = data[doc["prefixBytes"]:].decode()
for target in re.findall(r"\]\(([^)]+)\)", appended):
    path = (Path(doc["path"]).parent / target).resolve()
    check(path.is_file() or path == HERE / "validation.json", f"Missing link: {target}")
owner = read("owner-contributor-clarification.json")
addendum = read("proposal-addendum.json")
findings = read("evidence-findings.json")
for obj in [owner, addendum, findings]:
    references(obj)
check(owner["instructionSource"]["verbatim"] == "there is no distinct deputy, I am the owner and only contibutor", "Owner statement changed")
check(owner["ownerCount"] == owner["contributorCount"] == 1, "Staffing changed")
check(owner["distinctDeputy"]["status"] == "OwnerConfirmedUnavailable", "Absence left unknown")
check(not owner["distinctDeputy"]["existsInProject"], "Deputy invented")
check(not owner["distinctDeputy"]["appointmentRequestOutstanding"], "Impossible nomination remains requested")
check(not owner["distinctDeputy"]["independentHumanRecoveryRequirementSatisfied"], "Unmet capability marked satisfied")
check(not owner["distinctDeputy"]["assistantOrOwnerCountedAsSecondHuman"], "Second Human invented")
check(owner["documentaryModel"]["independentHumanReviewerRequired"] is False, "Extra documentary reviewer required")
check(not owner["technicalIndependenceOrHistoricalEvidenceWaived"], "Waiver inferred")
check(not owner["documentaryModel"]["deployedAuthorizationOrAcceptanceRulesChanged"], "Policy changed")
check(all("deputy" not in fact.lower() for fact in findings["remainingOwnerFactsOnly"]), "Deputy request retained")
prior = json.loads(Path(addendum["priorProposal"]["path"]).read_text())
check(addendum["priorScopeSha256"] == prior["scopeSha256"] == "5d976b39f5d75e80eab42289787194de79947113a63cecd0c8792a5111d8b836", "Prior scope changed")
check(addendum["confirmedCutoff"] == prior["scope"]["calendarWindow"] == owner["confirmedCutoff"], "Confirmed cutoff changed")
check(addendum["confirmedCutoff"]["exactExclusiveDeadlineUtc"] == "2026-11-15T23:00:00Z", "Deadline changed")
check(addendum["historicalScope"] == prior["historicalScope"], "Historical expired scope changed")
check(addendum["historicalScope"]["status"] == "ExpiredIneligibleUnderOriginalBindings", "Historical expiry lost")
check(addendum["preservation"] == prior["preservation"], "Secret/Memories/register requirements changed")
check(addendum["evidenceAvailability"] == prior["evidenceAvailability"], "Other sources declared unavailable")
check(addendum["technicalWindows"] == prior["technicalWindows"], "Technical windows changed")
check(addendum["constraints"] == prior["constraints"], "Effort/spend changed")
check(addendum["allOtherUnresolvedRequirementsRetained"] and findings["allOtherUnresolvedRequirementsRetained"], "Other blockers lost")
check(all(v == 0 for v in addendum["effects"].values()), "Operational effect recorded")
check(addendum["effects"] == findings["effects"], "Effects mismatch")
check(not addendum["admissionAllowed"] and not addendum["executionAllowed"], "Recovery admitted")
check(addendum["liveStopped"] and addendum["fullCustodyStopped"], "Stop lost")
check(not addendum["agentsRegisterChanged"] and addendum["newOperationalRunId"] is None, "Register/run changed")
body = {k: v for k, v in addendum.items() if k not in ["addendumSha256", "fingerprintMethod"]}
check(hashlib.sha256(json.dumps(body, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()).hexdigest() == addendum["addendumSha256"], "Addendum fingerprint mismatch")
argv = ["git", "-c", "core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol", "diff", "--check", "--", "docs/implementation/ext-parties-1-operational-continuation-2026-10-30.md"]
result = subprocess.run(argv, cwd=ROOT, text=True, capture_output=True)
check(result.returncode == 0, f"Whitespace failure: {result.stdout}{result.stderr}")
for path in HERE.iterdir():
    if path.is_file():
        check(b"\n" not in path.read_bytes().replace(b"\r\n", b""), f"Expected CRLF: {path.name}")
receipt = {"verifiedAtUtc": datetime.now(timezone.utc).isoformat(), "passed": True, "checksPassed": checks, "protectedFilesPreserved": len(before["files"]), "earlierDocumentPrefixPreserved": True, "ownerConfirmedNoDeputy": True, "deputyNominationRequestRemoved": True, "soleOwnerDocumentaryPreparationAllowed": True, "independentHumanRecoveryRequirementSatisfied": False, "priorProposalsAndExpiredScopePreserved": True, "allOtherUnresolvedRequirementsRetained": True, "confirmedCutoffAndTechnicalWindowsUnchanged": True, "canonicalOwnerFormAndAgentsRegisterUnchanged": True, "admissionAllowed": False, "executionAllowed": False, "effects": addendum["effects"], "constraints": addendum["constraints"], "command": "python3 -B " + str(Path(__file__).relative_to(ROOT)), "whitespaceCommand": {"argv": argv, "exitCode": result.returncode, "stdout": result.stdout, "stderr": result.stderr}, "limit": "Offline integrity/consistency only. No deputy, independent recovery, historical completeness or current grant fabricated; no operational policy changed. Earlier tests, qualification, service checks and discovery were not repeated."}
(HERE / "validation.json").write_bytes((json.dumps(receipt, indent=4, ensure_ascii=False) + "\n").replace("\n", "\r\n").encode())
manifest = "".join(f"{sha(p)}  {p.name}\n" for p in sorted(HERE.iterdir()) if p.is_file() and p.name != "SHA256SUMS")
(HERE / "SHA256SUMS").write_bytes(manifest.replace("\n", "\r\n").encode())
print(json.dumps({"passed": True, "checksPassed": checks, "protectedFilesPreserved": len(before["files"]), "distinctDeputy": "OwnerConfirmedUnavailable", "nominationRequestOutstanding": False, "recoveryStopped": True}))
