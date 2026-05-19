# Story 2.8: Projection Rebuild and Health Monitoring

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator of the Parties service,
I want projection corruption and rebuild state to be detected and handled predictably,
so that read models can recover without unsafe data exposure.

## Acceptance Criteria

1. **Healthy projection state is observable**
   - Given detail and index projection actors activate with valid state,
   - When service health checks inspect projection actor routing and responsiveness,
   - Then projection health is reported healthy through the accepted health surface,
   - And normal detail, list, and search read behavior remains available.

2. **Corrupt projection state enters a bounded unsafe state**
   - Given a detail or index projection actor detects corrupt, malformed, incompatible, or unreadable projection state,
   - When the actor activates, loads, or attempts to read the affected state,
   - Then the projection is marked degraded or rebuilding,
   - And unsafe party data, rows, counts, match metadata, cache ages, actor keys, state keys, raw JSON, stream names, exception text, or tenant membership details are not returned.

3. **Rebuild replays durable party events through pure handlers**
   - Given rebuild tooling replays party events for a party detail projection or tenant index projection,
   - When the rebuild processes EventStore actor metadata and event records,
   - Then rebuilt state matches the state produced by normal event delivery through `PartyDetailProjectionHandler` and `PartyIndexProjectionHandler`,
   - And rejection events, unknown events, redacted events, missing events, duplicate sequence checkpoints, and erased-party payloads do not mutate successful projection state incorrectly.

4. **Rebuilding read paths stay tenant-safe**
   - Given a rebuild is in progress for a party or tenant projection,
   - When consumers query affected detail, list, or search paths,
   - Then tenant authorization and projection provenance checks still run before any data is returned,
   - And responses expose only bounded rebuilding, degraded, unavailable, empty, not-found, or current status allowed by Stories 2.6 and 2.7.

5. **Successful rebuild returns projections to current health**
   - Given a projection rebuild completes successfully,
   - When projection actor state is reloaded and health is refreshed,
   - Then the projection leaves rebuilding mode,
   - And subsequent health checks and read paths use the rebuilt projection state without stale/degraded indicators unless another infrastructure condition requires them.

6. **Failed or untrusted rebuilds fail closed**
   - Given rebuild fails, cannot enumerate a trusted party set, cannot prove same-tenant state, cannot deserialize durable payloads safely, cannot persist rebuilt state, or cannot clear checkpoints,
   - When consumers query affected detail, list, or search paths,
   - Then the service fails closed for unsafe reads,
   - And operational logging records bounded metadata and correlation context without personal data, raw payloads, tenant membership payloads, actor/state keys, or infrastructure secrets.

7. **Health and readiness expose bounded projection state**
   - Given projection health is exposed through the service health/readiness or operational health surface,
   - When projection actors are healthy, rebuilding, degraded, unavailable, corrupt, or unsafe,
   - Then health output reports bounded status only,
   - And it does not leak party names, contact values, identifiers, raw projection state, tenant counts, actor/state keys, exact stream positions, or exception bodies.

8. **Focused tests prove status transitions and read behavior**
   - Given projection health/rebuild tests run,
   - When they simulate healthy, corrupt, rebuilding, successful rebuild, failed rebuild, canceled rebuild, missing manifest/index state, redacted payloads, rejection replay, state-store write failure, actor routing failure, and cross-tenant probes,
   - Then health transitions and detail/list/search read responses match the documented behavior.

## Acceptance Evidence and Traceability

