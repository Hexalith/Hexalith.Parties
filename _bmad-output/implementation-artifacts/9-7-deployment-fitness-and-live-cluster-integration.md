# Story 9.7: Deployment Fitness Tests + Live-Cluster Integration

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story Boundary

Story 9.7 delivers the safety net for the Epic 9 v2 deployment platform. It must turn the contracts from Stories 9.1-9.6 into default-pass tests, plus a small opt-in live-cluster suite. It must not redesign the deployment topology, rewrite `publish.ps1`, rewrite `validate-deployment.ps1`, change Dapr ACL policy, or re-open superseded Epic 9 v1 story files.

Default deploy-validation tests must be fixture-driven and deterministic. Non-LiveCluster tests must use temporary workspaces, scripted command shims, and poison credentials only. LiveCluster tests must be opt-in through both `[Trait("Category", "LiveCluster")]` and `KUBECONFIG_TEST_PATH`.

Ownership split:

| Story | Owns | Must not claim |
|---|---|---|
| 9.1 | Zot registry documentation, registry credential model, image tagging policy | Fitness-test implementation |
| 9.2 | Aspire AppHost composition and seven Aspirate-emitted service folders | Test-only topology rewrites |
| 9.3 | `deploy/k8s/redis/`, `deploy/k8s/keycloak/`, top-level Kustomize carve-out wiring | Changing carve-out MVP scope |
| 9.4 | `deploy/dapr/*.yaml` production CR set, ACLs, subscriptions, resiliency | Changing Dapr route topology while testing it |
| 9.5 | `publish.ps1`, `teardown.ps1`, `_lib/Confirm-KubeContext.ps1`, operator mutation path | Live-cluster tests that run by default |
| 9.6 | `deploy/validate-deployment.ps1`, eight lint categories, JSON schema, focused validator tests | Full fixture matrix and LiveCluster suite |
| 9.7 | Deploy fitness suite, curated fixtures, documentation-fitness checks, opt-in LiveCluster tests | Reworking validator/operator script implementations except narrow testability seams |

Important disambiguation: `_bmad-output/implementation-artifacts/9-7-memories-server-redis-connection-string.md` is the superseded Epic 9 v1 story. This story is the Epic 9 v2 sprint-status entry `9-7-deployment-fitness-and-live-cluster-integration`.

## Story

As a developer evolving the deployment topology,
I want a sealed set of fitness tests in `tests/Hexalith.Parties.DeployValidation.Tests/` that guard the architectural contracts of Stories 9.1-9.6, plus a small set of trait-gated live-cluster integration tests that exercise the `publish.ps1` happy path end-to-end,
so that byte-determinism, idempotency, deny-by-default ACL, credential redaction, carve-out preservation, deterministic MinVer emission, and runtime publish/teardown contracts are enforced in CI as code.

## Acceptance Criteria

### AC1 - Fitness test classes exist and map to deployment contracts

Given the deploy-validation test project,
when `tests/Hexalith.Parties.DeployValidation.Tests/` is inspected,
then it contains or refactors toward the following sealed test classes:

| Class | Required contract coverage |
|---|---|
| `K8sManifestGenerationTests` | Per-service folder emission from Story 9.2; byte-determinism of non-image lines for generated app Deployments; generated folder set is exactly `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`, `memories`. |
| `K8sManifestPublishTests` | MinVer-shaped registry image tags, `imagePullSecrets` on registry-backed Deployments, Dapr annotations on five Dapr apps only, JWT `secretKeyRef` on five daprd-equipped apps, cross-patch idempotency on a second publish-script patch pass. |
| `DaprAccessControlFitnessTests` | `defaultAction: deny` in all five ACL configs, no wildcard `appId`, no global wildcard operations, topology call allowlist matches docs: `parties` may call `tenants`/`eventstore` through Dapr service invocation; Memories search updates use the in-cluster Memories Service URL and `accesscontrol-memories` remains deny-only; `tenants` may not call `parties`; `parties-mcp` and `eventstore-admin-ui` do not appear as Dapr callers. |
| `DaprSubscriptionFitnessTests` | Exactly two declarative subscriptions, documented topics and routes, explicit scopes, dead-letter topics, and resiliency compatibility with `resiliency.yaml`. |
| `CarveOutPreservationFitnessTest` | Simulated publish cleanup/patch cycle preserves `redis/` and `keycloak/`, keeps them byte-identical, and proves no Zot pull secret or Dapr annotations land in either carve-out. |
| `DocumentationFitnessTest` | Entry-point docs do not contain stale v1 script names, old local-cluster regex allowlist text, stale validator flags, `registry.hexalith.com/*:latest`, or dangling forward references; every deployment entry-point doc links to `docs/kubernetes-deployment-architecture.md`. |
| `ValidateDeploymentLintFitnessTests` | Curated fixture matrix for the eight Story 9.6 lint categories, valid baseline, near misses, JSON schema version `1`, exact category strings, and safe diagnostics. Existing `ValidateDeploymentLintToolingTests` can be renamed or kept if the final suite still exposes this coverage clearly. |
| `CredentialLeakPoisonSweepTest` | Runs scripts through a mocked or sandboxed target, captures stdout/stderr, and proves no credential-shaped or cluster-secret-shaped value leaks. |

