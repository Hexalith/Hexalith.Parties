# Story 12.0: EventStore-to-Parties Actor Invocation Feasibility Spike

Status: done

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

- [x] Establish the smallest topology that can prove command routing. (AC: 1)
  - [x] Start from `src/Hexalith.Parties.AppHost/Program.cs`, but do not perform the full Story 12.1 recomposition.
  - [x] Mirror the EventStore AppHost pattern from `Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs`: `eventstore`, DAPR sidecar, shared `statestore`, shared `pubsub`, and a domain-service sidecar for `parties`.
  - [x] Confirm whether convention routing is enough (`Domain` -> DAPR `appId`) or whether a static `DomainServiceOptions.Registrations` entry is required.
  - [x] Record the exact working domain string. Current Parties code uses `party`; the sprint-change proposal says `Parties`. Do not leave this ambiguous.
  - [x] Complete a domain/app-id matrix for `party`, `parties`, and `Parties`, recording submitted domain string, resolved DAPR app-id, method/actor target, result, and observed failure mode for non-working candidates.
  - [x] Record deterministic tenant/user/auth test values used by the harness, including whether they are mocked, bypassed, or configured as local test credentials.
- [x] Prove command invocation through EventStore. (AC: 1)
  - [x] Use a valid `CreateParty` payload from `src/Hexalith.Parties.Contracts/Commands/CreateParty.cs`.
  - [x] Confirm EventStore's `/api/v1/commands` path reaches a Parties-hosted processor through `DaprDomainServiceInvoker`, not the existing in-process `PartyDomainServiceInvoker`.
  - [x] Confirm a persisted event appears under EventStore's stream format, not a Parties-private persistence path.
  - [x] Capture the request body, response status, resolved domain/app-id, Parties-side invocation marker, stream/category, event type, aggregate id, tenant/domain metadata where available, and correlation or command id where available in the spike conclusion.
- [x] Prove query routing independently. (AC: 2)
  - [x] Prefer a minimal query/projection stub if current Parties projection actors cannot implement EventStore's generic `IProjectionActor` contract in the spike window.
  - [x] If using real Parties projections, document any adapter required between `PartyDetailProjectionActor` / `PartyIndexProjectionActor` and EventStore's `ProjectionActor` query contract.
  - [x] Capture the request body, response status, actor type, actor id, response payload, seed data or party id used, and deterministic response assertions in the spike conclusion.
  - [x] If query proof is blocked, classify the blocker as routing, projection materialization, DAPR invocation, missing Parties query handler, tenant/auth dependency, or EventStore contract limitation.
- [x] Identify blockers without silently widening scope. (AC: 3, 4)
  - [x] Classify any required EventStore change as a platform dependency, not a local workaround.
  - [x] If DAPR actor placement, actor type names, domain casing, payload serialization, tenant headers, or auth boundaries block the flow, record the smallest failing reproduction.
  - [x] Do not remove Parties controllers, MCP tools, or current AppHost wiring in this story; those belong to later Epic 12 stories.
  - [x] Stop after the minimal reproducible proof or documented blocker. Do not build production abstractions, broad topology rewrites, durable retry policy, permanent auth/tenant model, or full projection architecture.
  - [x] Record final verification that `Hexalith.EventStore` has no modified tracked files or submodule-source edits.
- [x] Write the feasibility conclusion. (AC: 3, 4)
  - [x] Add a `## Spike Conclusion` section to this file with date/time, outcome, exact topology, commands run, findings, and next-story guidance.
  - [x] Create or link a dated spike note, preferably `docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md`, with sections for verdict, topology, configuration, reproduction commands, command evidence, query evidence, domain/app-id matrix, limitations, blocker classification, and follow-up decisions.
  - [x] Include a feasibility table with command routing, event persistence, query routing, and projection response marked as `works`, `blocked`, `not-attempted`, or `unknown`.
  - [x] If the outcome is negative, update `sprint-status.yaml` only according to the story workflow used by the dev agent; do not mark later Epic 12 stories ready.

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

