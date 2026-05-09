# EventStore-to-Parties Actor Invocation Feasibility Spike

Date/time: 2026-05-09 16:34 Europe/Paris

Story: 12.0 - EventStore-to-Parties Actor Invocation Feasibility Spike

Verdict: partial

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
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`
  - Uses `TryAddTransient<IDomainServiceInvoker, DaprDomainServiceInvoker>()`, so it does not replace the already-registered Parties invoker.
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`
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
- EventStore authentication:
  - Local test token must include `sub=spike-user` and global-admin or tenant authorization appropriate for tenant `spike-tenant`.
  - If auth is bypassed in test harness, document it as a local-only shortcut.

## Domain/App-id Matrix

| Submitted domain | Convention app-id | Static registration needed | Current result | Failure mode |
|---|---|---:|---|---|
| `party` | `party` | Yes, map to `parties/process` | Blocked | Current Parties sidecar app-id is `parties`, not `party`; convention misses the running app. |
| `parties` | `parties` | Not for app-id, but endpoint still required | Blocked | App-id matches current sidecar, but Parties does not expose `POST /process`; command type/domain also differ from existing `PartyDomainServiceInvoker` expectation. |
| `Parties` | `Parties` | Yes, map to `parties/process` | Blocked | Casing does not match current app-id and no `/process` endpoint exists. |

Selected domain string for follow-up proof: `party`, with explicit wildcard registration to `parties/process`. Reason: current Parties domain invoker accepts `party` case-insensitively, and this avoids silently changing domain naming policy during the spike.

## Command Evidence

Request sample for the next positive proof:

```json
{
  "messageId": "01HXSPK000000000000000001",
  "tenant": "spike-tenant",
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

Current evidence:

- EventStore command router sends `SubmitCommand` to `AggregateActor`.
- `AggregateActor` invokes `IDomainServiceInvoker`.
- In the current Parties host, that service is `PartyDomainServiceInvoker`, not `DaprDomainServiceInvoker`.
- Therefore command invocation through EventStore exists only as in-process Parties handling today; the remote DAPR Parties-hosted handler path is not proven.
- EventStore persistence could not be proven for the desired remote path because the desired path stops before a compatible `parties/process` domain service exists.

Command status: blocked by local topology and missing Parties domain-service endpoint.

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

Query status: blocked by EventStore contract mismatch in Parties projections. This is a local adapter/projection-contract blocker, not evidence that EventStore submodule changes are required.

## Feasibility Table

| Area | Status | Classification | Evidence |
|---|---|---|---|
| Command routing | blocked | local topology / missing endpoint | EventStore can invoke DAPR domain services, but Parties currently has no compatible `/process` endpoint and AppHost does not start separate EventStore API. |
| Event persistence | unknown | dependent on command routing | `AggregateActor` persists events after domain invocation, but remote Parties invocation did not reach that point in this topology. |
| Query routing | blocked | EventStore contract mismatch in Parties adapter | EventStore requires `IProjectionActor`; Parties actors currently expose custom actor interfaces. |
| Projection response | blocked | missing Parties query adapter | No deterministic response can be returned through EventStore until a compatible projection actor/adapter exists. |

## Commands Run

```powershell
rg -n "^" src/Hexalith.Parties.AppHost/Program.cs
rg -n "^" src/Hexalith.Parties.Aspire/HexalithPartiesExtensions.cs
rg -n "^" src/Hexalith.Parties/Program.cs
rg -n "DomainService|PartyDomainServiceInvoker|Add|Dapr|Projection|Controller|MCP|EventStore" src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs
rg -n "^" src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs
rg -n "^" Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs
rg -n "^" Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs
rg -n "^" Hexalith.EventStore/src/Hexalith.EventStore.Server/Commands/CommandRouter.cs
rg -n "^" Hexalith.EventStore/src/Hexalith.EventStore.Server/Queries/QueryRouter.cs
rg -n "^" Hexalith.EventStore/samples/Hexalith.EventStore.Sample/DomainServiceRequestRouter.cs
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
