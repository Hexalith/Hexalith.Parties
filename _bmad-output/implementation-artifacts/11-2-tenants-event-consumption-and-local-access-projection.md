# Story 11.2: Tenants Event Consumption and Local Access Projection

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As Hexalith.Parties,
I want to consume Hexalith.Tenants lifecycle, membership, role, and configuration events,
so that authorization decisions can be made locally without polling.

## Acceptance Criteria

1. Given relevant Hexalith.Tenants lifecycle, membership, role, or configuration events are published, when Parties receives them through DAPR pub/sub, then Parties updates a local tenant access projection/cache through the public Tenants client pipeline.
2. Given the local tenant access projection/cache, when queried for tenant access, then it records active tenant state, user membership, roles, and tenant configuration values exposed by the Tenants projection without inventing Parties-owned tenant configuration state.
3. Given a tenant is disabled or a user is removed from a tenant, when the corresponding Tenants event is processed by Parties, then the Story 11.2 `ITenantAccessService` fail-closed decision reflects that tenant/user state; broad REST/MCP endpoint and tool enforcement remains Story 11.3.
4. Given Tenants event consumption is eventually consistent, when developer documentation is reviewed, then it explains the timing window and documents that any synchronous enforcement path is outside Story 11.2 unless an existing Tenants/EventStore authorization plugin is already enabled.

## Tasks / Subtasks

- [x] Register the Tenants client event infrastructure in Parties CommandApi (AC: 1)
  - [x] Add the required project/package reference from `src/Hexalith.Parties.CommandApi` to `Hexalith.Tenants.Client`; add `Hexalith.Tenants.Contracts` only where role/status types are directly needed.
  - [x] Call `Hexalith.Tenants.Client.Registration.AddHexalithTenants()` from `PartiesServiceCollectionExtensions.AddParties(...)` or a small dedicated extension invoked by it.
  - [x] Bind the existing `Tenants` configuration section so `PubSubName`, `TopicName`, and `CommandApiAppId` match Story 11.1 configuration. Preserve the default topic `system.tenants.events` unless planning/configuration explicitly changes it.
  - [x] Keep `Hexalith.Parties.Contracts` free of Tenants runtime dependencies.

- [x] Map the DAPR tenant event subscription endpoint (AC: 1)
  - [x] Add `app.UseCloudEvents()` before endpoint mapping and `app.MapSubscribeHandler()` before or with the subscription endpoint mapping in `src/Hexalith.Parties.CommandApi/Program.cs`.
  - [x] Call `app.MapTenantEventSubscription()` from `Hexalith.Tenants.Client.Subscription` so DAPR subscribes Parties to the configured pub/sub/topic.
  - [x] Preserve the existing middleware and endpoint order for GDPR warning, correlation, exception handling, degraded response, authentication, controllers, MCP, actor handlers, and default health endpoints.
  - [x] Verify the DAPR sidecar app id for Parties CommandApi can subscribe to the Tenants topic under `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.yaml` if topic scoping is enforced.

- [x] Provide a Parties-facing tenant access service boundary (AC: 2, 3)
  - [x] Add `ITenantAccessService` under `src/Hexalith.Parties.CommandApi/Authorization` or an equivalent CommandApi-owned namespace. This is the only service Story 11.3 should depend on for REST/MCP authorization.
  - [x] Add a concrete implementation backed by `Hexalith.Tenants.Client.Projections.ITenantProjectionStore`.
  - [x] Model access checks with explicit operation intent, for example `TenantAccessRequirement.Read`, `Write`, and `Admin`, without inventing new Tenants roles.
  - [x] Map Tenants roles as: `TenantReader` permits read/search only, `TenantContributor` permits read/write party operations but not admin, and `TenantOwner` permits read/write/admin party operations. Removed, unknown, unmapped, or missing roles grant no Parties permission. If global administrator support is needed, defer the exact policy unless Tenants client exposes it directly.
  - [x] Fail closed when the tenant state is missing, disabled, unknown, the user id is missing, the user is not a member, or the role is insufficient. Do not invent a stale-state TTL or timestamp in Parties; if the Tenants client exposes explicit freshness metadata during implementation, stale state must fail closed with a structured reason code.
  - [x] Resolve tenant and user identity from existing request, MCP, plugin context, or normalized claims inputs supplied to the service; do not add tenant id fields to Parties command contracts.
  - [x] Return a small structured result such as allowed/denied plus reason code; avoid throwing for expected authorization denials so REST/MCP can translate consistently in Story 11.3.

