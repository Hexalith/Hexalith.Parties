# Story 9.5: Operator Scripts (publish.ps1 + teardown.ps1)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story Boundary

Story 9.5 turns the Epic 9 v2 deployment artifacts into an operator-driven pipeline. It owns the PowerShell scripts and the exact operational sequencing; it must not redesign the AppHost service graph, Redis/Keycloak carve-outs, or Dapr CR topology delivered by Stories 9.2, 9.3, and 9.4.

Ownership split:

| Story | Owns | Must not claim |
|---|---|---|
| 9.2 | Aspire AppHost composition and Aspirate-emitted seven application folders | Operator scripts, Dapr CR apply, Redis/Keycloak manifests |
| 9.3 | `deploy/k8s/redis/`, `deploy/k8s/keycloak/`, top-level Kustomize carve-out wiring | Regenerating carve-outs or adding them to AppHost |
| 9.4 | `deploy/dapr/*.yaml` exact CR file set and static Dapr manifest validation | Cluster install/apply automation or annotation patch runtime proof |
| 9.5 | `publish.ps1`, `teardown.ps1`, `_lib/Confirm-KubeContext.ps1`, bounded stdout, Secret bootstrap, ordered apply/teardown, focused script tests | Re-designing existing topology, widening ACLs, changing Redis/Keycloak MVP scope |
| 9.6 | Full static lint tool and JSON output | Operator script implementation |
| 9.7 | Complete fitness and live-cluster integration suite | Replacing 9.5 script acceptance |

## Story

As an operator deploying or tearing down Hexalith.Parties,
I want two PowerShell scripts - `deploy/k8s/publish.ps1` and `deploy/k8s/teardown.ps1` - that share the `-ConfirmContext` gate and a common helper module `_lib/Confirm-KubeContext.ps1`,
so that one command takes me from clean checkout to 9 Ready pods, another command unwinds everything, and both scripts apply the same context-confirmation, credential-safety, and bounded-stdout discipline.

## Acceptance Criteria

### AC1 - publish.ps1 executes the 13-phase pipeline in order

Given the 13-phase pipeline in `docs/kubernetes-deployment-architecture.md` section 8,
when `pwsh deploy/k8s/publish.ps1 -ConfirmContext <name>` runs,
then it executes these phases in order with a clear `[publish] Step N:` boundary marker and a specific non-zero exit code on failure:

| Step | Action | Required proof/output | Failure signal |
|---|---|---|---|
| 0 | Assert `-ConfirmContext` via `_lib/Confirm-KubeContext.ps1` | Active context echoed once | Exit 2 on mismatch before any mutation command or cleanup |
| 1 | Resolve the MinVer version | Bounded line naming the resolved tag, not a build log dump | Exit 5 on empty or malformed SemVer; one warning and proceed on `+dirty` |
| 2 | Clean `deploy/k8s/` using the exact cleanup contract below | Bounded count of removed generated entries | Fail before deleting anything outside the allowed generated set |
| 3 | Run `dotnet aspirate generate --container-image-tag <minver> --container-registry registry.hexalith.com` without `--skip-build` | Aspirate command line printed without secrets | Propagate aspirate exit code |
| 4 | Strip Aspirate placeholder/orphan files | Bounded count/list of stripped files | Exit 1 on unsafe/unrecognized cleanup target |
| 5 | Patch Dapr annotations on `eventstore`, `eventstore-admin`, `parties`, `tenants`, and `memories` | Patch target summary | Exit 1 if any expected target is missing or any forbidden target is selected |
| 6 | Patch JWT `secretKeyRef` for Secret `hexalith-jwt-signing` into the five Dapr-equipped Deployments | Patch target summary | Exit 1 on duplicate/env-shape ambiguity |
| 7 | Inject pod-template-level `imagePullSecrets: [{ name: zot-pull-secret }]` into every `registry.hexalith.com/*` Deployment; skip vendor carve-outs | Patch target summary | Exit 1 if registry-backed workload lacks the pull secret after patch |
| 8 | Verify expected Aspirate service folders exist | Expected/actual folder summary | Exit 4 on any missing or unexpected generated service folder |
| 9 | Run `dapr init -k` unless `-SkipDaprInit` is passed | Dapr install/existing-install status | Exit 3 if Dapr CLI is missing; exit 1 on unhealthy partial install |
| 10 | Ensure namespace `hexalith-parties` exists and server-side dry-run `deploy/dapr/resiliency.yaml` | Dry-run success line naming the CR only | Exit 1 on dry-run failure |
| 11 | Bootstrap operator-managed Secrets | Name-only `<created|exists|updated>` lines | Exit 6 for Zot credential failures |
| 12 | Apply Dapr CRs in Component -> Resiliency -> Configuration -> Subscription order | Ordered apply summary | Exit 1 on any CR apply failure |
| 13 | Run `kubectl apply -k deploy/k8s/` | Final workload apply summary | Exit 1 on kustomize/apply failure |

