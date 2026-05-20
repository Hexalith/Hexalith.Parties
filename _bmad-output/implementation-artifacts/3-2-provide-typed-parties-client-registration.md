# Story 3.2: Provide Typed Parties Client Registration

Status: ready-for-dev

## Story

As a .NET developer,
I want to register a typed Parties client with one DI call,
so that I can send commands and queries without learning Dapr or service internals.

## Acceptance Criteria

1. Given a consuming .NET application references `Hexalith.Parties.Client`, when the developer calls the documented `AddPartiesClient()` registration, then `IPartiesCommandClient` and `IPartiesQueryClient` are registered in dependency injection, the registration returns the original `IServiceCollection` for fluent chaining, and the consuming app does not need to reference Dapr, MediatR, FluentValidation, Server, Projections, or the Parties actor-host project.
2. Given the typed command client is resolved from DI, when the developer sends create, update, contact-channel, identifier, deactivate, reactivate, composite, or natural-person commands, then the client submits EventStore-fronted command requests with `Domain="party"`, the configured tenant, the correct aggregate id, the concrete Parties command type, and typed success or rejection/error results.
3. Given the typed query client is resolved from DI, when the developer queries by id, lists, filters, or searches parties, then the client submits EventStore-fronted query requests through the configured gateway boundary and returns typed `PartyDetail`, `PagedResult<PartyIndexEntry>`, or `PagedResult<PartySearchResult>` results, preserving additive freshness/degradation metadata where current contracts expose it.
4. Given required client configuration is missing or malformed, when the client is registered or used, then base URL and tenant configuration fail closed with clear developer-facing messages, unauthorized/forbidden gateway responses map to `PartiesClientException`, and no request is sent with incomplete local configuration.
5. Given client package dependency tests run, when package dependencies and packed output are inspected, then the Client package stays under the NFR31 target of fewer than 10 transitive dependencies totaling under 5 MB where measurable, and forbidden service infrastructure dependencies fail tests.

## Acceptance Evidence

| AC | Evidence to provide |
| --- | --- |
| 1 | DI tests prove `AddPartiesClient(IConfiguration)` registers `IPartiesCommandClient` as `HttpPartiesCommandClient`, `IPartiesQueryClient` as `HttpPartiesQueryClient`, binds `PartiesClientOptions`, validates `Parties:BaseUrl` and `Parties:Tenant`, and returns the same `IServiceCollection`. Package/reference fitness tests prove consumers do not inherit Dapr, MediatR, FluentValidation, Server, Projections, or `Hexalith.Parties` actor-host references. |
| 2 | Command-client tests prove every public command method posts to `api/v1/commands`, uses `Domain="party"`, emits the configured tenant, uses the route `partyId` as authoritative for update/contact/identifier/composite methods, preserves concrete command type names, returns correlation ids, deserializes optional `resultPayload`, and maps gateway problem details without exposing protected payloads. |
| 3 | Query-client tests prove `GetPartyAsync`, `ListPartiesAsync`, and `SearchPartiesAsync` post to `api/v1/queries`, use accepted query/projection metadata, serialize filters with camelCase, string enums, ISO 8601 dates, and null omission, and deserialize typed result payloads. If freshness/degradation metadata is not yet represented by a typed public model, the evidence must state that the story remains metadata-additive and does not invent a parallel response shape. |
| 4 | Negative tests prove missing `Parties:BaseUrl`, relative base URL, missing `Parties:Tenant`, manually constructed clients without tenant, malformed success payloads, cancellation, 400/401/403/404/409/503 problem details, and sensitive detail strings all fail predictably and privacy-safely. |
| 5 | Packed `.nupkg`/`.nuspec` or assets inspection proves only approved dependencies are present. `Hexalith.EventStore.Contracts` is allowed only as the accepted EventStore-fronted gateway/query contract dependency and must not pull EventStore runtime, Dapr sidecar use, server, persistence, actor-host, gateway authorization implementation, or UI behavior into the client package. |

## Tasks

