---
project: parties
date: 2026-06-26
workflow: bmad-correct-course
status: approved-implemented
trigger: "Use HexalithEventStoreSecurityExtensions to initialize the security service in the Parties Aspire host."
mode: batch
scope_classification: minor
---

# Sprint Change Proposal: EventStore Security Extension in Parties AppHost

## 1. Issue Summary

The Parties Aspire AppHost still initializes local Keycloak directly:

- `src/Hexalith.Parties.AppHost/Program.cs` builds `AddKeycloak("keycloak", 8180)` and a local `realmUrl` by hand.
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` pins that direct `AddKeycloak` call.

The EventStore submodule now provides `HexalithEventStoreSecurityExtensions` in `Hexalith.EventStore.Aspire`, including:

- `AddHexalithEventStoreSecurity(...)`
- `WithSecurityDependency(...)`
- `WithJwtBearerSecurity(...)`
- `WithOpenIdConnectSecurity(...)`
- `WithEventStoreClientCredentials(...)`

EventStore's own AppHost already uses `builder.AddHexalithEventStoreSecurity()` as its local security resource initializer. Parties should align with that platform helper so security resource setup, realm import, `EnableKeycloak`, and optional persistent-Keycloak fast-start behavior are owned by one extension instead of duplicated by hand.

Evidence:

- Parties AppHost direct setup: `src/Hexalith.Parties.AppHost/Program.cs:225`
- EventStore AppHost helper usage: `references/Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs:67`
- Security helper implementation: `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs:24`
- Parties fitness test pinning direct setup: `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs:39`

## 2. Impact Analysis

### Epic Impact

Epic 1 is affected because it owns AppHost, secure sign-in, and deployment foundation. All other epics remain valid.

No new epic is required. This is a correction to completed infrastructure wiring, not new product scope.

### Story Impact

Affected story:

- Story 1.10: "Deploy parties-ui (container + K8s) with production-KMS prerequisite gate"

Story 1.10 is currently `done`, but its AppHost task should be corrected to require EventStore's security extension for local security-service initialization.

Related completed stories remain compatible:

- Story 1.1: AppHost resource composition remains `parties-ui` with no Dapr sidecar.
- Story 1.2: `parties-ui` remains an OIDC relying party with server-side tokens only.
- Story 1.3 to 1.5: role routing, `party_id`, and self-scope behavior are unchanged.

### Artifact Conflicts

No standalone PRD exists by prior project decision; `epics.md` is the de-facto requirements source.

Artifacts needing updates after approval:

- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/implementation-artifacts/1-10-deploy-parties-ui-container-k8s-with-production-kms-prerequisite-gate.md`
- `src/Hexalith.Parties.AppHost/Program.cs`
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs`
- `docs/development-guide.md` or `docs/getting-started.md` if dashboard instructions still refer to a `keycloak` resource name.

Architecture impact is narrow: D10 AppHost/deploy should mention that the local Keycloak/security resource is initialized through EventStore's Aspire security extension.

UX artifacts are not affected.

### Technical Impact

The implementation should:

- Replace the hand-built local `AddKeycloak("keycloak", 8180)` block with `HexalithEventStoreSecurityResources? security = builder.AddHexalithEventStoreSecurity();`.
- Derive run-mode `realmUrl` from `security?.RealmUrl`.
- Replace direct `WithReference(keycloak).WaitFor(keycloak)` calls with `WithSecurityDependency(security)`.
- Preserve existing publish-mode `tache` authority/issuer handling.
- Preserve the custom multi-audience JWT settings for `parties`, `parties-mcp`, and `tenants`.
- Preserve `parties-ui` as an OIDC relying party, not a JWT bearer resource server.
- Preserve `EnableKeycloak=false` behavior.
- Update tests so they pin the extension call and security dependency pattern rather than direct `AddKeycloak`.

## 3. Recommended Approach

Recommended path: Direct Adjustment.

Rationale:

- The change is localized to AppHost security-resource composition and its fitness tests.
- No user-facing UI behavior changes.
- No event contracts, Dapr ACLs, public endpoints, or domain command/query contracts change.
- Existing EventStore helper APIs already exist and are in the referenced submodule.

Effort estimate: Low.

Risk level: Low to medium. The main risk is accidentally changing run vs publish behavior. Mitigate by keeping the publish-mode `tache` fallback intact and adding/adjusting AppHost fitness tests for `EnableKeycloak=false`, run-mode security dependency, and the custom audience settings.

Rejected options:

- Rollback: Not useful; the current completed implementation works but duplicates now-centralized security setup.
- MVP review: Not needed; product scope and MVP boundaries are unchanged.
- New epic/story: Too heavy for a narrow topology correction.

## 4. Detailed Change Proposals

### Story Change

Story: `1.10-deploy-parties-ui-container-k8s-with-production-kms-prerequisite-gate`

Section: Acceptance Criteria, AC3

OLD:

```markdown
AC3 - AppHost and publish topology include a non-Dapr `parties-ui` workload. Given `deploy/k8s/publish.ps1` runs, when aspirate generates manifests, then `parties-ui` is part of the generated service-folder set, image manifest verification, host-alias patching, health-probe patching, imagePullSecret patching, rollout restart, and readiness wait; `deploy/k8s/kustomization.yaml` includes `parties-ui`; the topology grows from 11 to 12 Parties-owned pods. `parties-ui` must remain a no-Dapr-sidecar BFF over HTTP/SignalR.
```

NEW:

```markdown
AC3 - AppHost and publish topology include a non-Dapr `parties-ui` workload and centralized local security-resource initialization. Given `deploy/k8s/publish.ps1` runs, when aspirate generates manifests, then `parties-ui` is part of the generated service-folder set, image manifest verification, host-alias patching, health-probe patching, imagePullSecret patching, rollout restart, and readiness wait; `deploy/k8s/kustomization.yaml` includes `parties-ui`; the topology grows from 11 to 12 Parties-owned pods. `parties-ui` must remain a no-Dapr-sidecar BFF over HTTP/SignalR. In run mode, the AppHost initializes the local Keycloak-backed security resource through `HexalithEventStoreSecurityExtensions.AddHexalithEventStoreSecurity()` rather than hand-building `AddKeycloak(...)`, while publish mode continues to use the external `tache` realm.
```

Rationale: Story 1.10 owns AppHost/deploy topology. The security-resource initializer is topology plumbing, not a new feature.

Section: Tasks / Subtasks, Task 4

OLD:

```markdown
- [x] Task 4 - Preserve the AppHost `parties-ui` resource shape (`src/Hexalith.Parties.AppHost/Program.cs`) (AC3, AC4)
  - [x] Keep `builder.AddProject<Projects.Hexalith_Parties_UI>("parties-ui")` with references/waits for `eventstore` and `tenants`.
  - [x] Keep `EventStore__SignalR__HubUrl` pointed at the EventStore projection hub.
  - [x] Keep `parties-ui` out of `WithDaprSidecar(...)`.
  - [x] In publish mode, keep nonsecret OIDC config values for the `tache` realm and avoid committing a literal production client secret.
