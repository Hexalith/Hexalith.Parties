# Sprint Change Proposal — 2026-05-18

**Project:** Hexalith.Parties
**Author:** Jérôme (with Correct-Course workflow)
**Date:** 2026-05-18
**Scope classification:** Moderate (new epic + new PRD requirement + new architecture decision)

---

## 1. Issue Summary

Hexalith.Parties has, up to now, demonstrated deployability only via the local Aspire AppHost (`src/Hexalith.Parties.AppHost`). PRD FR31 currently reads "Developer can deploy a running instance from source with standard container tooling" — intentionally vague, with no Kubernetes commitment. The `deploy/` directory contains only DAPR component templates (`deploy/dapr/*.yaml`); no Kubernetes manifests, Helm chart, Kustomize overlays, or aspirate output exist in the repository. Epic 3 Story 3.6 ("Enable One-Command Local Run") covers Aspire-only local run, and Story 3.9 ("Add Deployment Security Validation") covers runtime configuration, not deployment manifests.

This proposal records a strategic pivot: **Kubernetes deployment to a local cluster (kind / minikube / k3d / Docker Desktop) is now in MVP scope**, generated from the existing Aspire AppHost via aspirate (aspir8) so that the Aspire model remains the single source of truth.

**Discovery:** Direct stakeholder decision (Jérôme, 2026-05-18) during the Correct-Course workflow.

---

## 2. Impact Analysis

### Epic Impact

| Epic | Impact | Action |
|---|---|---|
| Epic 3 — Developer Integration and Local Adoption | Out of scope semantically — Epic 3 is packages + Aspire-local + docs; K8s is a separate concern. Story 3.6 (Aspire local run) and Story 3.7 (getting-started doc) remain valid and will reference Epic 9 stories. | No structural changes inside Epic 3. |
| Epics 1, 2, 4, 5, 6, 7, 8 | No story-level impact. They deploy via the new K8s path but their stories don't change. | None. |
| **Epic 9 — Kubernetes Deployment (new)** | Holds the two new K8s deployment stories. Phase: MVP. | Add Epic 9 with Stories 9.1 and 9.2. |

### Artifact Conflicts

| Artifact | Conflict / Update | Resolution |
|---|---|---|
| `prd.md` (FR31, FR31a) | FR31 was tooling-agnostic — no K8s commitment | FR31 rewritten to name Kubernetes target; new FR31a locks aspirate as generator |
| `prd.md` (NFR30) | < 15 min target | Unchanged — still tooling-agnostic, applies to K8s path |
| `architecture.md` (Infrastructure & Deployment section) | No K8s decision record | New ADR `D-K8s — Kubernetes Deployment via Aspirate from Aspire Model` inserted after D13 |
| `architecture.md` (directory tree, ~line 922) | `deploy/` only showed `deploy/dapr/` | Added `deploy/k8s/` subtree (deployments/, dapr/, README.md) |
| `epics.md` (Epic List section + Epic 9 body) | No Epic 9 | Epic 9 entry added in Epic List; full Epic 9 section appended with Stories 9.1 and 9.2 |
| `sprint-status.yaml` | No Epic 9 tracking | Epic 9 block added with stories 9-1 and 9-2 (status `backlog`); `last_updated` bumped |
| `tests/Hexalith.Parties.DeployValidation.Tests` | Validates config only | Will be extended by Story 9.2 (no structural change to this proposal) |
| `docs/` (getting-started) | Aspire-only walkthrough | Will be extended by Story 9.1's AC referencing K8s-mode walkthrough |
| `src/Hexalith.Parties.AppHost` | Must remain aspirate-compatible | Captured in ADR `D-K8s` consequences; no immediate change |
| Sibling submodule projects (EventStore, Tenants, Memories, FrontComposer) | Included in generated topology as Aspire resources | No code changes; submodules may add their own deploy stories later |

### Technical Impact

- **New tooling dependency:** aspirate (aspir8) added to documented prerequisites; version pinned in `global.json` or equivalent (per ADR D-K8s).
- **New artifact directory:** `deploy/k8s/` (generated, checked into repo).
- **DAPR component parity:** Authoritative source remains `deploy/dapr/*.yaml`; aspirate-emitted DAPR CRs are validated against these (Story 9.2 AC).
- **Local-cluster scope only:** Managed cloud (AKS/EKS/GKE) explicitly out of scope until post-MVP. Validation tooling enforces a local-cluster context allowlist (Story 9.1 + 9.2 ACs).
- **No actor-host changes:** `src/Hexalith.Parties` actor host boundaries unaffected — no new REST/MCP exposure.

---

## 3. Recommended Approach

**Selected path: Option 1 — Direct Adjustment (with new epic).**

Rationale:

- Epic 3 hasn't started — adding work *now* is low-cost and doesn't disrupt in-progress stories.
- Aspirate keeps the Aspire model as single source of truth — no model drift risk.
- Local-cluster scope is contained — no managed-cloud secrets, identity, or registry decisions required for MVP.
- Generated manifests in the repo enable PR review of deployment topology changes and architectural fitness tests over the artifacts.
- Semantic separation: K8s deployment is its own concern, hence Epic 9 rather than Epic 3 extension.

**Rejected alternatives:**

- Option 2 (Rollback): N/A — nothing started to roll back.
- Option 3 (MVP Review): Not needed — this *is* the MVP-scope expansion, not a reduction.
- Extending Epic 3 with K8s stories: Rejected because Epic 3's theme is developer-integration (packages + Aspire-local + docs); K8s deployment is operationally distinct.