- [x] Ensure disabled tenants and removed users affect subsequent checks (AC: 2, 3)
  - [x] Rely on `TenantProjectionEventHandler` for `TenantDisabled`, `TenantEnabled`, `UserAddedToTenant`, `UserRemovedFromTenant`, `UserRoleChanged`, `TenantConfigurationSet`, and `TenantConfigurationRemoved` before writing custom event handlers.
  - [x] Consume the Tenants client-supported event contract only: `TenantCreated`, `TenantUpdated`, `TenantDisabled`, `TenantEnabled`, `UserAddedToTenant`, `UserRemovedFromTenant`, `UserRoleChanged`, `TenantConfigurationSet`, and `TenantConfigurationRemoved` on the configured `PubSubName`/`TopicName`. Do not invent new Tenants event names, CloudEvent types, or schema versions in Parties.
  - [x] Add only Parties-specific event handlers if a Parties-only configuration key must trigger cache invalidation or diagnostics beyond the Tenants local projection state.
  - [x] Preserve the Tenants client deduplication behavior in `TenantEventProcessor`; do not add a second message-id cache unless a durable store is explicitly required.
  - [x] Treat unknown event types as non-fatal skips and invalid payloads as processing failures, matching the Tenants client endpoint behavior.
  - [x] Use Tenants-provided message id and sequence metadata where exposed by the public client pipeline; older or duplicate events must not intentionally overwrite newer local projection state. If the current Tenants projection API cannot enforce ordering beyond its built-in `MessageId` deduplication, record that limitation in docs/tests rather than adding a separate ordering cache.

- [x] Document eventual consistency and synchronous enforcement behavior (AC: 4)
  - [x] Update the developer-facing documentation created by earlier stories, most likely `README.md` and/or `docs/`, to explain that Parties authorizes from a local Tenants projection updated by DAPR pub/sub.
  - [x] State that a just-disabled tenant or just-removed user may be accepted until the event is consumed unless a synchronous Tenants/EventStore authorization plugin is enabled on the command gateway.
  - [x] Document the fail-closed behavior when local projection state is missing, unknown, disabled, or explicitly stale according to any public Tenants freshness marker available during implementation.
  - [x] Do not claim strong immediate consistency for REST or MCP unless the synchronous enforcement path is actually implemented and configured.

- [x] Add focused tests for event projection and access decisions (AC: 1, 2, 3)
  - [x] Add unit tests in `tests/Hexalith.Parties.CommandApi.Tests` for `ITenantAccessService` allowed/denied outcomes using fake or in-memory `ITenantProjectionStore`.
  - [x] Cover active tenant with reader/contributor/owner, disabled tenant denied, removed user denied, insufficient role denied, unknown tenant denied, missing user id denied, and unknown or unmapped role denied.
  - [x] Cover the role matrix explicitly: reader allows read only; contributor allows read and write but not admin; owner allows read, write, and admin.
  - [x] Add subscription/registration tests that prove `AddHexalithTenants` registers the projection store, event processor, and handlers without requiring a running DAPR sidecar.
  - [x] Add endpoint-level tests for the subscription mapping if existing WebApplicationFactory infrastructure can assert routing, configured pub/sub/topic metadata, and CloudEvents envelope handling without a full sidecar.
  - [x] Cover duplicate message id behavior, unknown event type skips, invalid payload failure, and the limitation or behavior of event ordering based on the public Tenants client API available during implementation.
  - [x] Cover replay and restart behavior explicitly: reprocessing the same message id after an in-memory projection restart must be documented as a single-instance limitation unless a durable projection/deduplication store is implemented by configuration.
  - [x] Keep Tier 1 tests free of DAPR runtime dependencies.

- [x] Validate build and affected tests
  - [x] Run `dotnet test tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj --configuration Release`.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.
  - [x] If AppHost DAPR access-control scopes are changed, run or document an Aspire local smoke check that verifies the `commandapi` app can subscribe to `system.tenants.events`.

## Dev Notes

### Epic Context

