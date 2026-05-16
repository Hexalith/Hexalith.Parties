# Story 1.9: Return Updated Party State from Mutations

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a client application,
I want successful party mutation commands to return the resulting party state,
so that I can update my UI or workflow without issuing an immediate follow-up query.

## Acceptance Criteria

1. **Create commands return the created party detail**
   - Given a create party command succeeds,
   - When the command result is returned through the domain-service and public command response path,
   - Then the response includes the complete created party detail,
   - And the detail reflects all events emitted by the command, including `PartyCreated`, `PartyDisplayNameDerived`, contact-channel events, identifier events, and preferred-channel events where applicable.

2. **Simple mutation commands return the updated party detail**
   - Given a party detail, contact channel, identifier, natural-person flag, or lifecycle mutation succeeds,
   - When the command result is returned,
   - Then the response includes the updated party detail,
   - And the detail reflects the aggregate state after applying every success event emitted by that command.

3. **Composite mutations return final state after ordered events**
   - Given a command emits multiple events in one aggregate turn,
   - When the result payload is assembled,
   - Then the returned party detail reflects the final aggregate state after applying all emitted events in order,
   - And no stale pre-command state is returned.

4. **Rejected and no-op commands do not present a successful updated state**
   - Given a mutation command is rejected,
   - When the result is returned,
   - Then no updated party detail is presented as a successful mutation result,
   - And the rejection outcome remains explicit and typed.
   - Given a mutation command is a no-op,
   - When the result is returned,
   - Then the response does not pretend that a state change occurred,
   - And any no-op payload behavior is documented and covered by tests.

5. **API, client, and MCP consumers can access the enriched result**
   - Given public REST command submission, the typed Parties command client, and MCP tools submit a mutation command,
   - When the command is accepted and processed successfully in the current synchronous command path,
   - Then consumers can read the updated `PartyDetail` without issuing a separate immediate query,
   - And existing correlation-id/status tracking remains available for compatibility.

6. **Automated tests verify updated-state correctness and failure safety**
   - Given aggregate, domain-service, gateway, client, and MCP tests run,
   - When create, update, contact, identifier, deactivate, reactivate, composite, rejection, and no-op paths are exercised,
   - Then success paths assert returned detail correctness,
   - And rejection/no-op paths assert that misleading updated state is absent.

## Tasks / Subtasks

- [ ] Task 1: Confirm the current result propagation path before editing (AC: 1, 2, 3, 5)
  - [ ] Inspect `DomainResult.ResultPayload`, `DomainServiceWireResult`, `DaprDomainServiceInvoker.ToDomainResult`, `CommandProcessingResult.ResultPayload`, and `CommandsController` response assembly.
  - [ ] Verify whether `DomainServiceWireResult` still drops `DomainResult.ResultPayload`; if so, extend the existing EventStore result-payload hook instead of inventing a Parties-only side channel.
  - [ ] Verify whether `SubmitCommandResponse` needs an additive optional result payload property while preserving `correlationId` compatibility.
  - [ ] Keep EventStore-owned changes minimal and compatible; do not rework command processing status, event persistence, or actor pipeline behavior beyond carrying the existing result payload.

- [ ] Task 2: Add or refine a single enriched party mutation result shape (AC: 1, 2, 3, 4)
  - [ ] Reuse `PartyDetail` as the client/API/MCP-facing "updated party state" shape; do not introduce a second detail DTO unless serialization requires a tiny wrapper.
  - [ ] Extend `CompositeCommandResult` or add a narrowly named Parties result type that derives from `DomainResult` and overrides `ResultPayload` with serialized `PartyDetail`.
  - [ ] Keep `UpdatedPartyDetail` null for rejection results and for no-op results unless the implementation deliberately documents an unchanged-detail no-op response.
  - [ ] Ensure result payload serialization uses the same camelCase/string-enum JSON conventions as existing client/query contracts.

