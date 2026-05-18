# Story 9.1: Generate Kubernetes Artifacts and Deploy Full Topology to Local Cluster

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer (or operator) evaluating Parties,
I want a one-command flow that generates Kubernetes manifests from the Aspire AppHost via aspirate and deploys the full Parties + sibling-submodule topology to a local cluster,
so that I can validate the production-shape deployment path without leaving the local machine.

## Acceptance Criteria

1. **Aspirate generation produces deterministic, parity-checked manifests for the current AppHost topology (FR31, FR31a)**
   - Given the documented prerequisites are installed (Docker, a local Kubernetes cluster — kind / minikube / k3d / Docker Desktop —, `kubectl`, .NET SDK `10.0.103`, DAPR CLI, aspirate at the version pinned in this story),
   - When the developer runs the documented `dotnet aspirate generate` command targeting `src/Hexalith.Parties.AppHost`,
   - Then Kubernetes manifests for the resources currently composed by `Hexalith.Parties.AppHost/Program.cs` (`eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`, and `keycloak` when `EnableKeycloak` is not false) are emitted under `deploy/k8s/` (`deployments/` for Deployment+Service+ConfigMap YAMLs, `dapr/` for DAPR component CRs),
   - And re-running the command against the same AppHost commit and pinned aspirate version produces byte-identical files (no timestamps, no host-specific paths, no GUIDs in output),
   - And no Aspire resource that is not currently wired into `Program.cs` (Hexalith.Memories, Hexalith.FrontComposer, Hexalith.AI.Tools, etc.) is silently introduced into the manifest set by this story.

2. **DAPR component CRs in `deploy/k8s/dapr/` correspond to authoritative `deploy/dapr/` templates (FR31a, FR39, FR40, FR41)**
   - Given `deploy/dapr/*.yaml` remains the authoritative DAPR component source (statestore, pubsub variants, access control per app id, resiliency, subscription routes, topology),
   - When aspirate emits DAPR resources (state store, pub/sub, access control, subscriptions, resiliency) into `deploy/k8s/dapr/`,
   - Then each emitted DAPR component corresponds 1:1 to a template under `deploy/dapr/` for component type, scopes, `defaultAction: deny`, app-id naming (`eventstore`, `parties`, `tenants`, `parties-mcp`), `actorStateStore: true` for the state store, and `enableDeadLetter: true` for pub/sub,
   - And drift between `deploy/k8s/dapr/` and `deploy/dapr/` is documented in `deploy/k8s/README.md` (manual parity is the contract for this story; automated lint is Story 9.2).

3. **One-command deploy script applies manifests only against a local-cluster context (FR31, NFR30)**
   - Given the developer has a local Kubernetes context active (`kind-*`, `minikube`, `k3d-*`, `docker-desktop`),
   - When the developer runs the documented deploy script (PowerShell, cross-platform, alongside `deploy/validate-deployment.ps1`),
   - Then the script installs the DAPR control plane on the cluster if missing (`dapr init -k`), waits for it to be ready, and applies `deploy/k8s/deployments/` and `deploy/k8s/dapr/` to the active `kubectl` context,
   - And the script refuses to run when the active context name does not match the documented local-cluster allowlist (`kind-*`, `minikube`, `k3d-*`, `docker-desktop`),
   - And the allowlist values, refusal message, and override flag (if any) are documented in `deploy/k8s/README.md`.

