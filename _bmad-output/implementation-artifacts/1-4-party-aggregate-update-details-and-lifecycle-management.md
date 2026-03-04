# Story 1.4: Party Aggregate — Update Details & Lifecycle Management

Status: done

## Story

As a developer,
I want to update person/organization details and manage party lifecycle (deactivate/reactivate),
so that parties can be maintained throughout their lifecycle with accurate information.

## Acceptance Criteria

1. **Given** an existing person party, **When** an `UpdatePersonDetails` command is handled with new first name and last name, **Then** a `PersonDetailsUpdated` event is emitted with the new details (FR2), **And** a `PartyDisplayNameDerived` event is emitted with the re-derived display name and sort name (FR6).

2. **Given** an existing organization party, **When** an `UpdateOrganizationDetails` command is handled with new legal name, **Then** an `OrganizationDetailsUpdated` event is emitted with the new details (FR3), **And** a `PartyDisplayNameDerived` event is emitted with the re-derived display name (FR6).

3. **Given** an existing organization party with `IsNaturalPerson = false`, **When** a `SetIsNaturalPerson` command is handled with `true`, **Then** an `IsNaturalPersonChanged` event is emitted.

4. **Given** an active party, **When** a `DeactivateParty` command is handled, **Then** a `PartyDeactivated` event is emitted (FR4), **And** the party state reflects `IsActive = false`.

5. **Given** a deactivated party, **When** a `ReactivateParty` command is handled, **Then** a `PartyReactivated` event is emitted (FR5), **And** the party state reflects `IsActive = true`.

6. **Given** an already deactivated party, **When** a `DeactivateParty` command is handled again, **Then** the command is handled idempotently — no duplicate event.

7. **Given** a `CreateParty` command targeting a person party type, **When** an `UpdateOrganizationDetails` command is subsequently handled, **Then** a rejection event is returned (type mismatch).

## Tasks / Subtasks