- [ ] Task 3: Build final party detail from current state plus emitted events (AC: 1, 2, 3)
  - [ ] Reuse or generalize `PartyAggregate.BuildPartyDetailFromState(...)` rather than duplicating projection logic.
  - [ ] Support `CreateParty` and `CreatePartyComposite` when prior `PartyState` is null by deriving detail from emitted events in order.
  - [ ] Support simple commands: `UpdatePersonDetails`, `UpdateOrganizationDetails`, `AddContactChannel`, `UpdateContactChannel`, `RemoveContactChannel`, `AddIdentifier`, `RemoveIdentifier`, `DeactivateParty`, `ReactivateParty`, and `SetIsNaturalPerson`.
  - [ ] Preserve existing event emission order and `PartyState.Apply` behavior; do not mutate the incoming `PartyState` merely to assemble the response.
  - [ ] Keep `CreatedAt` from the existing state where available. For create responses, use a deterministic command/result-time value only if the existing contracts require one; document any unavoidable timestamp limitation in completion notes.

- [ ] Task 4: Wire enriched results through the Parties host and EventStore gateway (AC: 1, 2, 3, 5)
  - [ ] Ensure `src/Hexalith.Parties/Program.cs` can keep calling `DomainServiceWireResult.FromDomainResult(result)` once the wire DTO preserves payload.
  - [ ] Ensure `DaprDomainServiceInvoker` reconstructs a `DomainResult` that preserves `ResultPayload` after deserializing `DomainServiceWireResult`.
  - [ ] Ensure aggregate actor processing copies `domainResult.ResultPayload` into `CommandProcessingResult.ResultPayload` on success paths and never on rejection unless explicitly supported by existing EventStore semantics.
  - [ ] Add `resultPayload` or a typed `payload` field to the public accepted command response only as an additive optional property; do not remove `correlationId` or change status URLs.

- [ ] Task 5: Update client and MCP consumers to expose the party detail (AC: 5)
  - [ ] Evolve `IPartiesCommandClient` and `HttpPartiesCommandClient` so mutation methods can return the updated `PartyDetail` while retaining a way to access `correlationId`.
  - [ ] Prefer an additive command response record such as `PartiesCommandResult<T>` if changing every method from `Task<string>` would break too many existing callers.
  - [ ] Update MCP `create_party`, `update_party`, and `delete_party` flows to return `PartiesMcpToolResult.Succeeded(..., PartyDetail)` when a successful updated detail is available.
  - [ ] Keep MCP validation and client exception mapping unchanged for rejected/invalid commands; do not call the query client as a hidden follow-up query to fake FR69.

