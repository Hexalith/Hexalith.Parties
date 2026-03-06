# Story 4.3: Composite Command Unit Tests

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a comprehensive test matrix for both composite aggregate handlers,
So that the most complex domain logic is thoroughly verified before any consumer uses it.

## Acceptance Criteria

1. **Given** the Hexalith.Parties.Server.Tests project, **When** the composite command test matrix (D19) is implemented, **Then** the following test categories exist for `CreatePartyComposite`: happy path person with channels+identifiers, happy path organization with channels+identifiers, party only (no channels/identifiers), idempotent create (party already exists), duplicate channel IDs in payload (skipped), missing party type (full rejection), payload size limit exceeded (rejection), maximum channels (50) in single create, duplicate identifier IDs (skipped).

2. **And** the following test categories exist for `UpdatePartyComposite`: update person details only, update organization details only, add channels only, update channels only, remove channels only, add identifiers only, remove identifiers only, mixed (details + add + update + remove channels + add + remove identifiers), absent details (no change), duplicate add (skipped), invalid update channel ID (rejection), invalid remove channel ID (rejection), conflicting operations same ID in add+remove (rejection), no-op all lists empty (no events), non-existent party (rejection), payload size limit exceeded (rejection), within-list dedup for UpdateContactChannels, update+remove conflict (rejection), blank IDs in all operation lists (rejection), person details on organization party (PartyTypeMismatch), organization details on person party (PartyTypeMismatch), preferred channel logic during update.

3. **And** tests verify `CompositeCommandResult` structure: `Applied` collection contains correctly applied sub-operations, `Skipped` collection contains idempotently skipped sub-operations, `Rejected` collection contains specific error details.

4. **And** all tests follow naming convention: `Handle_{Command}_{Scenario}_{ExpectedResult}`

5. **And** Shouldly assertions are used.

6. **And** `PartyTestData` is extended with composite builder methods.

7. **And** all tests are Tier 1 compliant (pure domain logic, no infrastructure).

8. **And** estimated 38-42 test cases covering the full combinatorial matrix.

9. **And** all existing 170 tests continue to pass with zero regressions.

## Tasks / Subtasks

