# Story 9.3: Hand-Authored Carve-Outs (Redis + Keycloak)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer maintaining the deployment topology,
I want Redis (Dapr state + pubsub backing store) and Keycloak (OIDC issuer) to live as hand-authored manifests under `deploy/k8s/redis/` and `deploy/k8s/keycloak/` outside the Aspirate composition,
so that intentional MVP deviations from Aspire's defaults (Redis `emptyDir` + no AUTH; Keycloak randomized admin password from operator-managed Secret) are explicit, reviewable, and survive every `publish.ps1` regeneration.

## Scope & Non-Scope

**This story delivers:**

- New hand-authored `deploy/k8s/redis/` manifests: `deployment.yaml`, `service.yaml`, and `kustomization.yaml` if local folder-level Kustomizations remain the convention.
- New hand-authored `deploy/k8s/keycloak/` manifests: `deployment.yaml`, `service.yaml`, `configmap.yaml`, and `kustomization.yaml` if local folder-level Kustomizations remain the convention.
- `deploy/k8s/kustomization.yaml` update: uncomment `redis` and `keycloak` resources after the folders exist.
- `deploy/k8s/README.md` update: remove stale "folder contains only this README" wording and mark Stories 9.2 and 9.3 entries as present.
- A cross-check that `src/Hexalith.Parties.AppHost/Program.cs` `KeycloakRealmUrlInCluster` still matches the committed Keycloak Service name, namespace, port, and realm.
- Manual verification that `kubectl kustomize deploy/k8s/` exits 0 and that no Dapr annotations, `imagePullSecrets`, Zot image references, or passwords are introduced into vendor carve-outs.
- Self-contained folder-level Kustomize validation for `deploy/k8s/redis/` and `deploy/k8s/keycloak/` without special load-restrictor flags.

**This story does not deliver:**

| Out-of-scope | Owned by |
|---|---|
| `deploy/k8s/publish.ps1`, `teardown.ps1`, `_lib/Confirm-KubeContext.ps1`, clean-phase implementation, Secret bootstrap implementation | Story 9.5 |
| Dapr Components, ACL Configurations, Subscriptions, and Resiliency CRs under `deploy/dapr/` | Story 9.4 |
| The Redis Dapr Component referencing `redis:6379` and omitting `redisPassword` | Story 9.4 |
| Static lint tooling in `deploy/validate-deployment.ps1` | Story 9.6 |
| Production test code such as `CarveOutPreservationFitnessTest` | Story 9.7 |
| Production hardening for Redis: PVC, StatefulSet, replication, AUTH, resource envelopes | Deferred per `docs/kubernetes-deployment-architecture.md` section 12 |
| Keycloak production mode, database-backed HA Keycloak, external Ingress, TLS termination, or hostname hardening | Deferred per MVP boundaries |

## Acceptance Criteria

### AC1 - Carve-out folders exist and are wired into Kustomization

- Given the configuration-source taxonomy in `docs/kubernetes-deployment-architecture.md` section 7 Source 3,
- When the `deploy/k8s/` tree is inspected after this story,
- Then `deploy/k8s/redis/` and `deploy/k8s/keycloak/` exist as hand-authored folders.
- And `deploy/k8s/redis/` contains `deployment.yaml` and `service.yaml`.
- And `deploy/k8s/keycloak/` contains `deployment.yaml`, `service.yaml`, and `configmap.yaml`.
- And if the existing per-folder convention from the Aspirate folders is preserved, each carve-out folder also contains a minimal local `kustomization.yaml` that references only that folder's files.
- And `deploy/k8s/kustomization.yaml` references both `redis` and `keycloak` as uncommented resources alongside the seven Aspirate-emitted application folders.
- And the top-level Kustomization preserves the seven Story 9.2 application resources; the only intended resource-line change is flipping the two existing `# - redis` / `# - keycloak` anchors to active resources.
- And neither `redis` nor `keycloak` is added back to publish-mode composition in `src/Hexalith.Parties.AppHost/Program.cs`; they remain run-mode Aspire resources only.
- And no Aspirate-emitted application service folder is regenerated, moved, reformatted, or hand-edited by this story.

### AC2 - Redis is an explicit MVP backing store

- Given production-grade storage is explicitly outside MVP scope,
- When `deploy/k8s/redis/deployment.yaml` is inspected,
- Then it uses a pinned public vendor image. At story creation time the current Docker official Redis tag is `redis:8.6.3`; verify at implementation time and pin a concrete exact version tag, never `redis:latest`.
- And digest pinning is not required in this story; an exact vendor version tag is the intended MVP pinning level.
- And it does not reference `registry.hexalith.com/` and has no `imagePullSecrets`.
- And persistence uses `emptyDir: {}` only: no PersistentVolumeClaim, no StatefulSet, no `volumeClaimTemplates`.
- And authentication is disabled: no `--requirepass`, no password environment variable, and no Secret reference of any kind.
- And the container exposes port `6379`.
- And `deploy/k8s/redis/service.yaml` defines a `ClusterIP` Service named `redis`, selecting the Redis pod and exposing port `6379`.
- And the Deployment carries an inline comment block of 5-10 lines stating the MVP boundary: emptyDir, no AUTH, state is lost on Pod restart, production hardening is deferred to architecture section 12.
- And `deploy/k8s/README.md` explicitly calls this Redis shape non-production/MVP-only and points production persistence, AUTH, network policy, and HA hardening to architecture section 12.

