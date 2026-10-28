#!/usr/bin/env bash
# Trust the ephemeral runner's HTTPS development certificate for real UI requests.
set -euo pipefail

: "${GITHUB_ENV:?Run this preparation in the owning GitHub Actions job.}"
export SSL_CERT_DIR="${HOME}/.aspnet/dev-certs/trust:${SSL_CERT_DIR:-/usr/lib/ssl/certs}"
printf 'SSL_CERT_DIR=%s\n' "$SSL_CERT_DIR" >> "$GITHUB_ENV"
dotnet dev-certs https --trust
dotnet dev-certs https --check --trust