Epic 11 makes `Hexalith.Tenants` the authority for tenant lifecycle, membership, roles, and tenant configuration while Parties continues to own party aggregates, projections, REST/MCP surfaces, and tenant-scoped data isolation. Story 11.2 is the event-driven local projection slice. Story 11.1 owns local topology and package composition; Story 11.3 owns enforcing this service across REST and MCP; Story 11.4 broadens integration tests, deployment validation, and documentation. [Source: _bmad-output/planning-artifacts/epics.md#Epic 11: Hexalith.Tenants Integration for Parties]

### Approved Change Context

The approved May 2, 2026 sprint change proposal says Parties must not treat tenant handling as a local concern. It explicitly calls for a tenant access service, Tenants event subscription, a local projection/cache for fast command-path checks, preservation of EventStore aggregate identity and projection tenant filtering, and Tenants testing helpers where useful. [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-02.md#Technical Impact]

The same proposal warns that current API/MCP flows use tenant claims directly and need validation backed by Tenants state. This story creates the shared boundary and local state so Story 11.3 can wire it into each operation without duplicating event consumption logic. [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-02.md#Implementation/documentation conflict]

### Architecture Constraints

- Target framework is `net10.0`; warnings are treated as errors. [Source: Directory.Build.props]
- Current Parties package versions are centrally managed in `Directory.Packages.props`: DAPR SDK 1.16.1, Aspire 13.1.2, CommunityToolkit.Aspire.Hosting.Dapr 13.0.0, MediatR 14.0.0, FluentValidation 12.1.1, MCP SDK 1.0.0.
- Architecture describes DAPR state store, pub/sub, actors, configuration store, and service invocation as core platform building blocks. [Source: _bmad-output/planning-artifacts/architecture.md#Technical Constraints & Dependencies]
- Multi-tenancy is split between authority and enforcement: Tenants owns lifecycle/membership/roles/configuration; EventStore and Parties enforce tenant-scoped aggregate identity, actor keys, projection filtering, pub/sub, API/MCP authorization, and log safety. [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-02.md#Architecture - Cross-Cutting Multi-Tenancy]
- The read side must fail closed for tenant access. Query-time tenant filtering and negative isolation tests remain Parties responsibilities. [Source: _bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns Identified]

### Current Code State and Files Likely Touched

`src/Hexalith.Parties.CommandApi/Program.cs`

- Current state: registers service defaults, DAPR client, Parties DAPR health checks, Parties services, Swagger in development, middleware, controllers, MCP, actor handlers, and default health endpoints.
- Story change: add CloudEvents/subscription mapping for Tenants events, likely `UseCloudEvents()`, `MapSubscribeHandler()`, and `MapTenantEventSubscription()`.
- Preserve: GDPR/correlation/error/degraded/auth middleware order, `app.MapMcp().RequireAuthorization()`, actor handler mapping, and `/health`, `/alive`, `/ready` endpoint behavior.

`src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs`

- Current state: central DI registration for ProblemDetails, authentication, EventStore server infrastructure, crypto/GDPR services, projection actors/services, search provider, FluentValidation, controllers, JSON options, and MCP.
- Story change: register Tenants client services and a Parties-owned `ITenantAccessService` boundary.
- Preserve: existing claims transformation. Story 11.2 can read user id/tenant inputs through service arguments; Story 11.3 will decide how REST/MCP extracts and passes request context.

`src/Hexalith.Parties.CommandApi/Authentication/PartiesClaimsTransformation.cs`

- Current state: normalizes tenant claims into `eventstore:tenant` from token `tenants`, `tenant_id`, or `tid`.
- Story change: avoid changing this unless tests show the access service needs a normalized user id helper. The Tenants-backed access check should not treat claim presence as sufficient authorization.
- Preserve: no tenant id from command payloads.

`src/Hexalith.Parties.CommandApi/HealthChecks/PartiesHealthCheckExtensions.cs`

- Current state: readiness is gated by `dapr-sidecar` and `dapr-statestore`; `dapr-pubsub` and `projection-actors` contribute degraded health.
- Story change: optional. If adding a local projection health signal, keep failure status explicit. Do not make Tenants event lag block readiness unless Story 11.1's health behavior requires it.

`src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.yaml`

- Current state: DAPR access control was introduced for local topology in earlier stories.
- Story change: verify topic subscription scopes if DAPR topic/app access is constrained. Include both app ids where relevant: EventStore command gateway `commandapi` and Tenants service `tenants`.
- Preserve: existing sidecar access needed by EventStore, Parties, projections, MCP, and health checks.

Documentation such as `README.md` or `docs/`

- Current state: getting-started and troubleshooting docs predate full Tenants-backed local projection behavior.
- Story change: document local projection/eventual consistency and fail-closed rules.
- Preserve: tenant lifecycle and membership management belong to `Hexalith.Tenants`; do not add Parties-local tenant management instructions.

### Hexalith.Tenants APIs to Reuse

Current Tenants submodule status observed in the prior story: `Hexalith.Tenants` is present as a root-level submodule. Do not run recursive submodule initialization/update; use the checked-out root-level submodule content. [Source: _bmad-output/implementation-artifacts/11-1-apphost-and-package-integration.md#Hexalith.Tenants APIs to Reuse]

- `Hexalith.Tenants.Client.Registration.AddHexalithTenants()` registers DAPR client, `HexalithTenantsOptions`, tenant event handlers, `TenantEventProcessor`, and an in-memory `ITenantProjectionStore` default. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Registration/TenantServiceCollectionExtensions.cs]
- `HexalithTenantsOptions` defaults are `PubSubName = "pubsub"`, `TopicName = "system.tenants.events"`, and `CommandApiAppId = "commandapi"`. Keep `CommandApiAppId = "commandapi"` aligned with the EventStore command gateway unless Story 11.1 changed the configuration deliberately. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Configuration/HexalithTenantsOptions.cs]
- `TenantEventSubscriptionEndpoints.MapTenantEventSubscription()` maps `POST /tenants/events` and attaches `WithTopic(options.PubSubName, options.TopicName)`. It requires ASP.NET Core CloudEvents and DAPR subscribe handler mapping. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Subscription/TenantEventSubscriptionEndpoints.cs]
- `TenantEventProcessor` deduplicates by `MessageId`, resolves event payload CLR types, deserializes payload JSON, dispatches to registered handlers, skips duplicate/unknown/no-handler events, and returns failure for invalid payloads. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Subscription/TenantEventProcessor.cs]
- `TenantEventEnvelope` carries `MessageId`, `TenantId`, `EventTypeName`, `SequenceNumber`, `OccurredAt`, and payload JSON; use these public metadata fields only through the Tenants client pipeline and do not create a parallel Parties envelope contract. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Subscription/TenantEventEnvelope.cs]
- `TenantProjectionEventHandler` already applies `TenantCreated`, `TenantUpdated`, `TenantDisabled`, `TenantEnabled`, `UserAddedToTenant`, `UserRemovedFromTenant`, `UserRoleChanged`, `TenantConfigurationSet`, and `TenantConfigurationRemoved` into `TenantLocalState`. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Handlers/TenantProjectionEventHandler.cs]
- `ITenantProjectionStore` exposes `GetAsync(tenantId)` and `SaveAsync(TenantLocalState)`. The default `InMemoryTenantProjectionStore` is thread-safe and clone-based, suitable for single-instance services; scaled-out production may require a durable store implementation. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Projections/ITenantProjectionStore.cs]
- `TenantLocalState` records tenant id, name, description, `TenantStatus`, members mapped by user id to `TenantRole`, and configuration key/value pairs. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Projections/TenantLocalState.cs]
- Tenants roles are `TenantOwner`, `TenantContributor`, and `TenantReader`; do not invent unqualified `Owner`, `Contributor`, or `Reader` enum values in Parties code. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Enums/TenantRole.cs]

