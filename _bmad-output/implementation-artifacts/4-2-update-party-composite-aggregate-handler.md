# Story 4.2: UpdatePartyComposite Aggregate Handler

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want to update a party's details, channels, and identifiers in a single atomic command with explicit add/update/remove lists,
So that AI agents can make complex party modifications without multiple sequential commands.

## Acceptance Criteria

1. **Given** an existing person party with 2 email channels and 1 VAT identifier, **When** an `UpdatePartyComposite` command is handled with: `PersonDetails` present (replace person details), `AddContactChannels` with 1 new phone channel, `UpdateContactChannels` with 1 existing email channel updated, `RemoveContactChannelIds` with 1 existing email channel removed, `AddIdentifiers` with 1 SIRET identifier, **Then** the following events are emitted atomically: `PersonDetailsUpdated`, `PartyDisplayNameDerived` (re-derived), `ContactChannelAdded` x 1, `ContactChannelUpdated` x 1, `ContactChannelRemoved` x 1, `IdentifierAdded` x 1, **And** a `CompositeCommandResult` is returned with all in `Applied` (FR22).

2. **Given** an `UpdatePartyComposite` command with `PersonDetails` absent (null), **When** the command is handled, **Then** person details remain unchanged -- absent means "no change" (D9).

3. **Given** an `UpdatePartyComposite` command with `AddContactChannels` containing a channel ID that already exists in state, **When** the command is handled, **Then** the duplicate addition is skipped (D10 -- idempotency), **And** it appears in `CompositeCommandResult.Skipped`.

4. **Given** an `UpdatePartyComposite` command with `UpdateContactChannels` referencing a non-existent channel ID, **When** the command is handled, **Then** the entire composite is rejected with error: "channel ID not found" (D10).

5. **Given** an `UpdatePartyComposite` command with `RemoveContactChannelIds` referencing a non-existent channel ID, **When** the command is handled, **Then** the entire composite is rejected with error: "channel ID not found" (D10).

6. **Given** an `UpdatePartyComposite` command where the same channel ID appears in both `AddContactChannels` and `RemoveContactChannelIds`, **When** the command is handled, **Then** the entire composite is rejected with error: "conflicting operations on same channel ID" (D10).

7. **Given** an `UpdatePartyComposite` command where the same identifier ID appears in both `AddIdentifiers` and `RemoveIdentifierIds`, **When** the command is handled, **Then** the entire composite is rejected with error: "conflicting operations on same identifier ID" (D10).

8. **Given** an `UpdatePartyComposite` command with all lists empty and no details present, **When** the command is handled, **Then** a no-op result is returned -- no events emitted, no error.

9. **Given** an `UpdatePartyComposite` command with more than 100 total sub-operations, **When** the command is handled, **Then** the command is rejected before processing with "payload size exceeded" error (D17).

10. **Given** an `UpdatePartyComposite` command targeting a non-existent party, **When** the command is handled, **Then** a rejection is returned with "party not found".

## Tasks / Subtasks

