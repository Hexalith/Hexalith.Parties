# Story 11.1: AppHost and Package Integration

Status: done

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

- [x] Compose Hexalith.Tenants into the Parties AppHost topology (AC: 1)
  - [x] Add project references needed by `src/Hexalith.Parties.AppHost` to use `Hexalith.Tenants.Aspire` and the Tenants service project.
  - [x] Reuse `Hexalith.Tenants.Aspire.HexalithTenantsExtensions.AddHexalithTenants`; do not recreate Tenants state store/pubsub wiring by hand unless the Tenants Aspire API is unusable.
  - [x] Ensure the Tenants resource appears with a stable Aspire resource name, preferably `tenants`, and the existing `commandapi` resource keeps its current DAPR app id as the EventStore command gateway hosting Parties module handlers.
  - [x] Wire the `commandapi` EventStore command gateway to the Tenants resource with `WithReference(...)` and `WaitFor(...)` or an equivalent health-gated dependency, without breaking the existing EventStore topology created by `AddHexalithParties`.
  - [x] Keep the local access-control path resolution intact; if touched, prefer the Tenants AppHost `ResolveDaprConfigPath(builder.AppHostDirectory, ...)` pattern because it works under both `dotnet run` and Aspire testing.

- [x] Align local development bootstrap/configuration (AC: 2, 3)
  - [x] Add explicit configuration for Tenants bootstrap/user setup in local development, using Tenants-supported settings such as `Tenants:BootstrapGlobalAdminUserId` where applicable.
  - [x] Document or wire the path for creating a default active tenant and assigning a sample/test user with a role that permits party commands. The current Tenants bootstrap service only bootstraps a global administrator; do not claim it creates a tenant/member unless the code is extended or a documented command path is provided.
  - [x] Add a Parties-side tenant integration configuration section, for example `Tenants:Enabled`, `Tenants:ServiceName`, `Tenants:CommandApiAppId`, `Tenants:PubSubName`, and `Tenants:TopicName`, matching the actual options consumed by the code. Keep `Tenants:CommandApiAppId` pointed at `commandapi` when it identifies the EventStore command gateway.
  - [x] Fail fast at startup only when tenant authorization is enabled and required Tenants settings are missing; keep existing non-Tenants local smoke paths usable when the feature is explicitly disabled.

- [x] Surface Tenants reachability through health/readiness (AC: 4)
  - [x] Add a health check that verifies Tenants integration reachability/configuration using the concrete integration path selected for this story.
  - [x] Decide and document whether Tenants unreachability is `Unhealthy` for `/ready` when tenant authorization is enabled, or `Degraded` when the feature is disabled or operating in an explicitly documented degraded mode.
  - [x] Preserve the existing DAPR readiness behavior: `dapr-sidecar` and `dapr-statestore` remain readiness-gating checks, while pub/sub and projection actor health currently contribute to `/health` as degraded dependencies.

- [x] Add focused tests and validation (AC: 1, 3, 4)
  - [x] Add AppHost or configuration tests that prove the Tenants project/reference is present and the `commandapi` EventStore command gateway dependency is wired.
  - [x] Add startup/options validation tests for missing Tenants configuration when tenant authorization is enabled.
  - [x] Add health-check tests for Tenants reachable/unreachable behavior, using fakes where possible instead of requiring a full DAPR/Tenants runtime.
  - [x] Run at minimum `dotnet build Hexalith.Parties.slnx --configuration Release` and the affected test projects.

- [x] Keep package and dependency boundaries clean (AC: 1)
  - [x] Do not add Tenants dependencies to `src/Hexalith.Parties.Contracts`; that package must remain free of Tenants runtime references.
  - [x] Add Tenants references only where needed: AppHost/Aspire for topology, CommandApi for client/options/health if required, and tests for test helpers.
  - [x] Do not initialize or update nested submodules recursively. The root-level `Hexalith.Tenants` submodule already exists for this work.

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

GPT-5

### Debug Log References