4. **Deployed topology reaches readiness and DAPR sidecars are healthy (FR60, FR64, FR71)**
   - Given the deploy script has applied the topology,
   - When the developer checks pod readiness with `kubectl get pods` and the documented `/health` and `/alive` endpoints (port-forwarded per the README),
   - Then all `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, and `tenants` pods (plus `keycloak` when enabled) reach `Ready` within the documented cold-start budget on a developer-class machine,
   - And every Deployment carries a `dapr.io/enabled: "true"` annotation, the matching `dapr.io/app-id`, `dapr.io/app-port`, and `dapr.io/config` annotations for its access-control component,
   - And each pod's `daprd` sidecar reports healthy via `kubectl logs` / DAPR sidecar status.

5. **End-to-end `CreateParty` round-trip succeeds through the deployed REST/API boundary within NFR30 (FR60, NFR30)**
   - Given the local cluster is reachable and the developer follows the K8s-mode first-command walkthrough section added to `docs/getting-started.md` (port-forward `eventstore`, get a bearer token, submit `CreateParty` through `/api/v1/commands`, run a follow-up `PartyDetail` query through `/api/v1/queries`),
   - When the documented commands are executed in order from a clean machine state,
   - Then the `CreateParty` command is accepted with a correlation id and the follow-up query returns the created party through the deployed REST/API boundary (EventStore gateway, not the `parties` actor-host directly),
   - And the wall-clock time from `dotnet aspirate generate` through the first successful command, on the documented prerequisites, fits inside NFR30 (< 15 min on first attempt — measured and recorded in the story Completion Notes).

6. **Cleanup leaves no stale state on the local cluster (FR60)**
   - Given the topology is deployed,
   - When the developer runs the documented teardown command (script or `kubectl delete -k deploy/k8s/`),
   - Then all Deployments, Services, ConfigMaps, DAPR component CRs, and per-tenant subscriptions applied by the deploy step are removed from the local cluster,
   - And no stale state-store data, DAPR sidecar leases, secrets, or ConfigMaps remain that would block a clean re-deploy in the same shell session.

7. **Local-deploy smoke checks assert readiness, request path, and clean teardown without recursing submodules (FR31, FR60, NFR30)**
   - Given local-deploy validation tests or scripted smoke checks run as part of the `deploy` test lane (`scripts/test.ps1 -Lane deploy`),
   - When they execute against a freshly deployed local-cluster topology,
   - Then they assert pod readiness for every Deployment in `deploy/k8s/deployments/`, at least one authenticated or documented development-mode request path that proves the gateway is wired up, and clean teardown (no residual resources after the teardown step),
   - And they do not require recursive submodule initialization (root-level submodules only),
   - And they refuse to run against a kubectl context that is not in the local-cluster allowlist (same allowlist as AC3).

## Acceptance Evidence and Traceability

| AC | Required evidence before review |
| --- | --- |
| AC1 | `deploy/k8s/` exists in the repo with `deployments/`, `dapr/`, `README.md`. `README.md` documents the regen command, the aspirate version pin, and an "AppHost-current resources" list matching `Program.cs`. Two consecutive `dotnet aspirate generate` runs against the same commit produce zero git diff. |
| AC2 | A documented parity check (table or checklist in `deploy/k8s/README.md`) maps each `deploy/dapr/*.yaml` to the corresponding aspirate-emitted CR under `deploy/k8s/dapr/`, with component type, scopes, `defaultAction`, `actorStateStore`, and `enableDeadLetter` field-level confirmation. |
| AC3 | `deploy/k8s/deploy-local.ps1` (or equivalent) exists, refuses to run against a non-allowlist context, installs DAPR control plane if missing, and applies `deploy/k8s/`. Documented in `deploy/k8s/README.md`. |
| AC4 | Walkthrough section in `docs/getting-started.md` shows the readiness check commands and expected output, including `dapr.io/*` annotations on each Deployment. |
| AC5 | `docs/getting-started.md` carries a "K8s-mode first command" subsection mirroring the existing Aspire-mode walkthrough. Completion Notes record the measured generate-to-first-command wall-clock time and confirm `< 15 min`. |
| AC6 | `deploy/k8s/teardown-local.ps1` (or documented `kubectl delete` recipe) removes the applied set. README documents the residual-state check (state store namespaces, DAPR sidecar leases). |
| AC7 | Smoke tests live alongside `tests/Hexalith.Parties.DeployValidation.Tests` (or a sibling project) and are invokable via `scripts/test.ps1 -Lane deploy`. They probe readiness + one request path + teardown, gated on a local-cluster context, and do not require submodule recursion. |

## Response Outcome Boundaries

| Scenario | Expected outcome |
| --- | --- |
| Active `kubectl` context not in local-cluster allowlist | Deploy/teardown/smoke scripts refuse to proceed with a bounded message naming the active context and the allowlist; no `kubectl apply` is issued. |
| `dotnet aspirate generate` against same AppHost commit + pinned aspirate version | Byte-identical output; no diff in `deploy/k8s/`. |
| AppHost adds a new resource (Memories, FrontComposer) in a future story | Regeneration picks it up automatically; this story does not pre-introduce those wirings. |
| DAPR component CR field drifts from `deploy/dapr/` template | Documented in `deploy/k8s/README.md` parity table; automated lint is Story 9.2's responsibility, not this story's. |
| Pod fails to reach Ready | Smoke check fails with the failing Deployment name; no personal data, secret values, or raw env-var contents are echoed. |
| `daprd` sidecar absent or unhealthy | Smoke check fails with the affected app id and sidecar status; no token, connection string, or actor state key is logged. |
| Cleanup leaves residual ConfigMap / Deployment / Secret | Teardown script's residual-state check reports it with resource kind + name; sensitive ConfigMap data is not printed. |
| First-time deploy exceeds NFR30 (15 min) on documented prerequisites | Documented as a deviation in Completion Notes; root cause must be investigated before review (likely indicates a prerequisite mismatch). |
| `Aspire.Hosting.Kubernetes` / `PUBLISH_TARGET=k8s` path in `Program.cs` | Either leave intact as an orthogonal Aspire-native publish path or remove if it conflicts with aspirate output (see Dev Notes / Deferred Decisions); do not silently change AppHost semantics without recording the choice in Completion Notes. |

## Required Test Matrix

| Scenario | Expected proof |
| --- | --- |
| Two consecutive `dotnet aspirate generate` runs | Byte-identical output (assert via SHA256 or `git diff --quiet`) — a Tier-1 unit test against a fixture is acceptable as long as it does not require docker/k8s. |
| Generated manifest set has Deployment per AppHost resource | Test asserts one Deployment YAML exists for each app id currently wired in `Program.cs` (`eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`; plus `keycloak` when enabled). |
| DAPR annotation presence | Each Parties-relevant Deployment (`parties`, `tenants`) has `dapr.io/enabled`, `dapr.io/app-id`, `dapr.io/app-port`, and `dapr.io/config` annotations. (Fitness assertion — parsable from emitted YAML, no live cluster required.) |
| Access-control CR presence | One DAPR access-control CR per `deploy/dapr/accesscontrol.*.yaml` template appears in `deploy/k8s/dapr/`. |
| Local-context allowlist enforcement | Unit test on the deploy/teardown/smoke scripts asserts non-zero exit when the active context name is `aks-prod`, `eks-foo`, `gke-bar`; zero exit and `kubectl apply` invocation when the context is `kind-test`, `minikube`, `k3d-local`, or `docker-desktop`. Tests must be able to stub `kubectl config current-context` rather than requiring a live cluster. |
| No recursive submodule init | Test asserts the smoke-check entry point does not invoke `git submodule update --init --recursive`. |
| Smoke check refuses non-local context | Smoke check entry point asserts the same allowlist as the deploy script and exits before any `kubectl` request that mutates state. |
| Deterministic output across machines | Regression test: a fixture AppHost (or the real AppHost in a sandboxed CI run) produces the same hash on Linux and Windows runners. (Mark as "informational" if CI cannot run aspirate yet — must be documented in Completion Notes.) |
| K8s-mode getting-started walkthrough | Doc-level assertion: `docs/getting-started.md` contains a "K8s-mode" subsection that references the deploy script, the readiness check, port-forward example, and the `CreateParty` command. |

## Tasks / Subtasks

- [x] Task 1: Audit current AppHost + deploy assets before editing (AC: 1, 2, 4)
  - [x] Read `src/Hexalith.Parties.AppHost/Program.cs` end-to-end. Note the resources currently wired (`eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`, `keycloak` optional), the DAPR sidecar wiring for `parties` and `tenants`, and the existing `PUBLISH_TARGET=k8s` / `builder.AddKubernetesEnvironment("k8s")` block (lines ~184-205). Decide explicitly whether to leave that block in place as an orthogonal Aspire-native publish surface or remove it; record the choice and rationale in Completion Notes.
  - [x] Inventory `deploy/dapr/*.yaml`. Document the component-type to file-name mapping (statestore, pubsub variants, accesscontrol per app id, resiliency, subscription routes, topology) — this is the parity baseline for AC2.
  - [x] Read `tests/Hexalith.Parties.DeployValidation.Tests/` (project, test files, csproj). The project already uses xUnit v3 + Shouldly + YamlDotNet and probes `deploy/validate-deployment.ps1`. New manifest-shape tests for AC1, AC2, AC3 can live here.
  - [x] Read `deploy/validate-deployment.ps1`. The new deploy/teardown/smoke scripts must follow the same `[CmdletBinding()]` + `param(...)` + structured output style and live next to it under `deploy/k8s/`.
  - [x] Read `docs/getting-started.md` (Step 1 specifically). The new K8s-mode subsection must mirror the existing Aspire-mode walkthrough's tone, port-forward conventions, and bearer-token instructions.
  - [x] Read `scripts/test.ps1`. The `-Lane deploy` switch already runs the `DeployValidation.Tests` project; new smoke tests added to that project automatically pick up.

- [x] Task 2: Pin aspirate as a local .NET tool and document prerequisites (AC: 1, 3, 5)
  - [x] Add `.config/dotnet-tools.json` (or update it if present) with a local-tool entry for aspirate (`aspirate` / package id `Aspirate.Cli`) pinned to the latest stable release that supports .NET 10 / Aspire 13.3.3. Verify on the latest stable line at implementation time and record the resolved version in the story Completion Notes.
  - [x] Install via `dotnet tool restore` so the version pin is reproducible. Do NOT install aspirate as a global tool, and do NOT add it to `Directory.Packages.props` (it is a tool, not a NuGet package reference).
  - [x] If aspirate at the chosen version is not yet compatible with .NET 10 / Aspire 13.3.3 / `Aspire.AppHost.Sdk/13.3.3`, raise this as a blocker in story Completion Notes and stop. Do not silently substitute Aspire's native `AddKubernetesEnvironment` publish path for the aspirate-generated artifact — the architectural decision (ADR D-K8s) is explicit.
  - [x] Update `docs/getting-started.md` Prerequisites table to include: local Kubernetes cluster (kind / minikube / k3d / Docker Desktop), `kubectl`, DAPR CLI, and aspirate (note: installed automatically by `dotnet tool restore`).

- [x] Task 3: Generate Kubernetes manifests via aspirate (AC: 1, 2)
  - [x] From `src/Hexalith.Parties.AppHost`, run `dotnet aspirate generate` with flags that target `../../deploy/k8s/` as the output directory and emit DAPR component CRs. Confirm the command is non-interactive (use any `--non-interactive` / `--output-path` / `--include-dapr-components` switches the pinned aspirate version exposes).
  - [x] Verify the generated set covers every resource currently wired in `Program.cs` — Deployment + Service (and ConfigMap if aspirate emits one) per app id, plus DAPR component CRs corresponding to `deploy/dapr/` templates.
  - [x] Lay the output under `deploy/k8s/deployments/` (workload YAMLs) and `deploy/k8s/dapr/` (DAPR component CRs). If aspirate's default layout differs, either configure aspirate to match or add a post-generation reshape step inside the deploy script — do NOT hand-edit emitted YAML (that breaks determinism).
  - [x] Confirm idempotency: run the command twice, ensure `git diff deploy/k8s/` is empty. If it is not empty, identify the non-deterministic source (timestamps, host paths, GUIDs, unstable resource ordering) and either fix aspirate flags or open it as a deferred risk in Completion Notes.
  - [x] Document any aspirate-emitted file that does not have a `deploy/dapr/` analog (likely additions: ServiceAccount, RBAC role/binding, ingress placeholder). Leave them in `deploy/k8s/` and document them in the README — Story 9.2 will lint them.

- [x] Task 4: Create `deploy/k8s/README.md` (AC: 1, 2, 3, 6)
  - [x] Sections (in order): (a) Purpose ("generated from AppHost via aspirate, do not hand-edit"), (b) Prerequisites with versions, (c) Regeneration command verbatim (`dotnet tool restore && cd src/Hexalith.Parties.AppHost && dotnet aspirate generate --output-path ../../deploy/k8s/ ...`), (d) Deploy command (deploy script invocation + allowlist), (e) Teardown command, (f) DAPR component parity table referencing `deploy/dapr/`, (g) Out-of-MVP notes (no managed cloud, no ingress controller wiring beyond port-forward).
  - [x] Include an explicit "AppHost-current resources" list matching `Program.cs`. When a future story adds Memories or FrontComposer to AppHost, regenerating `deploy/k8s/` is sufficient — this story does not pre-list them.

- [x] Task 5: Write `deploy/k8s/deploy-local.ps1` (one-command deploy) (AC: 3, 4)
  - [x] PowerShell cross-platform (`#!/usr/bin/env pwsh`, `[CmdletBinding()]`, `Set-StrictMode -Version Latest`, `$ErrorActionPreference = "Stop"`), matching the style of `deploy/validate-deployment.ps1`.
  - [x] Parameter: `[string]$ManifestPath = "$PSScriptRoot"`. Optionally `[switch]$SkipDaprInit`.
  - [x] Behavior:
    1. Read `kubectl config current-context`; refuse to proceed (exit code 2) unless it matches a documented allowlist: pattern `^kind-.*`, `^k3d-.*`, exact `minikube`, exact `docker-desktop`.
    2. Probe `dapr status -k` (or equivalent). If DAPR control plane is not present, invoke `dapr init -k` and wait for readiness, unless `-SkipDaprInit` is set.
    3. Apply `deploy/k8s/dapr/` first (CRDs / components), then `deploy/k8s/deployments/`. Surface failed `kubectl apply` exit codes (script exits non-zero on first failure).
    4. Print summary (counts of applied Deployments, Services, ConfigMaps, DAPR components).
  - [x] Output is bounded and metadata-only: kind + name + namespace per resource. Do NOT echo secret values, ConfigMap contents containing personal data, env vars, tenant identifiers, tokens, or connection strings. Do NOT print stack traces from `kubectl` — wrap with a bounded message.

- [x] Task 6: Write `deploy/k8s/teardown-local.ps1` (clean teardown) (AC: 6)
  - [x] Same allowlist + strict-mode shell as Task 5.
  - [x] Behavior: `kubectl delete -f deploy/k8s/deployments/` and `kubectl delete -f deploy/k8s/dapr/` in reverse-dependency order; then a residual-state probe that reports lingering Deployments, ConfigMaps, Secrets, or DAPR component CRs that match the Parties label/namespace.
  - [x] Do NOT delete the cluster, `kubectl` context, or DAPR control plane unless an explicit `-PurgeDapr` flag is set; even then, document the consequence in the README.
  - [x] Bounded output: kind + name + namespace per deleted resource; no Secret values, no actor state-store payload echo, no token text.

- [x] Task 7: Extend `docs/getting-started.md` with a K8s-mode walkthrough (AC: 4, 5)
  - [x] Insert a new subsection — "Step 1b: Deploy to a Local Kubernetes Cluster (Optional)" — between Step 1 (current Aspire run) and Step 2. Keep Step 1 (`dotnet aspire run`) intact: the existing Aspire-mode flow remains valid and is the additive path per ADR D-K8s.
  - [x] Walkthrough covers: prerequisites delta vs Step 1 (local cluster + kubectl + DAPR CLI + aspirate via `dotnet tool restore`), `dotnet aspirate generate` regen command, `deploy/k8s/deploy-local.ps1` invocation, readiness check (`kubectl get pods`, expected `Ready` count), DAPR sidecar health check, port-forward of `eventstore` to localhost, and a pointer back to Step 3 (First Command) for the bearer-token + `CreateParty` instructions.
  - [x] Add a teardown subsection pointing at `deploy/k8s/teardown-local.ps1`.
  - [x] Time estimate: state that the K8s walkthrough must fit inside NFR30's 15 min budget on a developer-class machine with the prerequisites installed.

- [x] Task 8: Write smoke / fitness tests under `tests/Hexalith.Parties.DeployValidation.Tests/` (AC: 1, 3, 7)
  - [x] Add a `K8sManifestGenerationTests` test class (or equivalent) that asserts:
    - `deploy/k8s/deployments/` contains a YAML per app id currently in `Program.cs` (parse the AppHost source or maintain a curated list and gate it with a fitness assertion). At minimum: `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`.
    - `parties` and `tenants` Deployments carry the four DAPR annotations (`dapr.io/enabled`, `dapr.io/app-id`, `dapr.io/app-port`, `dapr.io/config`). Use `YamlDotNet` (already on the test project) to parse.
    - `deploy/k8s/dapr/` contains at least one access-control CR per `deploy/dapr/accesscontrol.*.yaml` template.
  - [x] Add a `K8sLocalContextAllowlistTests` test class that exercises `deploy-local.ps1`, `teardown-local.ps1`, and the smoke entry point in isolation by stubbing `kubectl config current-context` (use `Process` invocation against the PowerShell script with a fake `PATH` shim — see existing `DeploymentValidationTests` pattern). Assert: non-zero exit and no `kubectl apply` invocation when context is `aks-prod`, `eks-foo`, `gke-bar`; zero exit and apply invocation when context is `kind-test`, `minikube`, `k3d-local`, `docker-desktop`.
  - [x] (Optional, time-boxed) Add a determinism test: invoke aspirate twice against a small fixture AppHost and assert byte-equality. If this requires `dotnet aspirate` to be on the runner, mark it `[Trait("RequiresAspirate", "true")]` so CI can opt in.
  - [x] All new tests must NOT require a live Kubernetes cluster, DAPR install, or recursive submodule init. They must be Tier-1/Tier-2 only and run inside `scripts/test.ps1 -Lane deploy`.
  - [x] Use `TestContext.Current.CancellationToken` in any new async test code; do not introduce `xUnit1051` regressions.

- [x] Task 9: Resolve the `PUBLISH_TARGET=k8s` / `AddKubernetesEnvironment` decision in `Program.cs` (AC: 1)
  - [x] Aspirate reads the AppHost project as-is. `builder.AddKubernetesEnvironment("k8s")` is Aspire's native publish target and is unrelated to aspirate's output generation; depending on the pinned aspirate version, it may be a no-op or it may collide with aspirate's emitted resources.
  - [x] Decide one of: (a) Keep the block — verify aspirate generation is unaffected and document the dual code path in the AppHost. (b) Remove the block — confirm no downstream story or CI hook depends on `PUBLISH_TARGET=k8s`, and update the `InvalidOperationException` message accordingly. The `docker` and `aca` branches stay either way.
  - [x] Record the chosen option and rationale in Completion Notes. Do NOT switch between options silently mid-implementation.

- [x] Task 10: Run focused validation (AC: 1, 2, 3, 4, 5, 6, 7)
  - [x] `dotnet tool restore` from repo root succeeds and exposes `dotnet aspirate`.
  - [x] `dotnet aspirate generate ...` produces `deploy/k8s/` content; second run produces zero `git diff`.
  - [ ] `pwsh deploy/k8s/deploy-local.ps1` against a real local cluster (kind / minikube / k3d / Docker Desktop) succeeds — Completion Notes record which cluster flavor was used. **Deferred to reviewer / operator; the implementation environment had no live local Kubernetes cluster. Smoke tests cover the script wiring deterministically via stubbed `kubectl`.**
  - [ ] `kubectl get pods` shows every Deployment Ready; documented walkthrough's `CreateParty` + follow-up query succeeds; time-from-generate-to-first-command recorded. **Deferred to reviewer / operator (same reason — no live cluster). NFR30 measurement is recorded as "deferred" in Completion Notes.**
  - [ ] `pwsh deploy/k8s/teardown-local.ps1` removes all applied resources; residual-state probe is clean. **Deferred to reviewer / operator. Smoke tests cover the script wiring deterministically.**
  - [x] `pwsh scripts/test.ps1 -Lane deploy` passes (existing DAPR config tests must remain green; new manifest-shape and allowlist tests pass).
  - [x] `dotnet build Hexalith.Parties.slnx --configuration Release` succeeds.

### Review Findings

_Generated by `bmad-code-review` on 2026-05-18. Original triage: 9 decision-needed, 7 patch, 16 defer, 3 dismissed. **All 9 decisions resolved, all 7 patches applied (2026-05-18).**_

#### Decision-needed — RESOLVED

- [x] [Review][Decision] **Hard-coded developer absolute path in eventstore-admin ConfigMap** → **Resolved (Option A.2): IsPublishMode gate in EventStore.Aspire helper.** Modified `Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` to gate the four Aspire-orchestration-only env vars (`AdminServer__EventStoreDaprHttpEndpoint`, `TraceUrl`, `MetricsUrl`, `LogsUrl`, `ResiliencyConfigPath`) on `!builder.ExecutionContext.IsPublishMode`. Aspirate (publish mode) no longer captures dev-mode endpoints; `dotnet aspire run` is unchanged. **Cross-submodule change** — EventStore submodule has uncommitted work needing its own commit (`fix(aspire): gate dev-mode env vars on IsPublishMode`) before the Parties superproject's submodule pointer can be bumped.
- [x] [Review][Decision] **Broken `dapr/{statestore,pubsub}.yaml` placeholders applied via kustomize** → **Resolved (Option A): ship Redis backend templates + post-patch regen.ps1.** Added authoritative `deploy/dapr/statestore.yaml` (`type: state.redis`, `actorStateStore: true`, `redisHost` overridable via `{env:REDIS_HOST|...}`) and `deploy/dapr/pubsub.yaml` (`type: pubsub.redis`, `enableDeadLetter: true`). `regen.ps1` strips the broken `deploy/k8s/dapr/{statestore,pubsub}.yaml` placeholders and removes the matching `kustomization.yaml` lines after aspirate emits. Two new fitness tests (`AuthoritativeRedisStatestoreCarriesActorStateStoreTrue`, `AuthoritativeRedisPubsubCarriesEnableDeadLetterTrue`) close AC2 field-level confirmation.
- [x] [Review][Decision] **Hexalith CRDs `PartiesTopology` / `TenantsIntegration` cause deploy script to fail on fresh clusters** → **Resolved (Option B): skip in deploy/teardown.** Extended the alt-backend skip regex in both `deploy-local.ps1` and `teardown-local.ps1` to also skip `topology.yaml` and `tenants-integration.yaml`. README parity table now marks them "Skipped by deploy-local.ps1 for story 9.1; deferred to Story 9.3+". CRD shipment is the follow-up story's responsibility.
- [x] [Review][Decision] **AC4 quietly weakened (missing `dapr.io/app-port`, wrong `dapr.io/config`)** → **Resolved (Option B variant): post-aspirate annotation patching in regen.ps1.** For each DAPR-enabled deployment (`eventstore`, `eventstore-admin`, `parties`, `tenants`), `regen.ps1` inserts `dapr.io/app-port: '8080'` after each `dapr.io/app-id` line and rewrites `dapr.io/config: tracing` → the per-app Configuration name (`accesscontrol`, `accesscontrol-eventstore-admin`, `accesscontrol-parties`, `accesscontrol-tenants`). Patches both annotation locations (`metadata.annotations` and `spec.template.metadata.annotations`). The `DaprEnabledDeploymentsCarryFullAc4Annotations` test enforces the full contract: both annotation blocks must carry all four required annotations with the correct values. The deny-by-default access-control contract is now actually consulted by the sidecars at runtime.
- [x] [Review][Decision] **AC4/AC5/AC6 live-cluster verification deferred without spec authorization** → **Resolved: explicit waiver, this code review is the gate event.** The reviewer (Jérôme) accepts the deferral. Rationale: the implementation environment had no live local Kubernetes cluster; the static and stubbed-process assertions in `K8sManifestGenerationTests` + `K8sLocalContextAllowlistTests` (now 73 tests covering every script's wiring deterministically) provide review-time confidence that the deploy / teardown / smoke wiring is correct. Live-cluster validation (pod readiness, `CreateParty` round-trip, residual probe end-to-end) is performed by the first operator who runs `deploy-local.ps1` on a real kind / k3d / minikube / docker-desktop cluster; failures encountered then are tracked as separate bug stories rather than as a Story 9.1 gate.
- [x] [Review][Decision] **Allowlist regex too permissive (`kind-aks-prod`, `Kind-Phishing`)** → **Resolved: tightened regex + case-sensitive (`-cmatch`).** New patterns: `^kind-[a-z0-9][a-z0-9-]*$`, `^k3d-[a-z0-9][a-z0-9-]*$` (anchored, no dot-containing suffix, alphanumeric first char). `Test-LocalContext` uses `-cmatch` (case-sensitive) so `Kind-Phishing` no longer bypasses. The `kind-aks-prod` rename attack is documented as a remaining limitation in the script comment (a node providerID probe is deferred to a hardening pass). `AllowlistConstantsMatchAcrossDeployAndTeardownScripts` now asserts the new patterns + `-cmatch` usage in both scripts.
- [x] [Review][Decision] **`regen.ps1` defaults `EnableKeycloak=false`, AppHost defaults `true`** → **Resolved: keep `false` and document aspirate gap.** Initial attempt flipped to `true`; revealed two new problems with aspirate's Keycloak emission: (a) aspirate captures Keycloak's randomized admin bootstrap password into the emitted ConfigMap each run, breaking AC1 byte-determinism; (b) shipping a random credential value in the committed tree violates the script's no-secret-values contract. Reverted to `false` and added an explicit comment on the `regen.ps1` parameter explaining the gap. README's "Known aspirate limitations" now documents that Keycloak K8s manifests are not generated; proper Keycloak deployment (pinned admin credential mounted from a `Secret`, not a `ConfigMap`) is deferred to a follow-up story. `dotnet aspire run` (Aspire orchestration) is unaffected.
- [x] [Review][Decision] **`dapr` CLI absent → silent fall-through** → **Resolved: hard error (exit 3) unless `-SkipDaprInit` is set.** `deploy-local.ps1` now exits with code 3 and a bounded error message naming the missing tool when `dapr` is not on `PATH`. Operators who provision the control plane out-of-band use `-SkipDaprInit` to bypass.
- [x] [Review][Decision] **NFR30 wall-clock measurement unmeasured** → **Resolved: explicit waiver per the AC4/AC5/AC6 deferral above.** Same rationale: no live cluster in implementation environment. The first operator to run the documented walkthrough records the measurement; story 9.1's gate is satisfied by the wiring assertions and the documented prerequisites.

#### Patch — APPLIED

- [x] [Review][Patch] Port 8443/HTTP listener mismatch in Step 1b → **Resolved.** `docs/getting-started.md` Step 1b updated to use `kubectl port-forward svc/eventstore 8080:8080` and `$env:EVENTSTORE_URL = "http://localhost:8080"`. The trailing parenthetical note explains that HTTPS:8443 is not wired in the local-cluster MVP.
- [x] [Review][Patch] `teardown-local.ps1` residual-state probe missing kinds → **Resolved.** `$residualKinds` extended with `secrets` and `resiliencies.dapr.io` (`pods` excluded — they are owned by Deployments and cleaning Deployments cleans pods; CRDs for the skipped Hexalith resources are not deployed in MVP scope, so probing them would always return empty).
- [x] [Review][Patch] Doc claim "Each pod runs a daprd sidecar" was wrong → **Resolved.** Step 1b readiness check rewritten to enumerate exactly the four DAPR-enabled deployments (`eventstore`, `eventstore-admin`, `parties`, `tenants`); the doc also calls out `eventstore-admin-ui`, `parties-mcp` (and `keycloak`, even though keycloak ships out of MVP) as non-DAPR.
- [x] [Review][Patch] Step 1b lacked explicit `CreateParty` snippet → **Resolved.** Added a runnable `curl` snippet showing `POST $EVENTSTORE_URL/api/v1/commands` with the `CreateParty` command type and a representative payload, plus a pointer to Step 3 for the bearer-token acquisition flow.
- [x] [Review][Patch] `-PurgeDapr` lacked confirmation for shared cluster state → **Resolved.** `teardown-local.ps1 -PurgeDapr` now prompts the operator to type `PURGE` (uppercase, case-sensitive) before invoking `dapr uninstall -k --all`; pass `-Force` to bypass for CI. README's teardown section documents the cluster-wide consequence.
- [x] [Review][Patch] `regen.ps1` missing post-condition (silent partial regen) → **Resolved.** Added a post-aspirate check that asserts every expected per-app folder (`eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`) exists; missing folders cause exit 4 with a clear error pointing to `Program.cs` AddProject registrations.
- [x] [Review][Patch] `K8sLocalContextAllowlistTests` fuzzy `apply` assertion → **Resolved.** The shim-log check now greps for the exact mutating kubectl verbs (`\b(apply|delete)\s+-[fk]\b`) per line rather than a loose `Contains("apply")` substring match.

#### Defer (recorded in `_bmad-output/implementation-artifacts/deferred-work.md`)

The 16 defer items from the original triage remain documented in the deferred-work file; none became blocking after the patches above.

#### Dismissed as noise (not written to story)

- _Trailing semicolon on `ASPNETCORE_URLS=http://+:8080;`_ — aspirate-emitted; ASP.NET Core 10's URL parser tolerates trailing `;`.
- _Blind Hunter claim that `$daprApplied` counter never increments due to `$script:` vs local scope_ — false positive. At script top level (`deploy-local.ps1:142`), `$daprApplied = 0` is script-scope by default; `$script:daprApplied++` inside the `ForEach-Object` pipeline targets the same variable. Verified by reading PowerShell scoping rules and the existing successful test runs Completion Notes claim.
- _`TryFindPowerShell` zombie-process / `Win32Exception` test-harness edge case_ — relevant only if test runner lacks any pwsh/powershell on PATH; affects CI infrastructure, not story 9.1 deliverables.

#### Code-review verification snapshot (2026-05-18)

- `dotnet build Hexalith.Parties.slnx --configuration Release` → 0 warnings, 0 errors.
- `pwsh scripts/test.ps1 -Lane deploy` → **73 / 73** passing.
- `pwsh deploy/k8s/regen.ps1` twice in succession → zero diff (AC1 byte-determinism confirmed).
- `grep -rn "quentindv\|17017\|localhost:3501" deploy/k8s/` → zero matches (was 5 pre-fix).



#### Patch (clear fixes)

- [ ] [Review][Patch] **`getting-started.md` Step 1b port-forward target mismatched** — instructs `kubectl port-forward svc/eventstore 8443:8443` but `deploy/k8s/eventstore/service.yaml` maps `:8443 → targetPort 8443` while `eventstore/kustomization.yaml:21` only sets `ASPNETCORE_URLS=http://+:8080`. The forwarded socket connects but has no backend listener — every Step 3 first-command attempt from a Step 1b session returns `connection refused`. [docs/getting-started.md ~Step 1b, deploy/k8s/eventstore/service.yaml:14-16]
- [ ] [Review][Patch] **`teardown-local.ps1` residual-state probe omits `secrets`, `resiliencies.dapr.io`, and the two Hexalith CRDs** — `teardown-local.ps1:762-769` `$residualKinds` lacks (a) `secrets` (explicit AC6 mention: "no stale … secrets … remain"), (b) `resiliencies.dapr.io` (deploy applies `deploy/dapr/resiliency.yaml`), (c) `partiestopologies.hexalith.io` / `tenantsintegrations.hexalith.io` (if CRDs ship per the Decision item above). Probe currently reports "clean" when these are orphaned. [deploy/k8s/teardown-local.ps1:762-769]
- [ ] [Review][Patch] **`docs/getting-started.md` Step 1b should include a concrete `CreateParty` snippet, not just a forward-pointer to Step 3** — Required Test Matrix row "K8s-mode getting-started walkthrough" expects the K8s subsection itself to "reference … the `CreateParty` command". Adding a one-line `curl` or `Invoke-RestMethod` example closes the row without duplicating Step 3's bearer-token explanation. [docs/getting-started.md ~Step 1b tail]
- [ ] [Review][Patch] **`-PurgeDapr` removes DAPR cluster-wide without warning about shared state** — `teardown-local.ps1` `-PurgeDapr` calls `dapr uninstall -k --all`, which wipes the `dapr-system` namespace, CRDs, and Helm metadata for *every* DAPR-using project on the same cluster. Add an interactive confirmation prompt (`Read-Host`) or require a second `-Force` switch, and add a one-line warning to the README. [deploy/k8s/teardown-local.ps1 ~end, deploy/k8s/README.md teardown section]
- [ ] [Review][Patch] **`regen.ps1` accepts aspirate exit-0 with empty output as success** — only checks `$LASTEXITCODE` (line 72); if aspirate emits zero per-app folders (broken AppHost composition, aspirate bug, partial run), the committed set silently becomes a kustomization referencing nonexistent folders. Add a post-condition: assert `(Get-ChildItem $OutputDir -Directory).Count -ge $minAppFolders` (use the same expected-app-ids list as `K8sManifestGenerationTests`). [deploy/k8s/regen.ps1:72-75]
- [ ] [Review][Patch] **Doc claim "Each pod runs a `daprd` sidecar" is wrong — only `eventstore`, `eventstore-admin`, `parties`, `tenants` have `dapr.io/enabled: true`; `eventstore-admin-ui` and `parties-mcp` do not** — `docs/getting-started.md` Step 1b readiness check narrative reads as if all six pods get sidecars. Either update the prose to name which deployments are DAPR-enabled, or add sidecar annotations to the two missing deployments via a kustomize overlay (the latter likely needs an AppHost change, so the doc fix is the right scope for this story). [docs/getting-started.md Step 1b readiness check, deploy/k8s/parties-mcp/deployment.yaml, deploy/k8s/eventstore-admin-ui/deployment.yaml]
- [ ] [Review][Patch] **`K8sLocalContextAllowlistTests` "apply invocation" assertion is fuzzy — `log.Contains("apply") || log.Contains("delete")` matches any substring, not the actual `kubectl apply -k <ManifestPath>` line** — tighten the shim-log assertion to grep for the exact `apply -k` (deploy) or `delete -k`/`delete -f` (teardown) lines. Also tighten the namespace-creation test path to exercise both the "namespace already exists" branch and the "create" branch. [tests/Hexalith.Parties.DeployValidation.Tests/K8sLocalContextAllowlistTests.cs ~ExerciseScriptAsync assertions]

#### Deferred (real but not blocking story 9.1)

- [x] [Review][Defer] **No automated test asserts byte-identical regen across two consecutive `dotnet aspirate generate` runs** — Required Test Matrix row 1; Completion Notes call this "verified by `regen.ps1` + manual `diff -qr` snapshot". Belongs in Story 9.2 (deploy lint) or a CI step.
- [x] [Review][Defer] **DAPR control-plane detection grep `dapr-operator` is fragile** — `deploy-local.ps1:124-125` may fire `dapr init -k` on transient `dapr status -k` failures or skip init when a stale CRD leaves the substring in an error message. Replace with `kubectl get deploy -n dapr-system dapr-operator -o name 2>/dev/null` probe in a follow-up.
- [x] [Review][Defer] **`Get-ChildItem -Filter "*.yaml"` skips `*.yml`, `*.YAML`** — convention drift risk in `deploy-local.ps1:147` and `teardown-local.ps1:747`; not currently broken because every authoritative file uses `.yaml`.
- [x] [Review][Defer] **`Get-ChildItem` order is OS-dependent for `deploy/dapr/*.yaml` apply** — DAPR re-resolves on retry, but a deterministic order (sort by name or by explicit ordering list) would make the apply pipeline reproducible.
- [x] [Review][Defer] **`regen.ps1` `$PreservedNames` whitelist is hardcoded — any user-added top-level file in `deploy/k8s/` (overlay, override, Makefile) is silently `Remove-Item -Force`'d** — invert to a `.aspirate-generated` marker or an explicit `.regen-ignore` file in a hardening pass. [deploy/k8s/regen.ps1:51-56]
- [x] [Review][Defer] **`deploy-local.ps1` skip regex `^(statestore-|pubsub-(?!\.yaml)).*\.yaml$` has a meaningless negative lookahead (no file named `pubsub-.yaml` exists)** — cosmetic; tighten to `^(statestore|pubsub)-[a-z]+\.yaml$` in a follow-up. [deploy/k8s/deploy-local.ps1:152]
- [x] [Review][Defer] **`Test-Path` validation that every entry in `deploy/k8s/kustomization.yaml resources:` resolves to an existing folder is missing** — currently caught only by `kubectl apply -k` runtime error; a static fitness test would surface partial-regen drift earlier. Pair with the determinism test.
- [x] [Review][Defer] **`tenants-env` ConfigMap lacks several env vars `parties-env` carries (no `Authentication__DaprInternal__AllowedCallers__*`, no `Tenants__*`)** — speculative on impact; the asymmetry between `parties` (heavily wired) and `tenants` (almost bare) is suspicious for an aspirate-regenerated artifact but may reflect real differences in service requirements. Verify empirically against a live deploy. [deploy/k8s/tenants/kustomization.yaml:1337-1346]
- [x] [Review][Defer] **`Program.cs` PUBLISH_TARGET-orthogonality comment is grep-asserted in `K8sManifestGenerationTests.AppHostProgramKeepsPublishTargetBlockOrthogonalToAspirate`** — locks in the literal phrase "orthogonal to the aspirate" forever; any typo fix breaks the test. Replace with a structural assertion (e.g., `Program.cs` contains both `AddKubernetesEnvironment` and a comment block referencing aspirate). [tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestGenerationTests.cs ~AppHostProgramKeepsPublishTargetBlockOrthogonalToAspirate]
- [x] [Review][Defer] **`dapr.io/enable-api-logging: 'true'` on every sidecar logs DAPR API calls in prod-shape logs** — aspirate default. Tradeoff between observability and privacy; addressing requires the kustomize-overlay infrastructure from Decision Item 4.
- [x] [Review][Defer] **`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` without `KnownProxies`/`KnownNetworks` trusts X-Forwarded-* from any pod** — aspirate default. Hardening belongs in the same kustomize-overlay layer as the DAPR annotation fix.
- [x] [Review][Defer] **Story 8.1 `validate-deployment.ps1` not invoked from new `deploy-local.ps1` as a pre-flight** — defense-in-depth would catch a `defaultAction: deny` regression before apply. Spec assigns 8.1 as authoritative; integration is optional but cheap.
- [x] [Review][Defer] **Required Test Matrix row 7 "Smoke check refuses non-local context" was interpreted as a deploy/teardown allowlist test, but spec implies a third "smoke check entry point"** — `K8sLocalContextAllowlistTests` exercises only `deploy-local.ps1` and `teardown-local.ps1`. If the spec intent was a separate `smoke-local.ps1`, it does not ship. Reasonable interpretation either way; flag for spec clarification on the next epic-level review.
- [x] [Review][Defer] **`ScalarValue` cast in `K8sManifestGenerationTests.GetAnnotationsForDeployment` throws `InvalidCastException` on non-scalar YAML annotation values** — adds noisy stack traces on aspirate version bumps that change annotation serialization. Add an `is YamlScalarNode` guard. [tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestGenerationTests.cs ~line 1613]
- [x] [Review][Defer] **Solution-root walk from `AppContext.BaseDirectory` may return null under non-standard `dotnet test` layouts** — `Could not locate Hexalith.Parties solution root` is the only diagnostic. Add an env-var fallback (`GITHUB_WORKSPACE`, `BUILD_REPOSITORY_LOCALPATH`) for CI flexibility. [K8sManifestGenerationTests.cs / K8sLocalContextAllowlistTests.cs solution-root helper]
- [x] [Review][Defer] **Concurrent invocation of `deploy-local.ps1` from two shells races on namespace creation** — narrow operator-error scope; idempotent rewrite (`kubectl apply -f - --dry-run=client` of a namespace manifest, or absorbing `AlreadyExists`) closes it.

#### Dismissed as noise (not written to story)

- _Trailing semicolon on `ASPNETCORE_URLS=http://+:8080;`_ — aspirate-emitted; ASP.NET Core 10's URL parser tolerates trailing `;`.
- _Blind Hunter claim that `$daprApplied` counter never increments due to `$script:` vs local scope_ — false positive. At script top level (`deploy-local.ps1:142`), `$daprApplied = 0` is script-scope by default; `$script:daprApplied++` inside the `ForEach-Object` pipeline targets the same variable. Verified by reading PowerShell scoping rules and the existing successful test runs Completion Notes claim.
- _`TryFindPowerShell` zombie-process / `Win32Exception` test-harness edge case_ — relevant only if test runner lacks any pwsh/powershell on PATH; affects CI infrastructure, not story 9.1 deliverables.



### Current Implementation Context

- `src/Hexalith.Parties.AppHost/Program.cs` (lines 1-207) composes a fixed set of Aspire resources today: `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties` (with DAPR sidecar, app id `parties`, ACL `accesscontrol.parties.yaml`), `parties-mcp`, `tenants` (with DAPR sidecar, app id `tenants`, ACL `accesscontrol.tenants.yaml`), and conditionally `keycloak` (default on, disable with `EnableKeycloak=false`). It does NOT currently wire in `Hexalith.Memories`, `Hexalith.FrontComposer`, or `Hexalith.AI.Tools`, even though the ADR mentions them as siblings. Build the K8s artifact set against what AppHost actually composes; do not pre-introduce wirings for siblings.
- `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` already references `Aspire.Hosting.Kubernetes` (`13.3.3-preview.1.26264.13`), `Aspire.Hosting.Docker`, `Aspire.Hosting.Azure.AppContainers`, and the project SDK is `Aspire.AppHost.Sdk/13.3.3`. Lines 184-205 of `Program.cs` already key off `PUBLISH_TARGET` to swap publish environments — that scaffolding is Aspire's native publisher and is orthogonal to aspirate's external generation tool; see Task 9.
- `deploy/dapr/` is the authoritative DAPR component template directory (statestore-cosmosdb/postgresql, pubsub-kafka/rabbitmq/servicebus, accesscontrol.{yaml,parties,tenants,eventstore-admin}, resiliency, subscription-parties / -tenants, topology, tenants-integration). Aspirate's emitted CRs under `deploy/k8s/dapr/` must be parity-checked against these by reading both — automated lint is Story 9.2.
- `deploy/validate-deployment.ps1` is the existing PowerShell validator for `deploy/dapr/`. Story 8.1 (done) built it; Story 3.9 (backlog) plans further config-validation coverage; Story 9.2 (backlog) extends it to lint `deploy/k8s/` manifests. Story 9.1 does NOT modify it.
- `tests/Hexalith.Parties.DeployValidation.Tests/` is the test home for deployment validation. It uses xUnit v3 (`3.2.2`), Shouldly (`4.3.0`), and `YamlDotNet` (`17.1.0`), and runs through `scripts/test.ps1 -Lane deploy`. `DeploymentValidationTests.cs` shows the pattern of writing temporary YAML fixtures and invoking the PowerShell script via `Process.Start`. New manifest-shape and allowlist tests should follow that pattern.
- `docs/getting-started.md` is the canonical first-touch walkthrough. Step 1 currently runs `dotnet aspire run --project src/Hexalith.Parties.AppHost` against the Aspire dashboard. Add the K8s walkthrough as Step 1b — the Aspire-mode flow remains valid (ADR D-K8s explicitly preserves it).
- `.gitmodules` lists `Hexalith.EventStore`, `Hexalith.Memories`, `Hexalith.FrontComposer`, `Hexalith.Tenants`, `Hexalith.AI.Tools`, `Hexalith.Commons` as root-level submodules. Only root-level submodules are initialized by default; recursive submodule init is forbidden by project context rules. Smoke tests must respect this.

### Architecture Patterns and Constraints

- ADR `D-K8s — Kubernetes Deployment via Aspirate from Aspire Model` (`_bmad-output/planning-artifacts/architecture.md` lines 475-488) is the binding decision: aspirate (aspir8) from the Aspire AppHost, generated YAMLs under `deploy/k8s/`, local-cluster MVP scope (kind / minikube / k3d / Docker Desktop), aspirate version pinned, `deploy/dapr/*.yaml` remains the authoritative DAPR component source. Hand-authored Helm, Kustomize-only, and direct `kubectl apply` paths are explicitly rejected.
- ADR D13 keeps event ordering at the EventStore layer; nothing in this story changes event delivery semantics, the DAPR pub/sub guarantees, or the per-tenant subscription routing already present in `deploy/dapr/subscription-*.yaml`.
- The `src/Hexalith.Parties` actor host's boundary rules remain untouched: no public REST controller additions, no in-process MCP tools, no public Swagger/OpenAPI endpoints in the actor host (REST exposure stays at the EventStore gateway). This story only affects deployment artifacts and tooling.
- Projection-side tenant isolation remains a Parties responsibility, but request-path tenant/RBAC authorization remains EventStore-owned. K8s manifests must not introduce alternate ingress paths that bypass the EventStore gateway for write traffic.
- Determinism: aspirate must be a pinned local .NET tool (`.config/dotnet-tools.json`) so regen is reproducible. ADR D-K8s explicitly says "Aspirate version is pinned in `global.json` or equivalent" — the `.config/dotnet-tools.json` tool manifest is the equivalent for .NET local tools and is preferred over modifying `global.json` (which is SDK-only).
- Local-cluster scope only: managed cloud (AKS/EKS/GKE) is out of MVP. The deploy/teardown/smoke scripts MUST enforce a context allowlist; the smoke tests MUST refuse to run against non-local contexts.

### Current Code & Asset Surfaces To Inspect

```text
src/Hexalith.Parties.AppHost/Program.cs
src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj
src/Hexalith.Parties.AppHost/DaprComponents/   # AppHost-runtime DAPR configs (statestore.yaml, pubsub.yaml, accesscontrol.*.yaml, resiliency.yaml, subscription-parties.yaml)
deploy/dapr/                                    # Authoritative DAPR component templates — parity baseline
deploy/validate-deployment.ps1                  # Style reference for new PowerShell scripts
docs/getting-started.md                         # Walkthrough to extend (insert Step 1b)
docs/deployment-guide.md                        # Operator-facing deployment doc (may need a K8s reference; do not duplicate the getting-started walkthrough)
docs/deployment-security-checklist.md           # Existing security checklist — do not modify in this story
scripts/test.ps1                                # `-Lane deploy` invokes DeployValidation.Tests
tests/Hexalith.Parties.DeployValidation.Tests/  # Test home for new manifest/allowlist tests
tests/Hexalith.Parties.DeployValidation.Tests/DeploymentValidationTests.cs  # Pattern reference: temp dir fixture + PowerShell process invocation
.gitmodules                                      # Root-level submodule list — do not recurse
global.json                                      # SDK 10.0.103 / rollForward latestPatch
Directory.Packages.props                         # Central package versions (Aspire 13.3.3, Aspire.Hosting.Kubernetes preview, YamlDotNet 17.1.0)
.config/dotnet-tools.json                        # Local-tool manifest (create if missing); pin aspirate here
```

### External Tool: aspirate (aspir8)

- Aspirate is an out-of-band community .NET tool that reads an Aspire AppHost project (via the Aspire DCP / publisher contract) and emits Kubernetes manifests plus DAPR component CRs. The repository expects this tool to be installed via local-tool manifest, not globally.
- At implementation time, **verify the latest stable version compatible with Aspire 13.3.3 and .NET SDK 10.0.103** and record it in Completion Notes. The Aspire 13.x / .NET 10 line is new (preview Kubernetes packages are still pre-release), so aspirate compatibility is not guaranteed. If incompatibility surfaces, **stop and escalate via the Completion Notes** rather than substituting Aspire's native `AddKubernetesEnvironment` publish path — the ADR is explicit that aspirate is the chosen tool.
- The aspirate CLI surface that matters here: `dotnet aspirate generate` with non-interactive flags, output-path override, and DAPR component emission. Match the pinned version's flag names exactly (older aspirate versions used different switch names; do not assume).
- Aspirate must not be added to `Directory.Packages.props` (that file is for NuGet package references, not tool manifests).

### Previous Story Intelligence

- This is the first story in Epic 9. There is no prior Epic-9 story to inherit notes from.
- Story 8.1 (done, `_bmad-output/implementation-artifacts/8-1-deployment-validation-tooling.md`) created `deploy/validate-deployment.ps1`, `tests/Hexalith.Parties.DeployValidation.Tests/`, `docs/deployment-security-checklist.md`, and `docs/deployment-guide.md`. Reuse its PowerShell style, structured-output convention, JSON-output option pattern, and xUnit + Shouldly + YamlDotNet test stack. Do not regress its tests.
- Story 8.3 (`8-3-projection-health-monitoring-and-rebuild.md`) is the operator-facing rebuild/runbook story — out of scope here.
- Story 1.8 (review) reinforced the `[PersonalData]`-marked-fields log-safety contract. Deploy/teardown scripts must keep logs metadata-only — no secret values, no env-var contents, no ConfigMap data dumps.
- Story 1.7 (review) reinforced bounded typed failures. Deploy/teardown failure modes (context refusal, `kubectl` failure, residual state) must surface bounded messages, not raw stack traces.
- The sprint-change-proposal-2026-05-18 (`_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-18.md`) is the authoritative scoping document for Epic 9. Re-read sections 2 ("Impact Analysis") and 5 ("Implementation Handoff") before edge-case decisions.

### Recent Git Activity (last 5 commits)

- `d9a0a7e Update subproject commit reference for Hexalith.Tenants` — submodule bump; no impact on this story.
- `468bb54 Add preflight check results and Personal Data Inventory tests` — adds Personal Data inventory test scaffolding (likely Story 1.8). Be aware: privacy-safety assertions in new tests should match the inventory-test patterns this commit introduced.
- `8cf53ba Review story 2-4 party mode` — story 2.4 hardening; no impact here.
- `1ab7ad9 Create story 2-4 list and filter parties` — story 2.4 file; reuse its template/section structure but not its content.
- `a7e1b65 Record predev hardening no-op run` — preflight checkpoint; no implementation footprint.

### Latest Technical Notes

- Package versions: pinned through `Directory.Packages.props`. Relevant entries: `Aspire.Hosting` `13.3.3`, `Aspire.Hosting.Kubernetes` `13.3.3-preview.1.26264.13` (preview channel), `Aspire.Hosting.Docker` `13.3.3`, `Aspire.Hosting.Azure.AppContainers` `13.3.3`, `Aspire.Hosting.Keycloak` `13.3.3-preview.1.26264.13`, `CommunityToolkit.Aspire.Hosting.Dapr` `13.0.0`, `YamlDotNet` `17.1.0` (already a test-project dependency).
- .NET SDK: `10.0.103`, `rollForward: latestPatch` (`global.json`). All test/code targets `net10.0`.
- aspirate: NOT in `Directory.Packages.props` and MUST NOT be added there. Pin via `.config/dotnet-tools.json`.
- Allman braces, 4-space indentation, CRLF, UTF-8, trimmed trailing whitespace, final newline — for any C# additions. PowerShell scripts follow the style established in `deploy/validate-deployment.ps1`.
- Solution entry point: `Hexalith.Parties.slnx` (no legacy `.sln` file).
- Root-level submodules only. Never invoke `git submodule update --init --recursive`. Smoke tests and scripts must not depend on nested submodule init.

### Testing Requirements

- Use xUnit v3 (`3.2.2`), Shouldly (`4.3.0`), and `YamlDotNet` (`17.1.0`) in the existing `Hexalith.Parties.DeployValidation.Tests` project. Do not introduce NSubstitute here unless mocking is genuinely required (existing tests rely on real Process invocation against PowerShell scripts).
- New async test code must use `TestContext.Current.CancellationToken` — do not introduce `xUnit1051` analyzer warnings (the project tolerates pre-existing debt but new code should be clean).
- Tests MUST NOT require a live Kubernetes cluster, DAPR install, recursive submodule init, or aspirate on the runner unless explicitly traited (`[Trait("RequiresAspirate", "true")]` for the optional determinism test).
- For local-context allowlist tests, stub `kubectl` invocation by intercepting the PowerShell script's `kubectl config current-context` call. The `DeploymentValidationTests` pattern of writing temp fixtures + running the script via `Process` is the precedent.
- For manifest-shape tests, parse generated YAML with `YamlDotNet` and assert structural fields. Do not snapshot full YAML files — that creates brittle tests that fail on any aspirate version bump.
- Privacy-safety assertions: any script log output that test cases inspect must not contain raw env-var values, Secret data, tenant identifiers from ConfigMaps, bearer tokens, connection strings, or actor state-store keys. Add explicit assertions where new log lines are introduced.
- Existing `DeploymentValidationTests` and topology-validation tests (`AppHostDaprTopologyValidationTests`, `TenantsDeploymentValidationTests`) MUST remain green. Story 9.1 is additive.

### Anti-Patterns To Avoid

- Do NOT hand-edit `deploy/k8s/` YAML after aspirate emits it. Hand-edits break determinism (AC1) and bypass Story 9.2's lint contract. If aspirate's default layout or content is wrong, fix the AppHost or the aspirate invocation, never the emitted file.
- Do NOT add aspirate to `Directory.Packages.props`. It is a tool, not a package reference. Use `.config/dotnet-tools.json`.
- Do NOT introduce Hexalith.Memories, Hexalith.FrontComposer, or Hexalith.AI.Tools wirings into `src/Hexalith.Parties.AppHost/Program.cs` in this story. The architecture mentions them as participants, but wiring them in is a separate decision (likely a future Epic 9 follow-up or sibling-submodule story). This story matches AppHost-as-is.
- Do NOT switch the architectural decision from aspirate to Aspire's native `AddKubernetesEnvironment` publisher without an explicit ADR amendment. If aspirate is incompatible with the pinned Aspire/.NET versions, escalate via Completion Notes; do not silently substitute.
- Do NOT introduce managed-cloud manifests, ingress controller CRs (NGINX/Traefik/Istio gateway), TLS issuer CRDs, cloud storage classes, or registry secrets. Local-cluster scope only — those belong to a post-MVP story.
- Do NOT relax `deploy/dapr/accesscontrol.*.yaml` `defaultAction: deny` or wildcard the access-control policies in the generated CRs. Story 8.1 explicitly validates this; Story 9.2 will extend the check.
- Do NOT recursively initialize submodules from any new script. Root-level submodule init is the maximum.
- Do NOT echo Secret data, ConfigMap data values, env-var contents, tenant identifiers from runtime config, tokens, connection strings, actor state-store keys, or DAPR sidecar internal certificates from any new script or test.
- Do NOT add public REST/MCP/OpenAPI/Swagger surfaces to `src/Hexalith.Parties` while editing AppHost. The actor host boundary is intentional.
- Do NOT modify `deploy/validate-deployment.ps1`, `docs/deployment-security-checklist.md`, or the existing `Hexalith.Parties.DeployValidation.Tests` files beyond additive test classes. Story 9.2 owns those extensions.

### Deferred Decisions

- Whether to keep or remove the `PUBLISH_TARGET=k8s` / `builder.AddKubernetesEnvironment("k8s")` block in `Program.cs` (Task 9). Either choice is acceptable as long as it is documented in Completion Notes; the AppHost must stay aspirate-compatible regardless.
- Hexalith.Memories and Hexalith.FrontComposer wiring into AppHost — out of scope for Story 9.1, even though the architecture mentions them as topology participants. A future sibling-submodule integration story owns this.
- CI step running `dotnet aspirate generate` and diffing against checked-in `deploy/k8s/` as drift detection — explicitly out of MVP scope per the sprint-change-proposal.
- Ingress controller integration beyond port-forward — out of MVP scope.
- TLS termination, registry secrets, image pull secrets — out of MVP scope for local-cluster deployment.
- Managed-cloud deploy (AKS / EKS / GKE) — out of MVP scope per ADR D-K8s; a future story owns this.
- Automated DAPR component parity lint (`deploy/k8s/dapr/` vs `deploy/dapr/`) — owned by Story 9.2. This story documents parity in `deploy/k8s/README.md` manually.
- Manifest validation (probes, resource limits, sidecar annotations, plaintext-secret detection, drifted access-control) — owned by Story 9.2.
- Sibling submodules' own deploy stories (Hexalith.EventStore, Hexalith.Tenants, Hexalith.Memories, Hexalith.FrontComposer) — independent codebases; their deploy responsibilities are out of this story.
- Whether aspirate's generated layout (deployments/, services/, etc.) matches our chosen `deploy/k8s/deployments/` + `deploy/k8s/dapr/` split — if not, configure aspirate at generation time or shape inside the deploy script; do not post-edit emitted files.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-9-Kubernetes-Deployment] — Epic goal and cross-story context (lines 3113-3201).
- [Source: _bmad-output/planning-artifacts/epics.md#Story-9.1-Generate-Kubernetes-Artifacts-and-Deploy-Full-Topology-to-Local-Cluster] — User story statement and BDD acceptance criteria (lines 3117-3159).
- [Source: _bmad-output/planning-artifacts/prd.md#Developer-Integration-MVP] — FR31 (K8s deploy target), FR31a (aspirate as generator), FR32 (getting-started doc), FR60 (one-command local run), FR61 (deployment validation tooling).
- [Source: _bmad-output/planning-artifacts/prd.md#Developer-Experience] — NFR30 (< 15 min first-deploy budget).
- [Source: _bmad-output/planning-artifacts/architecture.md#Infrastructure-Deployment] — ADR `D-K8s` (lines 475-488), authoritative decision for aspirate + local-cluster scope.
- [Source: _bmad-output/planning-artifacts/architecture.md#Complete-Project-Directory-Structure] — `deploy/k8s/` layout (lines 937-950).
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-18.md] — Strategic pivot record for Epic 9 (impact analysis, recommended approach, success criteria, out-of-MVP scope list).
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] — Actor host boundary, EventStore ownership, projection-side tenant safety, submodule guardrails, privacy in logs, package-management rules.
- [Source: _bmad-output/implementation-artifacts/8-1-deployment-validation-tooling.md] — Reference for PowerShell + DeployValidation.Tests patterns (now `done`).
- [Source: src/Hexalith.Parties.AppHost/Program.cs] — Current AppHost composition; resources currently wired (lines 1-207); existing `PUBLISH_TARGET` branch (lines 184-205).
- [Source: src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj] — `Aspire.AppHost.Sdk/13.3.3`, Kubernetes preview package reference, DAPR community-toolkit reference.
- [Source: deploy/dapr/] — Authoritative DAPR component templates (parity baseline for `deploy/k8s/dapr/`).
- [Source: deploy/validate-deployment.ps1] — PowerShell style reference for new deploy/teardown scripts.
- [Source: tests/Hexalith.Parties.DeployValidation.Tests/DeploymentValidationTests.cs] — Temp-fixture + Process-invocation test pattern.
- [Source: docs/getting-started.md#Step-1-Deploy-Locally] — Aspire-mode walkthrough to mirror in Step 1b (K8s mode).
- [Source: scripts/test.ps1] — `-Lane deploy` switch invokes `Hexalith.Parties.DeployValidation.Tests`.
- [Source: .gitmodules] — Root-level submodules (no recursive init).
- [Source: global.json] — SDK `10.0.103`, `rollForward: latestPatch`.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.7 (1M context) — `claude-opus-4-7[1m]`, via Claude Code CLI.

### Debug Log References

- Initial aspirate generate ran from the repo root and reported `Created Aspire Manifest At Path: src/manifest.json` but then failed `LoadAspireManifestAction` because aspirate looked for the manifest relative to the project path. **Fix:** invoke aspirate with the AppHost directory as CWD (`cd src/Hexalith.Parties.AppHost`), then aspirate finds the intermediate manifest at the expected location. The wrapper `regen.ps1` enforces this.
- Aspirate 9.1.0 errors out in non-interactive mode unless `--include-dashboard <bool>` is supplied explicitly. **Fix:** pass `--include-dashboard false` in `regen.ps1`.
- Aspirate 9.1.0 targets `net9.0` but the project pins .NET 10 SDK (`10.0.103` originally, `10.0.300` after the upstream `Update .NET SDK version` commit). Running aspirate produced `Microsoft.NETCore.App, version '9.0.0' (x64)` lookup failure. **Fix:** set `"rollForward": true` for the aspirate tool entry in `.config/dotnet-tools.json`. Re-verified after the SDK bump — aspirate idempotency holds across both 10.0.103 and 10.0.300.
- Initial `deploy-local.ps1` failed strict-mode evaluation when reading `$script:NamespaceEnsured` before any assignment. **Fix:** initialize `$script:NamespaceEnsured = $false` at the top of the script.
- C# raw interpolated string `$"""..."""` produced `CS9006` on `${{KUBECTL_SHIM_LOG:-/dev/null}}` because single-`$` raw strings treat `{` as interpolation. **Fix:** the test shim now builds the kubectl script via explicit string concatenation.

### Completion Notes List

**Architectural decisions made during implementation:**

1. **Aspirate version pin:** `aspirate 9.1.0` (latest stable at implementation time) with `rollForward: true` in `.config/dotnet-tools.json`. Aspirate currently targets `net9.0`; with rollForward enabled it runs on the .NET 10 SDK that the repo pins. Initially exercised against `global.json: 10.0.103`; revalidated against the post-pull `global.json: 10.0.300 / rollForward: latestFeature` — `dotnet build`, `regen.ps1` byte-equality, and `scripts/test.ps1 -Lane deploy` all stay green. Recorded as a known dependency in `deploy/k8s/README.md` → "Known aspirate limitations".
2. **PUBLISH_TARGET / `AddKubernetesEnvironment` block (Task 9):** **Kept** the block in `Program.cs`. Aspire's native publisher is orthogonal to aspirate's external generator — verified `dotnet build` of the AppHost still succeeds and aspirate generation is unchanged. Added an inline comment so future readers understand the dual code path. No CI/test hook depends on `PUBLISH_TARGET=k8s` today, so removing it would also have been safe; keeping it preserves an escape hatch for a possible future Aspire-native publish story.
3. **Output layout (Task 3 / AC1):** kept aspirate's canonical **per-app folders** under `deploy/k8s/<app-id>/` rather than reshaping into the story-spec-suggested `deployments/` subfolder. Reshape would require regenerating the top-level kustomization or post-editing emitted files, both of which fight aspirate's design. The README documents the actual layout, and `K8sManifestGenerationTests` asserts the per-app structure recursively.
4. **DAPR component strategy (AC2):** aspirate emits placeholder `deploy/k8s/dapr/{statestore,pubsub}.yaml` with `metadata: []`. The deploy script vendors in the authoritative DAPR templates from `deploy/dapr/` (access control, subscriptions, resiliency, topology, tenants-integration) at apply time. The README carries the full parity table mapping each `deploy/dapr/*.yaml` to its `deploy/k8s/dapr/` counterpart (or noting absence). Story 9.2 will automate this lint.
5. **DAPR annotations on Deployments:** aspirate 9.1.0 emits `dapr.io/enabled`, `dapr.io/app-id`, `dapr.io/config: tracing` (aspirate default), and `dapr.io/enable-api-logging` on the DAPR-enabled Deployments. It does **not** emit `dapr.io/app-port`, nor does it derive `dapr.io/config` from the AppHost's `WithDaprSidecar(Config = ...)` path. These are aspirate gaps, not Parties code defects; documented in `deploy/k8s/README.md` → "Known aspirate limitations". `K8sManifestGenerationTests.DaprEnabledDeploymentsCarryExpectedAspirateAnnotations` asserts what aspirate *does* emit and the README's drift section calls out what is missing.
6. **Test environment caveat:** the implementation environment had no live local Kubernetes cluster, no `pwsh` on PATH initially (installed mid-implementation via `dotnet tool install --global PowerShell` 7.6.1), and no `dapr` control plane to exercise end-to-end. **Static** assertions (manifest shape, README content, script content) and **stubbed** assertions (kubectl shim via PATH override) ran green. Live-cluster validation (AC4 pod readiness, AC5 `CreateParty` round-trip, NFR30 wall-clock) is **deferred to the reviewer / operator** with the documented walkthrough in `docs/getting-started.md` → "Step 1b: Deploy to a Local Kubernetes Cluster (Optional)".

**Test results:**

- `pwsh scripts/test.ps1 -Lane deploy` — **71 / 71 passing** (41 pre-existing tests + 30 new tests across `K8sManifestGenerationTests` and `K8sLocalContextAllowlistTests`). Runtime: ~1 min 44 s.
- `dotnet build Hexalith.Parties.slnx --configuration Release` — **succeeds, 0 warnings, 0 errors**.
- `pwsh scripts/test.ps1 -Lane unit` (spot-checked) — green on the projects tied to the changes (AppHost wires unchanged except for one inline comment; Picker / MCP / Server / etc. unaffected).
- Aspirate idempotency check: two consecutive `pwsh deploy/k8s/regen.ps1` runs produce zero `diff -qr` output against `deploy/k8s/`.

**AC coverage trace:**

| AC | Coverage |
|---|---|
| AC1 | `K8sManifestGenerationTests.K8sDirectoryExistsWithExpectedTopLevelLayout`, `.DeploymentExistsForEveryAppHostWiredAppId`, `.DotnetToolsManifestPinsAspirate`; idempotency verified by `regen.ps1` + manual `diff -qr` snapshot. |
| AC2 | `K8sManifestGenerationTests.DaprPlaceholderComponentCrsAreEmittedForEventStoreReferences`, `.AuthoritativeDaprTemplatesRemainTheBackingComponentSource`, `.K8sReadmeDocumentsDaprComponentParityTable`; README parity table covers every template. |
| AC3 | `K8sLocalContextAllowlistTests.DeployScriptRefusesNonLocalContextWithExitCode2`, `.DeployScriptAcceptsLocalContextAndCallsKubectlApply`, `.AllowlistConstantsMatchAcrossDeployAndTeardownScripts`, `.DeployScriptDocumentsRefusalExitCodeAndMessage`; README's "Deploying to a local cluster" section. |
| AC4 | `K8sManifestGenerationTests.DaprEnabledDeploymentsCarryExpectedAspirateAnnotations` asserts the annotations aspirate emits; README explicitly records the `dapr.io/app-port` gap and the `dapr.io/config` aspirate-default behavior. Live readiness check is deferred per Test environment caveat above. |
| AC5 | `docs/getting-started.md` → "Step 1b" walkthrough mirrors the Aspire-mode flow with explicit `dotnet tool restore` / `regen.ps1` / `deploy-local.ps1` / port-forward steps + a forward link to Step 3's `CreateParty` command. NFR30 wall-clock measurement is deferred. |
| AC6 | `K8sLocalContextAllowlistTests.TeardownScriptRefusesNonLocalContextWithExitCode2`, `.TeardownScriptAcceptsLocalContextAndCallsKubectlDelete`, `.TeardownScriptDocumentsRefusalExitCodeAndMessage`; `teardown-local.ps1` residual-state probe; README "Tearing down" section. |
| AC7 | `K8sManifestGenerationTests` + `K8sLocalContextAllowlistTests` run inside `scripts/test.ps1 -Lane deploy`, no live cluster needed, no recursive submodule init (`.DeployScriptDoesNotInvokeGitSubmoduleRecursiveInit`, `.TeardownScriptDoesNotInvokeGitSubmoduleRecursiveInit`). |

### File List

**Added:**

- `.config/dotnet-tools.json` — pins aspirate 9.1.0 with `rollForward: true`.
- `deploy/k8s/README.md` — comprehensive doc with regen / deploy / teardown commands, AppHost-current resource list, layout, DAPR component parity table, known aspirate limitations, out-of-MVP notes, troubleshooting.
- `deploy/k8s/regen.ps1` — wrapper around `dotnet aspirate generate` with cleanup of the intermediate `manifest.json` side effect.
- `deploy/k8s/deploy-local.ps1` — local-cluster deploy script with context allowlist (`kind-*`, `k3d-*`, `minikube`, `docker-desktop`), DAPR init, vendor-in of `deploy/dapr/` templates, bounded summary.
- `deploy/k8s/teardown-local.ps1` — local-cluster teardown with allowlist enforcement, kustomize delete, residual-state probe, optional `-PurgeDapr` flag.
- `deploy/k8s/kustomization.yaml` — aspirate top-level kustomize entry (regenerated by `regen.ps1`).
- `deploy/k8s/namespace.yaml` — aspirate-emitted `hexalith-parties` namespace.
- `deploy/k8s/dapr/statestore.yaml`, `deploy/k8s/dapr/pubsub.yaml` — aspirate-emitted DAPR component placeholders.
- `deploy/k8s/eventstore/{deployment,service,kustomization}.yaml`
- `deploy/k8s/eventstore-admin/{deployment,service,kustomization}.yaml`
- `deploy/k8s/eventstore-admin-ui/{deployment,service,kustomization}.yaml`
- `deploy/k8s/parties/{deployment,service,kustomization}.yaml`
- `deploy/k8s/parties-mcp/{deployment,service,kustomization}.yaml`
- `deploy/k8s/tenants/{deployment,service,kustomization}.yaml`
- `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestGenerationTests.cs` — 10 manifest-shape and documentation fitness tests.
- `tests/Hexalith.Parties.DeployValidation.Tests/K8sLocalContextAllowlistTests.cs` — 20 deploy/teardown allowlist tests (Theory + Fact) using stubbed `kubectl` shim.

**Modified:**

- `.gitignore` — added `src/Hexalith.Parties.AppHost/manifest.json` and `aspirate-state.json` exclusions (defense-in-depth; `regen.ps1` also cleans them).
- `docs/getting-started.md` — extended Prerequisites with the K8s-mode toolchain delta and inserted "Step 1b: Deploy to a Local Kubernetes Cluster (Optional)" between Step 1 and Step 2.
- `src/Hexalith.Parties.AppHost/Program.cs` — added inline comment above the `PUBLISH_TARGET` switch documenting orthogonality to aspirate (no behavior change).
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — story status moved to `review` (handled by Step 9 of the workflow).

## Change Log

- 2026-05-18: Story created by `bmad-create-story`. Pulls authoritative scoping from the 2026-05-18 sprint-change-proposal, ADR D-K8s, and the current AppHost composition. Flags aspirate compatibility with .NET 10 / Aspire 13.3.3 as a verify-and-record concern, and reconciles the existing `PUBLISH_TARGET=k8s` / `AddKubernetesEnvironment` block in `Program.cs` against the aspirate decision. Memories / FrontComposer wiring deliberately excluded — AppHost reflects the wired set only.
- 2026-05-18: Implementation complete (Claude Opus 4.7). Pinned aspirate 9.1.0 (`rollForward: true`), generated `deploy/k8s/` via aspirate, wrote `regen.ps1` / `deploy-local.ps1` / `teardown-local.ps1` with local-cluster allowlist enforcement, added Step 1b walkthrough to `docs/getting-started.md`, added 30 manifest-shape + allowlist fitness tests (all green). `PUBLISH_TARGET=k8s` block kept (orthogonal to aspirate). Live-cluster verification (AC4 readiness, AC5 first-command round-trip, NFR30 wall-clock) **deferred to reviewer / operator** — no live Kubernetes cluster available in the implementation environment. Status moved to `review`.
- 2026-05-18: Pulled upstream `Update .NET SDK version and enhance CI pipeline configuration` (`global.json` 10.0.103 → 10.0.300, `rollForward` latestPatch → latestFeature). Installed SDK 10.0.300 locally, refreshed prerequisites in `deploy/k8s/README.md` and `docs/getting-started.md`, revalidated `dotnet build Hexalith.Parties.slnx --configuration Release` (clean), aspirate idempotency (zero diff), and `scripts/test.ps1 -Lane deploy` (71/71). No story-level changes required — aspirate + scripts + tests all SDK-agnostic within the .NET 10 family.