| AC | Required evidence before review |
| --- | --- |
| AC1 | `ProjectionActorsHealthCheckTests` prove responsive detail and index projection actors report healthy and do not degrade normal read behavior. |
| AC2 | `PartyDetailProjectionActorCorruptionTests` and `PartyIndexProjectionActorCorruptionTests` prove corrupt detail/index state enters degraded/rebuilding behavior and returns no unsafe data or raw internals. |
| AC3 | `ProjectionRebuildServiceTests` prove detail and index rebuilds replay durable EventStore events through pure projection handlers and preserve rejection/no-op behavior. |
| AC4 | Detail/index actor corruption tests plus gateway/list/search boundary tests only where observable read behavior changes prove rebuilding read paths still require tenant-safe provenance before returning detail, rows, counts, or match metadata. |
| AC5 | `ProjectionRebuildServiceTests`, detail/index actor corruption tests, and `ProjectionActorsHealthCheckTests` prove actors reload trusted rebuilt state, clear rebuilding indicators only after trusted completion, and health/read paths return to current behavior. |
| AC6 | Rebuild service and actor corruption tests prove missing manifest/index state, write failures, unreadable payloads, checkpoint failures, replay exceptions, cancellation, duplicate rebuild requests, and failed reloads fail closed with metadata-only logging. |
| AC7 | `ProjectionActorsHealthCheckTests` and readiness/health extension tests prove bounded status output for healthy, degraded, rebuilding, unavailable, corrupt/untrusted, and failed-closed states. |
| AC8 | Focused validation commands cover rebuild service, detail/index actor corruption, health checks, degraded middleware alignment, optional search/gateway boundary tests only for observable read changes, and architecture fitness. |

## Party-Mode Review Clarifications

- Projection health semantics must stay bounded and Story 2.7-aligned. Implementation may use existing names or the smallest additive names needed by current code, but tests must distinguish the equivalent of healthy/current, degraded, rebuilding, corrupt or untrusted, and failed-closed states without creating a second public status vocabulary.
- Health and readiness output is allow-listed operational metadata only: projection/component category, bounded status, safe reason code, and correlation context when already available. It must not include raw JSON, stream names, actor ids, state keys, partition keys, tenant counts, exact stream positions, cache dictionaries, exception bodies, personal data, tenant membership payloads, Dapr URLs, tokens, or infrastructure secrets.
- Rebuilding, corrupt, failed, canceled, interrupted, or untrusted rebuild state must keep affected reads fail-closed unless the exact stale/degraded read is already permitted by Stories 2.6 and 2.7 with proven tenant, projection kind, party or partition identity, erasure filtering, and cache provenance. Partial or speculative rebuilt state must never be surfaced as current.
- Successful rebuild completion requires replay completion through the existing pure projection handlers, trusted persisted actor state, checkpoint/manifest consistency appropriate to the projection kind, actor reload or equivalent trust refresh, bounded health transition, and safe read availability. Do not clear degraded/rebuilding indicators merely because a rebuild command was attempted.
- Rebuild replay must use the existing `ProjectionRebuildService`, detail/index projection actors, actor reminders, `PartyDetailProjectionHandler`, and `PartyIndexProjectionHandler` unless a focused test proves one of those existing surfaces cannot be hardened. Do not introduce a parallel rebuild framework.
- Normal query-time reads must not replay aggregate streams, query command-status records, fan out through detail actors from index rows, or call Memories/search providers to make stale projections look current.
- Cancellation, timeout, replay exception, validation failure, duplicate rebuild request, failed state write, failed checkpoint/manifest update, or failed reload must leave the affected projection in bounded degraded/rebuilding/failed-closed behavior and must not start follow-on writes, retries, query fallbacks, or secondary lookups after cancellation is observed.
- Gateway, list, and search boundary tests are required only when this story changes externally observable read behavior; otherwise coverage should stay focused on rebuild service, projection actors, health checks, degraded middleware, and architecture fitness guardrails.

## Response Outcome Boundaries