And each class must be `sealed`, use xUnit v3 + Shouldly, and be placed in namespace `Hexalith.Parties.DeployValidation.Tests`.

And deploy-validation fitness tests must stay scoped to deployment artifacts, scripts, and docs. Do not duplicate application architecture checks that belong in the architecture fitness surface.

### AC2 - Default pass remains pure and context-free

Given the default test pass runs with:

```bash
/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release
```

then all fitness tests from AC1 except `LiveCluster*` run without Kubernetes reachability, without `KUBECONFIG`, without Docker, without pushing images, without `dapr init`, and without mutating the committed `deploy/` tree.

And default-pass tests may invoke local scripts only when the target is a copied temp workspace or command-shim sandbox.

And default-pass tests must prove no call is made to these mutation commands unless a shim captures and asserts the exact allowed dry-run/local behavior: `kubectl apply`, `kubectl delete`, `dapr init`, `dapr uninstall`, `docker push`, `dotnet aspirate generate` against the real repo tree.

And non-LiveCluster tests must never call a real cluster, real registry, real Docker credential store, or real kubeconfig. If a test needs external commands, it must prepend a temp `bin/` directory to `PATH` containing fake executables and assert the captured command log.

### Required command-shim contract

Default-pass script tests must use a single, consistent shim contract:

- Fake executables live under a per-test temp directory such as `<temp>/bin/`.
- Tests prepend that temp `bin/` to `PATH` for the child process only.
- Each fake executable appends one line per invocation to `<temp>/commands.log` in the form `<command> <argv>`.
- Shims must return deterministic exit codes and deterministic stdout/stderr based on the test fixture.
- Tests must assert both the script exit code and the captured command log.
- Tests must assert the committed `deploy/` tree is unchanged after the script invocation.
- No production script should be modified solely to support tests unless the change is a narrow, documented testability seam that preserves operator behavior.

### AC3 - LiveCluster tests are explicit opt-in and sandbox-gated

Given the live-cluster suite is separate from default fitness tests,
when the operator invokes:

```bash
/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --filter Category=LiveCluster
```

then only LiveCluster tests run.

And the live suite contains at most these probes unless the story records a reason to expand:

- `LiveCluster_PublishHappyPath`: runs `publish.ps1 -ConfirmContext <test-context>` against a sandbox kubeconfig and asserts 9 pods become Ready inside the documented budget.
- `LiveCluster_TeardownClean`: runs `teardown.ps1 -ConfirmContext <test-context>` and asserts residual-state probe success.
- `LiveCluster_IdempotentRepublish`: runs `publish.ps1` twice and asserts the second run produces zero `kubectl apply` changes.

And LiveCluster tests must require both:

- `[Trait("Category", "LiveCluster")]`
- `KUBECONFIG_TEST_PATH` pointing to a sandbox kubeconfig.

And if `KUBECONFIG_TEST_PATH` is absent, LiveCluster tests must skip with an explicit reason, not fail the default suite.

And if `KUBECONFIG_TEST_PATH` is present but does not point to an existing file, or the active context is not the expected sandbox context, LiveCluster tests must fail before mutation.

And LiveCluster tests must set `KUBECONFIG` for child processes from `KUBECONFIG_TEST_PATH`; they must never fall back to `~/.kube/config`, the ambient `KUBECONFIG`, or `kubectl config current-context` from the developer's default environment.

And normal CI must not run LiveCluster tests unless the job explicitly selects `Category=LiveCluster` and provides `KUBECONFIG_TEST_PATH`.

### AC4 - Curated fixture trees are reviewable and complete

Given fixtures are inspected under `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/`,
then they are hand-curated text fixtures, not generated dumps, and include:

- `valid-deploy-tree/`: valid baseline Dapr/K8s tree.
- `lint-negative/<category>/`: one negative tree per Story 9.6 lint category: `K8sWorkload-MissingImagePullSecret`, `K8sWorkload-MissingDaprAnnotations`, `K8sWorkload-MissingProbes`, `K8sWorkload-NonSemVerTag`, `K8sWorkload-DirtyTagOnConsumerImage`, `DaprACL-WildcardAppId`, `DaprACL-WildcardOperation`, `Secret-Plaintext`.
- `lint-near-miss/`: valid SemVer, documented Dapr prefix wildcard routes such as `/api/v1/.../**`, vendor images outside `registry.hexalith.com/`, placeholder secrets, and unrelated literal asterisks.
- `carve-out-preservation/`: Redis/Keycloak carve-out baseline plus a simulated generated workspace.
- `byte-determinism/`: before/after generated Deployment samples proving non-image-line stability.
- `minver-edge-cases/`: clean release, preview, and dirty tag samples.
- `stale-docs/`: small positive/negative documentation samples for the stale-contract test.
- `poisoned-secrets/`: fake secret-bearing files with raw and encoded-looking poison values.
- `publish-workspace/`: minimal temp-copy source for publish/teardown shim tests.

And each fixture tree is reviewable as a unit: YAML/JSON/text only, no binaries, no opaque generated blobs, and preferably under 50 KB per tree.

And fixtures are contract assets. Do not regenerate them mechanically from the current `deploy/` tree without trimming and reviewing the resulting files.

### AC5 - Documentation fitness closes stale deployment docs

Given deployment docs are searched,
when `DocumentationFitnessTest` runs,
then these stale patterns are rejected unless the file is explicitly historical under `_bmad-output/`:

- `regen.ps1`
- `deploy-local.ps1`
- `teardown-local.ps1`
- `-AllowCloudCapabilities`
- `--output json` for `validate-deployment.ps1` examples; docs should use `-Format json`, while Story 9.6 script keeps `-Output json` as a compatibility alias.
- `kind-*`, `minikube`, `docker-desktop`, `k3d-*` as an allowlist/gate for deployment scripts.
- `registry.hexalith.com/` image snippets ending in `:latest` or `:staging-latest`.

And at minimum these live docs are covered: `docs/kubernetes-deployment-architecture.md`, `deploy/k8s/README.md`, `docs/deployment-guide.md`, `docs/getting-started.md`, `docs/deployment-security-checklist.md`.

Developer note: `docs/deployment-security-checklist.md` currently contains stale validator examples (`--output json`, `-AllowCloudCapabilities`, missing required `-K8sPath` in one example, and v1-era category wording). Either update it in this story or make the documentation fitness test fail first and then fix it.

And this is a stale-contract test, not a prose-quality linter. It must target named live deployment docs and curated `stale-docs/` fixtures; it must not scan the whole repo except for explicit credential poison sweeps.

And the test must allow intentional historical references under `_bmad-output/` and explicit "superseded by" references in live docs when those references direct the reader to the current v2 path.

And every deployment entry document below must link to `docs/kubernetes-deployment-architecture.md` or a repo-relative equivalent:

- `deploy/k8s/README.md`
- `docs/deployment-guide.md`
- `docs/getting-started.md`
- `docs/deployment-security-checklist.md`

Do not ban valid `-ConfirmContext` guidance. `-ConfirmContext` remains current for `publish.ps1` and `teardown.ps1`; the stale case is adding it to `validate-deployment.ps1`.

### AC6 - ValidateDeployment lint fixture matrix preserves Story 9.6 API

Given Story 9.6 delivered `deploy/validate-deployment.ps1`,
when `ValidateDeploymentLintFitnessTests` run against curated fixtures,
then the tests assert:

- Human output format and summary line remain stable.
- JSON output has schema version `"1"` and exact fields `{ severity, category, file, jsonpath, reason }` plus summary `{ findings, blocking, warnings, status }`.
- Category strings are exactly the eight Story 9.6 category strings.
- Exit-code precedence remains: `2` invalid args/self-check, `3` missing path, `1` findings, `0` pass.
- The script remains context-free and does not import `_lib/Confirm-KubeContext.ps1`.
- `deploy/dapr-alternatives/` is skipped unless explicitly supplied as `-ConfigPath`.
- Suspicious values never appear in human output, JSON output, stderr, parser diagnostics, or test assertion messages.

