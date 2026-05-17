# Story 2.3: Query Party Details by ID

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a consumer of party data,
I want to retrieve full party details by party id,
so that applications and AI tools can display or use the current party record without replaying events.

## Acceptance Criteria

1. **Tenant-authenticated detail query returns the current projection**
   - Given a valid authenticated request with tenant context and an existing party id for that tenant,
   - When the consumer queries party details by id through the accepted query/client boundary,
   - Then the service reads the party detail projection,
   - And returns the current `PartyDetail` record for that tenant,
   - And no aggregate event stream replay is performed as part of the query response.

2. **Cross-tenant detail access fails closed**
   - Given a party detail projection exists for another tenant,
   - When a consumer from a different tenant queries the same party id,
   - Then the query fails closed,
   - And the response does not reveal whether the party exists in another tenant,
   - And no projection actor state for the other tenant is read or serialized.

3. **Missing or unreadable projections return bounded failure**
   - Given the requested party id has no readable projection in the current tenant,
   - When the consumer queries party details by id,
   - Then the service returns a bounded not-found or unavailable result,
   - And it does not fall back to aggregate rehydration, aggregate event scans, list/index search, or cross-tenant lookup.

4. **Inactive parties remain inspectable**
   - Given the party has been deactivated,
   - When the consumer queries party details by id,
   - Then the returned detail record includes inactive status,
   - And the detail remains inspectable unless an accepted GDPR erasure rule prevents it.

5. **Stale, rebuilding, or degraded projection state is bounded**
   - Given the party detail projection actor reports stale, rebuilding, corrupted, or degraded state,
   - When the consumer queries party details by id,
   - Then the response includes or maps to a bounded freshness/degradation signal already accepted by the current EventStore/query boundary,
   - And personal data, raw projection JSON, raw query payloads, tenant membership payloads, tokens, and infrastructure exception text are not logged or returned.

6. **Focused tests verify detail-query behavior**
   - Given detail query tests run,
   - When they cover success, missing tenant, unauthorized tenant, cross-tenant party id, not-found, inactive, malformed/empty projection payload, stale/rebuilding/degraded projection, and client cancellation,
   - Then tenant isolation, bounded response behavior, query contract shape, and privacy-safe diagnostics are verified.

## Acceptance Evidence and Traceability

| AC | Required evidence before review |
| --- | --- |
| AC1 | Client/gateway/query tests prove `GetPartyAsync` or the accepted query handler reads `PartyDetailProjectionActor`/EventStore projection query state and does not scan aggregate events. |
| AC2 | Negative tests prove tenant B cannot infer tenant A's party existence and query routing does not read tenant A actor state. |
| AC3 | Tests cover missing state, empty JSON, malformed JSON, unavailable projection state, and router not-found mapping without aggregate replay fallback. |
| AC4 | A detail query over a deactivated party returns `PartyDetail.IsActive == false` and preserves authorized detail fields. |
| AC5 | Degraded/rebuilding/corrupt-state tests assert bounded status/error mapping plus absence of names, contact values, identifiers, raw payloads, tokens, and infrastructure details from logs/responses. |
| AC6 | Focused tests cover `HttpPartiesQueryClient`, EventStore query gateway routing, projection actor extension fallback order, and any new query-handler code. |

## Tasks / Subtasks

- [ ] Task 1: Audit and reuse current query and projection surfaces before editing (AC: 1, 3, 6)
  - [ ] Start with `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs` and `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`; `GetPartyAsync` already posts an EventStore `SubmitQueryRequest` to `api/v1/queries` with query type `PartyDetail`.
  - [ ] Inspect `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs`; it already pins the client request shape and typed error mapping for query responses.
  - [ ] Inspect `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs`; it already pins EventStore query gateway routing, tenant/domain authorization gates, not-found mapping, and default party projection routing.
  - [ ] Inspect `src/Hexalith.Parties/Extensions/PartyDetailProjectionActorExtensions.cs`; it already contains the detail-read resilience ladder: JSON string, serialized bytes, then typed actor call.
  - [ ] Inspect `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs` and `src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs`; they expose `GetDetailAsync`, `GetDetailJsonAsync`, `GetSerializedDetailAsync`, `IsRebuildingAsync`, and erasure behavior.
  - [ ] Treat the current EventStore-fronted query path as the accepted public boundary. Do not create retired `GET /api/v1/parties/{id}` endpoints in `src/Hexalith.Parties`.

