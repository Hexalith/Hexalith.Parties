"""Reproduce the two bounded deferred-report dispositions; no runtime qualification."""

import hashlib
import json
from pathlib import Path
import re
import subprocess


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
CATALOG = "references/Hexalith.Builds/Props/Directory.Packages.props"


def git(*arguments):
    return subprocess.check_output(["git", *arguments], cwd=ROOT, text=True).strip()


def verify():
    operative = git(
        "ls-files", "--", ".github/workflows", "scripts", ".config", "module",
        "Directory*.props", "Directory*.targets", "global.json",
    ).splitlines()
    obsolete = "scripts/validate-runtime-toolchain-evidence.py"
    assert not git("ls-files", "--", obsolete, "*runtime-toolchain*")
    forbidden = [
        "validate-runtime-toolchain-evidence",
        "13.5.0-preview.1.260825-0345",
        "13.6.0-preview.1.261001-0243",
    ]
    hits = [name for name in operative if any(
        value in (ROOT / name).read_text() for value in forbidden
    )]
    assert not hits, hits
    ci = (ROOT / ".github/workflows/ci.yml").read_text()
    assert "solution: Hexalith.Parties.Standalone.slnx" in ci
    assert "dotnet tool run hexalith-module -- validate" in ci
    tools = json.loads((ROOT / ".config/dotnet-tools.json").read_text())
    tool = tools["tools"]["hexalith.builds.module.cli"]
    assert tool["commands"] == ["hexalith-module"]
    assert tool["version"] and tool["rollForward"] is False
    helper = "tests/Hexalith.Parties.IntegrationTests/Identity/TestOnlyIdentityHistoryCustody.cs"
    assert git("ls-files", "--", helper) == helper
    source = (ROOT / helper).read_text()
    assert not re.search(r"\b(?:pending|destroyed|acknowledged)\s*:", source)
    assert "new UnitStatus(" in source
    # Reproduce current report checks while retaining the original catalog/graph observation.
    # Re-evaluating MSBuild here could silently substitute a different current catalog.
    recorded = json.loads((HERE / "disposition-verification.json").read_text())
    historical = recorded["historical_inputs"]
    if set(historical) != {CATALOG}:
        raise ValueError("Only the historical selected Builds catalog may use a snapshot")
    catalog = historical[CATALOG]
    if catalog["snapshot"] != "historical-inputs/" + catalog["sha256"] + ".catalog":
        raise ValueError("Historical catalog must use its hash-named archive")
    catalog_bytes = (HERE / catalog["snapshot"]).read_bytes()
    if hashlib.sha256(catalog_bytes).hexdigest() != catalog["sha256"] \
            or catalog["sha256"] != recorded["input_sha256"][CATALOG]:
        raise ValueError("Historical catalog snapshot SHA-256 mismatch")
    inputs = [*operative, helper]
    hashes = {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in inputs}
    hashes[CATALOG] = catalog["sha256"]
    return {
        "head": git("rev-parse", "HEAD"),
        "checks": {
            "obsolete_validator_and_packet_not_tracked": "Pass",
            "operative_files_do_not_reference_obsolete_validator_or_preview": "Pass",
            "current_ci_and_pinned_module_tool_identified": "Pass",
            "tracked_helper_has_no_incorrectly_cased_named_arguments": "Pass",
            "historical_effective_package_properties_recorded": "Pass",
        },
        "helper_last_change": git("log", "-1", "--format=%H", "--", helper),
        "module_tool": tool,
        "graph_command": recorded["graph_command"],
        "graph": recorded["graph"],
        "graph_scope": recorded["graph_scope"],
        "input_sha256": hashes,
        "historical_inputs": historical,
        "limits": recorded["limits"],
    }


if __name__ == "__main__":
    print(json.dumps(verify(), indent=2))
