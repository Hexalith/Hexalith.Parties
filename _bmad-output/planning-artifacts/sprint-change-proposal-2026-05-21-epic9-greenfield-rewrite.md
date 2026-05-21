---
title: 'Sprint Change Proposal — Epic 9 Greenfield Rewrite'
date: '2026-05-21'
proposed_by: 'Jérôme (user-directed)'
authored_by: 'bmad-correct-course (Claude)'
status: 'approved-and-executed'
approved_by: 'Jérôme (operator)'
approved_at: '2026-05-21'
executed_at: '2026-05-21'
execution_log:
  - 'epics.md Epic 9 v1 sections deleted + 7-story v2 narrative inserted'
  - 'deploy/k8s/ deleted (9 per-service folders + 5 root files + empty parent removed)'
  - 'deploy/dapr/ deleted (17 CR files + folder)'
  - 'deploy/validate-deployment.ps1 deleted'
  - 'deploy/ parent folder removed (was empty)'
  - 'tests/Hexalith.Parties.DeployValidation.Tests: 10 Epic 9 v1 test files + expected-test-names.txt deleted; csproj + DeployValidationTestCollection.cs preserved'
  - 'prd.md FR31a rewritten to reference Epic 9 v2 pipeline + canonical architecture doc'
  - 'architecture.md ADR D-K8s-3 (ConfirmContext gate) and ADR D-K8s-4 (Epic 9 v2 greenfield rewrite + canonical doc) appended after existing D-K8s-2'
  - 'sprint-status.yaml Epic 9 v1 entries marked superseded; 7 v2 entries added as backlog with blocked_by graph'
scope_class: 'Major (full-epic redesign + artefact regeneration)'
supersedes:
  - 'sprint-change-proposal-2026-05-12-readiness-correction.md (Epic 9 readiness slice)'
  - 'sprint-change-proposal-2026-05-13.md (Epic 9 readiness extensions)'
  - 'sprint-change-proposal-2026-05-14.md (Epic 9 zot pre-flight)'
  - 'sprint-change-proposal-2026-05-15.md (Epic 9 readiness fold-in)'
  - 'sprint-change-proposal-2026-05-17.md + readiness-follow-up'
  - 'sprint-change-proposal-2026-05-18.md (Epic 9 K8s pivot)'
  - 'sprint-change-proposal-2026-05-19.md (Epic 9 follow-ups)'
  - 'sprint-change-proposal-2026-05-20-zot-build-push.md (Story 9.5 v1)'
artefacts_wiped:
  - 'deploy/k8s/* (except namespace.yaml regenerated, README.md regenerated, publish.ps1 regenerated, teardown.ps1 regenerated, kustomization.yaml regenerated)'
  - 'deploy/dapr/*'
  - 'deploy/validate-deployment.ps1'
  - 'tests/Hexalith.Parties.DeployValidation.Tests/K8sManifest*.cs and related fitness tests'
canonical_reference: 'docs/kubernetes-deployment-architecture.md'
---

# Sprint Change Proposal — Epic 9 Greenfield Rewrite

## 1. Issue Summary

### Problem statement

Epic 9 (Kubernetes Deployment) has grown organically across **9 sprint-change-proposals + 2 follow-up stories** between 2026-05-12 and 2026-05-20. Each proposal patched the previous state — adding a story, splitting a contract, superseding an AC, folding in a readiness gap. The current Epic 9 in `epics.md` is functionally correct but no longer cleanly readable:

- Story 9.1 carries an **Addendum** dated 2026-05-20 that supersedes part of its AC1 contract.
- Story 9.5 was added as a third story (between 9.2 and a missing 9.3/9.4) and renumbered the original 9.3 (`close K8s deployment spec gaps`) implicitly into prior history.
- Two follow-up stories (9.10, 9.11) exist in the implementation-artifacts folder without narrative anchoring in `epics.md`.
- The deployed architecture (the **9-pod topology** with Zot registry, `-ConfirmContext` gate, MinVer-stamped tags, 3 operator-managed Secrets, 3 configuration sources) is fully captured in `docs/kubernetes-deployment-architecture.md` (281 lines, 13 sections, authored 2026-05-21) — but Epic 9 narrative does not match this final state.

### Context of discovery

The architecture document `docs/kubernetes-deployment-architecture.md` (committed `9c97b8a` on 2026-05-21) crystallised the final K8s deployment shape. With that document as the new canonical reference, the operator (Jérôme) requested a complete **greenfield rewrite of Epic 9** — replanning the work as if the team were planning Epic 9 today, given the final architecture, instead of layering patches on the historical record.

### Evidence

- `docs/kubernetes-deployment-architecture.md` §1–§13 — the canonical architecture.
- `_bmad-output/planning-artifacts/epics.md` lines 350–357 (epic blurb) + 3113–3265 (stories 9.1/9.2/9.5 with addenda) — the current fragmented state.
- 9 dated `sprint-change-proposal-2026-05-*.md` files in `_bmad-output/planning-artifacts/` — the patch chain that produced the current state.
- `deploy/k8s/`, `deploy/dapr/`, `deploy/validate-deployment.ps1`, `deploy/k8s/{publish,teardown}.ps1`, `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifest*.cs` — the existing implementation, functional but slated for full regeneration per the operator's directive.

### Operator's directive (verbatim, 2026-05-21)

> "j'aimerais que tu analyses cette documentation expliquant l'architecture finale du deploiement sur Kube et que recreer totalement l'epic 9 du début sans prendre en compte l'état actuel de Kube je veux que tu recreer tout les fichiers et scripts pour obtenir le meilleur résultat"

### Scope choices confirmed during this workflow

- **Scope class**: Replan + artefact regeneration (Option 2 of the proposal-mode question).
- **Old stories**: delete from `epics.md` (git history preserves the trace).
- **Preservation**: nothing — wipe carve-outs, fitness tests, retros are kept on disk for historical reference but not reused as source of truth.
- **Story count**: 7 (after merging publish.ps1 + teardown.ps1 into one operator-scripts story).
- **Phasing**: SCP written first → explicit approval → wipe + epics.md update + PRD/architecture updates.
- **Handoff**: pending decision (see §5 below).

---

## 2. Impact Analysis

### Epic-level impact

| Epic | Impact | Note |
|---|---|---|
| Epic 9 | **Full rewrite** | Old narrative + stories deleted, replaced with 7 greenfield stories. |
| Epic 1–8, 10–12 | None | Epic 9 is terminal in the dependency chain. No upstream contract changes. |

### Artefact-level impact

| Artefact | Action |
|---|---|
| `_bmad-output/planning-artifacts/epics.md` (Epic 9 sections) | **Delete + rewrite.** Old: lines 350–357 (blurb) + 3113–3265 (stories). New: greenfield blurb + 7 stories. |
| `_bmad-output/planning-artifacts/prd.md` (FR31a) | **Refine.** Replace generic "K8s manifest generation via aspirate" with the one-command publish-pipeline framing + 9-pod topology + 3 operator-managed Secrets pointer. |
| `_bmad-output/planning-artifacts/architecture.md` | **Append.** Reference `docs/kubernetes-deployment-architecture.md` as the canonical K8s source. Add **ADR D-K8s-2** (Zot pull-secret Path B) and **ADR D-K8s-3** (`-ConfirmContext` gate replacing the local-cluster regex allowlist). |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | **Update.** 7 backlog entries for the new stories. Old entries marked `superseded`. |
| `_bmad-output/implementation-artifacts/9-{1..11}-*.md` | **Leave on disk** (historical reference). Not reused as source of truth. |
| `deploy/k8s/*` (except top-level `namespace.yaml`, `kustomization.yaml`, `README.md`, `publish.ps1`, `teardown.ps1` — regenerated) | **Wipe.** All per-service folders + `keycloak/` + `redis/` carve-outs deleted; will be regenerated by DEV during Story 9.2 (aspirate) and Story 9.3 (carve-outs). |
| `deploy/dapr/*` | **Wipe.** All Components, Configurations, Subscriptions, Resiliency CRs deleted; regenerated by DEV during Story 9.4. |
| `deploy/validate-deployment.ps1` | **Wipe.** Regenerated by DEV during Story 9.6 (was 9.7). |
| `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifest*.cs` + related fitness tests | **Wipe.** Regenerated by DEV during Story 9.7 (was 9.8). |
| `docs/kubernetes-deployment-architecture.md` | **Unchanged.** This is the source of truth driving the rewrite. |
| `docs/getting-started.md`, `docs/deployment-guide.md`, `deploy/k8s/README.md` | **Refresh** during Story 9.1 (entry-point doc consolidation). |

