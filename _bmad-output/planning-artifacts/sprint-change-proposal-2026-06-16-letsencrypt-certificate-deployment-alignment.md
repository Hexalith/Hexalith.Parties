---
project_name: parties
user_name: Administrator
date: 2026-06-16
scope_classification: Minor
status: implemented
---

# Sprint Change Proposal - Let’s Encrypt certificate deployment alignment

## 1. Issue Summary

The live Kubernetes certificate configuration moved to cert-manager managed Let’s Encrypt HTTP-01 certificates. Deployment manifests and deployment validation must use those new TLS Secrets so future applies do not revert the live certificate routing.

Evidence from the live cluster:

- `Certificate/hexalith-pages-letsencrypt` in namespace `hexalith-parties` is Ready and writes `hexalith-pages-letsencrypt-tls`.
- `Certificate/registry-hexalith-letsencrypt` in namespace `zot` is Ready and writes `registry-hexalith-letsencrypt-tls`.
- `Ingress/hexalith-pages-ingress` and `Ingress/zot-ingress` already route through `nginx-public`.

## 2. Impact Analysis

Epic impact: Deployment hardening only. Product scope, UI stories, EventStore gateway behavior, and domain services are unchanged.

Story impact: Existing Kubernetes deployment acceptance is tightened to match cert-manager issued certificates.

Artifact impact:

- `deploy/k8s/ingress.yaml` uses `hexalith-pages-letsencrypt-tls`.
- `deploy/zot/ingress.yaml` uses `registry-hexalith-letsencrypt-tls`.
- `deploy/k8s/publish.ps1` validates the live `nginx-public` ingress class and both Let’s Encrypt TLS Secrets.
- `deploy/validate-deployment.ps1`, deployment docs, and DeployValidation fixtures/tests now assert the Let’s Encrypt TLS Secret names.

## 3. Recommended Approach

Direct Adjustment. The change is infrastructure configuration alignment, not a scope or architecture replan.

Effort: Low.
Risk: Low. Certificates are already Ready in the live cluster and the apply is idempotent.

## 4. Detailed Change Proposals

Public UI Ingress:

OLD:

- TLS Secret expectation: `hexalith-pages-tls`.

NEW:

- TLS Secret expectation: `hexalith-pages-letsencrypt-tls`.

Rationale: The live cert-manager `Certificate/hexalith-pages-letsencrypt` now owns the page-host certificate.

Zot registry Ingress:

OLD:

- TLS Secret expectation: `wildcard-hexalith-tls`.

NEW:

- TLS Secret expectation: `registry-hexalith-letsencrypt-tls`.

Rationale: The live cert-manager `Certificate/registry-hexalith-letsencrypt` now owns the registry certificate.

## 5. Implementation Handoff

Scope classification: Minor.

Routed to: Developer agent for direct implementation.

Success criteria:

- Both Ingress resources use the Let’s Encrypt TLS Secrets.
- Both cert-manager Certificates are Ready.
- HTTPS endpoints serve Let’s Encrypt certificates.
- Static deployment validation passes.