And this story may refactor existing `ValidateDeploymentLintToolingTests.cs`, but it must not weaken any existing assertion from Story 9.6 review patches.

Required golden assertions:

- Exact category set, no more and no less: `DaprACL-WildcardAppId`, `DaprACL-WildcardOperation`, `K8sWorkload-DirtyTagOnConsumerImage`, `K8sWorkload-MissingDaprAnnotations`, `K8sWorkload-MissingImagePullSecret`, `K8sWorkload-MissingProbes`, `K8sWorkload-NonSemVerTag`, `Secret-Plaintext`.
- JSON schema `version` equals `"1"`.
- Exit codes remain `0`, `1`, `2`, and `3` only for the documented validator contract.
- GNU-style `--config-path` remains accepted.
- `-Format json` remains canonical and `-Output json` remains compatibility-only.
- Validator source contains no `-ConfirmContext`, no `_lib/Confirm-KubeContext.ps1`, and no cluster mutation commands.

### AC7 - Publish and teardown behavior is tested through safe seams

Given `publish.ps1` and `teardown.ps1` are mutation scripts,
when default-pass tests exercise their behavior,
then they must run against temp-copied script workspaces and command shims rather than the live cluster or committed tree.

And tests must cover at least:

- `publish.ps1` cleanup preserves `redis`, `keycloak`, `kustomization.yaml`, `namespace.yaml`, `README.md`, `publish.ps1`, `teardown.ps1`, and `_lib`.
- Dapr annotation patch is idempotent and targets only `eventstore`, `eventstore-admin`, `parties`, `tenants`, and `memories`.
- JWT `secretKeyRef` patch is idempotent and targets the five daprd-equipped app Deployments.
- `imagePullSecrets` patch is idempotent, applies only to `registry.hexalith.com/*` consumers, and never patches `redis` or `keycloak`.
- Zot docker config handling never decodes or prints auth values.
- Teardown residual-state detection exits 7 with bounded names and no cluster-secret material.

And publish/teardown tests must use the command-shim contract from AC2. They must not add hidden test switches to the production scripts when a temp workspace and fake command log can prove the behavior.

### AC8 - Failure messages are useful but redacted

Given any deploy-validation test fails,
then its failure message includes the artifact path and a category or contract name.

And if a failure involves credential-shaped data, the message must include only a descriptor such as `jwt-shaped`, `base64-shaped`, `docker-auth-shaped`, or `password-prefixed`, never the original value.

And tests must avoid absolute user-machine paths in expected output unless the path is purely a temp workspace diagnostic.

Required poison examples:

- JWT-shaped token beginning with `eyJ`.
- Base64-looking value of at least 40 characters.
- Password-prefixed or token-prefixed value such as `Password=...`, `client_secret=...`, or `Authorization: Bearer ...`.
- Docker config JSON containing an `auths` object.
- Kubeconfig-looking content containing `certificate-authority-data`, `client-key-data`, or bearer token fields.
- Redis or SQL-style connection string with an inline password.

Poison tests must assert both detection and redaction. The original poison string must not appear in stdout, stderr, JSON output, exception text, assertion messages, command logs, copied manifests, or generated temp files that are retained after failure.

### AC9 - Verification commands pass

Given implementation is complete,
then run and record:

```bash
pwsh -NoProfile -File deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/
pwsh -NoProfile -File deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/ -Format json
/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release
/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --filter "Category!=LiveCluster"
git diff --check
```

And do not run the LiveCluster tests unless the operator provides a sandbox kubeconfig and explicitly asks for them.

## Tasks / Subtasks

- [x] Task 1 - Rationalize deploy-validation test structure (AC: 1, 2)
  - [x] Keep `DaprManifestValidationTests` coverage or split it into `DaprAccessControlFitnessTests` and `DaprSubscriptionFitnessTests`.
  - [x] Keep `OperatorScriptValidationTests` coverage or split publish/teardown-focused tests into `K8sManifestPublishTests` and `CredentialLeakPoisonSweepTest`.
  - [x] Keep `ValidateDeploymentLintToolingTests` coverage or rename/refactor it into `ValidateDeploymentLintFitnessTests`.
  - [x] Ensure final classes are sealed and class names make failure ownership obvious.

- [x] Task 2 - Add curated fixture tree structure (AC: 4, 6, 8)
  - [x] Create `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/` with the named subfolders required by AC4.
  - [x] Keep fixtures text-only and reviewable; avoid copied full generated trees unless trimmed to contract-relevant files.
  - [x] Replace large inline YAML strings with fixture files where readability improves.

