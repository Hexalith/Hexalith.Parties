"""Audit this additive evidence packet offline; perform no service operations."""

import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
checks = []


def load(path):
    return json.loads(path.read_text())


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check(condition, description):
    if not condition:
        raise AssertionError(description)
    checks.append(description)


before = load(HERE / "preservation-before.json")
check(load(HERE / "whitespace-checks.json")["passed"], "git whitespace checks have no diagnostics")
for name, expected in before["files"].items():
    check(digest(Path(name)) == expected, "protected bytes: " + name)
document = before["additiveDocument"]
doc = Path(document["path"])
check(hashlib.sha256(doc.read_bytes()[:document["prefixBytes"]]).hexdigest() == document["prefixSha256"], "entire earlier document remains exact prefix")
check(doc.stat().st_size > document["prefixBytes"], "document continuation is additive")

source_drifts = load(HERE / "source-drift-at-validation.json")["sources"]
for ref in load(HERE / "source-references.json")["files"]:
    if "mutableSourceDriftReference" in ref:
        recorded = next(item for item in source_drifts if item["path"] == ref["path"])
        check(recorded["earlierObservationSha256"] == ref["sha256"], "earlier mutable source fingerprint retained")
        snapshot = recorded["retainedSnapshot"]
        check(digest(Path(snapshot["path"])) == snapshot["sha256"] == recorded["recapturedSha256"], "later source snapshot retained independently")
        initial_lines = load(HERE / "source-observations.json")["deputyWorkTracking"]["lines"]
        check(len(recorded["relevantLinesAtRecapture"]) == 2 and recorded["relevantLinesAtRecapture"] == initial_lines, "both deputy entries unchanged in later snapshot")
    else:
        check(digest(Path(ref["path"])) == ref["sha256"], "observed source: " + ref["path"])

plan = load(HERE / "recovery-proposal-continuation.json")
original = load(Path(plan["inheritsEntireProposal"]["path"]))
scope = load(Path(plan["window"]["source"]["path"]))
check(digest(Path(plan["inheritsEntireProposal"]["path"])) == plan["inheritsEntireProposal"]["sha256"], "full original proposal inherited unchanged")
check(plan["window"]["proposedWindow"] == original["window"] == scope["proposedWindow"], "exact fixed recovery window")
checked_at = datetime.fromisoformat(plan["window"]["checkedAtUtc"])
start = datetime.fromisoformat(scope["proposedWindow"]["executionNotBefore"])
deadline = datetime.fromisoformat(scope["proposedWindow"]["exclusiveDeadline"])
expected_status = "WithinFixedWindow" if start <= checked_at < deadline else "OutsideFixedWindow"
check(plan["window"]["statusAtCheck"] == expected_status, "deadline classification at original observation time")
check(deadline.isoformat() == "2026-10-30T16:53:50+00:00", "original exclusive deadline retained")
check(plan["scopeSha256"] == original["approval"]["scopeSha256"], "exact scope digest")
check(plan["runId"] == original["runId"], "exact original run identifier")
check(original["authority"]["operations"] == scope["nativeOwnerOperations"], "all nine original owner operation bodies unchanged")
check(original["keyPlan"] == scope["keyPlan"], "original key mappings and technical windows unchanged")
check(original["installedWorkloadImages"] == scope["installedWorkloadImages"], "original workload images unchanged")
check(original["failureContainment"] == scope["failureContainment"], "original containment retained")
check(plan["otherPendingInputsInherited"] == original["pendingInputs"], "all original pending inputs retained")
check(plan["liveStopped"] and plan["fullCustodyStopped"] and not plan["executionAllowed"], "all execution stops retained")
check(not plan["qualificationAllowed"] and not plan["productionBackendAllowed"], "no backend implementation or qualification")
check(plan["constraints"]["capEngineerDays"] == 2 and plan["constraints"]["newInfrastructureSpend"] == 0, "approved time and spend ceilings retained")
check(not plan["constraints"]["spentEffortInferred"], "no invented effort allocation")
check(all(plan["appointments"][k] is None for k in ["assessmentImplementer", "accountableRecoveryLead", "differentQualifiedHumanDeputy", "existingAllocationReference", "appointmentSource"]), "no inferred appointments")

preservation = plan["preservation"]
segments = preservation["separatelyRequiredHistoricalSegments"]
check([(s["fromResourceVersion"], s["throughResourceVersion"]) for s in segments] == [("36774577", "37223806"), ("37223806", "37671992"), ("37671992", "38120141")], "three separate historical transitions retained")
for segment in segments:
    check(all(segment[k] is None for k in ["authenticWriteSource", "authenticatedDisposition", "completeIntervalCovered"]), "no invented historical write coverage")
