# Story 9.14: Public UI Subdomain Access for Admin and Sample Pages

Status: done

## Story Boundary

This story exposes the two browser UI workloads through dedicated hostnames:

- `eventstore.hexalith.com` -> EventStore Admin UI
- `sample.hexalith.com` -> EventStore sample Blazor UI

This story may change:

- `src/Hexalith.Parties.AppHost/Program.cs`
- `src/Hexalith.Parties.AppHost/DaprComponents/*`
- `deploy/dapr/*`
- `deploy/k8s/ingress.yaml`
- `deploy/k8s/publish.ps1`
- `deploy/k8s/teardown.ps1`
- generated Kubernetes manifests for `sample`, `sample-blazor-ui`, and UI workloads
- deployment validation scripts/tests
- deployment documentation

This story must not:

- expose EventStore, Parties, Tenants, or Admin API backend services directly to the public internet
- weaken Dapr access control with wildcard callers or unrestricted operations
- replace authentication behavior; external Keycloak migration is Story 9.13
- rely on manual host nginx configuration as the only durable deployment model unless documented as a temporary operational bridge

## Story

As a Kubernetes operator,
I want `eventstore.hexalith.com` and `sample.hexalith.com` to route to the deployed UI pages,
so that users can access the Admin UI and sample UI through stable browser URLs.

## Audience and Product Posture

- `eventstore.hexalith.com` is an admin-facing browser surface. The hostname may be public-routeable, but backend/admin capability must remain protected by the existing UI, Admin Server, JWT, and RBAC behavior.
- `sample.hexalith.com` is a sample/demo and operator smoke-test browser surface for the EventStore sample UI. It is a durable deployment surface for this cluster unless a later story removes it.
- This story proves safe routing to UI workloads. It does not prove authenticated end-to-end product workflows unless Story 9.13 or the existing development-token path is also verified.

## Discovery Context

The deployed cluster currently needs browser access to two UI surfaces. Prior analysis found:

- `eventstore-admin-ui` exists in the Parties topology.
- The EventStore sample UI lives in the EventStore submodule as `samples/Hexalith.EventStore.Sample.BlazorUI`.
- The sample backend lives in `samples/Hexalith.EventStore.Sample`.
- The sample UI needs EventStore command/query access and SignalR hub access.
- Browser UI hostnames requested by the operator are:
  - `eventstore.hexalith.com`
  - `sample.hexalith.com`
- No in-cluster ingress controller was observed during the live deployment session; temporary host-level nginx routing may be needed until the cluster ingress path is installed.

## Acceptance Criteria

### AC1 - Sample UI workloads are part of the publish topology

Given AppHost publish mode runs,
when Aspirate generates Kubernetes manifests,
then `sample` and `sample-blazor-ui` service folders exist under `deploy/k8s/`.

And `sample` runs as a full Dapr service with app id `sample`.

And `sample-blazor-ui` runs as a Dapr client-only workload with app id `sample-blazor-ui`.

And `eventstore` has SignalR enabled for the sample UI.

And static validation fails if `sample-blazor-ui` receives `dapr.io/app-port` or `dapr.io/config`.

And static validation fails if `sample` is missing `dapr.io/app-port`, `dapr.io/config: accesscontrol-sample`, or app id `sample`.

### AC2 - UI host routing is committed and reproducible

Given `deploy/k8s/` is rendered,
when Kustomize output is inspected,
then committed `deploy/k8s/ingress.yaml` is the durable source of truth and includes:

- host `eventstore.hexalith.com` to `service/eventstore-admin-ui` port `8080`
- host `sample.hexalith.com` to `service/sample-blazor-ui` port `8080`

And the Ingress uses class `nginx`, TLS secret `hexalith-pages-tls`, and the namespace implied by `deploy/k8s/namespace.yaml`.

And static validation fails if any public Ingress route targets `eventstore`, `eventstore-admin`, `parties`, `tenants`, `sample`, Dapr sidecars, or other backend/internal services.

And if host-level nginx is used because no ingress controller exists, the operator documentation marks it as a temporary bridge with exact upstream Kubernetes services, owner, exit condition, and the committed Ingress remains the long-term source of truth.

### AC3 - Dapr access control allows only required UI paths

