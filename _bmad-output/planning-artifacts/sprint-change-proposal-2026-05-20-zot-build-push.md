# Sprint Change Proposal — 2026-05-20 — Zot Registry Build+Push Pipeline

**Project:** Hexalith.Parties
**Author:** Jérôme (with Correct-Course workflow)
**Date:** 2026-05-20
**Scope classification:** **Moderate** (new MVP story in Epic 9, deletion of two scripts, replacement script, PRD requirement clarification, ADR addition)

---

## 1. Issue Summary

Story 9.1 / Epic 9 closed in mid-May 2026 with `regen.ps1` passing `--skip-build` to `aspirate generate` and the generated manifests referencing `registry.hexalith.com/<app>:latest` or `:staging-latest`. The framing assumed `registry.hexalith.com` was a placeholder and that operators would either (a) load images into a `kind` / `k3d` node out-of-band or (b) point at their own registry via `-ContainerRegistry`.

**This framing is incorrect for the actual platform.** `registry.hexalith.com` is a **real Zot registry** running on the Kubernetes platform itself:

- Namespace: `zot`, NodePort `30500` (internal `zot.zot.svc.cluster.local:5000`).
- External: nginx Ingress `zot-ingress` terminating HTTPS on `registry.hexalith.com`.
- Auth: htpasswd at `/etc/zot/auth/htpasswd`, mounted from Secret `zot-auth-secret`.
- Zot `accessControl.groups`:
  - `admins`: users `jpiquot`, `qdassivignon` (push + admin).
  - `builders`: users `kaniko`, `github-ci` (CI push accounts).
- Deploy target cluster context: `kubernetes-admin@cluster.local` (not a local-cluster context per the existing Story 9.1 allowlist `^kind-.*$ | ^k3d-.*$ | ^minikube$ | ^docker-desktop$`).

The Epic 9 deployment workflow as-shipped does not build or push images — it only generates manifests that reference images that nobody actually publishes from the project. The pipeline needs to be replaced with an integrated MinVer-tagged build → Zot push → manifest emission → cluster apply workflow.

**Discovery:** Direct stakeholder confirmation (Jérôme, 2026-05-20, this workflow run). Verified live via `kubectl` against the cluster.

---

## 2. Impact Analysis

### 2.1 Epic Impact

Epic 9's prior stories are **not** invalidated. They delivered architectural decisions and structural artifacts that the new story inherits. Only the two `regen.ps1` / `deploy-local.ps1` scripts get replaced.

| Epic / Story | Status after this proposal | Note |
|---|---|---|
| **Epic 9 — Kubernetes Deployment** | Active, gains Story 9.5 | Story 9.1 AC1 byte-determinism contract is **superseded** by Story 9.5 (addendum to epics.md). |
| Story 9.1 (done) | Stays Done | Architectural decisions preserved (aspirate as generator, `deploy/k8s/<app-id>/` layout, post-aspirate dapr annotation patch, `deploy/dapr/` as authoritative CR source, `$PreservedNames` carve-out mechanism, Hexalith CRD skip). Only the `regen.ps1` + `deploy-local.ps1` scripts are replaced. |
| Story 9.2 (done) | Stays Done | The static K8s manifest linter (`validate-deployment.ps1 -K8sPath`) is orthogonal to build/push. Story 9.5 reuses it as-is. |
| Story 9.3 (done) | Stays Done | `memories/` in-cluster composition, `keycloak/` + `redis/` hand-authored carve-outs, `hexalith-jwt-signing` and `hexalith-keycloak-admin` Secret bootstrap, JWT `secretKeyRef` patch (Story 9.3 AC4), Resiliency CRD drift check (Story 9.3 AC6), and the 3 new lint categories all preserved and inherited by `publish.ps1`. |
| Story 9.4 (backlog) | Unchanged backlog | FrontComposer deployable host carve-out is **completely orthogonal** to image build/push. Without 9.4 the admin portal / picker / shell are not in the topology regardless of registry strategy. |
| **Story 9.5 (new)** | Backlog, MVP | New story — Zot Registry Build+Push Pipeline & Script Consolidation. See §4. |
| Epics 1–8, Epic 10+ | No impact | None. |

### 2.2 Artifact Conflicts

