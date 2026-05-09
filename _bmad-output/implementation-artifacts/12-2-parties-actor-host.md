# Story 12.2: Parties Actor Host

Status: ready-for-dev

## Story

As a developer,
I want `Hexalith.Parties` to become a thin actor host with no public REST or MCP surface,
so that all client traffic flows through EventStore.

## Acceptance Criteria

1. Given the `Hexalith.Parties` service project, then it contains no MVC controller endpoints, no `[ApiController]` request types, no `MapControllers` call, no in-process MCP server registration, and no `MapMcp` endpoint mapping.
2. Given `src/Hexalith.Parties/Program.cs`, then `MapActorsHandlers` and `MapDefaultEndpoints` are the only externally reachable application mappings owned by the Parties actor host; any retained DAPR/Tenants subscription route must be justified as sidecar-internal and protected by DAPR configuration.
3. Given the Parties actor host sidecar, then AppHost registers it with DAPR `appId="parties"` and a Configuration CRD scoped to EventStore-to-Parties actor/domain invocation only.
4. Given the FR62 GDPR notice demotion, then the notice is logged once at `Warning` level during startup and no per-response GDPR warning header is added.
5. Given architectural fitness coverage, then `ArchitecturalFitnessTests` fails if `Hexalith.Parties` contains `[ApiController]` types, controller base types, in-process MCP tool attributes, `AddMcpServer`, `WithToolsFromAssembly`, `MapMcp`, or `MapControllers`.
6. Given existing controller and MCP tests, then this story removes or retires tests that assert the old Parties REST/MCP surface and does not replace them with EventStore gateway coverage; Story 12.4 and Story 12.6 own those replacements.

## Tasks / Subtasks

- [ ] Confirm the Epic 12 gate before implementation. (AC: 1-6)
  - [ ] Read `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md`.
  - [ ] If `## Spike Conclusion` is still pending or negative, stop through the normal dev workflow and do not remove public surfaces yet.
  - [ ] Confirm Story 12.1 has established the recomposed AppHost and dedicated Parties DAPR configuration, or keep AppHost changes limited to the sidecar/app-id checks required here.
- [ ] Remove the public REST surface from `Hexalith.Parties`. (AC: 1, 2, 4)
  - [ ] Remove `app.MapControllers()` from `src/Hexalith.Parties/Program.cs`.
  - [ ] Remove or exclude `src/Hexalith.Parties/Controllers/PartiesController.cs` and `src/Hexalith.Parties/Controllers/AdminController.cs` from the service project.
  - [ ] Remove controller-only service wiring from `AddParties`, including `AddControllers`, OpenAPI/Swagger setup, controller ProblemDetails assumptions that are no longer used by the actor host, and controller-specific request-path authorization helpers when they become unreachable.
  - [ ] Preserve exception handling, correlation, authentication/authorization, CloudEvents, actor hosting, projection, crypto, Tenants projection, and health behavior needed by the actor host.
- [ ] Remove the in-process MCP surface from `Hexalith.Parties`. (AC: 1, 5, 6)
  - [ ] Remove `app.MapMcp().RequireAuthorization()` from `Program.cs`.
  - [ ] Remove `AddMcpServer().WithHttpTransport(...).WithToolsFromAssembly()` from `PartiesServiceCollectionExtensions`.
  - [ ] Remove the `ModelContextProtocol.AspNetCore` package reference from `src/Hexalith.Parties/Hexalith.Parties.csproj`.
  - [ ] Remove, exclude, or quarantine `src/Hexalith.Parties/Mcp/**` so `Hexalith.Parties` has zero `[McpServerToolType]` and `[McpServerTool]` registrations. Do not create `Hexalith.Parties.Mcp`; Story 12.6 owns the new thin MCP host.
