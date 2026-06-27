# EventStore-to-Parties Actor Invocation Feasibility Spike

Date/time: 2026-05-09 16:34 Europe/Paris

Story: 12.0 - EventStore-to-Parties Actor Invocation Feasibility Spike

Verdict: partial

> **Verdict basis: static-analysis only.** No DAPR runtime round-trip, no `aspire run`, no `POST /api/v1/commands` was executed. Every entry in the feasibility table, command evidence, query evidence, and domain/app-id matrix below was derived from reading source — not from observing runtime behavior. Per the Party-Mode Clarification "logs alone are not enough" and "a two-day timeout without an incompatible platform behavior is `not-proven`, not automatically `no`", the `partial` label here means: *static reading shows no EventStore submodule change is required and surfaces concrete topology gaps; a runtime proof remains outstanding for Story 12.1+ to upgrade `blocked`/`unknown` rows to evidence-backed status*.

## Summary

EventStore already contains the required remote invocation seam for commands: `DomainServiceResolver` can resolve static registrations, wildcard registrations, config-store registrations, or convention fallback, and `DaprDomainServiceInvoker` calls the resolved DAPR app and method with `DomainServiceRequest`.

The current Parties repository does not yet prove the desired EventStore API -> DAPR -> Parties actor/domain host path. The current `Hexalith.Parties.AppHost` starts `parties` and `tenants`, then `AddHexalithParties` wraps the `parties` resource as the EventStore API resource instead of starting a separate `eventstore` project. The `parties` service also does not expose the EventStore sample-style `POST /process` endpoint expected by `DaprDomainServiceInvoker`, and `AddParties` intentionally registers `PartyDomainServiceInvoker` before `AddEventStoreServer`, leaving the command handler on the in-process domain invoker path.

No EventStore submodule source change is indicated by this spike. The blockers are local Parties topology and adapter work: compose a separate EventStore API resource, expose a Parties domain-service endpoint compatible with `DomainServiceRequest`/`DomainServiceWireResult`, and adapt Parties projections to EventStore's generic query actor contract or explicitly route to compatible actor types.

## Topology Inspected

- `src/Hexalith.Parties.AppHost/Program.cs`
  - Starts `parties` with DAPR app-id `parties`.
  - Starts `tenants`.
  - Delegates to `AddHexalithParties(parties, ...)`.
  - Does not start `Projects.Hexalith_EventStore` as a distinct `eventstore` API resource.
- `src/Hexalith.Parties.Aspire/HexalithPartiesExtensions.cs`
  - Adds a DAPR sidecar for `parties` with app-id `parties`.
  - Returns `HexalithEventStoreResources(stateStore, pubSub, parties, parties)`, so the same `parties` resource acts as both EventStore command API and admin resource.
