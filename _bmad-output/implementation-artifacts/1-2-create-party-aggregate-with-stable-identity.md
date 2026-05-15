# Story 1.2: Create Party Aggregate with Stable Identity

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized client,
I want to create a person or organization party with a client-generated stable UUID,
so that Parties can become the durable source of truth for party identity.

## Acceptance Criteria

1. **Person party creation stores stable identity and derived names**
   - Given an authorized command with a valid tenant context, client-generated party UUID, and person details,
   - When the client creates a person party,
   - Then the aggregate emits a party-created event for a person,
   - And the immutable UUID remains the EventStore aggregate/stream identity supplied by `CreateParty.PartyId`, not a `PartyCreated` payload field,
   - And rehydrated party state stores the party type, person details, active status, and derived display/sort names.

2. **Organization party creation stores stable identity and derived names**
   - Given an authorized command with a valid tenant context, client-generated party UUID, and organization details,
   - When the client creates an organization party,
   - Then the aggregate emits a party-created event for an organization,
   - And the immutable UUID remains the EventStore aggregate/stream identity supplied by `CreateParty.PartyId`, not a `PartyCreated` payload field,
   - And rehydrated party state stores the party type, organization details, active status, and derived display/sort names.

3. **Invalid type-specific details are rejected**
   - Given a create command with missing or invalid type-specific details,
   - When the command is handled,
   - Then the aggregate rejects the command with a typed rejection event,
   - And no successful party-created event is emitted.

4. **Duplicate creation does not change existing identity**
   - Given an existing party state for the requested party UUID,
   - When a create command is handled for the same party UUID,
   - Then the aggregate idempotently skips duplicate creation as a no-op within that aggregate stream,
   - And no new success or rejection events are emitted,
   - And the existing aggregate identity and party state are not changed.

5. **Rehydration preserves creation state**
   - Given a party has been created,
   - When the state is rehydrated from emitted events,
   - Then the rehydrated party preserves the same UUID, party type, active status, details, display name, and sort name.

## Tasks / Subtasks

- [ ] Task 1: Audit the existing creation contracts and aggregate implementation (AC: 1, 2, 3, 4)
  - [ ] Confirm `CreateParty`, `PartyCreated`, `PartyDisplayNameDerived`, creation rejection events, `PersonDetails`, `OrganizationDetails`, `PartyType`, and `PartyState` still match the current story scope.
  - [ ] Confirm `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` exposes pure static `Handle(CreateParty, PartyState?)` behavior through the EventStore aggregate convention.
  - [ ] Prove the stable UUID is preserved through EventStore aggregate identity/metadata and any read-model mapping that consumes the stream identity.
  - [ ] Confirm `CreateParty.PartyId` remains the client-generated stable UUID command field and is not duplicated onto `PartyCreated`; aggregate identity belongs to EventStore metadata, not the success-event payload.
  - [ ] If the current EventStore conventions cannot expose aggregate identity to the required state/read-model evidence without adding `PartyId` to `PartyCreated`, stop and record an architecture decision gap instead of inventing a new event contract.
  - [ ] Repair only verified drift; do not recreate the historical broad contract-bootstrap story.

- [ ] Task 2: Validate person and organization creation behavior (AC: 1, 2, 5)
  - [ ] For person creation, verify `PartyCreated` stores `PartyType.Person` and `PersonDetails`, followed by `PartyDisplayNameDerived`.
  - [ ] For organization creation, verify `PartyCreated` stores `PartyType.Organization` and `OrganizationDetails`, followed by `PartyDisplayNameDerived`.
  - [ ] Verify MVP display-name rules: person display name `"{FirstName} {LastName}"`, person sort name `"{LastName}, {FirstName}"`, organization display and sort name both use `LegalName`.
  - [ ] Apply emitted events to a fresh `PartyState` and verify type, details, active status, display name, and sort name are preserved after rehydration.