### Implementation Guardrails

- Do not implement tenant lifecycle, membership, role, or configuration management in Parties.
- Do not duplicate Tenants event projection logic if `TenantProjectionEventHandler` already handles the event.
- Do not modify Parties command contracts to accept tenant id payload fields.
- Do not wire REST/MCP endpoint enforcement across the whole API in this story. Provide the access service and tests; Story 11.3 performs broad endpoint/tool integration.
- Tenants runtime dependencies are allowed only in CommandApi and infrastructure/test projects that need the local projection pipeline. Do not add them to `Hexalith.Parties.Contracts`, shared contract packages, or client-facing packages unless a later story explicitly requires it.
- Do not block query access on polling Tenants HTTP APIs. The point of this story is local event-fed projection state.
- Do not treat missing projection state as "allow and refresh later." Unknown tenant/user state is denied.
- Keep tenant access denial reasons stable and safe for Story 11.3 to translate later. At minimum distinguish missing tenant, missing user id, unknown tenant, disabled tenant, missing member, insufficient role, and stale/unknown state only when the public Tenants client exposes freshness state.
- Do not invent Parties-owned freshness metadata for `TenantLocalState`; if stale-state enforcement is required beyond missing/unknown projection state, defer it until the public Tenants client exposes durable freshness metadata.
- Do not change EventStore actor key formats or Parties projection keys such as `{tenant}:party-detail:{partyId}` and `{tenant}:party-index`.
- Avoid adding Tenants dependencies to `src/Hexalith.Parties.Client` unless a client-facing API contract is explicitly needed. Authorization is server-side.
- Keep logs free of personal data and avoid logging full token contents or membership lists.
- Keep event-consumption diagnostics secret- and PII-safe. Logs may include event type, tenant id, message id, sequence number, outcome category, and correlation id, but not membership dictionaries, role lists, token claims, party names, contact values, or raw invalid payloads.

### Testing Requirements

- Unit test access decisions against `TenantLocalState` through `ITenantProjectionStore`.
- Unit test role hierarchy exactly: reader allows read only; contributor allows read and write; owner allows read, write, and admin.
- Unit test deny reasons for disabled tenant, missing tenant projection, missing user id, user not in tenant, removed user after event application, and insufficient role.
- Unit test that Tenants event handler registration can process at least `TenantCreated`, `UserAddedToTenant`, `TenantDisabled`, and `UserRemovedFromTenant` into the projection store.
- Keep tests deterministic and sidecar-free. Instantiate `TenantEventProcessor` and handlers directly or through DI, and assert subscription route/topic metadata without starting a DAPR sidecar.
- Run affected CommandApi tests and the full solution build before moving the story to implementation complete.

### Latest Technical Information