- [ ] Audit the current typed client registration baseline. (AC: 1, 4, 5)
  - [ ] Inspect `src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs`.
  - [ ] Inspect `src/Hexalith.Parties.Client/PartiesClientOptions.cs`.
  - [ ] Inspect `src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj`, `Directory.Build.props`, and `Directory.Packages.props`.
  - [ ] Confirm `AddPartiesClient()` remains the one-line registration and that `Parties:BaseUrl` is documented as the EventStore gateway base URL, not the Parties actor-host URL.
  - [ ] Clarify whether any non-command/query registrations, such as `IAdminPortalGdprClient`, are incidental to the package or intentionally part of the one-line registration; do not make admin/GDPR behavior required evidence for this story.
- [ ] Preserve and verify command-client behavior. (AC: 2, 4)
  - [ ] Inspect `src/Hexalith.Parties.Client/Abstractions/IPartiesCommandClient.cs` and preserve source-compatible method names, parameters, return shapes, cancellation token behavior, and nullability unless an explicit breaking-change decision is recorded.
  - [ ] Inspect `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs`.
  - [ ] Prove create, update details, contact channel, identifier, deactivate/reactivate, composite, and `SetIsNaturalPerson` methods submit EventStore command envelopes with `Domain="party"` and configured tenant.
  - [ ] Prove route-party-id methods overwrite stale payload `PartyId` before serialization.
  - [ ] Preserve the `WithResultAsync` payload contract: correlation id is always returned on accepted command responses, and malformed/non-Parties `resultPayload` fails closed to `Payload = null` rather than throwing when the correlation id is valid.
- [ ] Preserve and verify query-client behavior. (AC: 3, 4)
  - [ ] Inspect `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs` and `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`.
  - [ ] Prove `GetPartyAsync`, `ListPartiesAsync`, and `SearchPartiesAsync` use `POST /api/v1/queries` and accepted EventStore query request shapes.
  - [ ] Preserve typed results for `PartyDetail`, `PagedResult<PartyIndexEntry>`, and `PagedResult<PartySearchResult>`.
  - [ ] Preserve pagination, list filters, active/type filters, date filters, search mode, optional case id, and `requestCustomizer` behavior where already exposed.
  - [ ] Do not add direct projection actor calls, local search calls, old Parties REST fallback routes, or query-time aggregate replay.
- [ ] Harden configuration and safe error mapping. (AC: 4)
  - [ ] Keep local configuration validation fail-closed for missing/relative base URL and missing tenant.
  - [ ] Confirm authorization and authentication are supplied through the host HttpClient/gateway pipeline, not token storage or token parsing inside `Hexalith.Parties.Client`.
  - [ ] Map gateway validation, unauthorized, forbidden, not found, conflict, degraded/unavailable, malformed response, timeout, and cancellation paths to `PartiesClientException` or normal cancellation semantics.
  - [ ] Ensure exception details redact payload values, tokens, API keys, client secrets, connection strings, sidecar internals, and personal data.
- [ ] Prove package and architecture boundaries. (AC: 1, 5)
  - [ ] Extend or preserve `tests/Hexalith.Parties.Client.Tests/FitnessTests/ClientArchitecturalFitnessTests.cs`.
  - [ ] Assert forbidden references: `Hexalith.Parties`, `Hexalith.Parties.Server`, `Hexalith.Parties.Projections`, Dapr, MediatR, FluentValidation, ASP.NET MVC, Swagger/OpenAPI, MCP host packages, actor-host projects, and UI-only infrastructure.
  - [ ] Assert allowed production package references remain limited to `Hexalith.Parties.Contracts`, the accepted `Hexalith.EventStore.Contracts` gateway contract surface, `Microsoft.Extensions.Http`, and `Microsoft.Extensions.Options` or narrowly justified configuration abstractions required by the registration API.
  - [ ] Inspect packed output and dependency graph, not only source project references, before claiming NFR31 package size/count compliance.
- [ ] Verify consumer usability. (AC: 1-5)
  - [ ] Add or preserve a minimal consumer-style test that builds a service provider from configuration, resolves command/query clients, and sends mocked command/query requests without Dapr, MediatR, FluentValidation, Server, Projections, or Parties actor-host references.
  - [ ] Ensure docs/samples that show `AddPartiesClient()` use `Parties:BaseUrl` as the EventStore gateway URL and `Parties:Tenant` as the envelope tenant.
  - [ ] Keep any sample assertions privacy-safe and avoid logging personal-data fixture values as evidence.