```

NEW:

```markdown
- [ ] Task 4a - Align AppHost local security-resource initialization with EventStore Aspire helpers (`src/Hexalith.Parties.AppHost/Program.cs`, `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs`) (AC3, AC4)
  - [ ] Replace direct `builder.AddKeycloak("keycloak", 8180).WithRealmImport("./KeycloakRealms")` setup with `builder.AddHexalithEventStoreSecurity()`.
  - [ ] Use the returned `HexalithEventStoreSecurityResources` for run-mode `realmUrl` and security dependencies.
  - [ ] Keep publish mode on the external `https://auth.tache.ai/realms/tache` authority/issuer.
  - [ ] Keep `EnableKeycloak=false` clearing behavior for Admin UI auth env vars.
  - [ ] Keep `parties-ui` out of `WithDaprSidecar(...)`.
  - [ ] Update AppHost fitness tests so the contract pins `AddHexalithEventStoreSecurity`, `WithSecurityDependency`, and the existing custom audience settings.
```

Rationale: The story is already done, so this should be recorded as a corrective subtask rather than reshaping the whole story.

### Architecture Change

File: `_bmad-output/planning-artifacts/architecture.md`

Section: Infrastructure & Deployment, D10 - Aspire

OLD:

```markdown
- **D10 - Aspire:** add `builder.AddProject<Projects.Hexalith_Parties_UI>("parties-ui")`
  referencing `eventstore` (gateway) + `tenants`, with OIDC config (Keycloak run /
  `tache` publish). **No DAPR sidecar** - the UI is a BFF over HTTP + SignalR, like
  `parties-mcp` / `eventstore-admin-ui`.
