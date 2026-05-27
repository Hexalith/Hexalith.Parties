# Story 9.13: Use Existing Keycloak `tache` Realm for Kubernetes Authentication

Status: done

## Story Boundary

This story moves the Hexalith.Parties Kubernetes deployment away from the Keycloak instance currently deployed inside namespace `hexalith-parties`.

This story may change:

- `src/Hexalith.Parties.AppHost/Program.cs`
- `deploy/k8s/publish.ps1`
- `deploy/k8s/teardown.ps1`
- `deploy/k8s/kustomization.yaml`
- `deploy/k8s/keycloak/`
- generated Kubernetes manifests under `deploy/k8s/*`
- deployment validation scripts/tests
- deployment documentation and sprint planning artifacts

This story must not:

- deploy a new Keycloak instance in `hexalith-parties`
- hard-code Keycloak user passwords, admin passwords, access tokens, or client secrets
- weaken JWT validation by disabling issuer, audience, signature, or lifetime checks
- expose backend APIs publicly as part of the authentication migration
- change browser UI behavior into a full OIDC login unless a separate UI-auth story explicitly scopes it

## Story

As a Kubernetes operator,
I want Hexalith.Parties to use the existing Keycloak deployment in namespace `keycloak` with realm `tache`,
so that authentication is centralized on the platform identity provider and the duplicate Keycloak workload is removed from the Parties namespace.

## Discovery Context

Live cluster inspection on 2026-05-27 showed:

- Namespace `keycloak` contains the existing `deployment/keycloak` and `service/keycloak`.
- `service/keycloak` listens on port `8080`.
- Realm `tache` responds through the cluster-local service.
- OpenID discovery advertises issuer `http://auth.tache.ai:8080/realms/tache`.
- `auth.tache.ai:8080` was not reachable directly from a pod in `hexalith-parties`; the cluster-local service was reachable.
- Current Parties publish mode uses the development symmetric issuer `hexalith-dev` and `hexalith-jwt-signing`.
- Current repo still contains a hand-authored local Keycloak carve-out in `deploy/k8s/keycloak/` and references it from publish, teardown, docs, and topology counts.

Important implementation implication:

The .NET JWT bearer handlers validate token issuer against the token `iss`. Keycloak tokens from realm `tache` will use `http://auth.tache.ai:8080/realms/tache`, so the configured issuer must match that value. However, metadata/JWKS retrieval must also be reachable from pods. The implementation must solve this deliberately, either by making `auth.tache.ai` resolve to the internal Keycloak service from Parties pods or by adding a supported metadata/JWKS override in the auth configuration path.

## Acceptance Criteria

### AC1 - Local Keycloak is removed from Parties Kubernetes topology

Given `deploy/k8s/` is applied,
when the final Kustomization is rendered,
then it does not include `deploy/k8s/keycloak`.

And namespace `hexalith-parties` does not contain `deployment/keycloak`, `service/keycloak`, `configmap/keycloak-realm`, or Secret `hexalith-keycloak-admin` after teardown/publish reconciliation.

And true non-Dapr workload checks no longer include `keycloak` as a Parties-owned workload.

### AC2 - Publish mode uses the external `keycloak/tache` realm

Given AppHost publish mode emits manifests,
when generated service ConfigMaps are inspected,
then JWT-protected services use realm `tache` as their authority/issuer contract rather than `hexalith-dev`.

And the expected token issuer is `http://auth.tache.ai:8080/realms/tache`.

And `RequireHttpsMetadata=false` is retained only because the existing Keycloak service is HTTP on port 8080.

And symmetric signing-key fallback is not used for production-shape publish mode once Keycloak authority is configured.

### AC3 - Metadata and JWKS are reachable from pods

Given a pod in namespace `hexalith-parties`,
when it requests the OpenID configuration and JWKS used by the JWT bearer handler,
then both requests succeed without reaching the removed local Keycloak workload.

And the implementation documents the chosen resolution mechanism:

- host alias / DNS override for `auth.tache.ai` to the `keycloak` service endpoint, or
- a code-supported metadata/JWKS endpoint override that preserves issuer validation, or
- an upstream Keycloak configuration change that emits reachable backchannel URLs.