Given deny-by-default Dapr access control is enabled,
when Dapr access-control configuration is inspected,
then the allowed operation matrix is:

| Caller app id | Receiver app id | Configuration file | Allowed operations |
| --- | --- | --- | --- |
| `sample-blazor-ui` | `eventstore` | `deploy/dapr/accesscontrol.yaml` and `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.yaml` | `POST /api/v1/commands`, `POST /api/v1/queries` |
| `eventstore` | `sample` | `deploy/dapr/accesscontrol-sample.yaml` and `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.sample.yaml` | `POST /process`, `POST /project`, `POST /replay-state`, `POST /admin/operational-index-metadata` |

And SignalR hub access is explicitly validated separately: if the sample UI continues using the Kubernetes service URL `http://eventstore:8080/hubs/projection-changes`, document that this path is internal service traffic outside Dapr ACL enforcement; if it is changed to Dapr service invocation, add only the exact hub path required for `/hubs/projection-changes`.

And no wildcard caller app id, unrestricted `/**`, loose `/*`, or production-only dev-token bypass is added to Dapr configuration.

### AC4 - Publish, teardown, and validation know the new resources

Given an operator runs publish or teardown,
when the scripts execute,
then they include `sample`, `sample-blazor-ui`, and the UI ingress in generated/preserved/cleanup resource sets.

And static validation understands full Dapr workloads versus client-only Dapr workloads for both UIs.

And workload and sidecar counts in docs match the rendered topology.

And `deploy/k8s/publish.ps1` preserves the UI Ingress, patches Dapr config only for documented full Dapr workloads, keeps `eventstore-admin-ui` and `sample-blazor-ui` in the client-only target set, and applies `accesscontrol-sample.yaml`.

And `deploy/k8s/teardown.ps1` removes `sample`, `sample-blazor-ui`, `hexalith-pages-ingress`, and `accesscontrol-sample` without leaving public UI routing behind.

### AC5 - Public URLs are verified

Given the workloads are rolled out and DNS/routing is configured,
when the operator requests the two URLs,
then:

- `https://eventstore.hexalith.com/` completes TLS with the configured certificate, returns HTTP `200` or an expected auth redirect, and serves a page marker unique to the EventStore Admin UI.
- `https://sample.hexalith.com/` completes TLS with the configured certificate, returns HTTP `200` or an expected auth redirect, and serves a page marker unique to the sample Blazor UI.

And the same evidence confirms neither hostname routes to a backend API, Dapr sidecar, EventStore service root, Parties, Tenants, or Admin Server service.

And backend unavailability/authentication errors are treated as separate runtime issues, not as routing success, unless the page shell itself fails to load.

### AC6 - Authentication dependency is explicit

Given the UI pages route successfully,
when they call protected backend APIs,
then any authentication failure must be documented as dependent on Story 9.13 or existing JWT development-token configuration.

And this story does not claim end-to-end authenticated UI operation unless token acquisition and backend authorization are also proven.

And authenticated browser-flow tests remain skipped or documented as blocked unless Story 9.13 or explicit JWT development-token evidence is present.

## Tasks / Subtasks