| Scenario | Expected public outcome |
| --- | --- |
| Projection actors responsive and not rebuilding | `/health` reports projection actors healthy; normal query behavior remains available. |
| Detail actor detects corrupt state on activation | Actor marks rebuilding/degraded, schedules or exposes rebuild path, and does not return unsafe detail data. |
| Index actor detects corrupt state on activation | Actor marks rebuilding/degraded, schedules or exposes rebuild path, and does not return unsafe rows/counts/metadata. |
| Detail rebuild succeeds for one party | Rebuilt `PartyDetail` matches handler replay output and actor leaves rebuilding mode. |
| Index rebuild succeeds for a tenant | Rebuilt tenant index matches handler replay output, manifest/checkpoint state is consistent, and actor leaves rebuilding mode. |
| Rebuild is in progress | Detail/list/search reads expose bounded rebuilding/degraded behavior and never bypass tenant authorization. |
| Rebuild cannot enumerate party ids | Bounded unavailable/degraded outcome; no speculative aggregate scans beyond accepted rebuild tooling and no unsafe read success. |
| Rebuild write or checkpoint delete fails | Rebuild remains degraded/failed; reads fail closed unless previously loaded same-tenant cache is already accepted by Stories 2.6 and 2.7. |
| Redacted or erased-party payload appears during rebuild | Payload is skipped or handled according to existing erasure semantics without rehydrating personal data or corrupting projections. |
| Cross-tenant probe during another tenant rebuild | Probing tenant receives only its own authorized outcome; no existence, count, cache age, rebuild status, or actor key leaks. |
| Cancellation during rebuild | Cancellation is honored; no follow-on actor state writes, checkpoint writes, query fallback, detail fan-out, or retries start after cancellation. |
| Duplicate rebuild request while actor is already rebuilding | Existing rebuilding/degraded state remains authoritative; no parallel replay corrupts state, clears indicators early, or exposes partially rebuilt data. |
| Rebuild replay throws or stops midway | Affected projection remains degraded/rebuilding/failed-closed; persisted partial state is not reported as current. |
| Rebuild completes but trust validation, reload, manifest, checkpoint, or state consistency fails | Health/readiness remains bounded degraded or failed-closed and reads do not claim current projection state. |
| Health output includes failures | Status includes bounded component/projection category only; no personal data, raw JSON, keys, stream names, payloads, or exception bodies. |

## Tasks / Subtasks

- [ ] Task 1: Audit existing projection rebuild and health surfaces before editing (AC: 1, 3, 5, 7)
  - [ ] Start with `src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs`; it already reads EventStore actor metadata/events through Dapr actor state, replays payloads through pure projection handlers, writes checkpoints, and skips erased/redacted payloads safely.
  - [ ] Inspect `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs` and `PartyIndexProjectionActor.cs`; both already maintain `_isRebuilding`, schedule `auto-rebuild` reminders after deserialization failures, expose `IsRebuildingAsync`, and keep last-known in-process state.
  - [ ] Inspect `src/Hexalith.Parties/HealthChecks/ProjectionActorsHealthCheck.cs`; it currently pings detail and index projection actors and reports the registration failure status on actor routing failures.
  - [ ] Inspect `src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs`; it already suppresses stale-read headers for sidecar/projection-actor unhealthy states and removes them on 5xx responses.
  - [ ] Treat these surfaces as existing implementation to reconcile and harden. Do not replace them with a new rebuild framework, public controller, MCP tool, AdminPortal route, or direct UI actor call.

- [ ] Task 2: Make projection rebuilding state observable without leaking internals (AC: 1, 2, 4, 7)
  - [ ] Extend actor or health-check behavior only as much as needed to distinguish healthy, rebuilding, degraded, unavailable, and unsafe projection states.
  - [ ] Keep status values bounded. Do not expose raw sequence numbers, stream names, state keys, actor ids, partition keys, tenant counts, exact positions, exception messages, serialized projection state, or raw Dapr URLs.
  - [ ] If `IsRebuildingAsync` is used by health checks, probe it through the existing actor interfaces and keep timeout/cancellation behavior bounded.
  - [ ] Preserve the `/health`, `/alive`, `/ready`, and `/actors/` middleware bypass to avoid recursive health invocation.
  - [ ] Ensure projection health remains operational metadata, not an alternate authorization or query path.

