"""Validate additive evidence integrity offline; never invoke a service or old verifier."""

import hashlib
import json
import re
import subprocess
from datetime import datetime, timezone
from pathlib import Path


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
OLD = HERE.with_name("ext-parties-1-operational-source-follow-up-2026-10-30")
checks = []


def load(path):
    return json.loads(Path(path).read_text())


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def check(condition, description):
    if not condition:
        raise AssertionError(description)
    checks.append(description)


def verify_reference(reference):
    check(digest(reference["path"]) == reference["sha256"], "reference: " + reference["path"])


before = load(HERE / "preservation-before.json")
check(not before["priorHistoricalDigestComparison"], "no inherited historical drift at start")
for name, expected in before["files"].items():
    check(digest(name) == expected, "protected file: " + name)
document = before["additiveDocument"]
doc = Path(document["path"])
raw = doc.read_bytes()
check(hashlib.sha256(raw[:document["prefixBytes"]]).hexdigest() == document["prefixSha256"], "entire earlier document remains exact prefix")
check(len(raw) > document["prefixBytes"], "document is additive")

old_manifest_counts = {}
for folder in [OLD, OLD.with_name("ext-parties-1-operational-continuation-2026-10-30")]:
    count = 0
    for line in (folder / "SHA256SUMS").read_text().splitlines():
        expected, name = line.split(None, 1)
        check(digest(folder / name.strip()) == expected, "retained packet manifest: " + name)
        count += 1
    old_manifest_counts[str(folder)] = count

source_manifest_counts = {}
for name in load(OLD / "validation.json")["originalManifestCounts"]:
    manifest_path = Path(name)
    manifest = load(manifest_path)
    members = manifest.get("files", manifest)
    for member, expected in members.items():
        path = Path(member)
        path = path if path.is_absolute() else manifest_path.parent / path
        check(digest(path) == expected, "original source manifest: " + str(path))
    source_manifest_counts[name] = len(members)

plan = load(HERE / "recovery-proposal-continuation.json")
prior = load(OLD / "recovery-proposal-continuation.json")
for reference in plan["inheritsEntireSourceFollowUp"].values():
    if isinstance(reference, dict):
        verify_reference(reference)
verify_reference(plan["inheritsEntireProposal"])
verify_reference(plan["window"]["source"])
scope = load(plan["window"]["source"]["path"])
original = load(plan["inheritsEntireProposal"]["path"])
changed = {"schema", "preparedAtUtc", "status", "window", "preservation", "effects", "nextAction"}
for name, value in prior.items():
    if name not in changed:
        check(plan[name] == value, "inherited requirement: " + name)
for name, value in prior["preservation"].items():
    if name != "currentIdentityObservation":
        check(plan["preservation"][name] == value, "inherited preservation requirement: " + name)
check(plan["preservation"]["priorIdentityObservation"] == prior["preservation"]["currentIdentityObservation"], "earlier dated identity preserved")
check(plan["window"]["proposedWindow"] == prior["window"]["proposedWindow"] == scope["proposedWindow"], "exact fixed window unchanged")
check(plan["scopeSha256"] == scope["scopeSha256"] == "ef4a6896ed192137c4394cf5ef177ace1ce43ddf27a632731cf891f1c80cfbe3", "exact scope digest")
check(plan["runId"] == scope["runId"] == "9deb3d3afbbf42d39686abc30be4f64b", "exact run ID")
check(original["authority"]["operations"] == scope["nativeOwnerOperations"] and len(scope["nativeOwnerOperations"]) == 9, "all nine original operations retained")
for field in ["keyPlan", "installedWorkloadImages", "failureContainment"]:
    check(original[field] == scope[field], "original exact " + field)
check(plan["otherPendingInputsInherited"] == original["pendingInputs"], "every original pending input retained")
deadline = datetime.fromisoformat(plan["window"]["proposedWindow"]["exclusiveDeadline"])
checked = datetime.fromisoformat(plan["window"]["checkedAtUtc"])
check(deadline.isoformat() == "2026-10-30T16:53:50+00:00", "fixed exclusive deadline")
check(plan["window"]["expiredAtCheck"] == (checked >= deadline), "dated expiry classification")
check(plan["window"]["statusAtCheck"] == ("ExpiredIneligible" if checked >= deadline else "WithinFixedWindow"), "dated window status")
check(not plan["window"]["automaticRebaseOrExtensionAllowed"] and not plan["window"]["admissionAllowed"], "no extension, rebase or admission")
check(plan["window"]["scopeIneligibleForEffects"], "no effect eligibility inferred from open window")
check(plan["liveStopped"] and plan["fullCustodyStopped"] and not plan["executionAllowed"], "Live, custody and recovery stopped")
check(not plan["qualificationAllowed"] and not plan["productionBackendAllowed"], "no qualification or production backend")
check(plan["constraints"]["capEngineerDays"] == 2 and plan["constraints"]["newInfrastructureSpend"] == 0, "cap and zero spend unchanged")
segments = plan["preservation"]["separatelyRequiredHistoricalSegments"]
check([(x["fromResourceVersion"], x["throughResourceVersion"]) for x in segments] == [("36774577", "37223806"), ("37223806", "37671992"), ("37671992", "38120141")], "three separate Secret transitions")
check(plan["preservation"]["coverageThroughActualCurrentAtAdmissionRequired"], "complete history through actual admission revision required")
check(plan["preservation"]["futureAdmissionRequiresFreshActualIdentityAndCompleteHistory"], "new metadata is no admission substitute")
check(plan["sourceDelta"]["newRecordsAdmitted"] == 0 and plan["sourceDelta"]["ownerSuppliedConcreteRecords"] == 0, "no placeholder accepted as owner evidence")
for reference in plan["sourceDelta"]["sources"].values():
    verify_reference(reference)