- [x] Update `src/Hexalith.Parties.AppHost/Program.cs` so publish mode composes `sample` and `sample-blazor-ui`, enables EventStore SignalR, and keeps `sample-blazor-ui` as Dapr client-only.
- [x] Update `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.yaml`, `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.sample.yaml`, `deploy/dapr/accesscontrol.yaml`, and `deploy/dapr/accesscontrol-sample.yaml` to match the AC3 matrix.
- [x] Add or update `deploy/k8s/ingress.yaml` with `hexalith-pages-ingress`, class `nginx`, TLS secret `hexalith-pages-tls`, and only the two UI service backends.
- [x] Update `deploy/k8s/publish.ps1` generated service folders, Dapr patch maps, client-only Dapr targets, canonical Kustomization restoration, Ingress preservation, and Dapr CR apply list.
- [x] Update `deploy/k8s/teardown.ps1` cleanup for `sample`, `sample-blazor-ui`, `hexalith-pages-ingress`, generated config maps, services, deployments, and `accesscontrol-sample`.
- [x] Regenerate or update committed manifests under `deploy/k8s/sample/`, `deploy/k8s/sample-blazor-ui/`, `deploy/k8s/eventstore/`, and `deploy/k8s/kustomization.yaml` consistently.
- [x] Update `deploy/validate-deployment.ps1` to reject backend public ingress, wildcard ACLs, missing client-only/full-Dapr annotations, and missing UI ingress TLS configuration.
- [x] Update `tests/Hexalith.Parties.DeployValidation.Tests/DaprAccessControlFitnessTests.cs`, `K8sManifestGenerationTests.cs`, `K8sManifestPublishTests.cs`, `OperatorScriptValidationTests.cs`, and related fixtures for the new topology.
- [x] Update `docs/deployment-guide.md`, `docs/kubernetes-deployment-architecture.md`, and `deploy/k8s/README.md` with DNS/TLS prerequisites, topology counts, host-level nginx bridge instructions if needed, rollback notes, and authentication dependency.
- [x] Verify Kustomize rendering and static validation.
- [x] Verify live URL routing only after the operator approves deployment; capture host, TLS result, HTTP status or redirect, page marker, and backend non-exposure evidence.

### Review Findings

- [x] [Review][Patch] Delete the UI ingress from the intended namespace; raw `kubectl delete -f ingress.yaml` does not receive the Kustomize namespace and can leave `hexalith-pages-ingress` behind when the current kubectl namespace is not `hexalith-parties`. [deploy/k8s/teardown.ps1:204]
- [x] [Review][Patch] Make ingress validation structural and exhaustive; the current document-wide regex can accept cross-host route mismatches, does not enforce `/` plus `Prefix`, and does not validate every allowed UI-service route's host and port. [deploy/validate-deployment.ps1:754]
- [x] [Review][Patch] Reject unexpected extra public ingress hosts or UI-service routes instead of only rejecting backend service names; extra hostnames targeting `eventstore-admin-ui` or `sample-blazor-ui` currently pass. [deploy/validate-deployment.ps1:761]
- [x] [Review][Patch] Extend Dapr ACL matrix assertions to require `action: allow`, not only verb and route, so tests fail if a documented operation is present but denied. [tests/Hexalith.Parties.DeployValidation.Tests/DaprAccessControlFitnessTests.cs:133]
- [x] [Review][Patch] Update the canonical topology diagram; it still says five core pods and omits `sample` and `sample-blazor-ui` even though the workload table lists them. [docs/kubernetes-deployment-architecture.md:40]
- [x] [Review][Defer] AppHost EventStore access-control still has pre-existing bare `/**` routes for `eventstore-admin`, `tenants`, and `parties`. [src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.yaml:17] — deferred, pre-existing

## Dev Notes

- Keep UI ingress restricted to browser UI workloads only.
- `sample-blazor-ui` should not receive `dapr.io/app-port` or `dapr.io/config`; it is client-only.
- `sample` should receive full Dapr receiver annotations and its own access-control config.
- Do not expose `sample` directly through Ingress; only `sample-blazor-ui` is public-routeable.
- `sample-blazor-ui` currently uses `EventStore__SignalR__HubUrl=http://eventstore:8080/hubs/projection-changes`; if this remains true, prove the path is in-cluster-only and not public ingress.
- DNS and TLS are deployment prerequisites: `eventstore.hexalith.com` and `sample.hexalith.com` must resolve to the ingress endpoint, and `hexalith-pages-tls` must exist before live HTTPS proof.
- Admin UI may still display "Admin API Unavailable" if Dapr ACL or authentication is misconfigured; routing success and authenticated backend success must be validated separately.
- Earlier live diagnosis showed Admin UI calls through local Dapr and may receive 403 before the backend app sees the request. If that recurs, inspect `accesscontrol-eventstore-admin` path matching before assuming a browser-login problem.

## Negative Validation Requirements

