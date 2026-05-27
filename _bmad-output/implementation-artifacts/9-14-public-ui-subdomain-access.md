# Story 9.14: Public UI Subdomain Access for Admin and Sample Pages

Status: ready-for-dev

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

### AC2 - UI host routing is committed and reproducible

Given `deploy/k8s/` is rendered,
when Kustomize output is inspected,
then it includes an Ingress or documented equivalent routing:

- host `eventstore.hexalith.com` to `service/eventstore-admin-ui` port `8080`
- host `sample.hexalith.com` to `service/sample-blazor-ui` port `8080`

And TLS requirements are documented, including the Secret name expected by the committed manifest if TLS is configured in-cluster.

And if host-level nginx is used because no ingress controller exists, the operational bridge is documented with the exact upstream Kubernetes services and is not treated as the long-term source of truth.

### AC3 - Dapr access control allows only required UI paths

Given deny-by-default Dapr access control is enabled,
when `sample-blazor-ui` calls EventStore,
then EventStore allows only the required command/query and SignalR paths for the sample UI.

And when EventStore invokes the sample domain service,
then `accesscontrol-sample` allows only the required EventStore caller and method path.

And no wildcard caller app id or unrestricted `/**` operation is added to production Dapr configuration.

### AC4 - Publish, teardown, and validation know the new resources

Given an operator runs publish or teardown,
when the scripts execute,
then they include `sample`, `sample-blazor-ui`, and the UI ingress in generated/preserved/cleanup resource sets.

And static validation understands full Dapr workloads versus client-only Dapr workloads for both UIs.

And workload and sidecar counts in docs match the rendered topology.

### AC5 - Public URLs are verified

Given the workloads are rolled out and DNS/routing is configured,
when the operator requests the two URLs,
then:

- `https://eventstore.hexalith.com/` returns the EventStore Admin UI page.
- `https://sample.hexalith.com/` returns the sample Blazor UI page.

And backend unavailability/authentication errors are treated as separate runtime issues, not as routing success.

### AC6 - Authentication dependency is explicit

Given the UI pages route successfully,
when they call protected backend APIs,
then any authentication failure must be documented as dependent on Story 9.13 or existing JWT development-token configuration.

And this story does not claim end-to-end authenticated UI operation unless token acquisition and backend authorization are also proven.

## Tasks / Subtasks

- [ ] Compose `sample` and `sample-blazor-ui` in AppHost publish mode.
- [ ] Add `accesscontrol-sample` and EventStore ACL entries for sample UI access.
- [ ] Add or update `deploy/k8s/ingress.yaml` for the two hostnames.
- [ ] Update `publish.ps1` generated service folders, Dapr patch maps, client-only Dapr targets, canonical Kustomization restoration, and Dapr CR apply list.
- [ ] Update `teardown.ps1` resource cleanup for the new workloads and ingress.
- [ ] Regenerate or update committed manifests consistently.
- [ ] Update deployment validator/tests for the new topology.
- [ ] Update operator docs and topology counts.
- [ ] Verify Kustomize rendering and static validation.
- [ ] Verify live URL routing only after the operator approves deployment.

## Dev Notes

- Keep UI ingress restricted to browser UI workloads only.
- `sample-blazor-ui` should not receive `dapr.io/app-port` or `dapr.io/config`; it is client-only.
- `sample` should receive full Dapr receiver annotations and its own access-control config.
- Admin UI may still display "Admin API Unavailable" if Dapr ACL or authentication is misconfigured; routing success and authenticated backend success must be validated separately.
- Earlier live diagnosis showed Admin UI calls through local Dapr and may receive 403 before the backend app sees the request. If that recurs, inspect `accesscontrol-eventstore-admin` path matching before assuming a browser-login problem.

## Validation Plan

```bash
kubectl kustomize deploy/k8s
pwsh -NoProfile -File deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/
/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --filter "Category!=LiveCluster"
curl -fsS https://eventstore.hexalith.com/ | head
curl -fsS https://sample.hexalith.com/ | head
```

Live validation requires explicit operator approval before applying changes.
