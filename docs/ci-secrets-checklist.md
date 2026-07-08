# CI Secrets Checklist

Configure these under GitHub repository settings: Secrets and variables, Actions.

## Required For Build And Test

No secrets are required for the current .NET build and test jobs.

## Required For Parties-only Zot Container Publish

Configure these before running `.github/workflows/publish-parties-containers.yml`:

- `ZOT_REGISTRY_USERNAME`: Zot username / mapped Keycloak identity used by GitHub Actions. This is the username passed to `docker login registry.hexalith.com`.
- `ZOT_REGISTRY_API_KEY`: Zot API key generated after Keycloak/OIDC login for that identity. The Zot API key replaces the password in Docker/.NET container publish authentication.

The publishing identity must have rights to create/update these repositories:

- `registry.hexalith.com/parties`
- `registry.hexalith.com/parties-mcp`
- `registry.hexalith.com/parties-ui`

Prefer a dedicated CI identity for durable production use. A human-mapped test identity is acceptable only for temporary validation, with the API key rotated or deleted immediately after confirmation.

## Required For Pact Contract Gates

- `PACT_BROKER_BASE_URL`: Pact Broker or PactFlow base URL.
- `PACT_BROKER_TOKEN`: token used by Pact publish, provider verification, and can-i-deploy scripts.

## Pact Webhook Dispatch

If PactFlow webhooks are enabled, configure the webhook to send `repository_dispatch` events of type `contract_requiring_verification_published`.

Use a dedicated GitHub machine user token for the PactFlow webhook secret. Rotate that token on an explicit schedule and monitor for stale provider verification results so silent webhook failures do not block deployment later.

## Artifact Hygiene

Do not write bearer tokens, Zot API keys, tenant/user identifiers, command payloads, event payloads, or personal data into test logs, TRX attachments, coverage artifacts, scripts, docs, or workflow YAML.
