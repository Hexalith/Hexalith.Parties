#!/usr/bin/env bash
set -euo pipefail
repo=$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)
receipts=${FIVE_PROCESSING_LOG_DIR:-$(mktemp -d "${TMPDIR:-/tmp}/parties-five-processing.XXXXXX")}
mkdir -p "$receipts"
cd "$repo"
source_mode() {
    : "${FIVE_PROCESSING_SOURCE_WORKSPACE:?Select the current runner-owned workspace described in rerun-environment.md}"
    local mapping_dir="$FIVE_PROCESSING_SOURCE_WORKSPACE/source-mapping"
    local file
    for file in mapping.json SourceMapping.targets SourceMapping.props SourceMapping.before-targets.props SourceMapping.late.props SourceMapping.final.targets; do
        test -f "$mapping_dir/$file" || { printf 'Missing generated source mapping: %s\n' "$mapping_dir/$file" >&2; return 1; }
    done
    # Consume the owning tool's generated imports; never author or copy its mapping files.
    export CustomBeforeDirectoryBuildProps="$mapping_dir/SourceMapping.targets"
    export CustomAfterMicrosoftCommonProps="$mapping_dir/SourceMapping.props"
    export CustomBeforeDirectoryBuildTargets="$mapping_dir/SourceMapping.before-targets.props"
    export CustomAfterDirectoryBuildTargets="$mapping_dir/SourceMapping.late.props"
    export AfterMicrosoftNETSdkTargets="$mapping_dir/SourceMapping.final.targets"
    export HEXALITH_SOURCE_MAPPING_HASH
    HEXALITH_SOURCE_MAPPING_HASH=$(python3 - "$mapping_dir/mapping.json" "$repo" <<'PYMAP'
import json,pathlib,sys
mapping=json.loads(pathlib.Path(sys.argv[1]).read_text())
assert mapping['Mode']=='source', 'Requires source-mode mapping'
assert pathlib.Path(mapping['Root']).resolve()==pathlib.Path(sys.argv[2]).resolve(), 'Mapping belongs to another checkout'
print(mapping['ContentHash'])
PYMAP
)
}
package_mode() {
    unset CustomBeforeDirectoryBuildProps CustomAfterMicrosoftCommonProps CustomBeforeDirectoryBuildTargets CustomAfterDirectoryBuildTargets AfterMicrosoftNETSdkTargets HEXALITH_SOURCE_MAPPING_HASH
}
run() {
    local name=$1
    shift
    printf '%s\n' "$name"
    if "$@" > "$receipts/$name.log" 2>&1; then tail -n 8 "$receipts/$name.log"; else tail -n 24 "$receipts/$name.log"; return 1; fi
}
case "${1:-}" in
parties-source)
    source_mode
    run affected-parties-restore dotnet restore tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --force -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run affected-parties-build dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run affected-parties-focused-tests dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class '*PartyProcessingGenerationRecoveryTests' -class '*PartyGuardedRebuildErasureTests' -class '*PartySdkQueryHandlerTests' -class '*PartyDetailSdkProjectionRecoveryTests' -class '*PartySdkCrossInstanceErasureTests' -class '*PartySearchErasureFenceTests'
    ;;
projections-source)
    source_mode
    run affected-projections-source-restore dotnet restore tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --force -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run affected-projections-source-build dotnet build tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run affected-projections-source-tests dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll
    ;;
eventstore)
    package_mode
    cd references/Hexalith.EventStore
    run affected-eventstore-client-build dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run affected-eventstore-client-all-tests dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll
    run affected-eventstore-domain-final-build dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run affected-eventstore-dispatcher-tests dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainProjectionDispatcherV2Tests' -class '*MixedProjectionCatalogTests'
    run affected-eventstore-replay-tests dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -method '*RecoveryBoundReplay_ActualSdkMappingsReuseOnePersistentManifestAcrossRetryAndFreshHosts'
    ;;