```

NEW:

```markdown
- **D10 - Aspire:** add `builder.AddProject<Projects.Hexalith_Parties_UI>("parties-ui")`
  referencing `eventstore` (gateway) + `tenants`, with OIDC config (local Keycloak-backed
  security resource initialized via `HexalithEventStoreSecurityExtensions.AddHexalithEventStoreSecurity()` in run mode /
  `tache` publish). **No DAPR sidecar** - the UI is a BFF over HTTP + SignalR, like
  `parties-mcp` / `eventstore-admin-ui`.
```

Rationale: Makes the architecture match the centralized platform helper while preserving the current transport model.

### Code Change

File: `src/Hexalith.Parties.AppHost/Program.cs`

Section: local security resource setup

OLD:

```csharp
bool enableKeycloak = !bool.TryParse(builder.Configuration["EnableKeycloak"], out bool parsed) || parsed;
IResourceBuilder<KeycloakResource>? keycloak = null;
ReferenceExpression? realmUrl = null;
if (enableKeycloak)
{
    if (builder.ExecutionContext.IsRunMode)
    {
        keycloak = builder.AddKeycloak("keycloak", 8180)
            .WithRealmImport("./KeycloakRealms");

        EndpointReference keycloakEndpoint = keycloak.GetEndpoint("http");
        realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/hexalith");
    }
}

if (keycloak is not null)
{
    _ = eventStore.WithReference(keycloak).WaitFor(keycloak);
    _ = adminServer.WithReference(keycloak).WaitFor(keycloak);
    _ = parties.WithReference(keycloak).WaitFor(keycloak);
    _ = partiesMcp.WithReference(keycloak).WaitFor(keycloak);
    _ = tenants.WithReference(keycloak).WaitFor(keycloak);
    _ = adminUI.WithReference(keycloak).WaitFor(keycloak);
    _ = partiesUi.WithReference(keycloak).WaitFor(keycloak);
}
```

NEW:

```csharp
HexalithEventStoreSecurityResources? security = builder.ExecutionContext.IsRunMode
    ? builder.AddHexalithEventStoreSecurity()
    : null;
ReferenceExpression? realmUrl = security?.RealmUrl;

if (security is not null)
{
    _ = eventStore.WithSecurityDependency(security);
    _ = adminServer.WithSecurityDependency(security);
    _ = parties.WithSecurityDependency(security);
    _ = partiesMcp.WithSecurityDependency(security);
    _ = tenants.WithSecurityDependency(security);
    _ = adminUI.WithSecurityDependency(security);
    _ = partiesUi.WithSecurityDependency(security);
}
```

Implementation note: Keep the existing `WithJwtAuthentication(...)` helper and later environment chains unless the implementation chooses to adopt `WithJwtBearerSecurity(...)` in the same patch. If `WithJwtBearerSecurity(...)` is adopted, the patch must re-apply the custom `ValidAudiences` values for `parties`, `parties-mcp`, and `tenants`.

Comment update in the `parties-ui` OIDC block:

OLD:

```csharp
// Mirrors the adminUI realm/publish conditional above; parties-ui references and waits for keycloak
// in the `if (keycloak is not null)` block.
```

NEW:

```csharp
// Mirrors the adminUI realm/publish conditional above; parties-ui references and waits for the
// local security resource when AddHexalithEventStoreSecurity returns one.
```

### Test Change

File: `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs`

Section: `AppHostProgramComposesStandaloneEventStoreTopologyWithStableResourceNames`

OLD:

```csharp
program.ShouldContain(@"AddKeycloak(""keycloak"", 8180)");
```

NEW:

```csharp
program.ShouldContain("AddHexalithEventStoreSecurity()");
program.ShouldNotContain(@"AddKeycloak(""keycloak"", 8180)");
```

Section: `AppHostProgramWiresKeycloakToEventStoreAdminPartiesAndTenants`

OLD:

```csharp
program.ShouldContain("eventStore.WithReference(keycloak)");
program.ShouldContain("adminServer.WithReference(keycloak)");
program.ShouldContain("parties.WithReference(keycloak)");
program.ShouldContain("tenants.WithReference(keycloak)");
program.ShouldContain("adminUI.WithReference(keycloak)");
```

NEW:

```csharp
program.ShouldContain("eventStore.WithSecurityDependency(security)");
program.ShouldContain("adminServer.WithSecurityDependency(security)");
program.ShouldContain("parties.WithSecurityDependency(security)");
program.ShouldContain("partiesMcp.WithSecurityDependency(security)");
program.ShouldContain("tenants.WithSecurityDependency(security)");
program.ShouldContain("adminUI.WithSecurityDependency(security)");
program.ShouldContain("partiesUi.WithSecurityDependency(security)");
```

Rationale: The test should pin the centralized helper contract and include `parties-mcp` / `parties-ui`, which are currently part of the dependency block.

## 5. Implementation Handoff

Scope classification: Minor.

Route to: Developer agent.

Responsibilities:

- Update the AppHost security initialization and comments.
- Update AppHost fitness tests.
- Update Story 1.10 and architecture text if the team wants planning artifacts to stay executable as written.
- Run targeted verification.

Recommended verification:

- `dotnet build src/Hexalith.Parties.AppHost -c Release -m:1`
- `dotnet build tests/Hexalith.Parties.Tests -c Release -m:1`
- Direct xUnit v3 executable run for `Hexalith.Parties.Tests.FitnessTests.AppHostTenantsTopologyTests` if needed.
- `bash scripts/check-no-warning-override.sh`

Success criteria:

- Parties AppHost uses `HexalithEventStoreSecurityExtensions` to initialize the run-mode local security service.
- `EnableKeycloak=false` still leaves `realmUrl` null and keeps Admin UI stale-auth clearing behavior.
- Publish mode still uses `https://auth.tache.ai/realms/tache`.
- `parties`, `parties-mcp`, and `tenants` still accept their own audience plus `hexalith-eventstore`.
- `parties-ui` remains an OIDC relying party and has no Dapr sidecar.