### AC3 - Keycloak uses current bootstrap admin variables and never commits credentials

- Given Keycloak is a vendor image with operator-managed bootstrap credentials,
- When `deploy/k8s/keycloak/deployment.yaml` is inspected,
- Then it uses a pinned public vendor image. At story creation time the latest official Keycloak release is 26.6.2; verify at implementation time and pin a concrete exact version tag such as `quay.io/keycloak/keycloak:26.6.2`, never `:latest`.
- And digest pinning is not required in this story; an exact vendor version tag is the intended MVP pinning level.
- And it does not reference `registry.hexalith.com/` and has no `imagePullSecrets`.
- And it uses the current Keycloak 26.x bootstrap admin variables:
  - `KC_BOOTSTRAP_ADMIN_USERNAME`
  - `KC_BOOTSTRAP_ADMIN_PASSWORD`
- And both values come from Secret `hexalith-keycloak-admin` via `secretKeyRef`.
- And this story commits no Secret manifest, no Kustomize `secretGenerator`, no placeholder Secret, and no env file containing admin credentials or Secret payloads; `hexalith-keycloak-admin` is an external prerequisite until Story 9.5 bootstraps it.
- And the expected Secret keys are documented for Story 9.5 bootstrap and for any temporary manual precreation note in `deploy/k8s/README.md`:
  - `KC_BOOTSTRAP_ADMIN_USERNAME` - fixed value such as `admin`, generated/applied by `publish.ps1`.
  - `KC_BOOTSTRAP_ADMIN_PASSWORD` - random 24-byte value generated on first publish and preserved on re-publish.
- And if the README mentions temporary manual precreation before Story 9.5 exists, it must use a command shape that does not print or commit secret values, e.g. `kubectl create secret generic hexalith-keycloak-admin --from-literal=KC_BOOTSTRAP_ADMIN_USERNAME=admin --from-literal=KC_BOOTSTRAP_ADMIN_PASSWORD='<prompted-or-shell-private-value>' -n hexalith-parties`, with a warning not to put real values in docs, shell history, or manifests.
- And deprecated variables `KEYCLOAK_ADMIN` and `KEYCLOAK_ADMIN_PASSWORD` are not used unless the dev verifies a newer Keycloak image has changed the contract again and documents the reason in Dev Notes.
- And no literal password, token, admin secret, base64 docker auth, or JWT-shaped value appears in the manifest.
- And the Deployment carries an inline comment block stating: bootstrap password is created by `publish.ps1`, never echoed, never committed, and rotated by deleting Secret `hexalith-keycloak-admin` and re-running `publish.ps1`.

### AC4 - Keycloak realm import is explicit and matches AppHost publish-mode DNS

- Given Keycloak realm configuration must be explicit,
- When `deploy/k8s/keycloak/configmap.yaml` and `deployment.yaml` are inspected,
- Then the realm `hexalith` is imported from a ConfigMap-mounted JSON file at `/opt/keycloak/data/import/hexalith-realm.json`.
- And the committed `deploy/k8s/keycloak/configmap.yaml` is self-contained: it embeds the realm JSON data directly in the ConfigMap and does not depend on a Kustomize reference to `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json` outside the `deploy/k8s/keycloak/` tree.
- And the source JSON is copied from the existing run-mode realm at `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json` unless the dev intentionally removes sample users/passwords and documents the decision.
- And `deploy/k8s/keycloak/kustomization.yaml`, if present, references only local files inside `deploy/k8s/keycloak/` so `kubectl kustomize deploy/k8s/keycloak/` works without `--load-restrictor` or other special flags.
- And the Deployment mounts the ConfigMap as a volume at `/opt/keycloak/data/import/hexalith-realm.json` using `subPath: hexalith-realm.json`.
- And the Keycloak container args are exactly `start-dev` and `--import-realm` for MVP scope unless current Keycloak docs require a corrected equivalent, in which case the dev must document the change in Dev Agent Record.
- And the Service name is exactly `keycloak`, the deployment is namespaced by the top-level Kustomization namespace `hexalith-parties` rather than per-resource `metadata.namespace`, the Service exposes port `8080`, and the realm name is exactly `hexalith`.
- And the manifest header comment documents the OIDC issuer URL:
  `http://keycloak.hexalith-parties.svc.cluster.local:8080/realms/hexalith`
- And this exact URL still matches `KeycloakRealmUrlInCluster` in `src/Hexalith.Parties.AppHost/Program.cs`.
- And the preferred implementation keeps Service name, namespace, port, and realm aligned with the existing AppHost constant; do not change the AppHost constant in this story unless a mismatch is proven and the Service contract intentionally changes.
- And if the Service name, port, namespace, or realm intentionally changes, update the AppHost constant in the same commit and keep the `PUBLISH-MODE-DNS-ANCHOR` comment.

### AC5 - Carve-outs are preserved by the future publish clean phase