- [ ] Task 3: Harden detail projection rebuild behavior (AC: 2, 3, 4, 5, 6, 8)
  - [ ] Preserve actor id and state key shape `{tenant}:party-detail:{partyId}`.
  - [ ] Keep rebuild replay inside `ProjectionRebuildService.RebuildDetailProjectionAsync(...)` and `RebuildSingleDetailAsync(...)`; do not make normal read queries scan aggregate streams or command-status records.
  - [ ] Ensure checkpoint resume uses `resumeSequence + 1` and never replays already-applied events unless the checkpoint is absent or reset intentionally.
  - [ ] Ensure `PartyDetailProjectionHandler.Apply(...)` remains the only place rebuild mutates detail state.
  - [ ] Ensure corrupt/malformed detail state, failed writes, failed checkpoint deletes, or failed reloads leave the actor in a bounded degraded/rebuilding/failed-closed state.
  - [ ] Ensure logs and diagnostics include only projection kind, bounded status, tenant-safe operation category, party id where already authorized/operationally allowed, event type name, sequence number category if already internal, and correlation context.

- [ ] Task 4: Harden tenant index projection rebuild behavior (AC: 2, 3, 4, 5, 6, 8)
  - [ ] Preserve actor id shape `{tenant}:party-index`, index state key `{tenant}:party-index:{partitionKey}`, manifest key `{tenant}:party-index:manifest`, and per-party sequence checkpoint key `{tenant}:party-index:{partyId}:last-sequence`.
  - [ ] Ensure `ProjectionRebuildService.RebuildIndexProjectionAsync(...)` uses the existing index or manifest only as a trusted same-tenant party-id source.
  - [ ] Ensure `PartyIndexProjectionHandler.Apply(...)` remains the only place rebuild mutates index entries.
  - [ ] Keep erased entries excluded and rejection events non-mutating.
  - [ ] Ensure missing or untrusted manifest/index state does not fabricate party ids, rows, counts, pagination metadata, search matches, or health status for another tenant.
  - [ ] Ensure static last-known index cache is keyed by tenant/projection/partition state key and cannot be reused across tenants or malformed actor ids.

- [ ] Task 5: Align health, degraded headers, and read outcomes (AC: 1, 4, 5, 7)
  - [ ] Update `ProjectionActorsHealthCheck` tests and implementation so projection actor routing failure, timeout, and rebuilding state classify consistently with Story 2.7 degraded-read rules.
  - [ ] Keep sidecar-unavailable behavior unsafe: no stale-read success path and no degraded headers pretending cached reads are safe.
  - [ ] Keep projection-actor-unavailable behavior unsafe: no stale-read success path through `DegradedResponseMiddleware`.
  - [ ] Allow pub/sub degraded or state-store unavailable only when projection actor/read state provenance is already proven safe by Stories 2.6 and 2.7.
  - [ ] Ensure 5xx responses do not retain `X-Service-Degraded` or `X-Stale-Data-Age` headers.

- [ ] Task 6: Preserve adjacent boundaries and non-goals (AC: 3, 4, 6)
  - [ ] Do not add public REST controllers, Swagger/OpenAPI endpoints, in-process MCP tools, AdminPortal routes, picker routes, Memories repair paths, or semantic/graph search repair paths.
  - [ ] Do not move EventStore-owned public request authorization, tenant extraction, idempotency, event envelope, snapshot, or command-status behavior into Parties.
  - [ ] Do not make `Hexalith.Parties.Contracts` depend on Projections, Server, Dapr, MediatR, FluentValidation, UI, Memories, or infrastructure packages.
  - [ ] Do not weaken existing architecture fitness tests for actor-host boundaries, Dapr internal routes, projection interfaces, tenant authorization ownership, or personal-data logging.
  - [ ] Keep automated repair controls and operator dashboards limited to the accepted service/health/rebuild surfaces in this story; broad runbooks and UI controls belong to later operational/admin stories.