- [ ] Task 3: Validate rejection and idempotency behavior (AC: 3, 4)
  - [ ] Reject blank, whitespace, or non-GUID `PartyId` values with `PartyCannotBeCreatedWithInvalidId`.
  - [ ] Reject `PartyType.Unknown` or the default enum value with `PartyCannotBeCreatedWithoutType`.
  - [ ] Reject person creation without `PersonDetails` using `PartyCannotBeCreatedWithoutPersonDetails`.
  - [ ] Reject organization creation without `OrganizationDetails` using `PartyCannotBeCreatedWithoutOrganizationDetails`.
  - [ ] Verify person creation does not accept organization-only details as a substitute for valid `PersonDetails`.
  - [ ] Verify organization creation does not accept person-only details as a substitute for valid `OrganizationDetails`.
  - [ ] Verify every invalid create path emits only the typed rejection event and no `PartyCreated` or `PartyDisplayNameDerived` success event.
  - [ ] Verify duplicate creation against non-null state returns the accepted idempotent no-op behavior and emits no events.

- [ ] Task 4: Preserve EventStore, dependency, and privacy guardrails (AC: 1, 2, 3, 4, 5)
  - [ ] Keep domain behavior in pure aggregate `Handle` methods; do not add Dapr, HTTP, database, MediatR handler, REST, Swagger, or MCP hosting behavior to this story.
  - [ ] Keep `Hexalith.Parties.Contracts` free of hosting, Dapr, MediatR, FluentValidation, UI, and infrastructure dependencies beyond the accepted EventStore contracts reference.
  - [ ] Preserve `PartyState.Apply(...)` rejection-event ordering before success applies; EventStore suffix-based apply resolution depends on it.
  - [ ] Keep derived names marked as personal data and avoid adding logs, telemetry, exception messages, or assertions that expose sensitive values outside test-local expectations.

- [ ] Task 5: Run focused validation (AC: 1, 2, 3, 4, 5)
  - [ ] Run `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release`.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if implementation changes touch project references, shared build files, or EventStore-facing contracts.

## Dev Notes

### Current Implementation Context

- This story is the current backlog-tracked Epic 1 creation story, but the repository already contains historical completed artifacts for earlier March 2026 Story 1.2 contract bootstrapping and Story 1.3 party creation.
- Treat `_bmad-output/implementation-artifacts/1-2-domain-contracts-complete-type-definitions.md` and `_bmad-output/implementation-artifacts/1-3-party-aggregate-party-creation.md` as implementation intelligence, not as canonical status artifacts for this new story key.
- The expected implementation likely already exists in `src/Hexalith.Parties.Contracts`, `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`, `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCreateTests.cs`, and `tests/Hexalith.Parties.Contracts.Tests/State/PartyStateTests.cs`. The dev agent should audit and reconcile before editing.
- Do not add the historical broad contract inventory back into this story. Planning corrections explicitly warn that the old complete-contract slice is not a future slicing pattern.

### Architecture Patterns and Constraints

- Parties validates the Hexalith.EventStore domain-service pattern. If an EventStore abstraction does not fit, prefer fixing or adapting EventStore instead of adding Parties-side workaround structure.
- Domain behavior belongs in EventStore aggregate conventions: pure static `Handle(Command, PartyState?)` methods emit `DomainResult` events, and `PartyState.Apply(Event)` mutates state during rehydration.
- `PartyAggregate` lives in `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` and derives from `EventStoreAggregate<PartyState>`.
- `CreateParty` carries `PartyId`, `PartyType`, optional `PersonDetails`, and optional `OrganizationDetails`. Tenant context is supplied by the gateway/request pipeline, not by this aggregate story.
- Events must remain additive public contracts. Do not add `PartyId` or `TenantId` to `PartyCreated`; aggregate identity and tenant context are EventStore/request metadata concerns.
- The source AC originally said state stores the immutable UUID, while the current `PartyState` code does not expose a `PartyId` property. For this story, the stable UUID is the EventStore aggregate/stream identity supplied from `CreateParty.PartyId`; do not satisfy identity evidence by adding identity to success events. If direct `PartyState.PartyId` exposure is still needed after inspecting EventStore conventions, record it as an architecture decision gap unless an existing approved aggregate-identity mechanism already supports it.
- The main `src/Hexalith.Parties` project is an actor host. Do not reintroduce public REST, Swagger/OpenAPI, controllers, or in-process MCP tools there for this story.