### Risk inventory

| Risk | Severity | Mitigation |
|---|---|---|
| `main` branch contains a working Epic 9 implementation; the wipe will break the deploy pipeline until DEV re-implements all 7 stories. | **High** | Operator explicitly accepted this in the workflow ("Wipe maintenant + DEV écrit ensuite"). Mitigation: dev team agrees on an aggressive sequencing (9.3, 9.4 first to land the static YAML; then 9.2 + 9.5 to restore the pipeline). |
| Regression: idempotency contracts and patch anchors from `publish.ps1` v1 may be inadvertently weakened during regeneration. | **Medium** | Story 9.7 (was 9.8) fitness tests assert cross-patch idempotency as a contract. Story 9.6 (was 9.7) lint tooling re-asserts patch presence per category. |
| Documentation drift: the entry-point docs may not be updated in lockstep with the regeneration, leaving stale `regen.ps1` / `kind-*` references. | **Low** | Story 9.1 includes a `DocumentationFitnessTest` that scans entry-point docs for stale references and `:latest` patterns. |
| Operator-managed Secret semantics may regress (e.g., `hexalith-jwt-signing` regenerated on re-publish, invalidating live JWTs). | **High** | Story 9.5 AC explicitly mandates idempotent Secret bootstrap with `<created|exists>` status reporting. Story 9.7 fitness test verifies idempotency. |
| The Zot registry on the target cluster contains images already pushed under MinVer tags; rebuilding may push identical content under identical tags, which Zot accepts but does not de-duplicate. | **Low** | Acceptable. MinVer per-commit determinism means the rebuild produces byte-identical image content (same Dockerfile, same build inputs, same MinVer) and the registry simply stores the same digest. |

---

## 3. Recommended Path Forward

### Selected approach

**Hybrid: Replan Epic 9 + Regenerate artefacts (greenfield rewrite, 7-story structure)**

- **Option 1 — Direct Adjustment**: rejected. The operator explicitly chose the reset.
- **Option 2 — Rollback**: not viable. Epic 9 is merged on `main`; rollback is meaningless for a planning artefact concern.
- **Option 3 — PRD MVP Review**: not the right framing. MVP scope is preserved; the structure of Epic 9 is what changes.
- **Hybrid (selected)**: rewrite Epic 9 narrative + 7 stories in `epics.md`; mark old stories as deleted; wipe physical artefacts; let DEV regenerate via `bmad-dev-story` cycles.

### Effort + risk

| Dimension | Estimate |
|---|---|
| Effort (re-implementation by DEV) | **High** — ~7 story cycles, each cleanly scoped, no story expected to exceed 1 working day for a familiar agent. |
| Risk (regression) | **Medium-High** — fitness tests in Story 9.7 are the primary safety net. |
| Timeline impact (calendar) | **2026-05-21 to ~2026-05-28** depending on DEV agent throughput. |
| Reversibility | **High** — git history preserves the wiped artefacts; a revert restores the previous state in one command. |

---

## 4. Detailed Change Proposals

### 4.1 New Epic 9 narrative (replaces lines ~350–357 in `epics.md`)

```markdown
### Epic 9: Kubernetes Deployment Platform

Operators and developers deploy the full Hexalith.Parties 9-workload topology to a Kubernetes cluster via a single `publish.ps1` command — Zot OCI registry with MinVer-stamped image tags, hand-authored Redis + Keycloak carve-outs, Dapr control plane with deny-by-default ACLs, three operator-managed Secrets bootstrapped idempotently, and a static lint + fitness-test suite that guards every contract. The architecture is fully captured in `docs/kubernetes-deployment-architecture.md` as the canonical reference.

**Phase:** MVP
**Coverage type:** planned (greenfield rewrite, 2026-05-21, supersedes Epic 9 v1)
**FRs covered:** FR31, FR31a; supports FR60, FR61, NFR30
**Canonical reference:** `docs/kubernetes-deployment-architecture.md`
```

### 4.2 New Story set (7 stories — replaces lines ~3113–3265 in `epics.md`)

| ID | Title | Surface | Architecture reference |
|---|---|---|---|
| **9.1** | Zot OCI Registry & Deployment Documentation | Zot deployment + Ingress + htpasswd + tagging policy + ADR D-K8s-2 + ADR D-K8s-3 + canonical doc pointers | §5, §13 |
| **9.2** | Aspire AppHost → Aspirate Manifest Composition | AppHost composable + `dotnet aspirate generate` + 7 per-service folders + 3 patch contracts | §3.1, §7 Source 1 |
| **9.3** | Hand-Authored Carve-Outs (Redis + Keycloak) | Redis emptyDir + no AUTH; Keycloak admin via `secretKeyRef`; carve-outs preserved at every regen | §3, §7 Source 3 |
| **9.4** | Dapr Control Plane, Components, ACL, Subscriptions | `dapr init -k`; 3 Components; 5 ACL deny-by-default; 2 Subscriptions; server-side dry-run | §4, §7 Source 2 |
| **9.5** | Operator Scripts (publish.ps1 + teardown.ps1) | Shared `-ConfirmContext` gate + `_lib/Confirm-KubeContext.ps1` helper; 13-phase publish; 7-phase teardown + residual-state probe | §2, §6, §8, §10 |
| **9.6** | validate-deployment.ps1 Lint Tooling | 8 lint categories; BLOCKING/WARNING severity; JSON output for CI; poison sweep self-test | §8 step 10 |
| **9.7** | Deployment Fitness Tests + Live-Cluster Integration | 8 fitness classes (default test pass) + 3 trait-gated `LiveCluster` tests; hand-curated fixtures | §11 |

(Full AC bodies appear in the appendix §A below — same content as the edit proposals reviewed and approved in the workflow conversation.)

### 4.3 PRD update (`prd.md`)

**FR31a — current text (placeholder, exact phrasing in prd.md):**
> Kubernetes manifest generation via aspirate is the supported deployment path.

**FR31a — proposed text:**
> A single PowerShell pipeline (`deploy/k8s/publish.ps1 -ConfirmContext <name>`) takes the operator from a clean checkout to a healthy 9-pod cluster in one command: MinVer-resolved image tags, build + push to the self-hosted Zot OCI registry at `registry.hexalith.com`, regenerate Kubernetes manifests via `dotnet aspirate generate`, apply three post-aspirate patches (Dapr annotations + JWT `secretKeyRef` + Zot `imagePullSecrets`), bootstrap three operator-managed Secrets (`hexalith-jwt-signing`, `hexalith-keycloak-admin`, `zot-pull-secret`) idempotently, apply Dapr CRs from `deploy/dapr/`, then apply the Kustomization. The full architecture is documented in `docs/kubernetes-deployment-architecture.md`. NFR30 (< 15 min from clean checkout to first successful query) remains in force.

### 4.4 Architecture update (`architecture.md`)

Append a new top-level section reference:

> **Kubernetes Deployment Architecture.** The full K8s deployment shape — 9-workload topology, configuration sources, operator workflow, reproducibility guarantees, MVP boundaries — is documented in `docs/kubernetes-deployment-architecture.md`. That document is the canonical reference for any K8s-related work; this section captures only the ADRs that bind the design.

Add two ADRs:

**ADR D-K8s-2 — Zot Pull-Secret Path B (re-emit auths block wholesale)**

