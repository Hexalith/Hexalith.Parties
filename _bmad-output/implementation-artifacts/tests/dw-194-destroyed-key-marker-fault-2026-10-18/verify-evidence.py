"""Verify retained DW-194 source identities, results and text formatting from the repo root."""

import hashlib
import json
from pathlib import Path
import subprocess


folder = Path(__file__).resolve().parent
validation = json.loads((folder / "validation.json").read_text())
red = json.loads((folder / "red-summary.json").read_text())

for name, expected in validation["source_sha256"].items():
    assert hashlib.sha256(Path(name).read_bytes()).hexdigest() == expected, name

red_tests = (folder / red["test_source_artifact"]).read_bytes()
test_path = "tests/Hexalith.Parties.Security.Tests/PartyPayloadProtectionMarkerFailureTests.cs"
assert hashlib.sha256(red_tests).hexdigest() == red["source_sha256"][test_path]
source_path = "src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs"
baseline_source = subprocess.check_output(["git", "show", validation["baseline_commit"] + ":" + source_path])
assert hashlib.sha256(baseline_source).hexdigest() == red["source_sha256"][source_path]

post_review = [entry for entry in validation["commands"] if entry.get("phase") == "post-review" and "summary" in entry]
assert [int(entry["summary"][0]["total"]) for entry in post_review] == [20, 238]
for entry in post_review:
    assert entry["exit_code"] == 0
    for summary in entry["summary"]:
        assert all(int(summary[key]) == 0 for key in ("failed", "skipped", "errors", "not-run"))
        assert summary["total"] == summary["passed"]

paths = [Path(name) for name in validation["source_sha256"]]
paths += [folder.parent.parent / "spec-dw-194-destroyed-key-marker-fault.md"]
paths += [path for path in folder.iterdir() if path.is_file()]
for path in paths:
    raw = path.read_bytes()
    assert raw.endswith(b"\r\n"), path.name
    assert b"\n" not in raw.replace(b"\r\n", b""), path.name
    assert all(line == line.rstrip(" \t") for line in raw.decode("utf-8").splitlines()), path.name

print("Source hashes, original red sources, final results and CRLF whitespace checks pass.")
