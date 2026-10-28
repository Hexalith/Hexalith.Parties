#!/usr/bin/env bash
# Prepare real HTTPS requests and cold external source hosts before topology startup.
set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
source "${script_directory}/prepare-aspire-https-trust.sh"
bash "${script_directory}/prepare-aspire-runtime-hosts.sh"