- [x] Task 3 - Add manifest generation and publish fitness coverage (AC: 1, 2, 7)
  - [x] Assert exact generated service folder set under `deploy/k8s/`.
  - [x] Assert byte-determinism rules for non-image lines and carve-out files.
  - [x] Assert MinVer tag regex and dirty-tag rejection path align with docs and validator.
  - [x] Assert Dapr/JWT/imagePullSecrets patch idempotency in a temp workspace.

- [x] Task 4 - Add Dapr ACL and subscription fitness coverage (AC: 1, 6)
  - [x] Assert five ACL configs deny by default and use explicit callers.
  - [x] Assert documented route-map behavior, including the post-9.6 decision that documented `/**` prefix routes are allowed while global/loose wildcards are not.
  - [x] Assert two subscriptions map expected topics, routes, scopes, and dead-letter topics.
  - [x] Assert resiliency references are bounded and attached to declared targets.

- [x] Task 5 - Add documentation fitness and fix stale docs (AC: 5)
  - [x] Add `DocumentationFitnessTest` with stale-pattern checks.
  - [x] Update `docs/deployment-security-checklist.md` from v1-era validator guidance to the delivered Story 9.6/9.7 behavior.
  - [x] Confirm the four named deployment entry-point docs link to `docs/kubernetes-deployment-architecture.md`.

- [x] Task 6 - Add LiveCluster opt-in tests (AC: 3, 9)
  - [x] Add `LiveCluster_PublishHappyPath`, `LiveCluster_TeardownClean`, and `LiveCluster_IdempotentRepublish`.
  - [x] Mark each with `[Trait("Category", "LiveCluster")]`.
  - [x] Skip with an explicit reason when `KUBECONFIG_TEST_PATH` is absent.
  - [x] Fail before mutation when `KUBECONFIG_TEST_PATH` is invalid or the active context is not the expected sandbox context.
  - [x] Set child-process `KUBECONFIG` from `KUBECONFIG_TEST_PATH`; never use the default kubeconfig silently.
  - [x] Ensure default test runs exclude LiveCluster by default without needing a filter.

- [x] Task 7 - Verify and update story record (AC: 9)
  - [x] Run the two validator commands.
  - [x] Run the deploy-validation test project default pass.
  - [x] Run the explicit non-LiveCluster filter pass.
  - [x] Run `git diff --check`.
  - [x] Record completion notes and file list.

### Review Findings

- [x] [Review][Patch] Codify current Memories service-URL contract instead of requiring a Dapr `parties -> memories` ACL route — User chose option 2 in review: align story/docs/tests with `deploy/dapr/accesscontrol-memories.yaml`, which intentionally keeps Memories peer invocation denied because Parties uses the in-cluster Memories HTTP API.
- [x] [Review][Patch] LiveCluster publish test does not prove all 9 pods are Ready [tests/Hexalith.Parties.DeployValidation.Tests/LiveClusterDeploymentTests.cs:22]
- [x] [Review][Patch] LiveCluster idempotent republish can pass when the second publish still changes resources [tests/Hexalith.Parties.DeployValidation.Tests/LiveClusterDeploymentTests.cs:52]
- [x] [Review][Patch] LiveCluster teardown test does not assert residual-state cleanup success [tests/Hexalith.Parties.DeployValidation.Tests/LiveClusterDeploymentTests.cs:34]
- [x] [Review][Patch] LiveCluster publish and teardown processes wait indefinitely without an outer timeout [tests/Hexalith.Parties.DeployValidation.Tests/LiveClusterDeploymentTests.cs:117]
- [x] [Review][Patch] Publish patch idempotency is tested by source string search instead of a second temp-workspace patch pass [tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestPublishTests.cs:49]
- [x] [Review][Patch] Carve-out preservation is static and does not simulate a publish cleanup/patch cycle [tests/Hexalith.Parties.DeployValidation.Tests/CarveOutPreservationFitnessTest.cs:37]
- [x] [Review][Patch] Default-pass credential redaction test invokes the repo publish script instead of a temp-copied script workspace [tests/Hexalith.Parties.DeployValidation.Tests/CredentialLeakPoisonSweepTest.cs:60]
- [x] [Review][Patch] Poison redaction assertions can leak raw poison values in failure messages [tests/Hexalith.Parties.DeployValidation.Tests/CredentialLeakPoisonSweepTest.cs:39]
- [x] [Review][Patch] Kubeconfig-shaped context poison emitted by the kubectl shim is not in the exact no-leak assertion set [tests/Hexalith.Parties.DeployValidation.Tests/CredentialLeakPoisonSweepTest.cs:58]
- [x] [Review][Patch] Docker config `auths` poison example and `docker-auth-shaped` assertion are missing [tests/Hexalith.Parties.DeployValidation.Tests/CredentialLeakPoisonSweepTest.cs:9]

