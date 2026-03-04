# Story 1.5: Party Aggregate Tier 1 Unit Tests

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want comprehensive Tier 1 unit tests for all party aggregate Handle/Apply methods,
so that domain logic correctness is verified without any infrastructure dependencies.

## Acceptance Criteria

1. **Given** the Hexalith.Parties.Server.Tests project, **When** all aggregate tests are implemented, **Then** the following test classes exist:
   - `PartyAggregateCreateTests` — person creation, organization creation, idempotent create, rejection scenarios
   - `PartyAggregateUpdateTests` — update person details, update organization details, type mismatch rejection, display name re-derivation
   - `PartyAggregateLifecycleTests` — deactivate, reactivate, idempotent deactivate, reactivate already active

2. **Given** all test classes, **When** reviewed for naming convention, **Then** tests follow naming convention: `{Method}_{Scenario}_{ExpectedResult}`

3. **Given** all test classes, **When** reviewed for assertions, **Then** Shouldly assertions are used for all assertions

4. **Given** the Testing project, **When** reviewed for test data, **Then** `PartyTestData` static class exists in the Testing project with builder methods: `ValidCreatePerson()`, `ValidCreateOrganization()`, etc.

5. **Given** all tests, **When** reviewed for infrastructure dependencies, **Then** all tests are Tier 1 compliant — zero infrastructure dependencies (no DAPR, no HTTP, no database)

6. **Given** all tests, **When** run with `dotnet test tests/Hexalith.Parties.Server.Tests/`, **Then** all tests pass

7. **Given** all tests, **When** reviewed for Apply method coverage, **Then** Apply method correctness is verified — events applied to state produce the expected state mutations

## Tasks / Subtasks