- [ ] Task 7: Add or harden focused tests (AC: 1-8)
  - [ ] Extend `tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildServiceTests.cs` for successful detail rebuild, successful index rebuild, checkpoint resume, checkpoint delete, missing manifest/index behavior, write failure, redacted payload skip, unknown-event skip, erased-party payload handling, and cancellation.
  - [ ] Extend `tests/Hexalith.Parties.Tests/Projections/PartyDetailProjectionActorCorruptionTests.cs` for activation corruption, rebuilding read behavior, rebuild success, rebuild failure, failed reload, invalid actor id, and metadata-only logs.
  - [ ] Extend `tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs` for activation corruption, rebuilding cache behavior, manifest fallback, static cache tenant provenance, rebuild success, rebuild failure, malformed actor id, and no cross-tenant rows/counts.
  - [ ] Extend `tests/Hexalith.Parties.Tests/HealthChecks/ProjectionActorsHealthCheckTests.cs` for healthy, actor timeout, actor failure, rebuilding state, cancellation, and bounded description text.
  - [ ] Extend `tests/Hexalith.Parties.Tests/HealthChecks/DegradedResponseMiddlewareTests.cs` if health classification changes affect degraded header behavior.
  - [ ] Extend `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs` and search/list boundary tests only where rebuild/degraded query behavior changes observable read outcomes.
  - [ ] Run architecture fitness tests when touching actor interfaces, public endpoints, Dapr routes, package references, health endpoints, or logging boundaries.

## Required Test Matrix

| Scenario | Expected proof |
| --- | --- |
| Healthy projection actors | `ProjectionActorsHealthCheck` returns healthy with bounded description. |
| Index actor timeout or routing failure | Health returns configured degraded status and no raw route/state details. |
| Detail actor corrupt state on activation | Actor marks rebuilding, schedules rebuild, and does not return unsafe detail data. |
| Index actor corrupt state on activation | Actor marks rebuilding, schedules rebuild, and does not return unsafe index rows/counts. |
| Detail rebuild from durable events | Rebuilt detail equals normal handler replay output. |
| Index rebuild from durable events | Rebuilt index equals normal handler replay output and manifest is consistent. |
| Checkpoint resume | Rebuild resumes after the checkpoint sequence and does not replay already-applied events. |
| Checkpoint deletion success | Successful rebuild clears checkpoint state and leaves actor current. |
| Checkpoint deletion failure | Rebuild remains degraded/failed and reads do not claim current state. |
| Missing manifest and missing index | Rebuild cannot enumerate parties and fails closed without fabricated rows. |
| Unknown event type during rebuild | Unknown event is skipped with bounded operational log metadata. |
| Redacted/erased payload during rebuild | Payload is skipped or handled without reintroducing personal data. |
| Rejection event replay | Rejection events do not mutate successful detail or index state. |
| Rebuild write failure | Failure is surfaced as bounded degraded/failed state; no unsafe read success. |
| Rebuild cancellation | Cancellation stops follow-on event reads, writes, checkpoint updates, and retry work. |
| Duplicate rebuild request | Existing rebuilding state remains authoritative; no parallel replay clears degraded/rebuilding indicators early or exposes partial state. |
| Replay failure midway | Failed replay leaves the affected projection degraded/rebuilding/failed-closed and does not report partially rebuilt state as current. |
| Rebuild validation or reload failure | Completed replay without trusted persisted/reloaded state remains unsafe; health/readiness stay bounded degraded or failed-closed. |
| Rebuilding detail read | Read path returns only accepted bounded rebuilding/degraded/current-cache outcome after tenant proof. |
| Rebuilding list/search read | Rows, counts, and match metadata are returned only from proven same-tenant state. |
| Cross-tenant rebuild probe | No other-tenant existence, count, cache, rebuild, actor key, or health signal leaks. |
| Degraded middleware alignment | Degraded headers appear only for safe degraded reads and are removed from 5xx responses. |
| Fitness guardrails | No new public REST/MCP/AdminPortal/picker surface or forbidden dependency is introduced. |