### Current Code Surfaces To Inspect

```text
src/Hexalith.Parties.Contracts/
  Commands/CreateParty.cs
  Events/PartyCreated.cs
  Events/PartyDisplayNameDerived.cs
  Events/PartyCannotBeCreatedWithInvalidId.cs
  Events/PartyCannotBeCreatedWithoutType.cs
  Events/PartyCannotBeCreatedWithoutPersonDetails.cs
  Events/PartyCannotBeCreatedWithoutOrganizationDetails.cs
  State/PartyState.cs
  ValueObjects/PartyType.cs
  ValueObjects/PersonDetails.cs
  ValueObjects/OrganizationDetails.cs

src/Hexalith.Parties.Server/
  Aggregates/PartyAggregate.cs

tests/Hexalith.Parties.Server.Tests/
  Aggregates/PartyAggregateCreateTests.cs

tests/Hexalith.Parties.Contracts.Tests/
  State/PartyStateTests.cs
```

### Creation Behavior Contract

- New person creation should emit exactly the creation success event sequence the aggregate currently uses: `PartyCreated` followed by `PartyDisplayNameDerived`.
- New organization creation should emit the same success sequence.
- Duplicate creation against non-null `PartyState` should remain an idempotent no-op that emits no events unless a newer documented command-idempotency story changes that rule.
- Invalid command input should return typed rejection events through `DomainResult.Rejection(...)`, not thrown business exceptions.
- `ArgumentNullException` for a null command is acceptable as programmer misuse; business validation failures must be rejection events.

### PartyState Rehydration Guardrails

- `PartyState.Apply(PartyCreated)` sets type, person/organization details, natural-person flag, active default, and creation time.
- `PartyState.Apply(PartyDisplayNameDerived)` sets display and sort names.
- Rejection `Apply` overloads are intentional no-ops and must remain before success `Apply` overloads to protect EventStore suffix-match discovery.
- `CreatedAt` is currently derived during apply time. This story should not introduce a new timestamp contract unless architecture explicitly changes event metadata handling.

### Testing Requirements

- Use xUnit v3 and Shouldly, following existing tests in `PartyAggregateCreateTests` and `PartyStateTests`.
- Cover person success, organization success, display-name derivation, invalid `PartyId`, missing type, missing person details, missing organization details, duplicate creation no-op, and event application to state.
- Focused evidence should include:
  - `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyAggregateCreateTests`
  - `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyStateTests`
- Keep tests pure unit tests; creation behavior should not require Dapr, Aspire, actors, HTTP, databases, or full topology startup.
- If changing public contracts, run the Contracts tests and inspect architecture fitness tests before broader suites.

### Anti-Patterns To Avoid

- Do not create a second aggregate, MediatR handler, controller, or service wrapper for `CreateParty`.
- Do not put tenant ids on the command or success events for this story.
- Do not emit success events after a typed rejection.
- Do not change contact-channel, identifier, lifecycle, GDPR, projection, MCP, admin, or picker behavior while reconciling this story.
- Do not relax contract project dependencies.
- Do not reorder `PartyState.Apply` methods casually.
- Do not use recursive nested submodule initialization.

### Previous Story Intelligence