## Dev Notes

### Current State And Files To Touch

Primary existing files to update:

- `tests/Hexalith.Parties.DeployValidation.Tests/DaprManifestValidationTests.cs`: currently covers exact Dapr file set, headers, Redis component metadata, ACL default deny/no wildcard basics, subscriptions, resiliency policy bounds, and EventStore registration-key env binding. Preserve these contracts while expanding/splitting coverage.
- `tests/Hexalith.Parties.DeployValidation.Tests/OperatorScriptValidationTests.cs`: currently covers shared context helper, confirm-context mismatch, publish phase markers, cleanup/patch/secret guardrails, Dapr apply order, teardown residual behavior, and temp script workspaces. Reuse this rather than starting from scratch.
- `tests/Hexalith.Parties.DeployValidation.Tests/ValidateDeploymentLintToolingTests.cs`: currently covers Story 9.6 process-level validator behavior, exact eight categories, near misses, JSON schema, redaction, no cluster/mutation commands, and helper methods for synthetic fixtures. Do not weaken these tests.
- `tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj`: already references `xunit.v3`, `xunit.runner.visualstudio`, `Shouldly`, `YamlDotNet`, and configuration packages. Keep package versions centralized in `Directory.Packages.props`.
- `docs/deployment-security-checklist.md`: stale. It still references `-AllowCloudCapabilities`, `--output json`, incomplete validator invocations, v1 category wording, and old Dapr/state-store assumptions. This is a likely required doc update for AC5.

Possible new files:

- `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/**`
- `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestGenerationTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestPublishTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/DaprAccessControlFitnessTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/DaprSubscriptionFitnessTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/CarveOutPreservationFitnessTest.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/DocumentationFitnessTest.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/CredentialLeakPoisonSweepTest.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/LiveClusterDeploymentTests.cs`

Files to read and preserve before editing:

- `deploy/validate-deployment.ps1`: delivered by Story 9.6 as a PowerShell 7, context-free, read-only validator with exact category strings and schema version `1`. Fitness tests may expose defects, but do not rewrite it unless a test proves a contract break.
- `deploy/k8s/publish.ps1`: 13-step mutation pipeline with `-ConfirmContext`, MinVer resolution, cleanup, aspirate generate, Dapr/JWT/imagePullSecrets patches, Dapr init/apply, Secret bootstrap, and Kustomize apply. Default-pass tests must use temp workspaces/shims.
- `deploy/k8s/teardown.ps1`: context-gated teardown with residual-state probe and guarded purge switches. Default-pass tests must use temp workspaces/shims.
- `deploy/k8s/_lib/Confirm-KubeContext.ps1`: shared by publish/teardown only; `validate-deployment.ps1` must not import it.
- `deploy/dapr/*.yaml`: production Dapr CR set from Story 9.4; test it, do not alter topology unless the story records an explicit architecture correction.
- `deploy/k8s/{eventstore,eventstore-admin,eventstore-admin-ui,parties,parties-mcp,tenants,memories,redis,keycloak}/deployment.yaml`: current workload manifests that define the default-pass baseline.
- `docs/kubernetes-deployment-architecture.md`: canonical deployment architecture.
- `deploy/k8s/README.md`, `docs/deployment-guide.md`, `docs/getting-started.md`, `docs/deployment-security-checklist.md`: deployment entry-point docs for documentation fitness.

### Current Deployment Tree Observations