### AC4 - UI token acquisition is explicitly handled

Given `eventstore-admin-ui` and `sample-blazor-ui` currently acquire backend tokens server-side,
when Keycloak authority is configured,
then the deployment provides their `EventStore:Authentication:Username` and `EventStore:Authentication:Password` through Kubernetes Secret references.

And the Secret name and keys are documented.

And publish fails fast with a bounded, non-secret diagnostic if required UI credential Secret keys are missing.

And no credential value is printed to logs or committed to the repository.

### AC5 - Scripts, validation, and docs agree on the new topology

Given `publish.ps1`, `teardown.ps1`, deployment validation, and documentation are updated,
when an operator reads or runs them,
then the topology is described as using external Keycloak in namespace `keycloak`.

And `hexalith-keycloak-admin` is no longer bootstrapped by Parties publish.

And workload counts and Dapr sidecar counts exclude a Parties-owned Keycloak workload.

### AC6 - Live migration proof is captured

Given the current cluster context is explicitly confirmed,
when the story is implemented,
then validation evidence includes:

- rendered Kustomize output excludes the local Keycloak resources
- static deployment validator passes
- focused deployment tests pass
- live rollout reaches Ready for all Parties-owned workloads
- `kubectl -n hexalith-parties get deploy keycloak` returns not found
- a token minted by realm `tache` can authenticate against at least one protected Parties/EventStore path

## Tasks / Subtasks

- [x] Remove the Parties-owned Keycloak carve-out from the rendered topology.
  - [x] Remove `keycloak` from `deploy/k8s/kustomization.yaml`.
  - [x] Remove `keycloak` from publish preservation and canonical Kustomization restoration in `deploy/k8s/publish.ps1`.
  - [x] Remove `keycloak` from publish readiness waits and any true non-Dapr workload checks owned by Parties.
- [x] Delete or retire `deploy/k8s/keycloak/` manifests so they cannot be applied by `kubectl apply -k deploy/k8s`.
- [x] Remove local Keycloak secret ownership from Parties scripts.
  - [x] Remove `hexalith-keycloak-admin` bootstrap from `deploy/k8s/publish.ps1`.
  - [x] Remove `hexalith-keycloak-admin`, `deployment/keycloak`, `service/keycloak`, and `configmap/keycloak-realm` from `deploy/k8s/teardown.ps1` owned-resource cleanup.
  - [x] Ensure teardown never deletes shared Keycloak resources in namespace `keycloak`.
- [x] Configure production-shape publish-mode JWT auth for the external `tache` realm.
  - [x] Set validated issuer to `http://auth.tache.ai:8080/realms/tache`.
  - [x] Configure EventStore, Admin Server, Parties, Parties MCP, Tenants, and any sample/domain service using authority/JWKS discovery, not `hexalith-dev`.
  - [x] Keep `RequireHttpsMetadata=false` only because the existing realm is served over HTTP port `8080`.
  - [x] Remove production-shape dependence on `hexalith-jwt-signing` and `Authentication__JwtBearer__SigningKey` / `EventStore__Authentication__SigningKey` Secret patches once authority mode is active.
- [x] Solve Keycloak metadata/JWKS reachability from pods while preserving issuer validation.
  - [x] Prove whether discovery from `http://keycloak.keycloak.svc.cluster.local:8080/realms/tache/.well-known/openid-configuration` returns a pod-reachable `jwks_uri`.
  - [x] If discovery or JWKS still points at unreachable `auth.tache.ai`, implement one explicit supported mechanism: DNS/host resolution for `auth.tache.ai`, a code-supported metadata/JWKS override, or an upstream Keycloak backchannel URL change.
  - [x] Document the chosen mechanism in deployment docs and in this story's Dev Agent Record.
- [x] Add UI credential Secret references for `eventstore-admin-ui` and `sample-blazor-ui`.
  - [x] Use an operator-provided Secret for `EventStore__Authentication__Username` and `EventStore__Authentication__Password`.
  - [x] Document the Secret name and key names.
  - [x] Do not create, log, commit, or print credential values.
- [x] Add publish-time validation for the UI credential Secret without printing secret values.
  - [x] Fail fast if the Secret or required keys are missing.
  - [x] Keep diagnostics bounded and name only the missing Secret/key identifiers.