- `dotnet test tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj --configuration Release --filter "FullyQualifiedName~TenantIntegrationOptionsValidatorTests|FullyQualifiedName~TenantsIntegrationHealthCheckTests|FullyQualifiedName~AppHostTenantsTopologyTests"` — passed, 10/10.
- `dotnet test tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj --configuration Release --filter "FullyQualifiedName~HealthEndpointIntegrationTests"` — passed, 12/12.
- `dotnet build src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --configuration Release` — passed.
- `dotnet build Hexalith.Parties.slnx --configuration Release` — passed after clearing stale sample `bin/obj` artifacts.
- `dotnet test tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj --configuration Release --filter "FullyQualifiedName!~SemanticSearchPerformanceBenchmarkTests"` — passed, 396/396.
- Full `Hexalith.Parties.CommandApi.Tests` run: 399/400 passed; `SemanticSearchPerformanceBenchmarkTests.Search_100KEntries_ExactMatch_CompletesWithin500ms` missed the 500ms threshold in full-suite runs (509-514ms) and passed when rerun alone once. No Tenants/AppHost functional failures remained.

### Review Round

#### Party-Mode Review - 2026-05-03T09:39:25.7734981+02:00

- Selected story key: `11-1-apphost-and-package-integration`
- Command/skill invocation used: `/bmad-party-mode 11-1-apphost-and-package-integration; review;`
- Participating BMAD agents: Winston (System Architect), John (Product Manager), Amelia (Senior Software Engineer), Murat (Test Architect)
- Findings summary:
  - AC2 leaves too much implementation discretion by saying the default tenant and sample user may be "seeded or documented"; the story needs one explicit Tenants-supported path and must not imply Parties can fake tenant membership locally.
  - AC3 needs a precise tenant-authorization configuration gate, including the exact enabled flag, required option keys, validation location, and proof that disabled tenant authorization does not require Tenants configuration.
  - AC4 needs an observable health contract for Tenants unreachability, including endpoint, status semantics, timeout expectations, and liveness/readiness behavior.
  - AppHost topology guidance should make `tenants`, `commandapi`, and `eventstore` app-id/resource boundaries explicit so development does not replace the EventStore command gateway or overbuild later Story 11.2/11.3 responsibilities.
  - Test guidance should name conditional options validation, fakeable Tenants health behavior, topology/resource-name verification, and a Contracts dependency guard.
- Changes applied:
  - Recorded this canonical party-mode review trace.
  - Marked the story `blocked` because the review surfaced product/architecture decisions that should not be resolved silently during party-mode review.
- Findings deferred:
  - Decide whether AC2 is fulfilled by documented manual bootstrap or executable Tenants-supported bootstrap.
  - Decide the exact sample/test user identity and minimum Tenants role for party commands.
  - Define the precise Tenants health/readiness contract for unreachable Tenants service.
  - Define the exact `Tenants:*` option keys and startup validation location.
  - Confirm whether `WaitFor(tenants)` is required for `commandapi` startup or only for AppHost topology visibility.
- Final recommendation: `blocked`

### Local Development Bootstrap (AC 2)

The Tenants bootstrap service (`Hexalith.Tenants` `BootstrapHostedService`) only ensures a global administrator user exists — it does not create an application tenant or assign tenant membership. AC 2 is therefore satisfied by **documented manual setup**, not automatic seeding. To prepare a local environment in which a sample/test user can issue Parties commands:

1. Configure the bootstrap admin user id (Aspire AppHost forwards `Tenants:BootstrapGlobalAdminUserId` to the `tenants` project as `Tenants__BootstrapGlobalAdminUserId`):
   - **Default fixture**: `appsettings.Development.json` ships `Tenants:BootstrapGlobalAdminUserId="tenant-a-user"` so `dotnet run --project src/Hexalith.Parties.AppHost` works out of the box for the canonical sample identity.
   - **Override**: set the environment variable `Tenants__BootstrapGlobalAdminUserId=<your-user-id>` (or use user-secrets on the AppHost project) to bootstrap a different admin.
2. Start the topology: `dotnet run --project src/Hexalith.Parties.AppHost`. The `tenants` resource starts and the global admin user is provisioned automatically.
3. Create a default tenant and assign the sample user a Parties-permitting role using the Tenants Command API (`commandapi` DAPR app id, exposed by the EventStore command gateway). Issue the canonical Tenants commands as that admin:
   - `CreateTenant` to provision the tenant aggregate.
   - `AddUserToTenant` (or the Tenants role-assignment command) to attach the sample user with a role whose policy permits `parties.commands.*`.