- `deploy/dapr/` currently contains exactly ten production Dapr CR files: `statestore.yaml`, `pubsub.yaml`, `resiliency.yaml`, five `accesscontrol*.yaml`, and two `subscription-*.yaml`.
- `deploy/k8s/` currently contains seven generated application folders, two hand-authored carve-outs (`redis`, `keycloak`), top-level `namespace.yaml`, `kustomization.yaml`, `README.md`, `publish.ps1`, `teardown.ps1`, and `_lib/Confirm-KubeContext.ps1`.
- `deploy/validate-deployment.ps1` currently sorts findings by file/category/jsonpath/reason and returns exact exit codes `0`, `1`, `2`, `3`.
- `ValidateDeploymentLintToolingTests.cs` currently uses inline temp fixture strings. Story 9.7 should move the broader matrix to curated fixture files where practical.
- Existing `OperatorScriptValidationTests.cs` already contains temp workspace and command-shim patterns. Reuse and standardize that style rather than inventing a second script-test harness.
- There is no `tests/Hexalith.Parties.Architecture.Tests` directory in this worktree; do not invent cross-project architecture tests for this deployment-only story.
- The working tree was clean before story creation.

### Previous Story Intelligence

Story 9.6 delivered and reviewed the validator. Critical learnings to carry forward:

- Exact category strings are output API. Tests must assert them literally.
- Dapr ACL wildcard contract was corrected after review: documented prefix wildcard routes such as `/api/v1/.../**` are allowed; bare/global wildcard operations (`*`, `/**`) and loose trailing `/*` remain blocking.
- Several review patches fixed file-wide checks that should have been scoped to pod templates or primary containers. New tests should target structure, not just text presence.
- Redaction self-checks must pass sentinel values through the actual renderers, not a bypass path.
- Parser diagnostics and invalid-argument errors must redact suspicious values.
- Case-sensitive matching matters in PowerShell; tests should catch accidental case-insensitive matches where YAML keys/app ids/category names are case-sensitive.
- Story 9.6 intentionally did not own the full fixture matrix or LiveCluster suite. This story does.

Recent commit pattern:

- `736ab89 feat(deploy): add story 9-6 validation linting`
- `9eab497 feat(deploy): add story 9-5 operator scripts`
- `2da6141 feat(deploy): add story 9-4 dapr manifests`
- `4273b59 feat(deploy): story 9-3 add redis and keycloak carve-outs`
- `6dbcb6e feat(deploy): story 9-2 aspirate apphost composition`

### Architecture Compliance

