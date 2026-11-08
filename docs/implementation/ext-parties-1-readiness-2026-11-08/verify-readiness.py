#!/usr/bin/env python3
"""Read-only readiness/preservation checks; never invokes a runtime or owner verifier."""

import hashlib
import json
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path


folder = Path(__file__).resolve().parent
root = folder.parents[2]
evidence = json.loads((folder / "evidence.json").read_text())
application_path = folder / "applied-change.json"
application = json.loads(application_path.read_text()) if application_path.is_file() else None
checks = []


def sha(data):
    return hashlib.sha256(data).hexdigest()


def check(name, passed, detail=None):
    checks.append({"name": name, "passed": bool(passed), "detail": detail})


drift = []
authorized_addition_valid = False
for relative, expected in evidence["protectedInputsSha256"].items():
    path = root / relative
    if application and relative == application["path"] and path.is_file():
        raw = path.read_bytes()
        inserted = application["insertedUtf8"].encode()
        position = application["insertedAfterByte"]
        original = raw[:position] + raw[position + len(inserted):]
        authorized_addition_valid = (
            position == 1 and raw[position:position + len(inserted)] == inserted
            and sha(raw) == application["afterSha256"]
            and sha(original) == expected == application["beforeSha256"]
            and json.loads(raw)[application["addedPropertyName"]] == application["addedProperty"]
        )
        if not authorized_addition_valid:
            drift.append(relative)
    elif not path.is_file() or sha(path.read_bytes()) != expected:
        drift.append(relative)
check("48 protected inputs preserved, including exact original owner-form bytes", len(evidence["protectedInputsSha256"]) == 48 and not drift, drift)

if application:
    decision_path = folder / "applied-decision.json"
    decision = json.loads(decision_path.read_text())
    reference = application["addedProperty"]["appliedDecisionReference"]
    check("only the exact authorized owner metadata addition is present", authorized_addition_valid)
    check("canonical owner form binds the actual applied decision", Path(reference["path"]).resolve() == decision_path and reference["sha256"] == sha(decision_path.read_bytes()))
    check("single-contributor direction retains unmet production separation", decision["decisions"]["OD-1"]["status"] == "SingleContributorWorkModelApplied"
          and decision["decisions"]["OD-1"]["differentHumanDeputyAvailable"] is False
          and decision["decisions"]["OD-1"]["productionSeparationSatisfied"] is False
          and decision["guardState"]["distinctDeputyRequirementWaived"] is False)
    check("conditional design direction binds unchanged exact proposal bytes", decision["decisions"]["OD-3"]["effectiveDesignRatification"] is False
          and all(sha((root / "_bmad-output/specs/spec-ext-parties-1-contract-amendment" / name).read_bytes()) == expected
                  for name, expected in decision["decisions"]["OD-3"]["exactProposalSha256"].items()))
    check("applied direction leaves production and delivery gates closed", all(decision["guardState"][key] is False for key in
          ["implementationReady", "available", "liveQualified", "installationAuthorized", "qualificationAuthorized"])
          and decision["guardState"]["productionTasksRemainOpen"] is True
          and decision["decisions"]["OD-2"]["feasibilityReopened"] is False
          and decision["decisions"]["OD-4"]["replacementAccepted"] is False)
    check("original validation receipt unchanged", sha((folder / "validation.json").read_bytes()) == application["historicalValidationSha256"])

prior = evidence["preexistingBranchB"]
branch = (root / prior["path"]).read_bytes()
check("pre-existing user spec bytes retained as exact prefix", sha(branch[:prior["length"]]) == prior["sha256"])
frozen = branch.split(b"<frozen-after-approval", 1)[1].split(b"</frozen-after-approval>", 1)[0]
check("frozen intent unchanged", sha(frozen) == prior["frozenSha256"])
branch_text = branch.decode()
check("both production tasks remain open and status in-progress", branch_text.count("- [ ] ") == 2 and "status: 'in-progress'" in branch_text)

handoff = (root / evidence["originalHandoff"]["path"]).read_bytes()
pattern = rb"\n<!-- readiness-reconciliation-2026-11-08:start -->.*?<!-- readiness-reconciliation-2026-11-08:end -->\n"
old_handoff, overlays = re.subn(pattern, b"", handoff, flags=re.S)
check("historical handoff bytes retained outside one overlay", overlays == 1 and sha(old_handoff) == evidence["originalHandoff"]["sha256"])

