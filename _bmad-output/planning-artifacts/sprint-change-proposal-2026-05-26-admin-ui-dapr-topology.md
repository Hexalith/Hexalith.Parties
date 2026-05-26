# Sprint Change Proposal - Admin UI Dapr Topology Correction

Date: 2026-05-26
Project: Hexalith.Parties
Scope classification: Minor
Recommended path: Direct Adjustment

## 1. Issue Summary

During the post-publish investigation for Epic 9, the publish pipeline failed before Kubernetes apply with:

```text
[publish] ERROR: forbidden target eventstore-admin-ui already carries Dapr annotations
```

The failure exposed a stale deployment assumption: `eventstore-admin-ui` was classified as a non-Dapr workload in `deploy/k8s/publish.ps1`, deploy-validation tests, and canonical topology documentation. That assumption is contradicted by the current EventStore runtime. Admin UI invokes Admin Server through its local Dapr sidecar using header-based service invocation (`dapr-app-id: eventstore-admin`) and fails fast when no sidecar is present.

Confirmed evidence:

- `src/Hexalith.Parties.AppHost/Program.cs` passes `eventstore-admin-ui` into `AddHexalithEventStore(...)`.
- `Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` wires `eventstore-admin-ui` with `.WithDaprSidecar(...)`.
- `Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/Program.cs` calls `RequireDaprSidecar()`.
- `Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs` configures the Admin API client to target the local Dapr HTTP endpoint.
- `Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/Services/DaprAppIdHandler.cs` adds `dapr-app-id: eventstore-admin`.

The initial local idea to strip Dapr annotations from `eventstore-admin-ui` is therefore wrong and must be abandoned before implementation.

## 2. Impact Analysis

### Checklist Results

| Item | Status | Finding |
|---|---|---|
| 1.1 Trigger story | Done | Triggered by Story 9.8 runtime publish hardening after live publish with the GitHub version. |
| 1.2 Core problem | Done | Misunderstanding of the runtime topology: Admin UI is Dapr client-only, not non-Dapr. |
| 1.3 Evidence | Done | Runtime code and AppHost helper prove sidecar requirement. |
| 2.1 Current epic viability | Done | Epic 9 remains valid; it needs a post-epic corrective story or patch note, not a replan. |
| 2.2 Epic-level changes | Action-needed | Epic 9 topology text and story acceptance criteria must distinguish client-only Dapr workloads from full state/pubsub workloads. |
| 2.3 Future epic impact | Done | No future epic invalidated. This is deployment topology correction only. |
| 2.4 New epic needed | N/A | No new epic required. |
| 2.5 Priority/order | Done | Immediate fix before another publish run; no broader resequencing. |
| 3.1 PRD conflicts | Resolved-by-implementation | FR31a must describe the delivered 10-workload topology with Admin UI client-only sidecar. |
| 3.2 Architecture conflicts | Action-needed | `docs/kubernetes-deployment-architecture.md` says Admin UI has no Dapr sidecar. |
| 3.3 UI/UX conflicts | N/A | No screen or interaction change. |
| 3.4 Other artifacts | Action-needed | `publish.ps1`, Dapr ACL YAML, deploy-validation tests, and deployment docs require updates. |
| 4.1 Direct adjustment | Viable | Low effort, medium operational risk if left unfixed. |
| 4.2 Rollback | Not viable | Rolling back EventStore Admin UI Dapr service invocation would undo intentional D13 behavior. |
| 4.3 MVP review | Not viable | MVP scope is unchanged. |
| 4.4 Recommended path | Done | Direct adjustment. |

### Epic Impact

Epic 9 remains done as an MVP deployment platform, but its as-built topology must be corrected:

- `eventstore-admin-ui` is Dapr-equipped for service invocation only.
- `parties-mcp`, `redis`, `keycloak`, and `falkordb` remain non-Dapr.
- The topology should distinguish two Dapr categories:
  - Full Dapr workloads: state/pubsub and/or inbound app-port validation (`eventstore`, `eventstore-admin`, `parties`, `tenants`, `memories`).
  - Client-only Dapr workloads: outbound service invocation sidecar without state/pubsub (`eventstore-admin-ui`).

### Technical Impact

Affected implementation artifacts:

