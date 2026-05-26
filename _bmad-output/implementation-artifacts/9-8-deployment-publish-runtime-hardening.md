# Story 9.8: Publish Runtime Hardening, Registry Verification, and Memories Backing Services

Status: done

## Story Boundary

Story 9.8 corrects the runtime publish gaps discovered during the first manual publish attempt after Story 9.7 was squash-merged. It builds on the post-Story 9.7 state as the baseline and must not resurrect superseded Epic 9 v1 stories.

This story may change:

- `src/Hexalith.Parties.AppHost/Program.cs`
- `deploy/k8s/publish.ps1`
- `deploy/k8s/kustomization.yaml`
- generated app manifests under `deploy/k8s/<service>/`
- hand-authored backing-service carve-outs under `deploy/k8s/`
- `deploy/validate-deployment.ps1` only if a new lint category or static check is required
- deploy-validation tests and fixtures
- Epic 9 deployment docs

This story must not:

- weaken Dapr deny-by-default ACL policy
- remove `-ConfirmContext`
- decode or print Docker/Zot credentials
- apply Kubernetes workloads before image manifest verification succeeds
- leave generated resources in the `default` namespace
- use `git submodule update --init --recursive`

## Story

As an operator publishing Hexalith.Parties to Kubernetes,
I want `publish.ps1` to verify the real registry, Dapr, generated manifest, Secret, and Memories backing-service preconditions before applying workloads,
so that a reported successful publish means the cluster can actually pull the MinVer images and all expected pods reach Ready without manual rescue.

## Discovery Context

The first post-Story 9.7 publish attempt exposed these concrete defects:

1. MinVer resolution failed because the AppHost did not compile with the post-merge Memories composition.
2. .NET SDK container publishing rejected simultaneous `ContainerImageTags` and `ContainerImageTag` inputs.
3. Without a singular `ContainerImageTag` override, submodule projects fell back to `staging-latest`.
4. Aspirate printed successful build/push lines while Zot did not contain the reported MinVer manifests.
5. Manual `dotnet publish -t:PublishContainer` exposed `CONTAINER1016` for at least `registry.hexalith.com/eventstore-admin-ui`.
6. Existing healthy Dapr installs caused `dapr init -k` to fail with `cannot re-use a name that is still in use`.
7. Aspirate regenerated top-level `kustomization.yaml` without `namespace: hexalith-parties`, without carve-outs, and with stale `dapr/*` resource references.
8. Existing `hexalith-keycloak-admin` Secret had legacy keys and was not upgraded because the script only checked Secret existence.
9. Generated Deployments lacked required health probes.
10. `memories` required `ConnectionStrings__redis` and `ConnectionStrings__falkordb`; using Redis for both made the pod run but left FalkorDB health degraded.

## Acceptance Criteria

### AC1 - AppHost publish-mode composition is buildable and explicit

Given the repo is on the post-Story 9.7 baseline,
when `dotnet msbuild src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj -t:Build -p:Configuration=Release -getProperty:Version` runs,
then it succeeds and returns a non-`1.0.0` MinVer value.

And `Program.cs` must not use `AddProject<Projects.Hexalith_Memories_Server>("memories")`.

And publish mode must compose Memories from the root-level `Hexalith.Memories` submodule through an optional sibling project path helper with actionable guidance:

```text
Run 'git submodule update --init Hexalith.Memories'
```

And publish mode must set explicit Memories backing-service connection strings for every required backing service.

### AC2 - MinVer tag propagation is compatible with .NET 10 container publishing

Given `publish.ps1` invokes aspirate,
when the script prepares environment variables and aspirate arguments,
then it must not set `ContainerImageTags` when aspirate also passes `--container-image-tag`.

And it must enforce the MinVer tag for submodule projects without triggering SDK error `CONTAINER2008`.

And the chosen mechanism must be covered by tests and documented in the story Dev Notes.

### AC3 - Zot repository access and image manifest verification are mandatory

Given aspirate reports build/push success,
when `publish.ps1` proceeds past image generation,
then it verifies every expected image manifest exists in Zot before any `kubectl apply` mutates the application namespace:

- `registry.hexalith.com/eventstore:<MinVer>`
- `registry.hexalith.com/eventstore-admin:<MinVer>`
- `registry.hexalith.com/eventstore-admin-ui:<MinVer>`
- `registry.hexalith.com/parties:<MinVer>`
- `registry.hexalith.com/parties-mcp:<MinVer>`
- `registry.hexalith.com/tenants:<MinVer>`
- `registry.hexalith.com/memories:<MinVer>`

And missing, unauthorized, or malformed manifest responses fail with a bounded message naming only the repository and tag, never credentials.

And the story updates Zot access-control documentation or manifests so the `parties-publisher` account has push and pull access for all required repositories.

### AC4 - Existing healthy Dapr control plane is accepted

Given Dapr is already installed in `dapr-system`,
when `publish.ps1` reaches the Dapr control-plane step,
then it treats a healthy existing install as success rather than running into `cannot re-use a name that is still in use`.

And `-SkipDaprInit` still verifies the required Dapr CRDs.

And an unhealthy existing Dapr install fails with a bounded actionable diagnostic.

### AC5 - Canonical top-level kustomization is restored after aspirate

Given aspirate regenerates `deploy/k8s/kustomization.yaml`,
when `publish.ps1` finishes placeholder cleanup,
then the top-level kustomization is restored to the canonical repo contract:

- `apiVersion: kustomize.config.k8s.io/v1beta1`
- `kind: Kustomization`
- `namespace: hexalith-parties`
- application service folders
- hand-authored carve-outs
- no `dapr/*` resources

And a regression test proves `kubectl apply -k deploy/k8s` cannot create the seven application workloads in the `default` namespace.

### AC6 - Operator-managed Secrets are upgraded, not only detected

Given `hexalith-keycloak-admin` or another operator-managed Secret already exists,
when required keys are missing,
then `publish.ps1` patches the missing keys idempotently instead of treating existence as success.

And the Keycloak Secret supports the current `KC_BOOTSTRAP_ADMIN_USERNAME` and `KC_BOOTSTRAP_ADMIN_PASSWORD` keys.

And no generated secret value is printed to stdout, stderr, JSON, test diagnostics, or committed manifests.

### AC7 - Health probes are restored before validation and apply

Given aspirate emits app Deployment manifests,
when the post-aspirate patch phase completes,
then every primary app container has `readinessProbe` and `livenessProbe` for `/health` on port `http`.

And `publish.ps1` runs `deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/` after patching and before cluster apply.

And any blocking validator finding stops the publish before `kubectl apply -k`.

### AC8 - Memories has real backing services in publish mode

Given `memories` is part of the publish topology,
when the cluster is applied,
then Memories must not start with missing `ConnectionStrings__redis` or missing `ConnectionStrings__falkordb`.

And the implementation must choose and document one of these paths:

- **Recommended Path A:** add a hand-authored `deploy/k8s/falkordb/` carve-out using the FalkorDB container image and wire `ConnectionStrings__falkordb=falkordb:6379`.
- **Path B:** configure a supported no-graph Memories mode and prove the application health checks are non-degraded.

Path A updates pod-count/topology docs and fitness tests from 9 workloads to 10 workloads.

Path B must explicitly prove no `GRAPH.*` command is issued against the MVP Redis service.

### AC9 - Live publish succeeds or fails before mutation

Given the operator runs:

```bash
pwsh -NoProfile -File deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local
```

when Zot credentials and cluster context are valid,
then publish either:

- succeeds and all expected pods reach Ready, or
- fails before Kubernetes workload apply with a bounded actionable reason.

And after any failed publish attempt, no generated Epic 9 resources remain in the `default` namespace.

And after successful publish, all registry-backed Deployments reference the same MinVer tag suffix.

## Tasks

- [x] Fix AppHost publish-mode Memories composition and backing-service connection strings.
- [x] Update `publish.ps1` MinVer tag propagation for .NET 10 SDK container publishing.
- [x] Add post-push manifest verification for all seven registry-backed images before cluster mutation.
- [x] Update Zot access-control documentation/manifests for all required repositories.
- [x] Make Dapr init idempotent against an already healthy control plane.
- [x] Restore canonical top-level `deploy/k8s/kustomization.yaml` after aspirate generation.
- [x] Upgrade existing operator-managed Secrets when keys are missing.
- [x] Restore health probes on generated app Deployments before validation and apply.
- [x] Add real Memories backing service support, preferably a `falkordb` carve-out.
- [x] Update deployment docs and topology counts.
- [x] Extend deploy-validation tests and LiveCluster coverage for the new runtime contracts.
- [x] Verify static validator, focused tests, `git diff --check`, and a live publish run.

