---
project_name: parties
user_name: Administrator
date: 2026-06-16
scope_classification: Minor
status: implemented
---

# Sprint Change Proposal - Kubernetes nginx deploy path

## 1. Issue Summary

Deployment must use the Kubernetes nginx Ingress path directly instead of relying on a workstation or host-level local nginx bridge. The affected deployment path is infrastructure-scoped: generated application images are pushed to `registry.hexalith.com`, browser UI workloads are published through `deploy/k8s/ingress.yaml`, and Zot is published through `deploy/zot/ingress.yaml`.

Evidence from the current artifacts:

- `deploy/k8s/ingress.yaml` routes `eventstore.hexalith.com`, `sample.hexalith.com`, and `parties.hexalith.com` to Kubernetes Services using the live cluster `ingressClassName: nginx-public`.
- `deploy/zot/ingress.yaml` routes `registry.hexalith.com` to `Service/zot:5000` using the live cluster `ingressClassName: nginx-public`.
- Several docs still described a local/host-level nginx bridge as a tolerated temporary path.
- `publish.ps1` did not fail early when the registry path was not backed by the Kubernetes nginx Ingress contract.

## 2. Impact Analysis

Epic impact: Deployment hardening only. Existing UI, domain, Dapr, and EventStore gateway stories remain valid.

Story impact: Story 1.10 / AR-D10 deployment acceptance is tightened. The publish flow now requires live Kubernetes nginx prerequisites before image build/apply work continues.

Artifact impact:

- `deploy/k8s/publish.ps1` now validates the Kubernetes nginx path for Zot and public UI TLS prerequisites.
- `deploy/k8s/README.md`, `docs/deployment-guide.md`, `docs/kubernetes-deployment-architecture.md`, and `deploy/zot/README.md` now reject local nginx bridge operation as a publish path.
- `tests/Hexalith.Parties.DeployValidation.Tests/OperatorScriptValidationTests.cs` covers the new script contract.

Technical impact: Operators must have `IngressClass/nginx-public`, `Ingress/zot-ingress`, `Service/zot` as `ClusterIP`, `registry-hexalith-letsencrypt-tls`, and `hexalith-pages-letsencrypt-tls` ready before running `publish.ps1`.

## 3. Recommended Approach

Direct Adjustment. No PRD or epic reorder is required. The change is a deployment guard and documentation correction aligned with the existing Kubernetes manifests.

Effort: Low.
Risk: Low to medium. The stricter preflight may block clusters that still depend on local nginx or NodePort registry access; that is intentional.

## 4. Detailed Change Proposals

Deployment script:

OLD:

- Publish accepted Docker/Zot credentials and later verified image manifests at `registry.hexalith.com`, without proving that the registry host was served through Kubernetes nginx.

NEW:

- Publish validates `IngressClass/nginx-public`.
- Publish validates `Service/zot` is `ClusterIP` and has no `nodePort`.
- Publish validates `Ingress/zot-ingress` routes `registry.hexalith.com/` to `Service/zot:5000` with TLS Secret `registry-hexalith-letsencrypt-tls`.
- Publish validates `hexalith-pages-letsencrypt-tls` exists before applying public UI ingress workloads.

Rationale: Deployment of every generated service depends on the registry path. Failing before image build/apply prevents accidental reliance on a local nginx bridge.

Documentation:

OLD:

- Local or host-level nginx bridge language was allowed as a temporary operational route.

NEW:

- The supported publish path is Kubernetes nginx Ingress only.

Rationale: Documentation now matches the desired deployment contract and the script enforcement.

## 5. Implementation Handoff

Scope classification: Minor.

Routed to: Developer agent for direct implementation.

Success criteria:

- `publish.ps1` fails before image generation when Zot is NodePort-backed, missing `zot-ingress`, or missing Kubernetes nginx.
- Public UI ingress remains limited to browser UI Services.
- Static deployment validator remains green.
- Local nginx bridge guidance is removed from the supported deployment path.