reads = load(HERE / "system-secret-metadata-read-only.json")
verify_reference(plan["preservation"]["currentIdentityObservation"]["source"])
check(reads["exitCode"] == 0 and reads["argv"] == load(OLD / "kubernetes-read-only.json")[0]["argv"], "one exact selected-field existing GET")
check(reads["stdout"].strip().split("\t") == ["kubesphere-sa-token", "a97fa037-798b-46b6-9232-76e600480142", "38120141"], "current recorded Secret identity")
for effect, count in plan["effects"].items():
    check(count == (1 if effect == "kubernetesMetadataGetCommands" else 0), "effect count: " + effect)

observations = load(HERE / "source-observations.json")
for name in ["canonicalOwnerForm", "canonicalOwnerFormSnapshot"]:
    verify_reference(observations[name])
verify_reference(observations["deputyWorkTracking"]["snapshot"])
check(observations["deputyWorkTracking"]["snapshot"]["sha256"] == observations["deputyWorkTracking"]["source"]["sha256"], "dated mutable tracker snapshot retained")
for reference in observations["priorSourceFingerprintContinuity"]:
    if reference["path"] != observations["deputyWorkTracking"]["source"]["path"]:
        check(reference["matchesEarlierObservation"] and digest(reference["path"]) == reference["currentSha256"] == reference["priorSha256"], "retained source continuity: " + reference["path"])
verify_reference(observations["agentsRegister"]["source"])
check(not observations["agentsRegister"]["historicalMismatchReconciled"], "register mismatch remains unresolved")
findings = load(HERE / "evidence-findings.json")
verify_reference(findings["proposal"])
check(findings["allInheritedBlockersRetained"] and findings["effects"] == plan["effects"], "findings preserve all blockers and effect limits")
for target in re.findall(r"\]\(([^)]+)\)", doc.read_text()):
    if "://" in target:
        continue
    path = Path(target.split("#", 1)[0])
    path = path if path.is_absolute() else doc.parent / path
    if path.resolve() != (HERE / "validation.json").resolve():
        check(path.exists(), "local document link: " + target)
for path in HERE.glob("*.json"):
    if path.name != "validation.json":
        load(path)
        check(True, "parse JSON: " + path.name)
for path in [doc, *HERE.glob("*.json"), Path(__file__)]:
    check(all(line.rstrip(" \t") == line for line in path.read_text().splitlines()), "no trailing whitespace: " + path.name)
argv = ["git", "-c", "core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol", "diff", "--check", "--", str(doc.relative_to(ROOT))]
result = subprocess.run(argv, cwd=ROOT, capture_output=True, text=True, check=False)
check(result.returncode == 0 and not result.stdout and not result.stderr, "tracked document git whitespace check")
now = datetime.now(timezone.utc)
expired = now >= deadline
receipt = {
    "verifiedAtUtc": now.isoformat(), "passed": True, "checksPassed": len(checks),
    "historicalFilesPreserved": len(before["files"]), "earlierDocumentPrefixPreserved": True,
    "earlierPacketManifestCounts": old_manifest_counts, "originalSourceManifestCounts": source_manifest_counts,
    "canonicalOwnerFormUnchanged": True, "agentsRegisterUnchanged": True, "historicalRegisterMismatchReconciled": False,
    "allInheritedBlockersRetained": True, "noNewOwnerAttestationAuthenticated": True,
    "exclusiveDeadlineUtc": deadline.isoformat(), "windowStatusAtValidation": "ExpiredIneligible" if expired else "WithinFixedWindow",
    "existingScopeExpiredAndIneligibleAtValidation": expired, "admissionAllowed": False, "executionAllowed": False,
    "windowExtendedOrRebased": False, "effects": plan["effects"],
    "mutableTracker": {"retainedSnapshot": observations["deputyWorkTracking"]["snapshot"], "currentSha256AtValidation": digest(observations["deputyWorkTracking"]["source"]["path"]), "treatment": "Snapshot integrity checked; current equality of a concurrently editable tracker is not an acceptance condition."},
    "whitespaceCommand": {"argv": argv, "exitCode": result.returncode, "stdout": result.stdout, "stderr": result.stderr, "interpretation": "Recognize the CRLF line endings required by tracked .editorconfig, matching the earlier retained whitespace checks. No repository configuration changed."},
    "earlierFailedAttemptReference": "validation-attempts.json",
    "command": "python3 -B " + str(Path(__file__).relative_to(ROOT)),
    "limit": "Offline integrity/consistency plus retained receipt of one selected UID/RV GET only. No owner attestation, complete historical write evidence, current grant, independent currentness, copy disposal, recovery or qualification established. Prior receipts are unchanged."
}
(HERE / "validation.json").write_text(json.dumps(receipt, indent=2) + "\n", newline="\r\n")
print(json.dumps({key: receipt[key] for key in ["passed", "checksPassed", "historicalFilesPreserved", "windowStatusAtValidation", "admissionAllowed", "executionAllowed"]}))
