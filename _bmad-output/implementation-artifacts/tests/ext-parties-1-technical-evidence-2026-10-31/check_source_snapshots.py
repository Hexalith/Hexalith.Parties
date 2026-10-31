"""Compare retained bytes and reconstruct historical source text without executing it."""

import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile


HERE = Path(__file__).resolve().parent
EVIDENCE = Path("/home/administrator/hexalith-secrets-evidence/ext-secrets-1")
ORIGINAL = EVIDENCE / "expired-hmac-recovery-run-20261029-approved"
LATER = EVIDENCE / "provider-inventory-resume-20261029T175713722948Z"


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def ref(path):
    return {"path": str(path), "sha256": sha(path)}


def load(path):
    return json.loads(Path(path).read_text())


original_receipt = load(ORIGINAL / "verification.json")
original_manifest = load(ORIGINAL / "evidence-manifest.json")["files"]
later_receipt = load(LATER / "root-final-verification.json")
later_manifest = load(LATER / "final-evidence-sha256.json")
tested_pins = load(LATER / "tested-source-pins.json")
packet = {
    "schema": "EXT-PARTIES-1.retained-source-integrity.v1",
    "reason": "Explicit request to authenticate retained source snapshots after the historical executor worktree disappeared; existing test suites are not rerun.",
    "observedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "inputs": [ref(ORIGINAL / "verification.json"), ref(ORIGINAL / "evidence-manifest.json"), ref(LATER / "root-final-verification.json"), ref(LATER / "final-evidence-sha256.json"), ref(LATER / "tested-source-pins.json")],
    "originalSnapshot": [],
    "laterTestedSnapshot": [],
    "laterPreservedSnapshot": [],
    "laterFinalReconstruction": [],
    "historicalWorktreePaths": [],
    "currentSourceCandidates": [],
    "sourceImportedOrExecuted": False,
    "historicalTestsRerun": False,
    "currentSourceAdmission": False,
}
for relative, expected in original_receipt["finalTestedSourceSha256"].items():
    source = ORIGINAL / "final-tested-source" / relative
    manifest_key = "final-tested-source/" + relative
    packet["originalSnapshot"].append({"source": ref(source), "expectedSha256": expected, "matchesRecordedFingerprint": sha(source) == expected, "manifestEntryMatches": original_manifest[manifest_key] == sha(source)})
    tested = LATER / "tested-source" / relative
    packet["laterTestedSnapshot"].append({"source": ref(tested), "expectedSha256": tested_pins[relative], "matchesOwnGenerationPin": sha(tested) == tested_pins[relative], "manifestEntryMatches": later_manifest["tested-source/" + relative] == sha(tested), "equalsLaterFinalReceipt": sha(tested) == later_receipt["sourceSha256"][relative]})
    preserved = LATER / "preserved-source" / relative
    packet["laterPreservedSnapshot"].append({"source": ref(preserved), "expectedManifestSha256": later_manifest["preserved-source/" + relative], "matchesOwnGenerationPin": sha(preserved) == later_manifest["preserved-source/" + relative]})
    old = Path("/home/administrator/projects/hexalith/platform-ext-secrets-1-runtime-foundations-20261017") / relative
    packet["historicalWorktreePaths"].append({"path": str(old), "availableAsFile": old.is_file()})
    current = Path("/home/administrator/projects/hexalith/platform") / relative
    packet["currentSourceCandidates"].append({"source": ref(current), "expectedHistoricalFinalSha256": later_receipt["sourceSha256"][relative], "matchesHistoricalFinalReceipt": sha(current) == later_receipt["sourceSha256"][relative], "admittedForExecution": False})
with tempfile.TemporaryDirectory(prefix="ext-parties-source-integrity-") as temporary:
    for name in ["recover_expired_hmac.py", "test_recover_expired_hmac.py"]:
        relative = "eng/ext-secrets-1/" + name
        source = LATER / "preserved-source" / relative
        difference = LATER / ("review-final-" + name + ".diff")
        output = Path(temporary) / name
        argv = ["patch", "--batch", "--fuzz=0", "--output", str(output), str(source), str(difference)]
        result = subprocess.run(argv, capture_output=True, text=True, timeout=10)
        actual = sha(output) if output.is_file() else None
        packet["laterFinalReconstruction"].append({
            "base": ref(source), "difference": ref(difference),
            "differenceMatchesRecordedManifest": sha(difference) == later_manifest[difference.name],
            "argv": argv, "exitCode": result.returncode, "stdout": result.stdout, "stderr": result.stderr,
            "derivedSha256": actual, "expectedSha256": later_receipt["sourceSha256"][relative],
            "matchesFinalRecordedFingerprint": actual == later_receipt["sourceSha256"][relative],
            "outputTemporaryAndDeleted": True, "sourceExecuted": False,
        })
packet["finding"] = "Original four-file snapshot matches its 23+8-test generation. Later tested and preserved snapshots match their own recorded generations; their executor/test hashes differ from the final 60-test receipt. Applying each retained final-review diff to its preserved base with zero fuzz reconstructs exactly the two final fingerprints. All four files at the current Platform eng/ext-secrets-1 paths also match that final receipt. Historical worktree paths remain unavailable. These are integrity/location findings, not current operational admission, provenance of historical writes, or new test/qualification results."
(HERE / "source-snapshot-integrity.json").write_text(json.dumps(packet, indent=4) + "\n")
assert all(x["matchesRecordedFingerprint"] and x["manifestEntryMatches"] for x in packet["originalSnapshot"])
assert all(x["matchesOwnGenerationPin"] and x["manifestEntryMatches"] for x in packet["laterTestedSnapshot"])
assert all(x["matchesOwnGenerationPin"] for x in packet["laterPreservedSnapshot"])
assert all(x["exitCode"] == 0 and x["differenceMatchesRecordedManifest"] and x["matchesFinalRecordedFingerprint"] for x in packet["laterFinalReconstruction"])
assert all(x["matchesHistoricalFinalReceipt"] for x in packet["currentSourceCandidates"])
print(json.dumps({"originalSourceFilesMatched": 4, "laterTestedSourceFilesMatchedOwnGeneration": 4, "laterPreservedSourceFilesMatchedOwnGeneration": 4, "finalSourcesReconstructedAndMatched": 2, "currentCandidatesMatchingFinalReceipt": 4, "sourceExecuted": False, "currentSourceAdmission": False}))