- [x] Task 1: Add `Handle(UpdatePersonDetails, PartyState?)` to PartyAggregate (AC: #1, #7)
  - [x] 1.1: Validate command not null, state not null (party must exist), type is Person
  - [x] 1.2: Emit `PersonDetailsUpdated` + `PartyDisplayNameDerived` events
  - [x] 1.3: Reuse existing `DeriveDisplayName` helper
- [x] Task 2: Add `Handle(UpdateOrganizationDetails, PartyState?)` to PartyAggregate (AC: #2, #7)
  - [x] 2.1: Validate command not null, state not null, type is Organization
  - [x] 2.2: Emit `OrganizationDetailsUpdated` + `PartyDisplayNameDerived` events
- [x] Task 3: Add `Handle(SetIsNaturalPerson, PartyState?)` to PartyAggregate (AC: #3)
  - [x] 3.1: Validate command not null, state not null, type is Organization
  - [x] 3.2: Idempotency: if `state.IsNaturalPerson == command.IsNaturalPerson`, return `DomainResult.NoOp()`
  - [x] 3.3: Emit `IsNaturalPersonChanged` event
- [x] Task 4: Add `Handle(DeactivateParty, PartyState?)` to PartyAggregate (AC: #4, #6)
  - [x] 4.1: Validate command not null, state not null
  - [x] 4.2: Idempotency: if `state.IsActive == false`, return `DomainResult.NoOp()`
  - [x] 4.3: Emit `PartyDeactivated` event
- [x] Task 5: Add `Handle(ReactivateParty, PartyState?)` to PartyAggregate (AC: #5)
  - [x] 5.1: Validate command not null, state not null
  - [x] 5.2: Idempotency: if `state.IsActive == true`, return `DomainResult.NoOp()`
  - [x] 5.3: Emit `PartyReactivated` event
- [x] Task 6: Verify full solution build (all ACs)
  - [x] 6.1: `dotnet build Hexalith.Parties.slnx` — zero errors, zero warnings (excluding pre-existing ASPIRE004)
  - [x] 6.2: `dotnet test` — all existing tests still pass (zero regressions)

### Review Follow-ups (AI)

- [x] [AI-Review][HIGH] Add explicit payload guard in `Handle(UpdatePersonDetails, PartyState?)` for `command.PersonDetails` and return a domain rejection instead of allowing `DeriveDisplayName(...)` to throw `InvalidOperationException` on null payload (`src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs:63`, `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs:82`, `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs:187`).
- [x] [AI-Review][HIGH] Add explicit payload guard in `Handle(UpdateOrganizationDetails, PartyState?)` for `command.OrganizationDetails` and return a domain rejection instead of allowing `DeriveDisplayName(...)` to throw `InvalidOperationException` on null payload (`src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs:93`, `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs:112`, `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs:187`).
- [x] [AI-Review][MEDIUM] Add/expand aggregate tests for new handlers (`UpdatePersonDetails`, `UpdateOrganizationDetails`, `SetIsNaturalPerson`, `DeactivateParty`, `ReactivateParty`) to validate AC #1-#7 behavior and idempotency (current file has create-flow coverage only: `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateTests.cs`).
- [x] [AI-Review][MEDIUM] Reconcile Dev Agent Record File List with actual git changes (currently missing `_bmad-output/implementation-artifacts/sprint-status.yaml`).
- [x] [AI-Review][MEDIUM] Reconcile review transparency gaps before marking done (working tree changes are now fully reflected in File List, Change Log, and review notes before final status transition).

## Dev Notes

### Implementation Pattern — Follow Story 1.3 Exactly

All new Handle methods go in the **existing** file `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`. Do NOT create new files for this story. The EventStore framework discovers Handle methods via reflection on the `PartyAggregate` class.

**Handle method signature (mandatory):**
```csharp
public static DomainResult Handle(TCommand command, PartyState? state)
```

**Validation order (established in Story 1.3):**
1. `ArgumentNullException.ThrowIfNull(command)` — always first
2. State existence check — `state is null` means party doesn't exist → reject
3. Type-specific guard — reject if wrong party type for the command
4. Idempotency check — return `DomainResult.NoOp()` if action already in desired state
5. Domain logic — emit events

**DomainResult outcomes:**
- `DomainResult.Success([event1, event2])` — events to persist
- `DomainResult.Rejection([rejectionEvent])` — business rule violation
- `DomainResult.NoOp()` — idempotent duplicate, no action needed

### Exact Handle Method Implementations

#### Handle(UpdatePersonDetails)
```csharp
public static DomainResult Handle(UpdatePersonDetails command, PartyState? state)
{
    ArgumentNullException.ThrowIfNull(command);

    if (state is null)
    {
        return DomainResult.Rejection([new PartyTypeMismatch { Message = "Party does not exist." }]);
    }

    if (state.Type != PartyType.Person)
    {
        return DomainResult.Rejection([new PartyTypeMismatch { Message = $"Cannot update person details on a {state.Type} party." }]);
    }

    PersonDetailsUpdated updated = new()
    {
        PersonDetails = command.PersonDetails,
    };

    (string displayName, string sortName) = DeriveDisplayName(PartyType.Person, command.PersonDetails, null);

    PartyDisplayNameDerived nameDerived = new()
    {
        DisplayName = displayName,
        SortName = sortName,
    };

    return DomainResult.Success([updated, nameDerived]);
}
```

#### Handle(UpdateOrganizationDetails)
```csharp
public static DomainResult Handle(UpdateOrganizationDetails command, PartyState? state)
{
    ArgumentNullException.ThrowIfNull(command);

    if (state is null)
    {
        return DomainResult.Rejection([new PartyTypeMismatch { Message = "Party does not exist." }]);
    }

    if (state.Type != PartyType.Organization)
    {
        return DomainResult.Rejection([new PartyTypeMismatch { Message = $"Cannot update organization details on a {state.Type} party." }]);
    }

    OrganizationDetailsUpdated updated = new()
    {
        OrganizationDetails = command.OrganizationDetails,
    };

    (string displayName, string sortName) = DeriveDisplayName(PartyType.Organization, null, command.OrganizationDetails);

    PartyDisplayNameDerived nameDerived = new()
    {
        DisplayName = displayName,
        SortName = sortName,
    };

    return DomainResult.Success([updated, nameDerived]);
}
```

#### Handle(SetIsNaturalPerson)
```csharp
public static DomainResult Handle(SetIsNaturalPerson command, PartyState? state)
{
    ArgumentNullException.ThrowIfNull(command);

    if (state is null)
    {
        return DomainResult.Rejection([new PartyTypeMismatch { Message = "Party does not exist." }]);
    }

    if (state.Type != PartyType.Organization)
    {
        return DomainResult.Rejection([new PartyTypeMismatch { Message = $"SetIsNaturalPerson only applies to organization parties." }]);
    }

    // Idempotency: no change needed if already at desired value
    if (state.IsNaturalPerson == command.IsNaturalPerson)
    {
        return DomainResult.NoOp();
    }

    IsNaturalPersonChanged changed = new()
    {
        IsNaturalPerson = command.IsNaturalPerson,
    };

    return DomainResult.Success([changed]);
}
```

#### Handle(DeactivateParty)
```csharp
public static DomainResult Handle(DeactivateParty command, PartyState? state)
{
    ArgumentNullException.ThrowIfNull(command);

    if (state is null)
    {
        return DomainResult.Rejection([new PartyCannotBeDeactivatedWhenInactive()]);
    }

    // AC#6: Idempotent — already deactivated
    if (!state.IsActive)
    {
        return DomainResult.NoOp();
    }

    return DomainResult.Success([new PartyDeactivated()]);
}
```

#### Handle(ReactivateParty)
```csharp
public static DomainResult Handle(ReactivateParty command, PartyState? state)
{
    ArgumentNullException.ThrowIfNull(command);

    if (state is null)
    {
        return DomainResult.Rejection([new PartyCannotBeReactivatedWhenActive()]);
    }

    // Idempotent — already active
    if (state.IsActive)
    {
        return DomainResult.NoOp();
    }

    return DomainResult.Success([new PartyReactivated()]);
}
```

### Critical Implementation Rules

1. **Handle is `static`** — the framework discovers static methods via reflection
2. **Return type is `DomainResult`** — synchronous, never `Task<DomainResult>`
3. **No I/O in Handle** — pure domain logic only
4. **State is nullable** — `null` means party never created (reject all update/lifecycle commands)
5. **Events carry NO PartyId** — aggregate identity lives in EventEnvelope metadata
6. **Reuse `DeriveDisplayName`** — already exists as a private static helper in `PartyAggregate`
7. **DO NOT modify Contracts** — all commands, events, state Apply methods, and rejection events already exist from Story 1.2
8. **DO NOT add tests** — unit tests are Story 1.5's scope

### Display Name Re-Derivation (FR6)

Both `UpdatePersonDetails` and `UpdateOrganizationDetails` must emit a `PartyDisplayNameDerived` event with re-derived values. The existing `DeriveDisplayName` helper handles this:

| Party Type | Display Name | Sort Name |
|---|---|---|
| Person | `"{FirstName} {LastName}"` | `"{LastName}, {FirstName}"` |
| Organization | `LegalName` | `LegalName` |

### Idempotency Strategy

| Command | Idempotent Condition | Result |
|---|---|---|
| `DeactivateParty` | `state.IsActive == false` | `DomainResult.NoOp()` |
| `ReactivateParty` | `state.IsActive == true` | `DomainResult.NoOp()` |
| `SetIsNaturalPerson` | `state.IsNaturalPerson == command.IsNaturalPerson` | `DomainResult.NoOp()` |
| `UpdatePersonDetails` | Not idempotent — always emit update | `DomainResult.Success(...)` |
| `UpdateOrganizationDetails` | Not idempotent — always emit update | `DomainResult.Success(...)` |

Update commands are NOT idempotent because the caller may re-send with different data. The event stream records every update.

### Type Mismatch Rejection

The `PartyTypeMismatch` rejection event (already defined in Contracts) is used when:
- `UpdatePersonDetails` is sent to an Organization party
- `UpdateOrganizationDetails` is sent to a Person party
- `SetIsNaturalPerson` is sent to a Person party

### Existing Contracts (DO NOT MODIFY)

All types below already exist from Story 1.2. They are listed here for reference only.

**Commands:**
- `UpdatePersonDetails` — `{ PartyId: string, PersonDetails: PersonDetails }`
- `UpdateOrganizationDetails` — `{ PartyId: string, OrganizationDetails: OrganizationDetails }`
- `SetIsNaturalPerson` — `{ PartyId: string, IsNaturalPerson: bool }`
- `DeactivateParty` — `{ PartyId: string }`
- `ReactivateParty` — `{ PartyId: string }`

**Events:**
- `PersonDetailsUpdated` — `{ PersonDetails: PersonDetails }` (implements `IEventPayload`)
- `OrganizationDetailsUpdated` — `{ OrganizationDetails: OrganizationDetails }` (implements `IEventPayload`)
- `IsNaturalPersonChanged` — `{ IsNaturalPerson: bool }` (implements `IEventPayload`)
- `PartyDeactivated` — empty record (implements `IEventPayload`)
- `PartyReactivated` — empty record (implements `IEventPayload`)
- `PartyDisplayNameDerived` — `{ DisplayName: string, SortName: string }` (implements `IEventPayload`)

**Rejection Events:**
- `PartyTypeMismatch` — `{ Message: string? }` (implements `IRejectionEvent`)
- `PartyCannotBeDeactivatedWhenInactive` — empty record (implements `IRejectionEvent`)
- `PartyCannotBeReactivatedWhenActive` — empty record (implements `IRejectionEvent`)

**State Apply methods** already implemented in `PartyState.cs`:
- `Apply(PersonDetailsUpdated)` — updates `Person` property
- `Apply(OrganizationDetailsUpdated)` — updates `Organization` property
- `Apply(IsNaturalPersonChanged)` — updates `IsNaturalPerson`
- `Apply(PartyDeactivated)` — sets `IsActive = false`
- `Apply(PartyReactivated)` — sets `IsActive = true`
- `Apply(PartyDisplayNameDerived)` — updates `DisplayName` and `SortName`

### Project Structure Notes

**Modified file (this story):**
```
src/Hexalith.Parties.Server/
└── Aggregates/
    └── PartyAggregate.cs      ← MODIFY (add 5 new Handle methods)
```

**No new files needed.** All contracts are stable from Story 1.2.

**Required using directives** (already present from Story 1.3):
```csharp
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
```

**Namespace:** `Hexalith.Parties.Server.Aggregates`

### Anti-Patterns to Avoid

- **DO NOT** add PartyId to events — aggregate ID is in the EventEnvelope
- **DO NOT** make Handle async — always synchronous `DomainResult`
- **DO NOT** add instance fields/state to the aggregate class — use static Handle
- **DO NOT** throw exceptions for business rule violations — use `DomainResult.Rejection()`
- **DO NOT** add TenantId to commands or events — extracted from request context
- **DO NOT** create new files — all Handle methods go in the existing `PartyAggregate.cs`
- **DO NOT** modify any Contracts files — they are stable from Story 1.2
- **DO NOT** add unit tests — testing is Story 1.5
- **DO NOT** create `Handlers/` folder or MediatR handler — EventStore handles routing via reflection

### Previous Story Intelligence (Story 1.3)

**Key learnings:**
- The `DeriveDisplayName` private static helper is already in `PartyAggregate.cs` — reuse it directly
- `PartyState?` being `null` means the aggregate has never been created (no events exist for this ID)
- Review 1 found that type-specific detail validation was initially missing — this story must validate party type matches command type from the start
- Review 2 confirmed the `DeriveDisplayName` default case should throw `InvalidOperationException` (already fixed)
- Existing 21 tests from previous stories must continue to pass (zero regressions)
- Pre-existing ASPIRE004 build warning is unrelated — ignore it

**Code style confirmed:**
- Allman braces, 4-space indent, CRLF, UTF-8
- File-scoped namespaces
- `sealed record` for commands/events, `sealed class` for aggregate/state
- `{ get; init; }` for record properties
- `ArgumentNullException.ThrowIfNull()` as first line in every Handle method

### Git Intelligence

**Branch naming pattern:** `implement-story-1-4-party-aggregate` (follows established convention)
**Commit message pattern:** `Implement Story 1.4: Party Aggregate — Update Details & Lifecycle Management`

**Recent commits:**
```
d8777b1 Merge pull request #4 from Hexalith/implement-story-1-3-party-aggregate
66e8afd Implement Story 1.3: Party Aggregate — Party Creation
a87ca78 Merge pull request #3 from Hexalith/implement-story-1-2-domain-contracts
0297da7 Implement Story 1.2: Domain Contracts — Complete Type Definitions
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Story 1.4 acceptance criteria, lines 469-508]
- [Source: _bmad-output/planning-artifacts/architecture.md — Aggregate pattern, Handle/Apply convention, DomainResult API]
- [Source: _bmad-output/implementation-artifacts/1-3-party-aggregate-party-creation.md — Previous story implementation patterns and learnings]
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs — Current aggregate with CreateParty Handle and DeriveDisplayName helper]
- [Source: src/Hexalith.Parties.Contracts/State/PartyState.cs — Apply methods for all events already implemented]
- [Source: src/Hexalith.Parties.Contracts/Commands/ — UpdatePersonDetails, UpdateOrganizationDetails, SetIsNaturalPerson, DeactivateParty, ReactivateParty]
- [Source: src/Hexalith.Parties.Contracts/Events/ — PersonDetailsUpdated, OrganizationDetailsUpdated, IsNaturalPersonChanged, PartyDeactivated, PartyReactivated, PartyDisplayNameDerived]
- [Source: src/Hexalith.Parties.Contracts/Events/ — PartyTypeMismatch, PartyCannotBeDeactivatedWhenInactive, PartyCannotBeReactivatedWhenActive (rejection events)]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

- Build error CS0121: Ambiguous call in existing test `Handle_NullCommand_ThrowsArgumentNullException` due to multiple Handle overloads. Fixed by adding explicit `(CreateParty)` cast to the null argument.

### Completion Notes List

- Added 5 static Handle methods to PartyAggregate: UpdatePersonDetails, UpdateOrganizationDetails, SetIsNaturalPerson, DeactivateParty, ReactivateParty
- All methods follow established validation order: null check, state existence, type guard, idempotency, domain logic
- UpdatePersonDetails and UpdateOrganizationDetails emit paired events (details updated + display name re-derived) using existing DeriveDisplayName helper
- SetIsNaturalPerson, DeactivateParty, ReactivateParty implement idempotency via DomainResult.NoOp()
- Type mismatch rejections implemented for cross-type command attempts (AC#7)
- Fixed pre-existing test ambiguity caused by new overloads (added explicit cast)
- Build: zero errors, 1 pre-existing ASPIRE004 warning
- Tests: 32 pass (21 contracts + 11 server), zero regressions

### Change Log

- 2026-03-04: Implemented Story 1.4 — Added 5 Handle methods for update details and lifecycle management. Fixed test ambiguity in PartyAggregateTests.cs.
- 2026-03-04: Senior Developer Review (AI) completed. Requested changes: 2 HIGH, 3 MEDIUM findings; story returned to in-progress and follow-up tasks added.
- 2026-03-04: Fixed review findings for null payload handling in update handlers and expanded aggregate tests for Story 1.4 commands.
- 2026-03-04: Closed remaining review follow-up, approved review outcome, and marked Story 1.4 as done.

### File List

- src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs (modified — added 5 Handle methods)
- tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateTests.cs (modified — fixed ambiguous null call and expanded Story 1.4 handler coverage)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified — synced 1-4 status to in-progress)

### Senior Developer Review (AI)

Date: 2026-03-04
Reviewer: Jérôme
Outcome: Approved

Summary:
- Story claims are implemented and verified after remediation.
- Null payload paths now return deterministic domain rejections instead of throwing.
- Story metadata and File List are synchronized with actual modified files.
- Expanded Story 1.4 aggregate tests pass.

Remediation Applied (2026-03-04):
- Added explicit null payload guards in `Handle(UpdatePersonDetails, PartyState?)` and `Handle(UpdateOrganizationDetails, PartyState?)`, returning `DomainResult.Rejection` with `PartyTypeMismatch` messages instead of throwing via `DeriveDisplayName`.
- Expanded `PartyAggregateTests` with focused Story 1.4 coverage (null payload rejection, type mismatch rejection, idempotency no-op, and update success path).

Findings (Final Status):
- HIGH (resolved): `Handle(UpdatePersonDetails...)` null payload path now guarded with rejection.
- HIGH (resolved): `Handle(UpdateOrganizationDetails...)` null payload path now guarded with rejection.
- MEDIUM (resolved): File List now includes `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- MEDIUM (resolved): Story status flow reconciled (`review` → `in-progress` during fixes → `done` after closure).
- MEDIUM (resolved): Added dedicated Story 1.4 aggregate tests in `PartyAggregateTests`.