- `deploy/k8s/publish.ps1`
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.eventstore-admin.yaml`
- `deploy/k8s/eventstore-admin-ui/deployment.yaml` after regeneration/patching
- deploy-validation tests under `tests/Hexalith.Parties.DeployValidation.Tests`
- AppHost/topology fitness tests if they assert non-Dapr Admin UI behavior
- deployment docs and architecture topology tables
- `sprint-status.yaml` only if a new corrective story is tracked

No product API, user workflow, UX, or domain behavior changes are required.

## 3. Recommended Approach

Use a direct adjustment with a corrective implementation task, preferably tracked as a post-Epic 9 correction:

```text
Story 9.12: EventStore Admin UI Dapr Topology Correction
```

This avoids reopening completed Epic 9 stories while preserving traceability. If the team wants to avoid adding another story after the retrospective, the same work can be recorded as a Story 9.8 corrective patch, but a named 9.9 is cleaner because the issue was discovered after 9.8 was marked done.

Effort: Low to Medium.

Risk: Medium if not fixed, because publish is currently blocked before apply and any forced stripping of Dapr annotations would break Admin UI startup/API calls.

Timeline impact: Same-day correction expected. No epic resequencing.

## 4. Detailed Change Proposals

### Story Change Proposal

Story: New `9-12-eventstore-admin-ui-dapr-topology-correction`

Section: Story

OLD:

```text
No tracked story exists for the post-9.8 discovery that eventstore-admin-ui is Dapr client-only.
```

NEW:

```text
As an operator publishing Hexalith.Parties to Kubernetes,
I want eventstore-admin-ui to be modeled as a Dapr client-only workload,
so that publish.ps1 preserves the required sidecar for Admin UI -> Admin Server service invocation while still rejecting accidental Dapr injection on true non-Dapr workloads.
```

Acceptance Criteria:

```text
AC1 - Admin UI is no longer classified as forbidden Dapr
Given publish.ps1 regenerates manifests from the AppHost,
when eventstore-admin-ui carries dapr.io/enabled and dapr.io/app-id=eventstore-admin-ui,
then the Dapr patch phase treats those annotations as expected, not forbidden.

AC2 - Client-only Dapr validation is explicit
Given eventstore-admin-ui does not use state store or pub/sub components directly,
when publish.ps1 validates its annotations,
then it requires dapr.io/enabled=true and dapr.io/app-id=eventstore-admin-ui
and it does not require app-port/state/pubsub references unless inbound invocation is intentionally added later.

AC3 - Admin Server ACL permits Admin UI invocation
Given Admin UI invokes Admin Server through Dapr,
when accesscontrol-eventstore-admin is applied,
then it includes a policy allowing caller appId eventstore-admin-ui to invoke Admin Server HTTP API routes.

