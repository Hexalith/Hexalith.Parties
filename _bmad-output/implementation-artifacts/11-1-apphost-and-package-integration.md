# Story 11.1: AppHost and Package Integration

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer running Hexalith.Parties locally,
I want the Parties AppHost to compose with Hexalith.Tenants,
so that tenant lifecycle and membership are available through the same local topology as party management.

## Acceptance Criteria

1. Given the Parties AppHost, when the local development topology is started, then the AppHost references the Tenants service/topology using the Tenants Aspire integration or equivalent local composition, and Tenants service configuration is visible in the topology.
2. Given local development setup, when the default sample environment is prepared, then a default active tenant is seeded or documented through Hexalith.Tenants, and the sample/test user is assigned a role that permits party commands.
3. Given tenant authorization is enabled, when Parties starts, then startup validates that Tenants integration configuration is present, and missing configuration fails fast with actionable diagnostics.
4. Given the Tenants service cannot be reached, when Parties health and readiness are checked, then the failure is surfaced according to documented degraded behavior.

## Tasks / Subtasks

- [ ] Compose Hexalith.Tenants into the Parties AppHost topology (AC: 1)
  - [ ] Add project references needed by `src/Hexalith.Parties.AppHost` to use `Hexalith.Tenants.Aspire` and the Tenants service project.
  - [ ] Reuse `Hexalith.Tenants.Aspire.HexalithTenantsExtensions.AddHexalithTenants`; do not recreate Tenants state store/pubsub wiring by hand unless the Tenants Aspire API is unusable.
  - [ ] Ensure the Tenants resource appears with a stable Aspire resource name, preferably `tenants`, and the existing `commandapi` resource keeps its current DAPR app id as the EventStore command gateway hosting Parties module handlers.
  - [ ] Wire the `commandapi` EventStore command gateway to the Tenants resource with `WithReference(...)` and `WaitFor(...)` or an equivalent health-gated dependency, without breaking the existing EventStore topology created by `AddHexalithParties`.
  - [ ] Keep the local access-control path resolution intact; if touched, prefer the Tenants AppHost `ResolveDaprConfigPath(builder.AppHostDirectory, ...)` pattern because it works under both `dotnet run` and Aspire testing.

- [ ] Align local development bootstrap/configuration (AC: 2, 3)
  - [ ] Add explicit configuration for Tenants bootstrap/user setup in local development, using Tenants-supported settings such as `Tenants:BootstrapGlobalAdminUserId` where applicable.
  - [ ] Document or wire the path for creating a default active tenant and assigning a sample/test user with a role that permits party commands. The current Tenants bootstrap service only bootstraps a global administrator; do not claim it creates a tenant/member unless the code is extended or a documented command path is provided.
  - [ ] Add a Parties-side tenant integration configuration section, for example `Tenants:Enabled`, `Tenants:ServiceName`, `Tenants:CommandApiAppId`, `Tenants:PubSubName`, and `Tenants:TopicName`, matching the actual options consumed by the code. Keep `Tenants:CommandApiAppId` pointed at `commandapi` when it identifies the EventStore command gateway.
  - [ ] Fail fast at startup only when tenant authorization is enabled and required Tenants settings are missing; keep existing non-Tenants local smoke paths usable when the feature is explicitly disabled.

- [ ] Surface Tenants reachability through health/readiness (AC: 4)
  - [ ] Add a health check that verifies Tenants integration reachability/configuration using the concrete integration path selected for this story.
  - [ ] Decide and document whether Tenants unreachability is `Unhealthy` for `/ready` when tenant authorization is enabled, or `Degraded` when the feature is disabled or operating in an explicitly documented degraded mode.
  - [ ] Preserve the existing DAPR readiness behavior: `dapr-sidecar` and `dapr-statestore` remain readiness-gating checks, while pub/sub and projection actor health currently contribute to `/health` as degraded dependencies.

