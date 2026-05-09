# Story 12.0: EventStore-to-Parties Actor Invocation Feasibility Spike

Status: ready-for-dev

## Story

As a platform engineer,
I want to verify that EventStore's `SubmitCommand` and `SubmitQuery` MediatR handlers can route to Parties-hosted actors or domain handlers without changes to the EventStore submodule,
so that the rest of Epic 12 can proceed without an upstream platform dependency.

## Acceptance Criteria

1. Given a minimal AppHost topology wiring `eventstore` and a stub or adapted `parties` domain service, when `POST /api/v1/commands` is submitted with the Parties domain, `CommandType="CreateParty"`, and a valid payload, then the corresponding Parties command processor is invoked through EventStore and a Party event is persisted by EventStore, with evidence capturing request/response status, the resolved domain/app-id, a Parties-side invocation marker, stream/category, event type, aggregate id, tenant/domain metadata where available, and correlation or command id where available.
2. Given the same topology, when `POST /api/v1/queries` is submitted with the Parties domain, then the Parties query/projection handler path is invoked through EventStore and returns a deterministic response for the documented payload; if a deterministic query cannot be produced inside the spike window, the conclusion must classify the blocker as routing, projection materialization, DAPR invocation, missing Parties query handler, tenant/auth dependency, or EventStore contract limitation.
3. Given the spike result, then a written conclusion records a dated `yes`, `no`, `partial`, or `not-proven` feasibility recommendation; the exact DAPR/EventStore configuration required; domain/app-id naming tested and selected; commands run; request/response samples; logs or trace evidence; EventStore persistence evidence; query evidence or blocker classification; limitations; and required follow-up decisions.
4. Given this is a feasibility spike, then implementation is time-boxed to 2 working days; a negative result blocks Epic 12 only when concrete evidence shows that routing, persistence, or query proof requires an EventStore submodule change or cannot be achieved with local AppHost/config/test-harness changes outside the submodule.

## Party-Mode Clarifications

- Feasibility means the routing and persistence/query paths are proven without EventStore submodule source changes. It does not mean production security, multi-tenant policy, operational readiness, retry behavior, or final projection architecture are complete.
- The spike must use the smallest reproducible topology: EventStore API, EventStore state/event storage, DAPR sidecars, Parties app/actor host, and only the projection/query component needed for AC2. Additional services must be justified in the conclusion.
- Allowed changes are limited to Parties repository AppHost/config/test harness/spike artifacts outside `Hexalith.EventStore`. EventStore submodule source edits are forbidden for a positive local workaround; if required, they are the spike result.
- Treat `party`, `parties`, and `Parties` as explicit test inputs until the spike proves the working convention. Do not standardize global naming policy in this story unless an existing hard requirement is discovered.
- Use deterministic spike-only tenant and actor context values in requests, logs, and evidence. If auth is bypassed, mocked, or configured with local test credentials, record the exact values and state that production auth/tenant propagation remains out of scope.
- A command-path success requires both EventStore API acceptance and evidence that a Parties-hosted handler executed. Logs alone are not enough unless they are tied to a correlation or command id and paired with EventStore persistence evidence.
- A two-day timeout without an incompatible platform behavior is `not-proven`, not automatically `no`. Classify command routing, event persistence, query routing, and projection response separately as `works`, `blocked`, `not-attempted`, or `unknown`.
- The spike conclusion should be a dated artifact, preferably `docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md`, and should include copy-pastable reproduction commands from a clean checkout. Do not require nested submodule initialization unless a human explicitly requests it.

## Tasks / Subtasks

- [ ] Establish the smallest topology that can prove command routing. (AC: 1)
  - [ ] Start from `src/Hexalith.Parties.AppHost/Program.cs`, but do not perform the full Story 12.1 recomposition.
  - [ ] Mirror the EventStore AppHost pattern from `Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs`: `eventstore`, DAPR sidecar, shared `statestore`, shared `pubsub`, and a domain-service sidecar for `parties`.
  - [ ] Confirm whether convention routing is enough (`Domain` -> DAPR `appId`) or whether a static `DomainServiceOptions.Registrations` entry is required.
  - [ ] Record the exact working domain string. Current Parties code uses `party`; the sprint-change proposal says `Parties`. Do not leave this ambiguous.
  - [ ] Complete a domain/app-id matrix for `party`, `parties`, and `Parties`, recording submitted domain string, resolved DAPR app-id, method/actor target, result, and observed failure mode for non-working candidates.
  - [ ] Record deterministic tenant/user/auth test values used by the harness, including whether they are mocked, bypassed, or configured as local test credentials.