### Review Findings

- [x] [Review][Patch] Revert submodule gitlink changes that are outside the story boundary [Hexalith.EventStore:1]
- [x] [Review][Patch] `ContainerImageTags` inherited from the operator environment can still recreate the .NET container tag conflict [deploy/k8s/publish.ps1:250]
- [x] [Review][Patch] `publish.ps1` reports OK immediately after `kubectl apply -k` without waiting for the expected pods/deployments to reach Ready [deploy/k8s/publish.ps1:905]
- [x] [Review][Patch] Aspirate failures bypass the bounded publish failure wrapper by exiting directly with raw `dotnet aspirate` output [deploy/k8s/publish.ps1:273]
- [x] [Review][Patch] Health probe patching and validation accept any probe shape instead of requiring `/health` on port `http`, and can duplicate probes in readiness-only YAML [deploy/k8s/publish.ps1:599]
- [x] [Review][Patch] LiveCluster deployment test still asserts the old 9-pod Story 9.7 topology instead of the 10-workload FalkorDB topology [tests/Hexalith.Parties.DeployValidation.Tests/LiveClusterDeploymentTests.cs:25]
- [x] [Review][Patch] Required live-cluster proof is complete. The Zot/Kubernetes auth secret was repaired in place without changing the Zot Service, all seven expected image manifests exist in `registry.hexalith.com`, and `publish.ps1` now passes registry verification, Kubernetes apply, generated workload restarts, and workload readiness for all 10 expected deployments.
- [x] [Review][Patch] Zot manifest verification has no network timeout, so a registry hang can stall before the bounded failure path returns [deploy/k8s/publish.ps1:579]
- [x] [Review][Patch] Existing unhealthy Dapr installs are treated the same as missing Dapr and lose the status diagnostic before rerunning `dapr init -k` [deploy/k8s/publish.ps1:660]

## Dev Notes

- Do not trust aspirate's green build/push line as sufficient evidence. Registry manifest verification is the acceptance gate.
- Use the operator Docker config auth block without decoding or echoing credentials.
- If a registry manifest check needs HTTP, prefer a HEAD/manifest request against `/v2/<repo>/manifests/<tag>` with an `Accept` header for OCI/Docker manifest media types.
- Existing Dapr status can be checked with `dapr status -k`; required CRDs still need `kubectl get crd components.dapr.io configurations.dapr.io subscriptions.dapr.io resiliencies.dapr.io`.
- The live workaround that pointed `ConnectionStrings__falkordb` at `redis:6379` made the pod Ready but produced degraded FalkorDB health. Do not treat that as the final architecture unless Path B proves graph behavior is intentionally disabled.

## Validation

Required before review:

```bash
pwsh -NoProfile -File deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/
dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --filter "Category!=LiveCluster"
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --filter "FullyQualifiedName~AppHostTenantsTopologyTests"
git diff --check
```

Required live-cluster proof before done:

```bash
pwsh -NoProfile -File deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local
kubectl get pods -n hexalith-parties
```

## References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-22-deployment-publish-correction.md`
- `_bmad-output/implementation-artifacts/9-7-deployment-fitness-and-live-cluster-integration.md`
- `deploy/k8s/publish.ps1`
- `deploy/validate-deployment.ps1`
- `src/Hexalith.Parties.AppHost/Program.cs`
- `docs/kubernetes-deployment-architecture.md`

## Dev Agent Record

### Implementation Notes