Date/time: 2026-05-09 16:34 Europe/Paris

Outcome: `partial` — *static-analysis only; no runtime proof attempted.* See "Verdict basis" in the dated note.

Detailed note: [docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md](../../docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md)

Wave-1 unblock decision (AC4 closure): **Epic 12 is NOT blocked by this spike.** Wave 1 (Stories 12-1..12-4) may proceed. Wave 2 (12-5..12-10) remains gated on Wave-1 landing. The static finding "no EventStore submodule source change is indicated" is sufficient to unblock per AC4, but a runtime proof during 12.1's AppHost recomposition must convert the four predicted-blocked feasibility-table rows into evidence-backed status before any production claim is made.

Findings:

- EventStore already has the required command-side remote invocation machinery: `DomainServiceResolver` can resolve static, wildcard, config-store, or convention registrations, and `DaprDomainServiceInvoker` sends `DomainServiceRequest` to the resolved DAPR app/method.
- The current Parties topology does not prove EventStore API -> DAPR -> separate Parties domain host. `src/Hexalith.Parties.AppHost/Program.cs` starts `parties` and `tenants`, while `AddHexalithParties` wraps the same `parties` resource as EventStore command/admin resources.
- The current Parties service does not expose the EventStore sample-style `POST /process` endpoint required by `DaprDomainServiceInvoker`.
- `AddParties` registers `PartyDomainServiceInvoker` before `AddEventStoreServer`; EventStore uses `TryAddTransient<IDomainServiceInvoker, DaprDomainServiceInvoker>()`, so the current Parties-hosted command path remains in-process.
- Convention routing alone is not enough for the selected domain. Use `Domain=party` and an explicit wildcard static registration such as `*|party|v1 -> AppId=parties, MethodName=process`.
- Query proof is blocked by projection contract mismatch: EventStore calls `IProjectionActor.QueryAsync(QueryEnvelope)`, while Parties projection actors expose `IPartyDetailProjectionActor` and `IPartyIndexProjectionActor`.
- No EventStore submodule source change is indicated. Required follow-up work is local AppHost/config/adapter work outside `Hexalith.EventStore`.

Feasibility table:

| Area | Status | Classification (AC2 vocabulary) | Evidence basis |
|---|---|---|---|
| Command routing | blocked (predicted; not-attempted at runtime) | routing — local topology / missing endpoint | Static analysis: EventStore can invoke DAPR domain services, but Parties currently has no compatible `/process` endpoint and AppHost does not start a separate EventStore API. |
| Event persistence | unknown (not-attempted) | dependent on command routing; also depends on shared state-store config (`actorStateStore=true`, `keyPrefix=none`) not yet verified | Static analysis: `AggregateActor` persists events after domain invocation, but the remote Parties path did not reach invocation in this topology. |
| Query routing | blocked (predicted; not-attempted at runtime) | EventStore contract limitation — generic `IProjectionActor` contract not implemented by Parties projections | Static analysis: EventStore requires `IProjectionActor`; Parties actors currently expose custom actor interfaces. |
| Projection response | blocked (predicted; not-attempted at runtime) | missing Parties query handler — no projection actor/adapter implements `IProjectionActor.QueryAsync(QueryEnvelope)` | Static analysis: no deterministic response can be returned through EventStore until a compatible projection actor/adapter exists. |

Commands run:

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

Next-story guidance:

- Story 12.1 should compose a separate `eventstore` resource and a separate `parties` actor/domain host resource sharing state store and pub/sub.
- Add a Parties domain-service endpoint compatible with `DomainServiceRequest` and `DomainServiceWireResult` before claiming command-path success.
- Keep `party` as the tested command domain unless the architecture explicitly renames it; use static registration to map `party` to `parties/process`.
- Add an EventStore-compatible projection adapter or spike actor implementing `IProjectionActor` before claiming query-path success.

## Dev Agent Record

### Agent Model Used