And on success it prints `[publish] OK: <minver> applied to <context> in <duration>` and exits 0.

And the script centralizes the exit-code contract so tests and operators can depend on stable meanings:

| Exit code | Meaning |
|---:|---|
| 0 | Success or teardown no-op for absent namespace |
| 1 | General bounded operational failure: unsafe cleanup target, patch failure, Dapr CRD/dry-run/apply failure, or kustomize apply failure |
| 2 | `-ConfirmContext` mismatch or missing current context |
| 3 | Required CLI missing (`dapr`, `kubectl`, `dotnet`, or `pwsh` prerequisite check as applicable); Dapr CLI missing is explicitly code 3 |
| 4 | Expected Aspirate service folder missing or unexpected generated service folder detected |
| 5 | MinVer resolution empty, stale, or malformed |
| 6 | Zot Docker config/auth failure or credential-helper indirection |
| 7 | Teardown residual state detected |

And both scripts declare PowerShell 7 compatibility and fail-fast behavior with `#Requires -Version 7`, `Set-StrictMode -Version Latest`, and `$ErrorActionPreference = 'Stop'`; all paths resolve from the repository root, not the caller's current shell directory.

Cleanup contract for step 2:

- Preserve exactly these `deploy/k8s/` entries if present: `redis/`, `keycloak/`, `kustomization.yaml`, `namespace.yaml`, `README.md`, `publish.ps1`, `teardown.ps1`, `_lib/`.
- Generated service folders allowed to be removed/recreated are exactly: `eventstore/`, `eventstore-admin/`, `eventstore-admin-ui/`, `parties/`, `parties-mcp/`, `tenants/`, `memories/`.
- Any other top-level file or directory under `deploy/k8s/` must cause the cleanup phase to fail before deleting it, unless it is a known Aspirate placeholder explicitly handled in step 4 and covered by tests.
- The cleanup test must prove the preserved entries survive byte-identically and that unrecognized entries are not silently deleted.

### AC2 - MinVer resolution is proven, not assumed

Given the repo uses MinVer `7.0.0` and .NET SDK `10.0.300`,
when publish step 1 resolves the image tag,
then the resolved version must match `^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?(?:\+[A-Za-z0-9.-]+)?$`,
and a defensive leading `v` is stripped if present.

Developer guardrail: Epic text names `dotnet msbuild -getProperty:Version`, but the v1 Story 9.2 review found that a pure MSBuild property evaluation can miss MinVer target execution and return a stale default. The implementation must prove the exact command before relying on it. The preferred command is the repo-approved command that returns the MinVer-stamped package/image version from the AppHost publish path; if `dotnet msbuild -getProperty:Version` returns `1.0.0`, blank, or an un-stamped value in this repo, use the smallest command that actually triggers MinVer evaluation. Completion notes must record:

- the exact command used;
- its raw resolved value;
- the normalized image tag after defensive `v` stripping;
- one generated `registry.hexalith.com/<service>:<tag>` image line showing the same tag.

AC9 tests must fail if MinVer resolution returns an empty value, `1.0.0` without proof that it is the intended project version, a leading `v` after normalization, or a value that does not match the generated image tag.

### AC3 - post-Aspirate patches are idempotent and scoped

Given Story 9.2 currently leaves Dapr-equipped Deployments with `dapr.io/config: tracing`,
when publish steps 5-7 run,
then the final generated Deployments have:

| Deployment | Dapr annotations | JWT signing key | imagePullSecrets |
|---|---|---|---|
| `eventstore` | `dapr.io/enabled: "true"`, `dapr.io/app-id: eventstore`, `dapr.io/app-port: "8080"`, `dapr.io/config: accesscontrol` | `Authentication__JwtBearer__SigningKey` uses `valueFrom.secretKeyRef` to `hexalith-jwt-signing` | required |
| `eventstore-admin` | same shape, config `accesscontrol-eventstore-admin` | same | required |
| `parties` | same shape, config `accesscontrol-parties` | same | required |
| `tenants` | same shape, config `accesscontrol-tenants` | same | required |
| `memories` | same shape, config `accesscontrol-memories` | same | required |
| `eventstore-admin-ui` | no `dapr.io/*` annotations | no Story 9.5 JWT patch requirement unless existing config requires it | required because it uses `registry.hexalith.com/*` |
| `parties-mcp` | no `dapr.io/*` annotations | no Story 9.5 JWT patch requirement unless existing config requires it | required because it uses `registry.hexalith.com/*` |
| `redis` | forbidden | forbidden | forbidden |
| `keycloak` | forbidden | forbidden | forbidden |

And a second run on the same commit produces zero diff for Dapr annotations, JWT `secretKeyRef`, and `imagePullSecrets`.

And the patcher fails closed if any expected Deployment file is missing, duplicated, structurally unparseable, or if a forbidden target (`eventstore-admin-ui` for Dapr/JWT, `parties-mcp` for Dapr/JWT, `redis`, `keycloak`) would be modified by the wrong patch.