- [ ] Task 2: Implement or reconcile the party-detail query read path (AC: 1, 3, 5)
  - [ ] Confirm where the current EventStore query router resolves a `party` domain `PartyDetail` query. If the route is already implemented, harden tests instead of duplicating it.
  - [ ] If a Parties-owned query resolver is missing, add the narrow resolver/adapter needed for `PartyDetail` only, using `IActorProxyFactory` to create `IPartyDetailProjectionActor` with actor id `{tenant}:party-detail:{partyId}`.
  - [ ] Use `PartyDetailProjectionActorExtensions.ReadDetailAsync(...)` when reading from the actor so legacy/remoting variants are handled consistently.
  - [ ] Map a missing `PartyDetail` to the accepted not-found query result. Do not synthesize details from `PartyIndexEntry`, aggregate state, event streams, or command status.
  - [ ] Preserve `HttpPartiesQueryClient.GetPartyAsync` request shape unless an accepted EventStore query contract requires an additive change. If `ProjectionType`, `ProjectionActorType`, or query type naming must be supplied, make the change deliberately and update both client and gateway tests.
  - [ ] Reconcile the two current detail-query names before changing contracts: `HttpPartiesQueryClient` uses query type `PartyDetail`, while AdminPortal/FrontComposer configuration uses detail query type `GetParty` with projection type `PartyDetail`.

- [ ] Task 3: Enforce tenant fail-closed behavior (AC: 2, 3, 6)
  - [ ] Keep gateway authorization ownership in EventStore. Missing/unauthorized tenant or domain must fail before Parties projection actor state is accessed.
  - [ ] For projection-side reads, derive actor id strictly from the authenticated/request tenant and requested party id. Never accept tenant id from the party id, projection payload, search result, UI state, or client-supplied actor id.
  - [ ] Add tests where tenant A has detail state and tenant B queries the same party id. The result must be forbidden or not found according to the accepted query boundary and must not disclose cross-tenant existence.
  - [ ] Add tests for missing tenant, unauthorized tenant, unauthorized domain, malformed party id/actor id, and projection segment mismatch where relevant.
  - [ ] Ensure failures use bounded ProblemDetails/client exception fields and do not include raw party ids from other tenants, display names, contact values, identifiers, tenant membership payloads, tokens, or serialized actor state.

- [ ] Task 4: Preserve inactive, erasure, and degraded-state semantics (AC: 4, 5)
  - [ ] Querying a deactivated party must return `PartyDetail` with `IsActive == false`; do not hide inactive parties through detail-by-id unless a later product decision says so.
  - [ ] Preserve existing erasure behavior: `PartyDetailProjectionActor.EraseAsync(...)` applies erased/redacted projection state or removes missing state and clears the sequence checkpoint. Do not invent a new GDPR read policy in this story.
  - [ ] If erased-party detail response behavior is ambiguous, record it as deferred rather than silently returning personal data or designing a new erased-party DTO.
  - [ ] For rebuilding/degraded actors, preserve current cached-read behavior where safe and map unsafe/unavailable state to bounded not-found or unavailable responses.
  - [ ] Ensure log messages and exception details for degraded/corrupt actor state remain metadata-only: tenant id, party id, projection name, event type, sequence, and bounded reason are acceptable; names, contact values, identifiers, raw JSON, and secrets are not.

