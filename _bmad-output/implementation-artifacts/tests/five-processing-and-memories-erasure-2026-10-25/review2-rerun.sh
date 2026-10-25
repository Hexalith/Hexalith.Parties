#!/usr/bin/env bash
set -euo pipefail
repo=/home/administrator/projects/hexalith/parties
receipts=/tmp/parties-five-processing-20261025
cd "$repo"
source_mode() { source "$receipts/source-env.sh"; }
package_mode() {
    unset CustomBeforeDirectoryBuildProps CustomAfterMicrosoftCommonProps CustomBeforeDirectoryBuildTargets CustomAfterDirectoryBuildTargets AfterMicrosoftNETSdkTargets
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
    run rederive2-parties-restore dotnet restore tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --force -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run rederive2-parties-build dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run rederive2-parties-focused-tests dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class '*PartyProcessingGenerationRecoveryTests' -class '*PartySdkQueryHandlerTests'
    ;;
projections-source)
    source_mode
    run rederive2-projections-source-restore dotnet restore tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --force -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run rederive2-projections-source-build dotnet build tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run rederive2-projections-source-tests dotnet tests/Hexalith.Parties.Projections.Tests/bin/Debug/net10.0/Hexalith.Parties.Projections.Tests.dll
    ;;
eventstore)
    package_mode
    cd references/Hexalith.EventStore
    run rederive2-eventstore-client-build dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run rederive2-eventstore-client-all-tests dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll
    run rederive2-eventstore-domain-final-build dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run rederive2-eventstore-dispatcher-tests dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainProjectionDispatcherV2Tests' -class '*MixedProjectionCatalogTests'
    run rederive2-eventstore-replay-tests dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -method '*RecoveryBoundReplay_ActualSdkMappingsReuseOnePersistentManifestAcrossRetryAndFreshHosts'
    ;;
memories-runtime)
    source_mode
    cd references/Hexalith.Memories
    run rederive2-memories-integration-restore dotnet restore tests/Hexalith.Memories.IntegrationTests/Hexalith.Memories.IntegrationTests.csproj --force -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    run rederive2-memories-integration-build dotnet build tests/Hexalith.Memories.IntegrationTests/Hexalith.Memories.IntegrationTests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
    # Only these existing private task-owned native endpoints are authorized. The fixture owns its
    # temporary sidecar; existing placement/scheduler processes are task-owned too.
    export MEMORIES_TEST_OWNED_NATIVE=1 MEMORIES_TEST_REDISSTACK_CONNECTION=127.0.0.1:16388 MEMORIES_TEST_FALKORDB_CONNECTION=127.0.0.1:16389 MEMORIES_DAPR_PLACEMENT_HOST_ADDRESS=127.0.0.1:16400 MEMORIES_DAPR_SCHEDULER_HOST_ADDRESS=127.0.0.1:16403
    run rederive2-memories-runtime-final dotnet tests/Hexalith.Memories.IntegrationTests/bin/Debug/net10.0/Hexalith.Memories.IntegrationTests.dll -class '*NaturalLanguageErasureIntegrationTests' -class '*RetryWorkflowErasureIntegrationTests' -class '*RefreshPublicationIntegrationTests' -class '*InventoryWirePaginationIntegrationTests' -class '*ReferencedSourceProducerIntegrationTests' -class '*SourceInventoryProducerIntegrationTests' -class '*SourceInventoryIntegrationTests' -class '*TenantIngestionLifecycleIntegrationTests'
    ;;
client-package|projections-package)
    package_mode
    if [[ $1 == client-package ]]; then project=Client; else project=Projections; fi
    run "rederive2-${project,,}-release-build" dotnet build "tests/Hexalith.Parties.$project.Tests/Hexalith.Parties.$project.Tests.csproj" -c Release -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
    run "rederive2-${project,,}-release-tests" dotnet "tests/Hexalith.Parties.$project.Tests/bin/Release/net10.0/Hexalith.Parties.$project.Tests.dll"
    ;;
memories-package)
    package_mode
    cd references/Hexalith.Memories
    run rederive2-memories-release-build dotnet build tests/Hexalith.Memories.Server.Tests/Hexalith.Memories.Server.Tests.csproj -c Release -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
    run rederive2-memories-release-focused-tests dotnet tests/Hexalith.Memories.Server.Tests/bin/Release/net10.0/Hexalith.Memories.Server.Tests.dll -class '*NaturalLanguageEmbeddingRetryWorkflowTests' -class '*NaturalLanguageEmbeddingRetryHostedServiceTests' -class '*FailedNaturalLanguageEmbeddingRegistryTests' -class '*RedisNaturalLanguageNamespaceMigratorTests' -class '*DeleteMemoryUnitProjectionActivityTests'
    # The full suite's unrelated AccessTelemetryA41CloseOutTests wording failure is retained in
    # rederive2-memories-release-tests.log; do not treat the narrow pass as a full-suite pass.
    ;;
ci)
    package_mode
    run rederive2-ci-build dotnet build tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj -c Release -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
    run rederive2-ci-tests dotnet tests/Hexalith.Parties.Ci.Tests/bin/Release/net10.0/Hexalith.Parties.Ci.Tests.dll
    ;;
controls)
    run rederive2-publisher-tests python3 -m unittest discover -s references/Hexalith.Builds/Github/publish-containers/tests -p 'test_*.py'
    run rederive2-no-warning-overrides bash scripts/check-no-warning-override.sh
    run rederive2-eventstore-whitespace git -C references/Hexalith.EventStore diff --check
    run rederive2-memories-whitespace git -C references/Hexalith.Memories diff --check
    run rederive2-parties-code-whitespace git -c core.whitespace=cr-at-eol diff --check -- src tests
    ;;
*) printf '%s\n' 'Choose: parties-source, projections-source, eventstore, memories-runtime, client-package, projections-package, memories-package, ci, controls'; exit 2 ;;
esac
