---
project_name: parties
user_name: Administrator
date: 2026-06-21
scope_classification: Minor
status: implemented
supersedes: []
relates_to:
  - sprint-change-proposal-2026-06-16-kubernetes-nginx-deploy-path.md
  - sprint-change-proposal-2026-06-16-letsencrypt-certificate-deployment-alignment.md
---

# Sprint Change Proposal — Planning-artifact alignment for the K8s nginx / Let's Encrypt deploy path

## 1. Issue Summary

The 2026-06-16 deployment-hardening change (PR #48, commit `27a3afa` "fix(deploy): align
Kubernetes HTTPS ingress deployment") moved deployment to a **Kubernetes `nginx-public`
Ingress-only** path with **cert-manager Let's Encrypt HTTP-01** TLS, and added matching
`publish.ps1` preflight guards. That change updated the **implementation-side** artifacts —
`deploy/` manifests, `deploy/k8s/publish.ps1`, `docs/deployment-guide.md`,
`docs/kubernetes-deployment-architecture.md`, `docs/getting-started.md`, and
`DeployValidation.Tests` — and produced two sprint-change-proposals
(`…-kubernetes-nginx-deploy-path.md`, `…-letsencrypt-certificate-deployment-alignment.md`,
both `status: implemented`).

**Residual drift:** the two upstream **BMAD planning artifacts** were never updated to match.
`architecture.md` (D10) and `epics.md` (AR-D10 + Story 1.10) still described deployment only
coarsely ("aspirate publish → 11→12 pods; `deploy/k8s` gains ingress + OIDC config"), silent
on the nginx-public-only path, the cert-manager Let's Encrypt TLS Secrets, the Zot registry
ingress, and the `publish.ps1` preflight. This proposal closes that drift so the planning
baseline matches the live deployment contract.

**Discovery:** surfaced during the 2026-06-21 implementation-readiness re-run, while confirming
that the deployment change had been folded back through *all* artifact layers, not just `docs/`.

## 2. Impact Analysis

**Epic impact:** None. Deployment-scoped only. All 9 FRs / 9 NFRs, all 5 epics, the epic
dependency graph, and Story 1.10's *intent* are unchanged. No epic reorder, no new or obsolete
epic. The 2026-06-21 readiness verdict (✅ READY, 100% FR/NFR coverage) is unaffected.

**Story impact:** Story 1.10 (Deploy parties-ui) acceptance is **tightened** — a new
acceptance criterion makes the `publish.ps1` nginx-public + Let's Encrypt preflight an explicit,
testable deploy gate. This documents behaviour already implemented and already covered by
`OperatorScriptValidationTests` / `K8sManifestGenerationTests`; no new code work.

**Artifact impact (this change):**

- `_bmad-output/planning-artifacts/architecture.md` — D10 gains an "Ingress & TLS
  (live-cluster contract)" bullet.
- `_bmad-output/planning-artifacts/epics.md` — AR-D10 gains the nginx-public / Let's Encrypt /
  preflight sentence; **Story 1.10** gains a Given/When/Then preflight acceptance criterion.

**Artifacts already aligned (by PR #48, no action here):** `deploy/k8s/ingress.yaml`,
`deploy/zot/ingress.yaml`, `deploy/k8s/publish.ps1`, `deploy/validate-deployment.ps1`,
`docs/deployment-guide.md`, `docs/kubernetes-deployment-architecture.md`,
`docs/getting-started.md`, `DeployValidation.Tests`.

**Technical impact:** None beyond documentation. The live cluster already serves both Let's
Encrypt certificates (Ready) through `nginx-public`; this proposal only reconciles the planning
baseline to that reality.

## 3. Recommended Approach

**Direct Adjustment** (checklist Option 1). No PRD/epic reorder, no rollback, no MVP review —
identical classification to the two 2026-06-16 proposals it completes.

- **Effort:** Low (three documentation edits).
- **Risk:** Low. Documents already-implemented, already-tested behaviour; no manifests, code,
  or live resources are touched.

## 4. Detailed Change Proposals

### Edit A — `architecture.md` · §Infrastructure & Deployment · D10

**Added** (after the "Containers/K8s" bullet) a new **"Ingress & TLS (live-cluster contract)"**
bullet: browser UI workloads publish only through the `nginx-public` Ingress class
(`deploy/k8s/ingress.yaml`) — no local/host nginx bridge; images push to
`registry.hexalith.com` served by Zot behind its own `nginx-public` Ingress
(`registry.hexalith.com/` → `Service/zot:5000`, `ClusterIP`); TLS is cert-manager Let's Encrypt
HTTP-01 (`hexalith-pages-letsencrypt-tls` for pages, `registry-hexalith-letsencrypt-tls` for the
registry); `publish.ps1` preflights all of it and fails before image build if any is missing.

### Edit B — `epics.md` · Architecture Requirements · AR-D10

**Appended** a sentence: deploy path is Kubernetes `nginx-public` Ingress only with cert-manager
Let's Encrypt TLS (the two named Secrets), and `publish.ps1` preflights the class, the Zot
Ingress, and both Secrets before image build.

### Edit C — `epics.md` · Story 1.10 · Acceptance Criteria

**Added** a Given/When/Then:

> **Given** the live Kubernetes cluster · **When** `deploy/k8s/publish.ps1` runs · **Then** it
> preflights the `nginx-public` Ingress class, the Zot registry Ingress
> (`registry.hexalith.com/` → `Service/zot:5000`, `ClusterIP`, no NodePort), and both
> cert-manager Let's Encrypt TLS Secrets (`hexalith-pages-letsencrypt-tls`,
> `registry-hexalith-letsencrypt-tls`), and **fails before image build/apply** if any is
> missing — there is **no local / host-level nginx bridge fallback**.

### Housekeeping (readiness-report rec #4)

Archived the two superseded readiness reports
(`implementation-readiness-report-2026-06-09.md`, `…-2026-06-09-v2.md`) into
`_bmad-output/planning-artifacts/archive/`. The 2026-06-21 report is now the single current one.

## 5. Implementation Handoff

**Scope classification:** Minor.

**Routed to:** Developer agent — implemented in-line (documentation reconciliation only).

**Success criteria:**

- `architecture.md` D10 and `epics.md` AR-D10 / Story 1.10 name the `nginx-public`-only path,
  both Let's Encrypt TLS Secrets, the Zot registry ingress, and the `publish.ps1` preflight.
- Planning baseline now matches `deploy/`, `docs/`, and `DeployValidation.Tests` — zero residual
  drift between the two 2026-06-16 implemented proposals and the upstream planning artifacts.
- Only one readiness report remains in `planning-artifacts/`; the two stale ones are archived.
- No manifests, code, tests, or live cluster resources changed.