## Dev Notes

### Current Implementation Context

- This story is a hardening/reconciliation story, not a greenfield rebuild feature.
- `ProjectionRebuildService` already exists and replays EventStore actor state through `PartyDetailProjectionHandler` and `PartyIndexProjectionHandler`.
- Detail and index projection actors already expose `IsRebuildingAsync`, schedule `auto-rebuild` reminders after deserialization failures, and maintain in-memory last-known state.
- `ProjectionActorsHealthCheck` currently validates actor routing with `PingAsync` only; this story may need bounded rebuilding/degraded classification without exposing projection internals.
- `DegradedResponseMiddleware` already treats sidecar and projection-actor unhealthy states as unsafe for stale reads. Preserve that safety property.
- Existing Story 2.7 guidance owns freshness/degradation response semantics. Story 2.8 should align rebuild health with that model instead of creating a second public status vocabulary.

### Architecture Patterns and Constraints

- Read projections are Dapr actor-managed JSON state persisted to the Dapr state store. Detail reads target per-party detail actors; list/filter/search reads target per-tenant index actors.
- EventStore remains the durable source of truth and owns write-side tenant validation, idempotency, event envelopes, snapshots, status tracking, pub/sub publication, and event ordering guarantees.
- Rebuild tooling may replay durable events into pure projection handlers. Normal query-time reads must not scan aggregate streams or rehydrate aggregate state to fabricate currentness.
- Projection state, health state, cache state, and rebuild state are tenant-sensitive metadata. Treat them with the same non-enumeration rules as party data.
- `Hexalith.Parties` remains an actor host. Do not add public REST, Swagger/OpenAPI, or in-process MCP hosting to that project.
- Contracts must remain additive and infrastructure-free.
- Logs, health output, exceptions, ProblemDetails, and test artifacts must exclude personal data and raw projection payloads.

### Previous Story Intelligence

- Story 2.1 established pure projection handlers plus actor wrappers, checkpoints, rebuild/degraded mode, and metadata-only diagnostics.
- Story 2.2 established the tenant index projection, manifest, partition strategy abstraction, erased-entry exclusion, and user-visible list/search proof obligations.
- Story 2.3 established the EventStore-fronted query gateway, auth-before-actor-read ordering, no alternate-key probing, bounded corrupt/degraded payload behavior, and terminal cancellation.
- Story 2.4 established index-only list/filter semantics, post-filter metadata consistency, untrusted page/cursor handling, UTC metadata filtering, degraded cache provenance, and terminal cancellation.
- Story 2.5 established MVP display-name-only search, match metadata after current-tenant and erasure filtering, degraded-cache provenance gates, negative future-field tests, and terminal cancellation.
- Story 2.6 established fail-closed tenant-safe projection reads across detail, list/filter, search, degraded cache, actor key shape, and metadata-only diagnostics.
- Story 2.7 established bounded freshness/degradation behavior for current, stale, degraded, rebuilding, and unsafe projection states.
- L08 in the lessons ledger says party-mode review and advanced elicitation are separate dated traces. This story now carries a completed party-mode trace and still needs a separate advanced elicitation pass before that hardening phase can be considered complete.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs
src/Hexalith.Parties.Projections/Services/IProjectionRebuildService.cs
src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs
src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs
src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs
src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs
src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs
src/Hexalith.Parties.Projections/Handlers/PartyIndexProjectionHandler.cs
src/Hexalith.Parties.Projections/Strategies/SingleKeyPartitionStrategy.cs
src/Hexalith.Parties/HealthChecks/ProjectionActorsHealthCheck.cs
src/Hexalith.Parties/HealthChecks/PartiesHealthCheckExtensions.cs
src/Hexalith.Parties/HealthChecks/DaprSidecarHealthCheck.cs
src/Hexalith.Parties/HealthChecks/DaprStateStoreHealthCheck.cs
src/Hexalith.Parties/HealthChecks/DaprPubSubHealthCheck.cs
src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs
src/Hexalith.Parties/Search/LocalPartySearchService.cs
src/Hexalith.Parties/Search/PartySearchBoundary.cs
src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs
tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildServiceTests.cs
tests/Hexalith.Parties.Tests/Projections/PartyDetailProjectionActorCorruptionTests.cs
tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs
tests/Hexalith.Parties.Tests/HealthChecks/ProjectionActorsHealthCheckTests.cs
tests/Hexalith.Parties.Tests/HealthChecks/DegradedResponseMiddlewareTests.cs
tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs
tests/Hexalith.Parties.Tests/Search/PartySearchServiceBoundaryTests.cs
tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs
```

### Latest Technical Notes

- Local source of truth for versions is `Directory.Packages.props`: .NET SDK `10.0.300`, target framework `net10.0`, Dapr packages `1.17.9`, Aspire `13.3.3`, MediatR `14.1.0`, FluentValidation `12.1.1`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0`, and Microsoft.NET.Test.Sdk `18.5.1`.
- Use `Hexalith.Parties.slnx` for solution-level validation.
- Do not add package versions to individual `.csproj` files; central package management is enabled.
- Do not recursively initialize or update nested submodules. Root-level submodules are enough unless explicitly requested.

