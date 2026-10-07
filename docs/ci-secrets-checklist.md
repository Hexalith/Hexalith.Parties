# CI Secrets Checklist

Configure these under GitHub repository settings: Secrets and variables, Actions.

## Required For Build And Test

No secrets are required for the current .NET build and test jobs.

## Required For Release

Configure these before running `.github/workflows/release.yml`:

- Repository variable `NUGET_USER=jpiquot`: the individual NuGet account that created the trusted publishing policy. Do not substitute the package owner `Hexalith` or derive this account from the GitHub actor.
- An active NuGet trusted publishing policy for package owner `Hexalith`, GitHub repository `Hexalith/Hexalith.Parties`, workflow filename `release.yml`, and environment `production`. The local policy JSON records the user-reported registration; it does not configure NuGet.
- Repository variable `HEXALITH_RELEASE_PUBLISH_ENABLED`: set exactly lowercase `true` to allow publication. Set an explicit repository value to avoid unintentionally inheriting an organization value; other values keep login and publication frozen.
- `HEXALITH_ZOT_USERNAME`: Zot username / mapped Keycloak identity used by GitHub Actions. This is the username passed to `docker login registry.hexalith.com`.
- `HEXALITH_ZOT_API_KEY`: Zot API key generated after Keycloak/OIDC login for that identity. The Zot API key replaces the password in Docker/.NET container publish authentication.

The protected Parties job requests `id-token: write` and uses SHA-pinned official `NuGet/login` after successful shared preparation. Semantic-release receives `NUGET_API_KEY` only from that login's masked, short-lived output, with no fallback to `secrets.NUGET_API_KEY`. A stored long-lived NuGet key is not required, and this change does not delete existing secrets.

The publishing identity must have rights to create/update these repositories:

- `registry.hexalith.com/parties`
- `registry.hexalith.com/parties-mcp`
- `registry.hexalith.com/parties-ui`

Prefer a dedicated CI identity for durable production use. A human-mapped test identity is acceptable only for temporary validation, with the API key rotated or deleted immediately after confirmation.

Store the two Zot release secrets in the protected GitHub environment named `production`. Configure required reviewers and restrict deployments to `main`; the manual release must not reach these credentials until its unprotected exact-source gate succeeds and an authorized reviewer approves the environment. Ordinary releases require successful exact-source `ci.yml` proof; the typed `bypass-validation=true` path requires successful exact-source `commitlint.yml` proof. Shared preparation repeats the selected proof before NuGet login.

Credentials do not resolve the current EventStore package API prerequisite. Package-mode verification on 2026-10-07 against 3.115.0 reports CS1061 for `RequireEventStoreSidecarChannel`; green CI requires a compatible owner-published EventStore package and a separately approved dependency update. See [ci.md](ci.md). Environment, secret, and trusted-policy configuration remain operator-owned prerequisites.

## Required For Pact Contract Gates

- `PACT_BROKER_BASE_URL`: Pact Broker or PactFlow base URL.
- `PACT_BROKER_TOKEN`: token used by Pact publish, provider verification, and can-i-deploy scripts.

## Pact Webhook Dispatch

If PactFlow webhooks are enabled, configure the webhook to send `repository_dispatch` events of type `contract_requiring_verification_published`.

Use a dedicated GitHub machine user token for the PactFlow webhook secret. Rotate that token on an explicit schedule and monitor for stale provider verification results so silent webhook failures do not block deployment later.

## Artifact Hygiene

Do not write bearer tokens, Zot API keys, tenant/user identifiers, command payloads, event payloads, or personal data into test logs, TRX attachments, coverage artifacts, scripts, docs, or workflow YAML.