check(preservation["currentIdentityObservation"]["resourceVersion"] == "38120141", "current observed revision retained separately")
check(not preservation["oldTemplateAdmitsCurrentRevision"] and not preservation["templateOrBaselineEdited"], "old template not silently admitted or edited")
check(len(preservation["memoriesExactDifferencesInherited"]) == 3, "all three separate Memories differences retained")
check(all(preservation[k] is None for k in ["authenticatedSecretDisposition", "authenticatedMemoriesDisposition", "authenticatedRegisterDisposition", "authenticatedPreservationDecision"]), "separate missing authenticated dispositions retained")
check(not preservation["historicalRegisterMismatchReconciled"], "register equality not substituted for historical reconciliation")
metadata = plan["metadataCredential"]
check(metadata["admittedCurrentCredentialReference"] is None and metadata["authenticatedProvenance"] is None, "no fabricated current metadata credential or provenance")
check(metadata["datedSuccess"]["httpStatuses"] == [200] * 4 and metadata["datedSuccess"]["temporaryGrantReversed"], "dated four successes and reversal retained")
check(not metadata["datedSuccess"]["currentOrFutureReadTransportInferred"], "dated success not promoted to current transport")
check(metadata["providerCallsThisFollowUp"] == 0 and metadata["deniedProbesRepeated"] == 0 and not metadata["temporaryGrantReapplied"], "provider probes and grant not repeated")
check(plan["snapshotAndCopies"]["ownerProducedCurrentInventory"] is None and plan["snapshotAndCopies"]["validExistingReadOnlyTransport"] is None, "copy inventory and transport blockers retained")
check(plan["authorityAndCustody"]["qualifiedAlreadyOperatedAuthorityTimeMechanism"] is None and not plan["authorityAndCustody"]["backupCorrectionAllowed"] and not plan["authorityAndCustody"]["capacityContinuationAllowed"], "independent authority stop retained")
for effect, count in plan["effects"].items():
    check(count == (3 if effect == "kubernetesMetadataGetCommands" else 0), "effect scope: " + effect)
reads = load(HERE / "kubernetes-read-only.json")
check(len(reads) == 3, "three Kubernetes observations only")
for record in reads:
    check(record["exitCode"] == 0 and "get" in record["argv"] and any("jsonpath=" in arg for arg in record["argv"]), "successful selected read-only metadata GET")
check(reads[0]["stdout"].split("\t")[1:] == ["a97fa037-798b-46b6-9232-76e600480142", "38120141"], "Secret identity matches actual receipt")
check(not any("--audit-" in line for line in reads[2]["stdout"].splitlines()), "current selected API-server audit flag observation")
snapshot_readers = [line.split("\t") for line in reads[1]["stdout"].splitlines() if "openbao-snapshots" in line]
check(len(snapshot_readers) > 0 and all(row[3] == "Succeeded" for row in snapshot_readers), "snapshot-PVC users observed completed only")

manifest_counts = {}
secrets = Path("/home/administrator/hexalith-secrets-evidence/ext-secrets-1")
for relative in ["expired-hmac-recovery-scope-20261029T165350Z-final", "expired-hmac-recovery-run-20261029-approved", "expired-hmac-recovery-run-20261029-approved/metadata-access-correction-20261029", "expired-hmac-recovery-run-20261029-approved/metadata-access-application-20261029-approved"]:
    manifest_path = secrets / relative / "evidence-manifest.json"
    manifest = load(manifest_path)
    files = manifest.get("files", manifest)
    for name, expected in files.items():
        target = Path(name) if Path(name).is_absolute() else manifest_path.parent / name
        check(digest(target) == expected, "original manifest member: " + str(target))
    manifest_counts[str(manifest_path)] = len(files)

for target in re.findall(r"\]\(([^)]+)\)", doc.read_text()):
    if "://" in target:
        continue
    path = Path(target.split("#", 1)[0])
    path = path if path.is_absolute() else doc.parent / path
    if path.resolve() == (HERE / "validation.json").resolve():
        continue
    check(path.exists(), "local document link: " + target)
for path in HERE.glob("*.json"):
    if path.name != "validation.json":
        load(path)
        check(True, "parse JSON: " + path.name)
for path in [doc, *HERE.glob("*.json"), Path(__file__)]:
    check(all(line.rstrip(" \t") == line for line in path.read_text().splitlines()), "no trailing whitespace: " + path.name)

result = {"verifiedAtUtc": datetime.now(timezone.utc).isoformat(), "passed": True, "checksPassed": len(checks), "historicalFilesPreserved": len(before["files"]), "earlierDocumentPrefixPreserved": True, "originalManifestCounts": manifest_counts, "canonicalOwnerFormUnchanged": True, "agentsRegisterUnchanged": True, "existingIndexEditUnchanged": True, "allInheritedBlockersRetained": True, "windowStatusAtRecordedCheck": expected_status, "windowStatusAtValidation": "WithinFixedWindow" if start <= datetime.now(timezone.utc) < deadline else "OutsideFixedWindow", "exclusiveDeadlineUtc": deadline.isoformat(), "windowExtended": False, "effects": plan["effects"], "command": "python3 -B " + str(Path(__file__).relative_to(ROOT)), "limit": "Offline integrity and evidence consistency only. No attestation authenticity, complete historical coverage, current grant, independent currentness, all-copy disposal, recovery execution or qualification is established. Earlier validation receipt is unchanged; earlier mutable sprint-source drift is separately recorded."}
result["mutableSourceDrift"] = source_drifts
result["retainedMutableSourceSnapshotsVerified"] = len(source_drifts)
result["earlierFailedAttemptReference"] = "validation-attempts.json"
(HERE / "validation.json").write_text(json.dumps(result, indent=2) + "\n", newline="\r\n")
print(json.dumps({"passed": True, "checksPassed": len(checks), "historicalFilesPreserved": len(before["files"]), "originalManifestCounts": list(manifest_counts.values()), "windowStatusAtValidation": result["windowStatusAtValidation"]}))