- [ ] Task 6: Add focused tests for response payload behavior (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Add aggregate tests proving create, person update, organization update, contact add/update/remove, identifier add/remove, deactivate, reactivate, and `UpdatePartyComposite` return correct detail payloads.
  - [ ] Add rejection and no-op tests proving `UpdatedPartyDetail` or result payload is null/absent when no successful mutation result should be shown.
  - [ ] Add contract tests for result payload serialization and backward-compatible JSON shape.
  - [ ] Add `/process` and EventStore gateway tests proving result payload survives Parties host -> Dapr wire DTO -> EventStore actor -> command response.
  - [ ] Update `HttpPartiesCommandClientTests` and `PartiesMcpToolDispatchTests` to assert updated details are surfaced without immediate query calls.

- [ ] Task 7: Run focused validation (AC: 6)
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregate`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~CompositeCommandResult`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter "FullyQualifiedName~PartiesProcessEndpointTests|FullyQualifiedName~EventStoreGatewayRoutingTests"`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter FullyQualifiedName~HttpPartiesCommandClientTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj --configuration Release --filter FullyQualifiedName~PartiesMcpToolDispatchTests`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateApplyOrderingFitnessTests` if `PartyState` changes.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if public contracts, EventStore submodule contracts, or client method signatures change.

## Dev Notes

### Current Implementation Context

- This story is not a new endpoint story. The public request path is EventStore -> Parties domain service -> EventStore actor response. Keep the main `src/Hexalith.Parties` project as the actor host with only the sidecar-internal `/process`, `/dapr/subscribe`, and `/tenants/events` routes.
- EventStore already has a result-payload concept: `DomainResult.ResultPayload`, `CommandProcessingResult.ResultPayload`, and aggregate actor plumbing reference it. Current inspection shows `DomainServiceWireResult` only serializes `IsRejection` and `Events`, so Parties result payloads can be lost across Dapr service invocation unless that wire DTO is extended.
- `CompositeCommandResult` already has `UpdatedPartyDetail`, and `PartyAggregate.Handle(UpdatePartyComposite, PartyState?)` already calls `BuildPartyDetailFromState(command.PartyId, state, events)`. Treat that as the existing pattern to generalize.
- Simple command handlers currently return plain `DomainResult.Success(...)`, so they cannot expose `PartyDetail` without a new or enhanced `DomainResult` subtype.
- Public `SubmitCommandResponse` currently exposes only `CorrelationId`; client command methods currently return `Task<string>`. Any richer response must be additive and backward-compatible where possible.
- MCP command tools currently return `accepted` with correlation ids for create/update/delete. FR69 requires successful mutation operations to expose updated state, so MCP should surface `PartyDetail` when the command response contains it, without issuing a query as a workaround.

### Architecture Patterns and Constraints

- Domain logic remains pure: aggregate `Handle` methods are synchronous and produce `DomainResult`/events. No I/O, no query client calls, no Dapr calls, and no projection reads inside aggregate logic.
- Updated state must be assembled by applying emitted success events over the current aggregate state in order. This is the same mental model as `PartyState.Apply` and prevents stale pre-command responses.
- Rejections are persisted events but no-op `PartyState.Apply` overloads must remain before success `Apply` overloads. Do not reorder `PartyState` casually.
- `PartyDetail` is the API/MCP/client-facing complete party shape already used by query and MCP tools. Use it for mutation responses unless an existing EventStore response wrapper requires a generic `JsonElement`.
- If EventStore contract/server changes are required, make them in the `Hexalith.EventStore` submodule deliberately, with a separate validation mindset. Do not initialize nested submodules recursively.
- Keep response payloads personal-data aware. Returning `PartyDetail` is intentional product behavior, but logs, exception messages, status records, and telemetry must not include serialized detail payloads or personal data fields.

### Current Code Surfaces To Inspect

```text
Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Results/DomainResult.cs
Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs
Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs
Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs
Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/AggregateActor.cs
Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Commands/SubmitCommandResponse.cs
Hexalith.EventStore/src/Hexalith.EventStore/Models/SubmitCommandResponse.cs
Hexalith.EventStore/src/Hexalith.EventStore/Controllers/CommandsController.cs

src/Hexalith.Parties.Contracts/Results/CompositeCommandResult.cs
src/Hexalith.Parties.Contracts/Models/PartyDetail.cs
src/Hexalith.Parties.Contracts/State/PartyState.cs
src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs
src/Hexalith.Parties/Program.cs
src/Hexalith.Parties.Client/Abstractions/IPartiesCommandClient.cs
src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs
src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs
src/Hexalith.Parties.Mcp/Tools/PartiesMcpToolResult.cs

tests/Hexalith.Parties.Server.Tests/Aggregates/
tests/Hexalith.Parties.Contracts.Tests/Results/CompositeCommandResultTests.cs
tests/Hexalith.Parties.Tests/Gateway/PartiesProcessEndpointTests.cs
tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs
tests/Hexalith.Parties.Client.Tests/HttpPartiesCommandClientTests.cs
tests/Hexalith.Parties.Mcp.Tests/PartiesMcpToolDispatchTests.cs
```

### Previous Story Intelligence

- Story 1.3 established that detail update success emits both the specific update event and `PartyDisplayNameDerived`; returned state must include the derived display/sort names after both events.
- Stories 1.4 and 1.5 established contact-channel and identifier mutation semantics; returned state must include add/update/remove effects and preferred-channel normalization.
- Story 1.6 kept lifecycle soft deactivation/reactivation as state changes, not deletion or erasure; returned state should preserve party data while reflecting `IsActive`.
- Story 1.7 clarified rejection/no-op semantics and typed rejection events. Rejections must not carry misleading success payloads.
- Story 1.8 reinforced privacy-safe logging. It is acceptable to return `PartyDetail` to the caller, but not to log raw payloads, party details, contact values, identifiers, or serialized response bodies.
- Recent commits show Epic 1 hardening has favored narrow contract and aggregate changes plus focused tests before broader solution builds.

### Testing Requirements

- Use xUnit v3 and Shouldly. Keep tests close to existing aggregate/client/gateway/MCP projects.
- Prefer direct aggregate tests for final-state assembly because they are fast and isolate event ordering.
- Gateway tests should prove that a successful result payload is preserved through the same path production uses, not through a test-only shortcut.
- Rejection tests should assert absence of result payload and absence of `PartyDetail`, not merely `IsRejection`.
- Avoid snapshotting full personal-data-bearing JSON. Assert selected synthetic fields and structural presence.
- Use synthetic test data from `PartyTestData` where possible. If adding values, use obvious placeholders and avoid real personal data.

### Anti-Patterns To Avoid

- Do not add public REST controllers, Swagger/OpenAPI, or in-process MCP hosting to `src/Hexalith.Parties`.
- Do not implement FR69 by making an immediate query after every command. The story requires the mutation response to carry the state produced by the aggregate turn.
- Do not add a separate Parties-only command gateway that bypasses EventStore authorization, idempotency, status tracking, or event persistence.
- Do not put response assembly in MCP or client code by replaying events there; aggregate/domain result plumbing owns the authoritative final state.
- Do not serialize result payloads into logs, command status failure reasons, telemetry dimensions, or exception details.
- Do not weaken tenant/RBAC ownership, Dapr ACLs, rejection-event apply ordering, or contract dependency boundaries.
- Do not recursively initialize or update nested submodules.

### Deferred Decisions

- If EventStore intentionally keeps public command submission as `202 Accepted`, this story should still carry the enriched payload where the synchronous command processing result is available and document any remaining async/status endpoint limitation for a later EventStore API design story.
- If simple no-op commands should return unchanged detail for UX convenience, that behavior needs an explicit architecture decision. Default for this story is no misleading updated-state payload on no-op.
- If adding payload to `SubmitCommandResponse` is judged to be an EventStore API compatibility change beyond this story, record the exact limitation and ensure Parties `/process` plus internal result plumbing are still ready.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.9] - Story statement and BDD acceptance criteria for FR69.
- [Source: _bmad-output/planning-artifacts/prd.md#Developer-Integration-MVP] - FR69 update operations return updated party state, with command integration expectations.
- [Source: _bmad-output/planning-artifacts/architecture.md#Aggregate] - Aggregate `Handle` methods are synchronous and return `DomainResult`/`CompositeCommandResult`.
- [Source: _bmad-output/planning-artifacts/architecture.md#API-Data-Format-Patterns] - REST, JSON, and MCP response shape conventions.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - Actor-host, EventStore ownership, privacy, testing, and submodule guardrails.
- [Source: _bmad-output/implementation-artifacts/1-8-personal-data-marking-and-log-safe-domain-model.md] - Privacy-safe logging and previous-story guardrails.
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Results/DomainResult.cs] - Existing `ResultPayload` hook.
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs] - Current domain-service wire response shape.
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs] - Existing command-processing payload field.
- [Source: src/Hexalith.Parties.Contracts/Results/CompositeCommandResult.cs] - Existing `UpdatedPartyDetail` property.
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs] - Existing mutation handlers and `BuildPartyDetailFromState` helper.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-05-16: Story created by BMAD pre-dev context workflow with FR69 result-payload propagation, aggregate final-state assembly, EventStore wire-response, client, MCP, and privacy-safe testing guidance.