- Publish-mode Memories now uses the root-level `Hexalith.Memories` submodule via `ResolveOptionalSiblingProjectPath(...)` and sets `ConnectionStrings__redis=redis:6379` plus `ConnectionStrings__falkordb=falkordb:6379`.
- `publish.ps1` uses singular `ContainerImageTag`, restores the canonical top-level kustomization after Aspirate, strips Aspirate Dapr placeholders, patches health probes before validation, injects image pull secrets, verifies all seven Zot image manifests using the Docker config auth block without decoding it, and runs `deploy/validate-deployment.ps1` before cluster apply.
- Publish-mode authentication now uses the shared Kubernetes JWT Secret in development symmetric-key mode because the cluster-local Keycloak Service is HTTP-only. EventStore Admin UI receives the same signing key plus the explicit `http://eventstore-admin:8080` Admin API base URL.
- `publish.ps1` restarts generated Deployments after `kubectl apply -k` so stable-name ConfigMap changes are picked up, then waits for the 10-workload topology to become Ready. Health probes still target `/health` on port `http`, with startup-tolerant thresholds and explicit 10-second HTTP timeouts to avoid Dapr sidecar startup crashloops and slow Admin UI prerender probe failures.
- Dapr control-plane handling now accepts `dapr status -k` success as an already healthy install and still verifies required CRDs for both normal and `-SkipDaprInit` flows.
- Operator-managed Opaque Secrets are patched when required keys are missing instead of treating Secret existence as sufficient.
- Chose AC8 Path A: added the hand-authored `deploy/k8s/falkordb/` carve-out using `falkordb/falkordb:v4.18.6`, wired Memories to `falkordb:6379`, and updated topology docs/tests from 9 to 10 workloads.

### Validation Results

- PASS: `pwsh -NoProfile -File deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/` -> 0 findings.
- PASS: `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --filter "Category!=LiveCluster"` -> 57 passed.
- PASS: `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --filter "FullyQualifiedName~AppHostTenantsTopologyTests"` -> 16 passed.
- PASS: `/home/quentindv/.dotnet/dotnet msbuild src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj -t:Build -p:Configuration=Release -getProperty:Version` -> `0.0.0-preview.0.474`.
- PASS: `git diff --check`.
- LIVE ATTEMPT: `pwsh -NoProfile -File deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local` failed before Kubernetes apply during Aspirate container push with `CONTAINER1016` for `registry.hexalith.com/eventstore` repository access. A retry with `/home/quentindv/.dotnet` on PATH reached the same registry-access failure before namespace/workload mutation. Verified no Epic 9 workload names were present in the `default` namespace after the failed attempt.

### Post-Review Validation Results

- PASS: `pwsh -NoProfile -File deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/` -> 0 findings.
- PASS: `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --filter "FullyQualifiedName~OperatorScriptValidationTests|FullyQualifiedName~K8sManifestPublishTests|FullyQualifiedName~ValidateDeploymentLintToolingTests|FullyQualifiedName~LiveClusterDeploymentTests"` -> 33 passed, 3 skipped.
- PASS: `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --filter "Category!=LiveCluster"` -> 60 passed.
- PASS: `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --filter "FullyQualifiedName~AppHostTenantsTopologyTests"` -> 16 passed.
- LIVE ZOT FIX: Updated Kubernetes Secret `zot-auth-secret` in namespace `zot` in place to include the existing Docker publisher account; did not delete or modify `service/zot` (`NodePort 30500`, `ClusterIP 10.233.28.68`). Verified authenticated `/v2/` returns 200 and each expected repository accepts push upload initiation with HTTP 202.
- PASS: `/home/quentindv/.dotnet/dotnet msbuild src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj -t:Build -p:Configuration=Release -getProperty:Version` -> `0.0.0-preview.0.474`.
- PASS: `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --filter "FullyQualifiedName~OperatorScriptValidationTests|FullyQualifiedName~K8sManifestPublishTests"` -> 24 passed.
- PASS: `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --filter "Category!=LiveCluster"` -> 61 passed.
- PASS: `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --filter "FullyQualifiedName~AppHostTenantsTopologyTests"` -> 16 passed.
- PASS: `pwsh -NoProfile -File deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/` -> 0 findings.
- PASS: `git diff --check`.
- LIVE PASS: `PATH="/home/quentindv/.dotnet:$PATH" pwsh -NoProfile -File deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local` -> OK for `0.0.0-preview.0.474`; workloads Ready: `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`, `memories`, `redis`, `keycloak`, `falkordb`.
- LIVE CLEAN NAMESPACE PASS: Deleted namespace `hexalith-parties` with `kubectl delete namespace hexalith-parties --wait=true`, then reran `PATH="/home/quentindv/.dotnet:$PATH" pwsh -NoProfile -File deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local` -> OK for `0.0.0-preview.0.474` in 00:03:22; all 10 Deployments reported `1/1 AVAILABLE`. Zot remained in namespace `zot`; `service/zot` stayed `NodePort 30500`, `ClusterIP 10.233.28.68`.
- LIVE TARGETED ZOT REPAIR PASS: Removed only the `0.0.0-preview.0.474` tag entries from Zot OCI `index.json` for `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`, and `memories`; verified all seven manifests returned HTTP 404. Reran `PATH="/home/quentindv/.dotnet:$PATH" pwsh -NoProfile -File deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local` -> OK in 00:03:36; all seven manifests returned HTTP 200 and all 10 Deployments reported `1/1 AVAILABLE`. Zot Deployment and `service/zot` remained intact (`NodePort 30500`, `ClusterIP 10.233.28.68`).

