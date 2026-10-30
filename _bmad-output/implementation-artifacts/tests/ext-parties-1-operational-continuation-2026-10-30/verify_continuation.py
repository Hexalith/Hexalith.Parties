"""Validate this additive evidence packet offline; never operate resources."""

import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
DOC = ROOT / "docs/implementation/ext-parties-1-operational-continuation-2026-10-30.md"
SECRETS = Path("/home/administrator/hexalith-secrets-evidence/ext-secrets-1")
SCOPE = SECRETS / "expired-hmac-recovery-scope-20261029T165350Z-final/recovery-scope.json"
checks = []


def load(path):
    return json.loads(path.read_text())


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check(condition, message):
    if not condition:
        raise AssertionError(message)
    checks.append(message)


preserved = load(HERE / "preservation-before.json")["files"]
for name, expected in preserved.items():
    check(digest(Path(name)) == expected, "preserved: " + name)

references = load(HERE / "source-references.json")
for reference in references["files"]:
    check(digest(Path(reference["path"])) == reference["sha256"], "source: " + reference["path"])

manifest_counts = {}
for relative in [
    "expired-hmac-recovery-scope-20261029T165350Z-final",
    "expired-hmac-recovery-run-20261029-approved",
    "expired-hmac-recovery-run-20261029-approved/metadata-access-correction-20261029",
    "expired-hmac-recovery-run-20261029-approved/metadata-access-application-20261029-approved",
]:
    path = SECRETS / relative / "evidence-manifest.json"
    manifest = load(path)
    files = manifest.get("files", manifest)
    for name, expected in files.items():
        target = Path(name) if Path(name).is_absolute() else path.parent / name
        check(digest(target) == expected, "manifest: " + str(target))
    manifest_counts[str(path)] = len(files)

scope = load(SCOPE)
plan = load(HERE / "operational-recovery-proposal.json")
findings = load(HERE / "evidence-findings.json")
check(load(HERE / "whitespace-checks.json")["passed"], "whitespace checks have no diagnostics")
check(plan["authority"]["operations"] == scope["nativeOwnerOperations"], "nine exact original operation bodies")
check(len(plan["authority"]["operations"]) == 9, "nine operations only")
check(plan["window"] == scope["proposedWindow"], "fixed original execution window")
check(plan["keyPlan"] == scope["keyPlan"], "exact key mappings and technical windows")
check(plan["installedWorkloadImages"] == scope["installedWorkloadImages"], "exact immutable workload images")
check(plan["runtimeProposal"]["exactPublicDocument"] == scope["proposedRuntimeForReuseOrRenew"], "exact proposed public runtime")
check(plan["failureContainment"] == scope["failureContainment"], "exact inherited containment intents")
for resource in plan["resources"]:
    check(resource["scopePrecondition"] == scope["installedResourcePreconditions"][resource["reference"]], "resource precondition: " + resource["reference"])
    if resource["observedIdentity"]:
        check(resource["observedIdentity"]["uid"] == resource["scopePrecondition"]["uid"], "observed UID: " + resource["reference"])
check(plan["executionAllowed"] is False and plan["liveStopped"] and plan["fullCustodyStopped"], "all execution stops retained")
check(plan["credentialProposal"]["newContinuationPermitsRenewal"] is False, "no renewal permitted")
check(plan["preservation"]["authenticatedDecision"] is None, "no fabricated preservation acceptance")
check(plan["preservation"]["freshObservedSecretIdentity"]["resourceVersion"] == "38120141", "new RV recorded independently")
check(plan["appointments"]["assessmentImplementer"] is None and plan["appointments"]["accountableRecoveryLead"] is None and plan["appointments"]["differentHumanDeputy"] is None, "missing appointments preserved")
check(findings["authorityTime"]["qualifiedExistingMechanism"] is None and findings["authorityTime"]["fullCustodyStopRetained"], "authority stop retained")
check(findings["currentAcceptedProfile"] is None, "no invented current accepted profile")

for role, original in scope["credentialBranches"].items():
    check(plan["credentialProposal"]["roles"][role]["replacementIntent"] == original["replace"], "exact successor intent: " + role)
for record in load(HERE / "kubernetes-metadata-read-only.json"):
    check(record["exitCode"] == 0, "read-only GET succeeded")
    check("get" in record["argv"] and any("jsonpath=" in item for item in record["argv"]), "metadata-only output selection")
for name, item in load(HERE / "executor-source-observation.json")["files"].items():
    check(item["worktreeMatchesLaterTested"], "later tested source: " + name)
    source = Path("/home/administrator/projects/hexalith/platform-ext-secrets-1-runtime-foundations-20261017") / name
    check(digest(source) == item["laterTestedExpected"], "source still matches: " + name)

for target in re.findall(r"\]\(([^)]+)\)", DOC.read_text()):
    if "://" in target:
        continue
    target = target.split("#", 1)[0]
    path = Path(target) if target.startswith("/") else DOC.parent / target
    # The receipt is generated only after all validation succeeds.
    if path.resolve() == (HERE / "validation.json").resolve():
        continue
    check(path.exists(), "local link: " + target)

for path in HERE.glob("*.json"):
    load(path)
    check(True, "JSON parsed: " + path.name)

result = {
    "verifiedAtUtc": datetime.now(timezone.utc).isoformat(),
    "passed": True,
    "checksPassed": len(checks),
    "historicalFilesPreserved": len(preserved),
    "sourceManifestsVerified": manifest_counts,
    "agentsRegisterUnchanged": True,
    "canonicalOwnerFormUnchanged": True,
    "existingIndexEditUnchanged": True,
    "exactScopeProposalVerified": True,
    "qualificationRun": False,
    "runtimeRecoveryExecuted": False,
    "limit": "Offline file integrity and evidence consistency only. No attestation authenticity, historical completeness, current service validity, all-copy disposal or qualification is established.",
    "command": "python3 -B " + str(Path(__file__).relative_to(ROOT)),
}
(HERE / "validation.json").write_text(json.dumps(result, indent=2) + "\n", newline="\r\n")
print(json.dumps({"passed": True, "checks": len(checks), "historicalFilesPreserved": len(preserved), "manifestCounts": list(manifest_counts.values())}))