- DAPR pub/sub uses CloudEvents 1.0 as its event envelope and ASP.NET Core programmatic subscriptions use endpoint topic mapping such as `MapSubscribeHandler()` and topic endpoint mapping. The Tenants client endpoint already follows that model; Story 11.2 should wire it rather than hand-rolling a parallel DAPR endpoint. Sources: https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/ and https://docs.dapr.io/developing-applications/building-blocks/pubsub/subscription-methods/
- DAPR supports retry resiliency policies and dead letter topics for pub/sub delivery failures. This story should not build a custom retry queue; use DAPR component resiliency/access-control configuration and document operational behavior. Sources: https://docs.dapr.io/operations/resiliency/policies/retries/retries-overview/ and https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-deadletter/
- Aspire resource health can gate dependent startup with `WaitFor` and displays dependency health in the dashboard. Story 11.1 owns AppHost topology; Story 11.2 should not compensate for missing AppHost health wiring inside the event handler. Source: https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/health-checks
- Use this repository's pinned package versions unless a deliberate version alignment task is created. Do not opportunistically upgrade DAPR, Aspire, or Tenants package versions while implementing the local projection.

### Previous Story Intelligence

Story 11.1 established the AppHost/package integration slice and clarified several boundaries that still apply:

- `commandapi` is the EventStore command gateway hosting/routing Parties module handlers, not the Tenants service resource.
- The Tenants Aspire resource should normally be named `tenants`.
- Tenants bootstrap only creates a global administrator when configured; it does not create a default application tenant or membership by itself.
- Tenants event subscription and local access projection are explicitly deferred to Story 11.2.
- Tenants dependencies belong in AppHost/Aspire/CommandApi/tests as needed, not in `Hexalith.Parties.Contracts`.
- Avoid recursive submodule operations. [Source: _bmad-output/implementation-artifacts/11-1-apphost-and-package-integration.md]

### Advanced Elicitation Clarifications

The 2026-05-04 advanced elicitation pass added these pre-development constraints:

- The access service should return stable, safe reason codes so Story 11.3 can translate denials consistently across REST and MCP without inspecting Tenants projection internals.
- The DAPR subscription should stay on the public Tenants client endpoint and CloudEvents model. Parties must not add a second subscription route, alternate envelope contract, or custom retry queue.
- Tests should distinguish duplicate message id handling from ordering guarantees. If only in-memory deduplication is available, replay-after-restart behavior must be documented rather than hidden behind a fake durable guarantee.
- Invalid payload diagnostics must be actionable but safe: include event type/message metadata and outcome category, not raw payloads, full claim sets, membership lists, or party PII.
- Documentation should name the operational repair path for missing or lagging Tenants projection state: verify DAPR pub/sub configuration, Tenants topic subscription, projection health, and replay/rebuild procedure where the current platform exposes one.

### Git Intelligence

Recent commits show the repository has just added the Hexalith submodules and planning updates for Tenants/Memories alignment. Keep this story narrow: add the local Tenants projection/access-service implementation path without reopening completed tenant-isolation work from earlier epics.

### Project Structure Notes

- New authorization boundary files should live under `src/Hexalith.Parties.CommandApi/Authorization/`.
- Tenants event subscription mapping belongs in `src/Hexalith.Parties.CommandApi/Program.cs` or a small CommandApi extension invoked from Program.
- Tenants client DI registration belongs in `src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs` or a dedicated extension under `Extensions/`.
- Tests should stay in `tests/Hexalith.Parties.CommandApi.Tests`, with subfolders `Authorization/` and possibly `Tenants/` or `Subscription/`.
- Documentation updates should prefer existing docs/README locations rather than creating a parallel tenant guide.
- No separate UX artifact was found for this story.
- No `project-context.md` persistent fact file was found in the repository during story creation.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 11.2: Tenants Event Consumption and Local Access Projection]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-02.md#Technical Impact]
- [Source: _bmad-output/planning-artifacts/architecture.md#Technical Constraints & Dependencies]
- [Source: _bmad-output/implementation-artifacts/11-1-apphost-and-package-integration.md]
- [Source: src/Hexalith.Parties.CommandApi/Program.cs]
- [Source: src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs]
- [Source: src/Hexalith.Parties.CommandApi/Authentication/PartiesClaimsTransformation.cs]
- [Source: src/Hexalith.Parties.CommandApi/HealthChecks/PartiesHealthCheckExtensions.cs]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Registration/TenantServiceCollectionExtensions.cs]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Subscription/TenantEventSubscriptionEndpoints.cs]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Subscription/TenantEventProcessor.cs]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Handlers/TenantProjectionEventHandler.cs]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Projections/TenantLocalState.cs]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Enums/TenantRole.cs]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-04: Red phase confirmed missing `Hexalith.Parties.CommandApi.Authorization` boundary via CommandApi test compile failure.
- 2026-05-04: Aligned `Dapr.AspNetCore` central package version to 1.17.7 because `Hexalith.Tenants.Client` requires >= 1.17.7.
- 2026-05-04: Verified `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.yaml` does not enforce DAPR pub/sub topic scoping; no AppHost access-control change was required.
- 2026-05-04: `dotnet test tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj --configuration Release --no-build` passed: 304 tests.
- 2026-05-04: `dotnet build Hexalith.Parties.slnx --configuration Release` passed with 0 warnings and 0 errors.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Party-mode review completed on 2026-05-03; story clarified before development.
- Implemented Tenants client registration and explicit `Tenants` configuration defaults in CommandApi while keeping Parties contracts free of Tenants dependencies.
- Mapped the public Tenants DAPR CloudEvents subscription endpoint through `UseCloudEvents()`, `MapSubscribeHandler()`, and `MapTenantEventSubscription()`.
- Added `ITenantAccessService` with read/write/admin requirements, stable fail-closed denial reasons, and Tenants role mapping over `ITenantProjectionStore`.
- Added sidecar-free tests for access decisions, role matrix, registration, event processing, duplicate/unknown/invalid payload behavior, subscription topic metadata, and Contracts dependency boundaries.
- Documented eventual consistency, fail-closed access behavior, operational troubleshooting, and the in-memory projection/deduplication replay limitation.

