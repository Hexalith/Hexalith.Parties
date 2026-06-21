---
project_name: parties
user_name: Administrator
date: 2026-06-12
scope_classification: Minor
status: implemented
implementation_verified: 2026-06-21
verification_evidence: >-
  No `http://auth.tache.ai:8080` authority/issuer remains under deploy/ or AppHost;
  `https://auth.tache.ai/realms/tache` is used in deploy/k8s/eventstore-admin-ui/kustomization.yaml,
  AppHost Program.cs (PublishModeJwtIssuer), and publish.ps1 ($TacheIssuer); the eventstore-admin-ui
  deployment carries no auth.tache.ai hostAlias.
---

# Sprint Change Proposal: eventstore-admin-ui Keycloak HTTPS Authority

Date: 2026-06-12
Project: parties
Scope classification: Minor
Status: implemented (verified 2026-06-21)

## 1. Issue Summary

`eventstore-admin-ui` was acquiring Keycloak tokens through the internal HTTP endpoint:

- `http://auth.tache.ai:8080/realms/tache/protocol/openid-connect/token`

The Kubernetes deployment also carried a pod `hostAliases` entry mapping `auth.tache.ai` to the Keycloak ClusterIP, which bypassed the public HTTPS ingress. Keycloak logs showed the warning:

- `Non-secure context detected; cookies are not secured, and will not be available in cross-origin POST requests`

The correction is application/deployment-side only. The Keycloak service is unchanged.

## 2. Impact Analysis

Epic impact: no product epic replan required. This is an operational deployment hardening correction.

Story impact: no user-facing story scope changes required.

Artifact impact:

- `deploy/k8s/eventstore-admin-ui/kustomization.yaml` must use `https://auth.tache.ai/realms/tache` for Authority and Issuer.
- `deploy/k8s/eventstore-admin-ui/deployment.yaml` must not include the `auth.tache.ai` hostAlias.
- `src/Hexalith.Parties.AppHost/Program.cs` must generate the public HTTPS authority for `eventstore-admin-ui` in publish mode.
- `deploy/k8s/publish.ps1` must not reintroduce the admin-ui hostAlias and must enforce the public HTTPS issuer after aspirate generation.
- Deployment tests and docs must reflect the new contract.

Technical impact:

- Other workloads that still use `http://auth.tache.ai:8080/realms/tache` are intentionally left unchanged.
- `eventstore-admin-ui` still uses Keycloak direct-access grants. Replacing `grant_type=password` with `client_credentials` requires a dedicated Keycloak client/service account and secret rollout; that is outside this minor transport fix.
- `AdminApiAccessTokenProvider` already caches tokens until one minute before expiry and serializes refreshes with a semaphore. No cache defect was found in the inspected code.

## 3. Recommended Approach

Selected path: Direct Adjustment.

Rationale: the confirmed failure is caused by insecure transport and host aliasing for one workload. A localized manifest/AppHost/publish-script correction removes the insecure HTTP token endpoint without changing Keycloak or broadening the fix to other apps.

Risk: low. The main runtime dependency is that `eventstore-admin-ui` pods can resolve and reach `https://auth.tache.ai/realms/tache` through the cluster/network path used for public ingress.

## 4. Detailed Change Proposals

Deployment manifest:

- Old: `EventStore__Authentication__Authority=http://auth.tache.ai:8080/realms/tache`
- New: `EventStore__Authentication__Authority=https://auth.tache.ai/realms/tache`
- Old: `EventStore__Authentication__Issuer=http://auth.tache.ai:8080/realms/tache`
- New: `EventStore__Authentication__Issuer=https://auth.tache.ai/realms/tache`
- Remove `hostAliases` entry for `auth.tache.ai -> 10.233.41.235` from `eventstore-admin-ui`.

Publish pipeline:

- Skip Keycloak hostAlias patching for `eventstore-admin-ui`.
- Add a post-generation guard that rewrites and validates the admin-ui Keycloak Authority/Issuer as `https://auth.tache.ai/realms/tache`.

Tests and docs:

- Add static manifest coverage for admin-ui HTTPS authority and absence of hostAlias.
- Update publish-script tests to cover the post-generation guard.
- Update deployment docs to describe admin-ui as the HTTPS exception while other workloads remain out of scope.

## 5. Implementation Handoff

Route to: Developer agent for direct implementation.

Success criteria:

- `eventstore-admin-ui` generated and committed manifests contain no `http://auth.tache.ai:8080` authority/issuer.
- `eventstore-admin-ui` deployment contains no `auth.tache.ai` hostAlias.
- Keycloak token acquisition for `eventstore-admin-ui` uses `https://auth.tache.ai/realms/tache/protocol/openid-connect/token`.
- No secrets, passwords, bearer tokens, or token response payloads are logged or committed.
- Existing token caching remains in place.