- [x] Task 1: Implement `Handle(UpdatePartyComposite, PartyState?)` in PartyAggregate (AC: #1-#10)
    - [x] 1.1: Add method signature `public static CompositeCommandResult Handle(UpdatePartyComposite command, PartyState? state)`
    - [x] 1.2: Implement MaxSubOperations <= 0 safety guard (same as CreatePartyComposite)
    - [x] 1.3: Implement D17 payload size guard (count all sub-operation lists)
    - [x] 1.4: Implement state-null check (PartyNotFound rejection)
    - [x] 1.5: Implement no-op check (all lists empty, no details)
    - [x] 1.6: Build lookup structures from state (Dictionary\<string, ContactChannel\> + HashSet\<string\> for identifier IDs)
    - [x] 1.7: Implement conflict detection (same ID in add + remove, update + remove for channels; add + remove for identifiers)
    - [x] 1.8: Implement ID validation (blank IDs in all lists)
    - [x] 1.9: Implement validation of UpdateContactChannels IDs against state with existingChannelsById (D12)
    - [x] 1.10: Implement validation of RemoveContactChannelIds against state with deduplication (D12)
    - [x] 1.11: Implement validation of RemoveIdentifierIds against state with deduplication (D12)
    - [x] 1.12: Implement PersonDetails/OrganizationDetails type-check validation
    - [x] 1.13: Implement PersonDetails update event emission with display name re-derivation
    - [x] 1.14: Implement OrganizationDetails update event emission with display name re-derivation
    - [x] 1.15: Implement AddContactChannels processing with state-duplicate and payload-duplicate detection (D10)
    - [x] 1.16: Implement UpdateContactChannels event emission with dictionary lookup, within-list dedup, and preferred channel logic (nullable fields)
    - [x] 1.17: Implement RemoveContactChannelIds event emission (using deduplicated set from validation)
    - [x] 1.18: Implement AddIdentifiers processing with state-duplicate and payload-duplicate detection (D10)
    - [x] 1.19: Implement RemoveIdentifierIds event emission (using deduplicated set from validation)
    - [x] 1.20: Assemble and return CompositeCommandResult with Applied/Skipped/Rejected
- [x] Task 2: Build and regression verification
    - [x] 2.1: `dotnet build Hexalith.Parties.slnx` -- zero errors, zero warnings
    - [x] 2.2: `dotnet test Hexalith.Parties.slnx` -- all 181 tests pass, zero regressions

## Dev Notes

### What This Story Does

Add a single static `Handle` method to `PartyAggregate` that processes `UpdatePartyComposite` commands. This method updates an existing party's details, contact channels, and identifiers in one atomic operation using explicit add/update/remove lists, returning a `CompositeCommandResult` that tracks which sub-operations were applied vs skipped vs rejected. No new types need to be created -- all command, event, result, and rejection types already exist.

### What Already Exists (DO NOT CREATE)

**Command type (Contracts):**

- `UpdatePartyComposite` at `src/Hexalith.Parties.Contracts/Commands/UpdatePartyComposite.cs` -- sealed record with `PartyId`, `PersonDetails?`, `OrganizationDetails?`, `AddContactChannels` (IReadOnlyList\<AddContactChannel\>), `UpdateContactChannels` (IReadOnlyList\<UpdateContactChannel\>), `RemoveContactChannelIds` (IReadOnlyList\<string\>), `AddIdentifiers` (IReadOnlyList\<AddIdentifier\>), `RemoveIdentifierIds` (IReadOnlyList\<string\>)

**Result type (Contracts):**

- `CompositeCommandResult` at `src/Hexalith.Parties.Contracts/Results/CompositeCommandResult.cs` -- extends `DomainResult` with `Applied`, `Skipped`, `Rejected` (all IReadOnlyList\<string\>)

**Rejection events (Contracts):**

- `PartyNotFound` -- has `string? Message` property (use for "party does not exist")
- `CompositeOperationConflict` -- has `string? Message` property (use for payload size exceeded, conflicting operations)
- `ContactChannelNotFound` -- has `string? Message` property (use for invalid update/remove channel IDs)
- `IdentifierNotFound` -- has `string? Message` property (use for invalid remove identifier IDs)
- `PartyTypeMismatch` -- has `string? Message` property (use for wrong party type when updating details)

**Success events (Contracts):**

- `PersonDetailsUpdated` -- requires `PersonDetails` property
- `OrganizationDetailsUpdated` -- requires `OrganizationDetails` property
- `PartyDisplayNameDerived` -- requires `DisplayName`, `SortName` properties
- `ContactChannelAdded` -- requires `ContactChannelId`, `Type`, `Value`, `IsPreferred`
- `ContactChannelUpdated` -- has `ContactChannelId` (required), `Type?`, `Value?`, `IsPreferred?`
- `ContactChannelRemoved` -- requires `ContactChannelId`
- `IdentifierAdded` -- requires `IdentifierId`, `Type`, `Value`
- `IdentifierRemoved` -- requires `IdentifierId`
- `PreferredContactChannelChanged` -- requires `ContactChannelId`

**Existing Handle methods in PartyAggregate (reference patterns):**
Note: Line numbers below are from the pre-modification file. After inserting Handle(UpdatePartyComposite), all subsequent methods shift down ~150-250 lines. Search by method name, not line number.

- `Handle(CreatePartyComposite)` -- SISTER HANDLER, follow the same composite result assembly pattern exactly
- `Handle(UpdatePersonDetails)` -- shows type validation: `state.Type != PartyType.Person` -> reject with `PartyTypeMismatch`
- `Handle(UpdateOrganizationDetails)` -- shows type validation: `state.Type != PartyType.Organization` -> reject with `PartyTypeMismatch`
- `Handle(AddContactChannel)` -- shows state-duplicate detection with `state.ContactChannels.Any(c => c.Id == ...)`
- `Handle(UpdateContactChannel)` -- shows FindIndex loop pattern for channel lookup, preferred channel logic with type consideration. Key logic: `ContactChannelType targetType = command.Type ?? existingChannel.Type; if (command.IsPreferred == true && (!existingChannel.IsPreferred || targetType != existingChannel.Type))` -> emit `PreferredContactChannelChanged`
- `Handle(RemoveContactChannel)` -- shows channel not-found validation
- `Handle(AddIdentifier)` -- shows state-duplicate detection with `state.Identifiers.Any(i => i.Id == ...)`
- `Handle(RemoveIdentifier)` -- shows identifier not-found validation

**State type (Contracts):**

- `PartyState` at `src/Hexalith.Parties.Contracts/State/PartyState.cs` -- has `Type` (PartyType), `Person` (PersonDetails?), `Organization` (OrganizationDetails?), `ContactChannels` (IReadOnlyList\<ContactChannel\>), `Identifiers` (IReadOnlyList\<PartyIdentifier\>), `IsActive` (bool), `DisplayName` (string), `SortName` (string)
- `ContactChannel` value object: `Id` (string), `Type` (ContactChannelType), `Value` (string), `IsPreferred` (bool)
- `PartyIdentifier` value object: `Id` (string), `Type` (IdentifierType), `Value` (string), `Jurisdiction` (string?)

**Base class:** `DomainResult` at `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Results/DomainResult.cs` -- validates events cannot mix IRejectionEvent and non-rejection events.

### Implementation Guide

**File to modify:** `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`

Add one new method. Do NOT modify any existing methods. Place it immediately after the closing brace of the existing `Handle(CreatePartyComposite)` method. No new `using` directives are needed -- all referenced types are in already-imported namespaces.

**Method signature:**

```csharp
public static CompositeCommandResult Handle(UpdatePartyComposite command, PartyState? state)
```

**Critical difference from CreatePartyComposite:** UpdatePartyComposite REQUIRES state to exist (party must already be created). CreatePartyComposite rejects when state exists (idempotency). UpdatePartyComposite rejects when state is null.

**Processing order (validation-first, D12 all-or-nothing):**

**Variable scoping:** The following variables should be declared at method scope (not block scope) because they are used in both Phase 1 and Phase 2: `existingChannelsById` (Dictionary), `existingIdentifierIds` (HashSet), and the conflict-detection HashSets for remove channel IDs and remove identifier IDs. Phase 2 reuses these for O(1) lookups and deduplication.

**Phase 1 -- Early rejection (before any events):**

1. `ArgumentNullException.ThrowIfNull(command)` -- same pattern as all Handle methods

2. MaxSubOperations safety guard (same as CreatePartyComposite lines 22-25):

```csharp
if (MaxSubOperations <= 0)
{
    MaxSubOperations = DefaultMaxSubOperations;
}
```

3. D17 payload size guard -- count all sub-operation lists:

```csharp
int subOps = (command.PersonDetails is not null ? 1 : 0)
    + (command.OrganizationDetails is not null ? 1 : 0)
    + command.AddContactChannels.Count
    + command.UpdateContactChannels.Count
    + command.RemoveContactChannelIds.Count
    + command.AddIdentifiers.Count
    + command.RemoveIdentifierIds.Count;
```

If exceeds `MaxSubOperations`, return rejection with `CompositeOperationConflict`.

4. State null check: `state is null` -> reject with `PartyNotFound { Message = "Party does not exist." }`

5. No-op check: if `subOps == 0` (all lists empty, no details), return no-op CompositeCommandResult.

6. **Build lookup structures from state** (used throughout both phases for O(1) lookups):

```csharp
Dictionary<string, ContactChannel> existingChannelsById = new(StringComparer.Ordinal);
for (int i = 0; i < state.ContactChannels.Count; i++)
{
    existingChannelsById[state.ContactChannels[i].Id] = state.ContactChannels[i];
}
HashSet<string> existingIdentifierIds = new(StringComparer.Ordinal);
for (int i = 0; i < state.Identifiers.Count; i++)
{
    existingIdentifierIds.Add(state.Identifiers[i].Id);
}
```

These replace all repeated `state.ContactChannels.Any(...)` and `state.Identifiers.Any(...)` calls.

7. Conflict detection -- channel operations: Build `HashSet<string>` for each operation type's channel IDs. Check for intersections:
    - `AddContactChannels` IDs ∩ `RemoveContactChannelIds` -> reject with `CompositeOperationConflict { Message = "Conflicting operations on same channel ID: {id}." }`
    - `UpdateContactChannels` IDs ∩ `RemoveContactChannelIds` -> reject (same error)
    - **Note:** `AddContactChannels` ∩ `UpdateContactChannels` does NOT need explicit conflict detection -- if channel exists in state the add is skipped as D10 duplicate and the update proceeds; if channel doesn't exist the update fails validation at step 10.

8. Conflict detection -- identifier operations:
    - `AddIdentifiers` IDs ∩ `RemoveIdentifierIds` -> reject with `CompositeOperationConflict { Message = "Conflicting operations on same identifier ID: {id}." }`

9. ID validation -- blank IDs in all operation lists (same all-or-nothing pattern as CreatePartyComposite):
    - `AddContactChannels`: reject if any `ContactChannelId` is blank
    - `UpdateContactChannels`: reject if any `ContactChannelId` is blank
    - `RemoveContactChannelIds`: reject if any entry is blank
    - `AddIdentifiers`: reject if any `IdentifierId` is blank
    - `RemoveIdentifierIds`: reject if any entry is blank

10. Validate `UpdateContactChannels`: each `ContactChannelId` MUST exist in `existingChannelsById`. If any not found, reject with `ContactChannelNotFound { Message = $"Contact channel '{id}' not found." }` (D12 all-or-nothing).

11. Validate `RemoveContactChannelIds`: each ID MUST exist in `existingChannelsById`. If any not found, reject with `ContactChannelNotFound`. Also deduplicate within the list using a `HashSet<string>` -- skip duplicate remove IDs to prevent emitting two `ContactChannelRemoved` events for the same channel.

12. Validate `RemoveIdentifierIds`: each ID MUST exist in `existingIdentifierIds`. If any not found, reject with `IdentifierNotFound { Message = $"Identifier '{id}' not found." }`. Also deduplicate within the list.

13. PersonDetails type check: if `command.PersonDetails is not null` and `state.Type != PartyType.Person`, reject with `PartyTypeMismatch { Message = $"Cannot update person details on a {state.Type} party." }`.

14. OrganizationDetails type check: if `command.OrganizationDetails is not null` and `state.Type != PartyType.Organization`, reject with `PartyTypeMismatch { Message = $"Cannot update organization details on a {state.Type} party." }`.

**Phase 2 -- Event emission (after ALL validation passes):**

Initialize: `List<IEventPayload> events = []; List<string> applied = []; List<string> skipped = [];`

1. **PersonDetails update** (if present):
    - Emit `PersonDetailsUpdated { PersonDetails = command.PersonDetails }`
    - applied: `"Updated person details"`
    - Re-derive display name: `DeriveDisplayName(state.Type, command.PersonDetails, null)`
    - Emit `PartyDisplayNameDerived { DisplayName = displayName, SortName = sortName }`
    - applied: `"Derived display name"`

2. **OrganizationDetails update** (if present):
    - Emit `OrganizationDetailsUpdated { OrganizationDetails = command.OrganizationDetails }`
    - applied: `"Updated organization details"`
    - Re-derive display name: `DeriveDisplayName(state.Type, null, command.OrganizationDetails)`
    - Emit `PartyDisplayNameDerived { DisplayName = displayName, SortName = sortName }`
    - applied: `"Derived display name"`

3. **AddContactChannels** processing (skip duplicates against state AND payload):
    - Use `HashSet<string>(StringComparer.Ordinal)` for seen channel IDs within payload
    - For each channel in `command.AddContactChannels`:
        - If `existingChannelsById.ContainsKey(channel.ContactChannelId)` (state duplicate) -> skip, add to `skipped`: `"Duplicate contact channel: {id}"`
        - If `!seenChannelIds.Add(channel.ContactChannelId)` (payload duplicate) -> skip
        - Otherwise: emit `ContactChannelAdded` event, add to `applied`: `"Added contact channel: {id} ({type})"`
        - If `channel.IsPreferred`, also emit `PreferredContactChannelChanged { ContactChannelId = channel.ContactChannelId }`, add to `applied`: `"Set preferred contact channel: {id}"`

4. **UpdateContactChannels** processing:
    - Use `HashSet<string>(StringComparer.Ordinal)` to deduplicate within the list -- skip if same `ContactChannelId` appears twice
    - For each channel in `command.UpdateContactChannels`:
        - If `!seenUpdateChannelIds.Add(channel.ContactChannelId)` -> skip (duplicate within payload)
        - Look up existing channel using the dictionary built in Phase 1 step 6: `ContactChannel existingChannel = existingChannelsById[channel.ContactChannelId]` (already validated to exist in Phase 1)
        - Emit `ContactChannelUpdated { ContactChannelId = channel.ContactChannelId, Type = channel.Type, Value = channel.Value, IsPreferred = channel.IsPreferred }`
        - applied: `"Updated contact channel: {id}"`
        - **Preferred channel logic** (same as `Handle(UpdateContactChannel)` method -- search for it by name, not line number):
            - **Note:** `channel.Type` is `ContactChannelType?` (nullable), `channel.IsPreferred` is `bool?` (nullable), `channel.Value` is `string?` (nullable) -- null means "no change" for each field
            - Determine `ContactChannelType targetType = channel.Type ?? existingChannel.Type`
            - If `channel.IsPreferred == true && (!existingChannel.IsPreferred || targetType != existingChannel.Type)`:
              emit `PreferredContactChannelChanged { ContactChannelId = channel.ContactChannelId }`, applied: `"Set preferred contact channel: {id}"`

5. **RemoveContactChannelIds** processing:
    - Use `HashSet<string>(StringComparer.Ordinal)` to deduplicate within the list (already built during Phase 1 validation in step 11)
    - For each unique ID in `command.RemoveContactChannelIds`:
        - Emit `ContactChannelRemoved { ContactChannelId = id }`
        - applied: `"Removed contact channel: {id}"`

6. **AddIdentifiers** processing (skip duplicates against state AND payload):
    - Use `HashSet<string>(StringComparer.Ordinal)` for seen identifier IDs within payload
    - For each identifier in `command.AddIdentifiers`:
        - If `existingIdentifierIds.Contains(identifier.IdentifierId)` (state duplicate) -> skip, add to `skipped`: `"Duplicate identifier: {id}"`
        - If `!seenIdentifierIds.Add(identifier.IdentifierId)` (payload duplicate) -> skip
        - Otherwise: emit `IdentifierAdded` event, add to `applied`: `"Added identifier: {id} ({type})"`

7. **RemoveIdentifierIds** processing:
    - Use `HashSet<string>(StringComparer.Ordinal)` to deduplicate within the list (already built during Phase 1 validation in step 12)
    - For each unique ID in `command.RemoveIdentifierIds`:
        - Emit `IdentifierRemoved { IdentifierId = id }`
        - applied: `"Removed identifier: {id}"`

8. Return `new CompositeCommandResult(events, applied, skipped, [])`.

**For rejection returns:** Same pattern as CreatePartyComposite:

```csharp
return new CompositeCommandResult(
    [new PartyNotFound { Message = "Party does not exist." }],
    applied: [],
    skipped: [],
    rejected: ["Party does not exist."]);
```

**For no-op returns:**

```csharp
return new CompositeCommandResult(
    events: [],
    applied: [],
    skipped: [],
    rejected: []);
```

### Architecture Compliance

| Decision | Requirement                                                                          | Implementation                                                                                                                                                                                                                                                                              |
| -------- | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| D8/D9    | Single actor turn, atomic multi-event emission with explicit add/update/remove lists | One Handle call validates all, then emits all events in CompositeCommandResult                                                                                                                                                                                                              |
| D10      | Skip duplicate additions, reject invalid IDs, reject conflicting operations          | State-duplicate detection for adds (skip); ID validation for update/remove (reject); same-ID cross-list (reject)                                                                                                                                                                            |
| D12      | All-or-nothing -- no partial failure                                                 | All validation runs in Phase 1 before any events emitted in Phase 2. One rejection = zero events                                                                                                                                                                                            |
| D17      | Max sub-operation count                                                              | `MaxSubOperations` guard clause before processing (reuses existing configurable property from Story 4.1). Note: D17 counts sub-operations (items in command lists), NOT emitted events. Implicit events like `PreferredContactChannelChanged` and `PartyDisplayNameDerived` are not counted |
| D19      | Test matrix designed upfront                                                         | Test catalog defined in Story 4.3                                                                                                                                                                                                                                                           |

**Beyond-AC hardening (D10-principled, not in explicit ACs):**
The following behaviors are defensive engineering based on D10's principle of rejecting conflicting operations. They go beyond the 10 explicit ACs and should be included in Story 4.3's test matrix:

- Within-list dedup for `UpdateContactChannels` (skip duplicate channel IDs within the same list)
- Within-list dedup for `RemoveContactChannelIds` and `RemoveIdentifierIds` (prevent double-event emission)
- `UpdateContactChannels` IDs ∩ `RemoveContactChannelIds` conflict detection (update + remove same ID = reject)
  | Enforcement #4 | CompositeCommandResult from composite Handle | Return type is `CompositeCommandResult` |
  | Enforcement #5 | Synchronous Handle -- no `Task<>` | Return `CompositeCommandResult`, not `Task<CompositeCommandResult>` |

### Code Style Requirements

- File-scoped namespaces (`namespace X;`)
- `sealed` classes and records
- Allman brace style, 4-space indentation
- CRLF line endings, UTF-8 encoding
- `TreatWarningsAsErrors = true` -- zero warnings allowed
- No LINQ `First()`, `Single()`, `FirstOrDefault()`, `SingleOrDefault()` -- these throw or return ambiguous nulls. `Any()`, `Count`, and collection indexers are acceptable and used throughout the codebase
- No PII in log messages or Applied/Skipped/Rejected descriptions (use IDs, not names/values)

### ContactChannelType Enum Values

```csharp
public enum ContactChannelType { Email, Phone, PostalAddress, SocialMedia }
```

### IdentifierType Enum Values

```csharp
public enum IdentifierType { VAT, SIRET, NationalId, CompanyRegistration, TaxId, Other }
```

### DomainResult Constructor Constraint

`DomainResult` validates that events cannot mix `IRejectionEvent` and non-rejection events. `CompositeCommandResult` inherits this constraint. So:

- Success: Events = [PersonDetailsUpdated, DisplayNameDerived, ChannelAdded, ...] (all non-rejection), Rejected = []
- Rejection: Events = [rejection event] (all rejection), Applied = [], Skipped = []
- NoOp: Events = [] (empty), Applied = [], Skipped = [], Rejected = []

### Anti-Patterns to Avoid

- **DO NOT** delegate to individual Handle methods (Handle(UpdatePersonDetails), Handle(AddContactChannel), etc.) -- they return `DomainResult` not `CompositeCommandResult`, and have incompatible state/validation expectations
- **DO NOT** create new command, event, result, or rejection types -- all already exist
- **DO NOT** create new files -- this is a single method addition to an existing file
- **DO NOT** add async/await -- Handle is synchronous, pure domain logic
- **DO NOT** throw exceptions for domain rejections -- return CompositeCommandResult with rejection events
- **DO NOT** use LINQ `First()`, `Single()`, `FirstOrDefault()`, `SingleOrDefault()` -- use loops, `Any()`, dictionary lookups, or index access. `Any()` is acceptable.
- **DO NOT** emit duplicate events for the same entity ID -- deduplicate within Update/Remove lists using HashSets to prevent double `ContactChannelUpdated` or `ContactChannelRemoved` events
- **DO NOT** iterate `state.ContactChannels` or `state.Identifiers` repeatedly -- build `Dictionary<string, ContactChannel>` / `HashSet<string>` from state once, then use O(1) lookups throughout
- **DO NOT** treat nullable fields on `UpdateContactChannel` as required -- `Type?`, `Value?`, `IsPreferred?` are all nullable; null means "no change". Pass through to `ContactChannelUpdated` event as-is
- **DO NOT** reject duplicate add-channel/add-identifier requests -- skip them (D10 idempotency for MCP retry safety)
- **DO NOT** silently skip invalid update/remove IDs -- reject the entire composite (D12 all-or-nothing)
- **DO NOT** emit events before ALL validation is complete -- D12 requires validation-first, all-or-nothing
- **DO NOT** include PII (names, email addresses, values) in Applied/Skipped/Rejected strings -- use only IDs and types
- **DO NOT** forget `PreferredContactChannelChanged` when updating a channel to preferred (same logic as Handle(UpdateContactChannel))
- **DO NOT** redeclare `MaxSubOperations` -- it already exists from Story 4.1 as `public static int` with default 100
- **DO NOT** add test code or test data -- tests are Story 4.3; test data builders are added there
- **DO NOT** add REST endpoints or validators -- those are Story 4.4
- **DO NOT** use `channel.PartyId` for any sub-operation -- use `command.PartyId` as the authority

### Previous Story Intelligence (Story 4.1)

- **170 tests pass** after Story 4.1 completion (164 original + 6 new composite tests)
- Story 4.1 added `Handle(CreatePartyComposite)` -- **SISTER METHOD**, follow the identical result assembly pattern
- `MaxSubOperations` property already exists as `public static int` with default 100 -- REUSE, do not redeclare
- Validation order pattern established: null check -> payload size -> ID validation -> state checks -> type checks
- `HashSet<string>` with `StringComparer.Ordinal` used for duplicate detection -- follow same pattern
- Applied/Skipped strings use format: `"Verb noun: {id} ({type})"` -- follow same format
- All-or-nothing rejection pattern: return `CompositeCommandResult` with rejection event in events list + human-readable string in rejected list
- Code review findings from 4.1 that carry forward:
    - PII-safe applied messages (no display name values, no email values)
    - All-or-nothing rejection for blank sub-operation IDs
    - Configurable `MaxSubOperations`

### Git Intelligence

Recent commits (Epic 4 started):

```
39e713f Merge pull request #17 -- Story 4.1: CreatePartyComposite Aggregate Handler
85b67cf Implement Story 4.1: Create Party Composite Aggregate Handler
579aa3d Merge pull request #16 -- Story 3.4: Projection Unit & Integration Tests
```

Files modified in Story 4.1:

- `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` (177 lines added -- the Create composite handler)
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs` (217 lines -- 4.1 composite tests)

### Project Structure Notes

```
src/Hexalith.Parties.Server/Aggregates/
  PartyAggregate.cs                    <- MODIFY: Add Handle(UpdatePartyComposite) method after Handle(CreatePartyComposite)
```

No other files need to be modified or created for this story.

### Library & Framework Versions

| Package                       | Version                                          |
| ----------------------------- | ------------------------------------------------ |
| .NET SDK                      | 10.0.103 (net10.0)                               |
| Hexalith.EventStore.Contracts | (submodule, DomainResult/CompositeCommandResult) |
| xUnit                         | 2.9.3                                            |
| Shouldly                      | 4.3.0                                            |

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-4.2 -- Acceptance criteria and BDD requirements]
- [Source: _bmad-output/planning-artifacts/architecture.md#D8 -- Composite aggregate command strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md#D9 -- MCP update strategy: composite command with aggregate-side diff]
- [Source: _bmad-output/planning-artifacts/architecture.md#D10 -- Sub-operation idempotency and conflict detection]
- [Source: _bmad-output/planning-artifacts/architecture.md#D12 -- Partial failure eliminated by design]
- [Source: _bmad-output/planning-artifacts/architecture.md#D17 -- Maximum composite payload size]
- [Source: _bmad-output/planning-artifacts/architecture.md#D19 -- Test matrix designed upfront]
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs -- Handle(CreatePartyComposite) sister method + all existing Handle methods]
- [Source: src/Hexalith.Parties.Contracts/Commands/UpdatePartyComposite.cs -- Command record definition]
- [Source: src/Hexalith.Parties.Contracts/Results/CompositeCommandResult.cs -- Result type definition]
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Results/DomainResult.cs -- Base result class]
- [Source: src/Hexalith.Parties.Contracts/State/PartyState.cs -- Aggregate state with ContactChannels and Identifiers collections]
- [Source: src/Hexalith.Parties.Contracts/Events/ -- All event types referenced in this story]
- [Source: _bmad-output/implementation-artifacts/4-1-create-party-composite-aggregate-handler.md -- Previous story (sister handler)]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

No issues encountered during implementation.

### Completion Notes List

- Implemented `Handle(UpdatePartyComposite, PartyState?)` method in `PartyAggregate` (~280 lines)
- Phase 1 (validation-first): ArgumentNullException guard, MaxSubOperations safety, D17 payload size guard, state-null check, no-op check, lookup structure building, conflict detection (add/remove and update/remove for channels; add/remove for identifiers), blank ID validation, update/remove ID existence validation against state, person/organization type-check validation
- Phase 2 (event emission): PersonDetails/OrganizationDetails update with display name re-derivation, AddContactChannels with state+payload duplicate detection, UpdateContactChannels with within-list dedup and preferred channel logic, RemoveContactChannelIds with dedup, AddIdentifiers with state+payload duplicate detection, RemoveIdentifierIds with dedup
- All D8/D9/D10/D12/D17 architecture decisions satisfied
- Build: zero errors, zero warnings
- Tests: all 181 tests pass, zero regressions
- Existing aggregate and test files updated in place; no new production files were created
- Method placed immediately after Handle(CreatePartyComposite) sister method
- Review fixes applied: duplicate update/remove sub-operations now populate `CompositeCommandResult.Skipped`, and remove events preserve request order after deduplication
- Added focused automated coverage for `UpdatePartyComposite` acceptance criteria and duplicate outcome reporting in server aggregate tests

### File List

- `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` (modified -- added `Handle(UpdatePartyComposite)` and refined duplicate outcome reporting / stable remove ordering during review)
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs` (modified -- added comprehensive `UpdatePartyComposite` coverage for ACs and review fixes)

## Senior Developer Review (AI)

### Outcome

Approved after fixes.

Reviewer: GPT-5.4

### Findings Resolved

- Added automated server-level coverage for `UpdatePartyComposite` scenarios covering success, no-op, not-found, conflict, payload-limit, and duplicate-handling paths.
- Updated `CompositeCommandResult` behavior so duplicate update/remove payload entries are reported in `Skipped` instead of being silently ignored.
- Preserved request order for deduplicated remove operations to keep emitted event ordering deterministic.

### Verification Notes

- `dotnet build Hexalith.Parties.slnx --no-restore --nologo`
- `dotnet test Hexalith.Parties.slnx --no-restore --nologo`

## Change Log

- 2026-03-06: Implemented Handle(UpdatePartyComposite) method in PartyAggregate with full Phase 1 validation and Phase 2 event emission following CreatePartyComposite sister pattern. All 181 tests pass, zero build warnings.
- 2026-03-06: Review follow-up fixes applied for Story 4.2: added `UpdatePartyComposite` automated coverage, surfaced duplicate update/remove payload entries in `Skipped`, preserved removal event order, and approved the story.
