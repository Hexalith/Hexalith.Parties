# Story 12.0: EventStore-to-Parties Actor Invocation Feasibility Spike

Status: ready-for-dev

## Story

As a platform engineer,
I want to verify that EventStore's `SubmitCommand` and `SubmitQuery` MediatR handlers can route to Parties-hosted actors or domain handlers without changes to the EventStore submodule,
so that the rest of Epic 12 can proceed without an upstream platform dependency.

## Acceptance Criteria

1. Given a minimal AppHost topology wiring `eventstore` and a stub or adapted `parties` domain service, when `POST /api/v1/commands` is submitted with the Parties domain, `CommandType="CreateParty"`, and a valid payload, then the corresponding Parties command processor is invoked through EventStore and a Party event is persisted by EventStore.
2. Given the same topology, when `POST /api/v1/queries` is submitted with the Parties domain, then the corresponding query path is invoked through EventStore and returns a deterministic projection/query response.
3. Given the spike result, then a written conclusion documents feasibility `yes` or `no`, the exact DAPR/EventStore configuration required, the domain/app-id naming used, and any platform-level limitation discovered.
4. Given this is a feasibility spike, then implementation is time-boxed to 2 working days; a negative result blocks Epic 12 and triggers a follow-up sprint change targeting the EventStore submodule.

## Tasks / Subtasks

- [ ] Establish the smallest topology that can prove command routing. (AC: 1)
  - [ ] Start from `src/Hexalith.Parties.AppHost/Program.cs`, but do not perform the full Story 12.1 recomposition.
  - [ ] Mirror the EventStore AppHost pattern from `Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs`: `eventstore`, DAPR sidecar, shared `statestore`, shared `pubsub`, and a domain-service sidecar for `parties`.
  - [ ] Confirm whether convention routing is enough (`Domain` -> DAPR `appId`) or whether a static `DomainServiceOptions.Registrations` entry is required.
  - [ ] Record the exact working domain string. Current Parties code uses `party`; the sprint-change proposal says `Parties`. Do not leave this ambiguous.
- [ ] Prove command invocation through EventStore. (AC: 1)
  - [ ] Use a valid `CreateParty` payload from `src/Hexalith.Parties.Contracts/Commands/CreateParty.cs`.
  - [ ] Confirm EventStore's `/api/v1/commands` path reaches a Parties-hosted processor through `DaprDomainServiceInvoker`, not the existing in-process `PartyDomainServiceInvoker`.
  - [ ] Confirm a persisted event appears under EventStore's stream format, not a Parties-private persistence path.
  - [ ] Capture the request body, response status, and observable persistence evidence in the spike conclusion.
- [ ] Prove query routing independently. (AC: 2)
  - [ ] Prefer a minimal query/projection stub if current Parties projection actors cannot implement EventStore's generic `IProjectionActor` contract in the spike window.
  - [ ] If using real Parties projections, document any adapter required between `PartyDetailProjectionActor` / `PartyIndexProjectionActor` and EventStore's `ProjectionActor` query contract.
  - [ ] Capture the request body, response status, actor type, actor id, and response payload in the spike conclusion.
- [ ] Identify blockers without silently widening scope. (AC: 3, 4)
  - [ ] Classify any required EventStore change as a platform dependency, not a local workaround.
  - [ ] If DAPR actor placement, actor type names, domain casing, payload serialization, tenant headers, or auth boundaries block the flow, record the smallest failing reproduction.
  - [ ] Do not remove Parties controllers, MCP tools, or current AppHost wiring in this story; those belong to later Epic 12 stories.
- [ ] Write the feasibility conclusion. (AC: 3, 4)
  - [ ] Add a `## Spike Conclusion` section to this file with date/time, outcome, exact topology, commands run, findings, and next-story guidance.
  - [ ] If the outcome is negative, update `sprint-status.yaml` only according to the story workflow used by the dev agent; do not mark later Epic 12 stories ready.

## Dev Notes

### Source Context

- Epic 12 is not yet present in `_bmad-output/planning-artifacts/epics.md`; use `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md` as the authoritative Epic 12 source for this story.
- The pivot decision is that client commands and queries go to EventStore, and EventStore routes to a Parties actor/domain host through DAPR. Parties stops owning public REST/MCP entry points in later stories, but this spike must not perform that cleanup.
- Story 12.0 is the gate for the rest of Epic 12. If EventStore cannot invoke a separate Parties app without changing the submodule, later Epic 12 stories must wait for a sprint change.

### Current Implementation to Inspect