- [x] Task 1: Create `PartyTestData` static class in Testing project (AC: #4)
  - [x] 1.1: Create `src/Hexalith.Parties.Testing/PartyTestData.cs`
  - [x] 1.2: Add `ValidCreatePerson()` — returns a `CreateParty` command with Person type and valid PersonDetails
  - [x] 1.3: Add `ValidCreateOrganization()` — returns a `CreateParty` command with Organization type and valid OrganizationDetails
  - [x] 1.4: Add `ValidPersonDetails()` — returns valid PersonDetails value object
  - [x] 1.5: Add `ValidOrganizationDetails()` — returns valid OrganizationDetails value object
  - [x] 1.6: Add `ValidUpdatePersonDetails()` — returns an UpdatePersonDetails command with valid PersonDetails
  - [x] 1.7: Add `ValidUpdateOrganizationDetails()` — returns an UpdateOrganizationDetails command with valid OrganizationDetails
  - [x] 1.8: Add `ValidSetIsNaturalPerson(bool value = true)` — returns a SetIsNaturalPerson command
  - [x] 1.9: Add `ValidDeactivateParty()` — returns a DeactivateParty command
  - [x] 1.10: Add `ValidReactivateParty()` — returns a ReactivateParty command
  - [x] 1.11: Add `CreatePersonState()` — applies CreateParty(Person) + DisplayNameDerived to create a base person PartyState
  - [x] 1.12: Add `CreateOrganizationState()` — applies CreateParty(Organization) + DisplayNameDerived to create a base organization PartyState
  - [x] 1.13: Add `CreateDeactivatedPersonState()` — extends CreatePersonState with PartyDeactivated applied
  - [x] 1.14: Add `CreateDeactivatedOrganizationState()` — extends CreateOrganizationState with PartyDeactivated applied
  - [x] 1.15: All builder methods use a consistent PartyId (valid GUID string)

- [x] Task 2: Create `PartyAggregateCreateTests` class (AC: #1, #2, #3, #5, #7)
  - [x] 2.1: Create `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCreateTests.cs`
  - [x] 2.2: Move/refactor existing CreateParty tests from `PartyAggregateTests.cs` into this class
  - [x] 2.3: Test: `Handle_CreatePersonParty_EmitsPartyCreatedAndDisplayNameDerived` — verify two events emitted with correct types and data
  - [x] 2.4: Test: `Handle_CreateOrganizationParty_EmitsPartyCreatedAndDisplayNameDerived` — verify org creation events
  - [x] 2.5: Test: `Handle_CreatePersonParty_DisplayNameIsDerivedCorrectly` — verify "{FirstName} {LastName}" format
  - [x] 2.6: Test: `Handle_CreateOrganizationParty_DisplayNameUsesLegalName` — verify LegalName as display/sort name
  - [x] 2.7: Test: `Handle_CreateOrganizationParty_NullTradingName_UsesLegalName` — fallback behavior
  - [x] 2.8: Test: `Handle_CreatePartyWhenAlreadyExists_ReturnsNoOp` — idempotency
  - [x] 2.9: Test: `Handle_CreatePartyWithDefaultType_ReturnsRejection` — PartyCannotBeCreatedWithoutType
  - [x] 2.10: Test: `Handle_CreatePersonWithoutPersonDetails_ReturnsRejection` — PartyCannotBeCreatedWithoutPersonDetails
  - [x] 2.11: Test: `Handle_CreateOrganizationWithoutOrgDetails_ReturnsRejection` — PartyCannotBeCreatedWithoutOrganizationDetails
  - [x] 2.12: Test: `Handle_CreatePartyWithNullCommand_ThrowsArgumentNullException`
  - [x] 2.13: Test: `Handle_CreatePartyWithInvalidId_ReturnsRejection` — Theory with InlineData: "", "   ", "not-a-guid"
  - [x] 2.14: Test: `Handle_CreatePersonParty_ApplyEventsToState_ProducesCorrectState` — verify PartyState after applying emitted events

- [x] Task 3: Create `PartyAggregateUpdateTests` class (AC: #1, #2, #3, #5, #7)
  - [x] 3.1: Create `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateUpdateTests.cs`
  - [x] 3.2: Test: `Handle_UpdatePersonDetails_ValidState_EmitsUpdatedAndDisplayNameDerived` — verify two events with correct data
  - [x] 3.3: Test: `Handle_UpdatePersonDetails_NullState_ReturnsRejection` — party must exist
  - [x] 3.4: Test: `Handle_UpdatePersonDetails_OnOrganization_ReturnsTypeMismatch` — type guard
  - [x] 3.5: Test: `Handle_UpdatePersonDetails_NullPayload_ReturnsRejection` — null PersonDetails guard
  - [x] 3.6: Test: `Handle_UpdatePersonDetails_NullCommand_ThrowsArgumentNullException`
  - [x] 3.7: Test: `Handle_UpdatePersonDetails_DisplayNameReDerived_CorrectFormat` — verify "{NewFirst} {NewLast}"
  - [x] 3.8: Test: `Handle_UpdatePersonDetails_ApplyEvents_StateReflectsNewDetails` — Apply verification
  - [x] 3.9: Test: `Handle_UpdateOrganizationDetails_ValidState_EmitsUpdatedAndDisplayNameDerived`
  - [x] 3.10: Test: `Handle_UpdateOrganizationDetails_NullState_ReturnsRejection`
  - [x] 3.11: Test: `Handle_UpdateOrganizationDetails_OnPerson_ReturnsTypeMismatch`
  - [x] 3.12: Test: `Handle_UpdateOrganizationDetails_NullPayload_ReturnsRejection`
  - [x] 3.13: Test: `Handle_UpdateOrganizationDetails_NullCommand_ThrowsArgumentNullException`
  - [x] 3.14: Test: `Handle_UpdateOrganizationDetails_DisplayNameReDerived_UsesNewLegalName`
  - [x] 3.15: Test: `Handle_UpdateOrganizationDetails_ApplyEvents_StateReflectsNewDetails`
  - [x] 3.16: Test: `Handle_SetIsNaturalPerson_ValidOrg_EmitsChangedEvent`
  - [x] 3.17: Test: `Handle_SetIsNaturalPerson_NullState_ReturnsRejection`
  - [x] 3.18: Test: `Handle_SetIsNaturalPerson_OnPerson_ReturnsTypeMismatch`
  - [x] 3.19: Test: `Handle_SetIsNaturalPerson_SameValue_ReturnsNoOp` — idempotency
  - [x] 3.20: Test: `Handle_SetIsNaturalPerson_NullCommand_ThrowsArgumentNullException`
  - [x] 3.21: Test: `Handle_SetIsNaturalPerson_ApplyEvent_StateReflectsChange`

- [x] Task 4: Create `PartyAggregateLifecycleTests` class (AC: #1, #2, #3, #5, #7)
  - [x] 4.1: Create `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateLifecycleTests.cs`
  - [x] 4.2: Test: `Handle_DeactivateParty_ActiveParty_EmitsDeactivatedEvent`
  - [x] 4.3: Test: `Handle_DeactivateParty_NullState_ReturnsRejection` — party must exist
  - [x] 4.4: Test: `Handle_DeactivateParty_AlreadyInactive_ReturnsNoOp` — idempotency
  - [x] 4.5: Test: `Handle_DeactivateParty_NullCommand_ThrowsArgumentNullException`
  - [x] 4.6: Test: `Handle_DeactivateParty_ApplyEvent_StateReflectsInactive`
  - [x] 4.7: Test: `Handle_ReactivateParty_DeactivatedParty_EmitsReactivatedEvent`
  - [x] 4.8: Test: `Handle_ReactivateParty_NullState_ReturnsRejection`
  - [x] 4.9: Test: `Handle_ReactivateParty_AlreadyActive_ReturnsNoOp` — idempotency
  - [x] 4.10: Test: `Handle_ReactivateParty_NullCommand_ThrowsArgumentNullException`
  - [x] 4.11: Test: `Handle_ReactivateParty_ApplyEvent_StateReflectsActive`
  - [x] 4.12: Test: `Handle_DeactivateThenReactivate_FullCycle_StateIsActive` — round-trip lifecycle

- [x] Task 5: Remove old `PartyAggregateTests.cs` and verify (AC: #6)
  - [x] 5.1: Delete `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateTests.cs`
  - [x] 5.2: Verify no duplicate test methods across the 3 new classes
  - [x] 5.3: `dotnet build Hexalith.Parties.slnx` — zero errors
  - [x] 5.4: `dotnet test tests/Hexalith.Parties.Server.Tests/` — all tests pass
  - [x] 5.5: `dotnet test` (full solution) — all tests pass, zero regressions

## Dev Notes

### Implementation Strategy — Refactor Existing + Expand

Story 1.4 already created 19 tests in `PartyAggregateTests.cs`. This story restructures them into 3 focused classes and expands coverage to be comprehensive. The goal is:
1. **PartyAggregateCreateTests** — all CreateParty handler tests (migrated from existing + new)
2. **PartyAggregateUpdateTests** — UpdatePersonDetails, UpdateOrganizationDetails, SetIsNaturalPerson
3. **PartyAggregateLifecycleTests** — DeactivateParty, ReactivateParty

### Test Pattern — Aggregate Handle Tests

All tests follow this pattern:

```csharp
[Fact]
public void Handle_Scenario_ExpectedResult()
{
    // Arrange
    var command = PartyTestData.ValidCreatePerson();
    PartyState? state = null; // or build state via Apply

    // Act
    DomainResult result = PartyAggregate.Handle(command, state);

    // Assert (Shouldly)
    result.IsSuccess.ShouldBeTrue();
    result.Events.Count.ShouldBe(2);
    result.Events[0].ShouldBeOfType<PartyCreated>();
}
```

### Test Pattern — Apply Verification Tests

To verify Apply method correctness, tests should:
1. Call Handle to get emitted events
2. Apply those events to a fresh or existing PartyState
3. Assert the resulting state properties

```csharp
[Fact]
public void Handle_CreatePersonParty_ApplyEventsToState_ProducesCorrectState()
{
    // Arrange
    var command = PartyTestData.ValidCreatePerson();

    // Act
    DomainResult result = PartyAggregate.Handle(command, null);

    // Apply events to state
    var state = new PartyState();
    foreach (var evt in result.Events)
    {
        state.Apply((dynamic)evt);
    }

    // Assert
    state.Type.ShouldBe(PartyType.Person);
    state.Person.ShouldNotBeNull();
    state.Person.FirstName.ShouldBe(command.PersonDetails!.FirstName);
    state.DisplayName.ShouldNotBeNullOrWhiteSpace();
}
```

### PartyTestData Builder Design

The `PartyTestData` class in `src/Hexalith.Parties.Testing/PartyTestData.cs` provides:
- **Command builders** that return pre-populated valid commands
- **State builders** that create PartyState via event application (realistic state, not direct property setting)
- A **consistent PartyId** across all builders (valid GUID: `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"`)

State builders create state by replaying events through Apply methods — this ensures state is consistent with how it would be in production.

```csharp
public static class PartyTestData
{
    public const string DefaultPartyId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

    public static CreateParty ValidCreatePerson() => new()
    {
        PartyId = DefaultPartyId,
        Type = PartyType.Person,
        PersonDetails = ValidPersonDetails(),
    };

    public static PersonDetails ValidPersonDetails() => new()
    {
        FirstName = "John",
        LastName = "Doe",
    };

    // ... state builders apply events to PartyState
    public static PartyState CreatePersonState()
    {
        var state = new PartyState();
        state.Apply(new PartyCreated
        {
            Type = PartyType.Person,
            PersonDetails = ValidPersonDetails(),
        });
        state.Apply(new PartyDisplayNameDerived
        {
            DisplayName = "John Doe",
            SortName = "Doe, John",
        });
        return state;
    }
}
```

### Critical Implementation Rules

1. **Tests are static Handle method calls** — `PartyAggregate.Handle(command, state)` — no mocking needed
2. **DomainResult outcomes**: `.IsSuccess`, `.IsRejection`, `.IsNoOp` — check the right outcome
3. **Events collection**: `result.Events` — verify count and types
4. **Rejection events**: `result.Events[0].ShouldBeOfType<TRejection>()` — verify correct rejection type
5. **Shouldly only** — no `Assert.Equal`, no `Assert.True` — use `.ShouldBe()`, `.ShouldBeTrue()`, `.ShouldBeOfType<T>()`
6. **No infrastructure** — zero DAPR, HTTP, database, or external service references in tests
7. **xUnit attributes**: `[Fact]` for single tests, `[Theory]` + `[InlineData]` for parameterized tests
8. **File-scoped namespaces**, Allman braces, 4-space indentation
9. **One class per file** — file name matches class name
10. **DO NOT modify Contracts or Server projects** — this story is tests-only plus the Testing project's `PartyTestData`

### DomainResult API Reference

From Story 1.4 and the aggregate implementation:

```csharp
// Check result type
result.IsSuccess    // events to persist
result.IsRejection  // business rule violation
result.IsNoOp       // idempotent duplicate

// Access events
result.Events       // IReadOnlyList<object> — success events or rejection events
result.Events.Count // number of events
result.Events[0]    // first event (cast to specific type)
```

### Existing Contracts Reference (DO NOT MODIFY)

**Commands used in tests:**
- `CreateParty` — `{ PartyId, Type, PersonDetails?, OrganizationDetails? }`
- `UpdatePersonDetails` — `{ PartyId, PersonDetails }`
- `UpdateOrganizationDetails` — `{ PartyId, OrganizationDetails }`
- `SetIsNaturalPerson` — `{ PartyId, IsNaturalPerson }`
- `DeactivateParty` — `{ PartyId }`
- `ReactivateParty` — `{ PartyId }`

**Events to verify in results:**
- `PartyCreated` — `{ Type, PersonDetails?, OrganizationDetails? }`
- `PersonDetailsUpdated` — `{ PersonDetails }`
- `OrganizationDetailsUpdated` — `{ OrganizationDetails }`
- `IsNaturalPersonChanged` — `{ IsNaturalPerson }`
- `PartyDeactivated` — empty
- `PartyReactivated` — empty
- `PartyDisplayNameDerived` — `{ DisplayName, SortName }`

**Rejection events to verify:**
- `PartyCannotBeCreatedWithInvalidId` — empty
- `PartyCannotBeCreatedWithoutType` — empty
- `PartyCannotBeCreatedWithoutPersonDetails` — empty
- `PartyCannotBeCreatedWithoutOrganizationDetails` — empty
- `PartyTypeMismatch` — `{ Message? }`
- `PartyCannotBeDeactivatedWhenInactive` — empty
- `PartyCannotBeReactivatedWhenActive` — empty

**Value objects for test data:**
- `PersonDetails` — `{ FirstName (required), LastName (required), DateOfBirth?, Prefix?, Suffix? }`
- `OrganizationDetails` — `{ LegalName (required), TradingName?, LegalForm?, RegistrationNumber?, IsNaturalPerson }`

### Display Name Derivation Rules (FR6)

| Party Type | Display Name | Sort Name |
|---|---|---|
| Person | `"{FirstName} {LastName}"` | `"{LastName}, {FirstName}"` |
| Organization | `LegalName` | `LegalName` |

Tests must verify these exact formats for both creation and update scenarios.

### Idempotency Matrix

| Command | Idempotent Condition | Expected Result |
|---|---|---|
| `CreateParty` | `state is not null` (already created) | `DomainResult.NoOp()` |
| `DeactivateParty` | `state.IsActive == false` | `DomainResult.NoOp()` |
| `ReactivateParty` | `state.IsActive == true` | `DomainResult.NoOp()` |
| `SetIsNaturalPerson` | `state.IsNaturalPerson == command.IsNaturalPerson` | `DomainResult.NoOp()` |
| `UpdatePersonDetails` | NOT idempotent — always emits | `DomainResult.Success(...)` |
| `UpdateOrganizationDetails` | NOT idempotent — always emits | `DomainResult.Success(...)` |

### Project Structure Notes

**New files (this story):**
```
src/Hexalith.Parties.Testing/
└── PartyTestData.cs                            ← NEW

tests/Hexalith.Parties.Server.Tests/
└── Aggregates/
    ├── PartyAggregateCreateTests.cs            ← NEW (replaces PartyAggregateTests.cs)
    ├── PartyAggregateUpdateTests.cs            ← NEW
    └── PartyAggregateLifecycleTests.cs         ← NEW
```

**Deleted files:**
```
tests/Hexalith.Parties.Server.Tests/
└── Aggregates/
    └── PartyAggregateTests.cs                  ← DELETE (split into 3 files above)
```

**No modifications to existing src/ projects.** This story touches only test files and the Testing project.

### Anti-Patterns to Avoid

- **DO NOT** use `Assert.Equal` or `Assert.True` — use Shouldly (`.ShouldBe()`, `.ShouldBeTrue()`, `.ShouldBeOfType<T>()`)
- **DO NOT** mock the aggregate or state — call static Handle methods directly
- **DO NOT** set PartyState properties directly — build state via Apply methods for realistic test state
- **DO NOT** add infrastructure dependencies — no DAPR, no HTTP, no database in Tier 1 tests
- **DO NOT** modify any files in Contracts or Server projects — tests only
- **DO NOT** create test state by setting properties with reflection — use Apply methods
- **DO NOT** use `dynamic` dispatch for Apply unless necessary — prefer explicit typed Apply calls
- **DO NOT** add NSubstitute mocks — aggregate tests are pure function tests, no dependencies to mock

### Previous Story Intelligence (Story 1.4)

**Key learnings from Story 1.4:**
- The existing 19 tests in `PartyAggregateTests.cs` cover creation, some update scenarios, and some lifecycle scenarios
- Story 1.4 review found null payload handling issues in UpdatePersonDetails and UpdateOrganizationDetails — explicit guards were added
- Test ambiguity bug: `Handle_NullCommand_ThrowsArgumentNullException` needed explicit `(CreateParty)` cast due to multiple Handle overloads — **all null command tests must cast to the specific command type**
- Pre-existing ASPIRE004 build warning is unrelated — ignore it
- 32 tests currently pass across the full solution (21 contracts + 11 server)

**Code patterns established:**
- Allman braces, 4-space indent, CRLF, UTF-8
- File-scoped namespaces
- `sealed record` for commands/events
- `ArgumentNullException.ThrowIfNull()` as first line in every Handle method
- PartyState built via Apply methods, not direct property assignment

### Git Intelligence

**Branch naming pattern:** `implement-story-1-5-party-aggregate-tier-1-unit-tests`
**Commit message pattern:** `Implement Story 1.5: Party Aggregate Tier 1 Unit Tests`

**Recent commits:**
```
a3caccb Merge pull request #5 from Hexalith/implement-story-1-4-party-aggregate-update-lifecycle
04db0bd Implement Story 1.4: Party Aggregate — Update Details & Lifecycle Management
d8777b1 Merge pull request #4 from Hexalith/implement-story-1-3-party-aggregate
66e8afd Implement Story 1.3: Party Aggregate — Party Creation
a87ca78 Merge pull request #3 from Hexalith/implement-story-1-2-domain-contracts
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Story 1.5 acceptance criteria, lines 509-529]
- [Source: _bmad-output/planning-artifacts/architecture.md — Testing framework (xUnit, Shouldly, NSubstitute), Tier 1 strategy, D19 test matrix approach]
- [Source: _bmad-output/implementation-artifacts/1-4-party-aggregate-update-details-and-lifecycle-management.md — Previous story patterns, debug insights, review findings]
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs — All 6 Handle methods and DeriveDisplayName helper]
- [Source: src/Hexalith.Parties.Contracts/State/PartyState.cs — All Apply methods for event-driven state mutations]
- [Source: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateTests.cs — Existing 19 tests to refactor/expand]
- [Source: src/Hexalith.Parties.Contracts/Commands/ — All command types referenced in tests]
- [Source: src/Hexalith.Parties.Contracts/Events/ — All event types verified in test assertions]
- [Source: src/Hexalith.Parties.Contracts/ValueObjects/ — PersonDetails, OrganizationDetails for test data builders]

## Change Log

- 2026-03-04: Implemented Story 1.5 — Created PartyTestData builder class, restructured 19 existing tests into 3 focused test classes (Create, Update, Lifecycle), expanded to 45 comprehensive tests covering all Handle/Apply methods. Deleted old PartyAggregateTests.cs. Full solution: 66 tests passing (21 contracts + 45 server), zero regressions.
- 2026-03-04: Senior review fixes applied — strengthened 4 tests to assert emitted event payload data (not only event type/count), validated `dotnet build Hexalith.Parties.slnx`, and re-ran story scope tests with 66/66 passing.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

Senior review identified assertion-strength gaps in 4 tests; fixed and re-validated. Build succeeded (1 pre-existing ASPIRE004 warning), tests passed: 66/66.

### Completion Notes List

- Created `PartyTestData` static class with 14 builder methods (7 command builders + 4 state builders + 3 value object builders), all using consistent DefaultPartyId GUID
- Created `PartyAggregateCreateTests` with 13 tests (11 Fact + 1 Theory with 3 InlineData) covering person/org creation, display name derivation, idempotency, rejections, null/invalid guards, and Apply verification
- Created `PartyAggregateUpdateTests` with 21 tests covering UpdatePersonDetails (8 tests), UpdateOrganizationDetails (7 tests), and SetIsNaturalPerson (6 tests) — includes type mismatch guards, null payload guards, display name re-derivation, and Apply verification
- Created `PartyAggregateLifecycleTests` with 12 tests covering DeactivateParty (5 tests), ReactivateParty (5 tests), and full deactivate-reactivate round-trip cycle
- All tests use Shouldly assertions exclusively, follow `{Method}_{Scenario}_{ExpectedResult}` naming, and have zero infrastructure dependencies (Tier 1 compliant)
- Used explicit typed Apply calls (switch statement) instead of dynamic dispatch per anti-pattern guidance
- All null command tests use explicit type cast per Story 1.4 learning
- No modifications to Contracts or Server projects — tests-only story
- Senior review hardening completed: 4 test methods now verify command-to-event payload correctness for CreatePerson, CreateOrganization, UpdatePersonDetails, and UpdateOrganizationDetails success scenarios
- Post-fix verification: `dotnet build Hexalith.Parties.slnx` passed and story-scope tests passed (66/66)

### Senior Developer Review (AI)

- Reviewer: Jérôme
- Date: 2026-03-04
- Outcome: Approved with fixes applied
- High findings fixed: 4
- Medium findings fixed: 1

#### Findings Addressed

- `Handle_CreatePersonParty_EmitsPartyCreatedAndDisplayNameDerived` now verifies `PartyCreated` payload fields against command data
- `Handle_CreateOrganizationParty_EmitsPartyCreatedAndDisplayNameDerived` now verifies `PartyCreated` organization payload fields
- `Handle_UpdatePersonDetails_ValidState_EmitsUpdatedAndDisplayNameDerived` now verifies `PersonDetailsUpdated` payload fields
- `Handle_UpdateOrganizationDetails_ValidState_EmitsUpdatedAndDisplayNameDerived` now verifies `OrganizationDetailsUpdated` payload fields
- Verification claims reconciled with evidence via fresh build and test execution (66/66)

### File List

- `src/Hexalith.Parties.Testing/PartyTestData.cs` — NEW
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCreateTests.cs` — NEW
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateUpdateTests.cs` — NEW
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateLifecycleTests.cs` — NEW
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateTests.cs` — DELETED
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — MODIFIED (status update)
- `_bmad-output/implementation-artifacts/1-5-party-aggregate-tier-1-unit-tests.md` — MODIFIED (task tracking)