- Given Story 9.5 owns the `publish.ps1` implementation,
- When this story updates static manifests and handoff notes,
- Then the files and README make the preservation contract unambiguous:
  - preserve `deploy/k8s/redis/`
  - preserve `deploy/k8s/keycloak/`
  - preserve `deploy/k8s/kustomization.yaml`
  - preserve `deploy/k8s/namespace.yaml`
  - preserve `deploy/k8s/README.md`
  - preserve `deploy/k8s/publish.ps1`
  - preserve `deploy/k8s/teardown.ps1`
- And all other files/folders under `deploy/k8s/` are eligible for removal before Aspirate regeneration in Story 9.5, except the seven regenerated application folders after generation.
- And the story records this whitelist in Dev Notes or README so Story 9.5 does not reverse-engineer it.
- And running `git diff -- deploy/k8s/redis deploy/k8s/keycloak` after two no-op Kustomize validations remains empty; vendor carve-outs are byte-stable because they are not MinVer-stamped.

### AC6 - Namespace and patch boundaries are enforced

- Given the topology is a single namespace MVP deployment,
- When Redis and Keycloak Deployments are inspected,
- Then both are deployed through `deploy/k8s/kustomization.yaml` into namespace `hexalith-parties`.
- And individual Redis and Keycloak resources do not set `metadata.namespace`; namespace ownership remains centralized in the top-level Kustomization.
- And neither Deployment carries Dapr annotations (`dapr.io/enabled`, `dapr.io/app-id`, `dapr.io/config`, `dapr.io/app-port`).
- And neither Deployment contains a `daprd` container.
- And neither Deployment has `imagePullSecrets`.
- And neither Deployment references `zot-pull-secret`.
- And both are skipped by the future Story 9.5 post-Aspirate Dapr annotation and imagePullSecrets patch allowlists.

### AC7 - Verification commands prove the committed tree is deployable

- Given the two carve-out folders are committed,
- When `kubectl kustomize deploy/k8s/` runs from the repo root,
- Then it exits 0.
- And `kubectl kustomize deploy/k8s/redis/` and `kubectl kustomize deploy/k8s/keycloak/` each exit 0 without special flags.
- And the rendered output contains exactly one Redis Deployment, one Redis Service, one Keycloak Deployment, one Keycloak Service, and one Keycloak ConfigMap.
- And the following cleanliness checks return zero lines:
  ```bash
  grep -rEn 'registry\.hexalith\.com/' deploy/k8s/redis deploy/k8s/keycloak
  grep -rEn 'imagePullSecrets|zot-pull-secret' deploy/k8s/redis deploy/k8s/keycloak
  grep -rEn 'dapr\.io/|name:[[:space:]]*daprd' deploy/k8s/redis deploy/k8s/keycloak
  grep -rEn 'KEYCLOAK_ADMIN|KEYCLOAK_ADMIN_PASSWORD' deploy/k8s/keycloak
  grep -rEn 'Password:|Bearer eyJ|auths[[:space:]]*:' deploy/k8s/redis deploy/k8s/keycloak
  grep -rEn 'redis:[[:space:]]*latest|keycloak:[[:space:]]*latest|:latest\b' deploy/k8s/redis deploy/k8s/keycloak
  git diff --name-only -- deploy/k8s/eventstore deploy/k8s/eventstore-admin deploy/k8s/eventstore-admin-ui deploy/k8s/memories deploy/k8s/parties deploy/k8s/parties-mcp deploy/k8s/tenants
  ```
- And these topology checks return the expected values:
  ```bash
  grep -E '^[[:space:]]*-[[:space:]]*(redis|keycloak)\b' deploy/k8s/kustomization.yaml | wc -l
  # expected: 2

  grep -E '^[[:space:]]*#[[:space:]]*-[[:space:]]*(redis|keycloak)\b' deploy/k8s/kustomization.yaml | wc -l
  # expected: 0

  grep -c 'PUBLISH-MODE-DNS-ANCHOR' src/Hexalith.Parties.AppHost/Program.cs
  # expected: >= 1
  ```

### AC8 - README reflects current state

- Given `deploy/k8s/README.md` is the operator entry-point,
- When this story is complete,
- Then it no longer says the folder contains only the README or that manifests are intentionally empty.
- And its roadmap table still points to the canonical architecture doc but shows:
  - Story 9.1 Zot is delivered.
  - Story 9.2 seven Aspirate service folders, `namespace.yaml`, and `kustomization.yaml` are delivered.
  - Story 9.3 `redis/` and `keycloak/` carve-outs are delivered.
  - Story 9.4 Dapr CRs and Story 9.5 scripts remain forward references.
- And it explains that `redis/` and `keycloak/` are hand-authored carve-outs intentionally outside the Aspirate-generated app folders.
- And it labels Redis `emptyDir` + no AUTH as MVP/non-production, with production persistence, AUTH, network policy, and HA deferred to `docs/kubernetes-deployment-architecture.md` section 12.
- And it states that `hexalith-keycloak-admin` is not committed as YAML; Story 9.5 bootstraps it, and any temporary manual creation must avoid committing or printing real secret values.
- And the README must not duplicate the full topology table; keep `docs/kubernetes-deployment-architecture.md` as the canonical reference.