- Story 1.1 created a ready-for-dev scaffold audit story and warned not to delete later Epic 10-12 boundaries while reconciling early Epic 1 work.
- Historical Story 1.2 established EventStore contract dependency patterns, custom `[PersonalData]`, one-public-type-per-file, sealed record contracts, and no positional records.
- Historical Story 1.3 implemented `PartyAggregate.Handle(CreateParty, PartyState?)`, added creation tests, and recorded review fixes for missing type-specific details, invalid party id, legal-name derivation, and no-op duplicate creation.
- Recent commits show the current run created Story 1.1 (`374daec`) after planning realignment, with older code and story artifacts retained as historical intelligence.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.2] - Story statement and BDD acceptance criteria.
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-05-15.md#FR-coverage] - FR1, FR6, and FR7 mapping to this story.
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-14.md] - Planning warning that broad historical Story 1.2 contract bootstrap is not a future slicing pattern.
- [Source: _bmad-output/project-context.md#Critical-Implementation-Rules] - EventStore aggregate, contract dependency, actor-host, privacy, testing, and submodule guardrails.
- [Source: _bmad-output/implementation-artifacts/1-1-set-up-initial-project-from-eventstore-solution-structure.md] - Previous ready story and current scaffold guardrails.
- [Source: _bmad-output/implementation-artifacts/1-2-domain-contracts-complete-type-definitions.md] - Historical contract implementation intelligence.
- [Source: _bmad-output/implementation-artifacts/1-3-party-aggregate-party-creation.md] - Historical aggregate creation implementation and review intelligence.
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs] - Current aggregate implementation surface.
- [Source: src/Hexalith.Parties.Contracts/State/PartyState.cs] - Current rehydration behavior.
- [Source: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCreateTests.cs] - Current focused creation behavior coverage.
- [Source: tests/Hexalith.Parties.Contracts.Tests/State/PartyStateTests.cs] - Current state apply coverage.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Party-Mode Review

- Date/time: 2026-05-15T18:27:14+02:00
- Selected story key: `1-2-create-party-aggregate-with-stable-identity`
- Command/skill invocation used: `/bmad-party-mode 1-2-create-party-aggregate-with-stable-identity; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - All reviewers found the story valuable and close to ready, but initially recommended `needs-story-update` because identity source and duplicate-create behavior left too much implementation decision budget.
  - The immutable UUID acceptance wording needed to align with EventStore aggregate/stream identity and avoid pushing `PartyId` into `PartyCreated`.
  - Duplicate creation needed one explicit observable behavior for implementation and tests.
  - Invalid type-specific details, no-success-after-rejection evidence, deterministic derived-name assertions, and focused validation commands needed sharper wording.
  - Scope exclusions needed to keep the story on contracts, state, aggregate behavior, and focused tests only.
- Changes applied:
  - Clarified AC1 and AC2 so the stable UUID remains the EventStore aggregate/stream identity supplied by `CreateParty.PartyId`, while rehydrated party state stores type, details, active status, and derived names.
  - Tightened AC4 and the creation behavior contract to the already-documented idempotent duplicate-create no-op with no emitted events.
  - Added an explicit identity guardrail: if EventStore conventions cannot provide the required aggregate-identity evidence without adding `PartyId` to `PartyCreated`, stop and record an architecture decision gap.
  - Expanded rejection/idempotency tasks for type-specific detail mismatch, typed rejection-only outcomes, and no success events after invalid create commands.
  - Added focused filtered test commands for `PartyAggregateCreateTests` and `PartyStateTests`.
- Findings deferred:
  - Direct `PartyState.PartyId` exposure remains an architecture decision unless an existing approved aggregate-identity mechanism already supports it.
  - Cross-party duplicate/person matching remains out of scope; this story only covers duplicate create within the same aggregate stream.
  - Future lifecycle, contact, identifier, GDPR, projection, public API, MCP, admin, picker, and richer display-name canonicalization behavior remain out of scope.
- Final recommendation: ready-for-dev

## Change Log

- 2026-05-15: Party-mode review applied pre-dev clarifications for EventStore identity source, duplicate no-op behavior, rejection evidence, focused validation, and deferred identity decisions.
- 2026-05-15: Story created by BMAD pre-dev hardening automation with current aggregate reconciliation context.