- Fail if any production Dapr access-control file contains caller `*`, route `*`, route `/**`, or route ending in loose `/*`.
- Fail if `sample-blazor-ui` has `dapr.io/app-port` or `dapr.io/config`.
- Fail if `sample` lacks `dapr.io/app-port`, `dapr.io/config: accesscontrol-sample`, or `dapr.io/app-id: sample`.
- Fail if public Ingress targets any backend/internal service instead of `eventstore-admin-ui:8080` and `sample-blazor-ui:8080`.
- Fail if publish omits `sample`, `sample-blazor-ui`, the UI Ingress, or `accesscontrol-sample`.
- Fail if teardown leaves `hexalith-pages-ingress`, `sample`, `sample-blazor-ui`, or `accesscontrol-sample` behind.
- Fail if documented workload/sidecar counts diverge from rendered manifests.

## Validation Plan

```bash
kubectl kustomize deploy/k8s
pwsh -NoProfile -File deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/
/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --filter "Category!=LiveCluster"
curl -fsSI https://eventstore.hexalith.com/
curl -fsSI https://sample.hexalith.com/
curl -fsS https://eventstore.hexalith.com/ | head
curl -fsS https://sample.hexalith.com/ | head
```

Live validation requires explicit operator approval before applying changes. Record the resolved ingress endpoint, certificate subject/SAN, HTTP status or redirect target, and page marker used to prove each UI workload.

## Dev Agent Record

### Debug Log

- 2026-05-28: Red phase added focused deploy validation guardrails. Initial run failed as expected for missing explicit ingress deletion, missing `K8sIngress-InvalidPublicRoute`, and AppHost-local broad sample ACL routes.
- 2026-05-28: Fixed test helper tuple handling and PowerShell `$Host` variable collision in the validator ingress scan.
- 2026-05-28: Validation passed: `kubectl kustomize deploy/k8s`; `pwsh -NoProfile -File deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/`; AppHost topology tests 16/16; DeployValidation `Category!=LiveCluster` 70/70.
- 2026-05-28: Live URL validation not run because deployment/live routing requires explicit operator approval.
- 2026-05-28: Operator approved live validation. Existing cluster resources were already deployed and healthy, so no publish run was required: all 11 `hexalith-parties` Deployments were Available, `hexalith-pages-ingress` existed with class `nginx`, and `hexalith-pages-tls` existed.
- 2026-05-28: Live evidence captured at 08:37 UTC: DNS `eventstore.hexalith.com` and `sample.hexalith.com` both resolved to `82.67.127.189`; TLS certificate subject `CN=*.hexalith.com`, issuer `Sectigo Public Server Authentication CA DV R36`, SAN `DNS:*.hexalith.com, DNS:hexalith.com`, valid 2025-09-30 through 2026-09-30; GET `/` returned HTTP 200 for both hosts.
- 2026-05-28: Page markers captured: `eventstore.hexalith.com` served `Hexalith EventStore Admin` and Admin UI navigation markers; `sample.hexalith.com` served `Hexalith EventStore — UI Refresh Patterns`, `sample-blazor-ui`, and EventStore SignalR sample markers.
- 2026-05-28: Backend non-exposure probes on both hosts returned HTTP 404 for `/api/v1/commands`, `/api/v1/queries`, `/swagger/index.html`, and `/dapr/config`; Ingress backend inspection showed only `eventstore-admin-ui:8080` and `sample-blazor-ui:8080`.
- 2026-05-28: Operator requested full republish to ensure the new committed resources, not older live resources, were validated. Initial publish attempts exposed two pre-existing `publish.ps1` preflight issues: `curlimages/curl` entrypoint was not overridden by the preflight pod spec, and `kubectl run --rm` appended pod deletion text to the token JSON line.
- 2026-05-28: Fixed `publish.ps1` Keycloak preflight to run `/bin/sh -c` through the pod override, extract the `access_token` JSON from mixed kubectl output, and validate the live `hexalith-tache-ui-credentials` claims used by the sample UI: tenant `tenant-a`, domain `counter`, permission `commands:*`.
- 2026-05-28: Full publish succeeded with `PATH=/home/quentindv/.dotnet:$PATH pwsh -NoProfile -File deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local`; tag `0.0.0-preview.0.506` was generated, pushed, applied, restarted, and waited Ready in `00:03:25.6601111`.
- 2026-05-28: Post-publish evidence: `kubectl diff -k deploy/k8s` and `kubectl diff -f deploy/dapr` produced no drift; all generated live images matched manifests at `registry.hexalith.com/<app>:0.0.0-preview.0.506`; both public hosts still returned HTTP 200 with expected page markers; backend probes still returned 404.
- 2026-05-28: Post-publish validation passed: `deploy/validate-deployment.ps1` PASS, focused deploy validation 53/53, full DeployValidation `Category!=LiveCluster` 70/70, and `git diff --check` PASS.
- 2026-05-28: Code-review patches applied: teardown deletes the UI Ingress in `hexalith-parties`; ingress lint now validates exact host/path/pathType/service/port route tuples and rejects extra public UI routes; Dapr ACL tests assert `action: allow`; topology diagram updated for the sample workloads. Verification: `kubectl kustomize deploy/k8s` PASS, validator PASS, DeployValidation `Category!=LiveCluster` 75/75, `git diff --check` PASS.