text = (folder / "README.md").read_text()
check("all eight gates reconciled in order", re.findall(r"^\| (G0[1-8]) —", text, re.M) == [f"G{i:02}" for i in range(1, 9)])
check("four staged owner decisions", re.findall(r"^\| \*\*(OD-[1-4]) —", text, re.M) == [f"OD-{i}" for i in range(1, 5)])
check("all R1-R10 obligations mapped", all(re.search(rf"\bR{i}\b", text) for i in range(1, 11)))
check("contract bounds and strict/installed separation retained", all(value in text for value in [
    "31,536,000", "E+900", "E+2,592,000", "E+2,592,900", "<=100 ms", "<=1 s",
    "party-actor-retention-v1", "party-actor-history-v1", "binding-effective-at", "V1 strict",
    "P-01–P-10", "F1/F2", "Level 4", "Level 5", "OQ-5.4-1",
    "56cb4401c4179d431385ad3d931fefa402e4fc5a", "2026-10-08",
]))

owner_path = root / "../platform/docs/implementation/story-5-4-coupled-delivery-2026-10-25/owner-decisions.json"
owner = json.loads(owner_path.read_text())
null_changes = []
for pointer, expected in evidence["ownerFormNulls"].items():
    current = owner
    for part in pointer[2:].split("/"):
        current = current[part]
    if current is not None or expected is not None:
        null_changes.append(pointer)
check("17 selected owner returns remain null", len(evidence["ownerFormNulls"]) == 17 and not null_changes, null_changes)
check("owner installation/qualification/availability guards stay false", all(owner[key] is False and expected is False for key, expected in evidence["ownerFormGuards"].items()))
check("no implementation/availability/qualification claim", all(evidence[key] is False for key in ["implementationReady", "available", "liveQualified", "newRuntimeTestsRun"]))

links = []
scoped_texts = [(folder, text), ((root / prior["path"]).parent, branch[prior["length"]:].decode()),
                ((root / evidence["originalHandoff"]["path"]).parent, re.search(pattern, handoff, flags=re.S).group().decode())]
if application:
    scoped_texts.append((folder, (folder / "applied-direction.md").read_text()))
for base, content in scoped_texts:
    for target in re.findall(r"\]\(([^)]+)\)", content):
        if ":" not in target and not (base / target.split("#")[0]).exists():
            links.append(target)
check("new local Markdown links resolve", not links, links)

bad_whitespace = []
artifact_names = ["README.md", "evidence.json", "verify-readiness.py"]
if application:
    artifact_names += ["applied-direction.md", "applied-decision.json", "applied-change.json"]
for name in artifact_names:
    raw = (folder / name).read_bytes()
    if b"\n" in raw.replace(b"\r\n", b"") or not raw.endswith(b"\r\n"):
        bad_whitespace.append(name)
    if any(line != line.rstrip() for line in raw.decode().splitlines()):
        bad_whitespace.append(name + ": trailing whitespace")
check("new artifacts use CRLF and clean whitespace", not bad_whitespace, bad_whitespace)

command = ["git", "-c", "core.whitespace=cr-at-eol", "diff", "--check"]
result = subprocess.run(command, cwd=root, capture_output=True, text=True)
check("tracked whitespace check", result.returncode == 0, {"command": command, "exitCode": result.returncode, "output": result.stdout + result.stderr})

report = {
    "checkedAtUtc": datetime.now(timezone.utc).isoformat(),
    "passed": all(item["passed"] for item in checks),
    "checkCount": len(checks),
    "checks": checks,
    "outputHashes": {str(path.relative_to(root)): sha(path.read_bytes()) for path in [
        folder / "README.md", folder / "evidence.json", Path(__file__).resolve(),
        root / prior["path"], root / evidence["originalHandoff"]["path"],
    ]},
    "scope": "Documentation/preservation only; no runtime, provider, credential, Local, LiveReadiness, Live or consuming command.",
}
if application:
    for name in ["applied-direction.md", "applied-decision.json", "applied-change.json"]:
        report["outputHashes"][str((folder / name).relative_to(root))] = sha((folder / name).read_bytes())
print(json.dumps(report, indent=2))
sys.exit(0 if report["passed"] else 1)