## Tasks / Subtasks

- [x] Task 1 - Preflight and source verification (AC: 1, 4, 6)
  - [x] Subtask 1.1 - Confirm Story 9.2 is present: seven application folders exist and `deploy/k8s/kustomization.yaml` has commented `# - redis` and `# - keycloak` lines.
  - [x] Subtask 1.2 - Confirm publish-mode AppHost does not compose Redis or Keycloak by checking `builder.ExecutionContext.IsRunMode` around `AddRedis("redis")` and `AddKeycloak("keycloak", 8180)`.
  - [x] Subtask 1.3 - Confirm `KeycloakRealmUrlInCluster` equals `http://keycloak.hexalith-parties.svc.cluster.local:8080/realms/hexalith`.
  - [x] Subtask 1.4 - Verify current pinned vendor versions from official sources; update the chosen versions in Dev Agent Record if they differ from this story's baseline.

- [x] Task 2 - Add Redis carve-out manifests (AC: 1, 2, 5, 6, 7)
  - [x] Subtask 2.1 - Create `deploy/k8s/redis/deployment.yaml` as a hand-authored Deployment named `redis`, with labels matching its Service selector.
  - [x] Subtask 2.2 - Use a concrete public Redis image tag, no Zot registry, no `imagePullSecrets`, no auth arguments, and `imagePullPolicy: IfNotPresent`.
  - [x] Subtask 2.3 - Add `emptyDir: {}` volume usage; do not add PVC, StatefulSet, replication, or AUTH.
  - [x] Subtask 2.4 - Add the required MVP boundary comment block in the Deployment.
  - [x] Subtask 2.5 - Create `deploy/k8s/redis/service.yaml` as a `ClusterIP` Service named `redis` exposing port 6379.
  - [x] Subtask 2.6 - If preserving the per-folder convention, create `deploy/k8s/redis/kustomization.yaml` referencing the Redis files.

- [x] Task 3 - Add Keycloak carve-out manifests (AC: 1, 3, 4, 5, 6, 7)
  - [x] Subtask 3.1 - Create self-contained `deploy/k8s/keycloak/configmap.yaml` containing `hexalith-realm.json`, copied from `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json` unless intentionally sanitized. Do not reference the source file from Kustomize.
  - [x] Subtask 3.2 - Create `deploy/k8s/keycloak/deployment.yaml` using a concrete `quay.io/keycloak/keycloak:<version>` image, no Zot registry, no `imagePullSecrets`, and no literal credentials.
  - [x] Subtask 3.3 - Wire `KC_BOOTSTRAP_ADMIN_USERNAME` and `KC_BOOTSTRAP_ADMIN_PASSWORD` through `secretKeyRef` to Secret `hexalith-keycloak-admin`.
  - [x] Subtask 3.4 - Mount the realm ConfigMap at `/opt/keycloak/data/import/hexalith-realm.json` via `subPath: hexalith-realm.json` and configure args exactly `start-dev` and `--import-realm` unless current Keycloak docs require a corrected equivalent.
  - [x] Subtask 3.5 - Create `deploy/k8s/keycloak/service.yaml` as a `ClusterIP` Service named `keycloak` exposing port 8080.
  - [x] Subtask 3.6 - Add the required Keycloak credential-rotation and OIDC issuer comments.
  - [x] Subtask 3.7 - If preserving the per-folder convention, create `deploy/k8s/keycloak/kustomization.yaml` referencing the Keycloak files.

- [x] Task 4 - Wire carve-outs into top-level Kustomization (AC: 1, 4, 7)
  - [x] Subtask 4.1 - Update `deploy/k8s/kustomization.yaml`: uncomment only the existing `redis` and `keycloak` anchors.
  - [x] Subtask 4.2 - Keep `namespace.yaml` and the seven application folders in the resources list; do not regenerate, reorder unnecessarily, or hand-edit Aspirate folders.
  - [x] Subtask 4.3 - Preserve stable ordering: `namespace.yaml`, seven app folders alphabetically, then `redis`, `keycloak` or a documented alphabetical list. Do not remove comments unless they become misleading.
  - [x] Subtask 4.4 - Run the AC7 kustomization checks and record outputs.

- [x] Task 5 - Update operator README (AC: 5, 8)
  - [x] Subtask 5.1 - Remove stale text that says `deploy/k8s/` contains only the README or empty manifests.
  - [x] Subtask 5.2 - Update the roadmap/status table so Stories 9.2 and 9.3 are marked delivered and Stories 9.4 and 9.5 remain forward references.
  - [x] Subtask 5.3 - Add or preserve the Story 9.5 clean-phase preservation whitelist without duplicating the canonical topology table.
  - [x] Subtask 5.4 - Add explicit README notes that Redis is MVP/non-production and that Keycloak admin Secret creation is external until Story 9.5.
  - [x] Subtask 5.5 - Verify the README still links to `docs/kubernetes-deployment-architecture.md` as the canonical reference and does not print secrets.