### Completion Notes

- Confirmed the existing AppHost composition includes publish-mode `sample` and `sample-blazor-ui`, enables EventStore SignalR, and keeps `sample-blazor-ui` Dapr client-only.
- Tightened AppHost-local Dapr ACLs to match the production AC3 matrix for `sample-blazor-ui -> eventstore` and `eventstore -> sample`.
- Added static ingress validation for `hexalith-pages-ingress`, `nginx`, `hexalith-pages-tls`, required UI host routes, and forbidden backend public routes.
- Updated teardown to delete `deploy/k8s/ingress.yaml` explicitly before residual checks.
- Updated deployment docs with DNS/TLS prerequisites, host-level nginx bridge ownership/exit criteria, rollback cleanup, SignalR internal path behavior, and Story 9.13 authentication dependency.
- Live URL routing evidence is complete; authenticated backend operation remains a separate Story 9.13/runtime auth concern.
- Republished the topology from the current manifests and verified the live cluster is now running regenerated `0.0.0-preview.0.506` workloads with no K8s/Dapr drift.
- Hardened the publish-time Keycloak preflight so future full publishes can validate the existing `tache` realm and sample UI credential contract non-interactively.
- Applied code-review hardening for namespace-safe ingress teardown, structural public ingress linting, ACL action assertions, and sample topology documentation.

### File List

- `_bmad-output/implementation-artifacts/9-14-public-ui-subdomain-access.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `deploy/k8s/README.md`
- `deploy/k8s/eventstore-admin-ui/deployment.yaml`
- `deploy/k8s/eventstore-admin/deployment.yaml`
- `deploy/k8s/eventstore/deployment.yaml`
- `deploy/k8s/memories/deployment.yaml`
- `deploy/k8s/parties-mcp/deployment.yaml`
- `deploy/k8s/parties/deployment.yaml`
- `deploy/k8s/publish.ps1`
- `deploy/k8s/sample-blazor-ui/deployment.yaml`
- `deploy/k8s/sample/deployment.yaml`
- `deploy/k8s/teardown.ps1`
- `deploy/k8s/tenants/deployment.yaml`
- `deploy/validate-deployment.ps1`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `docs/deployment-guide.md`
- `docs/kubernetes-deployment-architecture.md`
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.sample.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.yaml`
- `tests/Hexalith.Parties.DeployValidation.Tests/DaprAccessControlFitnessTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/K8sManifestGenerationTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/OperatorScriptValidationTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/ValidateDeploymentLintFitnessTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/ValidateDeploymentLintToolingTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/lint-near-miss/deploy/k8s/ingress.yaml`
- `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/lint-negative/K8sIngress-InvalidPublicRoute/ingress.yaml`
- `tests/Hexalith.Parties.DeployValidation.Tests/Fixtures/valid-deploy-tree/deploy/k8s/ingress.yaml`

## Change Log

- 2026-05-28: Implemented static public UI ingress, Dapr ACL, teardown, validation, and documentation hardening for Story 9.14; blocked pending operator-approved live URL validation.
- 2026-05-28: Completed operator-approved live URL validation for `eventstore.hexalith.com` and `sample.hexalith.com`; story moved to review.
- 2026-05-28: Republished current manifests to the cluster with tag `0.0.0-preview.0.506`; fixed publish Keycloak preflight issues discovered during the full rerun; confirmed no live drift after apply.
- 2026-05-28: Applied code-review patches and moved story to done.