- **Context.** `publish.ps1` must bootstrap a `kubernetes.io/dockerconfigjson` Secret named `zot-pull-secret` from the operator's `~/.docker/config.json` so that consumer Deployments can pull images from `registry.hexalith.com` (a private, htpasswd-protected Zot registry).
- **Decision.** The script re-emits the `auths["registry.hexalith.com"]` block from the operator's docker config wholesale into the Secret payload. The Base64-encoded credential is never decoded, never echoed to stdout/stderr/manifest YAML, never written to disk outside the Secret. `credsStore` / `credHelpers` configurations are unsupported — the script exits 6 with an actionable message.
- **Rationale.** Minimises credential surface, avoids re-implementing a docker credential resolver in PowerShell, and lets `docker login -u parties-publisher` remain the only credential-handling step.
- **Consequences.** Operators must configure docker with the `auths` block (not a credential helper). The poison-sweep test in Story 9.7 asserts no credential leak across the full publish stdout.

**ADR D-K8s-3 — `-ConfirmContext` Gate (replaces local-cluster regex allowlist)**

- **Context.** Epic 9 v1 used a regex allowlist (`kind-*`, `minikube`, `docker-desktop`, `k3d-*`) to refuse runs against non-local kubectl contexts. With Zot now on a real cluster, the same scripts must run against any operator-owned context.
- **Decision.** Both `publish.ps1` and `teardown.ps1` require a mandatory `-ConfirmContext <name>` parameter that must match `kubectl config current-context` exactly. The two scripts share a helper module `deploy/k8s/_lib/Confirm-KubeContext.ps1`. On mismatch, exit 2 with `expected '<arg>', got '<active>'` — no URL, no CA, no token echoed.
- **Rationale.** Operator chooses the target context explicitly each invocation; the script is portable across local and remote clusters; the gate is human-verified, not regex-pattern-matched.
- **Consequences.** Every invocation requires the operator to type the current context name. Scripts cannot silently drift to a different context (e.g., if the operator changed contexts between sessions).

### 4.5 Sprint-status update (`sprint-status.yaml`)

Action: mark old Epic 9 entries (`9.1`, `9.2`, `9.3`, `9.5`, `9.10`, `9.11`) as `superseded` with a pointer to this SCP. Add 7 backlog entries:

```yaml
epic_9:
  status: planned-greenfield
  superseded_proposals:
    - 2026-05-12-readiness-correction
    - 2026-05-13, 2026-05-14, 2026-05-15
    - 2026-05-17, 2026-05-17-readiness-follow-up
    - 2026-05-18, 2026-05-19, 2026-05-20-zot-build-push
  stories:
    - id: 9.1
      title: Zot OCI Registry & Deployment Documentation
      status: backlog
    - id: 9.2
      title: Aspire AppHost → Aspirate Manifest Composition
      status: backlog
      blocked_by: [9.1]
    - id: 9.3
      title: Hand-Authored Carve-Outs (Redis + Keycloak)
      status: backlog
    - id: 9.4
      title: Dapr Control Plane, Components, ACL, Subscriptions
      status: backlog
      blocked_by: [9.3]   # Redis must exist before Components reference it
    - id: 9.5
      title: Operator Scripts (publish.ps1 + teardown.ps1)
      status: backlog
      blocked_by: [9.2, 9.3, 9.4]
    - id: 9.6
      title: validate-deployment.ps1 Lint Tooling
      status: backlog
      blocked_by: [9.2, 9.3, 9.4]
    - id: 9.7
      title: Deployment Fitness Tests + Live-Cluster Integration
      status: backlog
      blocked_by: [9.5, 9.6]
```

### 4.6 Wipe inventory (executed after SCP approval)

```
Delete:
  deploy/k8s/eventstore/
  deploy/k8s/eventstore-admin/
  deploy/k8s/eventstore-admin-ui/
  deploy/k8s/keycloak/
  deploy/k8s/memories/
  deploy/k8s/parties/
  deploy/k8s/parties-mcp/
  deploy/k8s/redis/
  deploy/k8s/tenants/
  deploy/k8s/publish.ps1
  deploy/k8s/teardown.ps1
  deploy/k8s/README.md
  deploy/k8s/kustomization.yaml
  deploy/k8s/namespace.yaml
  deploy/dapr/                       (whole directory — all 17 files)
  deploy/validate-deployment.ps1
  tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestGenerationTests.cs
  tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestPublishTests.cs
  (and related fitness-test files in that project; preserve the project file itself)

Preserve:
  docs/kubernetes-deployment-architecture.md  (canonical source of truth)
  _bmad-output/planning-artifacts/epic-9-retro-2026-05-07.md
  _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-*.md  (history)
  _bmad-output/implementation-artifacts/9-{1..11}-*.md                  (history)

After wipe, DEV regenerates each artefact via bmad-dev-story per the new 7-story plan.
```

---

## 5. Implementation Handoff

### Scope classification

**Major.** Full-epic redesign + artefact regeneration + multiple downstream artefact updates (PRD, architecture, sprint-status, documentation, fitness tests).

### Handoff recipients

| Role | Responsibility |
|---|---|
| **PM (John)** or operator (Jérôme) | Approve this SCP. Update `prd.md` FR31a per §4.3. |
| **Architect (Winston)** or operator (Jérôme) | Update `architecture.md` per §4.4 (canonical-doc pointer + ADRs D-K8s-2 and D-K8s-3). |
| **DEV agent (Amelia)** via `bmad-dev-story` | Implement Stories 9.1 → 9.7 sequentially per the `blocked_by` graph in §4.5. Each story execution regenerates its slice of `deploy/`, scripts, or tests from scratch. |
| **TEA agent (Murat)** via `bmad-testarch-trace` | After Story 9.7 lands, regenerate the traceability matrix and quality gate. |

### Success criteria

1. `epics.md` contains exactly the 7-story Epic 9 from §4.2; no historical addenda; no `Story 9.10`/`9.11` references.
2. `deploy/k8s/`, `deploy/dapr/`, and `deploy/validate-deployment.ps1` are wiped immediately after SCP approval (before DEV starts work).
3. `pwsh deploy/k8s/publish.ps1 -ConfirmContext <name>` (post Story 9.5 landing) takes the operator from a clean checkout to 9 Ready pods in ≤ 10 minutes.
4. `dotnet test --filter FullyQualifiedName~DeployValidation` (post Story 9.7 landing) passes; the trait-gated `LiveCluster` tests are excluded from the default pass.
5. `validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/` (post Story 9.6 landing) exits 0 against the regenerated tree.
6. `docs/kubernetes-deployment-architecture.md` is referenced from `deploy/k8s/README.md`, `docs/getting-started.md`, `docs/deployment-guide.md`, and `architecture.md` as the canonical K8s reference.

### Out of scope for this SCP

- Production hardening (PVC + StatefulSet + replication for Redis, HPA, PDB, resource-limit envelope sizing) — deferred per `docs/kubernetes-deployment-architecture.md` §12.
- External Ingress for Hexalith services (Zot + Keycloak already have Ingress) — deferred.
- TLS termination on Hexalith services — deferred (cluster-edge concern).
- OpenTelemetry collector stack (Prometheus / Loki / Tempo / Grafana) — deferred.
- `cosign` image signing + SBOM emission + registry vulnerability scanning — deferred.
- Multi-cluster / multi-region — deferred.

---

## A. Appendix — Full AC bodies for the 7 stories

> This appendix is the verbatim content reviewed and approved in the workflow conversation on 2026-05-21. It will be copy-pasted into the new Epic 9 section of `epics.md` when the SCP is approved.

### Story 9.1: Zot OCI Registry & Deployment Documentation

**Phase:** MVP
**Coverage type:** planned
**Requirements covered:** FR31a; supports FR31, FR60, FR61, NFR30

As an operator preparing Hexalith.Parties for cluster deployment,
I want a Zot OCI registry deployed on the target cluster with htpasswd-scoped access, a single canonical architecture reference, and decision records covering the credential pipeline and the kubectl-context gate,
So that the build+push+apply path has one authoritative registry, one authoritative architecture document, and the rationale for non-obvious operational choices is captured in stable ADRs reviewers can cite.

**Acceptance Criteria:**