| Artifact | Conflict | Resolution |
|---|---|---|
| `prd.md` (FR31a) | FR31a names aspirate as generator but is silent on container image publication | Clarify FR31a: "manifests AND container images for the Parties topology can be generated and published from the Aspire AppHost via aspirate, with images pushed to the project's authoritative OCI registry". |
| `prd.md` (NFR30) | < 15 min target — assumed build was out-of-band | Unchanged. Build+push fits the budget; revisit if measurement shows otherwise. |
| `architecture.md` (ADR D-K8s) | Documents aspirate + local-cluster framing only | Insert new ADR `D-K8s-2 — Zot Registry as Image Substrate` after `D-K8s`. |
| `epics.md` (Epic 9 section) | No Story 9.5 | Add Story 9.5 block after Story 9.4 (see §4.1). Add a one-line "Story 9.1 AC1 superseded by Story 9.5" addendum under Story 9.1. |
| `deploy/k8s/regen.ps1` | Passes `--skip-build`; emits stale `:latest` / `:staging-latest` references | **Delete.** Replaced by `deploy/k8s/publish.ps1`. |
| `deploy/k8s/deploy-local.ps1` | Local-cluster allowlist incompatible with real `kubernetes-admin@cluster.local` cluster; no build/push step | **Delete.** Replaced by `deploy/k8s/publish.ps1`. |
| `deploy/k8s/teardown-local.ps1` | Same allowlist mismatch (destructive op must still be operator-gated, but on a real cluster) | **Keep contents**, **rename** to `deploy/k8s/teardown.ps1`, and replace the regex allowlist with a mandatory `-ConfirmContext <name>` argument matching `kubectl config current-context`. |
| `deploy/k8s/README.md` | Documents the deleted scripts and the `--skip-build` rationale | Rewrite "Regeneration", "Deploying to a local cluster", and "Known aspirate limitations" sections to match Story 9.5. Move the "no MVP image build/push" caveat out of "Out of MVP scope". |
| `docs/deployment-guide.md` | "Prerequisites" / "K8s manifest validation (Story 9.2)" sections do not mention Zot credentials | Add Zot credential prerequisite subsection (`docker login registry.hexalith.com`). |
| `deploy/validate-deployment.ps1` (Story 9.2 / 9.3 K8s lint) | Three categories (`K8sWorkload-LatestImageTag`, etc.) are warn-only and currently fire on the as-emitted tree | No structural change to the linter. `K8sWorkload-LatestImageTag` clears naturally once tags are MinVer SemVer. Optional Story 9.5 follow-up adds `K8sWorkload-MissingImagePullSecret` (fail). |
| `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestGenerationTests` | Asserts post-aspirate invariants including (if present) byte-identical regen output | Add `K8sManifestPublishTests` (new file) covering: (a) MinVer tag emission, (b) `imagePullSecrets: [name: zot-pull-secret]` presence on every consumer Deployment, (c) post-aspirate dapr + JWT patches still idempotent. **Relax** the byte-identical regen contract on the image-tag line only (stable modulo image tag). |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | No Story 9.5 tracking | Add Epic 9 → Story 9.5 entry with `status: backlog`. Bump `last_updated`. |
| `_bmad-output/implementation-artifacts/9-1-...md` and `9-3-...md` | Reference the deleted scripts | No retroactive edit needed — they remain the immutable as-implemented record. Cross-reference from the Story 9.5 implementation artifact when it lands. |
| Sibling submodules (EventStore, Tenants, Memories, FrontComposer) | None at this layer | Story 9.5 only touches the Parties-owned deploy/k8s/ pipeline. Submodule projects continue to be consumed via `AddProject<>` in the AppHost. |

### 2.3 Technical Impact

- **New operator/CI credential requirement.** Zot htpasswd credentials are now load-bearing for any image-publishing run:
  - Operator path: personal user from the `admins` group (currently `jpiquot`, `qdassivignon`) → `docker login registry.hexalith.com` once on the workstation.
  - CI path: dedicated `builders` account (`kaniko` or `github-ci`) → `ZOT_USERNAME` / `ZOT_PASSWORD` injected from CI secret store before `publish.ps1`.