- [ ] Task 5: Keep query/client/AdminPortal contracts aligned without broad surface expansion (AC: 1, 5, 6)
  - [ ] Preserve `IPartiesQueryClient.GetPartyAsync(string partyId, CancellationToken ct)` as the typed client shape unless a separate accepted contract update requires an additive overload.
  - [ ] Keep `PartiesAdminPortalApiClient.GetPartyAsync(...)` on the accepted FrontComposer query service path. AdminPortal may use `GetParty`/`PartyDetail` configuration, but it must not call retired Parties REST endpoints or projection actor internals.
  - [ ] If public freshness/degradation metadata is already exposed by EventStore query results, map it without leaking personal data. If no accepted public shape exists, return the existing bounded error/status and defer the richer response contract.
  - [ ] Do not add list/search/index behavior, MCP `get_party`, OpenAPI generation, admin UI rendering, or picker behavior in this story. Those are covered by other Epic 2, Epic 3, Epic 4, and Epic 7/8 stories.

- [ ] Task 6: Strengthen focused tests (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Extend `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs` for any changed `GetPartyAsync` query payload, not-found/unavailable mapping, malformed query payload, and cancellation behavior.
  - [ ] Extend `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs` for party detail query routing, missing tenant, unauthorized tenant/domain, query router not-found, query router forbidden, and no query routing before auth failure.
  - [ ] Add or extend tests around `PartyDetailProjectionActorExtensions.ReadDetailAsync(...)` for JSON string success, empty JSON fallback, malformed JSON fallback, serialized bytes fallback, typed actor fallback, and all-null not-found behavior.
  - [ ] Extend `tests/Hexalith.Parties.Tests/Projections/PartyDetailProjectionActorCorruptionTests.cs` or adjacent actor tests for rebuilding/degraded reads, cached-state behavior, malformed actor id, state read failure fallback, and metadata-only logging.
  - [ ] Add a deactivated-party detail query test using `PartyDetail.IsActive == false`.
  - [ ] Add privacy-safety assertions for response/error/log text touched by this story. Assert absence of synthetic raw names, contact values, identifiers, serialized detail JSON, query payloads, tokens, and infrastructure connection strings.
  - [ ] If project references, actor boundaries, REST/MCP exposure, or gateway authorization ownership are touched, run the architectural fitness tests.

- [ ] Task 7: Run focused validation (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter FullyQualifiedName~HttpPartiesQueryClientTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~EventStoreGatewayRoutingTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyDetailProjectionActorCorruptionTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter FullyQualifiedName~AdminPortalQueryContractTests` if AdminPortal detail-query contract mapping is touched.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ArchitecturalFitnessTests` if boundaries or project references change.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if public contracts, project references, gateway routing, or package configuration change.

## Dev Notes

### Current Implementation Context

- This is not a greenfield query story. The typed client, EventStore query gateway tests, detail projection actor, detail actor extension fallback, AdminPortal query adapter, and projection tests already exist.
- `IPartiesQueryClient.GetPartyAsync(...)` is the typed consumer API for detail-by-id. `HttpPartiesQueryClient` currently posts to `api/v1/queries` with `Domain = "party"`, `AggregateId = partyId`, `QueryType = "PartyDetail"`, `EntityId = partyId`, and no explicit projection actor type.
- `EventStoreGatewayRoutingTests` currently prove `/api/v1/queries` routes party-domain queries through the EventStore query gateway, supplies default projection type `party`, returns 404 for router not-found, returns 403 for router forbidden, and blocks unauthorized tenant/domain before query routing.
- `PartyDetailProjectionActor` stores detail state under `{tenant}:party-detail:{partyId}` and exposes `GetDetailAsync`, JSON, serialized-byte, rebuilding-state, and erasure operations. It also caches last-known detail during rebuilding/degraded reads.
- `PartyDetailProjectionActorExtensions.ReadDetailAsync(...)` intentionally tries JSON string, then byte payload, then typed actor calls. Reuse it instead of duplicating fallback/deserialization behavior.
- AdminPortal uses FrontComposer query contracts through `PartiesAdminPortalApiClient`. Its current options distinguish list/search/detail query names and projection types; this story should not collapse AdminPortal onto the low-level HTTP client unless that is already accepted.
- The current planning PRD still mentions direct REST query endpoints. Current implementation and hardening direction use the EventStore-fronted query gateway instead. Treat the current gateway/client boundary as authoritative for this story.

### Architecture Patterns and Constraints

- Read projections are Dapr actor-managed JSON state persisted to the Dapr state store. Detail-by-id reads target the per-party detail actor, not the tenant index actor.
- EventStore remains the write-side source of truth. Query-by-id must read the projection and must not replay aggregate events on demand as an implicit query fallback.
- Projection-side tenant isolation is a Parties responsibility, but public request-path tenant/RBAC authorization remains EventStore-owned. Do not wire `ITenantValidator`, `IRbacValidator`, or retired request-path denial translators into Parties.
- The main `src/Hexalith.Parties` project is an actor host plus EventStore gateway integration. Do not add public REST controllers, Swagger/OpenAPI endpoints, or in-process MCP tools for this story.
- `PartyDetail` is the canonical complete party detail shape. Do not create a parallel detail DTO unless serialization/gateway shape forces a narrow wrapper and tests prove the mapping.
- Rebuilding/degraded responses must be bounded and privacy-safe. A stale/degraded indicator is useful only if an accepted public shape already exists; otherwise preserve existing bounded status/error behavior and defer response-shape expansion.
- Personal data may be returned in `PartyDetail` only to authorized consumers. Operational diagnostics, exception messages, ProblemDetails detail fields, query metadata, telemetry dimensions, and test failure strings must not include raw names, contact values, identifiers, raw payloads, tokens, or serialized detail JSON.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs
src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs
src/Hexalith.Parties.Contracts/Models/PartyDetail.cs
src/Hexalith.Parties/Extensions/PartyDetailProjectionActorExtensions.cs
src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs
src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs
src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs
src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs
src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs
src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalOptions.cs
tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs
tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalQueryContractTests.cs
tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs
tests/Hexalith.Parties.Tests/Projections/PartyDetailProjectionActorCorruptionTests.cs
tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs
```

### Previous Story Intelligence

- Story 2.1 established `PartyDetailProjectionHandler` and `PartyDetailProjectionActor` as the detail projection owner. Query code should consume this projection, not duplicate its state logic.
- Story 2.1 also clarified that actor/read fallback behavior must preserve no-op and degraded-state semantics without clearing stored projection state.
- Story 2.2 established the tenant index and local search/list behavior. Detail-by-id must not use index/list/search fallback to infer full details.
- Story 1.6 kept deactivation/reactivation as lifecycle state changes, not deletion. Detail query should return inactive parties unless erasure policy says otherwise.
- Story 1.7 clarified bounded typed failures and privacy-safe error details. Query failures should not expose personal data or cross-tenant existence.
- Story 1.8 reinforced personal-data marking and log safety. Projection and query diagnostics must remain metadata-only.
- Story 1.9 reinforced `PartyDetail` as the canonical complete party shape returned from successful mutations and detail projections.
- L08 in the story-creation lessons ledger says party-mode review and advanced elicitation are separate dated traces. This newly created story has neither trace yet.

### Latest Technical Notes

- Local source of truth for package versions is `Directory.Packages.props`: .NET SDK `10.0.103`, `net10.0`, Dapr packages `1.17.9`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0`, and Microsoft.NET.Test.Sdk `18.5.1`.
- Use `Hexalith.Parties.slnx` for solution-level build validation.
- Do not add package versions to individual project files; central package management is enabled.

### Testing Requirements

- Use xUnit v3 and Shouldly. Use NSubstitute for actor proxy, state manager, query router, and logging collaborators where the existing tests already do.
- Keep projection handler tests infrastructure-free. Query/gateway tests can use the existing WebApplicationFactory and fake query router patterns.
- Use synthetic placeholder personal data and assert selected fields rather than snapshotting full `PartyDetail` JSON.
- Verify cancellation tokens in any new async client/query code. New async test code should pass `TestContext.Current.CancellationToken` where practical.
- For tenant safety, prove both "not routed before auth failure" and "wrong-tenant party does not leak existence" paths.

### Anti-Patterns To Avoid

- Do not add retired `GET /api/v1/parties/{id}` endpoints, controllers, Swagger/OpenAPI work, or MCP tools in this story.
- Do not read aggregate streams or rehydrate `PartyState` as a detail-query fallback.
- Do not query tenant index/search to fabricate full `PartyDetail`.
- Do not accept tenant id from party id, actor id, payload, UI state, or client-supplied projection metadata.
- Do not expose projection actor internals directly to clients.
- Do not add Dapr dependencies to client or contract projects.
- Do not make `Hexalith.Parties.Contracts` depend on Projections, Server, Dapr, MediatR, FluentValidation, UI, or infrastructure packages.
- Do not log personal data, raw query payloads, serialized detail JSON, contact values, identifiers, tokens, tenant membership payloads, or infrastructure secrets.
- Do not recursively initialize or update nested submodules.

### Deferred Decisions

- A richer public freshness/degradation response contract for query responses remains deferred unless the current EventStore query boundary already defines it.
- Erased-party detail response shape remains governed by future GDPR stories unless current erasure behavior already defines a safe response.
- Direct REST/OpenAPI query endpoints remain outside this story; the current accepted boundary is EventStore-fronted queries and typed clients.
- MCP `get_party` tool behavior is owned by Epic 4 and must use the accepted tenant-safe query path when that story runs.
- Cross-projection consistency guarantees between detail and index projections remain deferred unless a later operational story explicitly defines them.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-2.3-Query-Party-Details-by-ID] - Story statement and BDD acceptance criteria.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2-Tenant-Safe-Party-Search-and-Retrieval] - Epic goal and cross-story context for detail, index, query, freshness, rebuild, and deferred search.
- [Source: _bmad-output/planning-artifacts/prd.md#Party-Discovery-Search-MVP] - FR18 detail retrieval, FR19 projection consistency, FR39-FR41 tenant/security context, and FR64 graceful degradation.
- [Source: _bmad-output/planning-artifacts/architecture.md#D1-Projection-Data-Store] - Dapr actor-managed JSON state decision.
- [Source: _bmad-output/planning-artifacts/architecture.md#D4-Projection-Actor-Granularity] - Per-party detail and per-tenant index projection split.
- [Source: _bmad-output/planning-artifacts/architecture.md#D15-Projection-Health-Monitoring-with-Auto-Rebuild-on-Corruption] - Degraded/corruption behavior for projections.
- [Source: _bmad-output/planning-artifacts/architecture.md#D18-Projection-Testability] - Pure projection handlers with thin actor wrappers.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - Actor host, EventStore ownership, projection-side tenant safety, privacy, testing, and submodule guardrails.
- [Source: src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs] - Current typed HTTP query client and `GetPartyAsync` request shape.
- [Source: src/Hexalith.Parties/Extensions/PartyDetailProjectionActorExtensions.cs] - Existing detail actor read fallback ladder.
- [Source: src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs] - Existing Dapr actor wrapper, state key, rebuilding, cache, serialized event dispatch, and erasure behavior.
- [Source: tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs] - Current EventStore query gateway routing and fail-closed authorization coverage.
- [Source: tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs] - Current typed client request and error mapping tests.
- [Source: _bmad-output/process-notes/story-creation-lessons.md#L08-Party-Review-vs-Elicitation] - Trace semantics for later hardening passes.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-05-17: Story created by BMAD pre-dev hardening automation with existing query client, EventStore query gateway, detail projection actor, tenant fail-closed, degraded-state, privacy, and focused validation guidance.