### File List

- Directory.Packages.props
- README.md
- _bmad-output/implementation-artifacts/11-2-tenants-event-consumption-and-local-access-projection.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- docs/getting-started.md
- docs/tenant-access-projection.md
- src/Hexalith.Parties.CommandApi/Authorization/ITenantAccessService.cs
- src/Hexalith.Parties.CommandApi/Authorization/TenantAccessDecision.cs
- src/Hexalith.Parties.CommandApi/Authorization/TenantAccessDenialReason.cs
- src/Hexalith.Parties.CommandApi/Authorization/TenantAccessRequirement.cs
- src/Hexalith.Parties.CommandApi/Authorization/TenantAccessService.cs
- src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs
- src/Hexalith.Parties.CommandApi/Hexalith.Parties.CommandApi.csproj
- src/Hexalith.Parties.CommandApi/Program.cs
- src/Hexalith.Parties.CommandApi/appsettings.json
- tests/Hexalith.Parties.CommandApi.Tests/Authorization/TenantAccessServiceTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/FitnessTests/ContractsArchitectureFitnessTests.cs
- tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj
- tests/Hexalith.Parties.CommandApi.Tests/Tenants/TenantEventInfrastructureTests.cs

### Change Log

- 2026-05-04: Completed Story 11.2 Tenants event consumption and local access projection; status moved to review.
- 2026-05-05: Code review complete — applied AC3 round-trip tests, replay-after-restart test, Client fitness test, sentinel-based path resolver, WebApplicationOptions hardening, doc clarifications for `CommandApiAppId`, event ordering, and cold-start behavior; documented Singleton lifetime contract on `TenantAccessService` registration. 6 deferred items recorded in `deferred-work.md`. Status moved to done.

### Party-Mode Review