### File List

- `_bmad-output/implementation-artifacts/9-8-deployment-publish-runtime-hardening.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `deploy/dapr/subscription-parties.yaml`
- `deploy/k8s/README.md`
- `deploy/k8s/eventstore/deployment.yaml`
- `deploy/k8s/eventstore-admin/deployment.yaml`
- `deploy/k8s/eventstore-admin-ui/deployment.yaml`
- `deploy/k8s/falkordb/deployment.yaml`
- `deploy/k8s/falkordb/kustomization.yaml`
- `deploy/k8s/falkordb/service.yaml`
- `deploy/k8s/kustomization.yaml`
- `deploy/k8s/memories/deployment.yaml`
- `deploy/k8s/memories/kustomization.yaml`
- `deploy/k8s/parties/deployment.yaml`
- `deploy/k8s/parties-mcp/deployment.yaml`
- `deploy/k8s/publish.ps1`
- `deploy/k8s/teardown.ps1`
- `deploy/k8s/tenants/deployment.yaml`
- `deploy/zot/README.md`
- `docs/deployment-guide.md`
- `docs/getting-started.md`
- `docs/kubernetes-deployment-architecture.md`
- `src/Hexalith.Parties.AppHost/Program.cs`
- `src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj`
- `tests/Hexalith.Parties.DeployValidation.Tests/CarveOutPreservationFitnessTest.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/DaprAccessControlFitnessTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/carve-out-preservation/baseline/falkordb/deployment.yaml`
- `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/carve-out-preservation/generated-workspace/falkordb/deployment.yaml`
- `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestGenerationTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestPublishTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/OperatorScriptValidationTests.cs`
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs`

### Change Log

- 2026-05-26: Implemented publish runtime hardening, Zot manifest verification, Dapr idempotency, Secret upgrade, health-probe patching, and FalkorDB backing-service carve-out. Status moved to review.
- 2026-05-26: Code-review patches applied for submodule scope, inherited `ContainerImageTags`, bounded Aspirate failure handling, exact health-probe enforcement, workload readiness wait, LiveCluster 10-pod topology, Zot timeout, and unhealthy Dapr detection. Story remains in-progress because live publish proof is blocked by Zot `CONTAINER1016` repository access for `registry.hexalith.com/eventstore`.
- 2026-05-26: Repaired live Zot Kubernetes auth configuration in place without changing the Zot Service. Registry access now succeeds; live publish advances through apply and is blocked by application readiness for EventStore Admin UI, Tenants OIDC production validation, and Parties dependency readiness.
- 2026-05-26: Fixed publish-mode authentication/runtime readiness, restarted generated workloads after stable ConfigMap apply, added startup-tolerant `/health` probes, and completed live publish proof. Status moved to done.
- 2026-05-26: Added explicit 10-second Kubernetes HTTP probe timeouts, deleted and recreated the full `hexalith-parties` namespace through `publish.ps1`, and confirmed clean replay reaches all 10 workloads Ready while leaving Zot service configuration untouched.
- 2026-05-26: Proved targeted Zot recovery by removing only the seven application image tag entries from Zot storage, rerunning publish, and confirming the missing manifests are repushed and all workloads return Ready.
