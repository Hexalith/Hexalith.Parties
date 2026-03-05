# Story 2.3: Contact Channel & Identifier Unit Tests

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->
<!-- Key Context: This story adds Tier 1 aggregate-level unit tests for Handle(AddIdentifier), Handle(RemoveIdentifier). Contact channel aggregate tests already exist in PartyAggregateContactChannelTests.cs (11 tests). The acceptance criteria also require extending PartyTestData with identifier builders and verifying aggregate-size behavior at 50 contact channels. -->

## Story

As a developer,
I want comprehensive Tier 1 unit tests for all contact channel and identifier aggregate operations,
So that domain logic correctness is verified for the full party enrichment model.

## Acceptance Criteria

1. **Given** the `Hexalith.Parties.Server.Tests` project, **When** all identifier tests are implemented, **Then** a `PartyAggregateIdentifierTests` test class exists covering: add (VAT, SIRET, NationalId), remove, duplicate rejection (NoOp), invalid ID rejection, idempotent add, null-state rejection.

2. **Given** the test naming convention `{Method}_{Scenario}_{ExpectedResult}`, **When** all tests are reviewed, **Then** every test follows this convention (e.g., `Handle_AddIdentifier_NullState_ReturnsPartyNotFound`).

3. **Given** the assertion library, **When** tests make assertions, **Then** Shouldly assertions are used exclusively (`.ShouldBeTrue()`, `.ShouldBeOfType<T>()`, `.ShouldBe()`, etc.).

4. **Given** `PartyTestData` in `src/Hexalith.Parties.Testing/PartyTestData.cs`, **When** identifier test data builders are needed, **Then** the following builders are added: `ValidAddVatIdentifier()`, `ValidAddSiretIdentifier()`, `ValidAddNationalIdIdentifier()`, `ValidRemoveIdentifier()`.

5. **Given** a party with 50 contact channels, **When** all contact channel operations (add, update, remove, preferred) are tested at this size, **Then** all operations work correctly at the aggregate size guideline.

6. **Given** all tests in the solution, **When** `dotnet test` is run, **Then** all tests pass (existing 86 + new tests), zero regressions.

7. **Given** all tests are Tier 1 compliant, **When** the test infrastructure is reviewed, **Then** zero infrastructure dependencies are present (no DAPR, no database, no HTTP — pure domain logic tests only).

## Tasks / Subtasks