- [x] Task 6 - Cleanliness and regression verification (AC: 2, 3, 6, 7)
  - [x] Subtask 6.1 - Run all AC7 grep checks; fix any non-zero output.
  - [x] Subtask 6.2 - Run `kubectl kustomize deploy/k8s/`, `kubectl kustomize deploy/k8s/redis/`, and `kubectl kustomize deploy/k8s/keycloak/`; all must exit 0.
  - [x] Subtask 6.3 - Verify the rendered resource kinds/names for Redis and Keycloak are present exactly once.
  - [x] Subtask 6.4 - Verify `git diff --name-only` shows no changes under the seven Aspirate-generated app service folders.
  - [x] Subtask 6.5 - Run a focused `git diff --check`.
  - [x] Subtask 6.6 - Do not run full solution tests unless code or csproj files are unexpectedly touched; this story is YAML/docs only unless drift forces an AppHost constant update.

### Review Findings

- [x] [Review][Patch] Keycloak realm/audience coverage is ambiguous [deploy/k8s/keycloak/configmap.yaml:21]
- [x] [Review][Patch] README Keycloak Secret bootstrap guidance is unsafe/incomplete [deploy/k8s/README.md:108]
- [x] [Review][Patch] Keycloak issuer URL is not documented in the manifest header [deploy/k8s/keycloak/deployment.yaml:1]
- [x] [Review][Patch] README roadmap omits the delivered Story 9.1 Zot status [deploy/k8s/README.md:70]
- [x] [Review][Patch] Redis and Keycloak pods should disable default service account token mounting [deploy/k8s/redis/deployment.yaml:19]

## Dev Notes

### Current State to Preserve

- `deploy/k8s/` currently contains the seven Aspirate-emitted application service folders from Story 9.2 plus `namespace.yaml`, `kustomization.yaml`, and `README.md`.
- `deploy/k8s/kustomization.yaml` currently has `# - redis` and `# - keycloak` commented out. This story flips those two lines to uncommented only after creating the folders.
- The existing per-service folders are Aspirate-owned. Do not hand-edit `deploy/k8s/eventstore*`, `deploy/k8s/parties*`, `deploy/k8s/tenants`, or `deploy/k8s/memories` to satisfy this story.
- `src/Hexalith.Parties.AppHost/Program.cs` currently gates `AddRedis("redis")` and `AddKeycloak("keycloak", 8180)` behind run mode and hard-codes the publish-mode issuer URL through `KeycloakRealmUrlInCluster`.
- The AppHost constant is intentionally coupled to the Keycloak Service this story owns. The story must cross-check it; update it only if the service shape changes.
- `deploy/k8s/README.md` still contains some Story 9.1 forward-reference wording saying this folder contains only the README. That was true before Story 9.2 and is stale now. Update it as part of this story.

### File Structure Requirements

Expected created files:

- `deploy/k8s/redis/deployment.yaml`
- `deploy/k8s/redis/service.yaml`
- `deploy/k8s/redis/kustomization.yaml` if preserving local folder Kustomizations
- `deploy/k8s/keycloak/deployment.yaml`
- `deploy/k8s/keycloak/service.yaml`
- `deploy/k8s/keycloak/configmap.yaml`
- `deploy/k8s/keycloak/kustomization.yaml` if preserving local folder Kustomizations

The `keycloak/configmap.yaml` must embed the realm JSON directly so folder-level Kustomize remains self-contained. Do not use `configMapGenerator.files` pointing at `../../../src/...`; default Kustomize load restrictions can reject that path.

Expected modified files:

- `deploy/k8s/kustomization.yaml`
- `deploy/k8s/README.md`
- `src/Hexalith.Parties.AppHost/Program.cs` only if the Keycloak Service name, namespace, port, or realm is intentionally changed. The preferred implementation keeps the existing shape and leaves this file untouched.

Forbidden edits:

- Do not modify Aspirate-emitted app service YAML to "make room" for Redis or Keycloak.
- Do not add Redis or Keycloak back to AppHost publish mode.
- Do not create `deploy/dapr/` CRs in this story.
- Do not create or edit `deploy/k8s/publish.ps1` or `teardown.ps1` in this story.
- Do not add Dockerfiles.
- Do not initialize nested submodules.

### Manifest Conventions

- Keep YAML small, explicit, and hand-reviewable.
- Prefer `app: <name>` labels to match the existing Aspirate-emitted folder style.
- Omit `metadata.namespace` from individual Redis and Keycloak resources; the top-level Kustomization applies `namespace: hexalith-parties`.
- Use `imagePullPolicy: IfNotPresent` for pinned vendor images.
- Use concrete image tags only. Never use `latest`.
- Exact version tags are enough for this MVP story; digest pinning is deferred unless the operator explicitly asks for it during implementation.
- Do not add Dapr annotations to Redis or Keycloak.
- Do not add `imagePullSecrets` to Redis or Keycloak.
- Avoid `stringData`, `data`, `secretGenerator`, env files, or any Secret manifests in this story; operator-managed Secrets are imperative and belong to Story 9.5.
- Redis and Keycloak resources should not set `metadata.namespace`; inherit `hexalith-parties` from the top-level Kustomization.
- Folder-level Kustomizations must build standalone with `kubectl kustomize deploy/k8s/redis/` and `kubectl kustomize deploy/k8s/keycloak/`.