- [x] Update deployment validator/tests for the external-Keycloak topology.
  - [x] Assert rendered output excludes local Keycloak resources and `hexalith-keycloak-admin`.
  - [x] Assert publish-mode manifests use the `tache` issuer and do not use symmetric signing-key fallback.
  - [x] Update tests that currently expect `keycloak` as a preserved carve-out or non-Dapr workload.
- [x] Update `docs/getting-started.md`, `docs/kubernetes-deployment-architecture.md`, `docs/deployment-guide.md`, and `deploy/k8s/README.md`.
  - [x] Describe Keycloak as pre-existing in namespace `keycloak`, realm `tache`.
  - [x] Remove local Keycloak workload, `hexalith-keycloak-admin`, and local bootstrap instructions from the Parties topology.
  - [x] Update workload counts and Dapr sidecar counts to exclude Parties-owned Keycloak.
  - [x] Document UI credential Secret requirements and metadata/JWKS reachability checks.
- [x] Run static validation and focused tests.
- [x] Run live rollout only after the operator approves implementation, then record evidence in this story.

### Review Findings

- [x] [Review][Patch] Reconcile legacy local Keycloak resources during publish/teardown [deploy/k8s/publish.ps1:1088]
- [x] [Review][Patch] Replace greedy env-removal regex and assert signing-key refs are gone [deploy/k8s/publish.ps1:521]
- [x] [Review][Patch] Validate external `tache` realm/client/token claim contract before apply [deploy/k8s/publish.ps1:591]
- [x] [Review][Patch] Move UI credential Secret and Keycloak preflight checks before generate/push/apply [deploy/k8s/publish.ps1:1013]
- [x] [Review][Patch] Preserve unrelated hostAliases and validate Keycloak ClusterIP when patching reachability [deploy/k8s/publish.ps1:601]
- [x] [Review][Patch] Add negative publish tests for missing Keycloak service/ClusterIP and UI credential keys [tests/Hexalith.Parties.DeployValidation.Tests/OperatorScriptValidationTests.cs:809]

## Dev Notes

- Current auth option classes require either `Authority` or `SigningKey`; production-shape Keycloak mode should use `Authority`.
- Existing JWT bearer configuration sets `ValidIssuer` independently from `Authority`; this is good because the internal metadata endpoint and public token issuer may differ, but JWKS retrieval must still be reachable.
- Current Admin UI and sample UI do not provide interactive browser login. When `EventStore:Authentication:Authority` is set, they use Keycloak direct access grant with configured username/password.
- Treat the existing `kc-tache` historical pod arguments as sensitive operational evidence only. Do not copy credentials into docs, story notes, commits, logs, or final answers.
- The local Aspire run-mode Keycloak can remain a dev convenience unless implementation proves it conflicts with the external publish-mode contract.

### Review Findings to Address Before Moving to Review

Party-mode review on 2026-05-27 found the current repository state still behaves like the old local-Keycloak topology. The dev-story implementation must explicitly address these findings rather than only adding new UI/sample topology:

- `deploy/k8s/kustomization.yaml` still included `keycloak`; this must be removed for AC1.
- `deploy/k8s/publish.ps1` still preserved/restored/waited for `keycloak` and bootstrapped `hexalith-keycloak-admin`; all local Keycloak ownership must be removed for AC1 and AC5.
- `deploy/k8s/teardown.ps1` still treated local Keycloak resources and `hexalith-keycloak-admin` as owned resources; teardown must not manage the shared namespace `keycloak` deployment.
- `src/Hexalith.Parties.AppHost/Program.cs` still used publish issuer `hexalith-dev`, empty authority, and symmetric signing-key fallback; publish-mode auth must be changed to the `tache` issuer contract for AC2.
- No implementation reference to `auth.tache.ai` or realm `tache` existed outside this story; generated manifests and tests must make that contract visible.
- Documentation still described the local Keycloak workload and `hexalith-keycloak-admin`; docs must become the operator source of truth for external Keycloak.
- Story 9.14 changes such as `sample`, `sample-blazor-ui`, and `ingress.yaml` may exist in the worktree, but this story must not claim UI ingress delivery as authentication migration evidence.

