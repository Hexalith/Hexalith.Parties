# Story 12.3: Validation Relocation and Tenant Auth Ownership

Status: ready-for-dev

## Story

As a developer,
I want Parties command and query payload validation to run inside the actor-host/domain invocation path and tenant authorization to be enforced exclusively by the EventStore gateway,
so that the request path has a single, unambiguous validation and authorization boundary.

## Acceptance Criteria

1. Given Parties command payload validators such as `CreatePartyValidator`, when a command is routed from EventStore to the Parties actor/domain host, then the payload is validated before `PartyAggregate.Handle` or `PartyAggregate.ProcessAsync` can execute.
2. Given an invalid Parties command payload submitted through the EventStore command path, then the request is rejected with the platform validation response shape and no Parties domain event is persisted or published.
3. Given EventStore gateway command/query ingress, then tenant validation and RBAC use EventStore's `ITenantValidator` and `IRbacValidator` plug-in surface; Parties does not register, replace, or wrap a gateway-level tenant validator.
4. Given the EventStore-fronted request path after Story 12.2, then `ITenantAccessService` and `TenantAccessDenialTranslator` are not used for command/query authorization in Parties; any retained Tenants projection services are limited to projection-side or internal actor-host concerns.
5. Given an unauthorized tenant, user, or role, when a Parties command is submitted to EventStore, then EventStore denies the request before invoking the Parties actor/domain host and returns the platform forbidden/problem-details response.
6. Given DAPR actor/service invocation from EventStore to Parties, then the EventStore `AggregateActor` tenant mismatch guard remains intact and Parties sidecar access control remains scoped to the EventStore caller and the required actor/domain invocation operation.
7. Given architectural and focused unit coverage, then tests prove validation-before-domain-execution, absence of Parties-owned gateway tenant authorization, absence of `ITenantAccessService` from command/query request paths, and preservation of Tenants projection behavior where still required.

## Tasks / Subtasks

- [ ] Confirm predecessor gates before implementation. (AC: 1-7)
  - [ ] Read `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md`.
  - [ ] If `## Spike Conclusion` is still pending or negative, stop through the normal dev workflow; do not move validation or authorization boundaries yet.
  - [ ] Confirm Story 12.1 established EventStore-fronted AppHost topology and Story 12.2 removed or quarantined Parties public REST/MCP surfaces, or keep this story limited to the actor-host/domain path that already exists.
- [ ] Inventory current validation and authorization locations. (AC: 1, 3, 4)
  - [ ] Inspect `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` for validator, EventStore, Tenants, authentication, and authorization registrations.
  - [ ] Inspect `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs` as the current in-process domain invocation point before `PartyAggregate.ProcessAsync`.
  - [ ] Inspect any remaining controller/admin/MCP code only to remove stale request-path assumptions left after Story 12.2; do not reintroduce public endpoints.
  - [ ] Inspect EventStore authorization contracts and defaults: `ITenantValidator`, `IRbacValidator`, `ClaimsTenantValidator`, `ClaimsRbacValidator`, `ActorTenantValidator`, and `ActorRbacValidator`.
- [ ] Move Parties payload validation into the actor-host/domain invocation path. (AC: 1, 2)
  - [ ] Register Parties FluentValidation validators in the actor host if Story 12.2 retained or moved the registrations.
  - [ ] Add a narrow validation component or decorate `PartyDomainServiceInvoker` so the concrete payload type is validated before protected state/events are unprotected and before aggregate processing.
  - [ ] Preserve existing payload protection behavior in `PartyDomainServiceInvoker`; validation must not bypass protected state/event handling or change the domain string accepted by the invoker.
  - [ ] If EventStore's payload-type convention cannot invoke Parties validators without changes to the `Hexalith.EventStore` submodule, document the platform gap and stop or defer the submodule work rather than editing EventStore in this story.
- [ ] Remove Parties tenant authorization from the command/query request path. (AC: 3, 4, 5)
  - [ ] Ensure Parties does not register an EventStore gateway `ITenantValidator` or `IRbacValidator` implementation.
  - [ ] Remove any remaining command/query use of `ITenantAccessService`, `TenantAccessDenialTranslator`, or Parties-specific gateway denial mapping.
  - [ ] Preserve Tenants event projection services, projection stores, and internal access data only when they are still used by projection-side or actor-host internals.
  - [ ] If `ITenantAccessService` remains registered for projection-side/internal use, rename, scope, or document it so tests can distinguish it from gateway request-path authorization.
- [ ] Preserve EventStore-owned authorization semantics. (AC: 3, 5, 6)
  - [ ] Verify EventStore gateway submit/validation paths call `ITenantValidator` and `IRbacValidator` before domain actor invocation.
  - [ ] Keep the EventStore `AggregateActor` tenant mismatch guard between actor id and command tenant id; do not treat it as a replacement for gateway authorization.
  - [ ] Verify Parties DAPR access-control configuration still allows only EventStore-origin invocation required by Story 12.0/12.1 and does not broaden to wildcard clients.
- [ ] Update tests and fitness coverage. (AC: 1-7)
  - [ ] Add focused tests proving invalid Parties command payloads are rejected before `PartyAggregate.ProcessAsync` can run.
  - [ ] Add or update tests proving unauthorized EventStore gateway submissions do not invoke the Parties domain invoker.
  - [ ] Add architectural fitness tests proving Parties does not register EventStore gateway `ITenantValidator`/`IRbacValidator` implementations and does not use `ITenantAccessService` in command/query request-path code.
  - [ ] Update Tenants projection tests to preserve projection-side behavior without asserting gateway request-path ownership by Parties.
  - [ ] Keep broad EventStore gateway Tier-1/Tier-2 rewrite scope out of this story; Story 12.4 owns the larger suite conversion.
- [ ] Verify the boundary change. (AC: 1-7)
  - [ ] Run the focused validation/domain-invoker tests added by this story.
  - [ ] Run the relevant authorization and Tenants projection tests.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter ArchitecturalFitnessTests`.
  - [ ] Run `dotnet build Hexalith.Parties.slnx`.

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
- Validation must be deterministic and side-effect-free. A failed validator must not call aggregate handlers, mutate protected state, persist events, publish events, update projections, or create audit records except safe denial/validation logs.
- Tenant authorization must fail before the Parties actor/domain host is invoked. Tests should assert absence of actor/domain invoker calls for unauthorized requests.
- Safe denial and validation logs may include correlation ids, tenant ids, command type names, denial categories, and validator rule names, but must not include access tokens, signing keys, raw encrypted payloads, protected PII values, or full command payload dumps.
- If local Tenants projection state is retained, keep it as replicated reference data for projections/internal logic. Do not make it a second public authorization decision point for command/query ingress.

### Testing Guidance

- Minimum focused tests:
  - A `PartyDomainServiceInvoker` or validation-wrapper test proves invalid `CreateParty` and representative update/composite commands throw validation before aggregate processing.
  - A regression test proves no domain events are emitted when validation fails.
  - A gateway authorization test or focused EventStore-host test proves denied tenant/RBAC submissions do not call the Parties domain invoker.
  - An architectural fitness test proves `Hexalith.Parties` does not register or implement EventStore gateway `ITenantValidator` or `IRbacValidator` for command/query ingress.
  - A source or dependency fitness test proves command/query request-path code does not reference `ITenantAccessService` or `TenantAccessDenialTranslator`.
  - A Tenants projection test proves local tenant projection consumption still works if retained for projection-side/internal use.
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