- [ ] Task 1: Extend `PartyTestData` with identifier builders (AC: #4)
  - [ ] 1.1: Add `ValidAddVatIdentifier()` → `AddIdentifier` with `Type = IdentifierType.VAT`, `Value = "FR12345678901"`
  - [ ] 1.2: Add `ValidAddSiretIdentifier()` → `AddIdentifier` with `Type = IdentifierType.SIRET`, `Value = "12345678901234"`
  - [ ] 1.3: Add `ValidAddNationalIdIdentifier()` → `AddIdentifier` with `Type = IdentifierType.NationalId`, `Value = "850101123456789"`
  - [ ] 1.4: Add `ValidRemoveIdentifier()` → `RemoveIdentifier` with default `IdentifierId`
  - [ ] 1.5: Add `CreatePersonStateWithIdentifier()` → `PartyState` with one pre-added VAT identifier

- [ ] Task 2: Create `PartyAggregateIdentifierTests.cs` (AC: #1, #2, #3, #7)
  - [ ] 2.1: `Handle_AddIdentifier_NullState_ReturnsPartyNotFound` — null state → `PartyNotFound` rejection
  - [ ] 2.2: `Handle_AddIdentifier_ValidVat_EmitsIdentifierAdded` — happy path, verify event properties (IdentifierId, Type=VAT, Value)
  - [ ] 2.3: `Handle_AddIdentifier_ValidSiret_EmitsIdentifierAdded` — SIRET type variant
  - [ ] 2.4: `Handle_AddIdentifier_ValidNationalId_EmitsIdentifierAdded` — NationalId type variant
  - [ ] 2.5: `Handle_AddIdentifier_DuplicateId_ReturnsNoOp` — idempotent add (D10)
  - [ ] 2.6: `Handle_RemoveIdentifier_NullState_ReturnsPartyNotFound` — null state → `PartyNotFound` rejection
  - [ ] 2.7: `Handle_RemoveIdentifier_NotFound_ReturnsIdentifierNotFound` — non-existent ID → `IdentifierNotFound` rejection
  - [ ] 2.8: `Handle_RemoveIdentifier_Existing_EmitsIdentifierRemoved` — happy path, verify event properties (IdentifierId)

- [ ] Task 3: Add aggregate-size contact channel test (AC: #5)
  - [ ] 3.1: Add test to `PartyAggregateContactChannelTests.cs`: `Handle_AddContactChannel_With50ExistingChannels_StillSucceeds` — add 50 channels via state.Apply, then add 51st, verify success
  - [ ] 3.2: Add test: `Handle_UpdateContactChannel_With50ExistingChannels_StillSucceeds` — update one of 50 channels
  - [ ] 3.3: Add test: `Handle_RemoveContactChannel_With50ExistingChannels_StillSucceeds` — remove one of 50 channels

- [ ] Task 4: Build and regression verification (AC: #6)
  - [ ] 4.1: `dotnet build Hexalith.Parties.slnx` — zero errors, zero new warnings
  - [ ] 4.2: `dotnet test` — all tests pass (86 existing + ~11 new = ~97 total), zero regressions

## Dev Notes

### What This Story Does

This story adds **Tier 1 aggregate-level unit tests** for the identifier Handle methods implemented in Story 2.2 and aggregate-size verification tests for contact channels. All tests are pure domain logic — no infrastructure dependencies.

### Existing Test Coverage — What Already Exists (DO NOT Recreate)

**Contact channel aggregate tests ALREADY exist** — `PartyAggregateContactChannelTests.cs` (11 tests):
- `Handle_AddContactChannel_NullState_ReturnsPartyNotFound`
- `Handle_AddContactChannel_NonPreferred_EmitsSingleContactChannelAdded`
- `Handle_AddContactChannel_DuplicateId_ReturnsNoOp`
- `Handle_AddContactChannel_IsPreferred_EmitsAddedAndPreferredChanged`
- `Handle_UpdateContactChannel_NullState_ReturnsPartyNotFound`
- `Handle_UpdateContactChannel_NotFound_ReturnsContactChannelNotFound`
- `Handle_UpdateContactChannel_ValidUpdate_EmitsContactChannelUpdated`
- `Handle_UpdateContactChannel_AlreadyPreferredAndTypeChanges_EmitsPreferredChanged`
- `Handle_RemoveContactChannel_NullState_ReturnsPartyNotFound`
- `Handle_RemoveContactChannel_NotFound_ReturnsContactChannelNotFound`
- `Handle_RemoveContactChannel_Existing_EmitsRemoved`

**PartyState Apply tests ALREADY exist** in `PartyStateTests.cs` (21 tests):
- `Apply_IdentifierAdded_AddsToList` — state-level identifier add
- `Apply_IdentifierRemoved_RemovesById` — state-level identifier remove
- All contact channel Apply tests

**DO NOT recreate these.** This story only adds:
1. New `PartyAggregateIdentifierTests.cs` for Handle-level identifier tests
2. Extended `PartyTestData` builders
3. Aggregate-size tests for contact channels

### Handle Methods Under Test (Already Implemented in PartyAggregate.cs)

**`Handle(AddIdentifier command, PartyState? state)`** (lines 300-323):
```csharp
public static DomainResult Handle(AddIdentifier command, PartyState? state)
{
    ArgumentNullException.ThrowIfNull(command);
    if (state is null)
        return DomainResult.Rejection([new PartyNotFound()]);
    if (state.Identifiers.Any(i => i.Id == command.IdentifierId))
        return DomainResult.NoOp();
    IdentifierAdded added = new()
    {
        IdentifierId = command.IdentifierId,
        Type = command.Type,
        Value = command.Value,
    };
    return DomainResult.Success([added]);
}
```

**`Handle(RemoveIdentifier command, PartyState? state)`** (lines 325-341):
```csharp
public static DomainResult Handle(RemoveIdentifier command, PartyState? state)
{
    ArgumentNullException.ThrowIfNull(command);
    if (state is null)
        return DomainResult.Rejection([new PartyNotFound()]);
    if (!state.Identifiers.Any(i => i.Id == command.IdentifierId))
        return DomainResult.Rejection([new IdentifierNotFound { Message = $"Identifier '{command.IdentifierId}' not found." }]);
    return DomainResult.Success([new IdentifierRemoved { IdentifierId = command.IdentifierId }]);
}
```

### Test Pattern — Follow PartyAggregateContactChannelTests Exactly

All identifier tests MUST follow the exact same pattern as `PartyAggregateContactChannelTests.cs`:

```csharp
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Server.Aggregates;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public class PartyAggregateIdentifierTests
{
    [Fact]
    public void Handle_AddIdentifier_NullState_ReturnsPartyNotFound()
    {
        AddIdentifier command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            IdentifierId = "id-1",
            Type = IdentifierType.VAT,
            Value = "FR12345678901",
        };

        var result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyNotFound>();
    }
}
```

**Key patterns:**
- Use `PartyTestData.CreatePersonState()` for valid party state
- Use `state.Apply(new IdentifierAdded { ... })` to pre-populate identifiers in state
- Verify `result.IsSuccess`, `result.IsRejection`, or `result.IsNoOp`
- Verify `result.Events.Count` and event types
- Verify event property values on happy paths
- Use `PartyTestData.DefaultPartyId` for PartyId

### PartyTestData Builders to Add

Location: `src/Hexalith.Parties.Testing/PartyTestData.cs`

Add after `ValidReactivateParty()` method:

```csharp
public static AddIdentifier ValidAddVatIdentifier() => new()
{
    PartyId = DefaultPartyId,
    IdentifierId = "id-vat-1",
    Type = IdentifierType.VAT,
    Value = "FR12345678901",
};

public static AddIdentifier ValidAddSiretIdentifier() => new()
{
    PartyId = DefaultPartyId,
    IdentifierId = "id-siret-1",
    Type = IdentifierType.SIRET,
    Value = "12345678901234",
};

public static AddIdentifier ValidAddNationalIdIdentifier() => new()
{
    PartyId = DefaultPartyId,
    IdentifierId = "id-natid-1",
    Type = IdentifierType.NationalId,
    Value = "850101123456789",
};

public static RemoveIdentifier ValidRemoveIdentifier() => new()
{
    PartyId = DefaultPartyId,
    IdentifierId = "id-vat-1",
};

public static PartyState CreatePersonStateWithIdentifier()
{
    PartyState state = CreatePersonState();
    state.Apply(new IdentifierAdded
    {
        IdentifierId = "id-vat-1",
        Type = IdentifierType.VAT,
        Value = "FR12345678901",
    });
    return state;
}
```

### Aggregate-Size Contact Channel Tests

The epics require verifying operations at 50 contact channels. Add these tests to the EXISTING `PartyAggregateContactChannelTests.cs`:

```csharp
[Fact]
public void Handle_AddContactChannel_With50ExistingChannels_StillSucceeds()
{
    PartyState state = PartyTestData.CreatePersonState();
    for (int i = 0; i < 50; i++)
    {
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = $"ch-{i}",
            Type = ContactChannelType.Email,
            Value = $"user{i}@example.com",
        });
    }

    AddContactChannel command = new()
    {
        PartyId = PartyTestData.DefaultPartyId,
        ContactChannelId = "ch-51",
        Type = ContactChannelType.Phone,
        Value = "+33600000051",
        IsPreferred = false,
    };

    var result = PartyAggregate.Handle(command, state);
    result.IsSuccess.ShouldBeTrue();
}
```

### Project Structure Notes

```
src/Hexalith.Parties.Testing/
└── PartyTestData.cs                    ← MODIFY: add identifier builders

tests/Hexalith.Parties.Server.Tests/
└── Aggregates/
    ├── PartyAggregateContactChannelTests.cs  ← MODIFY: add 3 aggregate-size tests
    ├── PartyAggregateIdentifierTests.cs      ← NEW: 8 identifier Handle tests
    ├── PartyAggregateCreateTests.cs          (existing — do not touch)
    ├── PartyAggregateLifecycleTests.cs       (existing — do not touch)
    └── PartyAggregateUpdateTests.cs          (existing — do not touch)
```

### Architecture Compliance

- **Tier 1 tests only:** Zero infrastructure dependencies. No DAPR, no HTTP, no database. Pure `PartyAggregate.Handle()` calls.
- **Event Sourcing test pattern:** Test Handle methods produce correct events. Test state by pre-applying events via `state.Apply()`.
- **DomainResult types:** Verify `IsSuccess`, `IsRejection`, or `IsNoOp` — never check for exceptions in domain logic.
- **Idempotency (D10):** Duplicate add → `IsNoOp` (not rejection). This is critical.
- **DAPR actor model:** Handle methods are static pure functions — no actor infrastructure needed in tests.

### Code Style Requirements

- File-scoped namespaces (`namespace X;`)
- `public class` (test classes are NOT sealed — xUnit requires public non-sealed)
- Allman brace style, 4-space indentation
- CRLF line endings, UTF-8 encoding
- `[Fact]` attribute on all tests (no `[Theory]` unless needed for parameterization)
- One test class per file, file name = class name
- `TreatWarningsAsErrors = true` — zero warnings allowed

### Library & Framework Versions

| Package | Version |
|---|---|
| .NET SDK | 10.0.103 (net10.0) |
| xUnit | 2.9.3 |
| Shouldly | 4.3.0 |
| Hexalith.EventStore | project reference |

No new packages needed for this story.

### Anti-Patterns to Avoid

- **DO NOT** recreate existing contact channel aggregate tests — they already exist (11 tests in `PartyAggregateContactChannelTests.cs`)
- **DO NOT** recreate existing PartyState Apply tests — they already exist (21 tests in `PartyStateTests.cs`)
- **DO NOT** add infrastructure dependencies to tests (no DAPR, no HTTP clients)
- **DO NOT** use `Assert.` xUnit assertions — use Shouldly exclusively
- **DO NOT** make test classes `sealed` — xUnit requires non-sealed
- **DO NOT** add validators, controller tests, or integration tests — those are Story 2.4 and beyond
- **DO NOT** modify `PartyAggregate.cs` — Handle methods are already complete (Story 2.2)
- **DO NOT** modify `PartyState.cs` — Apply methods are already complete
- **DO NOT** use LINQ `Single`/`First` in tests on domain collections — use `Any`/`FindIndex` or index access after count check

### Previous Story Intelligence (Story 2.2)

- 86 tests pass (21 contracts + 56 server + 2 integration + 7 CommandApi), zero regressions
- `PartyAggregate.cs` has 11 Handle methods: `CreateParty`, `UpdatePersonDetails`, `UpdateOrganizationDetails`, `SetIsNaturalPerson`, `DeactivateParty`, `ReactivateParty`, `AddContactChannel`, `UpdateContactChannel`, `RemoveContactChannel`, `AddIdentifier`, `RemoveIdentifier`
- `PartyAggregateContactChannelTests.cs` follows exact test pattern needed for identifier tests
- `PartyTestData` has builders for lifecycle commands but NOT for identifiers — needs extension
- Build succeeds with `TreatWarningsAsErrors = true`
- ASPIRE004 build warning is pre-existing and unrelated — ignore

**Key learnings from Stories 2.1 and 2.2:**
- Review cycle 1 (Story 2.1) found missing null-state and happy-path tests — ensure comprehensive coverage from the start
- Idempotent add uses `DomainResult.NoOp()`, not rejection — test must verify `IsNoOp` (not `IsRejection`)
- File list accuracy was flagged in reviews — ensure File List matches actual git changes
- Pre-existing integration test failure (`CreateThenGetParty_ReturnsAcceptedThenOk_WithGdprHeaderAsync`) may appear in some configs — requires DAPR infrastructure, unrelated to this story

### Git Intelligence

**Recent commits:**
```
a15ef56 Merge pull request #9 — Story 2.1: Party Aggregate Contact Channel Management
598099e Implement Story 2.1: Party Aggregate Contact Channel Management
ac072e7 Merge pull request #8 — Story 1.7: AppHost Local Development & GDPR Warning
```

**Branch naming pattern:** `implement-story-2-3-contact-channel-and-identifier-unit-tests`
**Commit message pattern:** `Implement Story 2.3: Contact Channel & Identifier Unit Tests`

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2 — Story 2.3 acceptance criteria]
- [Source: _bmad-output/planning-artifacts/architecture.md — D10 idempotency, Tier 1 testing strategy, xUnit+Shouldly+NSubstitute]
- [Source: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateContactChannelTests.cs — Test pattern reference (11 tests)]
- [Source: src/Hexalith.Parties.Testing/PartyTestData.cs — Test data builders to extend]
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs — Handle(AddIdentifier) at line 300, Handle(RemoveIdentifier) at line 325]
- [Source: _bmad-output/implementation-artifacts/2-2-party-aggregate-identifier-management.md — Previous story patterns and learnings]
- [Source: _bmad-output/implementation-artifacts/2-1-party-aggregate-contact-channel-management.md — Contact channel test patterns]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