**Given** a Kubernetes cluster reachable from the operator's workstation
**When** the Zot deployment manifest is applied (namespace `zot`, distinct from `hexalith-parties`)
**Then** Zot runs as a single Pod fronted by an nginx Ingress exposing `registry.hexalith.com` over HTTPS
**And** the Ingress terminates TLS at the cluster edge (not inside Zot)
**And** the namespace contains no Hexalith service workloads (registry concern is isolated from application concern).

**Given** Zot is reachable
**When** authentication is exercised
**Then** access is enforced via an htpasswd file mounted from Secret `zot-auth-secret`
**And** `accessControl.groups.admins` (members: `jpiquot`, `qdassivignon`) grants push + pull + delete
**And** `accessControl.groups.builders` (members: `kaniko`, `github-ci`, `parties-publisher`) grants push + pull only
**And** anonymous access is denied for all repositories.

**Given** the credential separation policy
**When** `publish.ps1` consumes registry credentials
**Then** it reads the `parties-publisher` entry from `~/.docker/config.json` exclusively
**And** human admin credentials (`jpiquot`, `qdassivignon`) are documented as out-of-band for emergency operations only
**And** `publish.ps1` exits 6 if the `parties-publisher` entry is missing, malformed, or sources credentials through `credsStore` / `credHelpers` (Path B requirement — see ADR D-K8s-2).