### Preferred Implementation Shape

Use the least surprising production-shape contract:

- Token issuer / validated issuer: `http://auth.tache.ai:8080/realms/tache`.
- Metadata/JWKS retrieval: a pod-reachable path that does not depend on the removed local Keycloak workload.
- Signing: OIDC/JWKS authority mode only for publish-mode protected services. Do not patch `hexalith-jwt-signing` into production-shape publish manifests for this story.
- UI credential Secret: operator-managed and pre-created. Recommended name: `hexalith-tache-ui-credentials`. Recommended keys: `username` and `password`.

Do not assume setting `Authority` to the cluster-local service is sufficient until the discovered `jwks_uri` is checked from a pod. If Keycloak discovery advertises an external `jwks_uri` that pods cannot reach, the implementation must add the selected DNS/backchannel/code-supported override and document it.

## Validation Plan

```bash
kubectl -n keycloak get svc keycloak
kubectl -n hexalith-parties run keycloak-reachability --rm -i --restart=Never --image=curlimages/curl -- curl -fsS http://keycloak.keycloak.svc.cluster.local:8080/realms/tache/.well-known/openid-configuration
kubectl -n hexalith-parties run keycloak-jwks-reachability --rm -i --restart=Never --image=curlimages/curl -- curl -fsS <jwks-uri-used-by-jwt-bearer>
kubectl kustomize deploy/k8s | rg "name: keycloak|keycloak-realm|hexalith-keycloak-admin" && exit 1 || true
kubectl kustomize deploy/k8s | rg "http://auth.tache.ai:8080/realms/tache"
kubectl kustomize deploy/k8s | rg "hexalith-dev|hexalith-jwt-signing" && exit 1 || true
pwsh -NoProfile -File deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/
/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --filter "Category!=LiveCluster"
```

Live validation requires explicit operator approval before applying changes.

## Dev Agent Record

### Implementation Plan

- Removed the local Keycloak carve-out from the top-level Kustomize topology, publish preservation/restore logic, readiness waits, and teardown owned-resource cleanup.
- Switched publish-mode authentication to OIDC authority mode with issuer `http://auth.tache.ai:8080/realms/tache`; generated manifests no longer reference `hexalith-dev`, `hexalith-jwt-signing`, or signing-key Secret patches.
- Chose DNS/host resolution as the metadata/JWKS reachability mechanism: `publish.ps1` resolves the `keycloak/keycloak` service ClusterIP and patches `hostAliases` for `auth.tache.ai` into generated workloads.
- Added operator-managed UI credential Secret references for `eventstore-admin-ui` and `sample-blazor-ui` using Secret `hexalith-tache-ui-credentials`, keys `username` and `password`.
- Updated deployment guardrail tests and docs to make the external Keycloak topology the source of truth.

### Debug Log

