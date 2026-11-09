#!/usr/bin/env bash
# Build selected runtime graphs serially so concurrent dotnet run launches are incremental.
set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_directory}/.." && pwd)"
gateway_profile="packaged"
configuration="Release"
artifacts_path=""
include_memory_qualification="false"
while (( $# > 0 )); do
    if (( $# < 2 )); then
        printf 'Usage: %s [--gateway-profile packaged|native] [--configuration Release|Debug] [--artifacts-path ABSOLUTE_PATH] [--include-memory-qualification true|false]\n' "$0" >&2
        exit 2
    fi
    case "$1" in
        --gateway-profile) gateway_profile="$2" ;;
        --configuration) configuration="$2" ;;
        --artifacts-path) artifacts_path="$2" ;;
        --include-memory-qualification) include_memory_qualification="$2" ;;
        *) printf 'Unsupported runtime preparation option: %s\n' "$1" >&2; exit 2 ;;
    esac
    shift 2
done
case "$configuration" in Release|Debug) ;; *) printf 'Unsupported configuration: %s\n' "$configuration" >&2; exit 2 ;; esac
if [[ -n "$artifacts_path" && "$artifacts_path" != /* ]]; then
    printf 'The isolated artifacts path must be absolute.\n' >&2
    exit 2
fi
output_arguments=()
if [[ -n "$artifacts_path" ]]; then
    output_arguments=("-p:UseArtifactsOutput=true" "-p:ArtifactsPath=$artifacts_path")
fi
case "$include_memory_qualification" in true|false) ;; *) printf 'Unsupported memory qualification selection: %s\n' "$include_memory_qualification" >&2; exit 2 ;; esac
case "$gateway_profile" in
    packaged)
        gateway_project=tests/Hexalith.Parties.EventStoreGateway.TestHost/Hexalith.Parties.EventStoreGateway.TestHost.csproj
        sample_project=tests/Hexalith.Parties.EventStoreSample.TestHost/Hexalith.Parties.EventStoreSample.TestHost.csproj ;;
    native)
        gateway_project=references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj
        sample_project=references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj ;;
    *) printf 'Unsupported gateway profile: %s\n' "$gateway_profile" >&2; exit 2 ;;
esac
if [[ "$gateway_profile" == packaged ]]; then
    receipt_directory="${artifacts_path:-$repository_root/artifacts}/runtime-profile/$configuration"
    mkdir -p "$receipt_directory"
    # An earlier completed manifest cannot qualify a failed or only partially completed preparation.
    rm -f -- "$receipt_directory/prepared.json"
fi
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

if [[ "$include_memory_qualification" == true ]]; then
    runtime_hosts+=(tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj
        tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj)
fi

catalog_artifacts=()
if [[ "$gateway_profile" == packaged ]]; then
    runtime_hosts+=(src/Hexalith.Parties/Hexalith.Parties.csproj
        src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj)
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
receipt_projects=()
receipt_metadata=()
receipt_started=()
receipt_roles=()
receipt_paths=()
if [[ "$gateway_profile" == packaged ]]; then
    sdk_version="$(dotnet msbuild "$gateway_project" -nologo "-p:Configuration=$configuration" -getProperty:HexalithEventStoreVersion)"
    if [[ ! "$sdk_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
        printf 'The packaged runtime requires one exact evaluated stable SDK version; received: %s\n' "$sdk_version" >&2
        exit 1
    fi
    catalog_hosts="$(dotnet msbuild "$gateway_project" -nologo "-p:Configuration=$configuration" -getProperty:HexalithEventStoreCatalogHostsSupported)"
    if [[ "$catalog_hosts" == true ]]; then
        runtime_hosts[2]=tests/Hexalith.Parties.EventStoreAdminUI.TestHost/Hexalith.Parties.EventStoreAdminUI.TestHost.csproj
        if [[ ! -f "${repository_root}/${runtime_hosts[2]}" ]]; then
            printf 'Missing declared private Admin UI project: %s\n' "${runtime_hosts[2]}" >&2
            exit 1
        fi
    fi
    peer_versions="$(dotnet msbuild "$gateway_project" -nologo "-p:Configuration=$configuration" -getProperty:HexalithTenantsVersion,HexalithMemoriesVersion,HexalithFrontComposerVersion,HexalithMcpCliVersion)"
    families_json="$(python3 -c 'import json,sys; print(json.dumps(json.load(sys.stdin)["Properties"]))' <<< "$peer_versions")"
    tenants_version="$(python3 -c 'import json,sys; print(json.load(sys.stdin)["HexalithTenantsVersion"])' <<< "$families_json")"
    memories_version="$(python3 -c 'import json,sys; print(json.load(sys.stdin)["HexalithMemoriesVersion"])' <<< "$families_json")"
    frontcomposer_version="$(python3 -c 'import json,sys; print(json.load(sys.stdin)["HexalithFrontComposerVersion"])' <<< "$families_json")"
    mcpcli_version="$(python3 -c 'import json,sys; print(json.load(sys.stdin)["HexalithMcpCliVersion"])' <<< "$families_json")"
    cohort_arguments=("-p:HexalithEventStoreVersion=$sdk_version" "-p:HexalithUsePublishedSdkForHostedTests=true"
        "-p:HexalithTenantsVersion=$tenants_version" "-p:HexalithMemoriesVersion=$memories_version"
        "-p:HexalithFrontComposerVersion=$frontcomposer_version" "-p:HexalithMcpCliVersion=$mcpcli_version")
fi
for project in "${runtime_hosts[@]}" "${catalog_artifacts[@]}"; do
    printf 'Preparing Aspire runtime/artifact: %s\n' "$project"
    if [[ "$gateway_profile" == packaged ]]; then
        started_ns="$(python3 -c 'import time; print(time.time_ns())')"
        dotnet build "$project" --configuration "$configuration" --disable-build-servers \
            -t:Rebuild -m:1 -p:BuildInParallel=false -p:CopyLocalLockFileAssemblies=true -warnaserror "${output_arguments[@]}" "${cohort_arguments[@]}"
        metadata="$(dotnet msbuild "$project" -nologo "-p:Configuration=$configuration" "${output_arguments[@]}" "${cohort_arguments[@]}" \
            -getProperty:TargetPath,ProjectAssetsFile,Configuration,HexalithEventStoreVersion)"
        receipt="$receipt_directory/$(printf '%s' "$project" | sha256sum | cut -d ' ' -f 1).json"
        role=runtime
        for artifact in "${catalog_artifacts[@]}"; do [[ "$project" != "$artifact" ]] || role=catalog; done
        python3 "$script_directory/validate-aspire-runtime-cohort.py" --record "$receipt" --project "$project" \
            --metadata-json "$metadata" --configuration "$configuration" --version "$sdk_version" --build-started-ns "$started_ns" --role "$role" --families-json "$families_json"
        receipt_projects+=("$project")
        receipt_metadata+=("$metadata")
        receipt_started+=("$started_ns")
        receipt_roles+=("$role")
        receipt_paths+=("$receipt")
        receipts+=(--receipt "$receipt")
    else
        dotnet build "$project" --configuration "$configuration" --disable-build-servers \
            -m:1 -p:BuildInParallel=false -warnaserror "${output_arguments[@]}"
    fi
done
if [[ "$gateway_profile" == packaged ]]; then
    # Referencing qualification projects can rebuild earlier host dependencies. Bind final
    # bytes only after every compiler has succeeded, retaining each original rebuild start.
    for index in "${!receipt_projects[@]}"; do
        python3 "$script_directory/validate-aspire-runtime-cohort.py" --record "${receipt_paths[$index]}" --project "${receipt_projects[$index]}" \
            --metadata-json "${receipt_metadata[$index]}" --configuration "$configuration" --version "$sdk_version" \
            --build-started-ns "${receipt_started[$index]}" --role "${receipt_roles[$index]}" --families-json "$families_json"
    done
    python3 "$script_directory/validate-aspire-runtime-cohort.py" --complete-manifest "$receipt_directory/prepared.json" \
        --configuration "$configuration" --version "$sdk_version" --families-json "$families_json" "${receipts[@]}" --archive-cache "$receipt_directory/public-archives" \
        --public-package "Hexalith.Tenants.Contracts=$tenants_version" --public-package "Hexalith.Tenants.Server=$tenants_version" \
        --public-package "Hexalith.Memories.EventStore=$memories_version" --public-package "Hexalith.Memories.EventStoreExtensions=$memories_version"
fi