- [ ] Add focused tests and validation (AC: 1, 3, 4)
  - [ ] Add AppHost or configuration tests that prove the Tenants project/reference is present and the `commandapi` EventStore command gateway dependency is wired.
  - [ ] Add startup/options validation tests for missing Tenants configuration when tenant authorization is enabled.
  - [ ] Add health-check tests for Tenants reachable/unreachable behavior, using fakes where possible instead of requiring a full DAPR/Tenants runtime.
  - [ ] Run at minimum `dotnet build Hexalith.Parties.slnx --configuration Release` and the affected test projects.

- [ ] Keep package and dependency boundaries clean (AC: 1)
  - [ ] Do not add Tenants dependencies to `src/Hexalith.Parties.Contracts`; that package must remain free of Tenants runtime references.
  - [ ] Add Tenants references only where needed: AppHost/Aspire for topology, CommandApi for client/options/health if required, and tests for test helpers.
  - [ ] Do not initialize or update nested submodules recursively. The root-level `Hexalith.Tenants` submodule already exists for this work.

## Dev Notes

### Epic Context

Epic 11 exists to make `Hexalith.Tenants` the source of truth for tenant lifecycle, membership, roles, and configuration while Parties continues to own party aggregates, projections, REST/MCP surfaces, and tenant-scoped data isolation. Epic 11 must be completed before Epic 10 so the admin portal consumes tenant context instead of duplicating tenant-management UI. [Source: _bmad-output/planning-artifacts/epics.md#Epic 11: Hexalith.Tenants Integration for Parties]

This story is the first integration slice. It should only compose Tenants into local topology, establish configuration/bootstrap expectations, and expose health/readiness behavior. Event subscription/local access projection is Story 11.2; REST/MCP authorization enforcement is Story 11.3; deployment validation/docs/testing breadth is Story 11.4. [Source: _bmad-output/planning-artifacts/epics.md#Story 11.1: AppHost and Package Integration]

### Command Gateway Boundary

Client applications send tenant-scoped commands to the EventStore command API identified by DAPR app id `commandapi`. EventStore owns the tenant access gate: it checks whether the user can access the target tenant, then routes the command to the Parties module handler. Parties owns command/domain validation and event production after the command has passed through that gateway.

For this story, `commandapi` is therefore intentional: it is the EventStore command gateway hosting/routing Parties module handlers, not a Tenants service resource and not a reason to point Tenants command routing at `tenants`. The `tenants` resource is the Tenants service/topology resource that supports tenant lifecycle and access decisions. Story 11.1 must preserve this boundary and must not introduce direct client-to-Parties command ingress or duplicate tenant-access authorization inside Parties.

### Approved Change Context

The May 2, 2026 sprint change proposal is approved and explicitly says Parties must use `Hexalith.Tenants` for tenant lifecycle, membership, roles, and configuration rather than treating tenant handling as a local Parties concern. It calls out AppHost/local development as an affected completed area and says local dev should provision tenant state through Tenants. [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-02.md#1. Issue Summary]

Developer responsibilities from the approved proposal:

- Inspect current public APIs/packages in `Hexalith.Tenants` before coding against assumptions.
- Add Tenants package/project references only where needed.
- Keep `Hexalith.Parties.Contracts` free of Tenants dependencies.
- Preserve EventStore tenant scoping and actor key formats; Tenants does not replace aggregate identity isolation.
- Use Tenants testing helpers in later authorization tests where appropriate. [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-02.md#5. Implementation Handoff]

### Architecture Constraints

- Target framework is `net10.0`; warnings are treated as errors. [Source: Directory.Build.props]
- Current Parties package versions are centrally managed in `Directory.Packages.props`: DAPR SDK 1.16.1, Aspire 13.1.2, CommunityToolkit.Aspire.Hosting.Dapr 13.0.0, MediatR 14.0.0, FluentValidation 12.1.1, MCP SDK 1.0.0.
- The Tenants submodule currently uses newer package versions: DAPR SDK 1.17.7 and Aspire.Hosting 13.2.2 in `Hexalith.Tenants/Directory.Packages.props`. Coordinate version alignment deliberately; do not silently mix incompatible Aspire SDK/package versions.
- Architecture says local full topology is run with `dotnet aspire run --project src/Hexalith.Parties.AppHost`, with DAPR sidecars managed by Aspire. [Source: _bmad-output/planning-artifacts/architecture.md#Development Commands]
- Architecture defines `AddHexalithParties()` on `IDistributedApplicationBuilder` as the Parties Aspire hosting extension. [Source: _bmad-output/planning-artifacts/architecture.md#Coding Standards and Patterns]
- Architecture currently describes multi-tenancy as EventStore/Parties enforcement at aggregate, event store, projection, API, MCP, and pub/sub layers. Epic 11 refines this: Tenants is the authority, while EventStore/Parties remain responsible for enforcement and tenant-scoped storage/query isolation. [Source: _bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns]

### Current Code State and Files Likely Touched

`src/Hexalith.Parties.AppHost/Program.cs`

- Current state: creates the distributed app builder; resolves `DaprComponents/accesscontrol.yaml` from the working directory with a base-directory fallback; adds `commandapi`; calls `builder.AddHexalithParties(commandApi, accessControlConfigPath)`; optionally wires Keycloak to `commandapi`; configures publish targets; then runs the AppHost.
- Story change: add the Tenants project resource, call `AddHexalithTenants`, and wire the `commandapi` EventStore command gateway to Tenants. Keep existing Keycloak and publish-target behavior.
- Preserve: `commandapi` DAPR app id, existing access-control validation, existing EventStore/Parties topology, Keycloak fallback behavior, and publish-target logic.

`src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj`

- Current state: uses `Aspire.AppHost.Sdk/13.1.2`, references `Hexalith.Parties.Aspire` with `IsAspireProjectResource="false"` and `Hexalith.Parties.CommandApi`, and copies `DaprComponents/**/*` and `KeycloakRealms/**/*`.
- Story change: add project references for Tenants service/topology as needed, likely `..\..\Hexalith.Tenants\src\Hexalith.Tenants\Hexalith.Tenants.csproj` and `..\..\Hexalith.Tenants\src\Hexalith.Tenants.Aspire\Hexalith.Tenants.Aspire.csproj` with `IsAspireProjectResource="false"` for the Aspire extension project.
- Preserve: content-copy behavior for DAPR components and Keycloak realms.

`src/Hexalith.Parties.Aspire/HexalithPartiesExtensions.cs` and `HexalithPartiesResources.cs`

- Current state: `AddHexalithParties` wraps `Hexalith.EventStore.Aspire.AddHexalithEventStore(commandApi, daprConfigPath)` and returns `HexalithPartiesResources(EventStoreResources, CommandApi)`.
- Story change: only modify these files if the team chooses to expose Tenants as part of the reusable Parties Aspire extension. A simpler AppHost-only composition is acceptable for this story.
- Preserve: EventStore wrapping behavior and the public extension shape unless a deliberate API change is needed.

`src/Hexalith.Parties.CommandApi/Program.cs`

- Current state: registers service defaults, DAPR client, Parties DAPR health checks, Parties services, Swagger in development, GDPR/correlation/error/degraded/auth middleware, controllers, MCP, actor handlers, and default endpoints.
- Story change: register Tenants client/options and map/health-check integration only if required for startup validation or readiness in this story. Full Tenants event subscription belongs to Story 11.2.
- Preserve: middleware order, `app.MapMcp().RequireAuthorization()`, actor handler mapping, and existing health endpoints.

`src/Hexalith.Parties.CommandApi/HealthChecks/PartiesHealthCheckExtensions.cs`

- Current state: `AddPartiesDaprHealthChecks` gates readiness on `dapr-sidecar` and `dapr-statestore`; `dapr-pubsub` and `projection-actors` are degraded health contributors.
- Story change: add Tenants health check registration in a way that keeps tags/failure status explicit and testable.
- Preserve: current DAPR health-check names and defaults.

`src/Hexalith.Parties.CommandApi/appsettings*.json`

- Current state: base appsettings only logging/allowed hosts; development appsettings contains JWT bearer dev settings.
- Story change: add Tenants configuration for local development and startup validation. Prefer a small, explicit config section over implicit magic defaults.
- Preserve: existing JWT development fallback.

### Hexalith.Tenants APIs to Reuse

Current Tenants submodule status: `Hexalith.Tenants` is checked out at `5e27c2b32c420ca400f8d1ca69d19cd09c4f2c7e` (`v1.0.0-8-g5e27c2b`). [Source: git submodule status -- Hexalith.Tenants]

Relevant Tenants APIs:

- `Hexalith.Tenants.Aspire.HexalithTenantsExtensions.AddHexalithTenants(IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> tenants, string? daprConfigPath = null)` wires a Redis-backed DAPR state store, DAPR pub/sub, and a Tenants DAPR sidecar with app id `tenants`. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Aspire/HexalithTenantsExtensions.cs]
- `HexalithTenantsResources` exposes `StateStore`, `PubSub`, and `CommandApi` resource builders. The `CommandApi` property is the Tenants project resource despite the generic name. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Aspire/HexalithTenantsResources.cs]
- `Hexalith.Tenants.Client.Registration.AddHexalithTenants()` registers DAPR client, `HexalithTenantsOptions`, event handlers, `TenantEventProcessor`, and an in-memory `ITenantProjectionStore` default. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Registration/TenantServiceCollectionExtensions.cs]
- `HexalithTenantsOptions` defaults: `PubSubName = "pubsub"`, `TopicName = "system.tenants.events"`, and `CommandApiAppId = "commandapi"`. In the Parties topology, `CommandApiAppId = "commandapi"` is intentional when it points to the EventStore command gateway that performs tenant access checks before dispatching to Parties module handlers; do not change it to `tenants` merely because the Tenants service resource uses app id `tenants`. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Configuration/HexalithTenantsOptions.cs]
- `TenantBootstrapHostedService` only bootstraps a global administrator when `Tenants:BootstrapGlobalAdminUserId` is configured. It sends `BootstrapGlobalAdmin` through DAPR service invocation to EventStore app id `eventstore`; it does not create an application tenant or assign tenant membership. [Source: Hexalith.Tenants/src/Hexalith.Tenants/Bootstrap/TenantBootstrapHostedService.cs]
- Tenants roles are `TenantOwner`, `TenantContributor`, and `TenantReader`; map Parties read/write/admin expectations to these names rather than inventing `Reader`, `Contributor`, or `Owner` enum values in Parties code. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Enums/TenantRole.cs]

### Implementation Guardrails

- Do not make Parties the tenant lifecycle authority. Parties must not create, update, disable, enable, or manage tenant membership directly except through documented Tenants command/client paths for local setup.
- Do not duplicate Tenants Aspire extension logic inside Parties. Reuse `AddHexalithTenants` or add a thin wrapper only if needed for the Parties AppHost API.
- Do not accept tenant identity from party command payloads. This story should not modify command contracts.
- Do not change EventStore actor key conventions or Parties projection keys.
- Do not bypass the EventStore command API for tenant-scoped commands. The intended command path is `client -> commandapi/EventStore -> tenant access validation -> Parties module handler`.
- Do not broaden this story into Tenants event projection or REST/MCP authorization. Leave those for Stories 11.2 and 11.3, but do not block their future paths.
- If changing DAPR component scopes or access-control policies, verify both app ids: EventStore command gateway `commandapi` and Tenants service `tenants`.
- Avoid recursive submodule operations. Use the already checked-out root-level submodule content.

### Testing Requirements

- Add unit tests for any new options validation or health-check class.
- Add AppHost/resource-model tests if existing test infrastructure supports Aspire AppHost testing; otherwise add a documented manual verification step and keep the code structure easy to test in Story 11.4.
- Keep Tier 1 tests free of DAPR runtime dependencies. Health-check behavior can be tested with fake clients/services.
- Run affected tests:
  - `dotnet test tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj --configuration Release`
  - `dotnet build Hexalith.Parties.slnx --configuration Release`
- If AppHost tests are added, include the test project in `Hexalith.Parties.slnx`.

### Latest Technical Information

- Aspire AppHost resources should use explicit resource references and health-aware startup ordering. Aspire docs state that resource health checks control dependent startup through `WaitFor`, and that resource health appears in the dashboard. Use this to make Tenants topology and command API dependency visible rather than relying only on environment variables. Source: https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/health-checks
- Aspire AppHost resource names participate in service discovery and visible topology. Keep names stable (`commandapi`, `tenants`) so future configuration and docs can rely on them. Source: https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/app-host-overview
- DAPR pub/sub subscriptions can be declarative or programmatic; ASP.NET Core programmatic subscriptions use endpoint mapping such as `MapSubscribeHandler`/topic mapping. Tenants client `MapTenantEventSubscription` requires `UseCloudEvents()` and `MapSubscribeHandler()`, but that subscription work should remain Story 11.2 unless needed for health/config validation. Source: https://docs.dapr.io/developing-applications/building-blocks/pubsub/subscription-methods/
- NuGet currently lists Aspire.Hosting 13.2.2 and CommunityToolkit.Aspire.Hosting.Dapr 13.0.0 as available packages, while this repository pins Aspire 13.1.2 and CommunityToolkit.Aspire.Hosting.Dapr 13.0.0. Align versions intentionally instead of opportunistically upgrading during this story. Sources: https://www.nuget.org/packages/Aspire.Hosting/ and https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.Dapr/

### Git Intelligence

Recent relevant commits:

- `a5f2ace feat: Add Hexalith submodules for Memories, FrontComposer, and Tenants` added the root-level Tenants submodule that this story should use.
- `d1c1ace Add retrospectives for Epic 7 and Epic 8, and propose changes for Epic 3 search scope alignment` updated planning artifacts and submodules; current working tree already contains planning/status changes, so preserve unrelated edits.

### Project Structure Notes

- New AppHost references belong in `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj`.
- Tenants-specific runtime integration for CommandApi belongs under `src/Hexalith.Parties.CommandApi`, likely `Configuration/`, `HealthChecks/`, or `Extensions/` depending on the final design.
- Reusable Parties hosting abstractions belong in `src/Hexalith.Parties.Aspire`.
- Tests should stay in the nearest existing test project: CommandApi validation/health checks in `tests/Hexalith.Parties.CommandApi.Tests`, AppHost topology tests in an AppHost-focused test project if one is introduced.
- No separate UX artifact was found for this story.
- No `project-context.md` persistent fact file was found in the repository during story creation.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 11: Hexalith.Tenants Integration for Parties]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-02.md#Implementation Handoff]
- [Source: _bmad-output/planning-artifacts/architecture.md#Technology Stack]
- [Source: src/Hexalith.Parties.AppHost/Program.cs]
- [Source: src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj]
- [Source: src/Hexalith.Parties.Aspire/HexalithPartiesExtensions.cs]
- [Source: src/Hexalith.Parties.CommandApi/Program.cs]
- [Source: src/Hexalith.Parties.CommandApi/HealthChecks/PartiesHealthCheckExtensions.cs]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Aspire/HexalithTenantsExtensions.cs]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Registration/TenantServiceCollectionExtensions.cs]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants/Bootstrap/TenantBootstrapHostedService.cs]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List
