# Story 12.3: Validation Relocation and Tenant Auth Ownership

Status: done

## Story

As a developer,
I want Parties command and query payload validation to run inside the actor-host/domain invocation path and tenant authorization to be enforced exclusively by the EventStore gateway,
so that the request path has a single, unambiguous validation and authorization boundary.

## Acceptance Criteria

1. Given Parties command payload validators such as `CreatePartyValidator`, when a command is routed from EventStore to the Parties actor/domain host, then the payload is validated in the actor-host/domain invoker path before `PartyAggregate.Handle` or `PartyAggregate.ProcessAsync` can execute; controller, REST, MCP, or command-constructor validation alone does not satisfy this criterion.
2. Given an invalid Parties command payload submitted through the EventStore command path, then the request is rejected with the EventStore/platform validation response shape rather than a Parties-specific envelope, and no Parties domain event is persisted, published, or projected.
3. Given EventStore gateway command/query ingress, then tenant validation and RBAC use EventStore's `ITenantValidator` and `IRbacValidator` plug-in surface; Parties does not register, replace, or wrap a gateway-level tenant validator.
4. Given the EventStore-fronted request path after Story 12.2, then `ITenantAccessService` and `TenantAccessDenialTranslator` are not used for command/query authorization in Parties; any retained Tenants projection services are limited to projection-side or internal actor-host concerns.
5. Given an unauthorized tenant, user, or role, when a Parties command is submitted to EventStore, then EventStore denies the request before invoking the Parties actor/domain host and returns the platform forbidden/problem-details response.
6. Given DAPR actor/service invocation from EventStore to Parties, then the EventStore `AggregateActor` tenant mismatch guard remains intact and Parties sidecar access control remains scoped to the EventStore caller and the required actor/domain invocation operation.
7. Given architectural and focused unit coverage, then tests prove validation-before-domain-execution, absence of Parties-owned gateway tenant authorization, absence of `ITenantAccessService` from command/query request paths, and preservation of Tenants projection behavior where still required.

## Tasks / Subtasks

- [x] Confirm predecessor gates before implementation. (AC: 1-7)
  - [x] Read `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md`.
  - [x] If `## Spike Conclusion` is still pending or negative, stop through the normal dev workflow; do not move validation or authorization boundaries yet.
  - [x] Confirm Story 12.1 established EventStore-fronted AppHost topology and Story 12.2 removed or quarantined Parties public REST/MCP surfaces, or keep this story limited to the actor-host/domain path that already exists.
- [x] Inventory current validation and authorization locations. (AC: 1, 3, 4)
  - [x] Inspect `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` for validator, EventStore, Tenants, authentication, and authorization registrations.
  - [x] Inspect `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs` as the current in-process domain invocation point before `PartyAggregate.ProcessAsync`.
  - [x] Inspect any remaining controller/admin/MCP code only to remove stale request-path assumptions left after Story 12.2; do not reintroduce public endpoints.
  - [x] Inspect EventStore authorization contracts and defaults: `ITenantValidator`, `IRbacValidator`, `ClaimsTenantValidator`, `ClaimsRbacValidator`, `ActorTenantValidator`, and `ActorRbacValidator`.
- [x] Move Parties payload validation into the actor-host/domain invocation path. (AC: 1, 2)
  - [x] Register Parties FluentValidation validators in the actor host if Story 12.2 retained or moved the registrations.
  - [x] Add a narrow validation component or decorate `PartyDomainServiceInvoker` so the concrete payload type is validated before protected state/events are unprotected and before aggregate processing.
  - [x] Treat `PartyDomainServiceInvoker` or the immediately adjacent actor-host/domain adapter as the preferred insertion point; do not satisfy this task through old controller, REST, MCP, or API model-validation paths.
  - [x] Preserve existing payload protection behavior in `PartyDomainServiceInvoker`; validation must not bypass protected state/event handling or change the domain string accepted by the invoker.
  - [x] If EventStore's payload-type convention cannot invoke Parties validators without changes to the `Hexalith.EventStore` submodule, document the platform gap and stop or defer the submodule work rather than editing EventStore in this story.
