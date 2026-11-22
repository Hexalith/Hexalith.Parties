#!/usr/bin/env bash
# Restore and verify the Aspire assets required by the package integration gate.
set -euo pipefail

scratch="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/parties-aspire-restore.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
version="$(dotnet msbuild "$repo_root/src/Hexalith.Parties.Aspire/Hexalith.Parties.Aspire.csproj" \
    -nologo -getProperty:HexalithAspireAppHostSdkVersion)"
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    printf 'Invalid Aspire SDK version from package catalog: %s\n' "$version" >&2
    exit 1
fi
cat > "$scratch/apphost.cs" <<APPHOST
#!/usr/bin/env dotnet
#:sdk Aspire.AppHost.Sdk@$version
#:package Aspire.Hosting.Orchestration.linux-x64@$version
#:package Aspire.Dashboard.Sdk.linux-x64@$version
using Aspire.Hosting;
var builder = DistributedApplication.CreateBuilder(args);
using var app = builder.Build();
APPHOST

dotnet restore "$scratch/apphost.cs" --verbosity minimal
packages="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
for binary in \
    "$packages/aspire.hosting.orchestration.linux-x64/$version/tools/dcp" \
    "$packages/aspire.dashboard.sdk.linux-x64/$version/tools/Aspire.Dashboard"; do
    if [[ ! -f "$binary" || ! -s "$binary" || -L "$binary" ]]; then
        printf 'Aspire package preparation did not materialize %s\n' "$binary" >&2
        exit 1
    fi
    # NuGet's Linux payloads can be extracted without an executable bit.
    chmod u+x -- "$binary"
    test -x "$binary"
done

if [[ -n "${GITHUB_ENV:-}" ]]; then
    printf 'HexalithPartiesAppHostSdkVersion=%s\n' "$version" >> "$GITHUB_ENV"
fi
