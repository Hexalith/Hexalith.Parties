#!/usr/bin/env bash
# Verify the Aspire assets restored by the declared runtime artifact graph.
set -euo pipefail

packages="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
for binary in \
    "$packages/aspire.hosting.orchestration.linux-x64/13.6.5/tools/dcp" \
    "$packages/aspire.dashboard.sdk.linux-x64/13.6.5/tools/Aspire.Dashboard"; do
    if [[ ! -x "$binary" ]]; then
        printf 'The standalone solution restore did not materialize %s\n' "$binary" >&2
        exit 1
    fi
done