- [ ] Task 1: Extend `PartyTestData` with composite test builder methods (AC: #6)
  - [ ] 1.1: Add `CreatePersonStateWithChannelsAndIdentifiers()` -- person state with 2 email channels + 1 VAT identifier (base state for UpdatePartyComposite tests)
  - [ ] 1.2: Add `CreateOrganizationStateWithChannelsAndIdentifiers()` -- organization state with channels + identifiers
  - [ ] 1.3: Add `ValidCreateOrganizationComposite()` -- organization composite command builder
  - [ ] 1.4: Add `ValidUpdatePersonComposite()` -- base update composite command builder for person party
  - [ ] 1.5: Add `ValidUpdateOrganizationComposite()` -- base update composite command builder for organization party

- [ ] Task 2: Complete missing `CreatePartyComposite` test cases (AC: #1, #3, #4, #5)
  - [ ] 2.1: `Handle_CreatePartyComposite_OrganizationWithChannelsAndIdentifiers_EmitsExpectedEvents` -- AC#2 from epics
  - [ ] 2.2: `Handle_CreatePartyComposite_PartyOnlyNoChannelsNoIdentifiers_EmitsOnlyCreateAndDisplayName` -- AC#6 from epics
  - [ ] 2.3: `Handle_CreatePartyComposite_PartyAlreadyExists_ReturnsNoOp` -- AC#7 idempotency
  - [ ] 2.4: `Handle_CreatePartyComposite_MissingPartyType_ReturnsRejection` -- AC#4 from epics
  - [ ] 2.5: `Handle_CreatePartyComposite_DuplicateIdentifierIds_SkipsDuplicate`
  - [ ] 2.6: `Handle_CreatePartyComposite_PreferredChannel_EmitsPreferredContactChannelChanged`
  - [ ] 2.7: `Handle_CreatePartyComposite_MaxChannels50InSingleCreate_AllApplied` -- epics AC: 50 channels in one create, verify all applied

- [ ] Task 3: Implement `UpdatePartyComposite` test cases -- basic operations (AC: #2, #3)
  - [ ] 3.1: `Handle_UpdatePartyComposite_PersonDetailsOnly_EmitsPersonDetailsUpdatedAndDisplayName`
  - [ ] 3.2: `Handle_UpdatePartyComposite_OrganizationDetailsOnly_EmitsOrganizationDetailsUpdatedAndDisplayName`
  - [ ] 3.3: `Handle_UpdatePartyComposite_AddChannelsOnly_EmitsContactChannelAddedEvents`
  - [ ] 3.4: `Handle_UpdatePartyComposite_UpdateChannelsOnly_EmitsContactChannelUpdatedEvents`
  - [ ] 3.5: `Handle_UpdatePartyComposite_RemoveChannelsOnly_EmitsContactChannelRemovedEvents`
  - [ ] 3.6: `Handle_UpdatePartyComposite_AddIdentifiersOnly_EmitsIdentifierAddedEvents`
  - [ ] 3.7: `Handle_UpdatePartyComposite_RemoveIdentifiersOnly_EmitsIdentifierRemovedEvents`
  - [ ] 3.8: `Handle_UpdatePartyComposite_MixedOperations_EmitsAllExpectedEvents`

- [ ] Task 4: Implement `UpdatePartyComposite` test cases -- edge cases and rejections (AC: #2, #3)
  - [ ] 4.1: `Handle_UpdatePartyComposite_AbsentDetails_NoDetailEventsEmitted`
  - [ ] 4.2a: `Handle_UpdatePartyComposite_AddChannelExistsInState_SkippedAsDuplicate` -- channel ID already in party state (state-level duplicate)
  - [ ] 4.2b: `Handle_UpdatePartyComposite_AddChannelDuplicateInPayload_SkippedAsDuplicate` -- same channel ID twice in AddContactChannels list (payload-level duplicate)
  - [ ] 4.3: `Handle_UpdatePartyComposite_DuplicateAddIdentifier_SkipsDuplicate`
  - [ ] 4.4: `Handle_UpdatePartyComposite_InvalidUpdateChannelId_ReturnsRejection`
  - [ ] 4.5: `Handle_UpdatePartyComposite_InvalidRemoveChannelId_ReturnsRejection`
  - [ ] 4.6: `Handle_UpdatePartyComposite_InvalidRemoveIdentifierId_ReturnsRejection`
  - [ ] 4.7: `Handle_UpdatePartyComposite_ConflictingChannelAddAndRemove_ReturnsRejection`
  - [ ] 4.8: `Handle_UpdatePartyComposite_ConflictingIdentifierAddAndRemove_ReturnsRejection`
  - [ ] 4.9: `Handle_UpdatePartyComposite_ConflictingChannelUpdateAndRemove_ReturnsRejection`
  - [ ] 4.10: `Handle_UpdatePartyComposite_NoOp_AllListsEmpty_ReturnsNoOpResult`
  - [ ] 4.11: `Handle_UpdatePartyComposite_NonExistentParty_ReturnsPartyNotFound`
  - [ ] 4.12: `Handle_UpdatePartyComposite_PayloadExceedsMax_ReturnsRejection`
  - [ ] 4.13: `Handle_UpdatePartyComposite_PersonDetailsOnOrganization_ReturnsTypeMismatch`
  - [ ] 4.14: `Handle_UpdatePartyComposite_OrganizationDetailsOnPerson_ReturnsTypeMismatch`
  - [ ] 4.15: `Handle_UpdatePartyComposite_BlankAddChannelId_ReturnsRejection`
  - [ ] 4.16: `Handle_UpdatePartyComposite_BlankUpdateChannelId_ReturnsRejection`
  - [ ] 4.17: `Handle_UpdatePartyComposite_BlankRemoveChannelId_ReturnsRejection`
  - [ ] 4.18: `Handle_UpdatePartyComposite_BlankAddIdentifierId_ReturnsRejection`
  - [ ] 4.19: `Handle_UpdatePartyComposite_BlankRemoveIdentifierId_ReturnsRejection`
  - [ ] 4.20: `Handle_UpdatePartyComposite_PayloadExactlyAtLimit_Succeeds` -- boundary test: subOps == MaxSubOperations should succeed (not reject)
  - [ ] 4.21: `Handle_UpdatePartyComposite_MaxSubOperationsResetFromZero_UsesDefault` -- safety guard: set MaxSubOperations=0, verify it resets to DefaultMaxSubOperations (100) and processes normally
  - [ ] 4.22: `Handle_UpdatePartyComposite_BothPersonAndOrgDetails_RejectsTypeMismatch` -- person party with both PersonDetails AND OrganizationDetails non-null: handler checks PersonDetails first (passes for person party), then checks OrganizationDetails (fails -- `state.Type != PartyType.Organization`), rejects with `PartyTypeMismatch { Message = "Cannot update organization details on a Person party." }`. Verify: zero events emitted, Applied empty, single rejection event is `PartyTypeMismatch`

- [ ] Task 5: Implement `UpdatePartyComposite` test cases -- dedup and preferred channel (AC: #2, #3)
  - [ ] 5.1: `Handle_UpdatePartyComposite_WithinListDedupUpdateChannels_SkipsDuplicate`
  - [ ] 5.2: `Handle_UpdatePartyComposite_WithinListDedupRemoveChannels_EmitsSingleRemove` -- Note: dedup uses the `removeChannelIds` HashSet built during Phase 1 validation (step 11), not a fresh HashSet in Phase 2. Verify only ONE `ContactChannelRemoved` event is emitted when `["ch-email-1", "ch-email-1"]` is passed. If a future refactor creates a separate Phase 2 HashSet, dedup would still work but validation-existence checking could diverge
  - [ ] 5.3: `Handle_UpdatePartyComposite_WithinListDedupRemoveIdentifiers_EmitsSingleRemove` -- Same pattern as 5.2: dedup uses `removeIdentifierIds` HashSet from Phase 1 validation (step 12)
  - [ ] 5.4: `Handle_UpdatePartyComposite_UpdateChannelToPreferred_EmitsPreferredChanged`
  - [ ] 5.5: `Handle_UpdatePartyComposite_UpdateChannelAlreadyPreferred_NoPreferredChangedEvent`
  - [ ] 5.6: `Handle_UpdatePartyComposite_AddChannelPreferred_EmitsPreferredChanged`
  - [ ] 5.7: `Handle_UpdatePartyComposite_UpdateChannelTypeChangeToPreferred_EmitsPreferredChanged` -- channel currently preferred Email, update changes Type to Phone with IsPreferred=true: `targetType != existingChannel.Type` triggers PreferredContactChannelChanged
  - [ ] 5.8: **[HIGHEST VALUE TEST]** `Handle_UpdatePartyComposite_UpdateChannelNullableFieldsPreserved_EventHasNullValues` -- UpdateContactChannel with Type=null, Value=null, IsPreferred=null: verify ContactChannelUpdated event preserves all null values (no substitution from existing channel). If the dev agent incorrectly substitutes existing values for nulls, it silently corrupts the event stream -- this test is the canary in the coal mine

- [ ] Task 6: Build and regression verification (AC: #9)
  - [ ] 6.1: `dotnet build Hexalith.Parties.slnx` -- zero errors, zero warnings
  - [ ] 6.2: `dotnet test Hexalith.Parties.slnx` -- all existing 170 tests + new ~35 tests pass, zero regressions

## Dev Notes

### What This Story Does

Add comprehensive unit tests for both `Handle(CreatePartyComposite)` and `Handle(UpdatePartyComposite)` methods in `PartyAggregate`. This completes the D19 test matrix requirement. Extend `PartyTestData` with composite builder methods. No production code changes -- tests only.

### What Already Exists (DO NOT CREATE)

**Existing test file:**
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs` -- currently has 6 tests for CreatePartyComposite:
  - `Handle_CreatePartyComposite_PersonWithTwoChannelsAndOneIdentifier_EmitsExpectedEvents`
  - `Handle_CreatePartyComposite_DuplicateContactChannelIds_SkipsDuplicate`
  - `Handle_CreatePartyComposite_DerivedDisplayNameApplied_DoesNotContainPiiName`
  - `Handle_CreatePartyComposite_InvalidContactChannelId_ReturnsRejectionWithoutSuccessEvents`
  - `Handle_CreatePartyComposite_InvalidIdentifierId_ReturnsRejectionWithoutSuccessEvents`
  - `Handle_CreatePartyComposite_PayloadExceedsConfiguredMax_ReturnsRejection`
  - Private helper: `BuildValidPersonComposite()` -- builds CreatePartyComposite with person details, 2 email channels, 1 VAT identifier

**Existing test data helper:**
- `src/Hexalith.Parties.Testing/PartyTestData.cs` -- has `DefaultPartyId`, `ValidPersonDetails()`, `ValidOrganizationDetails()`, `CreatePersonState()`, `CreateOrganizationState()`, `CreatePersonStateWithIdentifier()`, `ValidAddVatIdentifier()`, `ValidAddSiretIdentifier()`, etc.

**Production code under test (DO NOT MODIFY):**
- `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` -- `Handle(CreatePartyComposite)` (lines 18-188) and `Handle(UpdatePartyComposite)` (lines 190-563)

**Command types (already exist):**
- `CreatePartyComposite` -- `PartyId`, `Type`, `PersonDetails?`, `OrganizationDetails?`, `ContactChannels` (IReadOnlyList\<AddContactChannel\>), `Identifiers` (IReadOnlyList\<AddIdentifier\>)
- `UpdatePartyComposite` -- `PartyId`, `PersonDetails?`, `OrganizationDetails?`, `AddContactChannels` (IReadOnlyList\<AddContactChannel\>), `UpdateContactChannels` (IReadOnlyList\<UpdateContactChannel\>), `RemoveContactChannelIds` (IReadOnlyList\<string\>), `AddIdentifiers` (IReadOnlyList\<AddIdentifier\>), `RemoveIdentifierIds` (IReadOnlyList\<string\>)
- `UpdateContactChannel` -- `PartyId` (required), `ContactChannelId` (required), `Type?` (ContactChannelType?), `Value?` (string?), `IsPreferred?` (bool?) -- null means "no change"
- `AddContactChannel` -- `PartyId`, `ContactChannelId`, `Type` (ContactChannelType), `Value` (string), `IsPreferred` (bool)
- `AddIdentifier` -- `PartyId`, `IdentifierId`, `Type` (IdentifierType), `Value` (string)

**Result types:**
- `CompositeCommandResult` -- extends `DomainResult` with `Applied`, `Skipped`, `Rejected` (all IReadOnlyList\<string\>). Properties: `IsSuccess`, `IsRejection`, `IsNoOp`, `Events`.

**Event types (for assertions):**
- Success: `PartyCreated`, `PartyDisplayNameDerived`, `ContactChannelAdded`, `ContactChannelUpdated`, `ContactChannelRemoved`, `IdentifierAdded`, `IdentifierRemoved`, `PreferredContactChannelChanged`, `PersonDetailsUpdated`, `OrganizationDetailsUpdated`
- Rejection: `PartyNotFound`, `CompositeOperationConflict`, `ContactChannelNotFound`, `IdentifierNotFound`, `PartyTypeMismatch`, `PartyCannotBeCreatedWithoutType`

**State types:**
- `PartyState` -- `Type` (PartyType), `Person` (PersonDetails?), `Organization` (OrganizationDetails?), `ContactChannels` (IReadOnlyList\<ContactChannel\>), `Identifiers` (IReadOnlyList\<PartyIdentifier\>), `IsActive` (bool), `DisplayName` (string), `SortName` (string)
- `ContactChannel` -- `Id` (string), `Type` (ContactChannelType), `Value` (string), `IsPreferred` (bool)
- `PartyIdentifier` -- `Id` (string), `Type` (IdentifierType), `Value` (string), `Jurisdiction` (string?)
- `PartyState.Apply()` -- mutates state from event payloads (used to build test state)

**Enums:**
- `PartyType { Person, Organization }`
- `ContactChannelType { Email, Phone, PostalAddress, SocialMedia }`
- `IdentifierType { VAT, SIRET, NationalId, CompanyRegistration, TaxId, Other }`

### Implementation Guide

**Files to modify:**

1. **`src/Hexalith.Parties.Testing/PartyTestData.cs`** -- Add composite test builder methods:

```csharp
// Add these methods to the existing PartyTestData class:

public static PartyState CreatePersonStateWithChannelsAndIdentifiers()
{
    PartyState state = CreatePersonState();
    state.Apply(new ContactChannelAdded
    {
        ContactChannelId = "ch-email-1",
        Type = ContactChannelType.Email,
        Value = "john@example.com",
        IsPreferred = true,
    });
    state.Apply(new PreferredContactChannelChanged { ContactChannelId = "ch-email-1" });
    state.Apply(new ContactChannelAdded
    {
        ContactChannelId = "ch-email-2",
        Type = ContactChannelType.Email,
        Value = "john.alt@example.com",
        IsPreferred = false,
    });
    state.Apply(new IdentifierAdded
    {
        IdentifierId = "id-vat-1",
        Type = IdentifierType.VAT,
        Value = "FR12345678901",
    });
    return state;
}

public static PartyState CreateOrganizationStateWithChannelsAndIdentifiers()
{
    PartyState state = CreateOrganizationState();
    state.Apply(new ContactChannelAdded
    {
        ContactChannelId = "ch-email-1",
        Type = ContactChannelType.Email,
        Value = "info@acme.com",
        IsPreferred = true,
    });
    state.Apply(new PreferredContactChannelChanged { ContactChannelId = "ch-email-1" });
    state.Apply(new IdentifierAdded
    {
        IdentifierId = "id-vat-1",
        Type = IdentifierType.VAT,
        Value = "FR98765432100",
    });
    return state;
}
```

2. **`tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs`** -- Extend with all new tests. Keep existing 6 tests. Add missing CreatePartyComposite tests + all UpdatePartyComposite tests.

**Test state construction pattern for UpdatePartyComposite:**
Build base state using `PartyTestData.CreatePersonStateWithChannelsAndIdentifiers()` which gives you a person party with:
- 2 contact channels: `"ch-email-1"` (preferred, Email) and `"ch-email-2"` (not preferred, Email)
- 1 identifier: `"id-vat-1"` (VAT)

Then construct `UpdatePartyComposite` commands targeting specific operations:

```csharp
// Example: Update person details only
UpdatePartyComposite command = new()
{
    PartyId = PartyTestData.DefaultPartyId,
    PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Smith" },
};

// Example: Mixed operations
UpdatePartyComposite command = new()
{
    PartyId = PartyTestData.DefaultPartyId,
    PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Smith" },
    AddContactChannels = [new AddContactChannel { PartyId = PartyTestData.DefaultPartyId, ContactChannelId = "ch-phone-1", Type = ContactChannelType.Phone, Value = "+33111111111" }],
    UpdateContactChannels = [new UpdateContactChannel { PartyId = PartyTestData.DefaultPartyId, ContactChannelId = "ch-email-1", Value = "john.updated@example.com" }],
    RemoveContactChannelIds = ["ch-email-2"],
    AddIdentifiers = [new AddIdentifier { PartyId = PartyTestData.DefaultPartyId, IdentifierId = "id-siret-1", Type = IdentifierType.SIRET, Value = "12345678901234" }],
    RemoveIdentifierIds = [],
};
```

**Critical assertion patterns:**

For success tests, verify:
- `result.IsSuccess.ShouldBeTrue()`
- `result.Events.Count.ShouldBe(expectedCount)`
- Event type assertions: `result.Events[i].ShouldBeOfType<EventType>()`
- `result.Applied` strings follow format: `"Verb noun: {id} ({type})"` or `"Verb noun"`
- `result.Skipped` strings follow format: `"Duplicate noun: {id}"`
- `result.Rejected.ShouldBeEmpty()` on success path

For rejection tests, verify:
- `result.IsRejection.ShouldBeTrue()`
- `result.Events.Count.ShouldBe(1)` -- single rejection event
- `result.Events[0].ShouldBeOfType<RejectionEventType>()`
- `result.Applied.ShouldBeEmpty()` -- no events on rejection
- `result.Skipped.ShouldBeEmpty()` -- no skips on rejection
- `result.Rejected.ShouldContain(expectedMessage)` or `result.Rejected.ShouldContain(x => x.Contains(substring))`

For no-op tests, verify:
- `result.IsNoOp.ShouldBeTrue()`
- `result.Events.ShouldBeEmpty()`
- `result.Applied.ShouldBeEmpty()`

**Specific assertion values from Handle implementations:**

Create handler applied strings:
- `"Created person party"`, `"Created organization party"`, `"Derived display name"`, `"Added contact channel: {id} ({type})"`, `"Added identifier: {id} ({type})"`, `"Set preferred contact channel: {id}"`
- Skipped: `"Duplicate contact channel: {id}"`, `"Duplicate identifier: {id}"`

Update handler applied strings:
- `"Updated person details"`, `"Updated organization details"`, `"Derived display name"`, `"Added contact channel: {id} ({type})"`, `"Updated contact channel: {id}"`, `"Removed contact channel: {id}"`, `"Added identifier: {id} ({type})"`, `"Removed identifier: {id}"`, `"Set preferred contact channel: {id}"`
- Skipped: `"Duplicate contact channel: {id}"`, `"Duplicate identifier: {id}"`

**Rejection event + message pairs:**
- `PartyNotFound` + `"Party does not exist."`
- `CompositeOperationConflict` + `"Payload size exceeded: X sub-operations (maximum Y)."`
- `CompositeOperationConflict` + `"Conflicting operations on same channel ID: {id}."`
- `CompositeOperationConflict` + `"Conflicting operations on same identifier ID: {id}."`
- `CompositeOperationConflict` + `"Contact channel ID is required."`
- `CompositeOperationConflict` + `"Identifier ID is required."`
- `ContactChannelNotFound` + `"Contact channel '{id}' not found."`
- `IdentifierNotFound` + `"Identifier '{id}' not found."`
- `PartyTypeMismatch` + `"Cannot update person details on a Organization party."`
- `PartyTypeMismatch` + `"Cannot update organization details on a Person party."`
- `PartyCannotBeCreatedWithoutType` + `"Party type is required."`

**MaxSubOperations test pattern:**
Same as existing test `Handle_CreatePartyComposite_PayloadExceedsConfiguredMax_ReturnsRejection`:
```csharp
int previous = PartyAggregate.MaxSubOperations;
PartyAggregate.MaxSubOperations = 3; // set low for test
try
{
    // ... test code ...
}
finally
{
    PartyAggregate.MaxSubOperations = previous; // restore
}
```

**Preferred channel logic for UpdateContactChannel tests:**
- If `channel.IsPreferred == true` AND (`!existingChannel.IsPreferred` OR `targetType != existingChannel.Type`), emit `PreferredContactChannelChanged`
- `targetType = channel.Type ?? existingChannel.Type`
- Test with: channel currently not preferred, set IsPreferred=true -> should emit PreferredContactChannelChanged
- Test with: channel currently preferred with same type, set IsPreferred=true -> should NOT emit (already preferred with same type)
- Test with: channel type changes (e.g., Email->Phone) with IsPreferred=true -> should emit (targetType != existingChannel.Type)

**Display name derivation verification (for Tasks 3.1 and 3.2):**
- Person: `DeriveDisplayName(Person, personDetails, null)` -> DisplayName=`"Jane Smith"`, SortName=`"Smith, Jane"` (format: `"{First} {Last}"`, `"{Last}, {First}"`)
- Organization: `DeriveDisplayName(Organization, null, orgDetails)` -> DisplayName=`LegalName`, SortName=`LegalName` (both use `LegalName`)
- In Task 3.1, assert: `PartyDisplayNameDerived` event has `DisplayName = "Jane Smith"` and `SortName = "Smith, Jane"`
- In Task 3.2, assert: `PartyDisplayNameDerived` event has `DisplayName = orgDetails.LegalName` and `SortName = orgDetails.LegalName`

### Test Risk Categorization (Priority Guide)

**High-risk tests (most likely to catch real bugs -- implement with extra care):**
- Task 4.20: Boundary at exactly MaxSubOperations (off-by-one errors)
- Task 4.22: Both PersonDetails AND OrganizationDetails (sequential validation interaction)
- Task 5.7: Type change + preferred channel (multi-condition branch)
- Task 5.8: Nullable fields preserved (silent event stream corruption risk)

**Medium-risk tests (regression safety net -- essential but straightforward):**
- Tasks 3.1-3.8: Basic operations (each operation type independently)
- Tasks 4.2a/4.2b: State-dup vs payload-dup (two distinct code paths)
- Tasks 4.4-4.6: Existence validation (D12 all-or-nothing)
- Tasks 5.4-5.6: Preferred channel logic

**Low-risk tests (defense in depth -- completeness):**
- Tasks 4.15-4.19: Blank ID validation (simple guard clauses)
- Tasks 4.7-4.9: Conflict detection (straightforward HashSet intersection)
- Task 4.21: MaxSubOperations reset from zero (safety guard)

### Architecture Compliance

| Decision | Requirement | Verified By |
|----------|------------|-------------|
| D8 | Single actor turn, atomic multi-event emission | CreatePartyComposite happy path tests verify all events emitted atomically in CompositeCommandResult |
| D9 | Explicit add/update/remove lists, absent=no-change | UpdatePartyComposite tests verify each list independently + absent details produce no events |
| D10 | Skip duplicate adds, reject invalid IDs, reject conflicts | Duplicate tests verify Skipped collection; invalid ID tests verify rejection; conflict tests verify rejection |
| D12 | All-or-nothing -- no partial failure | All rejection tests verify Applied=empty, Events=single rejection event |
| D17 | Max sub-operation count | Payload size tests verify configurable rejection threshold |
| D19 | Test matrix designed upfront | This story IS the D19 requirement -- comprehensive test catalog covering all combinatorial scenarios |

### Code Style Requirements

- File-scoped namespaces (`namespace X;`)
- No `sealed` needed on test classes (xUnit convention)
- Allman brace style, 4-space indentation
- CRLF line endings, UTF-8 encoding
- `TreatWarningsAsErrors = true` -- zero warnings allowed
- Use Shouldly assertions exclusively (`ShouldBe`, `ShouldBeTrue`, `ShouldBeOfType`, `ShouldBeEmpty`, `ShouldContain`)
- Test naming: `Handle_{Command}_{Scenario}_{ExpectedResult}`
- No LINQ `First()`, `Single()` -- use index access `result.Events[0]` or `ShouldContain`
- Every `[Fact]` test is independent -- no shared mutable state between tests (except MaxSubOperations which is restored in finally blocks)

### Anti-Patterns to Avoid

- **DO NOT** modify production code (PartyAggregate.cs, any Contracts files) -- this story is tests only
- **DO NOT** create new test project -- tests go in existing `Hexalith.Parties.Server.Tests`
- **DO NOT** use Moq, NSubstitute, or any mocking framework -- tests call static `PartyAggregate.Handle()` directly (pure domain logic, no dependencies)
- **DO NOT** use `[Theory]` with inline data for fundamentally different scenarios -- each scenario gets its own `[Fact]` for clarity. `[Theory]` is acceptable ONLY for parameterizing the same logical scenario with identical structure but different inputs. **Recommended `[Theory]` candidate:** Tasks 4.15-4.19 (blank IDs across 5 list types) are structurally identical -- same assertion pattern, same rejection event (`CompositeOperationConflict`), different list being tested. Collapsing to 1 `[Theory]` with 5 `[InlineData]` cases saves ~80 lines without losing clarity
- **DO NOT** share mutable state between tests -- each test constructs its own command and state
- **DO NOT** assert on Applied/Skipped string content with exact full-list matching -- assert on count and use `ShouldContain` for specific strings. The order of applied operations is deterministic but future-proofing against minor string changes
- **DO NOT** create abstract base classes or complex test hierarchies -- keep tests flat and simple
- **DO NOT** add `using` for types not already referenced in existing test files -- all needed types are in `Hexalith.Parties.Contracts.Commands`, `Hexalith.Parties.Contracts.Events`, `Hexalith.Parties.Contracts.Results`, `Hexalith.Parties.Contracts.State`, `Hexalith.Parties.Contracts.ValueObjects`, `Hexalith.Parties.Testing`, `Hexalith.Parties.Server.Aggregates`
- **DO NOT** delete existing 6 tests in `PartyAggregateCompositeTests` -- extend the file, do not replace
- **DO NOT** move the existing `BuildValidPersonComposite()` private method -- keep it in place. Add new builder methods to `PartyTestData` instead
- **DO NOT** use `Assert.Throws<>` -- use Shouldly assertions

### Previous Story Intelligence (Story 4.2)

- **170 tests pass** after Story 4.2 completion (164 from Epic 1-3 + 6 from Story 4.1)
- Story 4.2 implemented `Handle(UpdatePartyComposite)` method (~370 lines) with:
  - Phase 1 validation: null check, payload size, state-null, no-op, lookup structures, conflict detection, blank ID validation, existence validation, type-check validation
  - Phase 2 event emission: person/org details with display name re-derivation, add channels with dedup, update channels with dedup + preferred logic, remove channels, add identifiers with dedup, remove identifiers
- Story 4.2 noted specific "Beyond-AC hardening" items that MUST be tested:
  - Within-list dedup for `UpdateContactChannels` (skip duplicate channel IDs within same list)
  - Within-list dedup for `RemoveContactChannelIds` and `RemoveIdentifierIds` (prevent double events)
  - `UpdateContactChannels` IDs intersect `RemoveContactChannelIds` conflict detection
- Story 4.1 established:
  - `MaxSubOperations` as `public static int` with default 100 -- test pattern: save, set low, restore in finally
  - Applied/Skipped strings use format: `"Verb noun: {id} ({type})"` or `"Verb noun"`
  - PII-safe applied messages (no display name values, no email values, only IDs and types)
  - `HashSet<string>(StringComparer.Ordinal)` for duplicate detection pattern

### Git Intelligence

Recent commits:
```
39e713f Merge pull request #17 -- Story 4.1: CreatePartyComposite Aggregate Handler
85b67cf Implement Story 4.1: Create Party Composite Aggregate Handler
579aa3d Merge pull request #16 -- Story 3.4: Projection Unit & Integration Tests
```

Files from Story 4.1 test work:
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs` (217 lines -- 6 tests)
- Pattern: all tests in single class, private helper methods for command construction

Branch naming pattern: `implement-story-X-Y-description`
Commit message pattern: `Implement Story X.Y: Description`

### Project Structure Notes

```
src/Hexalith.Parties.Testing/
  PartyTestData.cs                         <- MODIFY: Add composite builder methods

tests/Hexalith.Parties.Server.Tests/
  Aggregates/
    PartyAggregateCompositeTests.cs        <- MODIFY: Add ~35 new test methods
    PartyAggregateCreateTests.cs           <- DO NOT TOUCH (reference for test patterns)
    PartyAggregateContactChannelTests.cs   <- DO NOT TOUCH (reference for state construction)
    PartyAggregateIdentifierTests.cs       <- DO NOT TOUCH (reference for state construction)
    PartyAggregateLifecycleTests.cs        <- DO NOT TOUCH
    PartyAggregateUpdateTests.cs           <- DO NOT TOUCH
```

No production code files should be modified. No new files should be created.

### Library & Framework Versions

| Package | Version |
|---|---|
| .NET SDK | 10.0.103 (net10.0) |
| xUnit | 2.9.3 |
| Shouldly | 4.3.0 |
| Hexalith.EventStore.Contracts | (submodule, DomainResult/CompositeCommandResult) |

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-4.3 -- Test matrix acceptance criteria]
- [Source: _bmad-output/planning-artifacts/architecture.md#D8 -- Composite aggregate command strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md#D9 -- MCP update strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md#D10 -- Sub-operation idempotency and conflict detection]
- [Source: _bmad-output/planning-artifacts/architecture.md#D12 -- Partial failure eliminated by design]
- [Source: _bmad-output/planning-artifacts/architecture.md#D17 -- Maximum composite payload size]
- [Source: _bmad-output/planning-artifacts/architecture.md#D19 -- Test matrix designed upfront]
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs -- Handle(CreatePartyComposite) lines 18-188, Handle(UpdatePartyComposite) lines 190-563]
- [Source: tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs -- Existing 6 tests and BuildValidPersonComposite() helper]
- [Source: src/Hexalith.Parties.Testing/PartyTestData.cs -- Existing test data builders]
- [Source: _bmad-output/implementation-artifacts/4-1-create-party-composite-aggregate-handler.md -- Story 4.1 patterns and learnings]
- [Source: _bmad-output/implementation-artifacts/4-2-update-party-composite-aggregate-handler.md -- Story 4.2 implementation details and beyond-AC hardening items]
- [Source: src/Hexalith.Parties.Contracts/Commands/UpdatePartyComposite.cs -- Command record definition]
- [Source: src/Hexalith.Parties.Contracts/Commands/UpdateContactChannel.cs -- Nullable fields: Type?, Value?, IsPreferred?]
- [Source: src/Hexalith.Parties.Contracts/Results/CompositeCommandResult.cs -- Result type with Applied, Skipped, Rejected]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