## 6. Checklist Status

| Checklist item | Status | Notes |
|---|---:|---|
| 1.1 Triggering story identified | [x] | Story 1.10 AppHost/deploy topology. |
| 1.2 Core problem defined | [x] | Technical alignment issue: duplicated local Keycloak setup instead of platform extension. |
| 1.3 Evidence gathered | [x] | Code and tests listed above. |
| 2.1 Current epic evaluated | [x] | Epic 1 remains complete with a minor corrective follow-up. |
| 2.2 Epic-level changes | [N/A] | No epic scope change. |
| 2.3 Remaining epics reviewed | [x] | Epics 2-5 unaffected. |
| 2.4 New/obsolete epics | [N/A] | None. |
| 2.5 Epic priority/order | [N/A] | No resequencing. |
| 3.1 PRD/de-facto requirements conflicts | [x] | No product requirement conflict; de-facto PRD is `epics.md`. |
| 3.2 Architecture conflicts | [x] | D10 wording should be updated to name the security extension. |
| 3.3 UI/UX conflicts | [N/A] | No UI/UX behavior change. |
| 3.4 Other artifacts | [x] | AppHost tests and development docs may need small updates. |
| 4.1 Direct adjustment | [x] | Viable; recommended. Effort low, risk low-medium. |
| 4.2 Rollback | [N/A] | Not useful. |
| 4.3 MVP review | [N/A] | MVP scope unchanged. |
| 4.4 Recommended path | [x] | Direct adjustment. |
| 5.1 Issue summary | [x] | Included. |
| 5.2 Impact summary | [x] | Included. |
| 5.3 Recommendation | [x] | Included. |
| 5.4 MVP impact/action plan | [x] | No MVP impact; action plan included. |
| 5.5 Handoff plan | [x] | Developer agent direct implementation. |
| 6.1 Checklist completion | [x] | All applicable items addressed. |
| 6.2 Proposal accuracy | [x] | Based on current repo files and EventStore helper API. |
| 6.3 User approval | [x] | Approved by Administrator on 2026-06-26. |
| 6.4 sprint-status.yaml update | [N/A] | No epic/story add/remove/renumber. |
| 6.5 Next steps | [x] | Routed to Developer implementation and completed as a minor direct adjustment. |

## 7. Approval

Approved by Administrator on 2026-06-26 and implemented as a minor direct adjustment.

Implemented files:

- `src/Hexalith.Parties.AppHost/Program.cs`
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/implementation-artifacts/1-10-deploy-parties-ui-container-k8s-with-production-kms-prerequisite-gate.md`
- `README.md`
- `docs/development-guide.md`
- `docs/getting-started.md`
- `docs/source-tree-analysis.md`
