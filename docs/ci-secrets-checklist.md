# CI Secrets Checklist

Configure these under GitHub repository settings: Secrets and variables, Actions.

## Required Now

No secrets are required for the current .NET build and test jobs.

## Required For Pact Contract Gates

- `PACT_BROKER_BASE_URL`: Pact Broker or PactFlow base URL.
- `PACT_BROKER_TOKEN`: token used by Pact publish, provider verification, and can-i-deploy scripts.

## Pact Webhook Dispatch

If PactFlow webhooks are enabled, configure the webhook to send `repository_dispatch` events of type `contract_requiring_verification_published`.

Use a dedicated GitHub machine user token for the PactFlow webhook secret. Rotate that token on an explicit schedule and monitor for stale provider verification results so silent webhook failures do not block deployment later.

## Artifact Hygiene

Do not write bearer tokens, tenant/user identifiers, command payloads, event payloads, or personal data into test logs, TRX attachments, or coverage artifacts.