Patch idempotency proof must compare normalized YAML/object structure after applying each patch twice, not only script exit codes.

### AC4 - operator-managed Secrets are idempotent and never leaked

Given the cluster namespace is `hexalith-parties`,
when publish step 11 runs,
then it creates missing Secrets and preserves existing ones:

| Secret | Type | First-create payload | Existing behavior |
|---|---|---|---|
| `hexalith-jwt-signing` | `Opaque` | 32 random bytes | report `exists`; do not regenerate |
| `hexalith-keycloak-admin` | `Opaque` | `KC_BOOTSTRAP_ADMIN_USERNAME=admin` plus 24 random bytes for `KC_BOOTSTRAP_ADMIN_PASSWORD` | report `exists`; do not regenerate |
| `zot-pull-secret` | `kubernetes.io/dockerconfigjson` | re-emitted `auths["registry.hexalith.com"]` block from Docker config | create if missing; update only the dockerconfigjson payload from current Docker config without decoding |

And the script never prints, decodes, writes to committed files, or includes in error output any Secret value, Docker config body, bearer token, cluster URL, cluster CA, or JWT-shaped value.

And `hexalith-jwt-signing` and `hexalith-keycloak-admin` are create-if-missing and preserve-if-present only; Story 9.5 must not add rotation semantics. If rotation is ever needed, it belongs in a future explicit story or a separate flag with its own ACs and tests.

And the Zot path exits 6 with an actionable bounded error if `auths["registry.hexalith.com"]` is missing, empty, malformed JSON, malformed base64 auth, missing the `auth` field, or delegated through top-level `credsStore` or `credHelpers["registry.hexalith.com"]`; the message may mention `docker login -u parties-publisher registry.hexalith.com` and `$env:DOCKER_CONFIG` but must not echo credentials or the `.dockerconfigjson` body.

And Secret creation/update uses an idempotent apply path, such as `kubectl create secret ... --dry-run=client -o yaml | kubectl apply -f -`, or an equivalent tested implementation. Plain create-only commands are not acceptable for re-publish.

### AC5 - Dapr install and apply honor Story 9.4 topology

Given Dapr is cluster-wide and Story 9.4 delivered the exact `deploy/dapr/` CR file set,
when publish steps 9-12 run,
then:

- `dapr init -k` targets the active kubectl context and default Dapr namespace `dapr-system`; `-SkipDaprInit` skips only install, not CR apply.
- Existing healthy Dapr control-plane install is success. Version drift relative to the project baseline `1.14.4` is a warning, not a blocking failure.
- A partial or unhealthy Dapr install fails with exit 1 and bounded output unless the operator explicitly supplied `-SkipDaprInit`; even with `-SkipDaprInit`, the script must verify required Dapr CRDs exist before applying Dapr CRs.
- `deploy/dapr/resiliency.yaml` is server-side dry-run before consumers are applied; dry-run failures exit 1 without printing the full CR body.
- Dapr CR application order is Components (`statestore.yaml`, `pubsub.yaml`), Resiliency (`resiliency.yaml`), Configurations (`accesscontrol*.yaml`), then Subscriptions (`subscription-*.yaml`).
- `deploy/dapr-alternatives/` is never applied by the publish script.

### AC6 - teardown.ps1 is idempotent, bounded, and residual-state aware

Given `pwsh deploy/k8s/teardown.ps1 -ConfirmContext <name>` runs,
then it uses the shared context helper and exits 2 on mismatch.

And if namespace `hexalith-parties` does not exist, it logs `[teardown] namespace hexalith-parties not present - nothing to delete` and exits 0.

And otherwise it deletes in order, with bounded stdout markers and `--ignore-not-found=true` where supported:

1. `kubectl delete -k deploy/k8s/` for the nine workloads and namespace-scoped supporting resources.
2. `kubectl delete -f deploy/dapr/` for Components, Resiliency, Configurations, and Subscriptions.
3. Secrets `hexalith-jwt-signing`, `hexalith-keycloak-admin`, and `zot-pull-secret`.

And `-PurgeNamespace` additionally deletes namespace `hexalith-parties` with `--wait=true`, but only after the shared context gate has passed and the script has completed the namespace-scoped delete/residual check. If non-story resources remain in the namespace, the script must stop with exit 7 instead of purging them silently.

And `-PurgeDapr` additionally runs `dapr uninstall -k --all`; without this switch, the Dapr control plane remains untouched. This action is cluster-wide and must be logged as a distinct destructive phase after the same context gate has passed.

And the final residual probe checks owned Deployments, Services, ConfigMaps, Secrets, Dapr Components, Dapr Configurations, Dapr Subscriptions, and Dapr Resiliency resources in `hexalith-parties`. If any remain, exit 7 with the message `Residual state detected - manual intervention required before next publish` and a bounded count/list by kind/name; if none remain and the namespace is retained, print `[teardown] OK: namespace hexalith-parties clean` and exit 0.

### AC7 - shared ConfirmContext helper is the single gate implementation