- [ ] Demote the GDPR notice to startup logging only. (AC: 4)
  - [ ] Keep the existing startup `LogWarning` notice in `Program.cs`, or move it to an actor-host startup service if that improves testability.
  - [ ] Remove `GdprWarningMiddleware` from the request pipeline and delete the middleware if no other code uses it.
  - [ ] Update or retire tests that expect `X-GDPR-Warning` response headers; future EventStore gateway responses must not inherit a Parties per-response header.
- [ ] Keep the actor host runnable and narrow. (AC: 2, 3)
  - [ ] Keep `app.MapActorsHandlers()` and actor registrations for `PartyDetailProjectionActor`, `PartyIndexProjectionActor`, and `PartyKeyRetryActor`.
  - [ ] Keep `app.MapDefaultEndpoints()` for `/health`, `/alive`, and `/ready`.
  - [ ] Reassess `app.MapSubscribeHandler()` and `app.MapTenantEventSubscription()`: if still required for Tenants projection events, document them as DAPR sidecar-internal and ensure DAPR access control does not expose them as client APIs.
  - [ ] Ensure AppHost or Aspire code still assigns the Parties sidecar app ID as `parties`; do not change the domain string or app-id casing in this story unless Story 12.0 explicitly requires it.
- [ ] Harden architectural fitness tests. (AC: 1, 2, 5)
  - [ ] Extend `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs` to inspect the Parties assembly and source text for forbidden controller/MCP surface markers.
  - [ ] Add source checks for `Program.cs` so `MapControllers`, `MapMcp`, `MapOpenApi`, and `UseSwaggerUI` cannot reappear in the actor host.
  - [ ] Add project checks so `Hexalith.Parties.csproj` no longer references `ModelContextProtocol.AspNetCore` or Swagger/OpenAPI packages unless a remaining non-public use is explicitly justified.
  - [ ] Update any existing fitness tests that read `Controllers/PartiesController.cs`; after this story, projection actor key-format checks must read the actor/projection code that actually owns those keys.
- [ ] Retire obsolete REST/MCP tests without masking required future coverage. (AC: 6)
  - [ ] Remove or skip controller integration tests that hit `GET/POST /api/v1/parties` or `/api/v1/admin`.
  - [ ] Remove or quarantine in-process MCP tool tests that directly reference `Hexalith.Parties.Mcp`; Story 12.6 must recreate coverage against `Hexalith.Parties.Mcp`.
  - [ ] Do not rewrite broad server integration coverage through EventStore in this story; Story 12.4 owns that gateway test rewrite.