- **New cluster Secret.** `zot-pull-secret` (type `kubernetes.io/dockerconfigjson`) in namespace `hexalith-parties`, synthesized from the operator's local `~/.docker/config.json` entry for `registry.hexalith.com`. Bootstrapped by `publish.ps1` (idempotent), referenced via `imagePullSecrets` on every `registry.hexalith.com/*`-pulling Deployment.
- **MinVer-resolved tag.** Resolved once per publish invocation via `dotnet msbuild src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj -getProperty:MinVerVersion -nologo -v:q`, then passed to aspirate as the container image tag for all `AddProject<>` resources. Identical commit → identical MinVer version → identical manifest emission (true determinism per commit; the prior "byte-identical regen" was specific to the placeholder-tag world).
- **`imagePullPolicy` policy.** Aspirate default is `IfNotPresent`. Since MinVer tags are immutable per commit, `IfNotPresent` remains safe and avoids registry round-trips on pod restart. ADR `D-K8s-2` records the rationale.
- **No Dockerfile authoring required.** The .NET SDK Container Publish target (`/t:PublishContainer`) is what aspirate invokes when `--skip-build` is removed; no Dockerfile maintenance burden is introduced.
- **Cluster-side discovery.** The script enforces `-ConfirmContext <name>` against `kubectl config current-context` to prevent accidental cross-cluster pushes. No hardcoded allowlist.
- **Out-of-MVP-still.** Automated CI step that runs `publish.ps1` and validates the diff is deferred per sprint-change-proposal-2026-05-18 ("CI step that runs `regen.ps1` and diffs against committed manifests — explicitly deferred"). Multi-registry support (ACR / Harbor / Docker Hub) deferred.

---

## 3. Recommended Approach

**Direct Adjustment + targeted code deletion** — add Story 9.5 to Epic 9 (MVP), implement the new `publish.ps1` script, delete `regen.ps1` and `deploy-local.ps1`, rename `teardown-local.ps1` → `teardown.ps1` with explicit `-ConfirmContext` gate, regenerate the manifests, and update PRD / architecture / README / tests to match. No rollback of Stories 9.1 / 9.2 / 9.3 needed — their structural outputs (per-app folders, post-aspirate dapr annotation patch, JWT secretKeyRef wiring, hand-authored carve-outs, K8s lint) remain valid and are inherited by `publish.ps1`.

**Rationale:**

- **Direct adjustment** is the minimum-disruption path. The aspirate-generated layout, the post-aspirate dapr + JWT patches, the hand-authored Keycloak/Redis carve-outs, and the lint contract are all reusable. Only the "skip-build + local-cluster allowlist" framing breaks.
- **Rollback** would mean reverting Stories 9.1 / 9.2 / 9.3 (~3 weeks of work) and re-implementing the same architectural decisions. Not justified.
- **MVP review** would re-open the K8s deployment scope question already settled by sprint-change-proposal-2026-05-18; not revisited.

**Effort estimate:** Low–Medium (1 focused story, ~2 days for an experienced dev + 0.5 day for doc/test updates).
**Risk:** Low. The breaking concern is operator-facing (need credentials, must run new script name), not runtime. The cluster currently has running pods sourced from `registry.hexalith.com` already — the registry path is proven.
**Timeline impact:** Adds 1 MVP story to Epic 9; no impact on Epics 10+. Does **not** block Story 9.4 (FrontComposer deployable host) — they are independent.

**Alternatives considered:**

- *Drop `--skip-build` in-place in `regen.ps1`* and keep both scripts as-is. Rejected — the local-cluster allowlist still breaks on `kubernetes-admin@cluster.local`, and the operator UX of "two scripts, one builds, one applies" still exists. The user explicitly asked to consolidate.
- *Make `publish.ps1` an optional wrapper around `regen.ps1` + `deploy-local.ps1`.* Rejected — the two scripts both fail to run on the real cluster, so wrapping them does not solve the contract mismatch. Clean replacement is simpler.

---

## 4. Detailed Change Proposals

### 4.1 New Story 9.5 — Zot Registry Build+Push Pipeline & Script Consolidation

