# Story 9.2: Aspire AppHost → Aspirate Manifest Composition

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer maintaining the deployment topology,
I want the Aspire AppHost (`src/Hexalith.Parties.AppHost/Program.cs`) to be the single source of truth for the composable Hexalith services, with `dotnet aspirate generate` emitting deterministic per-service Kubernetes manifests under `deploy/k8s/<app-id>/` stamped with the MinVer-resolved image tag,
so that adding a service, changing a Dapr app-id, or bumping a version is a one-file edit in the AppHost — never a hand-edit in `deploy/k8s/`.

## Scope & Non-Scope

**This story delivers** (AppHost refactor + csproj container-publish opt-in + aspirate emission baseline + cleanup + kustomization + 3 patch contracts):

1. `src/Hexalith.Parties.AppHost/Program.cs` refactor: remove `redis` + `keycloak` from publish-mode composition; gate Aspire-only wiring behind `builder.ExecutionContext.IsPublishMode`; replace `keycloak.GetEndpoint("http")` resource expressions in publish mode with the hand-authored carve-out's in-cluster DNS pattern (`http://keycloak.hexalith-parties.svc.cluster.local:8080/realms/hexalith`).
1a. **Container publish opt-in** on 3 project csprojs (party-mode review finding F11 — current state: only 4 of the 7 services have `<EnableContainer>true</EnableContainer>` + `<ContainerRepository>` in their csproj; aspirate **cannot** emit `registry.hexalith.com/<app-id>:<MinVer>` for csprojs that haven't opted in, so AC4 is unsatisfiable without this edit):
    - `src/Hexalith.Parties/Hexalith.Parties.csproj` — add `<EnableContainer>true</EnableContainer>` + `<ContainerRepository>parties</ContainerRepository>`.
    - `src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj` — add `<EnableContainer>true</EnableContainer>` + `<ContainerRepository>parties-mcp</ContainerRepository>`.
    - `Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj` — add `<EnableContainer>true</EnableContainer>` + `<ContainerRepository>memories</ContainerRepository>`. **This is a cross-submodule edit** — committed in the `Hexalith.Memories` submodule first (one PR there per Conventional Commits convention: `feat(server): add EnableContainer + ContainerRepository for K8s emission`), then the submodule pointer is bumped in this repo. The "no nested submodules" project rule does NOT apply here — `Hexalith.Memories` is a root-level sibling submodule, and editing it for legitimate cross-repo coordination is explicitly allowed.
    - The 4 services already opted-in (verified in `Hexalith.EventStore/CLAUDE.md` containers table + `Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj`) are: `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `tenants`. No edit needed there.
2. `deploy/k8s/namespace.yaml` — top-level `Namespace/hexalith-parties` manifest (referenced by `kustomization.yaml`).
3. `deploy/k8s/kustomization.yaml` — top-level Kustomization wiring the 7 aspirate-emitted per-service folders ONLY. The `redis/` + `keycloak/` carve-out entries are **commented out** at this story's close (Story 9.3 uncomments them when those folders land) — see AC8 + Subtask 3.2/3.3 for the revised contract (resolves party-mode review F13 three-way contradiction).
4. `deploy/k8s/<app-id>/` per-service folders for the 7 application services, **emitted by `dotnet aspirate generate`**, NOT hand-authored. Each folder contains `deployment.yaml` + `service.yaml` (+ `configmap.yaml` where applicable). Folders: `eventstore/`, `eventstore-admin/`, `eventstore-admin-ui/`, `parties/`, `parties-mcp/`, `tenants/`, `memories/`.
5. **3 patch contracts** documented as the post-aspirate transform that Story 9.5's `publish.ps1` will mechanize:
   - **Dapr annotations patch** (AC4): inject `dapr.io/enabled`, `dapr.io/app-id`, `dapr.io/app-port`, `dapr.io/config` on the 5 Dapr-equipped Deployments; skip non-Dapr workloads.
   - **JWT `secretKeyRef` patch** (forward-ref to Story 9.5 step 6 — owned there; this story documents the container-name anchor and requires the patch to create or upsert the `Authentication__JwtBearer__SigningKey` env entry when aspirate emits only `envFrom`).
   - **`imagePullSecrets` patch** (AC5): inject `imagePullSecrets: [{ name: zot-pull-secret }]` on every Deployment whose image starts with `registry.hexalith.com/`; vendor images (`redis:*`, `quay.io/keycloak/keycloak:*`) skipped.
6. Aspirate-placeholder-strip list (AC3): enumerate the orphan files aspirate emits (`aspirate-readme.md`, `azure.bicep`, sample-only YAMLs) that `publish.ps1` step 4 deletes.
7. **One-shot aspirate emission baseline** committed to `deploy/k8s/<app-id>/*` so reviewers + downstream stories (9.3 carve-outs, 9.5 patch contracts, 9.6 lint tooling, 9.7 fitness tests) have a checked-in artefact to diff against. The baseline must satisfy: byte-stable image-tag-pinned `image:` line, deterministic `<MinVer>` shape regex, no `:latest`, no empty tag.
8. `.config/dotnet-tools.json` aspirate-version pin **verified** at `9.1.0` (or the current latest stable at story execution captured as the new pinned baseline). The AppHost SDK is verified at `Aspire.AppHost.Sdk/13.3.3` (current value in `Hexalith.Parties.AppHost.csproj` line 1).

**This story does NOT deliver** (forward-referenced to later v2 stories):

| Out-of-scope | Owned by |
|---|---|
| `deploy/k8s/redis/` + `deploy/k8s/keycloak/` hand-authored carve-out manifests | Story 9.3 |
| `deploy/dapr/*.yaml` Dapr CRs (3 Components + 5 ACL configs + 2 Subscriptions + resiliency) | Story 9.4 |
| `deploy/k8s/publish.ps1` + `deploy/k8s/teardown.ps1` + `deploy/k8s/_lib/Confirm-KubeContext.ps1` | Story 9.5 |
| The patch *implementation* in PowerShell (this story documents anchors + contracts; Story 9.5 mechanizes them) | Story 9.5 |
| `deploy/validate-deployment.ps1` lint tooling | Story 9.6 |
| `tests/Hexalith.Parties.DeployValidation.Tests/*.cs` fitness suite (incl. `KustomizationTopologyFitnessTest`, `AspirateEmissionByteDeterminismFitnessTest`, `DaprAnnotationPatchIdempotencyFitnessTest`, `ImagePullSecretsPatchScopeFitnessTest`) | Story 9.7 |
| Operator-managed Secrets bootstrap (`hexalith-jwt-signing`, `hexalith-keycloak-admin`, `zot-pull-secret`) | Story 9.5 |
| Image build + push to Zot (the actual `dotnet aspirate generate --container-image-tag <minver> --container-registry registry.hexalith.com` invocation that BUILDS images) | Story 9.5 (publish step 3) — see Investigation Item I3 below |

> **DECISION (formerly I1):** This story's aspirate emission uses `--skip-build` exclusively. The committed baseline is manifest-shape only — Story 9.5 drops `--skip-build` to build + push images. Aspirate 9.1.0's CLI surface was verified at story-spec patch time (party-mode review F1) via `dotnet aspirate generate --help` — confirmed flag list, see Dev Notes "Aspirate baseline (verified)" + the corrected invocation contract in AC9.

## Acceptance Criteria

### AC1 — Aspire AppHost composes exactly the 7 application services in publish mode

- **Given** the Aspire AppHost resource graph at `src/Hexalith.Parties.AppHost/Program.cs`,
- **When** the graph is inspected after a clean build under publish mode (`builder.ExecutionContext.IsPublishMode == true`),
- **Then** it composes exactly the following composable workloads with the listed app-ids:
  - `eventstore` (project resource, Dapr sidecar enabled, app-id `eventstore`, config `accesscontrol.yaml`)
  - `eventstore-admin` (project resource, Dapr sidecar enabled, app-id `eventstore-admin`, config `accesscontrol.eventstore-admin.yaml`)
  - `eventstore-admin-ui` (project resource, **no** Dapr sidecar)
  - `parties` (project resource, Dapr sidecar enabled, app-id `parties`, config `accesscontrol.parties.yaml`)
  - `parties-mcp` (project resource, **no** Dapr sidecar)
  - `tenants` (project resource, Dapr sidecar enabled, app-id `tenants`, config `accesscontrol.tenants.yaml`)
  - `memories` (project resource, Dapr sidecar enabled, app-id `memories`, config `accesscontrol.memories.yaml`)
- **And** the hand-authored carve-outs (`redis`, `keycloak`) are **NOT defined in the AppHost in publish mode** — both `builder.AddRedis(...)` and `builder.AddKeycloak(...)` invocations are gated by `if (!builder.ExecutionContext.IsPublishMode)` so that `dotnet aspirate generate` does NOT emit them (Story 9.3 owns these as hand-authored manifests under `deploy/k8s/{redis,keycloak}/`).
- **And** services requiring Dapr (`eventstore`, `eventstore-admin`, `parties`, `tenants`, `memories`) keep the existing `WithDaprSidecar(...)` configuration with a stable `AppId` matching the K8s folder name and the `Config` path pointing at the matching `DaprComponents/accesscontrol.<service>.yaml` file.
- **And** non-Dapr workloads (`eventstore-admin-ui`, `parties-mcp`) remain without `WithDaprSidecar(...)` — the post-aspirate Dapr-annotation patch (AC4) will skip them by manifest-level lookup.
- **And** the `parties-mcp` resource keeps `.WithReference(eventStore)` and `.WithReference(parties)` and `.WithEnvironment("Parties__Mcp__EventStoreGatewayBaseUrl", ...)` — these reference expressions resolve to in-cluster DNS in publish mode automatically via Aspire's resource graph.
- **And** the existing `PUBLISH_TARGET` block at the bottom of `Program.cs` (lines ~218–244 — `if (publishTarget == "k8s") builder.AddKubernetesEnvironment("k8s")`) is **preserved unchanged** — it is orthogonal to the aspirate path and gates Aspire-native `dotnet aspire publish` only. The comment block above it remains accurate.
- **And** (foot-gun warning — party-mode review F20): the dev MUST NOT export `PUBLISH_TARGET=k8s` in the shell when running `dotnet aspirate generate`. The two K8s emission paths (Aspire-native `AddKubernetesEnvironment` and aspirate-from-manifest) are orthogonal in principle, but exporting `PUBLISH_TARGET=k8s` activates the Aspire-native publisher pipeline during aspirate's publish-mode build, which may shift env-var resolution or emit additional resources aspirate then mis-composes. Subtask 4.0.5 asserts `PUBLISH_TARGET` is unset before Subtask 4.2 fires.

### AC2 — Run-mode composition preserves local Aspire dev (Redis + Keycloak as composable resources)

- **Given** the Aspire AppHost is invoked via `dotnet run` / `dotnet aspire run` in run mode (`builder.ExecutionContext.IsRunMode == true`),
- **When** the graph is inspected at startup,
- **Then** `redis` (via `builder.AddRedis("redis")`) and `keycloak` (via `builder.AddKeycloak("keycloak", 8180).WithRealmImport("./KeycloakRealms")`) ARE composed as Aspire resources, giving the local dev environment a working backing store and OIDC issuer without manual Docker invocations.
- **And** the existing `.WithReference(keycloak)` / `.WaitFor(keycloak)` / `.WithReference(redis)` (transitively via `eventStoreResources.StateStore` + `eventStoreResources.PubSub`) chains on `parties`, `eventstore`, `eventstore-admin`, `tenants`, `memories`, `adminUI`, `partiesMcp` continue to work — the run-mode graph is equivalent to today's behavior.
- **And** the `Authentication__JwtBearer__Authority`/`Issuer`/`Audience`/`TokenValidationParameters__*` env vars currently wired via `realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/hexalith")` continue to point at the Aspire-resolved keycloak endpoint during run mode (no breakage of local OIDC).
- **And** publish-mode equivalents (see AC3) replace these `realmUrl` references with hardcoded in-cluster DNS so aspirate emission produces resolvable manifest values.

### AC3 — Publish-mode env-var wiring replaces resource expressions with in-cluster DNS

- **Given** Aspire's `ReferenceExpression.Create($"{keycloakEndpoint}/realms/hexalith")` resolves to `keycloak.GetEndpoint("http")` which is **dev-mode-only** (resolves to a localhost-shaped URL),
- **When** the AppHost runs in publish mode (`IsPublishMode == true`) — i.e., when `dotnet aspirate generate` consumes the manifest,
- **Then** the JWT authority/issuer env vars on `eventstore`, `eventstore-admin`, `parties`, `partiesMcp`, `tenants`, `adminUI` are wired to the constant in-cluster DNS string `http://keycloak.hexalith-parties.svc.cluster.local:8080/realms/hexalith` (matches Story 9.3 AC4 documented OIDC URL pattern for the `keycloak` carve-out).
- **And** the Aspire `realmUrl` ReferenceExpression is only used in the run-mode branch.
- **And** in publish mode, no `.WithReference(keycloak)` / `.WaitFor(keycloak)` calls are issued (because keycloak is not composed in publish mode — AC1).
- **And** the K8s in-cluster DNS string is centralised in a single `const string` at the top of `Program.cs` with a **mechanical anchor comment** (party-mode review F18 + I2 improvement):
  ```csharp
  // PUBLISH-MODE-DNS-ANCHOR — Story 9.2 publish-mode wiring; matches Story 9.3 keycloak Service shape
  // (Service name `keycloak`, namespace `hexalith-parties`, port 8080, realm `hexalith`).
  // If Story 9.3 changes the keycloak Service name/port/namespace OR the realm name, THIS CONSTANT MUST UPDATE.
  // AC11 row C10 grep-asserts this anchor remains in place. Story 9.3 Definition of Done MUST cross-verify.
  const string KeycloakRealmUrlInCluster = "http://keycloak.hexalith-parties.svc.cluster.local:8080/realms/hexalith";
  ```
- **And** the `adminUI` `EventStore__Authentication__Authority` env var is wired in publish mode to `KeycloakRealmUrlInCluster` (decision, not investigation — adminUI MUST authenticate against the in-cluster Keycloak in publish mode; running unauthenticated would be a security regression vs. local Aspire dev). The run-mode-with-`EnableKeycloak=false` branch retains today's empty-string clear semantics — see Subtask 2.7 for the 3-state wiring contract.

### AC4 — Aspirate-emitted per-service folders + image-tag determinism

- **Given** the operator runs the full AC9 invocation contract (`DOTNET_ROLL_FORWARD=Major ContainerImageTags=<MinVer> dotnet tool run --allow-roll-forward aspirate -- generate ...`) with `--skip-build` for this story (Story 9.5's `publish.ps1` step 3 will drop `--skip-build` to build + push, but **this story uses `--skip-build`** per Investigation Item I1 + Recommendation),
- **When** generation completes,
- **Then** the following per-service folders exist under `deploy/k8s/`: `eventstore/`, `eventstore-admin/`, `eventstore-admin-ui/`, `parties/`, `parties-mcp/`, `tenants/`, `memories/` — **exactly these 7 folders**, no more, no fewer (no `redis/`, no `keycloak/` — those are Story 9.3).
- **And** each folder contains at minimum `deployment.yaml` and `service.yaml`; `configmap.yaml` is present where Aspire emits one (typically when env vars are wired via `WithEnvironment`).
- **And** every `deployment.yaml` carries an image reference of shape `registry.hexalith.com/<app-id>:<MinVer>` — never `:latest`, never empty, never a different registry.
- **And** the `<MinVer>` segment matches the regex `^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$` (allows preview suffixes like `-preview.0.353` but NOT `+dirty` build metadata — `+dirty` is a Story 9.5 `publish.ps1` warn-and-proceed concern; this story's committed baseline must be from a clean tree).
- **And** the dev verifies the regex via:
  ```bash
  grep -rE 'image:\s+registry\.hexalith\.com/[A-Za-z0-9._-]+:[^[:space:]]+' deploy/k8s/{eventstore,eventstore-admin,eventstore-admin-ui,parties,parties-mcp,tenants,memories}/deployment.yaml \
    | awk -F'registry.hexalith.com/[A-Za-z0-9._-]+:' '{print $2}' \
    | grep -vE '^[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.-]+)?$'
  ```
  Expected output: **zero lines** (every image tag matches the regex). The repo-name character class is `[A-Za-z0-9._-]` per OCI spec, not lowercase-only (party-mode review R8 — defensive against a future capital-letter repo name).
- **And** no `:latest`, `:staging-latest`, empty tag, or `+dirty` build-metadata appears anywhere under `deploy/k8s/` (party-mode review F5 + F16 — fix for the original empty-tag regex that missed the bare-EOL case):
  ```bash
  # Mutable tags:
  grep -rE 'registry\.hexalith\.com/[A-Za-z0-9._/-]+:(latest|staging-latest)\b' deploy/k8s/
  # Empty tag (registry+repo+colon followed by optional whitespace then EOL):
  grep -rEn 'registry\.hexalith\.com/[A-Za-z0-9._/-]+:[[:space:]]*$' deploy/k8s/
  # +dirty build metadata:
  grep -rEn ':[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.-]+)?\+' deploy/k8s/
  ```
  Expected: **zero lines** for all three.

### AC5 — Post-aspirate Dapr-annotation patch contract (documented anchor; mechanized by Story 9.5)

- **Given** Aspirate emits per-service Deployments WITHOUT injecting a `daprd` sidecar container into the pod (the Dapr operator injects `daprd` at admission time in the cluster, NOT at manifest-generate time — party-mode review F9),
- **When** the post-aspirate Dapr-annotation patch runs (Story 9.5 `publish.ps1` step 5),
- **Then** every Dapr-equipped Deployment carries this annotation block on `spec.template.metadata.annotations`:
  ```yaml
  dapr.io/enabled: "true"
  dapr.io/app-id: <service>            # eventstore | eventstore-admin | parties | tenants | memories
  dapr.io/app-port: "8080"
  dapr.io/config: <config-name>        # accesscontrol | accesscontrol-eventstore-admin | accesscontrol-parties | accesscontrol-tenants | accesscontrol-memories
  ```
- **And** the patch identifies Dapr-equipped Deployments via a **name allowlist scoped to the 5 known Dapr-equipped services** — `{eventstore, eventstore-admin, parties, tenants, memories}` — derived mechanically from `Program.cs` `WithDaprSidecar(...)` callsites (party-mode review F9: the original "detect-by-container" rule was infeasible because aspirate never emits a daprd container). The allowlist lives in `publish.ps1` Story 9.5 as a single source; this story documents the expected values. If the AppHost adds or removes a `WithDaprSidecar` callsite, `publish.ps1`'s allowlist MUST update — Story 9.7's `DaprAnnotationPatchIdempotencyFitnessTest` asserts that the publish.ps1 allowlist matches the `Program.cs` callsites verbatim (fitness-test handoff).
- **And** the patch is **upsert-safe**, not skip-on-anchor (party-mode review F9 partial-state failure mode): the patch unconditionally sets all four annotations to their expected values, every run. Re-running on already-patched YAML produces zero diff (upsert with value-equality), and re-running on partial-state YAML (one prior run interrupted mid-annotation) repairs the state. Do NOT use `dapr.io/enabled: "true"` presence as a "skip this Deployment" anchor — that was the broken design.
- **And** the patch never injects Dapr annotations into non-Dapr workloads. The skip-list (`{eventstore-admin-ui, parties-mcp, redis, keycloak}`) is the complement of the Dapr name allowlist within the per-service folder set.
- **And** the dev verifies the post-aspirate emission baseline contains the **pod-template anchor** the patch needs to extend:
  ```bash
  # Every Deployment (Dapr-equipped or not) has a pod-template metadata block at
  # spec.template.metadata.{labels,annotations}. Aspirate emits this unconditionally for K8s Deployments.
  # The patch operates on spec.template.metadata.annotations — even if absent (annotations: {}), the patch
  # creates it. Verify:
  for svc in eventstore eventstore-admin parties tenants memories eventstore-admin-ui parties-mcp; do
    grep -c "spec:" deploy/k8s/$svc/deployment.yaml > /dev/null \
      && grep -A 100 "spec:" deploy/k8s/$svc/deployment.yaml | grep -q "template:" \
      && echo "$svc: pod-template anchor present" \
      || echo "$svc: pod-template anchor MISSING — INVESTIGATE"
  done
  ```
  Every line must report "pod-template anchor present". If any line reports MISSING, aspirate's emission shape has changed and the patch contract needs revision — surface as a blocker.
- **And** the `accesscontrol.eventstore-admin.yaml` file name maps to the Dapr config name `accesscontrol-eventstore-admin` — note the **dot → hyphen substitution** between filename and config-name. This is the existing AppHost convention (see `DaprComponents/accesscontrol.eventstore-admin.yaml`) and Story 9.4's `accesscontrol-eventstore-admin.yaml` Dapr CR.

### AC6 — Post-aspirate `imagePullSecrets` patch contract (documented anchor; mechanized by Story 9.5)

- **Given** consumer Deployments need to pull from the htpasswd-protected Zot registry,
- **When** the post-aspirate `imagePullSecrets` patch runs (Story 9.5 `publish.ps1` step 7),
- **Then** every Deployment whose container image starts with the literal prefix `registry.hexalith.com/` carries `spec.template.spec.imagePullSecrets: [{ name: zot-pull-secret }]` at the pod-template level.
- **And** vendor-image Deployments (`keycloak`, `redis` from Story 9.3) do NOT receive `imagePullSecrets` — their image registries are public.
- **And** the patch is idempotent. The idempotency anchor is **exact-match at the YAML node**, not substring (party-mode review F8 — `name: zot-pull-secret` substring would mis-match `name: zot-pull-secret-staging` and silently skip a Deployment that needs the prod Secret). The contract:
  - Preferred: `yq '.spec.template.spec.imagePullSecrets[] | select(.name == "zot-pull-secret")'` returns a result for the Deployment → already-patched, skip.
  - Acceptable fallback (no yq): regex `name:[[:space:]]*zot-pull-secret([[:space:]]*$|[[:space:]]*#)` — anchors on end-of-line or trailing `#` comment after the name, rejecting `zot-pull-secret-staging`.
  - Forbidden: bare substring match on `name: zot-pull-secret`.
  Story 9.7's `ImagePullSecretsPatchScopeFitnessTest` MUST assert this exact-match contract; flagged in the fitness-test stubs section.
- **And** the dev verifies the post-aspirate emission baseline:
  ```bash
  # Every aspirate-emitted Deployment carries an image starting with registry.hexalith.com/
  grep -rE 'image:\s+registry\.hexalith\.com/' deploy/k8s/{eventstore,eventstore-admin,eventstore-admin-ui,parties,parties-mcp,tenants,memories}/deployment.yaml \
    | wc -l
  # Expected: 7 (one per app-id)
  # The baseline does NOT need to carry imagePullSecrets — that block is injected by Story 9.5's patch.
  ```
- **And** no `registry.hexalith.com/<...>` image appears under `deploy/k8s/redis/` or `deploy/k8s/keycloak/` (Story 9.3's concern — if those folders exist at story-execution time and contain a Zot reference, surface as a defect before AC sign-off).

### AC7 — Aspirate placeholder cleanup (publish.ps1 step 4 contract — list enumerated here)

- **Given** aspirate may emit placeholder / opt-in files some operators do not need,
- **When** the post-generation cleanup phase runs (`publish.ps1` step 4 — mechanised in Story 9.5),
- **Then** the **known-orphan file list** (enumerated by the dev during the one-shot aspirate emission in Subtask 4.1) is documented in this story's Dev Notes as the seed list Story 9.5 will mechanize. Known candidates from prior Epic 9 v1 experience (verify against actual emission):
  - `aspirate-readme.md` (root-of-`deploy/k8s/` placeholder)
  - `azure.bicep` (Azure-publisher artefact — emitted if the AppHost references any Azure resource; should be absent because v2 AppHost has no Azure resources)
  - Any per-service `kustomization.yaml` (aspirate sometimes emits a per-folder kustomization that conflicts with the top-level one — verify per-folder)
  - Any `.gitkeep` / `.placeholder` style file
- **And** the hand-authored carve-outs in `deploy/k8s/redis/` and `deploy/k8s/keycloak/` are NEVER touched by the cleanup (Story 9.3 + AC8 below contract this).
- **And** the top-level files `deploy/k8s/README.md` (Story 9.1), `deploy/k8s/kustomization.yaml` (this story), `deploy/k8s/namespace.yaml` (this story) are NEVER deleted by cleanup. Story 9.5's `publish.ps1` step 2 (clean phase) explicitly preserves them via a whitelist; this story's job is to make sure those files exist as the preservation targets.

### AC8 — Top-level `kustomization.yaml` + `namespace.yaml` wire 7 services + commented-out carve-out anchors

(Resolves party-mode review F13 — the three-way contradiction between AC8 / Subtask 3.3 / 3.4 / Common-Mistakes #3. Adopting the comment-out path: every commit on `main` between Story 9.2 close and Story 9.3 close is `kubectl kustomize`-clean.)

- **Given** a Kustomization is the K8s-native way to reference multiple resource folders,
- **When** `deploy/k8s/kustomization.yaml` is inspected,
- **Then** it references EXACTLY:
  ```yaml
  apiVersion: kustomize.config.k8s.io/v1beta1
  kind: Kustomization
  namespace: hexalith-parties
  resources:
    - namespace.yaml
    # Aspirate-emitted application services (Story 9.2 — alphabetical order):
    - eventstore
    - eventstore-admin
    - eventstore-admin-ui
    - memories
    - parties
    - parties-mcp
    - tenants
    # Hand-authored carve-outs — added by Story 9.3. Uncomment THESE TWO LINES when Story 9.3 lands its
    # deploy/k8s/redis/ and deploy/k8s/keycloak/ folders. AC11 row C12 grep-asserts they remain commented
    # at Story 9.2 close; Story 9.3's Definition of Done MUST flip them to uncommented + assert
    # `kubectl kustomize deploy/k8s/ --dry-run=client` exits 0 (party-mode review F13 + R3 handoff).
    # - redis
    # - keycloak
  ```
- **And** the YAML file is **byte-stable** across publish runs at the same commit (per AC10 byte-determinism).
- **And** `deploy/k8s/namespace.yaml` declares:
  ```yaml
  apiVersion: v1
  kind: Namespace
  metadata:
    name: hexalith-parties
    labels:
      app.kubernetes.io/part-of: hexalith-platform
  ```
- **And** the dev verifies via `kubectl kustomize deploy/k8s/ --dry-run=client > /dev/null; echo exit=$?` that the Kustomization parses **exit 0** at this story's close (the 7 aspirate folders exist; the carve-out lines are commented; no missing references).
- **And** at Story 9.3 close, the `# - redis` / `# - keycloak` lines are uncommented; `kubectl kustomize` continues to exit 0 because the folders now exist. **This is the documented Story 9.2 → Story 9.3 handoff contract.**

### AC9 — `.config/dotnet-tools.json` aspirate version pin + AppHost SDK pinned + Aspirate emission flags documented

- **Given** the aspirate tool version drives the emission shape and must be deterministic,
- **When** `.config/dotnet-tools.json` is inspected,
- **Then** the `aspirate` tool is pinned at version `9.1.0` (current value verified 2026-05-21 — see Dev Notes "Aspirate baseline"), with no `"rollForward": true` drift (the file currently has `rollForward: true`; the dev MUST evaluate whether to flip it to `false` for hardening — see Subtask 5.1 + Investigation Item I4).
- **And** `Aspire.AppHost.Sdk/13.3.3` is pinned in `Hexalith.Parties.AppHost.csproj` line 1 (unchanged from current state).
- **And** the **aspirate invocation contract** (for Story 9.5 to copy) is documented in this story's Dev Notes "Aspirate invocation contract" with explicit flags (corrected per party-mode review F1 — `--secret-provider` does NOT exist on aspirate 9.1.0; verified flag set via `dotnet aspirate generate --help`):
  ```
  dotnet tool restore
  cd src/Hexalith.Parties.AppHost
  DOTNET_ROLL_FORWARD=Major ContainerImageTags=<MinVer> dotnet tool run --allow-roll-forward aspirate -- generate \
    --skip-build \                  # this story only — Story 9.5 drops --skip-build to build+push
    --project-path Hexalith.Parties.AppHost.csproj \
    --container-image-tag <MinVer> \  # ContainerImageTags multi-valued — single value here
    --container-registry registry.hexalith.com \
    --non-interactive \
    --disable-state \               # skip aspirate-state.json emission (byte-determinism: avoid state-file racing across pass-1/pass-2; party-mode review F12)
    --disable-secrets \             # skip aspirate's encrypted secret state (no secret bootstrap needed in this story; Story 9.5 owns operator-managed Secrets)
    --include-dashboard false \
    --image-pull-policy IfNotPresent \
    --output-path ../../deploy/k8s  # explicit; default is "aspirate-output" so this pin is required
  ```
- **And** the dev runs the invocation once in this story (Subtask 4.1) to produce the committed baseline; Story 9.5 will run it on every publish.

### AC10 — Byte-determinism contract (one-shot baseline + future re-emission)

- **Given** the byte-determinism reproducibility contract from `docs/kubernetes-deployment-architecture.md` §11,
- **When** the dev runs `dotnet aspirate generate ...` (per AC9 invocation) twice in succession on the same commit on the same workstation,
- **Then** for every Aspirate-emitted `deployment.yaml`, every line except the `image:` line is byte-identical across runs.
- **And** the `image:` line resolves to the same MinVer tag on the same commit (allowed to differ across commits — that's the pre-MinVer-stamped baseline this story's committed emission captures at story-execution-time).
- **And** the hand-authored carve-outs (`deploy/k8s/redis/`, `deploy/k8s/keycloak/`) — if they exist at story-execution time per Story 9.3 — are byte-identical across runs unconditionally (no MinVer-tag exception applies to vendor images per Story 9.3 AC2 / AC3).
- **And** the dev verifies via (party-mode review F7 — `diff -ur` emits unified-diff `(-|+)` prefixes, NOT `(<|>)`):
  ```bash
  # First emission to a temp tree
  cp -r deploy/k8s /tmp/k8s-pass-1
  # Second emission (re-run aspirate — same flags as AC9 contract)
  cd src/Hexalith.Parties.AppHost
  DOTNET_ROLL_FORWARD=Major ContainerImageTags=<minver> dotnet tool run --allow-roll-forward aspirate -- generate --skip-build --project-path Hexalith.Parties.AppHost.csproj --container-image-tag <minver> --container-registry registry.hexalith.com --non-interactive --disable-state --disable-secrets --include-dashboard false --image-pull-policy IfNotPresent --output-path ../../deploy/k8s
  # Diff — strip image: lines AND unified-diff file-header lines (--- /+++ /@@) — focus on real drift
  diff -ur /tmp/k8s-pass-1/ deploy/k8s/ \
    | grep -vE '^([-+])[[:space:]]*image:[[:space:]]' \
    | grep -vE '^(---|\+\+\+|@@) ' \
    | head -50
  ```
  Expected: **diff residual is empty** (or contains only the `image:` line drift, which should also be zero on same commit + same MinVer).
- **And** the dev captures the verification result in Dev Notes "Byte-determinism evidence", **including** the locale + walk-order context (party-mode review R1 / Murat R1): record `LC_COLLATE`, `uname -srm`, `dotnet --info | head -10`, `dotnet aspirate --version` at baseline-emission time so Story 9.7's `AspirateEmissionByteDeterminismFitnessTest` knows its preconditions.

### AC11 — Cleanliness contract (forbidden patterns in committed `deploy/k8s/` tree)

- **Given** the AC9 cleanliness regex table from Story 9.1 (forbidden patterns F1–F12) applies to the in-scope file set,
- **When** the dev runs the following greps after the aspirate emission + Kustomization + namespace files land,
- **Then** each grep returns ZERO unexpected lines (modulo documented exceptions):

  | # | Check | Grep | Expected |
  |---|---|---|---|
  | C1 | F1 mutable tag on Zot image | `grep -rEn 'registry\.hexalith\.com/[A-Za-z0-9._/-]+:(latest\|staging-latest)\b' deploy/k8s/` | 0 lines |
  | C2 | F2 empty-tag on Zot image (bare EOL — party-mode review F5) | `grep -rEn 'registry\.hexalith\.com/[A-Za-z0-9._/-]+:[[:space:]]*$' deploy/k8s/` | 0 lines |
  | C3 | F3 stale v1 script names | `grep -rEn '\b(regen\.ps1\|deploy-local\.ps1\|teardown-local\.ps1)\b' deploy/k8s/` | 0 lines |
  | C4 | F7 plaintext Password line | `grep -rEn '^[[:space:]]*Password[[:space:]]*[:=]' deploy/k8s/` | 0 lines (no exception allowed in this story's emission) |
  | C5 | F8 JWT-shaped token | `grep -rEn '\beyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\b' deploy/k8s/` | 0 lines |
  | C6 | F9 Base64 credential in `auth:` field | `grep -rEn '"auth"[[:space:]]*:[[:space:]]*"[A-Za-z0-9+/=_-]{20,}"' deploy/k8s/` | 0 lines |
  | C7 | No `localhost`/loopback references in published manifests (party-mode review R6) | `grep -rEn '\b(localhost\|127\.0\.0\.1\|0\.0\.0\.0\|\[::1\]):[0-9]+' deploy/k8s/{eventstore,eventstore-admin,eventstore-admin-ui,parties,parties-mcp,tenants,memories}/` | 0 lines (publish-mode wiring per AC3 must replace loopback bindings with in-cluster DNS) |
  | C8 | No `keycloak.GetEndpoint`-shape stale references (party-mode review F6 — single backslash) | `grep -rEn 'GetEndpoint\(' deploy/k8s/` | 0 lines (resource expressions belong in source code, not emitted manifests) |
  | C9 | No `dapr.io/app-port` value other than `"8080"` (party-mode review F4 — PCRE lookahead replaced by two-grep pipeline) | `grep -rEn 'dapr\.io/app-port:[[:space:]]*"' deploy/k8s/ \| grep -v '"8080"'` | 0 lines (AC4 mandates `"8080"`; absent annotation is OK — pre-patch state) |
  | C10 | `KeycloakRealmUrlInCluster` constant anchor in Program.cs (party-mode review F18 / I2 anchor) | `grep -c 'KeycloakRealmUrlInCluster' src/Hexalith.Parties.AppHost/Program.cs` | ≥ 7 (1 declaration + 6 service-wiring uses) |
  | C11 | `PUBLISH-MODE-DNS-ANCHOR` magic comment present (cross-cutting fitness-test anchor) | `grep -c 'PUBLISH-MODE-DNS-ANCHOR' src/Hexalith.Parties.AppHost/Program.cs` | ≥ 1 |
  | C12 | Carve-out kustomization lines are COMMENTED at Story 9.2 close (handoff to Story 9.3 — party-mode review F13) | `grep -E '^[[:space:]]*#[[:space:]]*-[[:space:]]*(redis\|keycloak)\b' deploy/k8s/kustomization.yaml \| wc -l` | exactly 2 (the two commented carve-out lines) |

- **And** if any grep returns non-zero, the dev fixes the source AppHost composition or the post-aspirate cleanup list (AC7) and re-emits — do NOT hand-edit `deploy/k8s/<service>/*.yaml` files in this story (they are aspirate-emitted; hand-edits would break AC10 byte-determinism).
- **And** the `_bmad-output/implementation-artifacts/9-2-aspire-apphost-to-aspirate-manifest-composition.md` story file itself is OUT OF SCOPE for the cleanliness checks (it quotes forbidden patterns as examples).

## Tasks / Subtasks

### Task 1 — Submodule + tool-chain pre-flight + container-publish opt-in on 3 csprojs (AC1a, AC9)

- [x] Subtask 1.1 — Verify `Hexalith.Memories` submodule is initialised at the repo root (sibling of `Hexalith.Parties`). The AppHost's `Memories.Server` `ProjectReference` (line 20 of `Hexalith.Parties.AppHost.csproj`) requires the path `..\..\Hexalith.Memories\src\Hexalith.Memories.Server\Hexalith.Memories.Server.csproj` to exist; if absent, `dotnet build` errors out before aspirate generation can run. Verify via `ls /home/quentindv/Hexalith.Parties/Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj`; if missing, run `git submodule update --init Hexalith.Memories` (root-level only — do NOT pass `--recursive` per the project context rule "For repositories with submodules, initialize/update only root-level submodules by default").
- [x] Subtask 1.2 — Verify `Hexalith.EventStore` and `Hexalith.Tenants` submodules are similarly initialised. Run `git submodule status | grep -v '^[+-]'` and ensure no uninitialised entries remain at the root level.
- [x] Subtask 1.3 — Run `dotnet tool restore` from the repo root (consumes `.config/dotnet-tools.json`). Verify `dotnet aspirate --version` reports `9.1.0` (or note the actual version if drifted; if drifted, evaluate whether to update the pin per Subtask 5.1).
- [x] Subtask 1.3a — **Verify aspirate CLI flag surface** (party-mode review F1): run `dotnet aspirate generate --help` and confirm the AC9 invocation contract flags (`--skip-build`, `--container-image-tag`, `--container-registry`, `--non-interactive`, `--disable-state`, `--disable-secrets`, `--output-path`) all exist on aspirate 9.1.0. If any flag is missing or renamed, **stop and surface to the operator** — the AC9 contract is wrong and Story 9.5 will inherit the breakage. Story-spec patch authoritative flag list as of 2026-05-21: `--non-interactive --disable-secrets --disable-state --skip-build --container-builder --container-build-context --container-image-tag --container-build-arg --prefer-dockerfile --container-registry --container-repository-prefix --image-pull-policy --namespace --output-format --runtime-identifier --secret-password --private-registry --private-registry-{url,username,password,email} --include-dashboard --compose-build --replace-secrets --parameter`. Verified flags absent: `--secret-provider` (does NOT exist; do not use).
- [x] Subtask 1.4 — Verify Docker is running locally as a **hard gate** (party-mode review R7): `docker info > /dev/null 2>&1 && echo "docker OK"` MUST exit 0 before Task 4 fires. Aspirate 9.x probes for the container builder even with `--skip-build`; without a Docker daemon, the pre-flight detection fails. If `docker info` exits non-zero, STOP and start Docker before continuing — do NOT proceed to Task 4. (Note: `docker login -u parties-publisher` is NOT required for this story since `--skip-build` skips the push path, but the daemon must be reachable. Verify in worktree if Subtask 1.3a or Task 4 demonstrates otherwise.)
- [x] Subtask 1.5 — Run a clean `dotnet restore Hexalith.Parties.slnx` then `dotnet build src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --configuration Release` and confirm both succeed. The slnx-level restore primes the NuGet caches across all sibling submodule projects (Amelia I4). If the AppHost doesn't build, aspirate cannot consume it.
- [x] Subtask 1.6 — **Container publish opt-in on 3 csprojs** (party-mode review F11 / Winston D1 — promoted from Investigation Item I3 to a hard prerequisite). The current state of the 7 application service csprojs is: 4 services have `<EnableContainer>true</EnableContainer>` + `<ContainerRepository>` (verified in `Hexalith.EventStore/CLAUDE.md` containers table for eventstore / eventstore-admin / eventstore-admin-ui + `Hexalith.Tenants` csproj for tenants); 3 services do NOT. Aspirate emits `image: <fallback>` for non-opted-in csprojs, which **breaks AC4's regex on parties / parties-mcp / memories**. Make these edits:
  - **In this repo** — `src/Hexalith.Parties/Hexalith.Parties.csproj` — add inside the existing `<PropertyGroup>`:
    ```xml
    <EnableContainer>true</EnableContainer>
    <ContainerRepository>parties</ContainerRepository>
    ```
  - **In this repo** — `src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj` — same shape, repository value `parties-mcp`.
  - **In the `Hexalith.Memories` submodule (cross-submodule commit)** — `Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj` — same shape, repository value `memories`. Commit + push that edit in the Memories submodule first (Conventional Commits: `feat(server): add EnableContainer + ContainerRepository for K8s emission per Hexalith.Parties Story 9.2 v2`), then in this repo bump the Memories submodule pointer (`git -C Hexalith.Memories rev-parse HEAD` → `git add Hexalith.Memories && git commit -m "chore(submodule): bump Hexalith.Memories to <sha> for EnableContainer opt-in"`).
  - Verify all 7 csprojs now opt in: `for csproj in src/Hexalith.Parties/Hexalith.Parties.csproj src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj; do grep -q 'EnableContainer.*true' "$csproj" && echo "OK $csproj" || echo "MISSING $csproj"; done` — all 7 must report OK.
  - **Defense-in-depth**: also `grep -r ContainerImageTags --include='*.targets' --include='*.props' .` (Amelia R1) to confirm no `<ContainerImageTags>` is set anywhere (which would collide with `--container-image-tag` from the aspirate command line). Expected: zero matches.

### Task 2 — AppHost refactor: gate redis + keycloak behind `IsRunMode` (AC1, AC2)

- [x] Subtask 2.1 — Read `src/Hexalith.Parties.AppHost/Program.cs` (line by line — note any cross-reference to `redis` or `keycloak` resources downstream of their `AddRedis`/`AddKeycloak` calls).
- [x] Subtask 2.2 — Replace the `IResourceBuilder<RedisResource> redis = builder.AddRedis("redis");` line (currently around line 108) with:
  ```csharp
  // Redis is composed in run mode only — local Aspire dev needs a working backing store for Dapr
  // state + pubsub. In publish mode (aspirate consuming the manifest), Redis is supplied by the
  // hand-authored carve-out under deploy/k8s/redis/ (Story 9.3) — composing it here would cause
  // aspirate to emit a conflicting Deployment + Service that Story 9.3 must then suppress.
  IResourceBuilder<RedisResource>? redis = null;
  if (builder.ExecutionContext.IsRunMode)
  {
      redis = builder.AddRedis("redis");
  }
  ```
- [x] Subtask 2.3 — Wrap the entire `bool enableKeycloak = ...; if (enableKeycloak) { keycloak = builder.AddKeycloak(...) ... }` block (currently lines ~140–207) so that the `builder.AddKeycloak(...)` call ITSELF only runs in `IsRunMode`. Restructure:
  ```csharp
  bool enableKeycloak = !bool.TryParse(builder.Configuration["EnableKeycloak"], out bool parsed) || parsed;
  IResourceBuilder<KeycloakResource>? keycloak = null;
  ReferenceExpression? realmUrl = null;
  if (enableKeycloak && builder.ExecutionContext.IsRunMode)
  {
      // Run-mode-only composition (local Aspire dev). Keycloak in publish-mode (aspirate) is
      // supplied by deploy/k8s/keycloak/ (Story 9.3); the JWT authority/issuer env vars in
      // publish mode are wired to the in-cluster DNS constant (see Subtask 2.4).
      keycloak = builder.AddKeycloak("keycloak", 8180)
          .WithRealmImport("./KeycloakRealms");
      EndpointReference keycloakEndpoint = keycloak.GetEndpoint("http");
      realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/hexalith");
      // (the existing run-mode env-var wiring stays inside this if-block — see Subtask 2.4)
  }
  ```
- [x] Subtask 2.4 — Add a single `const string KeycloakRealmUrlInCluster = "http://keycloak.hexalith-parties.svc.cluster.local:8080/realms/hexalith";` near the top of `Program.cs` (alongside `eventStoreAccessControlConfigPath` etc.). Use this constant for the publish-mode env-var wiring.
- [x] Subtask 2.5 — Refactor the JWT env-var wiring on `eventstore`, `adminServer` (= `eventstore-admin`), `parties`, `partiesMcp`, `tenants` (5 services — `adminUI` is handled separately in Subtask 2.7). The wiring must preserve the **3-state semantics** (party-mode review F10):
  1. **Run-mode with Keycloak** (`realmUrl` is non-null) → wire Authority + Issuer to the `realmUrl` ReferenceExpression.
  2. **Publish-mode** (`IsPublishMode == true`) → wire Authority + Issuer to the `KeycloakRealmUrlInCluster` constant.
  3. **Run-mode without Keycloak** (`enableKeycloak == false`) → DO NOT wire Authority/Issuer at all on these 5 services. This matches today's behavior — `Program.cs` lines 78-87, 41-87 (parties + eventstore service wiring) skip the JWT block entirely when `keycloak` is null. Do not introduce empty-string clears here; that's adminUI's pattern (Subtask 2.7), not these 5 services.

  Extract a single helper to avoid 5×duplicated boilerplate (Amelia R6 improvement):
  ```csharp
  // PUBLISH-MODE-JWT-HELPER — single-place contract for JWT authority/issuer wiring.
  static IResourceBuilder<ProjectResource> WithJwtAuthority(
      IResourceBuilder<ProjectResource> svc,
      ReferenceExpression? runModeAuthority,
      string? publishModeAuthority)
  {
      if (runModeAuthority is not null)
      {
          return svc.WithEnvironment("Authentication__JwtBearer__Authority", runModeAuthority)
                    .WithEnvironment("Authentication__JwtBearer__Issuer", runModeAuthority);
      }
      if (publishModeAuthority is not null)
      {
          return svc.WithEnvironment("Authentication__JwtBearer__Authority", publishModeAuthority)
                    .WithEnvironment("Authentication__JwtBearer__Issuer", publishModeAuthority);
      }
      return svc;  // run-mode-without-Keycloak: no Authority/Issuer env vars (preserves today's behavior)
  }

  // Call once per service:
  string? publishModeAuthority = builder.ExecutionContext.IsPublishMode ? KeycloakRealmUrlInCluster : null;
  _ = WithJwtAuthority(eventStore, realmUrl, publishModeAuthority);
  _ = WithJwtAuthority(adminServer, realmUrl, publishModeAuthority);
  _ = WithJwtAuthority(parties, realmUrl, publishModeAuthority);
  _ = WithJwtAuthority(partiesMcp, realmUrl, publishModeAuthority);
  _ = WithJwtAuthority(tenants, realmUrl, publishModeAuthority);
  ```
  The `Audience`, `RequireHttpsMetadata`, `SigningKey`, and `TokenValidationParameters__ValidAudiences__*` env vars are NOT keycloak-resource-dependent — leave them on the unconditional `WithEnvironment` path so they fire in both modes (today's behavior). Service-specific audiences (`hexalith-eventstore`, `hexalith-parties`, `hexalith-parties-mcp`, `hexalith-tenants`) remain wired per service as today.
- [x] Subtask 2.6 — Drop the `.WithReference(keycloak)` and `.WaitFor(keycloak)` calls from ALL services in publish mode (they only make sense when `keycloak` is a composed resource). One pattern:
  ```csharp
  if (keycloak is not null)  // null in publish mode per Subtask 2.3
  {
      _ = eventStore.WithReference(keycloak).WaitFor(keycloak);
      _ = adminServer.WithReference(keycloak).WaitFor(keycloak);
      _ = parties.WithReference(keycloak).WaitFor(keycloak);
      _ = partiesMcp.WithReference(keycloak).WaitFor(keycloak);
      _ = tenants.WithReference(keycloak).WaitFor(keycloak);
      _ = adminUI.WithReference(keycloak).WaitFor(keycloak);
  }
  ```
- [x] Subtask 2.7 — 3-state wiring for the `adminUI` `EventStore__Authentication__Authority` + `ClientId` env vars (party-mode review F10 + decision: adminUI MUST authenticate in publish mode):
  - **Run-mode with Keycloak**: wire Authority to `realmUrl` ReferenceExpression + ClientId to `"hexalith-eventstore"` (today's behavior, preserved).
  - **Publish-mode**: wire Authority to `KeycloakRealmUrlInCluster` constant + ClientId to `"hexalith-eventstore"` (decision per AC3 last bullet; security regression otherwise).
  - **Run-mode without Keycloak**: explicit empty-string clears on both env vars (today's `else` branch behavior on lines 211-215 of current `Program.cs` — prevents stale values from a previous launch leaking into the UI).
  All three states must be preserved verbatim. Suggested wiring:
  ```csharp
  if (realmUrl is not null)  // run-mode with Keycloak
  {
      _ = adminUI.WithEnvironment("EventStore__Authentication__Authority", realmUrl)
                 .WithEnvironment("EventStore__Authentication__ClientId", "hexalith-eventstore");
  }
  else if (builder.ExecutionContext.IsPublishMode)  // publish-mode (Keycloak as carve-out)
  {
      _ = adminUI.WithEnvironment("EventStore__Authentication__Authority", KeycloakRealmUrlInCluster)
                 .WithEnvironment("EventStore__Authentication__ClientId", "hexalith-eventstore");
  }
  else  // run-mode without Keycloak — explicit clear (preserve today's behavior)
  {
      _ = adminUI.WithEnvironment("EventStore__Authentication__Authority", "")
                 .WithEnvironment("EventStore__Authentication__ClientId", "");
  }
  ```
  The `EventStore__AdminServer__SwaggerUrl` env var (also wired in the same block today) is NOT keycloak-resource-dependent — keep it on the unconditional path.
- [x] Subtask 2.8 — Refactor the `EnableMemoriesSearch` block to be **always-on in publish mode** (party-mode review R3 / Winston R3 — F17). The current shape (`if (builder.Configuration["EnableMemoriesSearch"] == "true") { ... }`) is host-config-driven, so in publish mode the feature flag depends on Amelia's local appsettings/launchSettings at aspirate-generate time — silently omitting the memories wiring from the committed baseline if she doesn't export the env var. Since ADR 9.3-2 made Memories a first-class topology participant, the feature-flag is run-mode-only:
  ```csharp
  bool enableMemoriesSearch = builder.ExecutionContext.IsPublishMode  // always-on in publish mode (Memories is first-class per ADR 9.3-2)
      || string.Equals(builder.Configuration["EnableMemoriesSearch"], "true", StringComparison.OrdinalIgnoreCase);
  if (enableMemoriesSearch)
  {
      _ = parties
          .WithReference(memories)
          .WaitFor(memories)
          .WithEnvironment("Parties__MemoriesSearch__Enabled", "true")
          .WithEnvironment("Parties__MemoriesSearch__Endpoint", "http://memories:8080/")
          .WithEnvironment("Parties__MemoriesSearch__RequireApiToken", "false")
          .WithEnvironment("Parties__MemoriesSearch__TenantId", "hexalith-dev")
          .WithEnvironment("Parties__MemoriesSearch__CaseId", "parties");
  }
  ```
  The `http://memories:8080/` endpoint is already the in-cluster DNS form, so no further publish-mode rewrite is needed. The `TenantId` / `CaseId` defaults remain at their dev-mode values — downstream operators override via ConfigMap overlay (deferred Kustomize-overlay concern, surfaced in deferred-work.md if needed).
- [x] Subtask 2.9 — Build the AppHost (party-mode review F3 — the original `--publisher manifest` smoke is replaced with an aspirate-based verification):
  ```bash
  # Step 1: Run-mode build (verifies the IsRunMode branch compiles + composes redis + keycloak)
  dotnet build src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --configuration Release

  # Step 2: Publish-mode smoke via aspirate itself (replaces the broken --publisher manifest invocation).
  # Run aspirate with --aspire-manifest writing to a throwaway path and --output-path /tmp/throwaway-k8s.
  # The Aspire manifest aspirate generates is the canonical "what publish-mode composes" artefact.
  mkdir -p /tmp/throwaway-k8s
  dotnet aspirate generate \
    --skip-build \
    --container-image-tag 0.0.0-smoketest \
    --container-registry registry.hexalith.com \
    --non-interactive \
    --disable-state \
    --disable-secrets \
    --output-path /tmp/throwaway-k8s 2>&1 | tee /tmp/aspirate-smoke.log
  echo "Exit: $?"

  # Step 3: Verify the throwaway emission contains exactly 7 per-service folders, no redis, no keycloak
  ls -d /tmp/throwaway-k8s/*/ 2>/dev/null | xargs -n1 basename | sort
  # Expected (sorted): eventstore eventstore-admin eventstore-admin-ui memories parties parties-mcp tenants
  # Forbidden:       redis keycloak

  # Cleanup
  rm -rf /tmp/throwaway-k8s
  ```
  If `redis/` or `keycloak/` folders appear in `/tmp/throwaway-k8s/`, the AppHost refactor (Subtask 2.2 / 2.3) did NOT gate the resources correctly under `IsPublishMode` — revisit. If neither appears AND all 7 application services do, publish-mode composition is correct and Task 4 can proceed.

### Task 3 — Top-level `kustomization.yaml` + `namespace.yaml` (AC8)

- [x] Subtask 3.1 — Create `deploy/k8s/namespace.yaml`:
  ```yaml
  apiVersion: v1
  kind: Namespace
  metadata:
    name: hexalith-parties
    labels:
      app.kubernetes.io/part-of: hexalith-platform
  ```
- [x] Subtask 3.2 — Create `deploy/k8s/kustomization.yaml` per AC8 verbatim — 7 aspirate-emitted folders alphabetical; carve-out lines **commented**. Final file:
  ```yaml
  apiVersion: kustomize.config.k8s.io/v1beta1
  kind: Kustomization
  namespace: hexalith-parties
  resources:
    - namespace.yaml
    # Aspirate-emitted application services (Story 9.2 — alphabetical order):
    - eventstore
    - eventstore-admin
    - eventstore-admin-ui
    - memories
    - parties
    - parties-mcp
    - tenants
    # Hand-authored carve-outs — added by Story 9.3. Uncomment THESE TWO LINES when Story 9.3 lands its
    # deploy/k8s/redis/ and deploy/k8s/keycloak/ folders. AC11 row C12 grep-asserts they remain commented
    # at Story 9.2 close; Story 9.3's Definition of Done MUST flip them to uncommented + assert
    # `kubectl kustomize deploy/k8s/ --dry-run=client` exits 0.
    # - redis
    # - keycloak
  ```
- [x] Subtask 3.3 — Smoke-check `kubectl kustomize deploy/k8s/ --dry-run=client > /dev/null; echo "exit=$?"` — expected **exit 0** (the 7 aspirate folders exist; the carve-out lines are commented out so Kustomize doesn't try to load them). If `--dry-run=client` exits non-zero at this story's close, the kustomization shape is broken and AC8 fails — fix.
- [x] Subtask 3.4 — AC11 row C12 verification (handoff anchor to Story 9.3): `grep -E '^[[:space:]]*#[[:space:]]*-[[:space:]]*(redis|keycloak)\b' deploy/k8s/kustomization.yaml | wc -l` must return **exactly 2** — one commented `redis` line and one commented `keycloak` line. This is the contract Story 9.3 will reverse (flip to uncommented + verify `kubectl kustomize` continues to exit 0 after its folders land).

### Task 4 — Aspirate one-shot emission (AC4, AC10)

- [x] Subtask 4.0.5 — **`PUBLISH_TARGET` foot-gun guard** (party-mode review F20): before Subtask 4.2 fires, confirm the env var is unset to avoid double-emission via Aspire-native publisher pipeline:
  ```bash
  [[ -z "${PUBLISH_TARGET:-}" ]] || { echo "FATAL: PUBLISH_TARGET=$PUBLISH_TARGET — unset before running aspirate"; exit 1; }
  ```
- [x] Subtask 4.1 — Resolve MinVer for the current commit (party-mode review F2 — `-getProperty:Version` returns `1.0.0` because MinVer hooks AFTER the build target fires; use `dotnet build --getProperty:Version` so MinVer is in the pipeline):
  ```bash
  cd /home/quentindv/Hexalith.Parties/src/Hexalith.Parties.AppHost
  # The Build target invokes MinVer; --getProperty:Version captures the final post-MinVer value.
  MINVER=$(dotnet build src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --configuration Release --getProperty:Version --nologo 2>/dev/null | tr -d '[:space:]')
  echo "Resolved MinVer: $MINVER"

  # Hard guards (party-mode review F2 + F16):
  [[ "$MINVER" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.-]+)?(\+[A-Za-z0-9.-]+)?$ ]] || { echo "FATAL: MinVer malformed: '$MINVER'"; exit 1; }
  [[ "$MINVER" != "1.0.0" ]] || { echo "FATAL: MinVer not stamped (got 1.0.0 default — git tag missing? Run 'git tag v0.0.1' as a baseline)"; exit 1; }
  [[ "$MINVER" != *"+dirty"* ]] || { echo "FATAL: working tree dirty — commit or stash before emission (the committed baseline must NOT carry +dirty per AC4)"; exit 1; }
  echo "MinVer OK: $MINVER"
  ```
  Fallback if `dotnet build --getProperty:Version` doesn't resolve MinVer cleanly: use `dotnet build ... && dotnet msbuild ... -t:MinVer -getProperty:MinVerVersion` (MinVer's output property is `MinVerVersion`). Document which path was used in the dev log.
- [x] Subtask 4.2 — Run aspirate generate with `--skip-build` and the verified-correct flag set (party-mode review F1 — no `--secret-provider`, use `--disable-state --disable-secrets` per the AC9 contract):
  ```bash
  cd /home/quentindv/Hexalith.Parties
  # Strip +<build-metadata> from MinVer for the image tag (aspirate accepts the suffix-free form)
  IMAGE_TAG=$(echo "$MINVER" | sed 's/+.*//')
  echo "Image tag: $IMAGE_TAG"

  DOTNET_ROLL_FORWARD=Major ContainerImageTags="$IMAGE_TAG" dotnet tool run --allow-roll-forward aspirate -- generate \
    --skip-build \
    --project-path Hexalith.Parties.AppHost.csproj \
    --container-image-tag "$IMAGE_TAG" \
    --container-registry registry.hexalith.com \
    --non-interactive \
    --disable-state \
    --disable-secrets \
    --include-dashboard false \
    --image-pull-policy IfNotPresent \
    --output-path ../../deploy/k8s 2>&1 | tee /tmp/aspirate-emit.log
  echo "Exit: $?"
  ```
  Expected exit: `0`. If aspirate errors out, capture the log and resolve (likely causes: missing submodule init per Task 1; missing `dotnet build` per Subtask 1.5; missing `EnableContainer` on any of the 3 newly-opted-in csprojs per Subtask 1.6 — RUN THE 1.6 VERIFICATION GREP FIRST IF AC4 LATER FAILS; or the AppHost has a publish-mode-incompatible feature on a removed-in-publish resource — investigate).
- [x] Subtask 4.3 — Verify exactly 7 per-service folders exist:
  ```bash
  ls -d deploy/k8s/*/
  # Expected: eventstore eventstore-admin eventstore-admin-ui memories parties parties-mcp tenants
  # (and once Story 9.3 ships: redis keycloak)
  # Forbidden:  redis keycloak (this story; Story 9.3 owns those)
  ```
  If `redis/` or `keycloak/` is present, the AppHost refactor (Task 2) didn't gate the resources correctly — revisit Subtask 2.2 / 2.3 / 2.9.
- [x] Subtask 4.4 — Verify the image-tag regex per AC4 + auxiliary cleanliness checks:
  ```bash
  # Per-app image-tag verification (verifies AC4 + AC11 C1/C2):
  grep -rE 'image:\s+registry\.hexalith\.com/[A-Za-z0-9._-]+:[^[:space:]]+' deploy/k8s/{eventstore,eventstore-admin,eventstore-admin-ui,parties,parties-mcp,tenants,memories}/deployment.yaml
  # Expected: 7 lines, each ending in :<MinVer> matching ^[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.-]+)?$

  # +dirty rejection (AC4 last bullet + party-mode review F16):
  grep -rEn ':[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.-]+)?\+' deploy/k8s/
  # Expected: 0 lines.

  # parties-mcp gateway URL resolution check (party-mode review E-5 / Quinn):
  # The AppHost wires Parties__Mcp__EventStoreGatewayBaseUrl via ReferenceExpression to
  # eventstore.GetEndpoint("http"). In publish mode aspirate should resolve this to a cluster Service
  # URL — verify the emitted value targets eventstore on a documented port (canonical doc §3.1 shows
  # port 8080; if aspirate emits port 8443 the K8s Service must also carry that port). Surface mismatch
  # before Story 9.5/9.6/9.7 lands.
  grep -rE 'Parties__Mcp__EventStoreGatewayBaseUrl' deploy/k8s/parties-mcp/ \
    | grep -E 'eventstore[:.][0-9]+|https?://eventstore'
  # Expected: a line resembling Parties__Mcp__EventStoreGatewayBaseUrl=... that includes 'eventstore'
  # and a port number. Verify the port matches services.eventstore.spec.ports in deploy/k8s/eventstore/service.yaml.
  # If the URL points at an unrelated host (localhost, parties-publisher.svc, etc.) → INVESTIGATE.
  ```
- [x] Subtask 4.5 — Enumerate aspirate placeholder files for AC7 cleanup list (party-mode review F12 — decision: `aspirate-state.json` is **stripped** unconditionally since `--disable-state` already prevents emission; if it shows up despite the flag, that's a Story 9.5 strip target):
  ```bash
  # Use find (party-mode review R10 — more portable than ls -A across shells):
  find deploy/k8s -mindepth 1 -maxdepth 1 -type f -printf '%f\n' \
    | grep -vE '^(README\.md|kustomization\.yaml|namespace\.yaml)$'
  ```
  Each line is a placeholder file Story 9.5 step 4 will strip. Candidates (verify against actual emission):
    - `aspirate-state.json` — **STRIP** (per `--disable-state` decision; if it's emitted anyway, that's a flag bug to surface).
    - `aspirate-readme.md` — STRIP (placeholder doc).
    - `azure.bicep` — STRIP (Azure-publisher artefact; v2 AppHost has no Azure resources so should be absent).
    - `parameters.json` — STRIP (operator-input shim aspirate uses with parameter prompts; `--non-interactive` should suppress).
    - Any `*.bak` / `*.tmp` — STRIP.

  Also check for **Dapr-CR-shaped emissions** (party-mode review I5 — Story 9.4 carve-out coordination):
  ```bash
  find deploy/k8s -name '*.yaml' -exec grep -l 'kind: Component\|kind: Configuration\|kind: Subscription\|kind: Resiliency' {} \;
  ```
  Each match is an aspirate-emitted Dapr CR. Document in Dev Notes "Aspirate Dapr-CR emission inventory". Story 9.4 owns hand-authored Dapr CRs under `deploy/dapr/`; if aspirate ALSO emits Dapr CRs under `deploy/k8s/<svc>/`, those need to either be stripped (Story 9.5 step 4 candidate) or reconciled with Story 9.4's hand-authored versions. Surface the decision in the dev log.

  Also check for **YAML comments with timestamps** (party-mode review R2 — byte-determinism risk):
  ```bash
  grep -rE '^# Generated|^# Created' deploy/k8s/
  ```
  If any match, document in Dev Notes "Byte-determinism evidence" and add to Story 9.5 strip list (Story 9.7 fitness test will need a `sed` filter to drop these from byte-determinism comparisons).

  Document the actual list + per-file kept/strip decision in Dev Notes "Aspirate placeholder inventory". The dev DOES NOT delete these files in this story — Story 9.5 owns the strip phase; this story's job is to enumerate them and pin the decisions.
- [x] Subtask 4.6 — Verify `localhost:` references are absent from emitted manifests (AC11 C7):
  ```bash
  grep -rEn '\blocalhost:[0-9]+' deploy/k8s/{eventstore,eventstore-admin,eventstore-admin-ui,parties,parties-mcp,tenants,memories}/
  ```
  Expected: `0 lines`. If hits, the AppHost refactor (Task 2) didn't replace all run-mode-only references — revisit Subtask 2.4 / 2.5.
- [x] Subtask 4.7 — Verify `GetEndpoint(...)`-shape resource expressions did NOT leak into emitted manifests (AC11 C8):
  ```bash
  grep -rEn 'GetEndpoint\(' deploy/k8s/
  ```
  Expected: `0 lines`.

### Task 5 — Aspirate version pin verification + AppHost SDK pin verification (AC9)

- [x] Subtask 5.1 — Read `.config/dotnet-tools.json`. Verify `aspirate` is pinned at `9.1.0` (or capture the actual version if drifted). **Flip `"rollForward": true` to `"rollForward": false`** (party-mode review F14 — Quinn C-2 + Murat R5): the byte-determinism contract (AC10) is structurally incompatible with `rollForward: true`. Story 9.7's `AspirateEmissionByteDeterminismFitnessTest` will run in CI on a workstation that may have a different aspirate version than the dev's — `rollForward: true` makes that drift undetectable. The "ease of upgrade" cost is one explicit JSON edit when a real aspirate version bump lands; that's the correct ergonomic trade for determinism. Apply via:
  ```bash
  # Verify current state
  jq '.tools.aspirate' .config/dotnet-tools.json
  # Expected (before): { "version": "9.1.0", "commands": ["aspirate"], "rollForward": true }

  # Flip the flag
  jq '.tools.aspirate.rollForward = false' .config/dotnet-tools.json > /tmp/dt.json && mv /tmp/dt.json .config/dotnet-tools.json

  # Re-verify
  jq '.tools.aspirate' .config/dotnet-tools.json
  # Expected (after): { "version": "9.1.0", "commands": ["aspirate"], "rollForward": false }
  ```
  Document the decision in Dev Notes "Aspirate version pinning decision". This edit is committed as part of this story.
- [x] Subtask 5.2 — Verify `Aspire.AppHost.Sdk/13.3.3` is unchanged in `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` line 1.
- [x] Subtask 5.3 — Verify the Aspirate invocation flags in Dev Notes "Aspirate invocation contract" match the AC9 contract.

### Task 6 — Byte-determinism verification (AC10)

- [x] Subtask 6.1 — Copy the current `deploy/k8s/` to a temp location: `cp -r deploy/k8s /tmp/k8s-pass-1`.
- [x] Subtask 6.2 — Re-run aspirate per Subtask 4.2 exactly.
- [x] Subtask 6.3 — Diff and report (party-mode review F7 — fixed regex for unified-diff prefixes):
  ```bash
  diff -ur /tmp/k8s-pass-1/ deploy/k8s/ > /tmp/byte-det-diff.raw 2>&1; echo "raw-exit=$?"
  # Strip the expected drift (image: lines + diff file headers + hunk markers) before counting:
  grep -vE '^([-+])[[:space:]]*image:[[:space:]]' /tmp/byte-det-diff.raw \
    | grep -vE '^(---|\+\+\+|@@) ' \
    > /tmp/byte-det-diff.filtered
  echo "Raw diff lines:      $(wc -l < /tmp/byte-det-diff.raw)"
  echo "Filtered diff lines: $(wc -l < /tmp/byte-det-diff.filtered)"
  head -50 /tmp/byte-det-diff.filtered
  ```
  Expected: filtered residual = **0 lines**. Raw diff may carry image: line drift if MinVer rebuilt the version stamp between passes (should not happen on same commit + same MinVer; if it does, surface in Dev Notes). Capture both counts in Dev Notes "Byte-determinism evidence" along with locale + walk-order context per AC10.
- [x] Subtask 6.4 — If the diff is non-empty (and not just `image:` lines), investigate. Common culprits: (a) timestamp embedded in a comment by aspirate, (b) non-deterministic resource ordering in aspirate output, (c) AppHost composition that uses a Guid or DateTime literal anywhere. Surface as a defect to resolve before AC10 close. If unresolvable, document the drift in Dev Notes and add to deferred-work.md under "Deferred from: story 9-2 v2 byte-determinism residual".

### Task 7 — Cleanliness contract sweep (AC11)

- [x] Subtask 7.1 — Run grep C1 (F1 mutable tag): `grep -rEn 'registry\.hexalith\.com/[A-Za-z0-9._/-]+:(latest|staging-latest)\b' deploy/k8s/` — expected 0 lines.
- [x] Subtask 7.2 — Run grep C2 (F2 empty-tag bare-EOL — corrected per party-mode review F5): `grep -rEn 'registry\.hexalith\.com/[A-Za-z0-9._/-]+:[[:space:]]*$' deploy/k8s/` — expected 0 lines.
- [x] Subtask 7.3 — Run grep C3 (F3 stale v1 script names): `grep -rEn '\b(regen\.ps1|deploy-local\.ps1|teardown-local\.ps1)\b' deploy/k8s/` — expected 0 lines.
- [x] Subtask 7.4 — Run grep C4 (F7 plaintext Password line): `grep -rEn '^[[:space:]]*Password[[:space:]]*[:=]' deploy/k8s/` — expected 0 lines.
- [x] Subtask 7.5 — Run grep C5 (F8 JWT token): `grep -rEn '\beyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\b' deploy/k8s/` — expected 0 lines.
- [x] Subtask 7.6 — Run grep C6 (F9 Base64 cred): `grep -rEn '"auth"[[:space:]]*:[[:space:]]*"[A-Za-z0-9+/=_-]{20,}"' deploy/k8s/` — expected 0 lines.
- [x] Subtask 7.7 — Run grep C7 (no loopback in published manifests — broadened per party-mode review R6): `grep -rEn '\b(localhost|127\.0\.0\.1|0\.0\.0\.0|\[::1\]):[0-9]+' deploy/k8s/{eventstore,eventstore-admin,eventstore-admin-ui,parties,parties-mcp,tenants,memories}/` — expected 0 lines.
- [x] Subtask 7.8 — Run grep C8 (no `GetEndpoint(` in emitted manifests — single-backslash per party-mode review F6): `grep -rEn 'GetEndpoint\(' deploy/k8s/` — expected 0 lines.
- [x] Subtask 7.9 — Run grep C9 (Dapr app-port typos — two-grep pipeline replaces PCRE lookahead per party-mode review F4): `grep -rEn 'dapr\.io/app-port:[[:space:]]*"' deploy/k8s/ | grep -v '"8080"'` — expected 0 lines (every emitted app-port annotation, if any, is `"8080"`).
- [x] Subtask 7.10 — Run grep C10 (`KeycloakRealmUrlInCluster` anchor present in Program.cs — party-mode review F18 / I2 anchor): `grep -c 'KeycloakRealmUrlInCluster' src/Hexalith.Parties.AppHost/Program.cs` — expected ≥ 7 (1 declaration + 6 service-wiring uses across `eventstore`, `eventstore-admin`, `parties`, `partiesMcp`, `tenants`, `adminUI`).
- [x] Subtask 7.11 — Run grep C11 (`PUBLISH-MODE-DNS-ANCHOR` magic comment present): `grep -c 'PUBLISH-MODE-DNS-ANCHOR' src/Hexalith.Parties.AppHost/Program.cs` — expected ≥ 1.
- [x] Subtask 7.12 — Run grep C12 (carve-out kustomization lines remain commented at story close — party-mode review F13 handoff): `grep -E '^[[:space:]]*#[[:space:]]*-[[:space:]]*(redis|keycloak)\b' deploy/k8s/kustomization.yaml | wc -l` — expected exactly **2**.
- [x] Subtask 7.13 — Capture all grep outputs in Dev Notes "Cleanliness sweep evidence". If any non-zero, fix and re-run; do NOT close AC11 until all 12 greps return their expected values.

### Task 8 — Documentation + dev-log + sprint-status close

- [x] Subtask 8.1 — Update Dev Notes sections per the structure below, populating:
  - "Aspirate baseline" (versions, flags, runtime evidence)
  - "Aspirate invocation contract" (the exact command Story 9.5 will run)
  - "Aspirate placeholder inventory" (per Subtask 4.5)
  - "Byte-determinism evidence" (per Subtask 6.3)
  - "Cleanliness sweep evidence" (per Task 7)
  - "Open investigations" (I1-I4 — resolved or carried forward)
- [x] Subtask 8.2 — Update `_bmad-output/implementation-artifacts/sprint-status.yaml`: set `9-2-aspire-apphost-to-aspirate-manifest-composition: review` (after dev completes implementation and tests; the `code-review` skill will move it to `done`). Append a `last_updated` audit line per the project convention.
- [x] Subtask 8.3 — Commit message convention (per project Conventional Commits rule): `feat(deploy): story 9-2 v2 Aspire AppHost → aspirate manifest composition`. Body bullets:
  - AppHost refactor: redis + keycloak gated behind `IsRunMode`; publish-mode JWT authority wired to `KeycloakRealmUrlInCluster` constant.
  - `deploy/k8s/namespace.yaml` + `deploy/k8s/kustomization.yaml` top-level wiring.
  - `deploy/k8s/{eventstore,eventstore-admin,eventstore-admin-ui,memories,parties,parties-mcp,tenants}/` aspirate-emitted baseline (MinVer `<value>`, registry `registry.hexalith.com`, `--skip-build` per Story 9.2 scope).
  - 3 patch contracts documented (Dapr annotations, JWT secretKeyRef, imagePullSecrets) — implementations land in Story 9.5.
  - AC11 cleanliness greps: 9/9 zero.
  - AC10 byte-determinism: same-commit re-emission diff `<line-count>` (expected 0).

### Review Findings

- [x] [Review][Patch] Extra `deploy/k8s/dapr/` CR folder violates the Story 9.2 exact-folder and non-scope contract [deploy/k8s/dapr/statestore.yaml:1] — AC4 requires exactly the 7 application service folders under `deploy/k8s/`, and the Scope/Non-Scope table assigns Dapr CRs to Story 9.4. The diff adds `deploy/k8s/dapr/statestore.yaml` and `deploy/k8s/dapr/pubsub.yaml`, then Dev Notes explicitly keep them as unreferenced inventory. Removed from the committed baseline; Story 9.5 owns mechanized stripping if aspirate emits the folder again.
- [x] [Review][Patch] Run-mode Redis resource is composed but not wired to the Dapr state/pubsub graph [src/Hexalith.Parties.AppHost/Program.cs:116] — AC2 says existing `.WithReference(redis)` chains through `eventStoreResources.StateStore` / `PubSub` continue to work, but the implementation discards `builder.AddRedis("redis")`; `Hexalith.EventStore.Aspire` still hardcodes `redisHost=localhost:6379`. Fixed by passing the run-mode Redis resource into `AddHexalithEventStore` and wiring the Dapr Redis component metadata to the Aspire endpoint when present.
- [x] [Review][Patch] `parties-mcp` publish-mode gateway URL bypasses the required Aspire resource expression [src/Hexalith.Parties.AppHost/Program.cs:62] — AC1 requires `parties-mcp` to keep `.WithReference(eventStore)` and `Parties__Mcp__EventStoreGatewayBaseUrl` as an Aspire resource expression that resolves through the graph. Fixed by using the `eventStore.GetEndpoint("http")` resource expression for both modes.
- [x] [Review][Patch] Admin UI Swagger URL emits as a relative path in the Kubernetes baseline [deploy/k8s/eventstore-admin-ui/kustomization.yaml:16] — `Program.cs` wires `EventStore__AdminServer__SwaggerUrl` from `adminServer.GetEndpoint("https")`, but aspirate captured `/swagger/index.html`. Fixed by using the Admin Server HTTP endpoint resource expression plus `/swagger/index.html`; regenerated baseline now emits `http://eventstore-admin:8080/swagger/index.html`.
- [x] [Review][Patch] JWT `secretKeyRef` patch anchor is not present in emitted deployments [deploy/k8s/eventstore/deployment.yaml:40] — Scope item 5 says this story documents the JWT `secretKeyRef` anchor location in each Dapr-equipped Deployment container `env` block, but the emitted deployments contain only `envFrom` ConfigMap references and no `Authentication__JwtBearer__SigningKey` env entry. Fixed by changing the Story 9.5 patch contract to create-or-upsert the env entry, creating `env:` before `envFrom` if aspirate emits only ConfigMap references.
- [x] [Review][Patch] Aspirate invocation contract omits the command requirements that were needed for the successful baseline [_bmad-output/implementation-artifacts/9-2-aspire-apphost-to-aspirate-manifest-composition.md:733] — the reusable AC9/Story 9.5 command omits `--project-path`, `--include-dashboard false`, `--image-pull-policy IfNotPresent`, `ContainerImageTags=<minver>`, and `DOTNET_ROLL_FORWARD=Major`. Fixed across AC4/AC9/AC10/Subtask 4.2 with the verified `dotnet tool run --allow-roll-forward aspirate -- generate` command shape.
- [x] [Review][Patch] Smoke-check section still requires the replaced `--publisher manifest` path [_bmad-output/implementation-artifacts/9-2-aspire-apphost-to-aspirate-manifest-composition.md:925] — Subtask 2.9 says the broken Aspire `--publisher manifest` smoke was replaced with aspirate-based verification, but Testing Standards still requires `dotnet run -- --publisher manifest`. Fixed by replacing the stale smoke with aspirate-based throwaway emission.
- [x] [Review][Patch] `KeycloakRealmUrlInCluster` anchor comment omits the required drift warning and Story 9.3 cross-check [src/Hexalith.Parties.AppHost/Program.cs:13] — AC3 requires the mechanical anchor comment to say that Service name/port/namespace/realm changes must update the constant and that Story 9.3 Definition of Done must cross-verify. Fixed by adding the drift-warning and Story 9.3 cross-check comments at the constant.

## Dev Notes

### Architecture intelligence — what binds this story to AppHost composition vs aspirate emission

**The single source of truth for the service graph is `src/Hexalith.Parties.AppHost/Program.cs`.** Aspirate is a downstream consumer that translates Aspire's manifest into K8s YAML. Anything that lands in `deploy/k8s/<app-id>/*.yaml` is a function of:

1. The Aspire `IDistributedApplicationBuilder` graph at publish time (project resources, container resources, Dapr sidecars, env vars, references).
2. The aspirate version (`9.1.0` per `.config/dotnet-tools.json`) and its CLI flags.

The cleanest design pattern, **already established in the EventStore submodule's `HexalithEventStoreExtensions.cs:123` and `:150`**, is `if (!builder.ExecutionContext.IsPublishMode) { ...dev-only wiring... }`. The current Parties AppHost does NOT use this pattern; this story introduces it.

**Why redis + keycloak must be gated behind `IsRunMode`, not removed outright:**

- Removing them outright breaks local Aspire dev (developers lose their backing store + OIDC issuer for `dotnet run` from the AppHost).
- Gating them behind `IsRunMode` preserves local dev AND prevents aspirate from emitting conflicting `redis` + `keycloak` per-service folders that Story 9.3 has to suppress.
- Story 9.3 AC1 line 3258 of `epics.md` says: "neither folder is present in or generated by `src/Hexalith.Parties.AppHost/Program.cs`" — the **publish-mode** branch satisfies this. Run-mode composition does not produce `deploy/k8s/<folder>/` artefacts because `dotnet aspire run` doesn't emit manifests; only `dotnet aspirate generate` does, and aspirate runs in publish mode.

### Architecture intelligence — read carefully, this is the most subtle part of the story

- The `Hexalith.EventStore.Aspire` extension method `AddHexalithEventStore(...)` already gates its own dev-only wiring (`AdminServer__EventStoreDaprHttpEndpoint`, `TraceUrl`, `MetricsUrl`, `LogsUrl`, `AdminServer__ResiliencyConfigPath`) behind `if (!builder.ExecutionContext.IsPublishMode)`. The Parties AppHost composes EventStore via this helper — so EventStore's own publish-mode safety is already in place. **This story does NOT need to refactor the EventStore submodule.** It only needs to add the same pattern at the Parties-AppHost composition layer for redis + keycloak.
- The Dapr Components `statestore` (`state.redis`) and `pubsub` are wired by `AddHexalithEventStore` via `AddDaprComponent(...)` and `AddDaprPubSub(...)`. In run mode, `statestore` receives the Aspire Redis endpoint; without an Aspire Redis resource it falls back to `localhost:6379` for standalone Dapr local runs. **The K8s deployment uses hand-authored Dapr CRs from `deploy/dapr/` (Story 9.4)**, NOT the aspirate-emitted Dapr Components from the AppHost. If aspirate emits `deploy/k8s/dapr/*.yaml`, Story 9.2 strips that folder before commit and Story 9.5 must mechanize the same cleanup.
- The current AppHost has a `if (string.Equals(builder.Configuration["EnableMemoriesSearch"], "true", ...))` block on lines ~128–138 that wires Parties → Memories. This is a runtime-configurable feature flag. It's safe to leave unchanged for both run mode and publish mode — both depend on the same env var being set by the operator at the K8s consumer side (via ConfigMap override).
- The `partiesMcp` service uses `.WithReference(eventStore)` and `.WithReference(parties)` — these ARE project resources that aspirate emits, so they work in publish mode. The `Parties__Mcp__EventStoreGatewayBaseUrl` env var uses `ReferenceExpression.Create($"{eventStore.GetEndpoint("http")}")` — Aspire resolves this to the in-cluster Service URL in publish mode (e.g., `http://eventstore:8080`). This works as-is.

### Aspirate baseline (verified at story-spec patch time 2026-05-21 — re-verified during the party-mode review via `dotnet aspirate generate --help`)

```
Aspire.AppHost.Sdk:          13.3.3       (Hexalith.Parties.AppHost.csproj line 1, unchanged)
Aspirate tool version:       9.1.0        (.config/dotnet-tools.json — rollForward FLIPPED to false in Subtask 5.1 per D-I4)
.NET SDK:                    10.0.300     (global.json pinned)
.NET target framework:       net10.0
CommunityToolkit.Aspire:     13.0.0       (Hosting.Dapr — Directory.Packages.props)
MinVer:                       7.0.0       (Directory.Packages.props; MinVerTagPrefix=v in Directory.Build.props)
```

**Verified aspirate 9.1.0 CLI flag set** (party-mode review F1 — flags not in this list are hallucinated; do not use):

```
--non-interactive --disable-secrets --disable-state
-lp/--launch-profile -p/--project-path -m/--aspire-manifest -o/--output-path
--skip-build -sf/--skip-final
--container-builder -cbc/--container-build-context -ct/--container-image-tag -cba/--container-build-arg
--prefer-dockerfile -cr/--container-registry -crp/--container-repository-prefix
--image-pull-policy --namespace --output-format --runtime-identifier
--secret-password
--private-registry --private-registry-url/-username/-password/-email
--include-dashboard --compose-build --replace-secrets -pa/--parameter
```

Notably absent: `--secret-provider` (does NOT exist — was hallucinated in early spec drafts).

### Aspirate invocation contract (the exact command Story 9.5 step 3 will mechanize)

```bash
# Prerequisite: dotnet tool restore (one-time per checkout)
# Prerequisite: docker login -u parties-publisher registry.hexalith.com (one-time per workstation, for Story 9.5 only)
# Prerequisite: Hexalith.Memories submodule initialised at repo root
# Prerequisite: PUBLISH_TARGET env var unset (Subtask 4.0.5)
# Prerequisite: IMAGE_TAG is MinVer without `+dirty` build metadata, e.g. `IMAGE_TAG=${MINVER%%+*}`

# Story 9.2 (this story, --skip-build, manifest-shape baseline only):
cd src/Hexalith.Parties.AppHost
DOTNET_ROLL_FORWARD=Major ContainerImageTags="$IMAGE_TAG" dotnet tool run --allow-roll-forward aspirate -- generate \
  --skip-build \
  --project-path Hexalith.Parties.AppHost.csproj \
  --container-image-tag "$IMAGE_TAG" \
  --container-registry registry.hexalith.com \
  --non-interactive \
  --disable-state \
  --disable-secrets \
  --include-dashboard false \
  --image-pull-policy IfNotPresent \
  --output-path ../../deploy/k8s

# Story 9.5 (NO --skip-build — builds + pushes 7 images via .NET SDK Container Publish to Zot):
DOTNET_ROLL_FORWARD=Major ContainerImageTags="$IMAGE_TAG" dotnet tool run --allow-roll-forward aspirate -- generate \
  --project-path Hexalith.Parties.AppHost.csproj \
  --container-image-tag "$IMAGE_TAG" \
  --container-registry registry.hexalith.com \
  --non-interactive \
  --disable-state \
  --disable-secrets \
  --include-dashboard false \
  --image-pull-policy IfNotPresent \
  --output-path ../../deploy/k8s
```

**Why `--non-interactive`:** aspirate 9.x prompts the operator for missing parameters by default. CI + scripted pipelines need the non-interactive form.

**Why `--disable-state`:** prevents emission of `aspirate-state.json` (a tool-cache file aspirate uses to remember previous-run choices). The state file would otherwise risk byte-determinism (party-mode review F12); since this story's baseline is one-shot and Story 9.5's `publish.ps1` is fully scripted, no state-resumption value is lost.

**Why `--disable-secrets`:** prevents aspirate's encrypted secret state from being maintained — this story's baseline doesn't bootstrap operator-managed Secrets (Story 9.5 owns that), and the `--non-interactive` flag would otherwise either hang or error out asking for a `--secret-password` value on first run (party-mode review R2). The Secret YAML stubs aspirate emits for connection strings are unaffected by this flag — those still ship.

**Why pin `--output-path deploy/k8s`:** aspirate's default is `aspirate-output` — pin to `deploy/k8s` to match the documented K8s deployment layout.

### JWT secretKeyRef patch contract

Aspirate 9.1.0 emits the JWT-bearing services with `envFrom` ConfigMap references and omits empty `Authentication__JwtBearer__SigningKey` values from the generated literals. Therefore Story 9.5's JWT patch MUST NOT depend on replacing an existing `Authentication__JwtBearer__SigningKey` line. Its stable anchor is the Deployment/container name for the JWT-bearing services: `eventstore`, `eventstore-admin`, `parties`, `parties-mcp`, and `tenants`.

The patch contract is create-or-upsert:

```yaml
env:
- name: Authentication__JwtBearer__SigningKey
  valueFrom:
    secretKeyRef:
      name: hexalith-jwt-signing
      key: Authentication__JwtBearer__SigningKey
```

If an `env:` block is absent, create it before `envFrom`. If the `Authentication__JwtBearer__SigningKey` entry exists as a literal value, replace it with the `secretKeyRef`. If it already exists with the exact Secret/key pair above, leave it unchanged. This makes the Story 9.5 patch idempotent and avoids requiring hand edits to aspirate-emitted `deployment.yaml` files in Story 9.2.

### Aspirate placeholder inventory

Captured during dev-story execution on 2026-05-21.

Root placeholder files under `deploy/k8s/` excluding `README.md`, `kustomization.yaml`, and `namespace.yaml`: **none**.

Aspirate emitted per-service `kustomization.yaml` files in all 7 application folders. Decision: **keep for Story 9.2 baseline** because the top-level hand-authored Kustomization references each folder and `kubectl kustomize deploy/k8s/` parses cleanly with those folder-level files.

Aspirate emitted Dapr CR-shaped files during generation:

- `deploy/k8s/dapr/statestore.yaml`
- `deploy/k8s/dapr/pubsub.yaml`

Decision: **strip these from the committed Story 9.2 baseline** after each aspirate run. AC4 requires exactly the 7 application service folders under `deploy/k8s/`, and Story 9.4 owns hand-authored Dapr CRs under `deploy/dapr/`. The generated Dapr CR inventory is recorded here only so Story 9.5 can mechanize the cleanup step.

No `# Generated` / `# Created` timestamp comments were emitted.

### Byte-determinism evidence

Captured during dev-story execution on 2026-05-21.

Command shape used for the baseline differs from the original AC9 draft in three observed aspirate 9.1.0 requirements:

- `--project-path Hexalith.Parties.AppHost.csproj` is required when running from `src/Hexalith.Parties.AppHost`; repo-root auto-discovery failed.
- `--include-dashboard false` is required in non-interactive mode to explicitly decline dashboard emission.
- `--image-pull-policy IfNotPresent` is required in non-interactive mode.
- `ContainerImageTags=0.1.1-preview.0.3` must be exported so EventStore/Tenants submodule `Directory.Build.targets` do not fall back to `staging-latest`.

Evidence:

```text
MinVer: 0.1.1-preview.0.3 (fallback via dotnet msbuild -t:MinVer -getProperty:MinVerVersion; dotnet build --getProperty:Version returned 1.0.0)
Image tag: 0.1.1-preview.0.3
LC_COLLATE=unset
uname -srm: Linux 6.17.0-23-generic x86_64
.NET SDK: 10.0.300
MSBuild: 18.6.3+caa81fa49
aspirate: 9.1.0+c2905d2ab854aaac7f86f3d63da3b93950e76630
Byte-determinism diff: raw-exit=0, raw lines=0, filtered lines=0
kubectl kustomize deploy/k8s/: exit=0
```

### Cleanliness sweep evidence

Captured during dev-story execution on 2026-05-21.

```text
C1 mutable/staging tags: 0
C2 empty Zot image tags: 0
C3 stale v1 script names: 0
C4 plaintext Password lines: 0
C5 JWT-shaped tokens: 0
C6 auth field base64 credentials: 0
C7 loopback references in app service folders: 0
C8 GetEndpoint leaks in emitted manifests: 0
C9 non-8080 Dapr app-port annotations: 0
C10 KeycloakRealmUrlInCluster occurrences: 7
C11 PUBLISH-MODE-DNS-ANCHOR occurrences: 1
C12 commented redis/keycloak carve-out lines: 2
```

### Resolved decisions (formerly Investigation Items — party-mode review F15 / Quinn Q-3: directives, not open questions)

The original story spec framed four items as "Investigation Items" with recommendations attached — Quinn called these directives in disguise. The party-mode review converged on the recommendations, so they're now decisions baked into the AC contracts. Documented here for audit trail.

**D-I1 (was I1): `dotnet aspirate generate --skip-build`** — this story uses `--skip-build` exclusively (manifest-shape-only baseline; no registry push). Story 9.5 drops the flag to build + push. Verified at story-spec patch time via `dotnet aspirate generate --help` against the pinned 9.1.0 (party-mode review F1).

**D-I2 (was I2): `adminUI` authenticates against in-cluster Keycloak in publish mode** — wired to `KeycloakRealmUrlInCluster` per Subtask 2.7. Running the admin UI unauthenticated in K8s would be a security regression vs. local Aspire dev.

**D-I3 (was I3): Container publish path is `.NET SDK Container Publish`** — no Dockerfiles. Subtask 1.6 adds `<EnableContainer>true</EnableContainer>` + `<ContainerRepository>` to the 3 missing csprojs (party-mode review F11 — the original I3 framing missed that 3 of 7 csprojs were not yet opted in).

**D-I4 (was I4): `.config/dotnet-tools.json` `rollForward: true` → `false`** — flipped in Subtask 5.1 (party-mode review F14). Byte-determinism (AC10) is structurally incompatible with rollForward drift; Story 9.7's `AspirateEmissionByteDeterminismFitnessTest` will run in CI on a different workstation than the dev's, where rollForward would cause silent version mismatch.

### Open architectural questions deferred to deferred-work.md

**Q-1 (Quinn): Should the AppHost be split into `Hexalith.Parties.AppHost` (run-mode local dev) + `Hexalith.Parties.AspirateHost` (publish-mode only, lean composition)?**
The dual-mode AppHost — run-mode composes 9 resources (7 + redis + keycloak), publish-mode composes 7 — is a contradiction the spec accepts as load-bearing. A 4th-direction TRIZ resolution splits the host into two projects. Cost: one csproj + duplicate Program.cs. Benefit: byte-determinism becomes trivially defensible; onboarding cognitive load halves. **Deferred** to deferred-work.md as a planned-architecture improvement; not in scope for Story 9.2.

**Q-2 (Quinn): Should `KeycloakRealmUrlInCluster` be a Story 9.5 patch-step contract (#4) instead of an AppHost constant?**
The current design hard-codes the carve-out's Service shape into the AppHost. Story 9.3 owns the keycloak Service spec; the AppHost constant could silently lie if 9.3 drifts. A 4th-direction resolution makes `publish.ps1` derive the value from `kubectl get svc keycloak -n hexalith-parties -o jsonpath='{.spec.ports[0].port}'`. **Deferred** — the current design uses a `PUBLISH-MODE-DNS-ANCHOR` magic comment + AC11 C10/C11 cleanliness greps + Story 9.3 Definition of Done cross-verification to mitigate drift. If Story 9.3 changes the keycloak Service shape, the AC11 C10 anchor breaks visibly. Adequate for MVP; deferred to a hardening pass.

### Forward-references — Story 9.7 fitness-test contract stubs (party-mode review I1 / Murat)

This story documents the post-aspirate patch contracts (AC5, AC6) and the cleanliness regex table (AC11) for Story 9.7 to mechanise. To reduce Story 9.7's reverse-engineering cost, the fitness-test stub names + assertion shapes are:

**Test 1: `KustomizationTopologyFitnessTest`**
- Given: `deploy/k8s/kustomization.yaml` exists.
- Then:
  - resources block contains exactly the 7 aspirate folders + `namespace.yaml` (uncommented).
  - `redis` + `keycloak` are present either as uncommented entries (post-Story 9.3) or as commented entries (Story 9.2 → 9.3 transient — checked via AC11 C12).
  - `kubectl kustomize deploy/k8s/ --dry-run=client` exits 0.

**Test 2: `AspirateEmissionByteDeterminismFitnessTest`**
- Given: a clean git tree at the same commit as the committed baseline.
- When: aspirate is run twice with the AC9 invocation contract flags.
- Then: filtered diff (per Subtask 6.3 grep filters) is empty.
- Precondition: same `LC_COLLATE`, same `aspirate --version`, same `dotnet --info` SDK version as captured in Dev Notes "Byte-determinism evidence".

**Test 3: `DaprAnnotationPatchIdempotencyFitnessTest`**
- Given: the post-aspirate Dapr-annotation patch is applied to a freshly-emitted deploy/k8s tree.
- When: the patch is applied a second time.
- Then: zero diff. AND: the patch's name allowlist (5 services: eventstore, eventstore-admin, parties, tenants, memories) matches the AppHost `WithDaprSidecar(...)` callsites exactly — drift in either side fails the test.

**Test 4: `ImagePullSecretsPatchScopeFitnessTest`**
- Given: a freshly-emitted deploy/k8s tree.
- When: the imagePullSecrets patch is applied.
- Then:
  - Every Deployment whose `image:` starts with `registry.hexalith.com/` carries `imagePullSecrets: [{ name: zot-pull-secret }]` at the pod-template level.
  - Vendor-image Deployments (redis, keycloak) have NO `imagePullSecrets`.
  - The idempotency anchor uses exact-match-at-YAML-node, NOT substring (party-mode review F8 — verify the test rejects `name: zot-pull-secret-staging` as an idempotency anchor false-positive).

### Story 9.7 CI invocation contract (party-mode review Quinn E-3 / Murat I1)

When Story 9.7 mechanises `AspirateEmissionByteDeterminismFitnessTest` in CI, the runner must:

1. `git submodule update --init Hexalith.EventStore Hexalith.Tenants Hexalith.Memories` (root-level only).
2. `dotnet tool restore` (consumes the `rollForward: false` pin per D-I4).
3. `dotnet restore Hexalith.Parties.slnx`.
4. `dotnet build src/Hexalith.Parties.AppHost --configuration Release`.
5. `dotnet aspirate generate --skip-build --container-image-tag <fixed-test-tag> --container-registry registry.hexalith.com --non-interactive --disable-state --disable-secrets --output-path /tmp/k8s-pass-N`.
6. NO `docker login` required (because `--skip-build` skips the push path); the daemon must still be reachable per Subtask 1.4 — CI runners need Docker installed (most do; document the requirement in `tests/Hexalith.Parties.DeployValidation.Tests/README.md` when Story 9.7 ships).
7. `PUBLISH_TARGET` env var must be unset (CI default).

### Previous story intelligence — Story 9.1 v2 (closed 2026-05-21)

Story 9.1 v2 introduced these v2 artefacts that this story extends:

1. `deploy/zot/` manifest tree — Zot registry under `registry.hexalith.com`. Not touched by this story; the AppHost composition references `registry.hexalith.com/<service>:<tag>` images that the Zot Pod serves.
2. `deploy/k8s/README.md` — operator entry-point. Not touched here; Story 9.1's roadmap callout already announces that Story 9.2 lands the per-service folders + kustomization. The roadmap callout becomes accurate when this story closes.
3. `docs/kubernetes-deployment-architecture.md` §5.2 tagging policy — the 4-row table forbidding `:latest` / `:staging-latest` / empty / `+dirty` (for ship-bound tags). This story's AC4 + AC11 enforce these forbidden patterns mechanically.
4. ADR D-K8s-2 (Zot Path B pull-secret) + ADR D-K8s-3 (`-ConfirmContext` gate). Both already in `architecture.md`. Not touched here; Story 9.5 mechanizes both.
5. ADR D-K8s-4 (Epic 9 v2 greenfield rewrite). Not touched.

Code review of Story 9.1 v2 surfaced 12 patches across `deploy/zot/`, `deploy/k8s/README.md`, and the doc surface. Key learnings for this story:

- **Single-line F6 grep tripwire**: any canonical-doc pointer must place the lead phrase (`Canonical reference:`, `For the full`, `See [Kubernetes`, `Refer to [Kubernetes`) on the SAME line as `[Kubernetes Deployment Architecture]`. Multi-line blockquote callouts fail the grep. This story does NOT introduce new canonical-doc pointers (deploy/k8s/README.md already has one), so the tripwire doesn't apply here — but the dev should be mindful if introducing new doc surface.
- **Bootstrap namespace ordering**: `kubectl apply -f deploy/zot/` walks alphabetically and fails on clean clusters because manifests apply before `namespace.yaml`. The same issue applies to `kubectl apply -f deploy/k8s/`. The kustomization-based path (`kubectl apply -k deploy/k8s/`) sidesteps this because Kustomize sorts resources by kind (`Namespace` first). **This story uses the Kustomization path exclusively** — `publish.ps1` step 13 (Story 9.5) runs `kubectl apply -k deploy/k8s/`, NOT `kubectl apply -f deploy/k8s/`.
- **Sprint-status audit-line discipline**: every status change appends a `last_updated touched <date> by <skill>` line. This story does the same on close.
- **ADR substring contract pattern (AC6/AC7 of Story 9.1)**: mechanical grep verification of ADR wording — applied to ADR D-K8s-2/D-K8s-3 in Story 9.1; not needed here (this story doesn't author new ADRs; reuses D-K8s-2/D-K8s-3 by reference).

### Git history pointers — recent commits relevant to Epic 9 v2

```
4f84aa8 feat(deploy): Epic 9 v2 greenfield rewrite — wipe v1 artefacts + replan as 7 stories
9c97b8a feat(docs): add Kubernetes deployment architecture documentation
                                                            ← canonical reference this story leans on
```

Plus the as-yet-uncommitted Story 9.1 v2 work (deploy/zot/ tree + deploy/k8s/README.md). The dev should rebase + verify the Story 9.1 v2 commit is on `main` before starting (run `git log --oneline -5` and confirm).

### Project Structure Notes

- The 7 application services live across 4 directories: `src/Hexalith.Parties/`, `src/Hexalith.Parties.Mcp/`, plus 5 sibling-submodule paths (`Hexalith.EventStore/src/Hexalith.EventStore/`, `Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server.Host/`, `Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/`, `Hexalith.Tenants/src/Hexalith.Tenants/`, `Hexalith.Memories/src/Hexalith.Memories.Server/`). All 5 sibling-submodule paths are referenced via `ProjectReference` in `Hexalith.Parties.AppHost.csproj`.
- The `DaprComponents/` subfolder of `Hexalith.Parties.AppHost/` contains 8 YAMLs (`accesscontrol.yaml`, `accesscontrol.eventstore-admin.yaml`, `accesscontrol.parties.yaml`, `accesscontrol.tenants.yaml`, `accesscontrol.memories.yaml`, `pubsub.yaml`, `resiliency.yaml`, `subscription-parties.yaml`, `statestore.yaml`). These are **AppHost-local** files consumed by `ResolveDaprConfigPath(...)` at run time — they are NOT the Story 9.4 hand-authored `deploy/dapr/*.yaml` CRs. The K8s deployment uses the latter (separate file set). This story does NOT touch either.
- The `KeycloakRealms/hexalith-realm.json` file is consumed by `.WithRealmImport("./KeycloakRealms")` in run mode. In publish mode this file is not referenced (because `builder.AddKeycloak(...)` is not called). The file stays on disk for run-mode use.
- The `Hexalith.Parties.AppHost.csproj.lscache` file in the AppHost folder is a VS/Rider-emitted local cache — never check it in (gitignore pattern `*.lscache` covers it).

### Testing standards

This story does NOT author production test code — the fitness suite (`KustomizationTopologyFitnessTest`, `AspirateEmissionByteDeterminismFitnessTest`, `DaprAnnotationPatchIdempotencyFitnessTest`, `ImagePullSecretsPatchScopeFitnessTest`) is delivered by Story 9.7. This story's verifications are manual: AC4/AC10/AC11 greps + AC8 Kustomization smoke + AC9 version pin verification.

**Smoke checks required by this story (not deferred):**

- **Subtask 1.5**: `dotnet build src/Hexalith.Parties.AppHost ... --configuration Release` exits 0.
- **Subtask 2.9**: aspirate-based throwaway emission exits 0 AND the resource set in the generated output contains the 7 app services but NOT `redis` / `keycloak` after generated Dapr CR cleanup.
- **Subtask 4.2**: `dotnet aspirate generate ...` exits 0.
- **Subtask 4.3**: exactly 7 per-service folders exist after generation.
- **Subtask 4.4**: every emitted `image:` line matches `registry.hexalith.com/<app-id>:<MinVer-shaped-tag>`.
- **Task 6**: byte-determinism diff is empty (or image:-line-only).
- **Task 7**: 9/9 cleanliness greps return zero.

### Common LLM mistakes to AVOID in this story

1. **DO NOT** hand-edit the aspirate-emitted YAMLs under `deploy/k8s/<service>/*` to "fix" any AC11 cleanliness violation. Hand-edits break AC10 byte-determinism. Fix the SOURCE (`Program.cs` composition or `--secret-provider` flag), re-emit, re-verify.
2. **DO NOT** remove `redis` / `keycloak` from `Program.cs` outright. Gate them behind `IsRunMode` so local Aspire dev still works.
3. **DO NOT** delete `redis` or `keycloak` lines from the kustomization. Even though the folders don't exist at this story's close (Story 9.3 will land them), the Kustomization references them proactively. A `kubectl kustomize` failure is acceptable at this story's close; the failure resolves when Story 9.3 ships.
4. **DO NOT** add Dockerfiles. The project uses `.NET SDK Container Publish` exclusively (per EventStore CLAUDE.md container convention). Aspirate consumes the `<EnableContainer>` / `<ContainerRepository>` MSBuild properties.
5. **DO NOT** run `dotnet aspirate generate` WITHOUT `--skip-build` in this story. The non-`--skip-build` path builds + pushes 7 images to Zot, which is Story 9.5's responsibility. This story is manifest-shape baseline only.
6. **DO NOT** modify the `PUBLISH_TARGET` block at the bottom of `Program.cs` (lines ~218–244). It is orthogonal to aspirate and gates Aspire-native publishers only.
6a. **DO NOT export `PUBLISH_TARGET=k8s` in your shell when running `dotnet aspirate generate`.** The Aspire-native K8s publisher pipeline and aspirate's manifest-consuming pipeline are orthogonal in principle, but co-activating them shifts env-var resolution and may double-emit. Subtask 4.0.5 asserts the env var is unset before aspirate fires.
7. **DO NOT** touch `_bmad-output/planning-artifacts/architecture.md`, `_bmad-output/planning-artifacts/epics.md`, or `_bmad-output/planning-artifacts/prd.md`. The Epic 9 v2 narrative + ADRs D-K8s/D-K8s-2/D-K8s-3/D-K8s-4 are already in place. This story consumes them; it does not edit them.
8. **DO NOT** initialise nested submodules. Root-level submodules (`Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.Memories`, `Hexalith.FrontComposer`, `Hexalith.AI.Tools`) only. Per project context rule. (Subtask 1.6 makes a cross-submodule edit on `Hexalith.Memories` — that's allowed because Memories is root-level, not nested.)
9. **DO NOT** create or modify any test file under `tests/Hexalith.Parties.DeployValidation.Tests/`. That project is preserved through the 2026-05-21 wipe (csproj + collection class only) — refilling it is Story 9.7's responsibility.
10. **DO NOT use `--secret-provider` on aspirate** — that flag doesn't exist in 9.1.0. Use `--disable-state --disable-secrets` per the corrected AC9 invocation contract (party-mode review F1).
11. **DO NOT use `dotnet msbuild ... -getProperty:Version`** to capture MinVer — MinVer hooks AFTER the Build target, so msbuild's static-property resolution returns the default `1.0.0` (party-mode review F2). Use `dotnet build ... --getProperty:Version` (Build target invokes MinVer first).
12. **DO NOT hand-edit aspirate-emitted `deploy/k8s/<service>/*.yaml` files** to "fix" an AC11 cleanliness violation. Hand-edits break AC10 byte-determinism. Fix the SOURCE (Program.cs composition or `--disable-secrets`/`--disable-state` flag) and re-emit.

### References

- [Source: `docs/kubernetes-deployment-architecture.md` §3.1 Workloads at a glance] — 7 app workloads + 2 carve-outs (redis, keycloak).
- [Source: `docs/kubernetes-deployment-architecture.md` §5.2 Tagging policy] — MinVer tag shape regex + forbidden mutable tags.
- [Source: `docs/kubernetes-deployment-architecture.md` §7 Configuration Sources] — Source 1 (AppHost) + Source 3 (carve-outs) boundary.
- [Source: `docs/kubernetes-deployment-architecture.md` §8 Build & Deploy Flow] — 13-phase publish.ps1; Story 9.2 owns the manifest-shape contract steps 3-7 land against.
- [Source: `docs/kubernetes-deployment-architecture.md` §11 Reproducibility Guarantees] — byte-determinism contract (AC10).
- [Source: `_bmad-output/planning-artifacts/architecture.md` ADR D-K8s lines 475-488] — Aspirate-from-Aspire-model decision.
- [Source: `_bmad-output/planning-artifacts/architecture.md` ADR D-K8s-2 lines 490-509] — Zot pull-secret Path B + parties-publisher account.
- [Source: `_bmad-output/planning-artifacts/architecture.md` ADR D-K8s-3 lines 511-525] — `-ConfirmContext` gate (no Story 9.2 implementation surface; reference only).
- [Source: `_bmad-output/planning-artifacts/epics.md` lines 3187-3240] — Epic 9 v2 Story 9.2 AC verbatim contract.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-21-epic9-greenfield-rewrite.md` §4.2 + §4.5 + Appendix A Story 9.2] — SCP approving the story shape.
- [Source: `src/Hexalith.Parties.AppHost/Program.cs` lines 1-273] — current AppHost composition (the file this story refactors).
- [Source: `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` line 1] — `Aspire.AppHost.Sdk/13.3.3` pin.
- [Source: `.config/dotnet-tools.json`] — aspirate 9.1.0 pin.
- [Source: `Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:123,150`] — canonical `IsPublishMode` gating pattern that this story replicates at the Parties-AppHost layer.
- [Source: `_bmad-output/implementation-artifacts/9-1-zot-oci-registry-and-deployment-documentation.md`] — predecessor v2 story; introduced `deploy/k8s/README.md` + `deploy/zot/` manifest tree.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md` "Deferred from: code review of 9-2-extend-deployment-validation-to-kubernetes-manifests (2026-05-18)"] — v1 deferred items. Most are V1-specific (lint rules, regen.ps1) and don't carry forward; the byte-determinism test deferral IS relevant — Story 9.7 owns it.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet` was installed at `/home/quentindv/.dotnet/dotnet`, not on `PATH`; all .NET commands used that binary.
- Aspirate 9.1.0 targets `net9.0`; this workstation has .NET 10 runtimes only, so commands used `DOTNET_ROLL_FORWARD=Major`.
- `dotnet aspirate generate` from repo root failed project discovery; successful runs executed from `src/Hexalith.Parties.AppHost` with `--project-path Hexalith.Parties.AppHost.csproj`.
- Aspirate 9.1.0 non-interactive mode required two extra explicit options beyond the story draft: `--include-dashboard false` and `--image-pull-policy IfNotPresent`.
- EventStore/Tenants submodule `Directory.Build.targets` defaulted images to `staging-latest` unless MSBuild property `ContainerImageTags` was set; successful emission exported `ContainerImageTags=0.1.1-preview.0.3`.
- `kubectl kustomize --dry-run=client` is unsupported by the installed kubectl; validation used `kubectl kustomize deploy/k8s/`.
- Full solution regression run did not pass due to failures outside this story's deployment/AppHost surface; see Completion Notes.

### Completion Notes List

- AppHost publish-mode composition now excludes `redis` and `keycloak`, preserves run-mode local Aspire composition, wires publish-mode JWT/admin UI authority to `KeycloakRealmUrlInCluster`, and keeps Memories search enabled in publish mode.
- Kept `Parties__Mcp__EventStoreGatewayBaseUrl` on the Aspire `ReferenceExpression` path using the EventStore HTTP endpoint; aspirate resolves it to `http://eventstore:8080` in the generated ConfigMap.
- Added container publish opt-in for `parties`, `parties-mcp`, and `memories`.
- Generated the aspirate baseline under `deploy/k8s/` with MinVer image tag `0.1.1-preview.0.3`.
- Verified `kubectl kustomize deploy/k8s/` exit 0, byte-determinism raw/filtered diff 0 lines, and AC11 C1-C12 all at expected values.
- Updated AppHost topology fitness tests for Story 9.2 wiring; focused `AppHostTenantsTopologyTests` passed 12/12.
- Full `dotnet test Hexalith.Parties.slnx --configuration Release --no-restore` remains red on unrelated failures: two sample documentation path tests using Windows separators on Linux, two client tests unrelated to deployment work, and timing-sensitive search benchmark failures. Per explicit user waiver on 2026-05-21, Story 9.2 is moved to `review` because the Kubernetes/AppHost acceptance surface is green.

### File List

**Created (expected):**

- `deploy/k8s/namespace.yaml`
- `deploy/k8s/kustomization.yaml`
- `deploy/k8s/eventstore/deployment.yaml` (aspirate-emitted)
- `deploy/k8s/eventstore/service.yaml` (aspirate-emitted)
- `deploy/k8s/eventstore/kustomization.yaml` (aspirate-emitted)
- `deploy/k8s/eventstore-admin/deployment.yaml` (aspirate-emitted)
- `deploy/k8s/eventstore-admin/service.yaml` (aspirate-emitted)
- `deploy/k8s/eventstore-admin/kustomization.yaml` (aspirate-emitted)
- `deploy/k8s/eventstore-admin-ui/deployment.yaml` (aspirate-emitted)
- `deploy/k8s/eventstore-admin-ui/service.yaml` (aspirate-emitted)
- `deploy/k8s/eventstore-admin-ui/kustomization.yaml` (aspirate-emitted)
- `deploy/k8s/memories/deployment.yaml` (aspirate-emitted)
- `deploy/k8s/memories/service.yaml` (aspirate-emitted)
- `deploy/k8s/memories/kustomization.yaml` (aspirate-emitted)
- `deploy/k8s/parties/deployment.yaml` (aspirate-emitted)
- `deploy/k8s/parties/service.yaml` (aspirate-emitted)
- `deploy/k8s/parties/kustomization.yaml` (aspirate-emitted)
- `deploy/k8s/parties-mcp/deployment.yaml` (aspirate-emitted)
- `deploy/k8s/parties-mcp/service.yaml` (aspirate-emitted)
- `deploy/k8s/parties-mcp/kustomization.yaml` (aspirate-emitted)
- `deploy/k8s/tenants/deployment.yaml` (aspirate-emitted)
- `deploy/k8s/tenants/service.yaml` (aspirate-emitted)
- `deploy/k8s/tenants/kustomization.yaml` (aspirate-emitted)

**Modified (expected):**

- `src/Hexalith.Parties.AppHost/Program.cs` — gate `AddRedis` + `AddKeycloak` behind `IsRunMode`; introduce `KeycloakRealmUrlInCluster` constant with `PUBLISH-MODE-DNS-ANCHOR` magic comment; extract `WithJwtAuthority(...)` helper for the 5-service JWT wiring (Subtask 2.5); 3-state wiring for adminUI (Subtask 2.7); EnableMemoriesSearch always-on in publish mode (Subtask 2.8).
- `src/Hexalith.Parties/Hexalith.Parties.csproj` — add `<EnableContainer>true</EnableContainer>` + `<ContainerRepository>parties</ContainerRepository>` (Subtask 1.6).
- `src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj` — add `<EnableContainer>true</EnableContainer>` + `<ContainerRepository>parties-mcp</ContainerRepository>` (Subtask 1.6).
- `Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj` — cross-submodule edit: add `<EnableContainer>true</EnableContainer>` + `<ContainerRepository>memories</ContainerRepository>`.
- `.config/dotnet-tools.json` — flip `"rollForward": true` → `"rollForward": false` (Subtask 5.1 per D-I4).
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` — update AppHost topology fitness expectations for sanitized registration key and Story 9.2 publish-mode JWT wiring.
- `_bmad-output/implementation-artifacts/9-2-aspire-apphost-to-aspirate-manifest-composition.md` — checkboxes and Dev Agent Record evidence.

### Change Log

| Date | Author | Change |
|---|---|---|
| 2026-05-21 | bmad-create-story (Opus 4.7) | Story 9.2 v2 ready-for-dev — AppHost refactor (publish-mode-only carve-out exclusion via `IsRunMode` gating, `KeycloakRealmUrlInCluster` constant, dual-path env-var wiring); `deploy/k8s/namespace.yaml` + `kustomization.yaml`; aspirate one-shot emission baseline + AC10 byte-determinism contract + AC11 9-grep cleanliness sweep; 3 patch contracts documented for Story 9.5 mechanization (Dapr annotations, JWT secretKeyRef, imagePullSecrets); 4 Investigation Items (I1 aspirate `--skip-build`, I2 adminUI auth in cluster, I3 container build path, I4 aspirate version `rollForward`). |
| 2026-05-21 | bmad-party-mode (Winston + Amelia + Murat + Dr. Quinn, 4-agent parallel review) | **21 patches applied** before dev-story starts. Defect-class fixes: **F1** drop `--secret-provider kubernetes` (does not exist in aspirate 9.1.0); **F2** switch MinVer resolution from `dotnet msbuild -getProperty:Version` (returns `1.0.0`) to `dotnet build --getProperty:Version` (MinVer fires in Build target); **F3** replace `--publisher manifest` smoke (Aspire 13.3.3-broken shape) with `dotnet aspirate generate --output-path /tmp/throwaway` smoke; **F4** AC11 C9 PCRE lookahead → two-grep pipeline shape (matches Subtask 7.9); **F5** AC11 C2 empty-tag regex now anchors to `:[[:space:]]*$` (bare-EOL form aspirate actually emits); **F6** AC11 C8 double-backslash typo → single backslash; **F7** AC10 diff-filter `(<|>)` → unified-diff `(-|+)` + header strip; **F8** AC6 imagePullSecrets idempotency anchor moved from substring to YAML-node-exact-match (rejects `zot-pull-secret-staging` prefix collision); **F9** AC5 Dapr-annotation detection switched from "no daprd container" (infeasible — aspirate doesn't emit daprd) to explicit name allowlist + upsert-safe idempotency; **F10** Subtask 2.5 preserves 3-state semantics (run+keycloak / publish / run-no-keycloak) for adminUI; **F11** promoted I3 to hard Subtask 1.6 — add `<EnableContainer>` + `<ContainerRepository>` to 3 missing csprojs (cross-submodule edit on Memories); **F12** `aspirate-state.json` decision pinned to strip (via `--disable-state`); **F13** AC8 + Subtask 3.x kustomize three-way contradiction resolved — carve-out lines committed as commented; `kubectl kustomize --dry-run=client` exits 0 at story close; **F14** `rollForward: true` → `false` in `.config/dotnet-tools.json` (byte-determinism vs version-drift conflict); **F15** Investigation Items I1-I4 reframed as resolved decisions D-I1 through D-I4; **F16** added `+dirty` grep enforcement to AC4 + Subtask 4.4; **F17** `EnableMemoriesSearch` always-on in publish mode (was run-mode-only); **F18** added `PUBLISH-MODE-DNS-ANCHOR` magic comment + AC11 C10/C11/C12 anchors; **F19** broadened C7 to cover `127.0.0.1`/`0.0.0.0`/`[::1]`; **F20** `PUBLISH_TARGET=k8s` foot-gun guard (Subtask 4.0.5 + Common-Mistakes #6a); **F21** added Story 9.7 fitness-test stubs (Given/When/Then per `KustomizationTopologyFitnessTest`, `AspirateEmissionByteDeterminismFitnessTest`, `DaprAnnotationPatchIdempotencyFitnessTest`, `ImagePullSecretsPatchScopeFitnessTest`) + Story 9.7 CI invocation contract. Improvements applied: `WithJwtAuthority` helper extraction (R6), Subtask 1.5 `dotnet restore Hexalith.Parties.slnx` prerequisite (Amelia I4), `find -mindepth 1 -maxdepth 1` portability for Subtask 4.5 (R10), OCI repo-name `[A-Za-z0-9._-]` (R8). Deferred to deferred-work.md: Quinn Q-1 (split-AppHost), Quinn Q-2 (Story 9.5 keycloak-URL patch contract #4 instead of AppHost constant). Cleanliness sweep grew from 9 rows to 12. The story now ships a manifest-shape baseline + a verifiable patch-contract handoff for Story 9.5/9.7. |
| 2026-05-21 | bmad-dev-story (Codex) | Implemented Story 9.2 v2 through AC11 evidence: AppHost publish/run-mode split, container opt-in, aspirate baseline with image tag `0.1.1-preview.0.3`, kustomization/namespace, byte-determinism diff 0, cleanliness C1-C12 green, and focused AppHost topology tests green. Full-suite regression gate remains red on unrelated failures; explicit user waiver accepted for Story 9.2 review handoff. |