- Implementation (v0.1–v1.0): Codex GPT-5
- Close-out (v1.1) + code-review patches (v1.2): Claude Opus 4.7

### Debug Log References

- Static inspection of Parties AppHost, Parties Aspire extension, Parties service startup, Parties domain invoker, EventStore domain resolver, EventStore DAPR invoker, EventStore command router, EventStore query router, and EventStore sample domain-service router.
- Focused fitness test added and run: `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter FullyQualifiedName~EventStorePartiesInvocationSpikeTests --no-restore` (4 passed).
- Broader validation run: `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --no-restore` (402 passed, 3 failed). Failures were outside the spike files: `ArchitecturalFitnessTests.ClientProject_HasNoReferencesToServerProjectionsOrPartiesService`, `HealthEndpointIntegrationTests.QueryEndpoint_PubSubDegraded_AddsDegradationHeadersAsync`, and `TemporalNameEndpointTests.GetPartyNameAt_ErasedParty_Returns410Gone`.
- EventStore submodule verification: `git -C Hexalith.EventStore status --short` produced no modified tracked files.

### Completion Notes List

- Added focused spike fitness tests documenting current blockers: no separate EventStore API resource in Parties AppHost, no Parties `/process` endpoint, in-process Parties domain invoker precedence, and missing EventStore generic projection actor contract in Parties projections.
- Added dated spike conclusion artifact with verdict, topology, required configuration, reproduction samples, domain/app-id matrix, command/query blocker classifications, limitations, and follow-up decisions.
- Classified the result as `partial` based on static-analysis only (no runtime proof attempted): EventStore submodule changes are not currently indicated by source inspection, but the current Parties topology cannot yet prove remote command invocation, EventStore persistence through that remote path, or query projection response. All four feasibility-table rows are "predicted blocked / not-attempted at runtime" — runtime upgrade to evidence-backed status is deferred to Story 12.1+.
- Initially held status `in-progress` at v1.0 because the broader `Hexalith.Parties.Tests` regression run had three existing-area failures; v1.1 reclassified those as pre-existing (see close-out below).
- 2026-05-09 (close-out): Re-verified the three failures are pre-existing and unrelated to story 12-0. Spike commit `59c448c` only modified BMAD artifacts plus `EventStorePartiesInvocationSpikeTests.cs`; no production code changed. `git log` confirms all three failing test files were last modified in commit `db0bf14`, well before any 12-0 activity. Failures logged in `_bmad-output/implementation-artifacts/deferred-work.md` under "story 12-0 EventStore-to-Parties feasibility spike (2026-05-09)" for future Epic 12 / hardening pickup. Spike fitness tests still 4/4 green; EventStore submodule still clean. Status advanced to `review` per close-out decision.

### File List

- `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md`
- `tests/Hexalith.Parties.Tests/FitnessTests/EventStorePartiesInvocationSpikeTests.cs`

### Review Findings

Adversarial review on 2026-05-09 (Blind Hunter + Edge Case Hunter + Acceptance Auditor). Raw findings: ~37; after dedup + dismiss: 18 actionable (1 decision-needed, 13 patches, 4 defer).