```markdown
### Story 9.5: Zot Registry Build+Push Pipeline & Script Consolidation

**Phase:** MVP
**Epic:** 9 — Kubernetes Deployment
**Status:** backlog

**As an** operator deploying Hexalith.Parties to a real Kubernetes cluster backed by the
Zot registry at `registry.hexalith.com`,
**I want** a single one-command pipeline that resolves the MinVer version, builds the
container images for every AppHost-composed service, pushes them to Zot with that tag,
emits the matching Kubernetes manifests, bootstraps the registry pull secret, and applies
the full topology,
**so that** there is no separate manifest-generation / build / push / apply ceremony, no
stale `:latest` references, and no script that refuses to run on the real cluster context.

**Acceptance Criteria**

1. **One-command publish.** `pwsh deploy/k8s/publish.ps1` resolves the MinVer version from
   `src/Hexalith.Parties.AppHost` (via `dotnet msbuild -getProperty:MinVerVersion`), then
   invokes `dotnet aspirate generate` **without** `--skip-build` so aspirate orchestrates
   `dotnet publish /t:PublishContainer` for every `AddProject<>` resource, tagging each
   image as `<minver-version>` and pushing to `registry.hexalith.com` via the operator's
   existing `docker login` credentials.

2. **Zot pull secret bootstrap.** Before the kustomize apply, `publish.ps1` creates or
   updates the `zot-pull-secret` Secret of type `kubernetes.io/dockerconfigjson` in
   namespace `hexalith-parties`, synthesized from the operator's `~/.docker/config.json`
   entry for `registry.hexalith.com`. Idempotent on re-run. Never echoed to logs.

3. **`imagePullSecrets` patched into every consumer Deployment.** Post-aspirate, the
   script patches `imagePullSecrets: [name: zot-pull-secret]` into the pod spec of every
   Deployment whose container image starts with `registry.hexalith.com/`. Idempotent —
   second invocation produces no diff in that field.

4. **MinVer-tagged manifest emission.** Every aspirate-emitted `image:` reference for a
   `registry.hexalith.com/*` repository carries the resolved MinVer tag (e.g.
   `registry.hexalith.com/parties:0.4.2-preview.0.17`), not `:latest` /
   `:staging-latest`. The `K8sWorkload-LatestImageTag` lint warning is cleared on the
   as-committed tree.

5. **Old scripts deleted.** `deploy/k8s/regen.ps1` and `deploy/k8s/deploy-local.ps1` are
   removed from the repo; `deploy/k8s/teardown-local.ps1` is **renamed** to
   `deploy/k8s/teardown.ps1` and its local-cluster regex allowlist is replaced by an
   explicit mandatory `-ConfirmContext <name>` argument matching
   `kubectl config current-context`.

6. **Context confirmation gate (publish-side).** `publish.ps1` requires
   `-ConfirmContext <name>` matching the active `kubectl config current-context`.
   Mismatch → exit 2 with a clear "expected X, got Y" message. No hardcoded allowlist.

7. **Post-aspirate patches preserved.** The existing dapr annotation patch
   (`app-port: 8080`, per-app `dapr.io/config`) from Story 9.1 and the JWT
   `secretKeyRef` patch from Story 9.3 AC4 remain functional and idempotent under the
   new script. Hand-authored carve-outs (`keycloak/`, `redis/`) remain preserved
   (Story 9.3 AC4 / AC5).

8. **DAPR component application unchanged.** `publish.ps1` applies `deploy/dapr/` CRs
   directly (Story 9.1 AC behavior preserved), runs the Story 9.3 AC6 server-side
   dry-run on `resiliency.yaml`, bootstraps `hexalith-jwt-signing` and
   `hexalith-keycloak-admin` Secrets (Story 9.3 AC4), then `kubectl apply -k deploy/k8s/`.
   Bootstrap of `zot-pull-secret` (AC2) joins this Secret bootstrap block.

9. **K8s manifest publish tests.** `tests/Hexalith.Parties.DeployValidation.Tests` gains
   `K8sManifestPublishTests` asserting: (a) every `registry.hexalith.com/*` image
   reference carries a non-`:latest` MinVer-shaped tag; (b) `imagePullSecrets` with name
   `zot-pull-secret` is present on every Deployment whose container image starts with
   `registry.hexalith.com/`; (c) the post-aspirate dapr annotation and JWT secretKeyRef
   patches remain idempotent (no diff on re-run). The byte-identical regen contract
   from Story 9.1 AC1 is relaxed to "stable modulo image tag" only on the image line.

10. **Documentation updated.** `deploy/k8s/README.md` and `docs/deployment-guide.md`
    reflect the new pipeline; Zot credential prerequisite documented
    (`docker login registry.hexalith.com`); the "no MVP image build/push" caveat removed
    from "Out of MVP scope".
```

### 4.2 Epics file addendum (`_bmad-output/planning-artifacts/epics.md`)

Add this one-line addendum directly under the Story 9.1 acceptance criteria block:

```diff
+ **Addendum (2026-05-20, sprint-change-proposal-2026-05-20-zot-build-push):** AC1's
+ "byte-identical regen output" contract is superseded by Story 9.5. With MinVer-derived
+ image tags, regen is "deterministic per commit" rather than "stable across regens at
+ the same commit modulo build receipts". The other Story 9.1 outputs (aspirate as
+ generator, deploy/k8s/<app-id>/ layout, post-aspirate dapr annotation patch,
+ deploy/dapr/ as authoritative CR source, $PreservedNames mechanism, Hexalith CRD
+ skip) remain in force.
```

And append the full Story 9.5 block from §4.1 after the Story 9.4 section.

### 4.3 PRD edits (`_bmad-output/planning-artifacts/prd.md`)

```diff
- FR31a: Kubernetes manifests for the Parties topology can be generated from the Aspire AppHost via aspirate.
+ FR31a: Kubernetes manifests AND container images for the Parties topology can be
+ generated and published from the Aspire AppHost via aspirate, with images pushed to
+ the project's authoritative OCI registry (Zot at `registry.hexalith.com` for the
+ in-MVP platform). Images carry the MinVer-resolved version as their tag and are
+ immutable per commit.
```

NFR30 (< 15 min target) is unchanged. The build+push step is assumed to fit the existing budget; revisit if measurement shows otherwise.

### 4.4 Architecture ADR (`_bmad-output/planning-artifacts/architecture.md`)

Insert new ADR `D-K8s-2` directly after the existing `D-K8s` ADR:

```markdown
### D-K8s-2 — Zot Registry as Image Substrate

**Status:** Accepted (2026-05-20)

**Context:** Story 9.1's `regen.ps1` passed `--skip-build` to aspirate and the emitted
manifests referenced `registry.hexalith.com/<app>:latest` / `:staging-latest`. The
registry name was treated as a placeholder. In fact, `registry.hexalith.com` is a real
Zot registry (project-zot/zot-linux-amd64) deployed on the cluster (namespace `zot`,
NodePort 30500, nginx Ingress, htpasswd auth) and is the project's authoritative OCI
substrate.

**Decision:** Container images for all AppHost-composed services are built via the .NET
SDK Container Publish target (invoked by aspirate when `--skip-build` is not passed),
tagged with the MinVer-resolved version per build, and pushed to the Zot registry at
`registry.hexalith.com`. The cluster pulls via a namespaced `zot-pull-secret` of type
`kubernetes.io/dockerconfigjson` bootstrapped from the operator's local
`~/.docker/config.json` by `publish.ps1`.

**Consequences:**

- Operator must `docker login registry.hexalith.com` before `publish.ps1` (personal
  account in Zot `admins` group).
- CI must inject `ZOT_USERNAME` / `ZOT_PASSWORD` from secrets, using a dedicated
  account in Zot `builders` group (e.g. `github-ci`).
- Image immutability per commit (MinVer tag) → `imagePullPolicy: IfNotPresent` is safe
  and avoids registry round-trips on pod restart.
- Story 9.1 AC1 "byte-identical regen" contract is superseded — image lines now carry
  MinVer-derived tags. Stability becomes "deterministic per commit" rather than
  "stable across regens at the same commit".
- No Dockerfile maintenance burden; SDK container publish covers all current services
  (`eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`,
  `tenants`, `memories`).
- Out-of-MVP: multi-registry support (ACR / Harbor / Docker Hub), image signing
  (cosign / sigstore), SBOM emission.
```

### 4.5 Sprint-status update (`_bmad-output/implementation-artifacts/sprint-status.yaml`)

Add under Epic 9:

```yaml
  - id: 9-5
    title: "Zot Registry Build+Push Pipeline & Script Consolidation"
    status: backlog
    phase: MVP
```

Bump `last_updated` to `2026-05-20`.

### 4.6 File operations summary

| Operation | File |
|---|---|
| **Delete** | `deploy/k8s/regen.ps1` |
| **Delete** | `deploy/k8s/deploy-local.ps1` |
| **Rename** | `deploy/k8s/teardown-local.ps1` → `deploy/k8s/teardown.ps1` (regex allowlist → mandatory `-ConfirmContext`) |
| **Create** | `deploy/k8s/publish.ps1` |
| **Edit** | `deploy/k8s/README.md` (Regeneration / Deploying / Known aspirate limitations / Out-of-scope sections) |
| **Edit** | `docs/deployment-guide.md` (Zot credentials prereq subsection) |
| **Edit** | `_bmad-output/planning-artifacts/prd.md` (FR31a) |
| **Edit** | `_bmad-output/planning-artifacts/architecture.md` (insert `D-K8s-2`) |
| **Edit** | `_bmad-output/planning-artifacts/epics.md` (Story 9.1 addendum + Story 9.5 block) |
| **Edit** | `_bmad-output/implementation-artifacts/sprint-status.yaml` (Story 9.5 entry, bump `last_updated`) |
| **Create** | `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestPublishTests.cs` |
| **Regenerate** | `deploy/k8s/<app-id>/deployment.yaml` × 6 (MinVer tag + `imagePullSecrets` patches) |

---

## 5. Implementation Handoff

**Scope:** Moderate (new MVP story + script replacement + cross-artifact updates + new test class).

**Recipients:**

- **Developer agent (Amelia)** — implements Story 9.5: writes `publish.ps1`, renames + reworks teardown, deletes old scripts, regenerates manifests, writes `K8sManifestPublishTests`, edits README + deployment-guide + PRD + architecture + epics + sprint-status.
- **Product Owner (Jérôme)** — approves PRD FR31a edit and accepts Story 9.5 acceptance criteria.
- **Architect (Winston, optional)** — second pair of eyes on ADR `D-K8s-2`; otherwise self-merge by Developer agent.

**Success criteria:**

1. `pwsh deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local` runs end-to-end on the live cluster: resolve MinVer → build → push → manifest emit → bootstrap Secrets → apply DAPR CRs → kustomize apply → pods Running.
2. `kubectl -n hexalith-parties get deploy -o=jsonpath='{range .items[*]}{.metadata.name}{"="}{.spec.template.spec.containers[*].image}{"\n"}{end}'` returns MinVer-tagged image refs for every `registry.hexalith.com/*` Deployment (no `:latest`, no `:staging-latest`).
3. `kubectl -n hexalith-parties get deploy -o=jsonpath='{.items[*].spec.template.spec.imagePullSecrets[*].name}'` includes `zot-pull-secret` on every consumer Deployment.
4. `pwsh deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s` returns exit 0; `K8sWorkload-LatestImageTag` warning cleared.
5. `K8sManifestPublishTests` passes; existing `K8sManifestGenerationTests` continue to pass (with the relaxed "stable modulo image tag" assertion on the image line).
6. `pwsh deploy/k8s/teardown.ps1 -ConfirmContext kubernetes-admin@cluster.local` cleans the namespace and leaves Zot + DAPR control plane intact.

**Dependencies:** None on other backlog stories. Story 9.4 (FrontComposer deployable host) remains independent — when 9.4 lands, the new FrontComposer host will be picked up automatically by `publish.ps1` via aspirate's `AddProject<>` resolution, with no further change to the publish pipeline.

**Out-of-scope for Story 9.5:**

- Automated CI step that runs `publish.ps1` and validates the diff (deferred per sprint-change-proposal-2026-05-18).
- Image signing (cosign / sigstore) or SBOM emission.
- Multi-registry support.
- Per-image resource limits / probes (still tracked as the Story 9.3 candidate "Aspirate output hardening" gap).

---

**Status:** Awaiting Jérôme's final approval to route to the Developer agent for Story 9.5 implementation.