Given `deploy/k8s/_lib/Confirm-KubeContext.ps1` exists,
when `publish.ps1` and `teardown.ps1` are inspected,
then both import and call the helper function `Assert-KubeContext -Expected <name>`.

And gate behavior is centralized:

- Required `-ConfirmContext` parameter on both scripts.
- Exact string comparison to `kubectl config current-context`.
- Active context echoed exactly once at run start for auditability.
- Mismatch exits parent script 2 with `expected '<arg>', got '<active>'`.
- No cluster URL, certificate authority, token, or kubeconfig body is printed.
- Mismatch, empty current context, or helper failure stops before any `kubectl apply/delete`, `dapr`, `dotnet aspirate`, Secret bootstrap, or filesystem cleanup mutation.

### AC8 - README/docs are moved from forward reference to delivered behavior

Given `deploy/k8s/README.md`, `docs/getting-started.md`, and `docs/deployment-guide.md` currently describe Story 9.5 as a forward reference,
when this story is implemented,
then those entry points are updated minimally to describe the delivered `publish.ps1`, `teardown.ps1`, and `_lib/Confirm-KubeContext.ps1` behavior.

And the documents continue to point to `docs/kubernetes-deployment-architecture.md` as the canonical topology source and do not duplicate the full 13-phase table.

And the docs must enable an operator to publish, verify the expected nine pods, and tear down a non-production Kubernetes deployment without reading test code.

And they do not include real credential values, bearer-token examples, decoded Docker auth values, or stale v1 script names (`regen.ps1`, `deploy-local.ps1`, `teardown-local.ps1`) except when explicitly naming superseded history in ADR rationale.

### AC9 - focused script tests guard this story before Story 9.7

Given Story 9.7 owns the complete fitness and live-cluster suite,
when Story 9.5 is implemented,
then AC9 is a hard completion gate, not optional. Add focused tests in `tests/Hexalith.Parties.DeployValidation.Tests/` for the behavior that can be verified without a real cluster:

- ConfirmContext mismatch, empty current context, whitespace/case mismatch, and helper failure exit before any mutation command or cleanup runs; assert zero downstream command invocations.
- ConfirmContext failure output does not leak URL/CA/token-shaped data.
- The clean phase preserves the exact Story 9.3 whitelist plus `_lib/`, removes only allowed generated service folders, and refuses unknown top-level entries.
- Dapr annotation patch maps exactly five Dapr-equipped deployments and skips `eventstore-admin-ui`, `parties-mcp`, `redis`, and `keycloak`.
- Patch tests fail when expected generated targets are missing, duplicated, or structurally unparseable.
- JWT patch replaces empty/literal `Authentication__JwtBearer__SigningKey` with `valueFrom.secretKeyRef` and is idempotent.
- `imagePullSecrets` patch touches every `registry.hexalith.com/*` Deployment at pod-template scope and skips vendor images.
- Secret bootstrap logic rejects missing auths, empty registry entries, malformed JSON/base64, `credsStore`, and `credHelpers` without printing Docker config contents or secret values.
- Dapr apply ordering is command-captured as Components, Resiliency, Configurations, Subscriptions, with final `kubectl apply -k deploy/k8s/` last.
- `-SkipDaprInit` still verifies required Dapr CRDs before CR apply.
- Teardown absent namespace returns 0; teardown residual probe returns exit 7 on remaining owned resources; `-PurgeNamespace` refuses non-story residuals; `-PurgeDapr` is only invoked when explicitly passed.
- Publish emits phase markers in order and returns the documented exit codes for injected failures.

Use PATH-based command shims, temp directories, or explicit internal functions for tests. If a test-only switch such as `-DryRun` is introduced, it must be documented, must not weaken the default operator path, and must still exercise credential-safety and bounded-output rules.

## Tasks / Subtasks

- [x] Task 1 - Add shared context gate (AC: 6, 7)
  - [x] Create `deploy/k8s/_lib/Confirm-KubeContext.ps1` with one exported function `Assert-KubeContext -Expected <name>`.
  - [x] Require `-ConfirmContext` on both scripts and call the helper before any cluster mutation.
  - [x] Ensure mismatch output is bounded and contains only the expected/active context names.
  - [x] Prove failed context checks execute before any filesystem cleanup, `dotnet aspirate`, `dapr`, or mutating `kubectl` command.

- [x] Task 2 - Implement publish.ps1 orchestration shell (AC: 1, 2, 5)
  - [x] Add `deploy/k8s/publish.ps1` with phase markers, stopwatch/elapsed-time markers, and exact exit-code mapping.
  - [x] Add the centralized exit-code table to script comments or help output so operators and tests do not infer it from implementation.
  - [x] Resolve and validate the MinVer tag against the current repo; record the actual working command in the Dev Agent Record.
  - [x] Run Aspirate with `--container-image-tag <minver>` and `--container-registry registry.hexalith.com`, without `--skip-build`.
  - [x] Verify the seven expected service folders after generation.