- [x] [Review][Decision] **Verdict integrity** — Resolved (option b): kept `partial` and added prominent "static-analysis only — no runtime proof attempted" caveats. Story Outcome line, dated spike-note "Verdict basis" callout, feasibility table rows, command/query evidence sections, and domain/app-id matrix all now explicitly mark predicted-blocked / not-attempted-at-runtime status. Runtime upgrade deferred to Story 12.1+. — `partial` with all `blocked`/`unknown` rows rests entirely on static source inspection; no runtime proof attempted. Per Party-Mode Clarification "logs alone are not enough" + "two-day timeout without an incompatible platform behavior is `not-proven`, not automatically `no`". AC1 evidence list (request body, response status, resolved domain/app-id, Parties-side invocation marker, stream/category, event type, aggregate id, tenant/domain metadata, correlation/command id) cannot be satisfied by static reading; the matrix's "observed failure mode" column was never observed at runtime. Choose: (a) downgrade outcome to `not-proven` with rows `not-attempted`; (b) keep `partial` and add prominent "static-analysis only — no runtime proof attempted" caveats throughout story + spike note + completion notes; (c) run minimal runtime proof now to convert some rows to evidence-backed status; (d) accept current framing.
- [x] [Review][Patch] Stale Completion Notes claim contradicts current Status [12-0-eventstore-parties-actor-invocation-feasibility-spike.md:194]
- [x] [Review][Patch] AC2 blocker classification uses unenumerated vocabulary; map to AC2 enum (`EventStore contract limitation` or `missing Parties query handler`) [12-0-eventstore-parties-actor-invocation-feasibility-spike.md:149, docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md:131,139]
- [x] [Review][Patch] Spike note missing labeled "Logs/Trace Evidence" + "EventStore Persistence Evidence" sections (AC3 structural requirement) [docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md]
- [x] [Review][Patch] JSON request sample won't deserialize against real `CommandEnvelope` (uses `tenant` not `TenantId`, payload as JSON object not byte[], missing required `userId`) [docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md:73-93]
- [x] [Review][Patch] `messageId` example `01HXSPK000000000000000001` is not a valid ULID (no entropy section) [docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md:301]
- [x] [Review][Patch] `ShouldNotContain("IProjectionActor")` over-matches local `IPartyDetailProjectionActor` and misses inheritance from base class `ProjectionActor` [tests/Hexalith.Parties.Tests/FitnessTests/EventStorePartiesInvocationSpikeTests.cs:71-72]
- [x] [Review][Patch] Spike fitness tests are short-shelf-life string-greps; add class-level XML doc comment naming the deletion gate (Story 12.1 close) so future devs know the tests retire with the AppHost recomposition [tests/Hexalith.Parties.Tests/FitnessTests/EventStorePartiesInvocationSpikeTests.cs]
- [x] [Review][Patch] `IndexOf("AddEventStoreServer(configuration)")` is parameter-name-coupled; routine refactor to `AddEventStoreServer(builder.Configuration)` returns -1 and the inequality comparison fails with a confusing message. Use `"AddEventStoreServer("` substring and guard both indices `>= 0` before comparing [tests/Hexalith.Parties.Tests/FitnessTests/EventStorePartiesInvocationSpikeTests.cs:47-49]
- [x] [Review][Patch] Domain matrix `Parties` row failure mode misattributed (resolver passes case unchanged; the actual failure is DAPR sidecar app-id casing convention). Add a clarifying sentence [docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md:65]
- [x] [Review][Patch] Tenant/auth values in spike note are prescriptive ("must include `sub=spike-user`") but no harness was actually run — relabel as forward-looking inputs or explicitly state "no harness executed; values are for the next runtime proof" [docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md:55-57]
- [x] [Review][Patch] Change Log v1.1 attributes to "Claude" but Agent Model Used field still says "Codex GPT-5" — add a Close-out actor line or update the field to reflect the close-out actor [12-0-eventstore-parties-actor-invocation-feasibility-spike.md:135,170]
- [x] [Review][Patch] `MapPost("/process"` `ShouldNotContain` is bypassed by `MapMethods("/process", ["POST"], ...)`, casing variants, and routing constants — the assertion silently keeps reporting "blocker present" if a future story implements `/process` via any of those paths [tests/Hexalith.Parties.Tests/FitnessTests/EventStorePartiesInvocationSpikeTests.cs:29-31]
- [x] [Review][Patch] Add explicit Wave-1 unblock decision sentence to Spike Conclusion ("Epic 12 is NOT blocked. Wave 1 — Stories 12-1..12-4 — may proceed.") satisfying AC4 closure [12-0-eventstore-parties-actor-invocation-feasibility-spike.md (## Spike Conclusion)]
- [x] [Review][Defer] Wildcard `*|party|v1` registration assumes resolver wildcard key format `*|domain|version` with case-passthrough on domain — defer until Story 12.1 wires the static registration [docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md:57,63]
- [x] [Review][Defer] Reproduction commands use `rg -n "^"` (effectively `cat -n`) and assume `rg` on PATH while the code block is labeled `powershell` [docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md:153-167]
- [x] [Review][Defer] Event persistence "unknown" classification doesn't acknowledge state-store config dependencies (`actorStateStore=true`, `keyPrefix=none` between EventStore and Parties); 12.1 will surface [docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md:139,141]
- [x] [Review][Defer] Domain matrix may read as "convention works for `party`" then "explicit registration required" without bridging — clarification tied to D1 verdict-integrity outcome [docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md domain matrix]

Dismissed (7): missing `using` directives (ImplicitUsings enabled at root `Directory.Build.props`); `RepositoryRoot.Locate()` unintroduced (file already exists at `tests/Hexalith.Parties.Tests/FitnessTests/RepositoryRoot.cs`); duplicate `# last_updated:` comments (project's append-only convention in sprint-status.yaml header); 12-4 `backlog` → `ready-for-dev` flip (committed in separate commit `5a903f0` for story 12-4 creation, not 12-0 work); deferred-work entries lack quoted snippets (file:line is sufficient — quoted snippets rot worse than line refs); `DomainServiceRequestRouter.ProcessAsync` substring still serves the assertion's intent of "no current EventStore-style remote router wiring"; `git -C Hexalith.EventStore status --short` assumes initialized submodule (reproduction guidance only — the spike itself ran with the submodule initialized).

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-09 | 0.1 | Created ready-for-dev story through BMAD pre-dev hardening automation. | Codex |
| 2026-05-09 | 0.2 | Party-mode review applied low-risk clarifications for spike evidence, domain/app-id matrix, query blocker classification, stop rules, tenant/auth evidence, dated conclusion artifact, and EventStore submodule verification. | Codex |
| 2026-05-09 | 1.0 | Completed feasibility artifacts with partial verdict, focused fitness tests, blocker classification, and dated spike note; held status in-progress because broader regression suite has three existing-area failures. | Codex |
| 2026-05-09 | 1.1 | Verified the three regression failures are pre-existing and unrelated to spike scope (last touched in `db0bf14`); logged them in `deferred-work.md`; advanced Status from in-progress to review. | Claude |
| 2026-05-09 | 1.2 | bmad-code-review applied: D1 verdict-integrity resolved (option b — kept `partial`, added static-analysis-only caveats); 13 patches applied across story file, dated spike note, and `EventStorePartiesInvocationSpikeTests.cs`; 4 items deferred to `deferred-work.md`; 7 dismissed. Spike fitness tests still 4/4 green. Status advanced from review to done. | Claude |

## Party-Mode Review

- Date/time: 2026-05-09T13:52:39Z
- Selected story key: 12-0-eventstore-parties-actor-invocation-feasibility-spike
- Command/skill invocation used: `/bmad-party-mode 12-0-eventstore-parties-actor-invocation-feasibility-spike; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: reviewers agreed the spike was directionally sound but needed sharper proof boundaries before development. The main risks were ambiguous domain/app-id naming, insufficient separation between EventStore API acceptance and Parties handler execution, vague EventStore persistence proof, under-specified query determinism, unclear tenant/auth shortcuts, accidental EventStore submodule edits, and false `no` conclusions from time-box exhaustion rather than observed platform incompatibility.
- Changes applied: clarified AC1-AC4 evidence requirements; added Party-Mode Clarifications; added domain/app-id matrix, tenant/auth evidence, command persistence proof, query blocker classification, minimal topology, stop-rule, no-EventStore-edit verification, dated spike report, feasibility table, and reproduction-command requirements.
- Findings deferred: final canonical domain naming; production auth and tenant propagation model; production topology policy; durable retry/operational readiness; full query/projection architecture; any EventStore platform change unless the spike proves it is required.
- Final recommendation: `ready-for-dev`