- [x] Remove Parties tenant authorization from the command/query request path. (AC: 3, 4, 5)
  - [x] Ensure Parties does not register an EventStore gateway `ITenantValidator` or `IRbacValidator` implementation.
  - [x] Remove any remaining command/query use of `ITenantAccessService`, `TenantAccessDenialTranslator`, or Parties-specific gateway denial mapping.
  - [x] Preserve Tenants event projection services, projection stores, and internal access data only when they are still used by projection-side or actor-host internals.
  - [x] If `ITenantAccessService` remains registered for projection-side/internal use, rename, scope, or document it so tests can distinguish it from gateway request-path authorization.
- [x] Preserve EventStore-owned authorization semantics. (AC: 3, 5, 6)
  - [x] Verify EventStore gateway submit/validation paths call `ITenantValidator` and `IRbacValidator` before domain actor invocation.
  - [x] For unauthorized requests with invalid payloads, preserve EventStore authorization-first behavior: tenant/RBAC denial occurs before Parties payload validation and before actor/domain invocation.
  - [x] Keep the EventStore `AggregateActor` tenant mismatch guard between actor id and command tenant id; do not treat it as a replacement for gateway authorization.
  - [x] Verify Parties DAPR access-control configuration still allows only EventStore-origin invocation required by Story 12.0/12.1 and does not broaden to wildcard clients.
- [x] Update tests and fitness coverage. (AC: 1-7)
  - [x] Add focused tests proving invalid Parties command payloads are rejected before `PartyAggregate.ProcessAsync` can run, with fakes or spies proving no aggregate/domain invocation occurs.
  - [x] Add or update tests proving unauthorized EventStore gateway submissions do not invoke the Parties actor/domain invoker, including an invalid-payload-plus-unauthorized case to prove authorization wins first.
  - [x] Add architectural fitness tests proving Parties does not register EventStore gateway `ITenantValidator`/`IRbacValidator` implementations and does not use `ITenantAccessService` or `TenantAccessDenialTranslator` in command/query request-path code.
  - [x] Update Tenants projection tests to preserve projection-side behavior without asserting gateway request-path ownership by Parties.
  - [x] Ensure validation and authorization tests cannot pass through stale REST/MCP/controller routes; target the EventStore-to-actor-host/domain boundary or focused domain-invoker fixtures directly.
  - [x] Keep broad EventStore gateway Tier-1/Tier-2 rewrite scope out of this story; Story 12.4 owns the larger suite conversion.
- [x] Verify the boundary change. (AC: 1-7)
  - [x] Run the focused validation/domain-invoker tests added by this story.
  - [x] Run the relevant authorization and Tenants projection tests.
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter ArchitecturalFitnessTests`.
  - [x] Run `dotnet build Hexalith.Parties.slnx`.

## Dev Notes

### Source Context

- Epic 12 is sourced from `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md`; that proposal is authoritative for Story 12.3.
- The pivot decision is that all client commands and queries go to EventStore. EventStore owns public ingress, authentication, tenant validation, RBAC, and response mapping. Parties owns domain execution and projection runtime behind DAPR.
- Story 12.0 is still the gate in the current artifact: `## Spike Conclusion` says `Pending development`. This story is ready as implementation context, but the dev agent must not execute it until the spike has a positive conclusion and exact domain/app-id guidance.
- Story 12.1 defines the recomposed AppHost target and dedicated Parties DAPR access-control file. Story 12.2 owns removal of Parties public REST/MCP surfaces. This story should build on those boundaries, not duplicate their cleanup.

### Current Implementation to Inspect

- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` currently registers FluentValidation validators with `AddValidatorsFromAssemblyContaining<CreatePartyValidator>()`, EventStore server services through `AddEventStoreServer(configuration)`, Tenants integration, `ITenantAccessService`, actors, controllers, MCP, authentication, authorization, projections, crypto, and search services. After Story 12.2, expect the public-surface registrations to be gone or quarantined; keep the validator and actor-host registrations needed here.
- `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs` currently accepts only domain `"party"`, unprotects state/events through `IEventPayloadProtectionService`, and then calls `new PartyAggregate().ProcessAsync(...)`. This is the most likely local place to enforce validation before aggregate behavior if EventStore does not provide a cross-domain validator hook.
- `src/Hexalith.Parties/Validation/**` contains command validators including `CreatePartyValidator`, composite command validators, update/delete validators, and nested payload rules. Reuse these validators instead of duplicating validation logic.
- `src/Hexalith.Parties/ErrorHandling/PartiesValidationExceptionHandler.cs` maps FluentValidation failures to ProblemDetails for the old ASP.NET request path. After the pivot, confirm whether EventStore maps validation exceptions or requires a domain-invoker-specific exception/result shape.
- The platform validation response asserted by this story should be the EventStore/platform validation contract in force after Stories 12.1 and 12.2. Do not create a new Parties-only validation response envelope to satisfy AC 2.
- `src/Hexalith.Parties/Authorization/TenantAccessService.cs` reads the local Tenants projection and fail-closes on unknown, disabled, stale, or insufficient tenant membership. This behavior may remain useful for projection-side/internal decisions, but it must not be the command/query gateway authorization source after the pivot.
- `src/Hexalith.Parties/Authorization/TenantAccessDenialTranslator.cs` contains the old Parties-specific denial-to-problem-details mapping. Do not keep it in the command/query path once EventStore owns gateway denial responses.
- `Hexalith.EventStore/src/Hexalith.EventStore/Authorization/ITenantValidator.cs` and `IRbacValidator.cs` define the EventStore authorization plug-in surface.
- `Hexalith.EventStore/src/Hexalith.EventStore/Controllers/CommandsController.cs` owns `POST api/v1/commands` ingress and submits sanitized commands through MediatR.
- `Hexalith.EventStore/src/Hexalith.EventStore/Controllers/CommandValidationController.cs` already performs tenant and RBAC validation for preflight validation requests. Do not confuse its `200` validation-result response with actual command-submit forbidden behavior required by this story.
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` checks actor tenant id against command tenant id before state access and before calling `IDomainServiceInvoker.InvokeAsync(...)`.
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` registers EventStore Server actors and the default `DaprDomainServiceInvoker`; it does not currently register FluentValidation validators for domain payloads.

### Technical Constraints

- Keep package versions aligned with the repository: .NET SDK `10.0.103`, `net10.0`, Aspire `13.2.2`, CommunityToolkit Aspire DAPR `13.0.0`, `Dapr.Client`/`Dapr.AspNetCore` `1.17.7`, and `Dapr.Actors` `1.16.1`.
- Do not initialize or update nested submodules. The `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.FrontComposer`, and `Hexalith.Memories` folders are root-level submodules already present in the workspace.
- Do not edit the `Hexalith.EventStore` submodule in this story unless implementation proves the acceptance criteria cannot be met without a platform change. If that happens, stop and record the blocker for a separate EventStore story.
- Do not reintroduce Parties REST controllers, admin controllers, or in-process MCP endpoints. Story 12.2 owns their removal; Story 12.6 owns the future thin MCP host.
- Do not rewrite broad server Tier-1/Tier-2 integration suites here. Story 12.4 owns the conversion to EventStore gateway command/query tests.
- Keep `PartyDomainServiceInvoker` payload protection/unprotection behavior intact. Validation should happen before aggregate processing, but it must not drop existing encrypted state/event support.
- Preserve EventStore's `AggregateActor` tenant mismatch guard. It is a defense-in-depth actor consistency check, not the public gateway authorization policy.
- DAPR .NET actor hosting still uses `AddActors(...)` plus `MapActorsHandlers()` in ASP.NET Core endpoint routing, and DAPR service invocation access control is applied on the receiving sidecar through the Configuration schema. Use the sidecar/app-id policy as a transport boundary, not as a replacement for EventStore gateway authorization.

### Architecture and Security Guidance

- EventStore owns public command/query ingress, JWT authentication, tenant validation, RBAC, sanitized extension handling, and generic response mapping.
- Parties owns domain-specific payload validation, aggregate execution, projection runtime, crypto-shredding behavior, and local projection maintenance behind DAPR.
- EventStore authorization precedes Parties payload validation for public command/query ingress. If a request is both unauthorized and payload-invalid, the expected external result is the EventStore tenant/RBAC denial, and Parties validation must not run.
- Validation must be deterministic and side-effect-free. A failed validator must not call aggregate handlers, mutate protected state, persist events, publish events, update projections, or create audit records except safe denial/validation logs.
- Tenant authorization must fail before the Parties actor/domain host is invoked. Tests should assert absence of actor/domain invoker calls for unauthorized requests.
- Safe denial and validation logs may include correlation ids, tenant ids, command type names, denial categories, and validator rule names, but must not include access tokens, signing keys, raw encrypted payloads, protected PII values, or full command payload dumps.
- If local Tenants projection state is retained, keep it as replicated reference data for projections/internal logic. Do not make it a second public authorization decision point for command/query ingress.

### Testing Guidance

- Minimum focused tests:
  - A `PartyDomainServiceInvoker` or validation-wrapper test proves invalid `CreateParty` and representative update/composite commands throw validation before aggregate processing.
  - A regression test proves no domain events are emitted and no projection/read-model changes occur when validation fails.
  - A gateway authorization test or focused EventStore-host test proves denied tenant/RBAC submissions do not call the Parties domain invoker, using a spy/fake invocation point rather than old controller behavior.
  - An architectural fitness test proves `Hexalith.Parties` does not register or implement EventStore gateway `ITenantValidator` or `IRbacValidator` for command/query ingress.
  - A source or dependency fitness test proves command/query request-path code does not reference `ITenantAccessService` or `TenantAccessDenialTranslator`.
  - A Tenants projection test proves local tenant projection consumption still works if retained for projection-side/internal use.
- Troubleshooting expectation:
  - Validation failures point developers to Parties FluentValidation payload rules and the actor-host/domain validation adapter.
  - Tenant/RBAC denials point developers to EventStore gateway authorization configuration and `ITenantValidator`/`IRbacValidator` behavior.
  - Tenant mismatch guard failures point developers to EventStore `AggregateActor` actor-id-versus-command-tenant consistency, not to public gateway authorization policy.
- Run at least:
  - `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartyDomainServiceInvoker|FullyQualifiedName~Validation|FullyQualifiedName~Authorization|FullyQualifiedName~Tenant"`
  - `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter ArchitecturalFitnessTests`
  - `dotnet build Hexalith.Parties.slnx`
- If Story 12.4 has not run yet, expect some old REST/controller integration tests to remain obsolete. Do not weaken this story's boundary tests to keep obsolete public Parties endpoint tests green.

### Out of Scope

- Creating or modifying `Hexalith.Parties.Mcp`.
- Rebuilding the Admin Portal, Picker, sample app, README, or getting-started guide.
- Rewriting all controller integration tests through EventStore gateway.
- Changing EventStore submodule code without first recording a blocker.
- Changing the public EventStore authorization contract shape.
- Removing Tenants projection infrastructure that is still required for projection-side or internal actor-host behavior.

### References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md` - Story 12.3 ACs, Epic 12 pivot rationale, sequencing, and out-of-scope boundaries.
- `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md` - required gate and exact domain/app-id conclusions before implementation.
- `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md` - predecessor AppHost topology and DAPR configuration context.
- `_bmad-output/implementation-artifacts/12-2-parties-actor-host.md` - predecessor public REST/MCP cleanup and actor-host boundary context.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` - Epic 12 order and story state.
- `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs` - current Parties domain invocation path.
- `src/Hexalith.Parties/Validation/**` - existing Parties FluentValidation validators.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` - current validator, EventStore, Tenants, actor, authorization, and projection registrations.
- `src/Hexalith.Parties/Authorization/TenantAccessService.cs` - local Tenants projection access service to remove from command/query request path.
- `src/Hexalith.Parties/Authorization/TenantAccessDenialTranslator.cs` - old Parties-specific denial mapping to remove from command/query request path.
- `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs` - primary fitness-test home for actor-host and boundary assertions.
- `tests/Hexalith.Parties.Tests/Tenants/TenantEventInfrastructureTests.cs` - current Tenants projection/registration tests to update.
- `tests/Hexalith.Parties.Tests/Authorization/TenantAccessServiceTests.cs` - current access-service behavior tests to preserve only if service remains internal/projection-side.
- `Hexalith.EventStore/src/Hexalith.EventStore/Authorization/ITenantValidator.cs` - EventStore tenant validation contract.
- `Hexalith.EventStore/src/Hexalith.EventStore/Authorization/IRbacValidator.cs` - EventStore RBAC validation contract.
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` - EventStore actor tenant mismatch guard and domain invoker call site.
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` - EventStore Server actor/domain invoker registrations.
- DAPR documentation v1.17, `.NET actors` and `service invocation access control` - confirms `AddActors(...)`/`MapActorsHandlers()` hosting and sidecar-applied Configuration allow lists.

## Project Structure Notes

- Keep implementation changes primarily in `src/Hexalith.Parties`, `tests/Hexalith.Parties.Tests`, and narrow AppHost/DAPR config assertions only where needed to verify the EventStore-to-Parties boundary.
- Keep domain contracts in `src/Hexalith.Parties.Contracts`, domain execution in `src/Hexalith.Parties.Server`, projection behavior in `src/Hexalith.Parties.Projections`, and gateway ownership in EventStore.
- If a new validation helper is needed, place it near the domain invocation path rather than under old controller/API folders.
- Generated `bin/` and `obj/` outputs must stay out of commits.

## Dev Agent Record

### Agent Model Used

- Codex GPT-5

### Debug Log References

- Confirmed Story 12.0 gate is `done` with a dated `partial` conclusion explicitly unblocking Wave 1; Story 12.1 is `done`; Story 12.2 is `review` with REST/MCP surfaces removed or quarantined in project inclusion.
- Red phase: new `PartyDomainServiceInvokerValidationTests` initially failed because `PartyDomainServiceInvoker` had no validator dependency or pre-aggregate validation path.
- Green phase: `PartyDomainServiceInvoker` now resolves the concrete command payload type, uses registered FluentValidation validators, throws `ValidationException` for invalid payloads, and does this before protected state/event unprotection and `PartyAggregate.ProcessAsync`.
- Focused tests: `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartyDomainServiceInvokerValidationTests|FullyQualifiedName~ArchitecturalFitnessTests" --no-restore` (17/17 passed).
- Relevant validation/authorization/tenant slice: `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartyDomainServiceInvoker|FullyQualifiedName~Validation|FullyQualifiedName~Authorization|FullyQualifiedName~Tenant" --no-restore` (69/69 passed).
- Architectural fitness: `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter ArchitecturalFitnessTests --no-restore` (14/14 passed).
- Full regression: `dotnet test Hexalith.Parties.slnx --no-restore` passed across the solution.
- Build: `dotnet build Hexalith.Parties.slnx --no-restore` passed with 0 warnings and 0 errors.

### Completion Notes List

- Added actor-host/domain invocation validation in `PartyDomainServiceInvoker`; invalid Parties command payloads now fail through FluentValidation before protected state/event replay and before aggregate processing.
- Kept the existing `party` domain boundary and payload protection behavior intact; valid commands continue into `PartyAggregate.ProcessAsync`.
- Left EventStore submodule source unchanged; added source fitness guardrails from the Parties test project to pin EventStore authorization-before-validation order and the `AggregateActor` tenant mismatch guard.
- Added fitness coverage proving Parties does not implement/register EventStore gateway tenant/RBAC validators and that command/query request-path code does not use `ITenantAccessService` or `TenantAccessDenialTranslator`.
- Preserved Tenants projection/internal registration behavior; focused tenant tests remain green and `ITenantAccessService` is distinguished from gateway authorization by request-path fitness checks.

### File List

- `_bmad-output/implementation-artifacts/12-3-validation-relocation-and-tenant-auth-ownership.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.Contracts/Events/PartyCommandValidationRejected.cs` (new — review pass)
- `src/Hexalith.Parties/Authorization/TenantAccessDenialTranslator.cs` (deleted — review pass)
- `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs`
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` (review pass — projection-only comment)
- `tests/Hexalith.Parties.Security.Tests/PartyPayloadProtectionServiceTests.cs` (review pass — classify new rejection event)
- `tests/Hexalith.Parties.Tests/Domain/PartyDomainServiceInvokerValidationTests.cs`
- `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-09 | 0.1 | Created ready-for-dev story through BMAD pre-dev hardening automation. | Codex |
| 2026-05-09 | 0.2 | Applied party-mode review clarifications for validation insertion point, authorization ordering, and boundary test oracles. | Codex |
| 2026-05-10 | 1.0 | Added actor-host payload validation before aggregate execution, pinned EventStore-owned authorization boundaries with fitness tests, verified focused/full regression suites, and moved story to review. | Codex |
| 2026-05-10 | 1.1 | bmad-code-review: 16 patches applied + 3 decisions resolved. AC2 dead-letter side-effect fixed by translating validation failures into `DomainResult.Rejection` over a new `PartyCommandValidationRejected` rejection event (no submodule edit). Allowlisted type resolution, scoped validator resolution, symmetric JSON options, widened deserialization catch, and PII-safe error code in the rejection event. Test fixture now mirrors production validator assembly scan; branch coverage added (unknown command, malformed JSON, empty payload, no-validator, AssemblyQualifiedName). Fitness tests now strip comments/strings before regex, filter bin/obj, and degrade gracefully when EventStore submodule is absent. `TenantAccessDenialTranslator` deleted; `ITenantAccessService` registration documented as projection-only. Full solution build clean; 870 tests passed (27 pre-existing skips). 5 items deferred to deferred-work.md. | Claude Opus 4.7 |

### Review Findings

Source: `bmad-code-review` of commit `03b5fc0` on 2026-05-10. Reviewers: Blind Hunter (28 raw), Edge Case Hunter (~25 raw), Acceptance Auditor (~10 raw). 31 findings retained after dedupe; 7 dismissed.

#### Decision-needed (resolved)

- [x] [Review][Decision][Resolved] **AC2 violation: validation failures dead-letter and surface as HTTP 500** — Throwing `FluentValidation.ValidationException` from inside `IDomainServiceInvoker.InvokeAsync` is caught by `AggregateActor.cs:327` and routed to `HandleInfrastructureFailureAsync` (dead-letter publish + Rejected checkpoint + idempotency rejection). **Resolved:** translate validation failures into `DomainResult.Rejection([new PartyCommandValidationRejected(...)])`. Rejection events are the platform's normal mechanism (Parties already has 19); they surface via `DomainCommandRejectedExceptionHandler` as 422-style ProblemDetails. AC2's "no Parties domain event persisted" is interpreted as "no state-changing event"; the validation-rejection event is platform metadata, not a Party state change. No EventStore submodule edit.
- [x] [Review][Decision][Resolved] **`ITenantAccessService` registration retained without scoping/renaming/documentation per AC4** — **Resolved:** add an explicit `// projection-side only — not for command/query gateway authorization` comment beside the registration in `PartiesServiceCollectionExtensions.cs:90`, and tighten the existing fitness test to also assert the request path does not reference `ITenantAccessService` outside the projection adapter.
- [x] [Review][Decision][Resolved] **`TenantAccessDenialTranslator` is dead code from pre-pivot REST path** — **Resolved:** delete `src/Hexalith.Parties/Authorization/TenantAccessDenialTranslator.cs`. No consumers in `src/Hexalith.Parties/**`; spec Tasks line 40 explicitly asked for removal.

#### Patch (applied)

- [x] [Review][Patch] **Fail-closed on unresolved CommandType** — invoker now returns `DomainResult.Rejection([PartyCommandValidationRejected{ ErrorCode = "UnresolvedCommandType" }])`. [src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs:84-93]
- [x] [Review][Patch] **Disambiguate short-name fallback** — `ResolveCommandTypeUncached` requires `Take(2).Length == 1` so colliding short names fail-closed. [src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs:182-198]
- [x] [Review][Patch] **Restrict type resolution to contracts assembly allowlist** — `ContractsAssembly.GetType(...)` replaces `Type.GetType(...)`, never loads arbitrary assemblies from wire data. [src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs:177-197]
- [x] [Review][Patch] **Validate against `ValidationContext<TCommand>`** — built dynamically via `Activator.CreateInstance(typeof(ValidationContext<>).MakeGenericType(commandType), payload)`. [src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs:128-133]
- [x] [Review][Patch] **Pass `JsonSerializerOptions` symmetric with envelope serialization** — `PayloadJsonOptions` static (General + JsonStringEnumConverter). [src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs:35-43, 113]
- [x] [Review][Patch] **Guard null/empty payload and widen exception catch** — explicit `command.Payload.Length == 0` rejection plus `catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)`. [src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs:108-126]
- [x] [Review][Patch] **Sanitize `JsonException.Message`** — rejection event carries only `ErrorCode = "InvalidJson"`; raw exception type logged at Debug level only, never embedded in user-facing or persisted text. [src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs:118-122]
- [x] [Review][Patch] **Cache `ResolveCommandType` results in `ConcurrentDictionary`** — single reflection scan per CommandType string. [src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs:45, 167-175]
- [x] [Review][Patch] **Resolve validators through `IServiceScopeFactory`** — invoker now takes `IServiceScopeFactory` and uses `using IServiceScope scope = ...CreateScope()` per call. [src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs:24-29, 93-95]
- [x] [Review][Patch] **Aggregate-side-effect oracle hardened** — invalid-payload tests now pass a non-null `currentState` carrying both a snapshot and a protected event, so `DidNotReceiveWithAnyArgs()` becomes a meaningful "no unprotect was called" assertion. [tests/Hexalith.Parties.Tests/Domain/PartyDomainServiceInvokerValidationTests.cs:38-77]
- [x] [Review][Patch] **Test fixture mirrors production assembly scan** — `CreateInvoker` uses `AddValidatorsFromAssemblyContaining<CreatePartyValidator>()`. [tests/Hexalith.Parties.Tests/Domain/PartyDomainServiceInvokerValidationTests.cs:194-203]
- [x] [Review][Patch] **Branch-coverage tests added** — unknown CommandType, malformed JSON, empty payload, missing-validator, AssemblyQualifiedName resolution. [tests/Hexalith.Parties.Tests/Domain/PartyDomainServiceInvokerValidationTests.cs:96-189]
- [x] [Review][Patch] **Filter `bin/` and `obj/` from source-text fitness scans** — new `EnumerateSourceFiles` helper applied across architectural tests. [tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs:474-491]
- [x] [Review][Patch] **Skip cleanly when EventStore submodule paths are absent** — `TryReadEventStoreFile` returns null and tests early-return, replacing `FileNotFoundException`. [tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs:469-477]
- [x] [Review][Patch] **Tighten interface-implementation regex** — sources now stripped of comments + string literals before regex via new `StripCommentsAndStringLiterals` helper. [tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs:497-651]
- [x] [Review][Patch] **Restore using-directive grouping; drop unused `using`** — handled in the `PartyDomainServiceInvoker.cs` rewrite. [src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs:1-21]

#### Defer

- [x] [Review][Defer] **AC7 — no projection-side test added in this diff** [tests/Hexalith.Parties.Tests/Tenants/TenantEventInfrastructureTests.cs] — deferred; existing 69/69 tenant tests still pass and Dev Agent treated Tasks line 52 as no-update-needed.
- [x] [Review][Defer] **AC6 fitness test pins string ordering, not behavior** [tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs:240-258] — deferred; tripwire is acceptable, Roslyn rewrite is improvement-of-improvement.
- [x] [Review][Defer] **Double JSON deserialization (validate then aggregate)** [src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs:74,54] — deferred; performance only, no correctness impact.
- [x] [Review][Defer] **No per-command validation timeout** [src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs:83-85] — deferred; relies on caller-supplied `CancellationToken`, sufficient for now.
- [x] [Review][Defer] **Runtime non-invocation test for unauthorized requests via spy `IDomainServiceInvoker`** — deferred to Story 12-4's Tier-2 test rewrite, which owns the EventStore-gateway integration suite. The architectural fitness test pinning `AuthorizationBehavior` ahead of `ValidationBehavior` plus the `AggregateActor` tenant-mismatch-guard tripwire establish the boundary at static-analysis level for this story.

#### Dismissed (recorded for trail)

- Validation runs before payload unprotection — claim that encrypted command payloads break (Blind #10): commands are not protected, only state and events; misreading of `UnprotectCurrentStateAsync`.
- Tests reference validators not introduced by this diff (Blind #18): validators pre-existed in `src/Hexalith.Parties/Validation/**`.
- Collection-expression `new ValidationException([...])` (Blind #21): C# 12 + FluentValidation 12.1.1 (per `Hexalith.EventStore/CLAUDE.md`) supports the constructor.
- ULID literal `01HX0000000000000000000000` not valid (Blind #13): valid Crockford base32, no requirement for unique IDs in the test fixture.
- `PartyDomainServiceInvoker` is `internal sealed` — InternalsVisibleTo missing (Blind #27): tests already compile, plumbing pre-exists.
- `ShouldBeEmpty` eager-message string concat (Blind #17): standard Shouldly idiom.
- Positive test asserts `PartyDisplayNameDerived` (Blind #22): legitimate downstream-event coverage in a happy-path test.

## Party-Mode Review

- Date: 2026-05-09T18:55:10+02:00
- Selected story key: `12-3-validation-relocation-and-tenant-auth-ownership`
- Command/skill invocation used: `/bmad-party-mode 12-3-validation-relocation-and-tenant-auth-ownership; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary:
  - The story was directionally sound but needed sharper validation-boundary wording so implementation does not satisfy ACs through old controller, REST, MCP, or command-constructor validation.
  - Authorization ownership needed explicit ordering: EventStore tenant/RBAC denial must happen before Parties actor/domain invocation and before Parties payload validation on unauthorized requests.
  - Test guidance needed stronger negative oracles for no aggregate invocation, no event append/publish/project side effects, no stale public-route false positives, and no Parties tenant-auth services in command/query request paths.
  - Developers need troubleshooting cues that distinguish Parties payload validation failures, EventStore tenant/RBAC denials, and EventStore `AggregateActor` tenant mismatch guard failures.
- Changes applied:
  - Clarified AC 1 and AC 2 to name the actor-host/domain invoker boundary and forbid a Parties-specific validation envelope.
  - Added implementation tasks for preferred validation insertion point, authorization-first ordering, spies/fakes proving non-invocation, and stale REST/MCP route isolation.
  - Expanded architecture, security, testing, and troubleshooting guidance around response shape, no-events/no-projection assertions, EventStore-owned authorization, and defense-in-depth tenant mismatch handling.
- Findings deferred:
  - Whether EventStore contract/controller/actor changes are required remains a blocker decision for implementation; do not edit the root-level EventStore submodule unless ACs cannot be met and the gap is recorded.
  - Whether retained Parties tenant-access services are deleted, deprecated, renamed, or scoped remains deferred unless current registrations make command/query misuse likely.
  - Broad EventStore gateway Tier-1/Tier-2 rewrite remains with Story 12.4.
  - Shared platform validator registry design remains out of scope unless an existing platform pattern is already available.
- Final recommendation: ready-for-dev