- [x] Task 3 - Implement safe cleanup and post-Aspirate patches (AC: 1, 3)
  - [x] Preserve `redis/`, `keycloak/`, `kustomization.yaml`, `namespace.yaml`, `README.md`, `publish.ps1`, `teardown.ps1`, and `_lib/` during cleanup.
  - [x] Strip only known Aspirate placeholder/orphan files; do not delete committed carve-outs.
  - [x] Patch Dapr annotations using the exact app-id/config map from AC3.
  - [x] Patch JWT signing key env entries to `valueFrom.secretKeyRef` against `hexalith-jwt-signing`.
  - [x] Patch pod-template-level `imagePullSecrets` for registry-backed Deployments.
  - [x] Make all patches no-op on already-patched manifests.

- [x] Task 4 - Implement Dapr install/apply path (AC: 1, 5)
  - [x] Check for Dapr CLI before `dapr init -k`; exit 3 if missing.
  - [x] Support `-SkipDaprInit` without skipping CR dry-run/apply.
  - [x] Ensure namespace `hexalith-parties` exists.
  - [x] Run server-side dry-run for `deploy/dapr/resiliency.yaml`; exit 1 on failure with bounded output.
  - [x] Apply `deploy/dapr/*.yaml` in Component -> Resiliency -> Configuration -> Subscription order.

- [x] Task 5 - Implement Secret bootstrap safely (AC: 4)
  - [x] Create or preserve `hexalith-jwt-signing` using 32 random bytes.
  - [x] Create or preserve `hexalith-keycloak-admin` with username `admin` and 24 random password bytes.
  - [x] Create/update `zot-pull-secret` by re-emitting `auths["registry.hexalith.com"]` from Docker config without decoding.
  - [x] Reject missing/malformed Docker auth and credential-helper indirection with exit 6.
  - [x] Add output guards so secret values and Docker config bodies never appear in stdout/stderr.

- [x] Task 6 - Implement teardown.ps1 (AC: 6, 7)
  - [x] Add `deploy/k8s/teardown.ps1` with shared context gate and bounded phase markers.
  - [x] Treat absent namespace as successful no-op.
  - [x] Delete Kustomize resources, Dapr CRs, then operator-managed Secrets.
  - [x] Add `-PurgeNamespace` and `-PurgeDapr` switches with the exact destructive-operation semantics from AC6.
  - [x] Implement residual-state probe with exit 7 on remaining owned resources.

- [x] Task 7 - Update entry-point documentation (AC: 8)
  - [x] Update `deploy/k8s/README.md` from Story 9.5 forward reference to delivered script behavior.
  - [x] Update `docs/getting-started.md` and `docs/deployment-guide.md` only where their Story 9.5 wording is stale.
  - [x] Preserve canonical-doc pointers and avoid duplicating the full topology or phase table.

- [x] Task 8 - Add focused deploy-validation tests (AC: 3, 4, 5, 6, 7, 9)
  - [x] Add script helper/patch tests under `tests/Hexalith.Parties.DeployValidation.Tests/`.
  - [x] Use temp copies of `deploy/k8s/` and `deploy/dapr/` or command shims; do not mutate the committed deployment tree during tests.
  - [x] Include negative tests for credential leaks, helper mismatch, unknown cleanup targets, missing patch targets, and injected exit-code failures.
  - [x] Keep LiveCluster end-to-end tests out of the default pass; those remain Story 9.7.

### Review Findings

- [x] [Review][Patch] Default teardown deletes `namespace.yaml` through `kubectl delete -k`, bypassing the guarded `-PurgeNamespace` path [deploy/k8s/teardown.ps1:168]
- [x] [Review][Patch] `-PurgeNamespace` can treat residual-enumeration failures as an empty namespace and purge non-story resources [deploy/k8s/teardown.ps1:143]
- [x] [Review][Patch] Dapr annotation patching is replace-only and can continue without required final annotations [deploy/k8s/publish.ps1:357]
- [x] [Review][Patch] JWT signing-key patch rejects literal/empty entries and can silently miss deployments without `envFrom` [deploy/k8s/publish.ps1:390]
- [x] [Review][Patch] Generated JWT and Keycloak Secret values are random bytes under `data`, not printable strings encoded for Kubernetes [deploy/k8s/publish.ps1:481]
- [x] [Review][Patch] Missing `kubectl` exits as context mismatch instead of required CLI-missing exit code 3 [deploy/k8s/publish.ps1:601]
- [x] [Review][Patch] Teardown residual probe omits generated application ConfigMaps [deploy/k8s/teardown.ps1:39]
- [x] [Review][Patch] Post-Aspirate validation checks only directories and can miss unexpected top-level generated files [deploy/k8s/publish.ps1:296]
- [x] [Review][Patch] `imagePullSecrets` verification is not scoped to `spec.template.spec.imagePullSecrets` [deploy/k8s/publish.ps1:401]
- [x] [Review][Patch] Zot Docker config null/malformed shapes can escape the bounded exit-6 contract [deploy/k8s/publish.ps1:544]
- [x] [Review][Patch] Zot Docker auth validation materializes decoded auth despite the no-decode credential-safety constraint [deploy/k8s/publish.ps1:559]
- [x] [Review][Patch] Dapr resiliency server-side dry-run runs in both Step 10 and `Apply-DaprResources` [deploy/k8s/publish.ps1:458]
- [x] [Review][Patch] AC9 tests mostly assert source text instead of executing cleanup, patching, secret, Dapr-order, and purge behavior [tests/Hexalith.Parties.DeployValidation.Tests/OperatorScriptValidationTests.cs:167]