- [ ] Verify the narrow actor-host result. (AC: 1-6)
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter ArchitecturalFitnessTests`.
  - [ ] Run `dotnet build Hexalith.Parties.slnx`.
  - [ ] If AppHost is already recomposed by Story 12.1 and local DAPR is available, run `dotnet aspire run --project src/Hexalith.Parties.AppHost` long enough to confirm the `parties` resource starts and exposes health while public REST/MCP endpoints are gone.

## Dev Notes

### Source Context

- Epic 12 is sourced from `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md`; that proposal is the authoritative source for Story 12.2.
- The pivot decision is that EventStore is the command/query gateway. Parties becomes a domain actor host plus projection runtime and stops exposing its own REST and in-process MCP endpoints.
- Story 12.0 is still the gate in the current artifact: `## Spike Conclusion` says `Pending development`. This story is ready as implementation context, but the dev agent must not execute it until the spike has a positive conclusion and exact domain/app-id guidance.
- Story 12.1 is the immediate predecessor and defines the recomposed AppHost target: resources `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, and `tenants`, with dedicated DAPR access-control files. This story should build on that shape, not recreate it.

### Current Implementation to Inspect

- `src/Hexalith.Parties/Program.cs` currently logs the GDPR warning, maps OpenAPI in development, installs `GdprWarningMiddleware`, maps controllers, maps tenant subscription routes, maps MCP, maps actor handlers, and maps default endpoints. The target actor host must remove public REST/MCP/OpenAPI mappings while preserving actor and health mappings.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` currently registers controllers, OpenAPI, MCP server transport/tool discovery, authentication/authorization, EventStore server infrastructure, Tenants integration, projections, crypto-shredding, search, validators, and actors. Remove only the public surface wiring and package dependencies; do not strip actor-host infrastructure needed by EventStore invocation.
- `src/Hexalith.Parties/Controllers/PartiesController.cs` and `src/Hexalith.Parties/Controllers/AdminController.cs` are the old public REST/admin surfaces. They contain tenant access checks, projection reads, command router calls, key management, and GDPR operations. Do not silently move their public API behavior into another Parties endpoint; future gateway/admin stories own replacement surfaces.
- `src/Hexalith.Parties/Mcp/**` contains the in-process MCP tool registrations and session context. Removing it from `Hexalith.Parties` is required, but preserving the tool contract names belongs to Story 12.6 in a new `Hexalith.Parties.Mcp` host.
- `src/Hexalith.Parties/Middleware/GdprWarningMiddleware.cs` adds `X-GDPR-Warning` to every response. FR62 is now startup-log-only, so this middleware should be removed from the actor host request path.
- `src/Hexalith.Parties.AppHost/Program.cs` in the current workspace still passes one `accesscontrol.yaml` path into Parties/Tenants composition. Story 12.1 owns the full split; this story should only assert that the Parties sidecar has app ID `parties` and actor-invocation-scoped access control once 12.1 lands.
- `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs` currently has MCP boundary tests that assume MCP types exist in the Parties assembly and a projection key-format test that reads `Controllers/PartiesController.cs`. These tests must be redesigned for the actor-host world.
- `tests/Hexalith.Parties.IntegrationTests/PartyApiRoundTripIntegrationTests.cs` currently expects Parties REST endpoints and the GDPR response header. That coverage is obsolete after this story and should not be treated as a failure to preserve old behavior.

### Technical Constraints

- Keep package versions aligned with the repository: .NET SDK `10.0.103`, `net10.0`, Aspire `13.2.2`, CommunityToolkit Aspire DAPR `13.0.0`, `Dapr.Client`/`Dapr.AspNetCore` `1.17.7`, and `Dapr.Actors` `1.16.1`.
- Do not initialize or update nested submodules. `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.FrontComposer`, and `Hexalith.Memories` are root-level submodules already present in the workspace.
- Do not edit the `Hexalith.EventStore` submodule for this story. If the actor-host cleanup exposes an EventStore platform gap, stop and record the blocker.
- Do not remove Parties aggregate, validators, projection actors, projection handlers, crypto-shredding services, key lifecycle services, or Tenants projection infrastructure merely because controllers used them.
- Do not implement the EventStore gateway test rewrite here. Story 12.4 owns Tier-1/Tier-2 server test conversion to `POST /api/v1/commands` and `POST /api/v1/queries`.
- Do not implement the new MCP host here. Story 12.6 owns `src/Hexalith.Parties.Mcp` and the preserved tool contracts.
- DAPR .NET actor hosting still uses `AddActors(...)` plus `MapActorsHandlers()` in ASP.NET Core endpoint routing, and DAPR service invocation access control is applied on the called application's sidecar through the Configuration schema. Use the sidecar/app-id policy rather than application-level public controllers for EventStore-to-Parties invocation.

### Architecture and Security Guidance

- EventStore owns public command/query ingress, gateway authentication, tenant validation, RBAC, and generic response mapping after the pivot.
- Parties owns domain execution and projection runtime behind DAPR. It should be callable by EventStore through the route proven by Story 12.0, not by end-user clients directly.
- DAPR access-control policy is evaluated by the receiving sidecar. The Parties sidecar should allow the EventStore caller and the exact operation path required for actor/domain invocation; avoid wildcard caller sets and broad method access.
- `MapDefaultEndpoints` can remain public for health and readiness, but health output must not leak personal data, tenant-sensitive details, signing keys, tokens, or operator-supplied secrets.
- Startup GDPR warning logging must be idempotent and non-sensitive. Removing the response header is intentional and should not be reintroduced by compatibility tests.
- Treat Tenants subscription endpoints carefully: if `MapTenantEventSubscription` remains in the actor host, ensure it is classified as internal DAPR integration and not as a public Parties API.