### Suggested Validation Commands

```text
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ProjectionRebuildServiceTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyDetailProjectionActorCorruptionTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyIndexProjectionActorCorruptionTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ProjectionActorsHealthCheckTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~DegradedResponseMiddlewareTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartySearchServiceBoundaryTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~EventStoreGatewayRoutingTests
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ArchitecturalFitnessTests
dotnet build Hexalith.Parties.slnx --configuration Release
```

### Anti-Patterns To Avoid

- Do not create a second rebuild framework, duplicate projection state model, alternate query gateway, public repair endpoint, or UI-only repair path.
- Do not make normal reads replay aggregate streams, query command status records, fan out through detail actors from index rows, or call Memories/search providers to make stale projections look current.
- Do not expose raw event payloads, projection JSON, Dapr URLs, actor ids, state keys, stream names, sequence maps, tenant counts, cache dictionaries, exception text, tokens, or tenant membership payloads.
- Do not treat health checks as authorization. EventStore public authorization must complete before Parties projection data is returned.
- Do not serve stale/cached projection data unless tenant, projection kind, partition or party id, erasure filtering, and cache provenance are all proven.
- Do not weaken existing tenant-safe read, privacy inventory, Dapr ACL, actor-host, or public-surface fitness tests.

### Deferred Decisions