## Dev Notes

This is an audit/reconciliation story for the already-present typed client package. The goal is not to invent a second client architecture; the goal is to prove and harden the current `Hexalith.Parties.Client` one-line registration against the Epic 3 package-first developer experience requirements.

Current baseline to preserve:

- `src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs` exposes `AddPartiesClient(this IServiceCollection services, IConfiguration configuration)`.
- `PartiesClientOptions.BaseUrl` is documented as the EventStore gateway base URL, and `PartiesClientOptions.Tenant` is the tenant placed in EventStore command/query envelopes.
- `HttpPartiesCommandClient` posts to `api/v1/commands` and constructs EventStore command envelopes locally because the API-facing command request DTO remains in the EventStore service assembly rather than the contracts package.
- `HttpPartiesQueryClient` posts to `api/v1/queries` and uses `Hexalith.EventStore.Contracts.Queries.SubmitQueryRequest`.
- `IPartiesCommandClient` currently exposes create, update details, contact channel, identifier, lifecycle, composite, and natural-person command methods, with both correlation-id-only and `PartiesCommandResult<PartyDetail>` variants.
- `IPartiesQueryClient` currently exposes `GetPartyAsync`, `ListPartiesAsync`, and `SearchPartiesAsync`.
- Client tests already cover DI, command/query envelope construction, route-party-id override behavior, safe problem-detail mapping, cancellation, and architecture fitness. Treat those tests as the first place to extend, not replace.

Predecessor context:

- Story 3.1 owns the stable `Hexalith.Parties.Contracts` package. This story assumes the typed client consumes those public contracts rather than redefining commands, events, models, value objects, or result types.
- Story 12.5 already rewrote the client as a thin EventStore-fronted wrapper and retired old direct Parties REST paths. Do not resurrect `api/v1/parties`, `api/v1/admin`, Dapr actor calls, service invocation, projection actor calls, or actor-host internals.
- Story 2.7 keeps projection freshness/degradation additive and may rely on existing query-result metadata or degraded response headers. Do not force a finalized long-term public freshness model in this story unless it already exists.

Architecture and package constraints:

- The main `src/Hexalith.Parties` project is an actor host. Public command/query ingress belongs to EventStore, not to new Parties controllers or minimal APIs.
- `Hexalith.Parties.Client` may depend on `Hexalith.Parties.Contracts` and approved HTTP/options/configuration abstractions. It must not depend on Server, Projections, the Parties actor host, AdminPortal implementation, Picker implementation, Dapr, MediatR, FluentValidation, MVC, Swagger/OpenAPI, MCP host packages, or concrete infrastructure.
- `Hexalith.EventStore.Contracts` is allowed only as the accepted EventStore-fronted gateway/query contract dependency. If implementation discovers that it pulls runtime or infrastructure behavior into the packed Client package, record the blocker and defer the dependency policy decision rather than hiding it.
- Keep central package management. Do not add versions to project-local `PackageReference` entries.
- Do not initialize or update nested submodules. Root-level submodules are enough unless explicitly requested.

Testing guidance:

- Use xUnit v3, Shouldly, and existing test helper patterns.
- Keep focused tests in `tests/Hexalith.Parties.Client.Tests`.
- Add package/dependency proof that inspects project references, assembly references, `project.assets.json`, and packed output where practical.
- Use mocked `HttpMessageHandler` or existing EventStore test seams for command/query envelope tests; do not require Dapr/Aspire infrastructure for Tier 1 client behavior.
- If touching async tests, prefer `TestContext.Current.CancellationToken` for new or changed async tests unless existing local style makes that impractical.

Suggested validation:

```powershell
dotnet test .\tests\Hexalith.Parties.Client.Tests\Hexalith.Parties.Client.Tests.csproj --configuration Release
dotnet test .\tests\Hexalith.Parties.Contracts.Tests\Hexalith.Parties.Contracts.Tests.csproj --configuration Release
dotnet build .\src\Hexalith.Parties.Client\Hexalith.Parties.Client.csproj --configuration Release
dotnet pack .\src\Hexalith.Parties.Client\Hexalith.Parties.Client.csproj --configuration Release --no-build --output .\artifacts\packages
dotnet package list .\src\Hexalith.Parties.Client\Hexalith.Parties.Client.csproj --include-transitive
dotnet build .\Hexalith.Parties.slnx --configuration Release
```