- `src/Hexalith.Parties.AppHost/Program.cs` currently starts `parties` and `tenants`, then delegates through `AddHexalithParties`; it does not explicitly start `eventstore`, `eventstore-admin`, or `eventstore-admin-ui`.
- `src/Hexalith.Parties/Program.cs` currently maps controllers, MCP, tenant subscriptions, actor handlers, and default endpoints. Later stories will remove controllers and MCP from this project; this spike should only prove routing.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` currently registers `PartyDomainServiceInvoker` before EventStore server infrastructure. That custom invoker handles Parties encryption/redaction behavior and is an implementation trap: EventStore's remote invocation path uses `DaprDomainServiceInvoker`, so the spike must prove whether a Parties remote domain service endpoint can preserve the needed behavior.
- `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs` accepts only the `party` domain and invokes `PartyAggregate` in-process. This may conflict with the sprint proposal's `Domain="Parties"` wording.
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` resolves domain services from static registrations, wildcard registrations, an optional config store, or convention fallback where `AppId` equals the submitted domain and `MethodName` is `process`.
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` invokes the resolved app/method with a `DomainServiceRequest` and expects a `DomainServiceWireResult`.
- `Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Program.cs` exposes `POST /process`; `DomainServiceRequestRouter.ProcessAsync` resolves a keyed `IDomainProcessor` by `request.Command.Domain`.
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/Commands/CommandRouter.cs` routes submitted commands to EventStore's `AggregateActor`; that actor then invokes the domain service. The spike must verify the full chain, not only direct DAPR service invocation.
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` routes queries to actor type `ProjectionActor` unless `ProjectionActorType` is supplied. Current Parties projection actors use `PartyDetailProjectionActor` and `PartyIndexProjectionActor`, not the generic EventStore projection contract.

### Technical Constraints

- Keep package versions aligned with the repo: .NET SDK `10.0.103`, `net10.0`, Aspire `13.2.2`, CommunityToolkit Aspire DAPR `13.0.0`, Dapr.Client/Dapr.AspNetCore `1.17.7`, and Dapr.Actors `1.16.1`.
- Do not initialize or update nested submodules. The `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.FrontComposer`, and `Hexalith.Memories` folders are root-level submodules already present in the workspace.
- Do not edit the EventStore submodule as part of a positive-path local workaround. If a platform change is needed, document it as the spike result and stop.
- Keep DAPR sidecar app IDs explicit. Test at least the casing mismatch between `party`, `parties`, and `Parties` before choosing the documented path.
- Keep tenant/auth boundaries clear. This spike can use local/dev auth shortcuts to prove transport, but the conclusion must say whether the shortcut bypasses the future EventStore gateway authorization boundary.
- A command-only success is not enough. AC2 requires a query proof or a documented blocker that explains exactly why the current EventStore query contract cannot reach Parties projections.

### Testing Guidance

- Prefer a focused spike harness or integration test that can be deleted or promoted later. Do not rewrite broad Parties controller or MCP tests in this story.
- Minimum verification evidence:
  - EventStore `/api/v1/commands` accepts a Parties command and reaches the Parties processor.
  - EventStore persists the resulting event in its own stream/state format.
  - EventStore `/api/v1/queries` reaches a projection/query actor or produces a documented contract blocker.
  - Logs identify the resolved domain registration, `AppId`, method name, actor type, actor id, tenant, domain, and correlation id.
- If using Aspire locally, record the exact resources visible in the dashboard and any ports/URLs used. Story 12.1 will formalize the topology after this spike.

### Out of Scope

- Full AppHost recomposition, EventStore Admin UI wiring, and DAPR component split. Those belong to Story 12.1.
- Removing Parties controllers, MCP registration, middleware, or public surface. That belongs to Story 12.2.
- Moving validators or tenant authorization ownership. That belongs to Story 12.3.
- Rewriting integration test suites. That belongs to Story 12.4.
- Changing the EventStore submodule. A required EventStore change is the spike result, not hidden work inside this repo.

### References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md` - Epic 12 scope, Story 12.0 ACs, pivot rationale, risks, and out-of-scope rules.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` - Epic 12 story order and ready/backlog state.
- `src/Hexalith.Parties.AppHost/Program.cs` - current Parties/Tenants AppHost shape.
- `src/Hexalith.Parties/Program.cs` - current Parties public mappings and actor hosting.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` - current EventStore server, custom domain invoker, projection, validation, MCP, and controller registrations.
- `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs` - current in-process Parties aggregate invocation.
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` - domain-to-DAPR app resolution.
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` - remote domain service invocation contract.
- `Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Program.cs` and `Hexalith.EventStore/samples/Hexalith.EventStore.Sample/DomainServiceRequestRouter.cs` - canonical remote domain service shape.

## Project Structure Notes

- Spike artifacts should stay isolated. Prefer a small test/spike project or documented local harness over production refactors.
- Any temporary code must be easy to identify in the File List and either promoted by later stories or removed before marking the story done.
- If the spike creates a written conclusion outside this file, place it under `_bmad-output/implementation-artifacts/` or `docs/` and link it from `## Spike Conclusion`.

## Spike Conclusion

Pending development.

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