AC4 - True non-Dapr workloads stay protected
Given parties-mcp, redis, keycloak, and falkordb are not Dapr-equipped,
when publish.ps1 validates generated and preserved manifests,
then those workloads still fail if they carry dapr.io/* annotations.

AC5 - Documentation and tests reflect the corrected topology
Given the deployment docs and tests are read,
when they describe the Kubernetes topology,
then eventstore-admin-ui is shown as Dapr client-only and parties-mcp/redis/keycloak/falkordb remain non-Dapr.

AC6 - Publish verification
Given valid Zot credentials and cluster context,
when publish.ps1 runs after the correction,
then it passes the Dapr annotation phase and proceeds to validation/apply, or fails later with a bounded actionable reason unrelated to Admin UI being forbidden Dapr.
```

Rationale:

Admin UI runtime code requires a Dapr sidecar. The deployment scripts must enforce the actual runtime topology rather than an outdated documentation assumption.

### PRD Change Proposal

Section: FR31a

OLD:

```text
... totalling 9 workloads in namespace `hexalith-parties`.
```

NEW:

```text
... totalling 10 workloads in namespace `hexalith-parties`: 7 Aspirate-composed application workloads (`eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, `tenants`, `memories`) plus 3 hand-authored carve-outs (`redis`, `keycloak`, `falkordb`). Dapr-equipped workloads include `eventstore`, `eventstore-admin`, `eventstore-admin-ui` (service-invocation client only), `parties`, `tenants`, and `memories`; `parties-mcp`, `redis`, `keycloak`, and `falkordb` remain non-Dapr.
```

Rationale:

FR31a is already stale after the FalkorDB addition and now also stale for Admin UI Dapr classification.

### Epic Change Proposal

Section: Epic 9 / Story 9.2 acceptance criteria

OLD:

```text
And non-Dapr workloads (`eventstore-admin-ui`, `parties-mcp`) are declared without a daprd sidecar.
```

NEW:

```text
And Dapr classification is explicit:
- `eventstore`, `eventstore-admin`, `parties`, `tenants`, and `memories` are full Dapr workloads.
- `eventstore-admin-ui` is Dapr client-only for Admin UI -> Admin Server service invocation.
- `parties-mcp`, `redis`, `keycloak`, and `falkordb` are non-Dapr workloads.
```

Section: Epic 9 / Story 9.4 acceptance criteria

OLD:

```text
And `parties-mcp` and `eventstore-admin-ui` carry NO `dapr.io/*` annotations.
```

NEW:

```text
And only true non-Dapr workloads (`parties-mcp`, `redis`, `keycloak`, `falkordb`) carry no `dapr.io/*` annotations.
And `eventstore-admin-ui` carries the client-only Dapr annotations required for service invocation.
```

Rationale:

The current text directly conflicts with the EventStore helper and Admin UI runtime.

### Architecture Change Proposal

Artifact: `docs/kubernetes-deployment-architecture.md`

OLD:

```text
eventstore-admin-ui ... (no Dapr sidecar)
```

NEW:

```text
eventstore-admin-ui ... (Dapr client-only sidecar)
```

OLD:

```text
Each Hexalith service that needs state or pubsub carries a daprd sidecar.
```

NEW:

```text
Each Hexalith service that needs state, pub/sub, actors, or Dapr service invocation carries a daprd sidecar. `eventstore-admin-ui` uses a client-only sidecar for Admin UI -> Admin Server invocation and does not reference state or pub/sub components directly.
```

Rationale:

The old wording misses service-invocation-only sidecars.

### Implementation Change Proposal

Artifact: `deploy/k8s/publish.ps1`

OLD:

```powershell
$DaprPatchMap = [ordered]@{
    'eventstore' = 'accesscontrol'
    'eventstore-admin' = 'accesscontrol-eventstore-admin'
    'parties' = 'accesscontrol-parties'
    'tenants' = 'accesscontrol-tenants'
    'memories' = 'accesscontrol-memories'
}

$ForbiddenDaprTargets = @('eventstore-admin-ui', 'parties-mcp', 'redis', 'keycloak', 'falkordb')
```

NEW:

```powershell
$DaprPatchMap = [ordered]@{
    'eventstore' = 'accesscontrol'
    'eventstore-admin' = 'accesscontrol-eventstore-admin'
    'parties' = 'accesscontrol-parties'
    'tenants' = 'accesscontrol-tenants'
    'memories' = 'accesscontrol-memories'
}

$DaprClientOnlyTargets = @('eventstore-admin-ui')
$ForbiddenDaprTargets = @('parties-mcp', 'redis', 'keycloak', 'falkordb')
```

Additional implementation notes:

- Remove the uncommitted `Remove-DaprAnnotations`/strip behavior from the current working tree.
- Add an assertion helper for client-only Dapr targets.
- Keep the full `Assert-DaprTemplateAnnotations` path for services requiring app-port/config.
- Keep Admin UI without `dapr.io/config`; receiver-side ACL lives on `eventstore-admin`, and tests assert `eventstore-admin-ui -> eventstore-admin` is allowed there.

Artifact: `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.eventstore-admin.yaml`

OLD:

```yaml
spec:
  accessControl:
    defaultAction: deny
    trustDomain: "public"
    policies: []
```

NEW:

```yaml
spec:
  accessControl:
    defaultAction: allow
    trustDomain: "public"
    policies:
      - appId: eventstore-admin-ui
        defaultAction: deny
        trustDomain: "public"
        namespace: "default"
        operations:
          - name: /**
            httpVerb: ['GET', 'POST', 'PUT', 'DELETE']
            action: allow
```

Rationale:

This aligns Parties' copied Dapr config with the EventStore submodule's current `accesscontrol.eventstore-admin.yaml`. If the deployment enables mTLS/Sentry later, the production hardening path can flip top-level `defaultAction` to deny with matching trust-domain/namespace identity.

### Test Change Proposal

Update tests that currently encode the stale non-Dapr assumption:

- `K8sManifestPublishTests`: move `eventstore-admin-ui` out of `s_nonDaprApps` and into a client-only Dapr set.
- `OperatorScriptValidationTests`: replace assertions that Admin UI lacks `dapr.io/app-id` with assertions that it has `dapr.io/enabled` and `dapr.io/app-id: eventstore-admin-ui`.
- `DaprManifestValidationTests`: stop rejecting `eventstore-admin-ui` app-id globally; only reject it if it appears in state/pubsub scopes where it should not.
- `DaprAccessControlFitnessTests`: allow `eventstore-admin-ui -> eventstore-admin` and continue rejecting unrelated callers.
- Add regression coverage that `parties-mcp`, `redis`, `keycloak`, and `falkordb` still fail if Dapr annotations appear.

## 5. Implementation Handoff

Scope: Minor.

Route to: Developer agent.

Responsibilities:

- Revert the local uncommitted strip-Dapr patch in `deploy/k8s/publish.ps1`.
- Implement the client-only Dapr classification.
- Align Admin Server Dapr ACL with the EventStore submodule.
- Update deploy-validation tests and topology docs.
- Regenerate/patch manifests through the existing publish pipeline, not by manually inventing runtime YAML.
- Verify with focused tests and, if credentials/context are valid, rerun publish.

Suggested verification:

```bash
pwsh -NoProfile -File deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/
dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Release --filter "Category!=LiveCluster"
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --filter "FullyQualifiedName~AppHostTenantsTopologyTests"
git diff --check
```

Live verification when available:

```bash
pwsh -NoProfile -File deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local
```

Success criteria:

- Publish no longer fails because `eventstore-admin-ui` carries Dapr annotations.
- Admin UI pod starts with a Dapr sidecar.
- Admin UI can invoke Admin Server through Dapr service invocation.
- True non-Dapr workloads stay annotation-free.
- Static docs, tests, and runtime topology agree.

## 6. Final Recommendation

Approve direct implementation of this correction. Do not remove Dapr from `eventstore-admin-ui`; the sidecar is required by the current EventStore Admin UI runtime.