- `src/Hexalith.Parties/Program.cs`
  - Maps controllers, subscriptions, MCP, actors, and default endpoints.
  - Does not map `POST /process`.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`
  - Registers `IDomainServiceInvoker` as `PartyDomainServiceInvoker` before calling `AddEventStoreServer`.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`
  - Uses `TryAddTransient<IDomainServiceInvoker, DaprDomainServiceInvoker>()`, so it does not replace the already-registered Parties invoker.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`
  - Defaults to actor type `ProjectionActor`, with optional `ProjectionActorType`.
- `src/Hexalith.Parties.Projections/Actors`
  - `PartyDetailProjectionActor` and `PartyIndexProjectionActor` do not implement EventStore's `IProjectionActor` contract.

## Configuration Required For A Positive Follow-up

Minimum local topology for the next proof:

- AppHost resources:
  - `eventstore` API project with DAPR app-id `eventstore`.
  - `parties` domain/actor host with DAPR app-id `parties`.
  - Shared `statestore` with `actorStateStore=true` and `keyPrefix=none`.
  - Shared `pubsub`.
- EventStore domain-service registration:
  - Preferred explicit registration: `*|party|v1 -> AppId=parties, MethodName=process`.
  - Convention-only fallback is not enough for the currently selected domain because `Domain=party` resolves to app-id `party`, while the current AppHost uses app-id `parties`.
- Parties domain-service endpoint:
  - `POST /process` accepting `DomainServiceRequest`.
  - Returns `DomainServiceWireResult.FromDomainResult(...)`.
  - Invokes the existing Parties aggregate/domain logic and preserves payload protection behavior or clearly documents any spike-only bypass.
- EventStore authentication (forward-looking — no harness was actually run):
  - When the next runtime proof attempts a `POST /api/v1/commands`, the local test token should include `sub=spike-user` and global-admin or tenant authorization appropriate for tenant `spike-tenant`.
  - If auth is bypassed in that future harness, the bypass should be documented as a local-only shortcut. Production auth/tenant propagation remains out of scope for this spike and any successor.

## Domain/App-id Matrix

| Submitted domain | Convention app-id | Static registration needed | Current result | Failure mode |
|---|---|---:|---|---|
| `party` | `party` | Yes, map to `parties/process` | Blocked | Current Parties sidecar app-id is `parties`, not `party`; convention misses the running app. |
| `parties` | `parties` | Not for app-id, but endpoint still required | Blocked | App-id matches current sidecar, but Parties does not expose `POST /process`; command type/domain also differ from existing `PartyDomainServiceInvoker` expectation. |
| `Parties` | `Parties` | Yes, map to `parties/process` | Blocked | Resolver passes domain casing through unchanged, so it would resolve to app-id `Parties`. The actual failure surface is DAPR sidecar app-id casing convention (sidecar app-ids are conventionally lowercased) — a sidecar registered as `parties` will not answer for app-id `Parties`. No `/process` endpoint exists either. |

> **Matrix is theoretical.** None of the three rows was actually submitted to a running EventStore API; the failure modes are predicted from code reading. The "Current result" column reflects what static analysis predicts, not observed behavior.

Selected domain string for follow-up proof: `party`, with explicit wildcard registration to `parties/process`. Reason: current Parties domain invoker accepts `party` case-insensitively, and this avoids silently changing domain naming policy during the spike. Note that for *every* row in the matrix, an explicit static registration is required — convention routing alone is insufficient because the running sidecar's app-id (`parties`) does not match the convention-derived app-id for any of the three submitted-domain candidates.

## Command Evidence

> **Illustrative pseudo-payload — not a literal POST body.** The JSON below mirrors AC1's evidence shape for human readability. The real wire format is `Hexalith.EventStore.Contracts.Commands.CommandEnvelope` (`TenantId`, `UserId`, `MessageId`, `CommandType`, `Payload` as `byte[]`, `CorrelationId`, etc., with throw-on-empty validation on most fields). A future runtime proof must serialize against `CommandEnvelope`, not against this shape; do not paste this JSON into a `POST /api/v1/commands` request and expect it to deserialize.

Illustrative request sample for the next positive proof:

```json
{
  "messageId": "01HXSPK4N3T8X9Q5K7B2D3F6Y8",
  "tenant": "spike-tenant",
  "userId": "spike-user",
  "domain": "party",
  "aggregateId": "party-spike-001",
  "commandType": "CreateParty",
  "payload": {
    "partyId": "party-spike-001",
    "type": "Person",
    "personDetails": {
      "firstName": "Spike",
      "lastName": "Person"
    }
  },
  "correlationId": "corr-spike-12-0-command",
  "extensions": {
    "domain-service-version": "v1"
  }
}
```

Current evidence (static analysis):

- EventStore command router sends `SubmitCommand` to `AggregateActor`.
- `AggregateActor` invokes `IDomainServiceInvoker`.
- In the current Parties host, that service is `PartyDomainServiceInvoker`, not `DaprDomainServiceInvoker`.
- Therefore command invocation through EventStore exists only as in-process Parties handling today; the remote DAPR Parties-hosted handler path is not proven.
- EventStore persistence could not be proven for the desired remote path because the desired path stops before a compatible `parties/process` domain service exists.
- Note: AC1's lower bound — "EventStore invokes a Parties processor and persists" — may already be satisfied by the in-process path today; this was not exercised by the spike either, and is left as a follow-up validation if the runtime proof in 12.1+ chooses to record it.

Command status: classified `blocked` based on static analysis. No runtime attempt was made, so this is "predicted blocked" not "observed blocked". Per Party-Mode Clarification, a true `blocked` would require a failing runtime attempt; treat this row as `not-attempted` for evidence purposes until 12.1 runs the proof.

## Logs/Trace Evidence

No runtime logs or traces were captured in this spike. AC3 requires this section; recording its absence explicitly: no `aspire run`, no DAPR sidecar logs, no OTel traces, no correlation-id flow was observed. Future runtime proof in Story 12.1+ should capture: resolved domain registration, DAPR `AppId`, method name, actor type, actor id, tenant, domain, and correlation id — at minimum from EventStore command-router logs and DAPR sidecar invocation logs.

## EventStore Persistence Evidence

No EventStore persistence evidence was captured. AC3 requires this section; recording its absence explicitly: no event was written to EventStore's stream/state format during this spike, because the remote command path stops before invocation. Future runtime proof should record: stream/category, event type, aggregate id, and persisted-event count via an EventStore admin or stream-read query after a successful command.

## Query Evidence

Request sample for the next positive proof:

```json
{
  "tenant": "spike-tenant",
  "domain": "party",
  "aggregateId": "party-spike-001",
  "queryType": "GetParty",
  "projectionType": "party",
  "entityId": "party-spike-001",
  "projectionActorType": "PartyDetailProjectionActor",
  "payload": {
    "partyId": "party-spike-001"
  }
}
```

Current evidence:

- EventStore's query path is actor-based, not domain-service invocation based.
- The default actor type is `ProjectionActor`.
- Request payloads can override `ProjectionActorType`, but EventStore still calls the `IProjectionActor.QueryAsync(QueryEnvelope)` contract.
- Current Parties projection actors expose `IPartyDetailProjectionActor` and `IPartyIndexProjectionActor`, not EventStore's `IProjectionActor`.

Query status: classified `blocked` per AC2 vocabulary as **missing Parties query handler** (the Parties side has no actor implementing EventStore's `IProjectionActor` contract). This is a local adapter/projection-contract blocker, not evidence that EventStore submodule changes are required. As with command routing, this is "predicted blocked" from static analysis — no `POST /api/v1/queries` was attempted.

## Feasibility Table

| Area | Status | Classification (AC2 vocabulary) | Evidence basis |
|---|---|---|---|
| Command routing | blocked (predicted; not-attempted at runtime) | routing — local topology / missing endpoint | Static analysis: EventStore can invoke DAPR domain services, but Parties currently has no compatible `/process` endpoint and AppHost does not start separate EventStore API. |
| Event persistence | unknown (not-attempted) | dependent on command routing; also depends on shared state-store config (`actorStateStore=true`, `keyPrefix=none`) not yet verified | Static analysis: `AggregateActor` persists events after domain invocation, but remote Parties invocation did not reach that point in this topology. |
| Query routing | blocked (predicted; not-attempted at runtime) | EventStore contract limitation — generic `IProjectionActor` contract not implemented by Parties projections | Static analysis: EventStore requires `IProjectionActor`; Parties actors currently expose custom actor interfaces. |
| Projection response | blocked (predicted; not-attempted at runtime) | missing Parties query handler — no projection actor/adapter implements `IProjectionActor.QueryAsync(QueryEnvelope)` | Static analysis: no deterministic response can be returned through EventStore until a compatible projection actor/adapter exists. |

> All four rows are classified from static source inspection only; convert to evidence-backed `works`/`blocked`/`not-attempted` during the Story 12.1+ runtime proof.

## Commands Run

```powershell
rg -n "^" src/Hexalith.Parties.AppHost/Program.cs
rg -n "^" src/Hexalith.Parties.Aspire/HexalithPartiesExtensions.cs
rg -n "^" src/Hexalith.Parties/Program.cs
rg -n "DomainService|PartyDomainServiceInvoker|Add|Dapr|Projection|Controller|MCP|EventStore" src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs
rg -n "^" src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs
rg -n "^" references/Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs
rg -n "^" references/Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs
rg -n "^" references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Commands/CommandRouter.cs
rg -n "^" references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Queries/QueryRouter.cs
rg -n "^" references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample/DomainServiceRequestRouter.cs
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter FullyQualifiedName~EventStorePartiesInvocationSpikeTests --no-restore
git -C Hexalith.EventStore status --short
```

## Limitations

- No DAPR runtime round-trip was executed in this spike because the currently composed topology lacks the separate EventStore API and Parties `/process` endpoint required to run the requested flow.
- No EventStore persistence evidence exists for remote Parties invocation yet.
- The command payload is documented as the reproduction input for the next positive proof, but not accepted by the current remote path.
- Production auth, tenant propagation, DAPR ACL hardening, retry behavior, and final projection architecture remain out of scope.

## Follow-up Decisions

1. Story 12.1 should compose a separate `eventstore` resource and a separate `parties` actor/domain host resource sharing state store and pub/sub.
2. Story 12.2 or a narrow spike follow-up should add a Parties domain-service endpoint compatible with `DomainServiceRequest` and `DomainServiceWireResult`.
3. Keep `party` as the tested command domain unless product architecture explicitly renames it; register `*|party|v1` to `parties/process`.
4. Add a query adapter that implements EventStore's `IProjectionActor` contract or a small compatible spike actor before claiming AC2 success.
5. Do not change `Hexalith.EventStore` submodule source for this path unless the next runtime proof surfaces a concrete platform limitation.
