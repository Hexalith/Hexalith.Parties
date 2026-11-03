#!/usr/bin/env bash
# Materialize the Aspire runner binaries required by the package integration gate.
set -euo pipefail

scratch="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/parties-aspire-restore.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT
cat > "$scratch/apphost.cs" <<'APPHOST'
#!/usr/bin/env dotnet
#:sdk Aspire.AppHost.Sdk@13.6.4
using Aspire.Hosting;
var builder = DistributedApplication.CreateBuilder(args);
using var app = builder.Build();
APPHOST

dotnet restore "$scratch/apphost.cs" --verbosity minimal
packages="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
for binary in \
    "$packages/aspire.hosting.orchestration.linux-x64/13.6.4/tools/dcp" \
    "$packages/aspire.dashboard.sdk.linux-x64/13.6.4/tools/Aspire.Dashboard"; do
    if [[ ! -x "$binary" ]]; then
        printf 'Aspire package preparation did not materialize %s\n' "$binary" >&2
        exit 1
    fi
done