## Current Code Surfaces

- `src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj`
- `src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs`
- `src/Hexalith.Parties.Client/PartiesClientOptions.cs`
- `src/Hexalith.Parties.Client/PartiesClientException.cs`
- `src/Hexalith.Parties.Client/Abstractions/IPartiesCommandClient.cs`
- `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs`
- `src/Hexalith.Parties.Client/Abstractions/PartiesCommandResult.cs`
- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs`
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`
- `tests/Hexalith.Parties.Client.Tests/DependencyInjectionTests.cs`
- `tests/Hexalith.Parties.Client.Tests/HttpPartiesCommandClientTests.cs`
- `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs`
- `tests/Hexalith.Parties.Client.Tests/PartiesClientExceptionTests.cs`
- `tests/Hexalith.Parties.Client.Tests/FitnessTests/ClientArchitecturalFitnessTests.cs`
- `docs/getting-started.md`
- `samples/Hexalith.Parties.Sample/Program.cs`
- `samples/Hexalith.Parties.Sample/appsettings.json`

## Anti-Patterns

- Treating `AddPartiesClient()` as ready because DI resolution works, without proving package dependency and packed-output boundaries.
- Reintroducing old direct Parties REST/admin URL literals as compatibility shims.
- Adding Dapr, MediatR, FluentValidation, MVC, actor proxy, service host, projection, or UI dependencies to the Client package.
- Duplicating tenant/RBAC authorization or domain validation in the client.
- Logging or surfacing raw command/query payload JSON, tokens, secrets, sidecar configuration, or personal data in exceptions.
- Breaking `IPartiesCommandClient` or `IPartiesQueryClient` source compatibility without an explicit migration decision.
- Using AdminPortal or Picker tests as the only proof that ordinary .NET consumers can use the typed client package.

## Deferred Decisions

- Whether the local EventStore command request record should move to an EventStore contracts package remains an EventStore/public-contract decision if drift risk becomes material.
- Long-term typed freshness/degradation public model shape remains deferred unless Story 2.7 or a later story finalizes it before implementation.
- Whether admin/GDPR helper clients should remain registered by `AddPartiesClient()` or move behind a separate admin registration remains a package-surface decision if dependency or consumer clarity issues appear.
- Rich authentication helper APIs are out of scope unless an accepted EventStore gateway/client contract already defines them; hosts may supply auth through standard `HttpClient` pipelines.

## References

- `_bmad-output/planning-artifacts/epics.md` - Story 3.2 definition and Epic 3 developer integration scope.
- `_bmad-output/planning-artifacts/prd.md` - Package-first developer experience, FR26, FR27, FR28, and NFR31 dependency expectations.
- `_bmad-output/planning-artifacts/architecture.md` - Client package boundary, DI registration patterns, package dependency direction, and validation commands.
- `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` - Accepted EventStore-fronted client/gateway contract dependency definition for downstream UI consumers.
- `_bmad-output/implementation-artifacts/3-1-publish-stable-contracts-package.md` - Predecessor package-boundary story and `Hexalith.EventStore.Contracts` dependency policy context.
- `_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md` - Existing EventStore-fronted typed client rewrite, route retirement, and review remediation notes.
- `_bmad-output/implementation-artifacts/2-7-handle-projection-freshness-and-graceful-degradation.md` - Additive freshness/degradation metadata boundary for query clients.
- `_bmad-output/project-context.md` - Current .NET 10, package, dependency, privacy, and submodule constraints.
- `_bmad-output/process-notes/story-creation-lessons.md` - L08 party review vs. elicitation sequencing.
- `README.md` - Current EventStore-fronted gateway and typed client positioning.
- `docs/getting-started.md` - Current `AddPartiesClient()` getting-started guidance.

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

TBD

### Completion Notes

TBD

### File List

TBD

### Change Log

- 2026-05-20: Story created by BMAD pre-dev hardening automation as a ready-for-dev typed client registration hardening story.
