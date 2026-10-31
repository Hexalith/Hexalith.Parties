#!/usr/bin/env bash
# Build selected runtime graphs serially so concurrent dotnet run launches are incremental.
set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_directory}/.." && pwd)"
gateway_profile="packaged"
configuration="Release"
while (( $# > 0 )); do
    if (( $# < 2 )); then
        printf 'Usage: %s [--gateway-profile packaged|native] [--configuration Release|Debug]\n' "$0" >&2
        exit 2
    fi
    case "$1" in
        --gateway-profile) gateway_profile="$2" ;;
        --configuration) configuration="$2" ;;
        *) printf 'Unsupported runtime preparation option: %s\n' "$1" >&2; exit 2 ;;
    esac
    shift 2
done
case "$configuration" in Release|Debug) ;; *) printf 'Unsupported configuration: %s\n' "$configuration" >&2; exit 2 ;; esac
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

catalog_artifacts=()
if [[ "$gateway_profile" == packaged ]]; then
    runtime_hosts+=(src/Hexalith.Parties/Hexalith.Parties.csproj)
    catalog_artifacts=(
        src/Hexalith.Parties.EventStoreExtensions/Hexalith.Parties.EventStoreExtensions.csproj
    )
fi
for project in "${runtime_hosts[@]}" "${catalog_artifacts[@]}"; do
    if [[ ! -f "${repository_root}/${project}" ]]; then
        printf 'Missing declared Aspire runtime/artifact project: %s\n' "$project" >&2
        exit 1
    fi
done

cd -- "$repository_root"
cohort_arguments=()
receipts=()
if [[ "$gateway_profile" == packaged ]]; then
    sdk_version="$(dotnet msbuild "$gateway_project" -nologo -getProperty:HexalithEventStoreVersion)"
    if [[ ! "$sdk_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
        printf 'The packaged runtime requires one exact evaluated stable SDK version; received: %s\n' "$sdk_version" >&2
        exit 1
    fi
    peer_versions="$(dotnet msbuild "$gateway_project" -nologo -getProperty:HexalithTenantsVersion,HexalithMemoriesVersion,HexalithFrontComposerVersion)"
    families_json="$(python3 -c 'import json,sys; print(json.dumps(json.load(sys.stdin)["Properties"]))' <<< "$peer_versions")"
    tenants_version="$(python3 -c 'import json,sys; print(json.load(sys.stdin)["HexalithTenantsVersion"])' <<< "$families_json")"
    memories_version="$(python3 -c 'import json,sys; print(json.load(sys.stdin)["HexalithMemoriesVersion"])' <<< "$families_json")"
    frontcomposer_version="$(python3 -c 'import json,sys; print(json.load(sys.stdin)["HexalithFrontComposerVersion"])' <<< "$families_json")"
    cohort_arguments=("-p:HexalithEventStoreVersion=$sdk_version" "-p:HexalithUsePublishedSdkForHostedTests=true"
        "-p:HexalithTenantsVersion=$tenants_version" "-p:HexalithMemoriesVersion=$memories_version"
        "-p:HexalithFrontComposerVersion=$frontcomposer_version")
    receipt_directory="$repository_root/artifacts/runtime-profile/$configuration"
    mkdir -p "$receipt_directory"
    # An earlier completed manifest cannot qualify a failed or only partially completed preparation.
    rm -f -- "$receipt_directory/prepared.json"
fi
for project in "${runtime_hosts[@]}" "${catalog_artifacts[@]}"; do
    printf 'Preparing Aspire runtime/artifact: %s\n' "$project"
    if [[ "$gateway_profile" == packaged ]]; then
        started_ns="$(python3 -c 'import time; print(time.time_ns())')"
        dotnet build "$project" --configuration "$configuration" --disable-build-servers \
            -t:Rebuild -m:1 -p:BuildInParallel=false -p:CopyLocalLockFileAssemblies=true -warnaserror "${cohort_arguments[@]}"
        metadata="$(dotnet msbuild "$project" -nologo "-p:Configuration=$configuration" "${cohort_arguments[@]}" \
            -getProperty:TargetPath,ProjectAssetsFile,Configuration,HexalithEventStoreVersion)"
        receipt="$receipt_directory/$(printf '%s' "$project" | sha256sum | cut -d ' ' -f 1).json"
        role=runtime
        for artifact in "${catalog_artifacts[@]}"; do [[ "$project" != "$artifact" ]] || role=catalog; done
        python3 "$script_directory/validate-aspire-runtime-cohort.py" --record "$receipt" --project "$project" \
            --metadata-json "$metadata" --configuration "$configuration" --version "$sdk_version" --build-started-ns "$started_ns" --role "$role" --families-json "$families_json"
        receipts+=(--receipt "$receipt")
    else
        dotnet build "$project" --configuration "$configuration" --disable-build-servers \
            -m:1 -p:BuildInParallel=false -warnaserror
    fi
done
if [[ "$gateway_profile" == packaged ]]; then
    python3 "$script_directory/validate-aspire-runtime-cohort.py" --complete-manifest "$receipt_directory/prepared.json" \
        --configuration "$configuration" --version "$sdk_version" --families-json "$families_json" "${receipts[@]}" --archive-cache "$receipt_directory/public-archives" \
        --public-package "Hexalith.Tenants.Contracts=$tenants_version" --public-package "Hexalith.Tenants.Server=$tenants_version" \
        --public-package "Hexalith.Memories.EventStore=$memories_version" --public-package "Hexalith.Memories.EventStoreExtensions=$memories_version"
fi
