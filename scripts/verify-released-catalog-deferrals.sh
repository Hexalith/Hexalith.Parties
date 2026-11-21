#!/usr/bin/env bash
# Run the local released-package checks for DW-50, DW-58, DW-82, DW-100 and DW-168.
set -euo pipefail

if [[ $# != 1 || -z "$1" ]]; then
    printf 'Usage: bash %s NEW_RESULTS_DIRECTORY\n' "$0" >&2
    exit 2
fi

results="$(realpath -m -- "$1")"
repository="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
for tool in dotnet python3 run-with-temp; do
    if ! command -v "$tool" >/dev/null; then
        printf 'Required command not found: %s\n' "$tool" >&2
        exit 2
    fi
done
mkdir -- "$results"
cd -- "$repository"
# UI fitness tests resolve source files from the assembly's parent directories.
mkdir -p -- artifacts/j

TMPDIR="$repository/artifacts/j" run-with-temp bash -euo pipefail -s -- "$results" <<'JOB'
results=$1
artifacts="$TMPDIR/artifacts"

# Use the standalone checkout's catalog even inside a source-mapped shell.
unset HexalithBuildsRoot Hexalith1BuildPackageProps Hexalith2BuildPackageProps \
    Hexalith3BuildPackageProps HexalithVersionsLoaded HexalithSourceMappingMode \
    HexalithEventStoreVersion HexalithMemoriesVersion HexalithFrontComposerVersion \
    HexalithCommonsVersion HexalithTenantsVersion HexalithMcpCliVersion

run_tests() {
    local project=$1 lane=$2
    shift 2
    local assembly="$artifacts/bin/$project/release/$project.dll"
    dotnet build "tests/$project/$project.csproj" -c Release -m:1 \
        --disable-build-servers --artifacts-path "$artifacts" \
        -p:UseHexalithProjectReferences=false </dev/null 2>&1 | tee "$results/$lane-build.log"
    sha256sum "$assembly" > "$results/$lane-assembly.sha256"
    cp -- "${assembly%.dll}.deps.json" "$results/$lane-dependencies.json"
    dotnet "$assembly" -failSkips -result-xml "$results/$lane.xml" "$@" \
        </dev/null 2>&1 | tee "$results/$lane-tests.log"
}

core_classes=(
    Hexalith.Parties.Tests.Projections.PartyProcessingGenerationRecoveryTests
    Hexalith.Parties.Tests.Gateway.PartySdkQueryHandlerTests
    Hexalith.Parties.Tests.Projections.PartyLegacyReadModelBackfillTests
    Hexalith.Parties.Tests.Search.PartyMemorySourceInventoryTests
    Hexalith.Parties.Tests.Search.PartyMemoryRestClientRecoveryTests
    Hexalith.Parties.Tests.Search.PartyMemoryRecoveryTests
    Hexalith.Parties.Tests.Search.PartyMemoryRecoveryRegressionTests
)
core_selectors=()
for class in "${core_classes[@]}"; do
    core_selectors+=(-class "$class")
done

dotnet --version > "$results/dotnet-sdk.txt"
run_tests Hexalith.Parties.Tests core "${core_selectors[@]}"
run_tests Hexalith.Parties.Projections.Tests projections \
    -nomethod '*PublishedSdkPreservesLargeLegacyLiveAndRebuildCompatibilityAsync'
run_tests Hexalith.Parties.UI.Tests ui

python3 - "$results" "${core_classes[@]}" <<'PY'
from collections import Counter
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

results = Path(sys.argv[1])
required_classes = {
    "core": sys.argv[2:],
    "projections": ["Hexalith.Parties.Projections.Tests.Handlers.PartySdkProjectionHandlerTests"],
    "ui": [
        f"Hexalith.Parties.UI.Tests.{name}"
        for name in (
            "DataFreshnessIndicatorTests",
            "OptimisticReconcileTests",
            "ProjectionFreshnessCompositionTests",
            "ProjectionFreshnessFallbackTests",
            "ProjectionTransportPackageTests",
            "StatusLiveRegionTests",
        )
    ],
}
summary = {}
for lane, classes in required_classes.items():
    report = ET.parse(results / f"{lane}.xml")
    assemblies = report.findall("assembly")
    if len(assemblies) != 1:
        raise SystemExit(f"{lane}: expected one xUnit assembly result")
    assembly = assemblies[0]
    if any(assembly.get(field) != "0" for field in ("errors", "failed", "skipped", "not-run")):
        raise SystemExit(f"{lane}: report contains errors or tests that did not pass")
    counts = Counter(test.attrib["type"] for test in assembly.findall(".//test[@result='Pass']"))
    missing = [name for name in classes if not counts[name]]
    if missing:
        raise SystemExit(f"{lane}: no passing tests in {', '.join(missing)}")
    passed = sum(counts.values())
    if passed != int(assembly.attrib["passed"]):
        raise SystemExit(f"{lane}: passing test total does not match individual results")
    summary[lane] = {"passed": passed, "classes": dict(counts)}
(results / "summary.json").write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")
for lane, outcome in summary.items():
    print(f"{lane}: {outcome['passed']} passed")
PY
JOB
