#!/usr/bin/env bash
# Run the local released-package checks for DW-50, DW-58, DW-82, DW-100 and DW-168.
set -euo pipefail

if [[ $# != 1 || -z "$1" ]]; then
    printf 'Usage: bash %s NEW_RESULTS_DIRECTORY\n' "$0" >&2
    exit 2
fi

results="$(realpath -m -- "$1")"
repository="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
for tool in dotnet python3; do
    if ! command -v "$tool" >/dev/null; then
        printf 'Required command not found: %s\n' "$tool" >&2
        exit 2
    fi
done
mkdir -- "$results"
cd -- "$repository"
# UI fitness tests resolve source files from the assembly's parent directories.
mkdir -p -- artifacts/j

scratch="$(mktemp -d "$repository/artifacts/j/catalog.XXXXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT
export TMPDIR="$scratch"
artifacts="$scratch/artifacts"

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
dotnet msbuild tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -nologo \
    -p:Configuration=Release -p:UseHexalithProjectReferences=false \
    -getProperty:HexalithEventStoreVersion,HexalithMemoriesVersion,HexalithFrontComposerVersion \
    > "$results/catalog.json"
run_tests Hexalith.Parties.Tests core "${core_selectors[@]}"
run_tests Hexalith.Parties.Projections.Tests projections \
    -nomethod '*PublishedSdkPreservesLargeLegacyLiveAndRebuildCompatibilityAsync'
run_tests Hexalith.Parties.UI.Tests ui

python3 scripts/verify-released-catalog-results.py "$results"