- `kubectl -n keycloak get service keycloak -o jsonpath='{.spec.clusterIP}'` returned `10.233.41.235`.
- `kubectl -n hexalith-parties run keycloak-reachability --rm -i --restart=Never --image=curlimages/curl -- curl -fsS http://keycloak.keycloak.svc.cluster.local:8080/realms/tache/.well-known/openid-configuration` succeeded; discovery advertises issuer `http://auth.tache.ai:8080/realms/tache` and JWKS URI `http://auth.tache.ai:8080/realms/tache/protocol/openid-connect/certs`.
- `kubectl -n hexalith-parties run keycloak-jwks-reachability --rm -i --restart=Never --image=curlimages/curl --overrides='{"spec":{"hostAliases":[{"ip":"10.233.41.235","hostnames":["auth.tache.ai"]}]}}' -- curl -fsS http://auth.tache.ai:8080/realms/tache/protocol/openid-connect/certs` succeeded, validating the host alias mechanism.
- `kubectl kustomize deploy/k8s | rg "name: keycloak|keycloak-realm|hexalith-keycloak-admin|hexalith-jwt-signing|hexalith-dev|Authentication__JwtBearer__SigningKey|EventStore__Authentication__SigningKey"` returned no matches.
- Broader solution regression was attempted with `/home/quentindv/.dotnet/dotnet test Hexalith.Parties.slnx -c Release --filter "Category!=LiveCluster"` and failed on pre-existing/unrelated issues: sample doc path separators (`docs\...` on Linux), package tests spawning `dotnet` from PATH, EventStore submodule type/load drift, client/package guardrail drift, and search performance thresholds. AppHost compile issue found during that run was fixed and separately verified.
- 2026-05-27 live rollout attempt was halted before publish because `kubectl -n hexalith-parties get secret hexalith-tache-ui-credentials` returned NotFound. Existing Secrets in `hexalith-parties` are `hexalith-jwt-signing`, `hexalith-keycloak-admin`, `hexalith-pages-tls`, and `zot-pull-secret`; none satisfy the required `hexalith-tache-ui-credentials` contract.
- 2026-05-27 after operator direction to generate random values for this test, created `secret/hexalith-tache-ui-credentials` in namespace `hexalith-parties` with random `username` and `password` values. Values were not printed or recorded.
- `PATH="/home/quentindv/.dotnet:$PATH" pwsh -NoProfile -File deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local` succeeded with tag `0.0.0-preview.0.505` and reported all Parties-owned workloads Ready: `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `sample`, `sample-blazor-ui`, `parties`, `parties-mcp`, `tenants`, `memories`, `redis`, and `falkordb`.
- `kubectl -n hexalith-parties get deploy,pods` showed all Parties-owned deployments available and pods running after publish.
- Removed legacy local Keycloak leftovers from namespace `hexalith-parties` after publish reconciliation: `deployment/keycloak`, `service/keycloak`, `configmap/keycloak-realm`, and `secret/hexalith-keycloak-admin`. Follow-up `kubectl -n hexalith-parties get deploy/keycloak service/keycloak configmap/keycloak-realm secret/hexalith-keycloak-admin --ignore-not-found` returned no output.
- `kubectl kustomize deploy/k8s | rg "name: keycloak|keycloak-realm|hexalith-keycloak-admin|hexalith-jwt-signing|hexalith-dev|Authentication__JwtBearer__SigningKey|EventStore__Authentication__SigningKey"` returned no matches after live publish.
- Token proof remains blocked: a token minted from realm `tache` with client `admin-cli` reached EventStore but returned HTTP `401`, likely because the token audience does not satisfy the `hexalith-eventstore` protected API contract. The random UI credential Secret values created for this test are not valid Keycloak users and cannot prove end-to-end token authentication.
- 2026-05-27 operator authorized creating a temporary test user if possible. Non-mutating admin access check with `/opt/keycloak/bin/kcadm.sh config credentials --server http://localhost:8080 --realm master` failed with `invalid_grant` using the admin username/password exposed by `secret/keycloak-secret` and by the running Keycloak pod environment. No realm user, client, flow, or database changes were made.
- 2026-05-27 operator authorized creating only the Keycloak objects necessary for Parties validation. Used Keycloak-supported `kc.sh bootstrap-admin user` to create temporary admin access, then created client `hexalith-eventstore` in realm `tache`, a dedicated test user, and client protocol mappers for audience and EventStore tenant/domain/permission claims. No realm flow, OTP/2FA setting, existing client, or database record was directly modified.
- Updated `secret/hexalith-tache-ui-credentials` in namespace `hexalith-parties` to reference the dedicated Keycloak test user. Secret values were not printed or recorded in the story.
- `kubectl -n hexalith-parties run keycloak-eventstore-token-proof --rm -i --restart=Never --image=curlimages/curl ...` obtained a token from `http://auth.tache.ai:8080/realms/tache/protocol/openid-connect/token` with HTTP `200`.
- The same proof pod called `http://eventstore.hexalith-parties.svc.cluster.local:8080/api/v1/commands/status/live-proof` with the Keycloak bearer token and received HTTP `404`, proving authentication and tenant authorization passed; the test correlation id had no stored command status. Previous invalid-token attempts returned `401` and missing-tenant-claim attempts returned `403`.
- Restarted only `deployment/eventstore-admin-ui` and `deployment/sample-blazor-ui` so their environment variables picked up the updated Secret; both rollouts completed successfully.
- Removed temporary Keycloak bootstrap admins after provisioning. The cleanup admin was deleted last so the temporary admin session did not remain active.