### Testing Guidance

- Minimum focused tests:
  - `ArchitecturalFitnessTests` asserts zero `[ApiController]` types in the Parties assembly.
  - `ArchitecturalFitnessTests` asserts no type in `Hexalith.Parties` has `McpServerToolTypeAttribute` or `McpServerToolAttribute`.
  - Source-text fitness checks assert `Program.cs` has no `MapControllers`, `MapMcp`, `MapOpenApi`, `UseSwaggerUI`, or `GdprWarningMiddleware`.
  - Project-file checks assert `Hexalith.Parties.csproj` no longer references `ModelContextProtocol.AspNetCore`, `Microsoft.AspNetCore.OpenApi`, or `Swashbuckle.AspNetCore.SwaggerUI` unless an explicit retained non-public reason is documented.
  - AppHost/Aspire checks assert the Parties DAPR app ID remains `parties` and uses the dedicated Parties access-control configuration once Story 12.1 is in place.
- Run at least:
  - `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter ArchitecturalFitnessTests`
  - `dotnet build Hexalith.Parties.slnx`
- Expect old controller and MCP tests to fail until removed or quarantined. Do not weaken new fitness tests to keep obsolete public-surface tests green.

### Out of Scope

- Creating `src/Hexalith.Parties.Mcp` or preserving MCP tool behavior in a new host.
- Rewriting controller integration tests through EventStore gateway.
- Moving validators or tenant authorization ownership into EventStore.
- Rebuilding the Admin Portal, Picker, sample app, README, or getting-started guide.
- Changing production deployment manifests beyond the Parties DAPR sidecar/configuration checks directly required by this actor-host cleanup.
- Changing EventStore submodule code.

### References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md` - Story 12.2 ACs, Epic 12 pivot rationale, sequencing, and out-of-scope boundaries.
- `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md` - required gate and exact domain/app-id conclusions before implementation.
- `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md` - predecessor AppHost topology and DAPR configuration context.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` - Epic 12 order and story state.
- `src/Hexalith.Parties/Program.cs` - current public mappings and actor/default endpoint mappings.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` - current REST/MCP/controller/service registrations.
- `src/Hexalith.Parties/Controllers/PartiesController.cs` - old public Parties REST API surface to remove.
- `src/Hexalith.Parties/Controllers/AdminController.cs` - old public admin REST API surface to remove.
- `src/Hexalith.Parties/Mcp/**` - old in-process MCP tool surface to remove from the service project.
- `src/Hexalith.Parties/Middleware/GdprWarningMiddleware.cs` - old per-response GDPR header middleware.
- `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs` - primary fitness-test home for actor-host boundaries.
- `tests/Hexalith.Parties.IntegrationTests/PartyApiRoundTripIntegrationTests.cs` - obsolete REST/header expectations to retire.
- DAPR documentation v1.17, `.NET actors` and `service invocation access control` - confirms `AddActors(...)`/`MapActorsHandlers()` hosting and sidecar-applied Configuration allow lists.

## Project Structure Notes

- Keep actor-host cleanup scoped to `src/Hexalith.Parties` and narrow AppHost/Aspire verification only where needed for the Parties sidecar app ID/configuration.
- Keep domain code in `src/Hexalith.Parties.Server`, projections in `src/Hexalith.Parties.Projections`, contracts in `src/Hexalith.Parties.Contracts`, and tests in existing test projects.
- If old MCP code must be preserved temporarily for Story 12.6 migration, quarantine it outside the `Hexalith.Parties` service assembly or explicitly exclude it from compilation; do not leave in-process MCP registrations in the actor host.
- Generated `bin/` and `obj/` outputs must stay out of commits.

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

TBD

### Completion Notes List

TBD

### File List

TBD

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-09 | 0.1 | Created ready-for-dev story through BMAD pre-dev hardening automation. | Codex |