**Given** the tagging policy must be deterministic per commit
**When** image tags are produced
**Then** the policy is documented as:
  - **git tag** form: `vMAJOR.MINOR.PATCH` on `main` for stable releases (the `v` prefix is MinVer's tag-recognition prefix only — see `docs/kubernetes-deployment-architecture.md` §13)
  - **image tag** form: `MAJOR.MINOR.PATCH` for stable releases (e.g., `0.2.0`)
  - **image tag** form: `MAJOR.MINOR.PATCH-preview.0.N` for preview commits past the last tag (N = `git rev-list --count v<last>..HEAD`)
  - **image tag** form: `MAJOR.MINOR.PATCH-preview.0.N+dirty` for uncommitted-tree builds (warn-and-proceed in `publish.ps1`; rejected by `validate-deployment.ps1` for any tag destined to ship)
**And** the documentation explicitly forbids mutable tags (`latest`, `staging-latest`, empty tag) for any `registry.hexalith.com/*` image consumed by `deploy/k8s/`
**And** `+dirty` build-metadata is permitted by `publish.ps1` (with warning) but rejected by `validate-deployment.ps1` as a blocking lint failure for any tag that ships to a real cluster.

**Given** `docs/kubernetes-deployment-architecture.md` exists in the repository
**When** any K8s deployment reference is consulted (`deploy/k8s/README.md`, `docs/getting-started.md`, `docs/deployment-guide.md`, `architecture.md`)
**Then** each entry-point document links to `docs/kubernetes-deployment-architecture.md` as the single canonical source of truth for cluster topology, configuration sources, and operator workflow
**And** none of these documents duplicates the topology table, the configuration-source taxonomy, or the publish-pipeline phase list (they reference the canonical doc instead).

**Given** the Zot pull-secret pipeline is operationally non-obvious
**When** ADR D-K8s-2 (Zot Pull-Secret Path B) is authored in `architecture.md`
**Then** the ADR documents: (a) `publish.ps1` re-emits the `auths["registry.hexalith.com"]` block from `~/.docker/config.json` wholesale into Secret `zot-pull-secret` (Path B); (b) the operator's password / token is never decoded, never echoed to stdout/stderr/manifest YAML, never written to disk outside the Secret payload; (c) `credsStore` / `credHelpers` configurations are explicitly unsupported and the script exits with an actionable message referencing the `docker login -u parties-publisher` + `$env:DOCKER_CONFIG` mitigations; (d) rationale: keeps the credential surface minimal and auditable, and avoids re-implementing a docker credential resolver in PowerShell.

**Given** the publish-context gate is operationally non-obvious
**When** ADR D-K8s-3 (`-ConfirmContext` Gate) is authored in `architecture.md`
**Then** the ADR documents: (a) `publish.ps1` and `teardown.ps1` require a mandatory `-ConfirmContext <name>` parameter that must match `kubectl config current-context` exactly; (b) the legacy local-cluster regex allowlist (`kind-*`, `minikube`, `docker-desktop`, `k3d-*`) is removed because the registry now lives on a real cluster and the same script must run against any operator-owned context; (c) on mismatch the script exits 2 with `expected '<arg>', got '<active>'` and does not echo cluster URLs, certificate authorities, or tokens; (d) the active context name is echoed once at the start of the run (auditability) and never again.

**Given** the deployment entry-point documents
**When** `deploy/k8s/README.md`, `docs/getting-started.md`, and `docs/deployment-guide.md` are updated
**Then** each document includes: a Zot credentials subsection citing `docker login -u parties-publisher registry.hexalith.com`, the one-command publish snippet (`pwsh deploy/k8s/publish.ps1 -ConfirmContext <name>`), the one-command teardown snippet, and a pointer to `docs/kubernetes-deployment-architecture.md` for full topology
**And** none of these documents prints credentials, tokens, certificate authorities, or example secret values.

**Given** documentation consistency is a fitness concern
**When** a documentation-fitness test (or pre-commit hook) runs against the entry-point documents
**Then** any of the following is flagged as a blocking failure: a `:latest` reference inside a `registry.hexalith.com/*` snippet, a stale reference to `regen.ps1` / `deploy-local.ps1` / `teardown-local.ps1` / `kind-*` / `minikube` (superseded names), a missing pointer to the canonical architecture doc, or a leaked credential pattern (`Password:`, `Bearer eyJ`, `auths.*:[^{]`).

### Story 9.2: Aspire AppHost → Aspirate Manifest Composition

**Phase:** MVP
**Coverage type:** planned
**Requirements covered:** FR31, FR31a; supports FR60, FR61, NFR30

As a developer maintaining the deployment topology,
I want the Aspire AppHost (`src/Hexalith.Parties.AppHost/Program.cs`) to be the single source of truth for the composable Hexalith services, with `dotnet aspirate generate` emitting deterministic per-service Kubernetes manifests under `deploy/k8s/<app-id>/` stamped with the MinVer-resolved image tag,
So that adding a service, changing a Dapr app-id, or bumping a version is a one-file edit in the AppHost — never a hand-edit in `deploy/k8s/`.

**Acceptance Criteria:**

**Given** the Aspire AppHost resource graph
**When** the graph is inspected after a clean build
**Then** it composes exactly the following composable workloads with the listed app-ids: `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`, `memories`
**And** services requiring Dapr (`eventstore`, `eventstore-admin`, `parties`, `tenants`, `memories`) are declared with their daprd sidecar attached (`WithDaprSidecar`) and a stable `app-id` matching the K8s folder name
**And** non-Dapr workloads (`eventstore-admin-ui`, `parties-mcp`) are declared without a daprd sidecar
**And** the hand-authored carve-outs (`redis`, `keycloak`) are NOT defined in the AppHost (they live in `deploy/k8s/{redis,keycloak}/` per Story 9.3).

**Given** the operator runs `dotnet aspirate generate` (invoked by `publish.ps1` — see Story 9.5)
**When** generation completes
**Then** the following per-service folders exist under `deploy/k8s/`: `eventstore/`, `eventstore-admin/`, `eventstore-admin-ui/`, `parties/`, `parties-mcp/`, `tenants/`, `memories/`
**And** each folder contains at minimum `deployment.yaml`, `service.yaml`, and (where applicable) `configmap.yaml`
**And** every `deployment.yaml` carries an image reference of shape `registry.hexalith.com/<app-id>:<MinVer>` (never `:latest`, never an empty tag)
**And** the `<MinVer>` segment matches the regex `^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$`.

**Given** aspirate emits placeholder files some users do not need
**When** the post-generation cleanup phase runs (`publish.ps1` step 4)
**Then** known-orphan files (e.g., `aspirate-readme.md`, `azure.bicep`, sample-only YAMLs) are stripped
**And** the hand-authored carve-outs in `deploy/k8s/redis/` and `deploy/k8s/keycloak/` are not touched by the cleanup
**And** `deploy/k8s/kustomization.yaml` references all per-service folders + carve-outs.

**Given** Dapr metadata is added by aspirate but needs project-specific values
**When** the post-aspirate Dapr-annotation patch runs (`publish.ps1` step 5)
**Then** every Dapr-equipped Deployment has `dapr.io/enabled: "true"`, `dapr.io/app-id: <service>`, `dapr.io/app-port: "8080"`, and `dapr.io/config: accesscontrol[-<service>]` annotations on its pod template
**And** the patch is idempotent (re-running on already-patched YAML produces no diff)
**And** the patch never injects Dapr annotations into non-Dapr workloads (`eventstore-admin-ui`, `parties-mcp`, `redis`, `keycloak`).

**Given** consumer Deployments need the registry pull secret
**When** the post-aspirate `imagePullSecrets` patch runs (`publish.ps1` step 7)
**Then** every Deployment whose container image starts with `registry.hexalith.com/` carries `spec.template.spec.imagePullSecrets: [{ name: zot-pull-secret }]` at the pod-template level
**And** vendor-image Deployments (`keycloak`, `redis`) do NOT receive `imagePullSecrets`
**And** the patch is idempotent.

**Given** the byte-determinism reproducibility contract from `docs/kubernetes-deployment-architecture.md` §11
**When** `publish.ps1` runs twice in succession on the same commit on the same workstation
**Then** for every Aspirate-emitted `deployment.yaml`, every line except the `image:` line is byte-identical across runs
**And** the `image:` line resolves to the same MinVer tag on the same commit (allowed to differ across commits)
**And** the hand-authored carve-outs (`redis/`, `keycloak/`) are byte-identical across runs unconditionally.

**Given** the aspirate tool version is pinned
**When** `.config/dotnet-tools.json` is inspected
**Then** `aspirate` is pinned at `9.1.0` (or the version current at story execution, captured as the new pinned baseline) with no `--prerelease` resolution flag drift
**And** the AppHost SDK version is captured in `docs/kubernetes-deployment-architecture.md` §13 Quick Reference (currently `13.3.3`).

### Story 9.3: Hand-Authored Carve-Outs (Redis + Keycloak)

**Phase:** MVP
**Coverage type:** planned
**Requirements covered:** FR31a; supports FR31, FR60, NFR30

As a developer maintaining the deployment topology,
I want Redis (Dapr state + pubsub backing store) and Keycloak (OIDC issuer) to live as hand-authored manifests under `deploy/k8s/redis/` and `deploy/k8s/keycloak/` outside the Aspirate composition,
So that intentional MVP deviations from Aspire's defaults (Redis emptyDir + no AUTH; Keycloak randomized admin password from operator-managed Secret) are explicit, reviewable, and survive every `publish.ps1` regeneration.

**Acceptance Criteria:**

**Given** the configuration-source taxonomy in `docs/kubernetes-deployment-architecture.md` §7 Source 3
**When** the `deploy/k8s/` tree is inspected after a fresh `publish.ps1` run
**Then** `deploy/k8s/redis/` and `deploy/k8s/keycloak/` exist as hand-authored folders containing their own `deployment.yaml` + `service.yaml` (+ `configmap.yaml` for Keycloak)
**And** these folders are referenced from `deploy/k8s/kustomization.yaml` alongside the aspirate-emitted per-service folders
**And** neither folder is present in or generated by `src/Hexalith.Parties.AppHost/Program.cs` (the Aspire AppHost composes the 7 application services only — see Story 9.2).

**Given** the MVP scope explicitly defers production-grade storage (doc §12 Boundaries)
**When** the `redis` Deployment manifest is inspected
**Then** the container image is `redis:<vendor-version>` from a public registry (no `imagePullSecrets`, no Zot reference)
**And** persistence uses `emptyDir: {}` (no PersistentVolumeClaim, no StatefulSet)
**And** authentication is disabled (no `--requirepass`, no Dapr `redisPassword` field referenced from the matching Component — see Story 9.4)
**And** the Deployment exposes port `6379` via a `ClusterIP` Service named `redis`
**And** the Deployment carries an inline comment block (5–10 lines) explicitly stating: "MVP scope — emptyDir, no AUTH. State does not survive Pod restart. Production hardening (PVC + StatefulSet + replication + AUTH) is tracked in doc §12 Boundaries and is out of scope here."

**Given** Keycloak is a vendor image with operator-managed admin credentials
**When** the `keycloak` Deployment manifest is inspected
**Then** the container image is `quay.io/keycloak/keycloak:<vendor-version>` from a public registry (no `imagePullSecrets`, no Zot reference)
**And** the admin user (`KEYCLOAK_ADMIN`) and admin password (`KEYCLOAK_ADMIN_PASSWORD`) are sourced from environment variables that reference Secret `hexalith-keycloak-admin` via `secretKeyRef`
**And** the Secret `hexalith-keycloak-admin` is operator-bootstrapped (Story 9.5 — `publish.ps1` generates 24 random bytes on first publish, idempotent thereafter); the manifest itself does NOT contain a literal password
**And** the Deployment carries an inline comment block stating: "Admin password is bootstrapped by publish.ps1 from a 24-byte random Secret. Never echo, never commit. To rotate, delete the Secret and re-run publish.ps1."

**Given** Keycloak realm configuration must be explicit
**When** the `keycloak` ConfigMap is inspected
**Then** the realm import (if any), client definitions, and start mode (`start-dev` for MVP, `start` for production — MVP uses `start-dev` per current scope) are declared in the ConfigMap or via container args
**And** the OIDC issuer URL pattern is documented (`http://keycloak.hexalith-parties.svc.cluster.local:8080/realms/<realm>`) in the manifest's header comment for downstream service config reference.

**Given** the carve-outs must survive every regeneration
**When** `publish.ps1` step 2 (clean) runs against `deploy/k8s/`
**Then** the clean operation explicitly preserves `deploy/k8s/redis/`, `deploy/k8s/keycloak/`, `deploy/k8s/kustomization.yaml`, `deploy/k8s/namespace.yaml`, `deploy/k8s/README.md`, `deploy/k8s/publish.ps1`, `deploy/k8s/teardown.ps1`
**And** all other files/folders under `deploy/k8s/` are removed before the aspirate regeneration runs
**And** running `publish.ps1` twice in succession produces zero diff in the carve-out folders (byte-identical contract, no MinVer-tag exception applies to vendor images).

**Given** the namespace separation policy
**When** Redis and Keycloak Deployments are inspected
**Then** both run in the `hexalith-parties` namespace alongside Hexalith services (not in a separate infrastructure namespace, to keep the MVP topology a single-namespace deploy per doc §3)
**And** the post-aspirate `imagePullSecrets` patch (Story 9.2 AC5 + Story 9.5) explicitly skips these folders
**And** the post-aspirate Dapr-annotation patch (Story 9.2 AC4) explicitly skips these folders (Redis and Keycloak do NOT carry a daprd sidecar — they are stateful backing services and an OIDC issuer, not Dapr-equipped applications).

**Given** a fitness test guards the carve-out boundary
**When** a `CarveOutPreservationFitnessTest` runs (delivered as part of Story 9.7)
**Then** it asserts that after a simulated `publish.ps1` cycle: (a) `redis/deployment.yaml` byte-matches the committed version; (b) `keycloak/deployment.yaml` byte-matches the committed version; (c) neither folder contains aspirate-generated artifacts (e.g., `aspirate-readme.md`); (d) no `imagePullSecrets` block exists in either Deployment; (e) no Dapr annotations exist in either Deployment.

### Story 9.4: Dapr Control Plane, Components, Access Control & Subscriptions

**Phase:** MVP
**Coverage type:** planned
**Requirements covered:** FR31a; supports FR31, FR40, FR41, FR60, FR61, NFR30

As a developer relying on Dapr for state, pub/sub, and service invocation across the 5 daprd-equipped Hexalith services,
I want the Dapr control plane installed cluster-wide and the project-specific Components, Access Control configurations, and declarative Subscriptions hand-authored under `deploy/dapr/` (outside the Aspirate composition),
So that the security-sensitive parts of the Dapr surface (deny-by-default ACLs, who-can-call-whom topology) are explicit, reviewable, and not at the mercy of Aspirate's emission defaults.

**Acceptance Criteria:**

**Given** Dapr is a cluster-wide capability shared with other projects on the same cluster
**When** `publish.ps1` step 9 runs
**Then** the script invokes `dapr init -k` against the active kubectl context (unless the operator passes `-SkipDaprInit`)
**And** the Dapr control plane lands in namespace `dapr-system` (never inside `hexalith-parties`)
**And** the pinned Dapr version (`1.14.4` per doc §13) is documented; mismatch with the installed version emits a warning but does not block (cluster-wide install may be older/newer than the project's pinned baseline).

**Given** the configuration-source taxonomy in doc §7 Source 2
**When** `deploy/dapr/` is inspected
**Then** the folder contains EXACTLY the hand-authored Dapr CRs the project requires, with no Aspirate-emitted Dapr files
**And** the file set is: `statestore.yaml`, `pubsub.yaml`, `resiliency.yaml`, `accesscontrol.yaml`, `accesscontrol-eventstore-admin.yaml`, `accesscontrol-parties.yaml`, `accesscontrol-tenants.yaml`, `accesscontrol-memories.yaml`, `subscription-parties.yaml`, `subscription-tenants.yaml`
**And** alternative-backend templates (Kafka/RabbitMQ pubsub, Cosmos/Postgres state) are NOT in `deploy/dapr/` proper — they live in a sibling `deploy/dapr-alternatives/` folder and are explicitly skipped by `publish.ps1` step 12 (apply phase).

**Given** the state store and pub/sub Components target Redis
**When** `statestore.yaml` and `pubsub.yaml` are inspected
**Then** both reference `host: redis:6379` (cluster-internal DNS, matches the Service from Story 9.3)
**And** neither references a `redisPassword` Secret (Redis MVP has no AUTH per Story 9.3)
**And** `pubsub.yaml` uses the Redis Streams driver (`pubsub.redis`) suitable for ordered topic delivery
**And** `statestore.yaml` enables actor state by default (`actorStateStore: "true"` metadata).

**Given** the per-service access-control configurations enforce who-can-call-whom
**When** the 5 access-control YAML files are inspected
**Then** each carries `defaultAction: deny` (deny-by-default contract — no wildcard app-id, no wildcard operation path)
**And** the allowed call paths match the topology in doc §4.2 and §9: `parties` may call `tenants` + `eventstore` + `memories`; `tenants` may NOT call `parties` (asymmetric); `eventstore-admin` may call `eventstore`; `memories` may receive but not initiate cross-service calls; `parties-mcp` (non-Dapr) does not appear as an allowed caller in any config
**And** every allowed entry enumerates explicit operations (not wildcards) — e.g., `POST/v1.0/invoke/parties/method/api/parties/v1/parties`.

**Given** declarative subscriptions wire Redis Streams topics to consumer endpoints
**When** `subscription-parties.yaml` and `subscription-tenants.yaml` are inspected
**Then** `subscription-parties.yaml` (`parties-events-sample-tenant`) subscribes the sample consumer to `party.*` topics on the `pubsub` Component
**And** `subscription-tenants.yaml` (`hexalith-parties-tenants-events-parties`) subscribes `parties` to tenant-lifecycle topics on the `pubsub` Component
**And** both subscriptions specify retry/dead-letter behavior consistent with `resiliency.yaml` defaults.

**Given** resiliency policy must be valid before any consumer applies
**When** `publish.ps1` step 10 runs
**Then** the script executes `kubectl apply -f deploy/dapr/resiliency.yaml --dry-run=server` against the active context
**And** a server-side dry-run failure exits with code 1 and a bounded error referencing the offending CR
**And** stdout never contains the full CR body on failure (only the validation-error summary).

**Given** every Dapr-equipped Pod must reference its access-control config
**When** the post-aspirate Dapr-annotation patch runs (Story 9.2 AC4 + Story 9.5 step 5)
**Then** `eventstore` references config `accesscontrol`; `eventstore-admin` references `accesscontrol-eventstore-admin`; `parties` references `accesscontrol-parties`; `tenants` references `accesscontrol-tenants`; `memories` references `accesscontrol-memories`
**And** `parties-mcp` and `eventstore-admin-ui` carry NO `dapr.io/*` annotations (they are not Dapr-equipped).

**Given** the apply phase orders Components → Configurations → Subscriptions
**When** `publish.ps1` step 12 runs `kubectl apply -f` over `deploy/dapr/*.yaml`
**Then** Components are applied first, Configurations second, Subscriptions third (Dapr CR ordering: subscriptions depend on Components existing)
**And** alternative-backend templates in `deploy/dapr-alternatives/` are not selected
**And** the apply is idempotent — re-running on already-applied CRs produces no resource changes (`unchanged` status from kubectl).

### Story 9.5: Operator Scripts (publish.ps1 + teardown.ps1)

**Phase:** MVP
**Coverage type:** planned
**Requirements covered:** FR31a; supports FR31, FR60, FR61, NFR30

As an operator deploying or tearing down Hexalith.Parties,
I want two PowerShell scripts — `deploy/k8s/publish.ps1` (13-phase build+push+apply pipeline) and `deploy/k8s/teardown.ps1` (7-phase removal + residual-state probe) — that share the `-ConfirmContext` gate and a common helper module `_lib/Confirm-KubeContext.ps1`,
So that one command takes me from clean checkout to 9 Ready pods, another command unwinds everything, and both scripts apply the same context-confirmation, credential-safety, and bounded-stdout discipline.

**Acceptance Criteria (publish.ps1 — 13-phase pipeline):**

**Given** the 13-phase pipeline declared in `docs/kubernetes-deployment-architecture.md` §8
**When** `publish.ps1 -ConfirmContext <name>` is invoked
**Then** the script executes the following phases in order, each phase bounded with a clear stdout boundary marker and a specific non-zero exit code on failure:
  - Step 0: `-ConfirmContext` gate (exit 2 on mismatch)
  - Step 1: MinVer resolution via `dotnet msbuild -getProperty:Version` (exit 5 on empty / non-SemVer; warn-and-proceed on `+dirty`)
  - Step 2: Clean `deploy/k8s/` preserving the whitelist from Story 9.3 (`redis/`, `keycloak/`, `kustomization.yaml`, `namespace.yaml`, `README.md`, `publish.ps1`, `teardown.ps1`)
  - Step 3: `dotnet aspirate generate --container-image-tag <minver> --container-registry registry.hexalith.com` (NO `--skip-build`) — builds + pushes 7 images to Zot; propagates aspirate exit code on failure
  - Step 4: Strip aspirate placeholder files (whitelist defined alongside Story 9.2 AC3)
  - Step 5: Patch Dapr annotations on the 5 Dapr-equipped Deployments (`app-id`, `app-port`, `config` per Story 9.4)
  - Step 6: Patch JWT `secretKeyRef` (`hexalith-jwt-signing`) into the 5 daprd-equipped Deployments
  - Step 7: Inject `imagePullSecrets: [{name: zot-pull-secret}]` into every Deployment whose image starts with `registry.hexalith.com/` (vendor carve-outs skipped per Story 9.3)
  - Step 8: Verify all expected per-service folders were emitted (exit 4 on any missing folder; expected set: `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`, `memories`)
  - Step 9: `dapr init -k` unless `-SkipDaprInit` is passed (exit 3 if dapr CLI is missing)
  - Step 10: Ensure namespace `hexalith-parties` exists + server-side dry-run of `deploy/dapr/resiliency.yaml` (exit 1 on dry-run failure)
  - Step 11: Bootstrap operator-managed Secrets (`hexalith-jwt-signing`, `hexalith-keycloak-admin`, `zot-pull-secret`), all idempotent (exit 6 if `~/.docker/config.json` `auths["registry.hexalith.com"]` is missing or `credsStore`/`credHelpers` is configured)
  - Step 12: Apply Dapr CRs from `deploy/dapr/` in Component → Configuration → Subscription order, skipping `deploy/dapr-alternatives/`
  - Step 13: `kubectl apply -k deploy/k8s/`
**And** the script prints a final summary line `[publish] OK: <minver> applied to <context> in <duration>` and exits 0 on success.

**Given** the MinVer resolution policy
**When** publish step 1 runs
**Then** the resolved version must match the regex `^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?(?:\+[A-Za-z0-9.-]+)?$` (allows `+dirty` build metadata)
**And** an empty or malformed result exits 5 with a bounded error message
**And** a `+dirty` resolution emits a single warning line and proceeds
**And** the resolved version is stripped of the `v` prefix if present (defensive — MinVer should not emit it but the script asserts).

**Given** the Zot pull-secret bootstrap (ADR D-K8s-2 Path B)
**When** publish step 11 creates or updates `zot-pull-secret` (`kubernetes.io/dockerconfigjson`)
**Then** the script re-emits the `auths["registry.hexalith.com"]` block from `~/.docker/config.json` wholesale (never decoded, never echoed, never written to disk outside the Secret payload)
**And** a missing entry, malformed JSON, or any `credsStore`/`credHelpers` directive exits 6 with an actionable error referencing `docker login -u parties-publisher registry.hexalith.com` and `$env:DOCKER_CONFIG`
**And** the operator's password / token never appears in stdout, stderr, the rendered manifest, or any log surface.

**Given** the JWT signing Secret and Keycloak admin Secret bootstraps
**When** publish step 11 runs for `hexalith-jwt-signing` and `hexalith-keycloak-admin`
**Then** on first publish the script generates 32 random bytes (jwt) / 24 random bytes (keycloak) and creates the respective `Opaque` Secret
**And** on subsequent publishes the script detects the existing Secret and does NOT regenerate (idempotent: re-publish must not invalidate a running cluster)
**And** the generated values never appear in stdout, stderr, manifest YAML, or any log; only `[publish] Secret hexalith-jwt-signing: <created|exists>` status lines are emitted.

**Given** the cross-patch idempotency contract
**When** `publish.ps1` runs twice in succession on the same commit + same workstation
**Then** the second run produces zero diff in `deploy/k8s/<service>/deployment.yaml` for all 3 patches (Dapr annotations + JWT secretKeyRef + imagePullSecrets) — i.e., patch anchors are detected and re-application is a no-op
**And** the second run produces zero cluster-state diff (`kubectl apply -k` returns all resources as `unchanged`).

**Acceptance Criteria (teardown.ps1 — 7-phase removal):**

**Given** the documented removal scope from doc §10
**When** `teardown.ps1 -ConfirmContext <name>` runs without optional switches
**Then** the script asserts `-ConfirmContext` matches (shared gate with publish.ps1; exit 2 on mismatch)
**And** the script proceeds only if the namespace `hexalith-parties` exists; if it does not, the script logs `[teardown] namespace hexalith-parties not present — nothing to delete` and exits 0
**And** the script removes — in this order — `kubectl delete -k deploy/k8s/` (all 9 workloads), `kubectl delete -f deploy/dapr/` (all Components, Configurations, Subscriptions, Resiliency CRs), and the 3 operator-managed Secrets (`hexalith-jwt-signing`, `hexalith-keycloak-admin`, `zot-pull-secret`)
**And** each removal block is bounded with a stdout marker and a `--ignore-not-found=true` flag (idempotent — re-running on an already-torn-down cluster exits 0).

**Given** the `-PurgeNamespace` optional switch
**When** the operator passes `-PurgeNamespace`
**Then** the script additionally deletes the `hexalith-parties` namespace itself
**And** the deletion uses `--wait=true` so the script returns only after the namespace is fully gone
**And** without `-PurgeNamespace`, the namespace remains (empty) for fast re-deploy.

**Given** the `-PurgeDapr` optional switch
**When** the operator passes `-PurgeDapr`
**Then** the script invokes `dapr uninstall -k --all` to remove the cluster-wide Dapr control plane
**And** without `-PurgeDapr`, the Dapr control plane is left untouched (it is cluster-wide and may be shared with other projects per doc §10).

**Given** the residual-state probe contract (doc §10 final paragraph)
**When** the teardown completes (without `-PurgeNamespace`)
**Then** the script probes the `hexalith-parties` namespace for any owned resource (Deployment, Service, ConfigMap, Secret, Dapr Component/Configuration/Subscription/Resiliency)
**And** if any owned resource remains, the script exits 7 with a bounded summary listing the offending resource kinds + names (counted, not enumerated if > 5 of any kind) and the message: "Residual state detected — manual intervention required before next publish"
**And** if no owned resource remains, the script prints `[teardown] OK: namespace hexalith-parties clean` and exits 0.

**Acceptance Criteria (shared contract):**

**Given** the two scripts share a context-resolution helper
**When** the `deploy/k8s/_lib/` folder is inspected
**Then** it contains `Confirm-KubeContext.ps1` consumed by BOTH `publish.ps1` and `teardown.ps1`
**And** changes to the gate logic (exit-code mapping, mismatch message format, single-echo-at-start discipline) touch only this one file
**And** the helper exports a single entry-point function `Assert-KubeContext -Expected <name>` returning `$null` on match and exiting the parent script on mismatch.

**Given** the credential-safety contract (shared between publish + teardown + helper)
**When** the full stdout/stderr capture of either script is inspected
**Then** none of the following appear: a Base64-decoded password fragment, the literal `Password:`, a JWT-shaped token (`eyJ` followed by Base64), the contents of `~/.docker/config.json`, the value of any operator-managed Secret, the cluster URL, the cluster certificate authority, or any bearer token
**And** Secret operations are reported by name only (`[publish] Secret hexalith-jwt-signing: created`, `[teardown] Secret hexalith-jwt-signing: deleted`).

**Given** the duration budget from doc §2
**When** `publish.ps1` runs against a 9-pod target on a developer-class machine
**Then** the script completes in 5–10 minutes (cold cache; warm cache faster) and the 9 pods reach Ready within 2 minutes of step 13
**And** the script emits per-step elapsed-time markers for diagnostics but does not enforce a hard timeout.

### Story 9.6: validate-deployment.ps1 Lint Tooling

**Phase:** MVP
**Coverage type:** planned
**Requirements covered:** FR61; supports FR31, FR31a, FR39, FR40, FR41

As an operator preparing a deployment for review,
I want `deploy/validate-deployment.ps1` to lint the committed `deploy/dapr/` + `deploy/k8s/` tree (or a generated tree at a candidate path) and report blocking violations across 8 well-defined categories,
So that unsafe or drifted artefacts are caught before they reach a cluster — and the lint output is itself safe to attach to a PR or CI log.

**Acceptance Criteria:**

**Given** the invocation contract
**When** the operator runs `pwsh deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/`
**Then** the script accepts both committed-tree and post-`publish.ps1` candidate-tree paths
**And** the script does not require a kubectl context (pure static lint — no cluster reachability)
**And** exit codes: 0 = pass, 1 = fail (at least one BLOCKING violation), 2 = invalid arguments, 3 = config-path or k8s-path not found.

**Given** the 8 lint categories
**When** the script runs against a target tree
**Then** the following categories are evaluated, each emitting one or more findings with severity `BLOCKING` (fail-the-build):
  - `K8sWorkload-MissingImagePullSecret`: a Deployment with `image: registry.hexalith.com/*` lacks `spec.template.spec.imagePullSecrets[*].name == "zot-pull-secret"`
  - `K8sWorkload-MissingDaprAnnotations`: a Deployment under a Dapr-equipped app-id (`eventstore`, `eventstore-admin`, `parties`, `tenants`, `memories`) lacks `dapr.io/enabled`, `dapr.io/app-id`, `dapr.io/app-port`, or `dapr.io/config`
  - `K8sWorkload-MissingProbes`: a Deployment lacks `readinessProbe` or `livenessProbe` on its primary container
  - `K8sWorkload-NonSemVerTag`: a Deployment image tag does not match `^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$` (rejects `:latest`, empty, malformed)
  - `K8sWorkload-DirtyTagOnConsumerImage`: a Deployment image tag contains `+dirty` build metadata (rejected for any tag destined to ship)
  - `DaprACL-WildcardAppId`: an access-control configuration contains an allowed-caller entry with `appId: "*"` or empty
  - `DaprACL-WildcardOperation`: an access-control configuration contains an `operation: "*"` or path-with-trailing-`*` entry
  - `Secret-Plaintext`: any Deployment / ConfigMap / Dapr Component contains a `value` field with a high-entropy string matching a credential-shape regex (Base64-decoded token, password-prefixed line, JWT-shaped token)

**Given** the output discipline
**When** findings are reported
**Then** each finding line is bounded (≤ 200 chars) and contains: `[<severity>] <category> at <file>:<jsonpath> — <reason>`
**And** the offending VALUE is never reproduced (only the path and the category)
**And** the script supports a `-Format json` flag emitting machine-readable JSON with the same fields (for CI ingestion)
**And** the default human-readable output ends with a summary line `[validate] <N> findings (<B> blocking, <W> warnings) — <PASS|FAIL>`.

**Given** the secret-leak fail-closed contract
**When** the lint inspects suspected credential-shaped strings
**Then** the lint reports the category + path + a SHAPE descriptor (`base64-shaped`, `jwt-shaped`, `password-prefixed`) but never the literal value
**And** the script's own source contains a self-test asserting it never logs a captured suspicious value (regex-based self-check at startup; fail fast if the script is tampered with to leak).

**Given** the alternative-backend templates in `deploy/dapr-alternatives/`
**When** the lint runs against `--config-path deploy/dapr` (NOT `deploy/dapr-alternatives`)
**Then** alternative-backend templates are skipped — they are not part of the deployed CR set and would generate false positives.

**Given** the validate-deployment.ps1 + publish.ps1 + teardown.ps1 share `-ConfirmContext` patterns
**When** `validate-deployment.ps1` is compared with the other two scripts
**Then** validate-deployment.ps1 does NOT carry `-ConfirmContext` (it is context-free static lint)
**And** the helper module `_lib/Confirm-KubeContext.ps1` is reused only by publish.ps1 + teardown.ps1.

**Given** the lint is wired into CI eventually (out-of-scope for MVP, but the contract must support it)
**When** `validate-deployment.ps1 -Format json` is consumed by an external tool
**Then** the JSON output has a stable schema: `{ version: "1", findings: [{ severity, category, file, jsonpath, reason }], summary: { findings, blocking, warnings, status } }`
**And** schema-breaking changes require a version bump on the `version` field.

### Story 9.7: Deployment Fitness Tests + Live-Cluster Integration

**Phase:** MVP
**Coverage type:** planned
**Requirements covered:** FR61; supports FR31, FR31a, FR40, FR41

As a developer evolving the deployment topology,
I want a sealed set of fitness tests in `tests/Hexalith.Parties.DeployValidation.Tests/` that guard the architectural contracts of Stories 9.1–9.6 — and a small set of trait-gated live-cluster integration tests that exercise the `publish.ps1` happy path end-to-end,
So that the contracts (byte-determinism, idempotency, deny-by-default ACL, no credential leak, carve-out preservation, deterministic MinVer emission) are enforced in CI as code, not as policy a reviewer must remember.

**Acceptance Criteria:**

**Given** the deploy-validation test project
**When** the project structure is inspected
**Then** the test project `Hexalith.Parties.DeployValidation.Tests` exists with the following test classes:
  - `K8sManifestGenerationTests` — guards the byte-determinism contract per commit (non-image lines) and the per-service-folder emission contract from Story 9.2
  - `K8sManifestPublishTests` — guards the publish.ps1 pipeline contracts: MinVer regex emission on every `registry.hexalith.com/*` image, imagePullSecrets presence on every consumer Deployment, dapr-annotation patch shape, JWT secretKeyRef shape, cross-patch idempotency on second invocation
  - `DaprAccessControlFitnessTests` — asserts deny-by-default in all 5 ACL configs, asserts no wildcard appId, asserts no wildcard operation path, asserts the topology call-allowlist matches the documented map (parties→tenants OK, tenants→parties forbidden)
  - `DaprSubscriptionFitnessTests` — asserts the 2 declarative subscriptions wire the documented topics to the documented consumer endpoints, with retry/dead-letter config consistent with `resiliency.yaml`
  - `CarveOutPreservationFitnessTest` — asserts a simulated `publish.ps1` cycle does not modify `redis/deployment.yaml` or `keycloak/deployment.yaml`; asserts no `imagePullSecrets` block and no Dapr annotations land in carve-outs
  - `DocumentationFitnessTest` — asserts no `:latest` references in `registry.hexalith.com/*` snippets across entry-point docs; asserts no stale references to `regen.ps1`, `deploy-local.ps1`, `teardown-local.ps1`, `kind-*`, `minikube`, the old local-cluster regex allowlist; asserts every entry-point doc links to `docs/kubernetes-deployment-architecture.md`
  - `ValidateDeploymentLintFitnessTests` — exercises `validate-deployment.ps1` against curated fixture trees (valid baseline, each of the 8 lint categories triggered, JSON-format output schema stability)
  - `CredentialLeakPoisonSweepTest` — runs `publish.ps1` against a mocked / sandboxed target, captures full stdout/stderr, asserts none of the documented leak shapes (base64-shaped, jwt-shaped, password-prefixed, `~/.docker/config.json` body) appear in the capture.

**Given** the unit-vs-integration boundary
**When** the test classes are inspected
**Then** all fitness tests above run as pure C# tests with NO cluster reachability requirement (they read committed YAML, invoke scripts in `-DryRun` mode, or operate on fixture trees)
**And** the test classes carry NO `xunit.skip` / `Trait("Category","Integration")` markers (they run in the default test pass).

**Given** the trait-gated live-cluster integration tests (separate from fitness)
**When** the integration suite is invoked with `dotnet test --filter Trait=LiveCluster`
**Then** the suite runs at most a small set of end-to-end probes against a live cluster the operator opted into:
  - `LiveCluster_PublishHappyPath`: runs `publish.ps1 -ConfirmContext <test-context>` against a sandbox namespace, asserts 9 pods reach Ready within the doc §13 budget
  - `LiveCluster_TeardownClean`: runs `teardown.ps1 -ConfirmContext <test-context>`, asserts the residual-state probe exits 0
  - `LiveCluster_IdempotentRepublish`: runs `publish.ps1` twice in succession, asserts the second run produces zero kubectl-detected resource changes
**And** the LiveCluster suite is NEVER run in the default test pass (requires explicit opt-in via the trait filter and a `KUBECONFIG_TEST_PATH` env var pointing to a sandbox kubeconfig).

**Given** the fitness-test boundary with existing test projects
**When** the test set is compared with `tests/Hexalith.Parties.Architecture.Tests` and other architectural fitness tests in the repo
**Then** deploy-validation fitness tests are scoped to the deployment surface (YAML, scripts, docs) and do NOT cross into application architecture (REST/MCP exposure guards, contract dependency rules, projection isolation — those live in the Architecture.Tests project)
**And** no test in either project asserts the same contract twice.

**Given** the test fixtures must be hand-curated, not auto-generated
**When** fixture trees are inspected under `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/`
**Then** fixtures cover: a baseline valid tree, a tree per lint category from Story 9.6 (8 negative cases), a tree exercising the carve-out boundary, a tree exercising the byte-determinism re-run, a fixture for each MinVer edge case (clean release, preview, dirty)
**And** every fixture is < 50 KB and is reviewable as a unit (no opaque binaries; only YAML/JSON/text).

**Given** test output discipline
**When** any deploy-validation test fails
**Then** the failure message includes the offending file path, the offending JSON-path/line, and the category — but NEVER the offending value if it is credential-shaped (the same poison-sweep rule from Story 9.6 applies to test diagnostics).

---

**End of Sprint Change Proposal.**