## Dev Notes

### Current State And Files To Touch

New files expected:

- `deploy/k8s/_lib/Confirm-KubeContext.ps1`
- `deploy/k8s/publish.ps1`
- `deploy/k8s/teardown.ps1`
- Focused test file(s) under `tests/Hexalith.Parties.DeployValidation.Tests/`, likely separate from `DaprManifestValidationTests.cs`.

Existing files likely updated:

- `deploy/k8s/README.md`: currently says Story 9.5 is a forward reference and lists the exact handoff requirements.
- `docs/getting-started.md`: already documents the publish/teardown flow; update only stale "future" wording.
- `docs/deployment-guide.md`: already documents Zot credentials and static validation; update only stale Story 9.5 status wording.

Files/directories to preserve:

- `deploy/dapr/*.yaml`: Story 9.4 owns the CR topology. Story 9.5 may apply these files but must not redesign them.
- `deploy/k8s/redis/` and `deploy/k8s/keycloak/`: Story 9.3 owns hand-authored carve-outs; preserve byte identity unless a direct Story 9.5 bug requires a documented fix.
- `src/Hexalith.Parties.AppHost/Program.cs`: Story 9.2 owns service graph composition. Story 9.5 should consume it through Aspirate, not remodel it.

### Current Deployment Tree Observations

- The repo currently has the seven app folders under `deploy/k8s/`: `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`, `memories`.
- Current generated images are `registry.hexalith.com/<service>:0.1.1-preview.0.3`; Story 9.5 must replace these on publish with the current MinVer value.
- Five Dapr-equipped Deployments currently contain `dapr.io/config: tracing`; Story 9.5 must patch them to the Story 9.4 configuration names.
- `eventstore-admin-ui` and `parties-mcp` use registry images but are not Dapr-equipped. They need `imagePullSecrets`, not Dapr annotations.
- `redis` uses `redis:8.6.3`; `keycloak` uses `quay.io/keycloak/keycloak:26.6.2`. They must not receive Zot pull secrets, JWT patches, or Dapr annotations.
- Keycloak already references Secret `hexalith-keycloak-admin` with keys `KC_BOOTSTRAP_ADMIN_USERNAME` and `KC_BOOTSTRAP_ADMIN_PASSWORD`; preserve those key names.

### Architecture Compliance