4. The sample user can now authenticate with a JWT carrying the corresponding tenant claim and issue Parties commands.

This procedure must run once per fresh local environment. Story 11.2 onward consumes the resulting tenant state through the local access projection.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Composed Hexalith.Tenants into the Parties AppHost with stable `tenants` resource name, reused `AddHexalithTenants`, shared the Tenants DAPR state/pubsub resources with Parties, and added `WithReference(...).WaitFor(...)` from `commandapi` to the Tenants project resource.
- Added Parties-side `Tenants` integration options with `Tenants:Enabled`, `Tenants:ServiceName`, `Tenants:CommandApiAppId`, `Tenants:PubSubName`, and `Tenants:TopicName`; startup validation now fails fast only when Tenants integration is enabled and required settings are missing.
- Added Tenants readiness health behavior: when enabled, `tenants-integration` is readiness-gating and reports `Unhealthy` if the Tenants service cannot be reached through DAPR service invocation; when disabled, the check returns healthy so local smoke paths can opt out explicitly.
- Kept `Hexalith.Parties.Contracts` free of Tenants references and did not initialize/update nested submodules.

### File List
- src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj
- src/Hexalith.Parties.AppHost/Program.cs
- src/Hexalith.Parties.Aspire/HexalithPartiesExtensions.cs
- src/Hexalith.Parties.CommandApi/Configuration/TenantIntegrationOptions.cs
- src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs
- src/Hexalith.Parties.CommandApi/HealthChecks/PartiesHealthCheckExtensions.cs
- src/Hexalith.Parties.CommandApi/HealthChecks/TenantsIntegrationHealthCheck.cs
- src/Hexalith.Parties.CommandApi/appsettings.Development.json
- src/Hexalith.Parties.CommandApi/appsettings.json
- tests/Hexalith.Parties.CommandApi.Tests/Configuration/TenantIntegrationOptionsValidatorTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/FitnessTests/AppHostTenantsTopologyTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/HealthChecks/HealthEndpointIntegrationTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/HealthChecks/TenantsIntegrationHealthCheckTests.cs
- _bmad-output/implementation-artifacts/11-1-apphost-and-package-integration.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

### Change Log

- 2026-05-07: Implemented AppHost Tenants composition, startup options validation, Tenants readiness health check, and focused test coverage; moved story to review.
- 2026-05-07: Code review applied 7 patches (3 from resolved decisions + 4 direct): base `Tenants:Enabled=false` opt-in default; shared state-store `keyPrefix=none` reapplied in 5-arg `AddHexalithParties`; bounded `HttpClient` timeout via named client; cooperative cancellation rethrow in Tenants health check; AC2 manual-bootstrap docs; switchable tenants probe + `/ready=503` integration test. 5 items deferred to `deferred-work.md`. Story moved to done.

### Review Findings

Adversarial review on 2026-05-07 (Blind Hunter + Edge Case Hunter + Acceptance Auditor). Raw findings: 35; after dedup + dismiss: 12.

