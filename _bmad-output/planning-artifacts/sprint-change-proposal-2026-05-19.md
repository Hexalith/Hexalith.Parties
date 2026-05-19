# Sprint Change Proposal — 2026-05-19

**Project:** Hexalith.Parties
**Author:** Claude (with Correct-Course workflow)
**Driver:** Jérôme
**Date:** 2026-05-19
**Scope classification:** Moderate (new story 9.3 within Epic 9 + AppHost code changes + new repo artifacts)
**Branch:** `feat/9-3-k8s-deploy-spec-alignment`

---

## 1. Issue Summary

End-to-end deploy of Hexalith.Parties against a real Kubernetes cluster (`kubernetes-admin@cluster.local`, namespace `hexalith-parties`, 2026-05-18) reached `Running` only after **out-of-repo manual interventions**. The branch `fix/k8s-deploy-workarounds` (merged 2026-05-18 as PR #39 / merge commit `8bae56b2`) landed the **minimum** edits required to make Story 9.1's manifests apply, but explicitly deferred the structural fixes to a future story. That work has not been created. The cluster is therefore in a state that **cannot be reproduced from `main`** alone.

Five concrete gaps between the **Story 9.1 AC1 contract** ("Kubernetes manifests for the full Aspire topology (Parties service + sibling-submodule service projects: EventStore, Tenants, **Memories, FrontComposer**) are emitted into `deploy/k8s/`") and **what aspirate actually emits** are documented below.

### Discovery

Direct inspection by Claude on 2026-05-19 at Jérôme's request: compared live cluster resources (`kubectl -n hexalith-parties get …`) against `deploy/k8s/**` and `src/Hexalith.Parties.AppHost/Program.cs`. Findings confirmed against commit message `c6897d2` (the workaround commit on PR #39) which lists the same items under "OUT OF SCOPE (left for follow-up Story 9-3)".

### Evidence

| # | Gap | Spec contract | Live state | Repo state |
|---|---|---|---|---|
| 1 | **Memories.Server absent from topology** | Story 9.1 AC1 names Memories | No Memories pod; `parties-env` does not contain `Parties__MemoriesSearch__*` (feature OFF) | `Program.cs` lines 90-104 treat Memories as **external HTTP dependency** behind `EnableMemoriesSearch` flag; no `AddProject<Hexalith_Memories_Server>` |
| 2 | **FrontComposer absent from topology** | Story 9.1 AC1 names FrontComposer | No FrontComposer pod | No reference in `Program.cs`. Zero env vars on any service |
| 3 | **Keycloak / JWT auth not emitted to `deploy/k8s/`** | `Program.cs` lines 106-173 wire `Authentication__JwtBearer__{Authority,Issuer,Audience,RequireHttpsMetadata,SigningKey}` on the 6 services when `EnableKeycloak=true` (default) | JWT env vars + Secret `hexalith-jwt-signing` injected via `kubectl set env` during the 2026-05-18 session | Aspirate output drops the Keycloak resource; no `keycloak` Deployment/Service/Realm manifest under `deploy/k8s/` |
| 4 | **Redis absent from repo** | Dapr components `pubsub` + `statestore` point at `redis.hexalith-parties.svc.cluster.local:6379` | `redis` Deployment exists (image `redis:7-alpine`, 1 replica, no PVC, no auth) — created manually on 2026-05-18 | No `deploy/k8s/redis/` directory; no manifest |
| 5 | **`deploy/dapr/resiliency.yaml` not applied** | Story 9.1 AC1: "DAPR component CRs in `deploy/k8s/dapr/` correspond to the templates in `deploy/dapr/` (… resiliency)." | No `Resiliency` CR in namespace | File exists; rejected by DAPR 1.14.4 CRD: unknown fields `spec.policies.timeouts.daprSidecar.general`, `spec.targets.components.statestore.{circuitBreaker,retry,timeout}` |

### Operational consequence

If a fresh operator runs `kubectl delete ns hexalith-parties && deploy-local.ps1` from `main` today, **none of the pods reach Ready**: no Redis means daprd cannot start, so all sidecars crash-loop; even if Redis were provided, the apps would refuse calls without the JWT issuer; even if both were patched, the resilience policies promised by Epic 8 wouldn't be in effect.

### Linked artifacts

- Cluster: `kubectl -n hexalith-parties get deploy,pod,cm,secret,components.dapr.io,configurations.dapr.io,resiliency.dapr.io`
- Commit: `c6897d2 fix(deploy): workarounds to make Epic 9 manifests apply on a real K8s cluster` (merged via PR #39 as `8bae56b2`)
- AppHost: `src/Hexalith.Parties.AppHost/Program.cs` lines 90-104 (Memories), 106-173 (Keycloak)
- Spec: `_bmad-output/planning-artifacts/epics.md` § Epic 9 / Story 9.1 (lines 3113-3170)
- ADR: `_bmad-output/planning-artifacts/architecture.md` § D-K8s (lines 475-488)

---

## 2. Impact Analysis

### Epic impact

| Epic | Impact | Action |
|---|---|---|
| **Epic 9 — Kubernetes Deployment** | In-progress. 9.1 + 9.2 done. The byte-determinism AC1 of 9.1 is **currently violated** by the workarounds in `deploy/k8s/eventstore/` (`regen.ps1` no longer produces zero diff). 9.1 AC1 also remains unmet for Memories + FrontComposer + Keycloak + Redis + Resiliency | **Add Story 9.3** (this proposal). Epic remains in-progress until 9.3 done |
| Epics 1, 2, 4-12 | No story-level impact. Application code unchanged. Feature flags (`EnableMemoriesSearch`, `EnableKeycloak`) still control client-side wiring | None |
| Epic 9 retrospective | Was marked `optional` in sprint-status. Should now be deferred until 9.3 done | Update `sprint-status.yaml` — leave `optional`, do not start retro |

**Note:** there are two "Epic 9" labels in the repo history. The K8s Epic 9 (this proposal) is the one created by `sprint-change-proposal-2026-05-18.md`. The retrospective document `epic-9-retro-2026-05-07.md` belongs to a prior **GDPR Compliance v1.1** epic that has since been renumbered. No ambiguity at the `sprint-status.yaml` level — only one `epic-9` entry there.

### Story impact

| Story | Status | Required change |
|---|---|---|
| **9.1 Generate Kubernetes Artifacts** | done | **No retroactive change.** Closing remediations move to 9.3. Story 9.1's record stays done as historical truth |
| 9.2 Extend Deployment Validation | done | **No change.** The lint rules already cover Story 9.3 outputs (missing probes, missing resources, plaintext secrets, drifted ACLs, non-local-cluster capabilities). 9.3 must keep 9.2's validator green |
| **9.3 (new) — Close the K8s deployment gaps** | new | See § 4 for AC and tasks |

### Artifact conflicts

| Artifact | Conflict / Update | Resolution |
|---|---|---|
| `prd.md` FR31a | Says "Aspire model as single source of truth for the … service graph (EventStore, Tenants, Memories, FrontComposer)". **Memories and FrontComposer are not part of the emitted graph** | **Update FR31a wording** to disambiguate: either (a) tighten to require both as composed resources, or (b) loosen to allow Memories/FrontComposer as configurable external dependencies. **Recommend (a)** — keep the spec, fix the implementation. Story 9.3 enforces (a) |
| `architecture.md` D-K8s | Says "Sibling submodules participate in the generated topology but remain independent codebases — their own deploy stories may follow" — ambiguous whether *participating in topology* means *composed by AppHost* | **Tighten D-K8s consequence** to explicitly state Memories.Server + FrontComposer.* are composed by `Hexalith.Parties.AppHost` (not external HTTP refs) for the MVP local-cluster path |
| `epics.md` Epic 9 | Add Story 9.3 body under existing Epic 9 section (after Story 9.2) | New AC, listed in § 4 |
| `sprint-status.yaml` | No 9-3 entry | Add `9-3-close-k8s-deployment-spec-gaps: backlog` under `epic-9` block; bump `last_updated` |
| `src/Hexalith.Parties.AppHost/Program.cs` | Source of the gap. Five problems: (a) `*\|party\|v1` env-var key chars violate K8s naming, (b) Memories not composed, (c) FrontComposer not composed, (d) Keycloak block emits `AddKeycloak(…)` but aspirate drops it, (e) Redis not composed | Restructured in 9.3 — see § 4 task list |
| `deploy/k8s/` | Generated; `regen.ps1` must produce **zero diff** after 9.3 closes. Currently it would clobber `eventstore/appconfig-cm.yaml` and re-add the `*\|party\|v1` literals | Restored to byte-determinism by AppHost fix (a). New per-service folders generated for `memories-server`, `frontcomposer`, `keycloak`, `redis` (or a single `keycloak-realm` / `redis-stateful` set, depending on chosen pattern) |
| `deploy/dapr/resiliency.yaml` | Schema rejected by DAPR 1.14.4 CRD | Rewrite against the actual CRD (consult `kubectl get crd resiliencies.dapr.io -o yaml`); add `deploy/k8s/` apply test that creates → deletes the CR to catch schema regression |
| `tests/Hexalith.Parties.DeployValidation.Tests` | 9.2 lint rules don't yet check for required Memories/FrontComposer/Keycloak/Redis services nor presence of JWT env vars | Add fitness tests that enforce the topology contract from FR31a (rejects manifest set missing any of the named services) |
| `deploy/validate-deployment.ps1` | Same gap as above | Add corresponding validator rules |
| `docs/deployment-guide.md` | Doesn't document Memories/FrontComposer pods in the topology diagram or the JWT secret-rotation procedure | Update topology diagram + add JWT key bootstrap section once Keycloak is composed |
| `docs/getting-started.md` | First-command walkthrough may need Memories preflight | Verify after 9.3 |

### Technical impact

- **AppHost surface area grows by ~3 projects** (Memories.Server, FrontComposer host, Keycloak resource emission for aspirate). Stays well below complexity threshold for an Aspire AppHost.
- **Submodule contract:** `Hexalith.Memories` and `Hexalith.FrontComposer` submodules expose `*.Server` projects that the AppHost can `AddProject<…>()`. Both have ServiceDefaults and OpenTelemetry plumbing per Story 9.1's existing pattern. **No code changes inside the submodules** are required for the topology emission.
- **Aspirate Keycloak support:** aspirate may need a configuration hint to translate `AddKeycloak()` into a `Deployment + Service + Realm ConfigMap` triple. Verify by running `dotnet aspirate generate` after a minimal AppHost change and inspecting the output. If aspirate doesn't natively support Keycloak, fallback: hand-author a `deploy/k8s/keycloak/` set parallel to `deploy/k8s/redis/`, mark both as **non-generated** in `regen.ps1` (carved out of the AC1 byte-determinism contract on those two subfolders only).
- **Redis as a StatefulSet:** for MVP local-cluster, single-replica `Deployment` with `emptyDir` volume is acceptable (data loss on pod restart matches Redis defaults). Document the operator-class limitation in `deploy/k8s/redis/README.md`. A proper StatefulSet + PVC + auth is post-MVP.
- **JWT secret bootstrap:** for MVP, document a `kubectl create secret generic hexalith-jwt-signing --from-literal=Authentication__JwtBearer__SigningKey=$(…)` step in `deploy-local.ps1`. The AppHost should emit a `SecretKeyRef` envFrom on each service that consumes it (not a literal value). This keeps the secret out of generated YAML.
- **`*|party|v1` registration keys:** must be replaced with array-indexed form (`EventStore__DomainServices__Registrations__0__Key`, `…__0__AppId`, etc.) in `Program.cs` so that aspirate emits valid K8s ConfigMap data keys. EventStore reader code must already support both forms — if not, this needs a parallel change in `Hexalith.EventStore` (sub-task validation).
- **Resiliency CR rewrite:** check live CRD with `kubectl get crd resiliencies.dapr.io -o yaml` to find which schema version is active; rewrite `deploy/dapr/resiliency.yaml` against it. Verify with a one-shot `kubectl apply --dry-run=server` in the deploy validation script.

---

## 3. Recommended Approach

**Selected path: Option 1 — Direct Adjustment (new story within existing Epic 9).**

Rationale:

- Epic 9 is still `in-progress` — adding a closing story is the canonical follow-up pattern. No epic restructure needed.
- The gaps are mechanical fix-ups, not strategic pivots. Story 9.3 has clear acceptance criteria derivable from Story 9.1 AC1.
- The 5 items are independent enough to land in sub-tasks but tight enough to keep in one story (they all touch `Program.cs` + `deploy/`).
- Option 2 (rollback) is non-viable: Story 9.1 + 9.2 produced real value (manifests + lint) and the workaround commit unblocked a live deploy validation. Rolling back loses both.
- Option 3 (PRD MVP reduction) is rejected: the cleanest framing is *the spec is right, the implementation lagged*. Reducing FR31a to exclude Memories/FrontComposer would be a stealth scope cut that contradicts the explicit decision in `sprint-change-proposal-2026-05-18.md`.

**Estimates:**

- **Effort:** Medium (≈ 2-3 dev-days). Largest sub-task is verifying aspirate's Keycloak emission and authoring fallback manifests if needed. Second-largest is the array-indexed registration key conversion (needs EventStore reader verification).
- **Risk:** Low. All work is verifiable on the existing local cluster. Story 9.2's lint suite catches the most likely regressions automatically.
- **Timeline impact on Epic 9:** retrospective deferred ≈ 1 week. No downstream epic depends on Epic 9 completion.

---

## 4. Detailed Change Proposals

### 4.1 New Story 9.3 (insert into `_bmad-output/planning-artifacts/epics.md` after Story 9.2)

```markdown
### Story 9.3: Close Kubernetes Deployment Spec Gaps

**Phase:** MVP
**Coverage type:** implemented
**Requirements covered:** FR31, FR31a; supports NFR30, FR60

As an operator (or developer) deploying Parties from `main` to a local Kubernetes cluster,
I want the aspirate-generated manifests in `deploy/k8s/` to be sufficient — without manual `kubectl set env`, hand-created secrets, or out-of-repo `Deployment` resources — to bring the documented topology to `Ready`,
So that Story 9.1's "one-command flow" promise holds reproducibly from a clean checkout.

**Acceptance Criteria:**

**Given** the developer runs `dotnet aspirate generate` on `main` and applies `deploy/k8s/` to an empty local cluster
**When** the apply completes
**Then** the full topology — Parties, Parties.Mcp, EventStore, EventStore.Admin.Server, EventStore.Admin.UI, Tenants, **Memories.Server**, **FrontComposer.Shell or .Mcp host**, **Keycloak**, **Redis** — reaches `Ready` within the documented cold-start budget
**And** no `kubectl set env`, `kubectl create secret generic`, or `kubectl apply -f <out-of-repo-file>` is required.

**Given** the AppHost has been edited
**When** `regen.ps1` is run twice in succession
**Then** the second run produces zero diff against the working tree (byte-determinism contract from Story 9.1 AC1 restored)
**And** the EventStore registration keys no longer contain `*` or `|` characters (replaced with array-indexed form readable by both Aspire local-run and aspirate-emitted K8s ConfigMaps).

**Given** `deploy/dapr/resiliency.yaml` is applied to the active DAPR control plane
**When** the live CRD `resiliencies.dapr.io` validates the manifest
**Then** the CR is created without `unknown field` errors
**And** the resilience policies promised by Epic 8 (state-store circuit breaker, sidecar timeouts) are reflected in `kubectl -n hexalith-parties describe resiliency <name>`.

**Given** the deployment validation tool runs against the new manifest set
**When** any of Memories.Server, FrontComposer host, Keycloak, Redis, or the `Authentication__JwtBearer__*` env wiring is missing from `deploy/k8s/`
**Then** validation reports a blocking failure (not a warning)
**And** the failure category clearly distinguishes "missing service in topology" from "missing service env var" from "missing component CR".

**Given** JWT signing material is required by the topology
**When** the deploy script runs
**Then** the script either provisions a development-mode signing key into a Kubernetes Secret (with documented rotation procedure) or fails with a clear message naming the missing secret reference
**And** the signing key is never emitted as a literal value in generated YAML.

**Given** the topology is torn down via the documented cleanup command
**When** teardown completes
**Then** all Memories/FrontComposer/Keycloak/Redis resources are removed alongside the Parties resources
**And** no stale Redis data or Keycloak realm state remains that would block clean re-deploy.
```

**Out of scope (explicit, link to future stories):**

- Production-grade Redis (StatefulSet + PVC + AUTH + TLS) — post-MVP cloud story
- Production-grade Keycloak (HA, PostgreSQL backend, external realm export) — post-MVP cloud story
- Managed cloud K8s (AKS/EKS/GKE) — out of MVP per `architecture.md` § D-K8s
- Authoritative `Memories.EventStore` and `Memories.Redis` projects (separate state plane for Memories) — Memories submodule's own deploy story
- AdminPortal / Picker UI deployment — separate Epic (UI delivery)

### 4.2 PRD edit (`_bmad-output/planning-artifacts/prd.md`, FR31a)

```
OLD:
- **FR31a:** Deployment artifacts are generated from the Aspire AppHost via aspirate (aspir8),
  keeping the Aspire model as the single source of truth for the Parties + sibling-submodule
  service graph (EventStore, Tenants, Memories, FrontComposer)

NEW:
- **FR31a:** Deployment artifacts are generated from the Aspire AppHost via aspirate (aspir8),
  keeping the Aspire model as the single source of truth for the Parties + sibling-submodule
  service graph: Parties, Parties.Mcp, EventStore, EventStore.Admin.Server, EventStore.Admin.UI,
  Tenants, Memories.Server, FrontComposer service host. Required infrastructure dependencies
  (Keycloak identity provider, Redis state-store/pubsub backing) are emitted by the same flow.
  External services consumed by Parties (e.g. managed Memories instance) may be supplied via
  configurable HTTP endpoints, but the default MVP path emits all in-cluster
```

Rationale: enumerates the exact services, removes ambiguity that allowed Memories/FrontComposer to slip out of the topology emission. Carves a deliberate escape hatch for "managed external Memories" as a *non-default* path.

### 4.3 Architecture edit (`_bmad-output/planning-artifacts/architecture.md`, D-K8s consequence list)

```
OLD bullet:
- Sibling submodules participate in the generated topology but remain independent
  codebases — their own deploy stories may follow

NEW bullet (replacing the above):
- Sibling-submodule service projects (EventStore.Server, EventStore.Admin.Server.Host,
  EventStore.Admin.UI, Tenants.Server, Memories.Server, FrontComposer service host)
  participate in the generated topology by being composed as `AddProject<…>` resources
  in `Hexalith.Parties.AppHost`. Their codebases remain independent — Parties does not
  edit submodule code. Each submodule may publish its own standalone deploy story for
  cloud or non-Parties contexts; that does not exempt them from the Parties MVP topology
- Infrastructure dependencies (Keycloak identity provider, Redis state-store/pubsub backing)
  are composed by the AppHost as Aspire resources and emitted to `deploy/k8s/` by aspirate.
  Where aspirate cannot translate a resource type natively (verified per resource), a
  hand-authored fallback manifest set is checked into a clearly named subfolder
  (e.g. `deploy/k8s/keycloak-fallback/`) and excluded from the byte-determinism contract
  on that subfolder only; `regen.ps1` documents the carve-out
```

### 4.4 AppHost code changes (`src/Hexalith.Parties.AppHost/Program.cs`)

Five sub-tasks, sequenced in implementation order:

1. **Replace `*|party|v1` env-var keys with array-indexed form** (lines 15-19):
   ```csharp
   // OLD:
   .WithEnvironment("EventStore__DomainServices__Registrations__*|party|v1__AppId", "parties")
   .WithEnvironment("EventStore__DomainServices__Registrations__*|party|v1__MethodName", "process")
   // … 3 more lines

   // NEW:
   .WithEnvironment("EventStore__DomainServices__Registrations__0__Key", "*|party|v1")
   .WithEnvironment("EventStore__DomainServices__Registrations__0__AppId", "parties")
   .WithEnvironment("EventStore__DomainServices__Registrations__0__MethodName", "process")
   .WithEnvironment("EventStore__DomainServices__Registrations__0__TenantId", "*")
   .WithEnvironment("EventStore__DomainServices__Registrations__0__Domain", "party")
   .WithEnvironment("EventStore__DomainServices__Registrations__0__Version", "v1")
   ```
   **Validation gate:** confirm `Hexalith.EventStore` reader supports both shapes. If not, parallel PR in EventStore submodule first.

2. **Compose Memories.Server** (insert new `AddProject<…>` after Tenants):
   ```csharp
   IResourceBuilder<ProjectResource> memoriesServer = builder.AddProject<Projects.Hexalith_Memories_Server>("memories")
       .WithDaprSidecar(sidecar => sidecar
           .WithOptions(new DaprSidecarOptions { AppId = "memories", Config = memoriesAccessControlConfigPath })
           .WithReference(eventStoreResources.StateStore)
           .WithReference(eventStoreResources.PubSub))
       .WaitFor(eventStoreResources.StateStore)
       .WaitFor(eventStoreResources.PubSub);
   ```
   The existing `if (EnableMemoriesSearch) { … }` block (lines 90-104) is retained — when the flag is on, `Parties__MemoriesSearch__Endpoint` now defaults to `http://memories:8080/` (in-cluster service name) instead of `http://localhost:5010/`. Add a new ACL config file `deploy/dapr/accesscontrol.memories.yaml`.

3. **Compose FrontComposer host:**
   The submodule exposes `Hexalith.FrontComposer.Mcp` and `Hexalith.FrontComposer.Shell`. Inspect both `.csproj` files for `<IsPublishable>true</IsPublishable>` (the Parties.Mcp project had this exact issue in the workaround commit). Pick **`FrontComposer.Mcp`** as the MVP service (matches the existing MCP-server pattern). Add:
   ```csharp
   IResourceBuilder<ProjectResource> frontComposer = builder.AddProject<Projects.Hexalith_FrontComposer_Mcp>("frontcomposer")
       .WithReference(eventStore)
       .WithReference(parties);
   ```

4. **Make Keycloak emission survive aspirate.**
   Verify by running `dotnet aspirate generate` against the current code and inspecting whether `keycloak` appears in `deploy/k8s/`. **If absent**, two options:
   - **(a)** Pin aspirate to a version that supports `AddKeycloak()`, or
   - **(b)** Author a hand-maintained `deploy/k8s/keycloak/` (Deployment + Service + ConfigMap with realm import) and a `deploy/k8s/keycloak/README.md` documenting the carve-out from byte-determinism.

   Either way, change `WithEnvironment("Authentication__JwtBearer__SigningKey", "")` (lines 123/131/144/154/166) to read from a Kubernetes Secret reference rather than a literal:
   ```csharp
   // Instead of inline SigningKey, the AppHost emits a SecretKeyRef envFrom on each consumer.
   // The secret itself is bootstrapped by deploy-local.ps1 (not committed) and rotated
   // per the documented procedure.
   ```

5. **Compose Redis** as an Aspire `AddRedis()` resource so aspirate emits the Deployment + Service:
   ```csharp
   IResourceBuilder<RedisResource> redis = builder.AddRedis("redis")
       .WithDataVolume(); // emptyDir for MVP; PVC for post-MVP
   ```
   Then change the `pubsub`/`statestore` Dapr components in `deploy/dapr/*.yaml` to use the redis service name `redis.hexalith-parties.svc.cluster.local` (already done in current workarounds — confirm aspirate doesn't revert). Add a `Memories__StateStore` ref if the Memories.Server composition requires its own state-store entry.

### 4.5 `deploy/dapr/resiliency.yaml` rewrite

Run `kubectl get crd resiliencies.dapr.io -o yaml > /tmp/resiliency-crd.yaml` to capture the actual schema, then rewrite `resiliency.yaml` against the active fields. Add a step to `deploy-local.ps1` that performs `kubectl apply --dry-run=server -f deploy/dapr/resiliency.yaml` after the DAPR control-plane install, failing fast on schema regression in future DAPR upgrades.

### 4.6 `tests/Hexalith.Parties.DeployValidation.Tests` additions

New fitness tests:

- `TopologyContract_RequiresMemoriesServerDeployment` — fails if `deploy/k8s/memories/deployment.yaml` is absent.
- `TopologyContract_RequiresFrontComposerDeployment` — same for FrontComposer.
- `TopologyContract_RequiresKeycloakOrAuthorityOverride` — passes if either (a) a Keycloak Deployment is present, or (b) `Authentication__JwtBearer__Authority` is set via env on all `*-env` ConfigMaps.
- `TopologyContract_RequiresRedisBackingService` — fails if no Service named `redis` exists in the manifest set.
- `JwtSigningKey_NotLiteralInManifests` — fails if any `*-env` ConfigMap or Deployment env contains a literal `Authentication__JwtBearer__SigningKey` value (must be a `secretKeyRef`).

### 4.7 `sprint-status.yaml` update

```yaml
# Under the existing epic-9 block:
epic-9: in-progress
9-1-generate-k8s-artifacts-and-deploy-full-topology-to-local-cluster: done
9-2-extend-deployment-validation-to-kubernetes-manifests: done
9-3-close-k8s-deployment-spec-gaps: backlog            # ← new
epic-9-retrospective: optional

# Bump:
last_updated: 2026-05-19 <HH:MM>:00 +02:00
# Trailing comment:
# last_updated touched 2026-05-19 by correct-course adding story 9-3 (spec-implementation alignment)
```

---

## 5. Implementation Handoff

**Scope classification:** Moderate.

| Recipient | Responsibility | Deliverable |
|---|---|---|
| **Developer agent (Amelia / bmad-dev-story)** | Implement Story 9.3 sub-tasks (§ 4.4 - 4.6) on branch `feat/9-3-k8s-deploy-spec-alignment`. Run `regen.ps1` and `dotnet test` after each sub-task. End state: byte-deterministic regen + green DeployValidation tests + clean `deploy-local.ps1` run on the live cluster | PR `feat: story 9-3 close K8s deployment spec gaps` |
| **Tester (in-loop with Dev)** | Validate on the live cluster: `kubectl delete ns hexalith-parties && deploy-local.ps1` — confirm 9 pods reach `Ready` (parties, parties-mcp, eventstore, eventstore-admin, eventstore-admin-ui, tenants, memories, frontcomposer, keycloak, redis = 10 pods) without any `kubectl set env` | Test report attached to PR |
| **Product Owner (Alice / bmad-pm)** | Approve the FR31a edit (§ 4.2) and the D-K8s edit (§ 4.3) — those are minor wording sharpenings, but they ratify scope and need PO sign-off | PRD + architecture amendments merged with the PR |
| **Architect (Winston, advisory)** | Validate the SecretKeyRef pattern for JWT signing material and the Keycloak fallback decision (aspirate-native vs hand-authored) | One-line review comment on the PR |

**Success criteria for 9.3 closure:**

1. `deploy-local.ps1` from a clean `main` checkout brings the topology to Ready on the local cluster within NFR30's 15-minute target — **without any manual cluster-side commands**.
2. `regen.ps1` produces zero diff against the committed `deploy/k8s/` tree.
3. `dotnet test tests/Hexalith.Parties.DeployValidation.Tests` passes including the new fitness tests.
4. `kubectl -n hexalith-parties get resiliency` returns the configured CR (no schema rejection).
5. PR description references this proposal by filename.

---

## 6. Risks & open questions

| Risk | Mitigation |
|---|---|
| Aspirate cannot natively translate `AddKeycloak()` | Fallback to hand-authored `deploy/k8s/keycloak/` with documented carve-out (§ 4.4 sub-task 4 option b) |
| EventStore reader does not support array-indexed `Registrations__N__*` form | Parallel PR in `Hexalith.EventStore` submodule first. 1-2h of work expected |
| Story 9.1 record (currently `done`) becomes misleading because its AC1 was incomplete | Story 9.1 stays `done` — its scope was "happy-path local-cluster topology emission". 9.3 explicitly takes the closing remediations. Cross-reference both ways in story file headers |
| Memories.Server submodule exposes a project name that doesn't match `Projects.Hexalith_Memories_Server` (Aspire codegen) | Verify after `dotnet build src/Hexalith.Parties.AppHost`. If different, alias the namespace in the `using Projects;` line |

---

## 7. Approval

Submitted by: Claude (correct-course workflow)
Awaiting approval: **Jérôme**
Decision: **[pending — please respond `approved` / `revise` / `rejected`]**

Once approved, the next step is handoff to the Developer agent (or direct implementation if you want to drive it). The proposal lives on branch `feat/9-3-k8s-deploy-spec-alignment` and will be the first commit on that branch.