- Operator-triggered rebuild APIs, dashboards, repair runbooks, progress percentages, and alert routing are deferred unless an accepted operational surface already exists in this story's code path.
- Multi-partition index rebuild orchestration remains deferred until the partition strategy moves beyond the v1.0 single-key strategy.
- Dedicated search-engine, Memories, semantic, graph, contact, identifier, duplicate-detection, and temporal projection rebuild flows remain outside this story.
- AdminPortal/picker localization, focus behavior, and user-facing rebuild copy are downstream UI concerns unless minimal client/server contract compatibility is required.
- Exact public freshness metadata shape remains owned by Story 2.7 unless this story must add a minimal bounded projection-health value to support rebuild status.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-2.8-Projection-Rebuild-and-Health-Monitoring] - Story statement and BDD acceptance criteria.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2-Tenant-Safe-Party-Search-and-Retrieval] - Epic goal and cross-story context for detail, index, list, search, freshness, rebuild, and deferred search.
- [Source: _bmad-output/planning-artifacts/prd.md#FR64] - Graceful degradation when infrastructure components are unavailable.
- [Source: _bmad-output/planning-artifacts/prd.md#FR71] - Health and readiness signals for infrastructure orchestration.
- [Source: _bmad-output/planning-artifacts/architecture.md#Cross-Cutting-Concerns-Identified] - Projection infrastructure, observability, graceful degradation, and testability rules.
- [Source: _bmad-output/planning-artifacts/architecture.md#Architecture-Validation-Results] - Projection lifecycle and health monitoring decision chain.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - EventStore ownership, projection-side tenant safety, privacy, testing, and actor-host guardrails.
- [Source: _bmad-output/implementation-artifacts/2-7-handle-projection-freshness-and-graceful-degradation.md] - Immediate predecessor and freshness/degradation response model.
- [Source: src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs] - Current rebuild service and event replay behavior.
- [Source: src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs] - Detail actor rebuild, corruption, checkpoint, and cache behavior.
- [Source: src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs] - Index actor rebuild, corruption, manifest, checkpoint, and cache behavior.
- [Source: src/Hexalith.Parties/HealthChecks/ProjectionActorsHealthCheck.cs] - Current projection actor health probe.
- [Source: src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs] - Current stale/degraded header behavior and unsafe component classification.
- [Source: tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildServiceTests.cs] - Existing rebuild service tests.
- [Source: tests/Hexalith.Parties.Tests/HealthChecks/ProjectionActorsHealthCheckTests.cs] - Existing projection health tests.
- [Source: _bmad-output/process-notes/story-creation-lessons.md#L08-Party-Review-vs-Elicitation] - Trace semantics for later hardening passes.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Party-Mode Review

- Date/time: 2026-05-19T18:04:21+02:00
- Selected story key: `2-8-projection-rebuild-and-health-monitoring`
- Command/skill invocation used: `/bmad-party-mode 2-8-projection-rebuild-and-health-monitoring; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary: Three reviewers recommended `needs-story-update` and one recommended `ready-for-dev` with the same low-risk clarifications. Shared risks were under-specified observable projection states, fail-closed read and health behavior, rebuild completion criteria, cancellation and partial replay semantics, privacy-safe health diagnostics, AC-to-test traceability, and accidental scope expansion into a second rebuild framework, public repair surface, query-time stream replay, or Parties-owned authorization.
- Changes applied:
  - Added `Party-Mode Review Clarifications` to define Story 2.7-aligned bounded projection states, allow-listed health/readiness metadata, fail-closed behavior for corrupt/rebuilding/canceled/failed/untrusted rebuild states, success criteria for clearing degraded/rebuilding indicators, existing-surface ownership, no query-time replay fallback, cancellation/failure semantics, and gateway/search test scope limits.
  - Expanded the required test matrix with duplicate rebuild, replay failure, and validation/reload failure scenarios.
  - Updated Dev Notes to record the completed party-mode trace and the remaining separate advanced elicitation pass.
- Findings deferred: Exact enum/type names for projection state, whether degraded and rebuilding are represented as separate health dimensions or one status, whether active rebuild readiness is degraded versus unavailable, whether trusted stale reads are served during rebuild beyond Story 2.7 allowances, rebuild retry/backoff policy, operator-facing repair endpoints or dashboards, broader rebuild framework decisions, tenant-level health inventory, EventStore authorization changes, persistence shape changes for rebuild metadata, and gateway/search contract changes unless implementation changes observable read behavior.
- Final recommendation: `ready-for-dev`

## Change Log

- 2026-05-19: Party-mode review applied low-risk clarifications for bounded projection states, fail-closed rebuild/read behavior, privacy-safe health output, existing rebuild-surface ownership, failure/cancellation coverage, and AC-to-test traceability.
- 2026-05-19: Story created by BMAD pre-dev hardening automation with existing projection rebuild service, detail/index projection actors, health checks, degraded middleware, tenant-safe cache provenance, privacy-safe diagnostics, and focused validation guidance.