- [ ] Prove command invocation through EventStore. (AC: 1)
  - [ ] Use a valid `CreateParty` payload from `src/Hexalith.Parties.Contracts/Commands/CreateParty.cs`.
  - [ ] Confirm EventStore's `/api/v1/commands` path reaches a Parties-hosted processor through `DaprDomainServiceInvoker`, not the existing in-process `PartyDomainServiceInvoker`.
  - [ ] Confirm a persisted event appears under EventStore's stream format, not a Parties-private persistence path.
  - [ ] Capture the request body, response status, resolved domain/app-id, Parties-side invocation marker, stream/category, event type, aggregate id, tenant/domain metadata where available, and correlation or command id where available in the spike conclusion.
- [ ] Prove query routing independently. (AC: 2)
  - [ ] Prefer a minimal query/projection stub if current Parties projection actors cannot implement EventStore's generic `IProjectionActor` contract in the spike window.
  - [ ] If using real Parties projections, document any adapter required between `PartyDetailProjectionActor` / `PartyIndexProjectionActor` and EventStore's `ProjectionActor` query contract.
  - [ ] Capture the request body, response status, actor type, actor id, response payload, seed data or party id used, and deterministic response assertions in the spike conclusion.
  - [ ] If query proof is blocked, classify the blocker as routing, projection materialization, DAPR invocation, missing Parties query handler, tenant/auth dependency, or EventStore contract limitation.
- [ ] Identify blockers without silently widening scope. (AC: 3, 4)
  - [ ] Classify any required EventStore change as a platform dependency, not a local workaround.
  - [ ] If DAPR actor placement, actor type names, domain casing, payload serialization, tenant headers, or auth boundaries block the flow, record the smallest failing reproduction.
  - [ ] Do not remove Parties controllers, MCP tools, or current AppHost wiring in this story; those belong to later Epic 12 stories.
  - [ ] Stop after the minimal reproducible proof or documented blocker. Do not build production abstractions, broad topology rewrites, durable retry policy, permanent auth/tenant model, or full projection architecture.
  - [ ] Record final verification that `Hexalith.EventStore` has no modified tracked files or submodule-source edits.
- [ ] Write the feasibility conclusion. (AC: 3, 4)
  - [ ] Add a `## Spike Conclusion` section to this file with date/time, outcome, exact topology, commands run, findings, and next-story guidance.
  - [ ] Create or link a dated spike note, preferably `docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md`, with sections for verdict, topology, configuration, reproduction commands, command evidence, query evidence, domain/app-id matrix, limitations, blocker classification, and follow-up decisions.
  - [ ] Include a feasibility table with command routing, event persistence, query routing, and projection response marked as `works`, `blocked`, `not-attempted`, or `unknown`.
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
| 2026-05-09 | 0.2 | Party-mode review applied low-risk clarifications for spike evidence, domain/app-id matrix, query blocker classification, stop rules, tenant/auth evidence, dated conclusion artifact, and EventStore submodule verification. | Codex |

## Party-Mode Review

- Date/time: 2026-05-09T13:52:39Z
- Selected story key: 12-0-eventstore-parties-actor-invocation-feasibility-spike
- Command/skill invocation used: `/bmad-party-mode 12-0-eventstore-parties-actor-invocation-feasibility-spike; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: reviewers agreed the spike was directionally sound but needed sharper proof boundaries before development. The main risks were ambiguous domain/app-id naming, insufficient separation between EventStore API acceptance and Parties handler execution, vague EventStore persistence proof, under-specified query determinism, unclear tenant/auth shortcuts, accidental EventStore submodule edits, and false `no` conclusions from time-box exhaustion rather than observed platform incompatibility.
- Changes applied: clarified AC1-AC4 evidence requirements; added Party-Mode Clarifications; added domain/app-id matrix, tenant/auth evidence, command persistence proof, query blocker classification, minimal topology, stop-rule, no-EventStore-edit verification, dated spike report, feasibility table, and reproduction-command requirements.
- Findings deferred: final canonical domain naming; production auth and tenant propagation model; production topology policy; durable retry/operational readiness; full query/projection architecture; any EventStore platform change unless the spike proves it is required.
- Final recommendation: `ready-for-dev`
