#!/usr/bin/env bash
# Build selected runtime graphs serially so concurrent dotnet run launches are incremental.
set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_directory}/.." && pwd)"
gateway_profile="packaged"
if (( $# > 0 )); then
    if (( $# != 2 )) || [[ "$1" != "--gateway-profile" ]]; then
        printf 'Usage: %s [--gateway-profile packaged|native]\n' "$0" >&2
        exit 2
    fi
    gateway_profile="$2"
fi
case "$gateway_profile" in
    packaged)
        gateway_project=tests/Hexalith.Parties.EventStoreGateway.TestHost/Hexalith.Parties.EventStoreGateway.TestHost.csproj
        sample_project=tests/Hexalith.Parties.EventStoreSample.TestHost/Hexalith.Parties.EventStoreSample.TestHost.csproj ;;
    native)
        gateway_project=references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj
        sample_project=references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj ;;
    *) printf 'Unsupported gateway profile: %s\n' "$gateway_profile" >&2; exit 2 ;;
esac
runtime_hosts=(
    "$gateway_project"
    references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj
    references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj
    references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj
    references/Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj
    references/Hexalith.Memories/src/Hexalith.Memories.DomainService/Hexalith.Memories.DomainService.csproj
    "$sample_project"
    references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj
)

for project in "${runtime_hosts[@]}"; do
    if [[ ! -f "${repository_root}/${project}" ]]; then
        printf 'Missing declared Aspire runtime host: %s\n' "$project" >&2
        exit 1
    fi
done

cd -- "$repository_root"
for project in "${runtime_hosts[@]}"; do
    printf 'Preparing Aspire runtime host: %s\n' "$project"
    dotnet build "$project" --configuration Release --disable-build-servers \
        -m:1 -p:BuildInParallel=false -warnaserror
done