- [x] [Review][Decision] Hardcoded `BootstrapGlobalAdminUserId="tenant-a-user"` in committed Development settings — Resolved: kept as documented dev fixture; `Tenants__BootstrapGlobalAdminUserId` env var or user-secrets remain the override path. Documented in the new "Local Development Bootstrap (AC 2)" section.
- [x] [Review][Decision] `Tenants:Enabled=true` default in committed `appsettings.json` — Resolved (option a): base `appsettings.json` now ships `Enabled=false`. Development.json + AppHost env vars enable it explicitly so non-Tenants smoke paths remain usable.
- [x] [Review][Decision] Shared Tenants state-store missing `keyPrefix=none` metadata — Resolved (option b): the 5-arg `AddHexalithParties` overload now reapplies `WithMetadata("keyPrefix", "none")` on the shared state store before wiring the sidecar, preserving the existing EventStore actor key format without touching the Tenants submodule.
- [x] [Review][Patch] HttpClient lacks bounded request timeout in DaprTenantsReadinessProbe — Fixed: named client `tenants-readiness-probe` registered with `Timeout = TimeSpan.FromSeconds(2)`; probe uses `CreateClient(HttpClientName)`. [src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs, HealthChecks/TenantsIntegrationHealthCheck.cs]
- [x] [Review][Patch] Catch swallows `OperationCanceledException` and reports it as a check failure — Fixed: added `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }` so graceful shutdown does not flap `/ready`. [src/Hexalith.Parties.CommandApi/HealthChecks/TenantsIntegrationHealthCheck.cs]
- [x] [Review][Patch] AC2 default-tenant creation path is neither seeded nor documented — Fixed: added "Local Development Bootstrap (AC 2)" section to this story documenting the manual `CreateTenant` + role-assignment command path operators must run once per fresh local environment.
- [x] [Review][Patch] No `/ready=503` integration test for unavailable Tenants probe — Fixed: replaced fixture-level `HealthyTenantsReadinessProbe` with `SwitchableTenantsReadinessProbe`; added `ReadyEndpoint_TenantsIntegrationUnreachable_Returns503Async` asserting both the 503 response and the `tenants-integration` check status in the JSON payload. [tests/Hexalith.Parties.CommandApi.Tests/HealthChecks/HealthEndpointIntegrationTests.cs]
- [x] [Review][Defer] AppHost env-vars duplicate `appsettings.json` Tenants values for `commandapi` [src/Hexalith.Parties.AppHost/Program.cs:48-52] — env-var precedence makes JSON values dead under AppHost; future maintenance trap. Low priority; pre-existing pattern in the host.
- [x] [Review][Defer] `ValidateOnStart` eagerness not asserted by any test [src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs:79-82] — likely fires correctly under modern host, but no failing-startup test exists; if validation actually fires lazily, missing config produces a 500 from `/ready` rather than a startup crash. Add a startup-fail integration test in a hardening pass.
- [x] [Review][Defer] `WaitFor(tenantsResources.CommandApi)` waits on the project resource, not the Tenants DAPR sidecar [src/Hexalith.Parties.AppHost/Program.cs:45-46] — first N readiness probes after startup may race the sidecar app-id resolution, briefly returning `/ready=503`. Pre-existing Aspire pattern across sibling stories.
- [x] [Review][Defer] `Uri.EscapeDataString(serviceName)` doesn't validate service-name format [src/Hexalith.Parties.CommandApi/HealthChecks/TenantsIntegrationHealthCheck.cs:25-26] — typos like `tenants/` or `tenants:dev` percent-encode and surface as opaque "not ready"; low impact.
- [x] [Review][Defer] Test fake `HealthyTenantsReadinessProbe` masks any future regression where `ITenantsReadinessProbe` is dropped from production DI [tests/Hexalith.Parties.CommandApi.Tests/HealthChecks/HealthEndpointIntegrationTests.cs:336-348] — `RemoveAll().Add()` is a no-op when the production registration is missing. Add a guard test asserting the production registration exists in a hardening pass.

Dismissed (12): `BootstrapGlobalAdminUserId` not in `TenantIntegrationOptions` (belongs to Tenants-side options); DAPR `/method/ready` invocation routes correctly to `Hexalith.Tenants.ServiceDefaults.MapDefaultEndpoints` `/ready`; `_configuration["DAPR_HTTP_PORT"]` resolves through .NET host's default env-var provider; `..\..\Hexalith.Tenants` ProjectReferences are the project's git-submodule layout; topology cycle via `WithReference(tenantsResources.CommandApi)` did not surface in completed sibling stories using the same pattern; `internal interface ITenantsReadinessProbe` is reachable from tests via `InternalsVisibleTo` in `Hexalith.Parties.CommandApi.csproj:9`; both 3-arg and 5-arg `AddHexalithParties` overloads coexist (no breaking change); `IsSuccessStatusCode` accepting 204 is not a credible regression vector for DAPR `/ready`; `CommandApiAppId` is consumed by the `HexalithTenants` client (separate options class registered in same DI scope); two options classes binding the same `Tenants` section is by design; `RepositoryRoot.Locate()` is the standard fitness test pattern; `WithEnvironment` ordering after `AddHexalithTenants` is fine in the current Aspire toolkit version (tests pass).
