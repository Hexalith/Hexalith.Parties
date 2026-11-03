"""Reproduce the closed G5 prerequisite receipt; grants no provider parity."""

import hashlib
import json
from pathlib import Path
import re
import shlex
import subprocess
import xml.etree.ElementTree as ET


root = Path(__file__).resolve().parents[4]
baseline = "fcf7cbc86076ce43f0982e4959da9691feaf2d3a"


def git(*args):
    return subprocess.check_output(["git", *args], cwd=root, text=True).strip()


matrix = (root / "_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md").read_text()
row = next(line for line in matrix.splitlines() if line.startswith("| Payload protection engine package |"))
assert row.split("|")[3].strip() == "needs-additive-api"
assert "| I2/I19a policy-hook classification |" in matrix
classification = next(line for line in matrix.splitlines() if line.startswith("| I2/I19a policy-hook classification |"))
classification_decision = classification.split("|")[5].strip()
assert classification_decision.startswith("Pending;"), classification_decision
commands = re.findall(r"`(rg -n -F [^`]+|test ! -f [^`]+)`(\s*\(expected no matches\))?", row)
assert len(commands) == 32
results = []
for command, absent in commands:
    result = subprocess.run(shlex.split(command), cwd=root, capture_output=True, text=True)
    expected = 1 if absent else 0
    assert result.returncode == expected, (command, result.returncode, result.stderr)
    results.append({"command": command, "expected_exit": expected, "actual_exit": result.returncode})

story = (root / "_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md").read_text()
inventory = story.split("### File Ownership and Change Boundaries")[1].split("Likely UPDATE/rebind surfaces")[0]
paths = re.findall(r"^- `(src/[^`]+\.cs)`", inventory, re.M)
assert len(paths) == 24 and all((root / path).is_file() for path in paths)
assert (root / "tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs").is_file()
di = (root / "src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs").read_text()
markers = [
    "AddSingleton<IKeyStorageBackend, LocalDevKeyStorageBackend>()",
    "AddSingleton<PartyPayloadProtectionService>()",
    "AddSingleton<EventStorePartyPayloadProtectionAdapter>()",
    "AddSingleton<IEventPayloadProtectionService>(sp => sp.GetRequiredService<EventStorePartyPayloadProtectionAdapter>())",
]
assert all(marker in di for marker in markers)
retained_paths = paths + [
    "tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs",
    "src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs",
]
for path in retained_paths:
    original = subprocess.check_output(["git", "show", f"{baseline}:{path}"], cwd=root)
    assert (root / path).read_bytes() == original, f"Retained source differs from baseline: {path}"
core = ET.parse(root / "references/Hexalith.EventStore/src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj")
assert core.find(".//IsPackable").text == "false"
mapping_path = Path(__file__).with_name("source-mapping.json")
mapping = json.loads(mapping_path.read_text())
assert mapping["ContentHash"] == "85abceed351e6c74267f582bac8849146847f991a503657beb12c18ddff68f63"
identities = {}
for entry in mapping["Entries"]:
    dependency = entry["Identity"]
    if dependency == "Hexalith.Parties":
        assert Path(entry["Path"]) == root
        continue
    path = f"references/{dependency}"
    assert entry["Origin"] == "source" and Path(entry["Path"]) == root / path
    pin = git("ls-tree", "HEAD", path).split()[2]
    checkout = git("-C", path, "rev-parse", "HEAD")
    status = git("-C", path, "status", "--porcelain")
    assert pin == checkout and not status, (path, pin, checkout, status)
    identities[dependency] = {"root_gitlink": pin, "checkout": checkout, "clean": True}
selectors = {}
for path, expected in [
    ("Directory.Packages.props", "3.132.2"),
    ("references/Hexalith.Builds/Props/Directory.Packages.props", "3.132.1"),
]:
    selector = ET.parse(root / path).find(".//HexalithEventStoreVersion").text
    assert selector == expected, (path, selector)
    selectors[path] = selector
ledger = (root / "_bmad-output/implementation-artifacts/sprint-status.yaml").read_text()
assert "8-6-projection-and-query-sdk-migration: done" in ledger
assert "8-7-data-protection-extraction: blocked" in ledger
print(json.dumps({
    "date": "2026-11-03",
    "parties_commit": git("rev-parse", "HEAD"),
    "retention_baseline": baseline,
    "gate": "needs-additive-api",
    "story_status": "blocked",
    "policy_hook_classification": "pending",
    "policy_hook_decision": classification_decision,
    "source_identities": identities,
    "source_mapping_content_hash": mapping["ContentHash"],
    "source_mapping_file_sha256": hashlib.sha256(mapping_path.read_bytes()).hexdigest(),
    "package_selectors": selectors,
    "matrix_checks": results,
    "retained_files": {path: hashlib.sha256((root / path).read_bytes()).hexdigest() for path in paths},
    "baseline_unchanged_harness_and_di": {path: hashlib.sha256((root / path).read_bytes()).hexdigest() for path in retained_paths[-2:]},
    "local_di_markers": markers,
    "product_or_dual_provider_parity_credited": False,
}, indent=2))