### Completion Notes

- Static/non-live implementation is complete.
- Operator-approved live publish completed successfully against context `kubernetes-admin@cluster.local`, and Parties-owned workloads reached Ready.
- Legacy local Keycloak resources were removed from namespace `hexalith-parties` after apply and verified absent.
- Realm `tache` now contains only the Parties validation objects needed by this story: client `hexalith-eventstore`, a dedicated test user, and protocol mappers that emit the expected EventStore audience and authorization claims.
- Token-authenticated protected-path proof is complete: Keycloak token acquisition returned HTTP `200`; EventStore returned HTTP `404` for the synthetic command status id instead of `401` or `403`.
- Temporary bootstrap admin users were removed after provisioning; no credential values were printed or committed.
- `publish.ps1` validates UI credential Secret key presence by Secret/key name only and does not print credential values.
- Code review patches applied: publish now preflights the external Keycloak service, UI credential Secret, OIDC discovery/JWKS, and token claims before image generation; legacy local Keycloak resources are reconciled during publish and teardown; signing-key env removal is line-based with postconditions; hostAliases preserve unrelated entries and validate ClusterIP; negative operator-script tests cover missing Secret keys, missing/invalid Keycloak ClusterIP, and invalid token claims.

### File List

- `_bmad-output/implementation-artifacts/9-13-use-existing-keycloak-tache-realm.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.AppHost/Program.cs`
- `deploy/k8s/kustomization.yaml`
- `deploy/k8s/publish.ps1`
- `deploy/k8s/teardown.ps1`
- `deploy/k8s/README.md`
- `deploy/k8s/eventstore/deployment.yaml`
- `deploy/k8s/eventstore/kustomization.yaml`
- `deploy/k8s/eventstore-admin/deployment.yaml`
- `deploy/k8s/eventstore-admin/kustomization.yaml`
- `deploy/k8s/eventstore-admin-ui/deployment.yaml`
- `deploy/k8s/eventstore-admin-ui/kustomization.yaml`
- `deploy/k8s/sample/deployment.yaml`
- `deploy/k8s/sample-blazor-ui/deployment.yaml`
- `deploy/k8s/sample-blazor-ui/kustomization.yaml`
- `deploy/k8s/parties/deployment.yaml`
- `deploy/k8s/parties/kustomization.yaml`
- `deploy/k8s/parties-mcp/deployment.yaml`
- `deploy/k8s/parties-mcp/kustomization.yaml`
- `deploy/k8s/tenants/deployment.yaml`
- `deploy/k8s/tenants/kustomization.yaml`
- `deploy/k8s/memories/deployment.yaml`
- `deploy/k8s/keycloak/configmap.yaml` (deleted)
- `deploy/k8s/keycloak/deployment.yaml` (deleted)
- `deploy/k8s/keycloak/kustomization.yaml` (deleted)
- `deploy/k8s/keycloak/service.yaml` (deleted)
- `docs/getting-started.md`
- `docs/kubernetes-deployment-architecture.md`
- `docs/deployment-guide.md`
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/CarveOutPreservationFitnessTest.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/DaprAccessControlFitnessTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/DaprManifestValidationTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestGenerationTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestPublishTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/OperatorScriptValidationTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/ValidateDeploymentLintToolingTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/publish-workspace/deploy/k8s/kustomization.yaml`
- `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/publish-workspace/deploy/k8s/keycloak/deployment.yaml` (deleted)
- `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/carve-out-preservation/baseline/keycloak/deployment.yaml` (deleted)
- `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/carve-out-preservation/generated-workspace/keycloak/deployment.yaml` (deleted)

## Change Log

- 2026-05-27: Implemented external Keycloak `tache` realm migration through non-live validation; story remains in progress pending operator-approved live rollout.
- 2026-05-27: Ran operator-approved live publish with random test UI Secret values; workloads reached Ready and local Keycloak leftovers were removed, but story remains in progress pending valid `tache` token authentication proof.
- 2026-05-27: Created minimal `tache` realm client/user/mappers for Parties validation, proved Keycloak token authentication against EventStore, and moved story to review.