- Canonical topology: `docs/kubernetes-deployment-architecture.md` sections 1-13.
- ADRs: `_bmad-output/planning-artifacts/architecture.md` D-K8s-2, D-K8s-3, D-K8s-4.
- PRD: `_bmad-output/planning-artifacts/prd.md` FR31a and NFR30.
- Sprint-change source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-21-epic9-greenfield-rewrite.md`.

Do not reintroduce v1 script names as live paths. `regen.ps1`, `deploy-local.ps1`, and `teardown-local.ps1` are superseded history.

Do not add `deploy/dapr/` to `deploy/k8s/kustomization.yaml`; Story 9.5 applies Dapr CRs separately because they are Source 2 in the canonical architecture.

Do not use `PUBLISH_TARGET=k8s` or Aspire's `AddKubernetesEnvironment` as the deployment path. This story uses Aspirate generation from the AppHost.

### Review Patch Intelligence

Party-mode review on 2026-05-22 tightened this story before dev:

- Winston: destructive operations need hard lifecycle rules; Secret publish is create/preserve except `zot-pull-secret` payload update, namespace purge must not erase non-story resources, and Dapr partial installs need explicit failure behavior.
- Amelia: cleanup and patch target identity must fail closed; MinVer evidence must be concrete; PowerShell must be `pwsh`/PowerShell 7 strict-mode and repo-root-relative.
- Murat: AC9 is mandatory; tests must prove zero mutation after context rejection, no credential leaks on failure paths, command ordering, exact exit codes, and residual-probe behavior.
- Paige: operator handoff should be runbook-shaped; publish phases need expected output/failure signals and docs must support publish/verify/teardown without reading tests.

### Testing Standards

- Use xUnit v3, Shouldly, and existing deploy-validation patterns.
- Existing `DaprManifestValidationTests.cs` verifies the Story 9.4 CR file set and YAML shapes; extend with new test files rather than overloading that class with script orchestration concerns.
- Tests must not require a live cluster unless explicitly trait-gated outside the default pass. Story 9.7 owns the live-cluster suite.
- For PowerShell scripts, prefer testable internal functions or command shims over broad end-to-end process tests that mutate the real `deploy/k8s/` tree.
- Run at minimum:
  - `dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release`
  - `pwsh deploy/k8s/publish.ps1 -ConfirmContext definitely-not-current` and verify exit 2 plus no leak-shaped output
  - `pwsh deploy/k8s/teardown.ps1 -ConfirmContext definitely-not-current` and verify exit 2 plus no leak-shaped output
  - `git diff --check`

### Latest Technical Information

- Dapr CLI docs current as of 2026-05-22 still document `dapr init -k` as installing to the current Kubernetes context, default namespace `dapr-system`, and support `--wait` / `--timeout`. The story's required `dapr init -k` remains valid. Do not use `dapr init -k --dev`; Story 9.3/9.4 provide Redis and CRs. [Source: Dapr init CLI reference, https://docs.dapr.io/reference/cli/dapr-init/]
- Dapr Kubernetes deployment docs explicitly tell operators to verify/select the target kubectl context before `dapr init -k`; Story 9.5's `-ConfirmContext` gate is the project-specific enforcement of that current-context risk. [Source: Dapr Kubernetes deploy docs, https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-deploy/]
- Dapr uninstall docs support `dapr uninstall -k`; Story 9.5 requires `dapr uninstall -k --all` only behind explicit `-PurgeDapr` because the control plane is cluster-wide. [Source: Dapr uninstall CLI reference, https://docs.dapr.io/reference/cli/dapr-uninstall/]
- Kubernetes `kubectl delete` supports `--ignore-not-found`; use it for idempotent teardown where the command supports the targeted resource. [Source: Kubernetes kubectl delete reference, https://kubernetes.io/docs/reference/kubectl/generated/kubectl_delete/]
- Kubernetes supports applying Kustomize directories with `kubectl apply -k`; Story 9.5's final workload apply should keep using the top-level `deploy/k8s/kustomization.yaml`. [Source: Kubernetes Kustomize task docs, https://v1-34.docs.kubernetes.io/docs/tasks/manage-kubernetes-objects/kustomization/]
- PowerShell current support docs show actively supported PowerShell 7 releases; scripts should target `pwsh`/PowerShell 7 behavior, not Windows PowerShell 5.1. Avoid platform-specific assumptions where simple cross-platform .NET/PowerShell APIs exist. [Source: Microsoft PowerShell support lifecycle, https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle]
- MSBuild `-getProperty` can return evaluation-time values without building targets when no target is specified. This reinforces AC2: prove the MinVer command in this repo and do not blindly trust a stale property value. [Source: Microsoft MSBuild property evaluation docs, https://learn.microsoft.com/en-us/visualstudio/msbuild/evaluate-items-and-properties]

### Previous Story Intelligence

Story 9.4 completed in commit `2da6141` with:

- Ten production Dapr CR files under `deploy/dapr/`.
- `DaprManifestValidationTests.cs` validating exact file set, headers, Redis passwordless metadata, deny-by-default ACLs, subscription contracts, and resiliency bounds.
- `deploy/k8s/README.md` handoff text for Story 9.5: `dapr init -k`, server-side dry-run of `resiliency.yaml`, apply order, Dapr config patch map, and runtime smoke-test ideas.

Actionable handoffs from Story 9.4:

- Apply Dapr CRs in safe dependency order. Treat Resiliency as a first-class apply step before Configurations and Subscriptions.
- Do not change `deploy/dapr/` filenames or introduce subfolders.
- Patch Dapr config annotations from `tracing` to the exact config names in AC3.
- Treat the sample party-events subscription as reference-only; it must not block the 9-pod runtime topology.

Recent git history:

- `2da6141 feat(deploy): add story 9-4 dapr manifests`
- `4273b59 feat(deploy): story 9-3 add redis and keycloak carve-outs`
- `6dbcb6e feat(deploy): story 9-2 aspirate apphost composition`
- `12a0f47 feat(deploy): story 9-1 zot registry documentation`
- `4f84aa8 feat(deploy): Epic 9 v2 greenfield rewrite - wipe v1 artefacts + replan as 7 stories`

### Common Mistakes To Avoid

- Do not overwrite or delete `redis/` and `keycloak/` during cleanup.
- Do not add Dapr annotations to `eventstore-admin-ui`, `parties-mcp`, `redis`, or `keycloak`.
- Do not add `imagePullSecrets` to `redis` or `keycloak`.
- Do not emit real Secret values, Docker config content, bearer tokens, cluster URLs, or CAs in error output.
- Do not rely on `credsStore` or `credHelpers`; Path B requires a plain `auths["registry.hexalith.com"]` block.
- Do not regenerate `hexalith-jwt-signing` or `hexalith-keycloak-admin` if they already exist.
- Do not run `dapr uninstall -k --all` unless `-PurgeDapr` is explicitly passed.
- Do not let test runs mutate the committed `deploy/k8s/` tree.
- Do not mark this story complete without proving the MinVer command and idempotent patch behavior.
- Do not treat AC9 as a best-effort checklist. It is the story's safety gate.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` Story 9.5]
- [Source: `docs/kubernetes-deployment-architecture.md` sections 2, 6, 8, 10, 11, 13]
- [Source: `_bmad-output/planning-artifacts/architecture.md` ADR D-K8s-2, D-K8s-3, D-K8s-4]
- [Source: `_bmad-output/planning-artifacts/prd.md` FR31a, NFR30]
- [Source: `_bmad-output/implementation-artifacts/9-4-dapr-control-plane-components-acl-subscriptions.md` Story Boundary and handoff notes]
- [Source: `deploy/k8s/README.md` current Story 9.5 forward-reference handoff]
- [Source: `Directory.Packages.props`, `.config/dotnet-tools.json`, `global.json`]
- [Source: Dapr init CLI reference, https://docs.dapr.io/reference/cli/dapr-init/]
- [Source: Dapr Kubernetes deploy docs, https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-deploy/]
- [Source: Dapr uninstall CLI reference, https://docs.dapr.io/reference/cli/dapr-uninstall/]
- [Source: Kubernetes kubectl delete reference, https://kubernetes.io/docs/reference/kubectl/generated/kubectl_delete/]
- [Source: Kubernetes Kustomize task docs, https://v1-34.docs.kubernetes.io/docs/tasks/manage-kubernetes-objects/kustomization/]
- [Source: Microsoft PowerShell support lifecycle, https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle]
- [Source: Microsoft MSBuild property evaluation docs, https://learn.microsoft.com/en-us/visualstudio/msbuild/evaluate-items-and-properties]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `pwsh deploy/k8s/publish.ps1 -ConfirmContext definitely-not-current` -> exit 2; context gate stops before MinVer, cleanup, dotnet aspirate, Dapr, or mutating kubectl.
- `pwsh deploy/k8s/teardown.ps1 -ConfirmContext definitely-not-current` -> exit 2; context gate stops before delete, residual probe, or Dapr uninstall.
- `dotnet build src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj -c Release --getProperty:Version` -> `1.0.0` (rejected as stale evaluation-time value).
- `dotnet msbuild src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj -t:Build -p:Configuration=Release -getProperty:Version` -> `0.1.1-preview.0.7` (selected MinVer command).
- `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release` -> Passed: 22/22.
- `git diff --check` -> passed.
- `dotnet test Hexalith.Parties.slnx -c Release` -> failed only on known unrelated suite issues already tracked in sprint history: Sample docs Linux path separator failures (2), Client tests (2), and search benchmark threshold failures (3). DeployValidation tests passed in this full run.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Party-mode review fixes applied: runbook table, exit-code matrix, exact cleanup contract, Secret lifecycle rules, Dapr readiness semantics, destructive teardown guardrails, and mandatory AC9 test gate.
- Implemented shared `Assert-KubeContext` helper and required it before any publish/teardown mutation path.
- Implemented `publish.ps1` 13-step orchestration with stable exit-code mapping, proven MinVer resolution via `dotnet msbuild ... -t:Build -getProperty:Version`, safe cleanup, Aspirate invocation, Dapr/JWT/imagePullSecrets patching, Dapr install/apply ordering, and Secret bootstrap guards.
- Implemented `teardown.ps1` with absent-namespace no-op, ordered delete, operator Secret cleanup, residual-state exit 7, guarded namespace purge, and explicit `-PurgeDapr`.
- Updated operator entry docs from Story 9.5 forward reference to delivered behavior.
- Added focused deploy-validation tests for context gates, leak-shaped output redaction, no-mutation rejection, absent namespace teardown, phase ordering, patch scope, Secret input guards, Dapr ordering, and purge contracts.
- Code-review patches applied: namespace-preserving teardown, fail-closed purge enumeration, Dapr/JWT/imagePullSecrets postcondition checks, printable Secret data, CLI-missing exit code preservation, generated ConfigMap residual checks, post-Aspirate top-level drift checks, no-decode Zot auth validation, single resiliency dry-run, and behavior-level AC9 tests.

### File List

- `_bmad-output/implementation-artifacts/9-5-operator-scripts-publish-and-teardown.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `deploy/k8s/_lib/Confirm-KubeContext.ps1`
- `deploy/k8s/publish.ps1`
- `deploy/k8s/teardown.ps1`
- `deploy/k8s/README.md`
- `docs/deployment-guide.md`
- `docs/getting-started.md`
- `tests/Hexalith.Parties.DeployValidation.Tests/OperatorScriptValidationTests.cs`

## Change Log

| Date | Author | Changes |
|---|---|---|
| 2026-05-22 | dev-story agent | Added Story 9.5 operator publish/teardown scripts, shared context helper, focused deploy-validation tests, documentation updates, and moved story to review. |
| 2026-05-22 | code-review agent | Applied 13 review patches, expanded focused deploy-validation tests to 22/22, and moved story to done. |