**Estimates:**

- Story 9.1 (generate + deploy): Medium effort, Low risk.
- Story 9.2 (manifest validation): Medium effort, Low risk.
- Documentation/PRD/architecture edits: Already applied (this proposal).

---

## 4. Detailed Change Proposals (Applied)

### Edit 1 — PRD `prd.md`

**Applied:** Lines 712–713 (now lines 712–714).

- **FR31** rewritten to name Kubernetes target and local-cluster MVP scope.
- **FR31a** added: locks aspirate as generator, names Aspire model as single source of truth, lists in-scope sibling submodules.

### Edit 2a — Architecture `architecture.md` (ADR D-K8s)

**Applied:** Inserted between D13 and D14 in "Infrastructure & Deployment" section.

- Decision: aspirate from Aspire AppHost.
- Rejected: hand-authored Helm, Kustomize-only, direct kubectl-apply.
- Consequences: local-cluster MVP scope; aspirate version pinned; documented deploy workflow; sibling submodules included.
- Affects: AppHost, `deploy/k8s/`, DeployValidation tests, getting-started doc, Story 3.6.

### Edit 2b — Architecture `architecture.md` (directory tree)

**Applied:** Lines 922–931.

- `deploy/` now shows two subtrees: `dapr/` (existing) and `k8s/` (new): `deployments/`, `dapr/`, `README.md`.

### Edit 3 — Epics `epics.md` (Epic 9 + Story 9.1)

**Applied:**

- Epic 9 entry added in the Epic List section after Epic 8 (MVP, FRs FR31/FR31a, supports FR60/FR61/NFR30).
- `## Epic 9: Kubernetes Deployment` section appended at end of file.
- **Story 9.1: Generate Kubernetes Artifacts and Deploy Full Topology to Local Cluster** — six AC blocks covering:
  1. Aspirate generation (deterministic output, sibling-submodule inclusion, DAPR component parity).
  2. One-command deploy script with local-context allowlist enforcement.
  3. Pod readiness + DAPR sidecar health.
  4. K8s-mode first-command walkthrough meeting NFR30 (< 15 min).
  5. Clean teardown (no stale state).
  6. Validation tests / smoke checks (no recursive submodule init, no non-local contexts).

### Edit 4 — Epics `epics.md` (Story 9.2)

**Applied:** Appended under Epic 9 after Story 9.1.

- **Story 9.2: Extend Deployment Validation to Kubernetes Manifests** — six AC blocks covering:
  1. Manifest lint (image refs, resource limits, probes, DAPR annotations, ConfigMaps).
  2. DAPR CR parity vs `deploy/dapr/` templates.
  3. Secret-handling lint.
  4. Local-cluster context capability check.
  5. Bounded, machine-readable, secret-safe output.
  6. Test coverage; Story 3.9 config-validation tests remain unchanged.

### Edit 5 — Sprint-status `sprint-status.yaml`

**Applied:**

- `last_updated` bumped with K8s pivot note.
- New Epic 9 block added with `epic-9: backlog`, `9-1-generate-k8s-artifacts-and-deploy-full-topology-to-local-cluster: backlog`, `9-2-extend-deployment-validation-to-kubernetes-manifests: backlog`, `epic-9-retrospective: optional`.

---

## 5. Implementation Handoff

**Scope classification:** Moderate (new epic + PRD requirement + architecture decision; no code yet, but backlog reorganization needed).

**Routing:**

- **Product Owner / Developer (Amelia):** Acknowledge the new Epic 9 in the backlog. Decide sequencing relative to Epic 3 (suggestion: Epic 9 starts when Epic 3 reaches a runnable Aspire-local state — i.e., after Story 3.6 completion — so K8s validation has a working baseline).
- **Developer (Amelia):** Implement Story 9.1 first (foundation), then Story 9.2 (validation). Stories follow the standard `create-story` → `dev-story` → `code-review` flow.
- **Architect (Winston):** Review the ADR `D-K8s` insertion at next architecture pass. No further design work required for MVP scope.
- **Tech Writer (Paige):** Update getting-started doc (Story 3.7) when Story 9.1 is in `review` — add the K8s-mode walkthrough section.

**Success criteria:**

- A developer on a clean machine with documented prerequisites can run `dotnet aspirate generate` followed by the deploy script and reach a working `CreateParty` end-to-end in < 15 minutes on a local cluster (NFR30).
- `tests/Hexalith.Parties.DeployValidation.Tests` lints generated `deploy/k8s/` manifests and fails closed on missing probes, drifted DAPR ACLs, plaintext secrets, or non-local-cluster capabilities.
- Generated manifests are deterministic for a given AppHost commit + aspirate version (regen produces identical bytes).
- No managed-cloud assumptions leak into MVP artifacts.

**Out of MVP scope (future stories):**

- Managed-cloud deployment (AKS / EKS / GKE).
- Sibling submodules' own deploy stories (EventStore, Tenants, Memories, FrontComposer).
- CI step running `aspirate generate` + diff against checked-in `deploy/k8s/` as drift detection.
- Ingress controller integration beyond port-forward.

---

## 6. Approval

**Status:** Pending explicit approval.

**Approval question:** Do you approve this Sprint Change Proposal for implementation, with the changes already applied to `prd.md`, `architecture.md`, `epics.md`, and `sprint-status.yaml`?