### Keycloak Current-Docs Correction

The Epic AC text uses `KEYCLOAK_ADMIN` and `KEYCLOAK_ADMIN_PASSWORD`. Official Keycloak 26.x documentation now deprecates those names and instructs operators to use `KC_BOOTSTRAP_ADMIN_USERNAME` and `KC_BOOTSTRAP_ADMIN_PASSWORD`. This story resolves the conflict in favor of current official docs. Do not implement the deprecated variables just to match the older Epic wording.

Relevant current-doc facts checked on 2026-05-22:

- Keycloak documentation reports the current doc version as 26.6.2.
- Keycloak 26.6.2 was released on 2026-05-19 and includes multiple security fixes.
- Keycloak container docs specify `KC_BOOTSTRAP_ADMIN_USERNAME` and `KC_BOOTSTRAP_ADMIN_PASSWORD` for container bootstrap admin credentials.
- Keycloak realm import in containers reads JSON from `/opt/keycloak/data/import` when started with `--import-realm`.
- The Docker official Redis page currently lists `8.6.3` tags.
- Kubernetes `emptyDir` is ephemeral storage tied to the Pod lifecycle; this matches the MVP boundary and must be documented in the Redis Deployment comment.

### Previous Story Intelligence - Story 9.2

Story 9.2 completed the AppHost-to-Aspirate baseline and left these direct handoffs:

- Redis and Keycloak are excluded from publish-mode AppHost composition and belong under `deploy/k8s/{redis,keycloak}/`.
- `deploy/k8s/kustomization.yaml` already has commented Redis/Keycloak resource anchors; Story 9.3 must flip them to uncommented.
- The `KeycloakRealmUrlInCluster` constant includes `PUBLISH-MODE-DNS-ANCHOR`; Story 9.3 must verify it against the Service it creates.
- Story 9.2's imagePullSecrets and Dapr annotation patch contracts require vendor carve-outs to be skipped by future Story 9.5 patch implementation.
- Story 9.2 observed that `kubectl kustomize --dry-run=client` is not supported by the installed kubectl; use `kubectl kustomize deploy/k8s/` for local validation unless the installed version changes.
- Story 9.2's generated app folders should be treated as a protected baseline in this story. A `git diff --name-only -- deploy/k8s/<seven app folders>` result must be empty.

Important correction from Story 9.2 review history:

- Do not treat a future `daprd` container as a detection anchor. Aspirate does not emit `daprd`; the Dapr operator injects it at admission time. Patch scopes must be explicit allowlists in Story 9.5 and tests in Story 9.7.

### Git Intelligence

Recent relevant commits:

- `6dbcb6e feat(deploy): story 9-2 aspirate apphost composition` - created the seven app-service manifest folders, AppHost publish/run split, `namespace.yaml`, and the top-level Kustomization handoff anchors.
- `12a0f47 feat(deploy): story 9-1 zot registry documentation` - delivered Zot docs/manifests and `deploy/k8s/README.md`.
- `4f84aa8 feat(deploy): Epic 9 v2 greenfield rewrite - wipe v1 artefacts + replan as 7 stories` - made v2 the source of truth and superseded the older 9.3 story.
- `9c97b8a feat(docs): add Kubernetes deployment architecture documentation` - introduced the canonical architecture document this story follows.

### Testing Requirements

This story is primarily YAML and documentation. Required verification is static and Kustomize-based:

- `kubectl kustomize deploy/k8s/`
- `kubectl kustomize deploy/k8s/redis/`
- `kubectl kustomize deploy/k8s/keycloak/`
- AC7 grep checks
- `git diff --check`

Run focused AppHost tests only if the AppHost constant changes:

```bash
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter AppHostTenantsTopologyTests
```

Full solution tests are not required for a YAML/docs-only change and are known to have unrelated failures from Story 9.2 handoff.

### Common LLM Mistakes to Avoid

