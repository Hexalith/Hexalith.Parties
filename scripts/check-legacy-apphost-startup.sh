#!/usr/bin/env bash
# Probe the running legacy AppHost from its own development environment.
set -euo pipefail

repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)
apphost="$repo_root/src/Hexalith.Parties.AppHost"
for resource in parties parties-dapr-cli; do
    aspire wait "$resource" --apphost "$apphost" --status up --timeout 120 --non-interactive >/dev/null
done

endpoints=$(aspire describe --apphost "$apphost" --format Json --non-interactive | jq -er '
    def http_url($name):
        [.resources[] | select(.displayName == $name) | .urls[] | select(.name == "http") | .url]
        | if length == 1 then .[0] else error("Expected one HTTP endpoint for " + $name) end;
    [http_url("parties"), http_url("parties-dapr-cli")] | @tsv')
IFS=$'\t' read -r parties_url sidecar_url <<< "$endpoints"

probe() {
    local url=$1 path=$2 expected=$3 status deadline=$((SECONDS + 60))
    if [[ ! $url =~ ^http://(localhost|127\.0\.0\.1|\[::1\]):[0-9]+/?$ ]]; then
        echo "Expected a loopback HTTP endpoint for $path" >&2
        return 1
    fi
    while true; do
        status=$(curl -q --noproxy '*' --silent --output /dev/null --write-out '%{http_code}' \
            --max-time 3 "${url%/}$path") || status=000
        if [[ $status == "$expected" ]]; then
            printf '%s returned HTTP %s\n' "$path" "$status"
            return 0
        fi
        if (( SECONDS >= deadline )); then
            echo "$path expected HTTP $expected, last status $status" >&2
            return 1
        fi
        sleep 1
    done
}

probe "$parties_url" /alive 200
probe "$sidecar_url" /v1.0/healthz 204