- Date: 2026-05-03T11:44:29.4500623+02:00
- Selected story key: 11-2-tenants-event-consumption-and-local-access-projection
- Command/skill invocation used: `/bmad-party-mode 11-2-tenants-event-consumption-and-local-access-projection; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect), John (Product Manager)
- Findings summary:
  - Enforcement scope was ambiguous between Story 11.2 access-service behavior and Story 11.3 broad REST/MCP enforcement.
  - The Tenants event contract needed explicit event names, topic boundary, and guidance against inventing parallel Parties event schema.
  - Role mapping needed a testable read/write/admin matrix and fail-closed behavior for unknown or unmapped roles.
  - Projection freshness/staleness needed guardrails because current `TenantLocalState` does not expose durable freshness metadata.
  - Test strategy needed sidecar-free subscription, event-processing, duplicate, invalid-payload, and denial-matrix coverage.
- Changes applied:
  - Clarified acceptance criteria so Story 11.2 exposes and tests `ITenantAccessService` fail-closed decisions while Story 11.3 owns broad REST/MCP enforcement.
  - Added the supported Tenants event list and guidance to use the public Tenants client pipeline rather than inventing Parties-owned event contracts.
  - Added role matrix, identity-source, dependency-boundary, and unknown-role fail-closed guardrails.
  - Added staleness guidance that avoids inventing Parties-owned TTL/freshness metadata and requires explicit fail-closed behavior if public Tenants metadata exists.
  - Expanded deterministic, sidecar-free test requirements.
- Findings deferred:
  - Exact stale-projection TTL or freshness policy remains deferred until the public Tenants client exposes durable freshness metadata or a later architecture decision defines it.
  - Global administrator policy remains deferred unless the Tenants client exposes it directly for consumer authorization decisions.
  - Broad REST/MCP endpoint and tool enforcement remains deferred to Story 11.3.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-04T13:03:38.5991802+02:00
- Selected story key: 11-2-tenants-event-consumption-and-local-access-projection
- Command/skill invocation used: `/bmad-advanced-elicitation 11-2-tenants-event-consumption-and-local-access-projection`
- Batch 1 method names: Red Team vs Blue Team; Failure Mode Analysis; Security Audit Personas; Architecture Decision Records; Critique and Refine
- Reshuffled Batch 2 method names: Pre-mortem Analysis; Self-Consistency Validation; User Persona Focus Group; Chaos Monkey Scenarios; Expand or Contract for Audience
- Findings summary:
  - Tenant access results needed stable denial reasons so Story 11.3 can translate REST/MCP denials consistently without coupling to projection internals.
  - The DAPR/CloudEvents subscription path needed an explicit guard against parallel Parties-owned envelopes, retry queues, or duplicate routes.
  - Duplicate message handling needed to distinguish current message-id deduplication from durable replay-after-restart guarantees.
  - Diagnostics and documentation needed stronger safety rules for invalid payloads, membership data, claims, and party PII.
  - Operators needed a clearer troubleshooting path for missing or lagging Tenants projection state.
- Changes applied:
  - Added replay-after-restart test/documentation guidance for in-memory projection and deduplication limitations.
  - Added stable safe tenant-access denial reason expectations for the later REST/MCP authorization story.
  - Added logging and diagnostics guardrails for event type, message metadata, outcome category, and excluded sensitive values.
  - Added Advanced Elicitation Clarifications covering subscription boundaries, duplicate/order guarantees, safe diagnostics, and operational repair guidance.
- Findings deferred:
  - Durable projection storage and durable deduplication remain deployment/configuration decisions unless a later story explicitly adds them.
  - Exact freshness or stale-state policy remains deferred until the public Tenants client exposes durable freshness metadata.
  - Broad REST/MCP denial translation remains Story 11.3 scope.
- Final recommendation: ready-for-dev

## Review Findings

- Date: 2026-05-05
- Reviewer: BMad code-review skill (Blind Hunter + Edge Case Hunter + Acceptance Auditor)
- Diff under review: commit `324a85d` — "Implement tenants event access projection"
- Triage: 3 decision-needed / 8 patch / 6 deferred / 10 dismissed as noise

### Decision-Needed

- [x] [Review][Decision→Defer] `/tenants/events` endpoint authorization posture — resolved as: defer to Story 11-4 deployment-validation probe. Story 11-1 (blocked) owns DAPR access-control YAML; adding a Parties-side check would duplicate logic that belongs in the Tenants client. The pragmatic safety net is a deployment-validation probe in 11-4 that fails closed if the topic isn't scoped to the expected publisher app id. [Source: src/Hexalith.Parties.CommandApi/Program.cs:49]
- [x] [Review][Decision→Patch] `TenantAccessService` Singleton lifetime vs `ITenantProjectionStore` swap — resolved as: documentation. Spec explicitly accepts singleton scope for the in-memory store; added a comment at `PartiesServiceCollectionExtensions.cs:77` stating the contract that any replacement store must be Singleton. Code-level enforcement deferred until a durable store is actually wired. [Source: src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs:77]
- [x] [Review][Decision→Dismiss] `Tenants:CommandApiAppId` config value is unused at runtime — resolved as: dismissed with documentation. The value is consumed by AppHost / DAPR access-control configuration (Story 11-1), not by CommandApi at runtime. Added a documentation note in `docs/tenant-access-projection.md` clarifying the role. [Source: src/Hexalith.Parties.CommandApi/appsettings.json:11]

### Patches

- [x] [Review][Patch] Add AC3 round-trip test — `TenantDisabled` event → projection → `ITenantAccessService.CheckAccessAsync` returns `DisabledTenant`. Existing tests construct `TenantLocalState` with `Status=Disabled` directly, never threading the event through `TenantEventProcessor`. [tests/Hexalith.Parties.CommandApi.Tests/Authorization/TenantAccessServiceTests.cs]
- [x] [Review][Patch] Add AC3 round-trip test — `UserRemovedFromTenant` event → projection → `ITenantAccessService.CheckAccessAsync` returns `MissingMember`. Same gap as above for the user-removal half of AC3. [tests/Hexalith.Parties.CommandApi.Tests/Authorization/TenantAccessServiceTests.cs]
- [x] [Review][Defer] Replace bespoke `WebApplication.CreateBuilder()` subscription test with a Program.cs-composition test — deferred to Story 11-4 integration suite. The existing `AddPartiesRegistersTenantsProjectionPipelineAndAccessService` already exercises real `AddParties` composition; full `WebApplicationFactory<Program>` route assertion is a larger refactor that fits more naturally with 11-4's deployment-validation tests. Hardened CI flake risk via P9 (`WebApplicationOptions` content-root pin). [tests/Hexalith.Parties.CommandApi.Tests/Tenants/TenantEventInfrastructureTests.cs:104-128]
- [x] [Review][Patch] Add explicit replay-after-restart test — instantiate a second `TenantEventProcessor` against the same in-memory store and process the same `MessageId`; assert it is re-processed (proving the documented single-instance limitation). Spec testing requirement is currently met only by docs. [tests/Hexalith.Parties.CommandApi.Tests/Tenants/TenantEventInfrastructureTests.cs]
- [x] [Review][Patch] Add event-ordering test or doc note — spec says "Cover duplicate message id behavior, unknown event type skips, invalid payload failure, and the limitation or behavior of event ordering". Only deduplication is exercised today; an out-of-order `SequenceNumber` test (or an explicit doc statement that ordering is not enforced beyond `MessageId` dedup) is missing. [tests/Hexalith.Parties.CommandApi.Tests/Tenants/TenantEventInfrastructureTests.cs]
- [x] [Review][Patch] Add fitness test for `Hexalith.Parties.Client` Tenants boundary — current `ContractsArchitectureFitnessTests` only protects `Hexalith.Parties.Contracts.csproj`. Spec guardrail also names `Hexalith.Parties.Client` ("Authorization is server-side"). Manual inspection confirms Client is clean today; the test would prevent regression. [tests/Hexalith.Parties.CommandApi.Tests/FitnessTests/ContractsArchitectureFitnessTests.cs]
- [x] [Review][Patch] Replace `..\..\..\..\..\src\...` path traversal in fitness test with a sentinel-based repo-root resolver — five `..` segments from `AppContext.BaseDirectory` are fragile to TFM/RID/output-path changes; missing files would silently throw `FileNotFoundException` instead of producing a clear "test infrastructure broken" failure. Walk up to `Hexalith.Parties.slnx` and combine. [tests/Hexalith.Parties.CommandApi.Tests/FitnessTests/ContractsArchitectureFitnessTests.cs:30-43]
- [x] [Review][Patch] Use `WebApplicationOptions` (or `CreateEmptyBuilder`) in `TenantEventInfrastructureTests` instead of bare `WebApplication.CreateBuilder()` — current call honors `appsettings.json`, `ASPNETCORE_*`, and content root, which can flake under different CI working directories. [tests/Hexalith.Parties.CommandApi.Tests/Tenants/TenantEventInfrastructureTests.cs:104]

### Deferred

- [x] [Review][Defer] Whitespace not trimmed from `tenantId`/`userId` — values like `"user-1\r"` survive `IsNullOrWhiteSpace` and miss in lookups, returning `UnknownTenant`/`MissingMember` instead of failing fast. — deferred, normalization belongs in Story 11-3's REST/MCP request pipeline / claims transformation. [src/Hexalith.Parties.CommandApi/Authorization/TenantAccessService.cs]
- [x] [Review][Defer] Case sensitivity of `userId` lookups — `Members` dict uses `StringComparer.Ordinal`; JWT `sub` casing varies by IdP. — deferred, requires alignment with Tenants client convention; not Parties-owned. [Hexalith.Tenants/src/Hexalith.Tenants.Client/Projections/TenantLocalState.cs:26-27]
- [x] [Review][Defer] `TenantRole.TenantOwner = 0` is the default enum value — a malformed `UserAddedToTenant` payload with missing `Role` field silently grants `TenantOwner`. — deferred, Tenants client contract concern. [Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Enums/TenantRole.cs]
- [x] [Review][Defer] `TenantStatus.Active = 0` is the default enum value — a malformed event without `Status` defaults to `Active` (fail-open). — deferred, Tenants client contract concern. [Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Enums/TenantStatus.cs]
- [x] [Review][Defer] DAPR retry / dead-letter not configured for `FailedInvalidPayload` — endpoint returns 500, DAPR will redeliver indefinitely. — deferred, deployment configuration concern, naturally Story 11-4 scope. [src/Hexalith.Parties.CommandApi/appsettings.json]
- [x] [Review][Defer] Cold-start denial trap not explicit in docs — after a process restart, in-memory projection is empty so every access decision is `UnknownTenant` until events are republished. — deferred, minor doc polish on top of the existing in-memory limitation note. [docs/tenant-access-projection.md]

### Dismissed (noise / false positives, recorded for traceability)

- `Members = { ... }` collection initializer NRE risk — `Members` defaults to `[]`, so the initializer mutates a real instance.
- `AddDaprClient` registered twice — Tenants client `EnsureCoreRegistrations` checks `services.Any(s => s.ServiceType == typeof(DaprClient))` before registering.
- `Members` dictionary thread-safety — `InMemoryTenantProjectionStore` clones state on both `GetAsync` and `SaveAsync`, so reader and writer never share a `Dictionary` instance.
- In-memory store loses state on restart — explicit documented limitation per spec.
- Singleton + in-memory dedup data loss — explicit documented limitation per spec.
- `state.Status != Active` mislabels future status values — `TenantStatus` only has `Active`/`Disabled` today; speculative.
- HTTP status mapping for processor results — that mapping lives in Tenants client, not Parties.
- Public types in `Authorization/` should be `internal` — Story 11-3 consumes them; visibility is intentional.
- `_ => false` switch fallthrough — that is the fail-closed posture the spec mandates.
- `default(TenantAccessRequirement) = Read` — callers explicitly pass requirement; no model binding into this enum.