1. Do not use `KEYCLOAK_ADMIN` / `KEYCLOAK_ADMIN_PASSWORD`; use the current `KC_BOOTSTRAP_ADMIN_*` variables.
2. Do not create a Kubernetes Secret manifest for `hexalith-keycloak-admin`; Story 9.5 bootstraps it imperatively.
3. Do not add `imagePullSecrets` to Redis or Keycloak. Public vendor images do not use Zot credentials.
4. Do not add Dapr annotations to Redis or Keycloak. They are backing services, not Dapr app workloads.
5. Do not use `redis:latest` or `quay.io/keycloak/keycloak:latest`.
6. Do not add Redis AUTH or a Dapr `redisPassword` requirement in this story; that would contradict the MVP boundary and break Story 9.4's Component contract.
7. Do not change the Service name from `keycloak` or port from `8080` unless you also update `KeycloakRealmUrlInCluster`.
8. Do not keep the Redis/Keycloak Kustomization lines commented after creating the folders.
9. Do not copy v1 Story 9.3 files blindly; Epic 9 v1 is superseded.
10. Do not duplicate the full topology in README; link the canonical architecture document.
11. Do not make `deploy/k8s/keycloak/kustomization.yaml` depend on a file outside `deploy/k8s/keycloak/`; copy/embed the realm JSON into the local ConfigMap.
12. Do not claim runtime readiness in this story. It proves manifest shape and Kustomize composition; Story 9.7 owns pod startup, Keycloak realm availability, issuer URL runtime checks, Redis connectivity, and missing-Secret behavior.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` lines 3242-3297] - Story 9.3 v2 user story and ACs.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-21-epic9-greenfield-rewrite.md` Appendix A Story 9.3] - authoring SCP for Epic 9 v2.
- [Source: `docs/kubernetes-deployment-architecture.md` section 3.1] - nine-workload topology and Redis/Keycloak roles.
- [Source: `docs/kubernetes-deployment-architecture.md` section 5.3] - vendor-image carve-outs do not need `zot-pull-secret`.
- [Source: `docs/kubernetes-deployment-architecture.md` section 6] - operator-managed Secrets, including `hexalith-keycloak-admin`.
- [Source: `docs/kubernetes-deployment-architecture.md` section 7 Source 3] - `deploy/k8s/{redis,keycloak}/` as hand-authored carve-outs.
- [Source: `docs/kubernetes-deployment-architecture.md` section 8] - future publish clean and apply flow.
- [Source: `docs/kubernetes-deployment-architecture.md` section 11] - carve-outs survive regeneration.
- [Source: `docs/kubernetes-deployment-architecture.md` section 12] - Redis production hardening deferred.
- [Source: `_bmad-output/implementation-artifacts/9-2-aspire-apphost-to-aspirate-manifest-composition.md`] - previous story handoff and review learnings.
- [Source: `deploy/k8s/kustomization.yaml`] - current commented handoff anchors to flip.
- [Source: `src/Hexalith.Parties.AppHost/Program.cs`] - current AppHost publish/run-mode split and Keycloak DNS anchor.
- [Official: Keycloak container docs](https://www.keycloak.org/server/containers) - current bootstrap admin env vars and startup realm import.
- [Official: Keycloak 26.6.2 release](https://www.keycloak.org/2026/05/keycloak-2662-released) - current Keycloak patch release and security context at story creation.
- [Official: Keycloak upgrading guide](https://www.keycloak.org/docs/latest/upgrading/index.html) - deprecation of `KEYCLOAK_ADMIN` / `KEYCLOAK_ADMIN_PASSWORD`.
- [Official: Redis Docker image](https://hub.docker.com/_/redis) - current official Redis image tags.
- [Official: Kubernetes volumes](https://kubernetes.io/docs/concepts/storage/volumes/) - `emptyDir` behavior.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex (OpenAI)

### Debug Log References

- `date '+%Y-%m-%d %H:%M:%S %z'` -> `2026-05-22 11:05:48 +0200`
- `grep -E '^[[:space:]]*#[[:space:]]*-[[:space:]]*(redis|keycloak)\b' deploy/k8s/kustomization.yaml` -> confirmed both Story 9.2 handoff anchors were commented before the flip.
- `grep -n -A8 -B3 'AddRedis("redis")\|AddKeycloak("keycloak"' src/Hexalith.Parties.AppHost/Program.cs` -> confirmed both resources remain gated by `builder.ExecutionContext.IsRunMode`.
- `grep -n 'const string KeycloakRealmUrlInCluster = "http://keycloak.hexalith-parties.svc.cluster.local:8080/realms/hexalith"' src/Hexalith.Parties.AppHost/Program.cs` -> confirmed AppHost DNS anchor matches the committed Keycloak Service contract.
- Official source checks on 2026-05-22: Keycloak 26.6.2 release/docs remain current; Keycloak container docs use `KC_BOOTSTRAP_ADMIN_USERNAME` / `KC_BOOTSTRAP_ADMIN_PASSWORD` and `/opt/keycloak/data/import` with `--import-realm`; Docker Hub official `library/redis:8.6.3` tag is active.
- `kubectl kustomize deploy/k8s/` -> exit 0.
- `kubectl kustomize deploy/k8s/redis/` -> exit 0.
- `kubectl kustomize deploy/k8s/keycloak/` -> exit 0.
- AC7 cleanliness greps for Zot registry references, image pull secrets, Dapr annotations, deprecated Keycloak env names, obvious credential tokens, and `latest` tags -> zero lines.
- Topology checks: active Redis/Keycloak top-level resource lines = 2; commented Redis/Keycloak resource lines = 0; `PUBLISH-MODE-DNS-ANCHOR` count = 1.
- Rendered resource check -> exactly one Redis Deployment, Redis Service, Keycloak Deployment, Keycloak Service, and Keycloak ConfigMap.
- `git diff --name-only -- deploy/k8s/eventstore deploy/k8s/eventstore-admin deploy/k8s/eventstore-admin-ui deploy/k8s/memories deploy/k8s/parties deploy/k8s/parties-mcp deploy/k8s/tenants` -> zero lines.
- `git diff --check` -> exit 0.
- Code-review patch verification: `kubectl kustomize deploy/k8s/`, `kubectl kustomize deploy/k8s/redis/`, and `kubectl kustomize deploy/k8s/keycloak/` -> exit 0.
- Code-review patch verification: embedded Keycloak realm JSON parses and includes clients `hexalith-eventstore`, `hexalith-parties`, `hexalith-parties-mcp`, and `hexalith-tenants`.
- Code-review patch verification: AC7 cleanliness greps remain zero lines after review patches.
- Code-review patch verification: active Redis/Keycloak top-level resource lines = 2; commented Redis/Keycloak resource lines = 0; `PUBLISH-MODE-DNS-ANCHOR` count = 1.
- Code-review patch verification: rendered resource check remains exactly one Redis Deployment, Redis Service, Keycloak Deployment, Keycloak Service, and Keycloak ConfigMap.
- Code-review patch verification: `git diff --name-only -- deploy/k8s/eventstore deploy/k8s/eventstore-admin deploy/k8s/eventstore-admin-ui deploy/k8s/memories deploy/k8s/parties deploy/k8s/parties-mcp deploy/k8s/tenants` -> zero lines.
- Code-review patch verification: `git diff --check` -> exit 0.

### Completion Notes List

- Added hand-authored Redis carve-out manifests under `deploy/k8s/redis/`: Deployment, Service, and local Kustomization.
- Redis uses the active official exact tag `redis:8.6.3`, `imagePullPolicy: IfNotPresent`, `emptyDir: {}`, no AUTH, no Secret reference, no Zot registry reference, no Dapr annotations, and no `imagePullSecrets`.
- Added hand-authored Keycloak carve-out manifests under `deploy/k8s/keycloak/`: self-contained realm ConfigMap, Deployment, Service, and local Kustomization.
- Keycloak uses `quay.io/keycloak/keycloak:26.6.2`, current `KC_BOOTSTRAP_ADMIN_*` variables, Secret `hexalith-keycloak-admin` via `secretKeyRef`, args exactly `start-dev` and `--import-realm`, and the ConfigMap-mounted realm file at `/opt/keycloak/data/import/hexalith-realm.json`.
- Intentionally sanitized the realm ConfigMap by copying the run-mode realm/client shape and omitting sample users and sample credential values from `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json`; this keeps the committed ConfigMap self-contained without embedding sample passwords.
- Flipped only the existing top-level Redis and Keycloak Kustomize anchors to active resources; did not edit or regenerate the seven Aspirate-owned application folders.
- Updated `deploy/k8s/README.md` to reflect the delivered Story 9.2/9.3 state, preserve the canonical architecture link, document the Story 9.5 clean-phase preservation whitelist, describe Redis as MVP/non-production, and describe Keycloak Secret bootstrap as external until Story 9.5.
- Code review expanded the committed Keycloak realm to include the configured publish-mode service clients/audiences: `hexalith-eventstore`, `hexalith-parties`, `hexalith-parties-mcp`, and `hexalith-tenants`.
- Code review moved the Keycloak issuer URL to the deployment manifest header, added the Story 9.1 Zot delivered roadmap row, clarified the Keycloak Secret generation/preservation contract, and disabled default service account token mounting for Redis and Keycloak pods.
- Full solution tests were intentionally not run because this story changed only YAML/docs/BMad tracking artifacts and did not change AppHost code or project files.

### File List

- `_bmad-output/implementation-artifacts/9-3-hand-authored-carve-outs-redis-and-keycloak.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `deploy/k8s/README.md`
- `deploy/k8s/kustomization.yaml`
- `deploy/k8s/redis/deployment.yaml`
- `deploy/k8s/redis/service.yaml`
- `deploy/k8s/redis/kustomization.yaml`
- `deploy/k8s/keycloak/configmap.yaml`
- `deploy/k8s/keycloak/deployment.yaml`
- `deploy/k8s/keycloak/service.yaml`
- `deploy/k8s/keycloak/kustomization.yaml`

### Change Log

| Date | Author | Change |
|---|---|---|
| 2026-05-22 | bmad-create-story (Codex) | Story 9.3 v2 ready-for-dev. Created comprehensive context for Redis and Keycloak hand-authored carve-outs, including current Keycloak bootstrap variable correction, Story 9.2 handoff anchors, Kustomization flip contract, README update, and carve-out cleanliness checks. |
| 2026-05-22 | bmad-party-mode review patch (Winston + Amelia + Murat + Paige) | Tightened story after review: self-contained Keycloak ConfigMap and local Kustomize contract, no committed Secret manifests/generators/env files, exact Keycloak Service/DNS/realm contract, exact startup args and mount path, explicit namespace inheritance, README MVP/security notes, protected Aspirate-folder baseline, independent folder Kustomize checks, and Story 9.7 runtime-risk handoff. |
| 2026-05-22 | bmad-dev-story (Codex) | Implemented Redis and Keycloak hand-authored carve-outs, wired top-level Kustomize resources, refreshed operator README, completed AC7 Kustomize/cleanliness verification, and moved story to review. |
| 2026-05-22 | bmad-code-review (Codex) | Applied 5 review patches: expanded Keycloak clients/audiences, hardened Secret bootstrap docs, moved issuer URL to manifest header, added Story 9.1 roadmap status, disabled vendor pod service account token mounts, reverified Kustomize/cleanliness/topology checks, and moved story to done. |