memories-runtime)
    source_mode
    cd references/Hexalith.Memories
    run affected-memories-integration-restore dotnet restore tests/Hexalith.Memories.IntegrationTests/Hexalith.Memories.IntegrationTests.csproj --force -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run affected-memories-integration-build dotnet build tests/Hexalith.Memories.IntegrationTests/Hexalith.Memories.IntegrationTests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    # By default the existing fixtures own their test containers and sidecar. A caller may
    # explicitly select private native adapters using the existing documented fixture contract.
    if [[ ${MEMORIES_TEST_OWNED_NATIVE:-0} == 1 ]]; then
        : "${MEMORIES_TEST_REDISSTACK_CONNECTION:?Select an owned loopback RedisSearch fixture}"
        : "${MEMORIES_TEST_FALKORDB_CONNECTION:?Select an owned loopback FalkorDB fixture}"
        : "${MEMORIES_DAPR_PLACEMENT_HOST_ADDRESS:?Select the fixture placement endpoint}"
        : "${MEMORIES_DAPR_SCHEDULER_HOST_ADDRESS:?Select the fixture scheduler endpoint}"
    fi
    run affected-memories-runtime-final dotnet tests/Hexalith.Memories.IntegrationTests/bin/Debug/net10.0/Hexalith.Memories.IntegrationTests.dll -class '*NaturalLanguageErasureIntegrationTests' -class '*RetryWorkflowErasureIntegrationTests' -class '*RefreshPublicationIntegrationTests' -class '*InventoryWirePaginationIntegrationTests' -class '*ReferencedSourceProducerIntegrationTests' -class '*SourceInventoryProducerIntegrationTests' -class '*SourceInventoryIntegrationTests' -class '*TenantIngestionLifecycleIntegrationTests'
    ;;
client-package|projections-package)
    package_mode
    if [[ $1 == client-package ]]; then project=Client; else project=Projections; fi
    run "affected-${project,,}-release-build" dotnet build "tests/Hexalith.Parties.$project.Tests/Hexalith.Parties.$project.Tests.csproj" -c Release -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
    run "affected-${project,,}-release-tests" dotnet "tests/Hexalith.Parties.$project.Tests/bin/Release/net10.0/Hexalith.Parties.$project.Tests.dll"
    ;;
memories-package)
    package_mode
    cd references/Hexalith.Memories
    run affected-memories-release-build dotnet build tests/Hexalith.Memories.Server.Tests/Hexalith.Memories.Server.Tests.csproj -c Release -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
    run affected-memories-release-focused-tests dotnet tests/Hexalith.Memories.Server.Tests/bin/Release/net10.0/Hexalith.Memories.Server.Tests.dll -class '*NaturalLanguageEmbeddingRetryWorkflowTests' -class '*NaturalLanguageEmbeddingRetryHostedServiceTests' -class '*FailedNaturalLanguageEmbeddingRegistryTests' -class '*RedisNaturalLanguageNamespaceMigratorTests' -class '*DeleteMemoryUnitProjectionActivityTests'
    # The full suite's unrelated AccessTelemetryA41CloseOutTests wording failure is retained in
    # affected-memories-release-tests.log; do not treat the narrow pass as a full-suite pass.
    ;;
ci)
    package_mode
    run affected-ci-build dotnet build tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj -c Release -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
    run affected-ci-tests dotnet tests/Hexalith.Parties.Ci.Tests/bin/Release/net10.0/Hexalith.Parties.Ci.Tests.dll
    ;;
controls)
    run affected-publisher-tests python3 -m unittest discover -s references/Hexalith.Builds/Github/publish-containers/tests -p 'test_*.py'
    run affected-no-warning-overrides bash scripts/check-no-warning-override.sh
    run affected-eventstore-whitespace git -C references/Hexalith.EventStore diff --check
    run affected-memories-whitespace git -C references/Hexalith.Memories diff --check
    run affected-parties-code-whitespace git -c core.whitespace=cr-at-eol diff --check -- src tests
    ;;
*) printf '%s\n' 'Choose: parties-source, projections-source, eventstore, memories-runtime, client-package, projections-package, memories-package, ci, controls'; exit 2 ;;
esac
