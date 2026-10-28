"""Run final affected UI regression checks using the recorded retained-map fallback."""

from collections import Counter
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
WRAPPER = ROOT / "_bmad-output/implementation-artifacts/tests/five-legacy-recovery-and-evidence-2026-10-27/build-source.py"
WORKSPACE = "/tmp/five-reconciliation-retained-map-20261028"
MAPPED = ["python3", str(WRAPPER), "--workspace", WORKSPACE, "--"]


def main():
    records = []
    producer = "references/Hexalith.FrontComposer"
    commands = [
        ("primary-retained-ui-build", ["dotnet", "build", "tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj", "-c", "Debug", "-m:1"]),
        ("primary-retained-ui-tests", ["dotnet", "tests/Hexalith.Parties.UI.Tests/bin/Debug/net10.0/Hexalith.Parties.UI.Tests.dll", "-xml", str(HERE / "primary-retained-ui-tests.xml")]),
        ("primary-mapped-ui-build", [*MAPPED, "dotnet", "build", "tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj", "-c", "Debug", "-m:1"]),
        ("primary-mapped-ui-tests", [*MAPPED, "dotnet", "tests/Hexalith.Parties.UI.Tests/bin/Debug/net10.0/Hexalith.Parties.UI.Tests.dll", "-xml", str(HERE / "primary-mapped-ui-tests.xml")]),
        ("primary-producer-build", [*MAPPED[:-1], "--cwd", producer, "--", "dotnet", "build", "tests/Hexalith.FrontComposer.Shell.Tests/Hexalith.FrontComposer.Shell.Tests.csproj", "-c", "Debug", "-m:1"]),
        ("primary-producer-tests", ["dotnet", producer + "/tests/Hexalith.FrontComposer.Shell.Tests/bin/Debug/net10.0/Hexalith.FrontComposer.Shell.Tests.dll", "-class", "Hexalith.FrontComposer.Shell.Tests.Services.FcOptimisticReconcilerTests", "-xml", str(HERE / "primary-producer-tests.xml")]),
        ("primary-audit-controls", ["python3", str(HERE / "test_audit_matrix.py")]),
        ("primary-audit-controls-optimized", ["python3", "-O", str(HERE / "test_audit_matrix.py")]),
        ("primary-warning-guard", ["bash", "scripts/check-no-warning-override.sh"]),
    ]
    try:
        for name, command in commands:
            log = HERE / (name + ".txt")
            with log.open("w") as output:
                result = subprocess.run(command, cwd=ROOT, stdout=output, stderr=subprocess.STDOUT, timeout=300)
            record = {"name": name, "command": command, "cwd": str(ROOT), "exit_code": result.returncode,
                      "log": log.name, "log_sha256": hashlib.sha256(log.read_bytes()).hexdigest()}
            xml = HERE / (name + ".xml")
            if name.endswith("-tests") and xml.exists():
                record["xml"] = xml.name
                record["xml_sha256"] = hashlib.sha256(xml.read_bytes()).hexdigest()
                record["results"] = dict(Counter(test.attrib["result"] for test in ET.parse(xml).iter("test")))
            records.append(record)
            print(name, result.returncode, record.get("results", {}), flush=True)
            if result.returncode:
                print("\n".join(log.read_text().splitlines()[-30:]), flush=True)
                raise SystemExit(result.returncode)
    finally:
        (HERE / "primary-verification.json").write_text(json.dumps(records, indent=2) + "\n")


if __name__ == "__main__":
    main()
