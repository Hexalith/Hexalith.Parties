# Sprint Change Proposal - Kubernetes UI Ingress

**Date:** 2026-05-27
**Project:** Hexalith.Parties
**Requested by:** Jérôme
**Mode:** Batch correction with direct implementation

## 1. Issue Summary

The Kubernetes deployment needed browser access for two UI surfaces:

- `eventstore.hexalith.com` must route to the EventStore Admin UI.
- `sample.hexalith.com` must route to the EventStore sample Blazor UI.

The current topology already had `eventstore-admin-ui`, but it had no public Ingress. The sample UI existed in the EventStore submodule as `Hexalith.EventStore.Sample.BlazorUI`, but it was not part of the Parties Kubernetes topology. The existing Zot registry Ingress provided the local model for nginx host routing and TLS edge termination.

## 2. Impact Analysis

### Epic Impact

- Epic 9 / FR31a Kubernetes Deployment Platform is affected.
- The change extends the deployment topology from 10 workloads to 12 workloads by adding:
  - `sample` EventStore counter domain service.
  - `sample-blazor-ui` browser UI.
- No PRD scope reduction is required. This is a direct deployment adjustment.

### Story Impact

- Existing Kubernetes deployment validation and publish scripts must recognize the new generated service folders.
- Teardown must include the new workloads, configmaps, Dapr configuration, and UI Ingress.
- Documentation that states pod counts, Dapr sidecar counts, or "no external Ingress" must be updated.

### Technical Impact

- Add nginx Ingress host rules:
  - `eventstore.hexalith.com` -> `Service/eventstore-admin-ui:8080`
  - `sample.hexalith.com` -> `Service/sample-blazor-ui:8080`
- Add `accesscontrol-sample` for EventStore -> sample domain invocation.
- Add EventStore ACL entries for `sample-blazor-ui` to submit sample commands and queries through Dapr.
- Enable EventStore SignalR for the sample UI.
- Reuse `hexalith-jwt-signing` for the sample UI token generator.
- Require an operator-managed TLS Secret named `hexalith-pages-tls` in namespace `hexalith-parties`.

## 3. Recommended Approach

**Direct Adjustment** is the right path.

Effort: Low to medium.
Risk: Medium, because public browser ingress and Dapr ACLs must stay aligned with deny-by-default service invocation.

Rollback is not useful because no completed feature needs to be removed. MVP scope remains achievable because this adds UI access without changing domain behavior or public API contracts.

## 4. Detailed Change Proposals

### Deployment Topology

OLD:

```text
Generated services: eventstore, eventstore-admin, eventstore-admin-ui, parties, parties-mcp, tenants, memories
UI access: port-forward or operator-added ingress only
```

NEW:

```text
Generated services: eventstore, eventstore-admin, eventstore-admin-ui, sample, sample-blazor-ui, parties, parties-mcp, tenants, memories
UI access: committed nginx Ingress for eventstore.hexalith.com and sample.hexalith.com
```

Rationale: The sample UI must exist as a Kubernetes workload before `sample.hexalith.com` can route to it.

### Dapr Access Control

OLD:

```text
EventStore ACL allows eventstore-admin, tenants, and parties.
No sample receiving-sidecar ACL exists in the Parties deployment.
```

NEW:

```text
EventStore ACL also allows sample-blazor-ui POST /api/v1/commands and POST /api/v1/queries.
accesscontrol-sample allows eventstore to invoke the sample service over POST.
```

Rationale: The browser UI uses a client-only Dapr sidecar and keeps backend access behind EventStore.

### Documentation

OLD:

```text
10 pods, 6 Dapr sidecars, no public Hexalith service Ingress.
```

NEW:

```text
12 pods, 8 Dapr sidecars, public Ingress only for the two browser UIs.
```

Rationale: Operator expectations must match what `publish.ps1` deploys.

## 5. Implementation Handoff

Scope classification: **Minor**.

Developer responsibilities:

- Keep AppHost as the source of generated workload topology.
- Keep `publish.ps1`, static validation, committed manifests, and teardown synchronized.
- Ensure the operator creates or provisions `Secret/hexalith-pages-tls` before relying on HTTPS.
- Verify with static validation and AppHost build.

Success criteria:

- `kubectl apply -k deploy/k8s/` includes `Ingress/hexalith-pages-ingress`.
- `eventstore.hexalith.com` routes to `eventstore-admin-ui`.
- `sample.hexalith.com` routes to `sample-blazor-ui`.
- `sample-blazor-ui` can invoke EventStore commands and queries through Dapr ACLs.
- `deploy/validate-deployment.ps1` reports no blocking findings.

## 6. Checklist Status

- [x] 1.1 Trigger identified: Kubernetes UI hostname access request.
- [x] 1.2 Core problem defined: missing Ingress plus missing sample UI workload.
- [x] 1.3 Evidence collected: Zot Ingress model exists; EventStore sample UI source exists; Parties topology lacked sample UI.
- [x] 2.1-2.5 Epic impact assessed: Epic 9 deployment scope extended without resequencing.
- [x] 3.1 PRD impact assessed: no MVP reduction.
- [x] 3.2 Architecture impact assessed: topology, Dapr ACL, and ingress documentation updated.
- [N/A] 3.3 UI/UX conflict: no UI behavior change, only deployment access.
- [x] 3.4 Secondary artifacts assessed: scripts, validation, teardown, docs.
- [x] 4.1 Direct adjustment selected.
- [N/A] 4.2 Rollback not useful.
- [N/A] 4.3 MVP review not required.
- [x] 5.1-5.5 Proposal and handoff completed.
- [x] 6.1-6.2 Proposal accuracy reviewed.
- [x] 6.3 Approval treated as implicit in the direct implementation request.
- [N/A] 6.4 No epic/story status reorganization required.
- [x] 6.5 Next steps defined.