- Canonical architecture: `docs/kubernetes-deployment-architecture.md` sections 1-13.
- ADRs: `_bmad-output/planning-artifacts/architecture.md` D-K8s-2, D-K8s-3, and D-K8s-4.
- Epic source: `_bmad-output/planning-artifacts/epics.md` Story 9.7.
- Sprint-change source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-21-epic9-greenfield-rewrite.md`.
- Project context: .NET SDK `10.0.300`, target `net10.0`, xUnit v3, Shouldly, YamlDotNet in tests, central package management in `Directory.Packages.props`.
- Dapr runtime in canonical doc is pinned to `1.14.4`, while package references are currently `Dapr.*` `1.17.9`; do not change either unless the implementation finds and records a concrete compatibility issue.
- Aspirate tool is pinned to `9.1.0` in `.config/dotnet-tools.json`; AppHost SDK baseline is `13.3.3`.

### Latest Technical Notes

- Official Dapr docs currently present `v1.17` as latest and `v1.18` as preview, but the project architecture still pins the Kubernetes control-plane baseline to `1.14.4`; treat that as a documented baseline, not an implicit upgrade instruction. [Source: Dapr init CLI docs, version selector; `docs/kubernetes-deployment-architecture.md` section 13]
- Dapr CLI documents `dapr init -k` for Kubernetes, `--wait`, and `--timeout`; Story 9.5 already uses `dapr init -k --wait --timeout 300`. Fitness tests should assert the repo contract, not add Helm management. [Source: Dapr Kubernetes deployment docs]
- Kubernetes `kubectl wait --for=condition=Ready pod/...` is the official form for waiting on pod readiness; LiveCluster happy path can use selector-based waits but should report the selector and timeout in failure messages. [Source: Kubernetes `kubectl wait` reference]
- Microsoft `dotnet test` VSTest filtering supports xUnit `Category`; use `[Trait("Category", "LiveCluster")]` with `--filter Category=LiveCluster` for explicit opt-in. [Source: Microsoft Learn `dotnet test` filter details]
- xUnit.net v3 guidance identifies `xunit.v3` as the typical package for test authors; this repo already references `xunit.v3` centrally and should not add ad hoc xUnit packages. [Source: xUnit.net v3 package guidance]

## References

- [Source: `_bmad-output/planning-artifacts/epics.md` Story 9.7]
- [Source: `_bmad-output/planning-artifacts/architecture.md` D-K8s-2, D-K8s-3, D-K8s-4]
- [Source: `docs/kubernetes-deployment-architecture.md`]
- [Source: `_bmad-output/implementation-artifacts/9-6-validate-deployment-lint-tooling.md`]
- [Source: `deploy/validate-deployment.ps1`]
- [Source: `tests/Hexalith.Parties.DeployValidation.Tests/DaprManifestValidationTests.cs`]
- [Source: `tests/Hexalith.Parties.DeployValidation.Tests/OperatorScriptValidationTests.cs`]
- [Source: `tests/Hexalith.Parties.DeployValidation.Tests/ValidateDeploymentLintToolingTests.cs`]
- [External: Dapr init CLI docs: https://docs.dapr.io/reference/cli/dapr-init/]
- [External: Dapr Kubernetes deployment docs: https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-deploy/]
- [External: Kubernetes `kubectl wait` reference: https://kubernetes.io/docs/reference/kubectl/generated/kubectl_wait/]
- [External: Microsoft `dotnet test` filter docs: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-vstest]
- [External: xUnit.net v3 package guidance: https://xunit.net/docs/nuget-packages-v3]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-22 18:14:19 +02:00 - Marked sprint-status story `9-7-deployment-fitness-and-live-cluster-integration` in-progress.
- 2026-05-22 - Red phase: initial deploy-validation test run failed on new compile assertions, then runtime failures exposed stale docs and overly broad carve-out/publish assertions.
- 2026-05-22 - Green/refactor: fixed assertion precision, updated stale deployment docs, and re-ran deploy-validation tests to green.
- 2026-05-22 - Full solution regression attempted with `Category!=LiveCluster`; unrelated pre-existing failures remain in Sample docs path separators, Client tests, and search benchmark thresholds.

### Completion Notes List

- Story context created 2026-05-22 by create-story workflow.
- Party-mode review fixes applied 2026-05-22: fixture naming, command-shim contract, LiveCluster skip/fail gates, validator golden assertions, documentation stale-contract scope, and credential poison examples.
- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added sealed Story 9.7 fitness classes for K8s manifest generation, publish contracts, Dapr ACL/subscriptions, carve-out preservation, documentation stale contracts, validator fixture matrix, credential poison redaction, and opt-in LiveCluster probes.
- Added a curated `Fixtures/` tree with valid, negative, near-miss, carve-out, byte-determinism, MinVer, stale-docs, poisoned-secret, and publish-workspace assets.
- Updated live deployment documentation by removing stale validator flags/output examples and local-cluster allowlist wording from `docs/deployment-security-checklist.md` and `docs/getting-started.md`.
- Verification passed: validator human output, validator JSON output, deploy-validation default test pass, deploy-validation `Category!=LiveCluster` pass, and `git diff --check`.
- LiveCluster tests were not executed against a cluster because no sandbox `KUBECONFIG_TEST_PATH` was provided; default pass reports them as explicit skips.
- Broader `dotnet test Hexalith.Parties.slnx -c Release --filter "Category!=LiveCluster"` was attempted and failed only outside Story 9.7 scope: sample docs Linux path separator file lookups, existing client tests, and search benchmark timing thresholds.
- Code-review patches applied 2026-05-22: aligned Memories service-URL contract, hardened LiveCluster outcome assertions/timeouts, added repeated publish idempotency and carve-out preservation coverage, moved redaction confirm-context test to a temp script workspace, sanitized poison assertion failures, and added Docker auth poison coverage.
- Verification after code-review patches passed: validator human output, validator JSON output, deploy-validation default test pass 56 passed / 3 LiveCluster skipped, deploy-validation `Category!=LiveCluster` pass 56/56, and `git diff --check`.

### File List

- `_bmad-output/implementation-artifacts/9-7-deployment-fitness-and-live-cluster-integration.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/deployment-security-checklist.md`
- `docs/getting-started.md`
- `docs/kubernetes-deployment-architecture.md`
- `tests/Hexalith.Parties.DeployValidation.Tests/CarveOutPreservationFitnessTest.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/CredentialLeakPoisonSweepTest.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/DaprAccessControlFitnessTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/DaprSubscriptionFitnessTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/DeploymentTestPaths.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/DocumentationFitnessTest.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/`
- `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestGenerationTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestPublishTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/LiveClusterDeploymentTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/ValidateDeploymentLintFitnessTests.cs`

### Change Log

- 2026-05-22 - Implemented Story 9.7 deployment fitness tests and LiveCluster opt-in coverage; updated stale deployment docs; moved story to review.
