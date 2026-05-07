# Story 3.1: Party Detail Projection Handler & Actor

Status: done

<!-- Key Context: This story creates the first read-side projection infrastructure. PartyDetailProjectionHandler is a pure class (zero DAPR refs, D18) with an Apply method that transforms domain events into PartyDetail read models. PartyDetailProjectionActor is a thin DAPR actor wrapper managing state at key {tenant}:party-detail:{partyId} (D4). The Hexalith.Parties.Projections project already exists with Dapr.Client and Dapr.Actors references but no source files. PartyDetail and PartyIndexEntry read models already exist in Contracts/Models. All domain events implement IEventPayload. Follow the same Apply pattern used in PartyState but returning new state instead of mutating. -->

## Story

As a developer,
I want a projection that maintains full party detail read models updated from domain events,
So that consumers can retrieve complete, up-to-date party information by ID without rehydrating the aggregate.

## Acceptance Criteria

1. **Given** a `PartyCreated` event is published, **When** the `PartyDetailProjectionHandler` processes it, **Then** a `PartyDetail` read model is created with party ID, type, details, display name, active status, CreatedAt timestamp.

2. **Given** a `PersonDetailsUpdated` event is published for an existing party, **When** the handler processes it, **Then** the `PartyDetail` read model is updated with the new person details and LastModifiedAt timestamp.

3. **Given** a `ContactChannelAdded` event is published for an existing party, **When** the handler processes it, **Then** the `PartyDetail` read model includes the new contact channel in its collection.

4. **Given** a `ContactChannelUpdated` event is published, **When** the handler processes it, **Then** the corresponding contact channel in the `PartyDetail` is updated.

5. **Given** a `ContactChannelRemoved` event is published, **When** the handler processes it, **Then** the corresponding contact channel is removed from the `PartyDetail`.

6. **Given** an `IdentifierAdded` event is published, **When** the handler processes it, **Then** the `PartyDetail` includes the new identifier.

7. **Given** an `IdentifierRemoved` event is published, **When** the handler processes it, **Then** the corresponding identifier is removed from the `PartyDetail`.

8. **Given** a `PartyDeactivated` event is published, **When** the handler processes it, **Then** the `PartyDetail` reflects `IsActive = false`.

9. **Given** the `PartyDetailProjectionHandler` class, **When** reviewed for architecture compliance, **Then** it has zero DAPR references (D18 -- pure handler, Tier 1 testable) **And** it receives events and returns state mutations.

10. **Given** the `PartyDetailProjectionActor` class, **When** reviewed for architecture compliance, **Then** it is a thin DAPR wrapper that delegates to `PartyDetailProjectionHandler` **And** its state key follows the pattern `{tenant}:party-detail:{partyId}` (one actor per party -- D4) **And** tenant isolation is enforced at the actor key level.

11. **Given** a domain event published via DAPR pub/sub, **When** the projection actor receives it, **Then** the `PartyDetail` state is updated within the eventual consistency window (< 2 seconds under normal load -- FR19, NFR6).

12. **Given** all tests in the solution, **When** `dotnet test` is run, **Then** all tests pass (existing 113 + new tests), zero regressions.

## Tasks / Subtasks

- [x] Task 1: Create `PartyDetailProjectionHandler` pure handler class (AC: #1-#8, #9)
  - [x] 1.1: Create `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs` with `Apply(string partyId, IEventPayload @event, PartyDetail? state)` method
  - [x] 1.2: Implement `PartyCreated` handling -- create new `PartyDetail` with Id, Type, PersonDetails/OrganizationDetails, IsActive=true, CreatedAt=DateTimeOffset.UtcNow
  - [x] 1.3: Implement `PartyDisplayNameDerived` handling -- update DisplayName, SortName, LastModifiedAt
  - [x] 1.4: Implement `PersonDetailsUpdated` handling -- update PersonDetails, LastModifiedAt
  - [x] 1.5: Implement `OrganizationDetailsUpdated` handling -- update OrganizationDetails, LastModifiedAt
  - [x] 1.6: Implement `ContactChannelAdded` handling -- append to ContactChannels, LastModifiedAt
  - [x] 1.7: Implement `ContactChannelUpdated` handling -- update matching channel by Id, LastModifiedAt
  - [x] 1.8: Implement `ContactChannelRemoved` handling -- remove matching channel by Id, LastModifiedAt
  - [x] 1.9: Implement `PreferredContactChannelChanged` handling -- update IsPreferred flags on matching type, LastModifiedAt
  - [x] 1.10: Implement `IdentifierAdded` handling -- append to Identifiers, LastModifiedAt
  - [x] 1.11: Implement `IdentifierRemoved` handling -- remove matching identifier by Id, LastModifiedAt
  - [x] 1.12: Implement `PartyDeactivated` handling -- set IsActive=false, LastModifiedAt
  - [x] 1.13: Implement `PartyReactivated` handling -- set IsActive=true, LastModifiedAt
  - [x] 1.14: Implement `IsNaturalPersonChanged` handling -- no-op for detail (not stored in PartyDetail), or ignore gracefully
  - [x] 1.15: Return `null` (unchanged) for unrecognized events -- forward compatibility

- [x] Task 2: Create `PartyDetailProjectionActor` DAPR actor wrapper (AC: #10, #11)
  - [x] 2.1: Create `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs` implementing DAPR `Actor` base class
  - [x] 2.2: Actor receives domain events, delegates to `PartyDetailProjectionHandler.Apply()`
  - [x] 2.3: State key format: `{tenant}:party-detail:{partyId}` -- extract tenant and partyId from actor ID
  - [x] 2.4: Load state from DAPR state store, apply handler result, save updated state
  - [x] 2.5: Handle null state (first event for new party) vs existing state gracefully

- [x] Task 3: Create Tier 1 unit tests for `PartyDetailProjectionHandler` (AC: #1-#8, #9, #12)
  - [x] 3.1: Create `tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerTests.cs`
  - [x] 3.2: Test `PartyCreated` -- creates new PartyDetail with correct fields
  - [x] 3.3: Test `PartyDisplayNameDerived` -- updates DisplayName and SortName
  - [x] 3.4: Test `PersonDetailsUpdated` -- updates PersonDetails
  - [x] 3.5: Test `OrganizationDetailsUpdated` -- updates OrganizationDetails
  - [x] 3.6: Test `ContactChannelAdded` -- adds channel to collection
  - [x] 3.7: Test `ContactChannelUpdated` -- updates existing channel
  - [x] 3.8: Test `ContactChannelRemoved` -- removes channel from collection
  - [x] 3.9: Test `PreferredContactChannelChanged` -- updates IsPreferred flags
  - [x] 3.10: Test `IdentifierAdded` -- adds identifier to collection
  - [x] 3.11: Test `IdentifierRemoved` -- removes identifier from collection
  - [x] 3.12: Test `PartyDeactivated` -- sets IsActive=false
  - [x] 3.13: Test `PartyReactivated` -- sets IsActive=true
  - [x] 3.14: Test multi-event sequence: PartyCreated -> ContactChannelAdded x3 -> IdentifierAdded x2 -> verify complete state
  - [x] 3.15: Test unrecognized event -- returns null/unchanged
  - [x] 3.16: Test null state + non-PartyCreated event -- returns null (cannot project without creation)

- [x] Task 4: Build and regression verification (AC: #12)
  - [x] 4.1: `dotnet build Hexalith.Parties.slnx` -- zero errors, zero new warnings
  - [x] 4.2: `dotnet test Hexalith.Parties.slnx` -- all tests pass (113 existing + 18 new = 131), zero regressions

## Dev Notes

### What This Story Does

This story creates the **first read-side projection** in the system. It builds two classes:
1. `PartyDetailProjectionHandler` -- a pure C# class that transforms domain events into `PartyDetail` read models (D18 pure handler extraction)
2. `PartyDetailProjectionActor` -- a thin DAPR actor wrapper that manages state persistence and delegates to the handler (D4 per-party granularity)

The `PartyDetail` read model already exists in `Contracts/Models/PartyDetail.cs`. The `Hexalith.Parties.Projections` project already exists with Dapr.Client and Dapr.Actors NuGet references but contains no source files.

### Handler Pattern -- Pure Function, No DAPR (D18)

The handler follows the same conceptual pattern as `PartyState.Apply()` but with key differences:
- **Returns new state** (immutable record `with` expressions) instead of mutating
- **Receives `IEventPayload`** and pattern-matches on concrete event types
- **No DAPR references** -- zero `using Dapr.*` statements (CI fitness test)
- **No side effects** -- pure function: (event, currentState) -> newState

**Reference pattern from PartyState** (`src/Hexalith.Parties.Contracts/State/PartyState.cs`):
```csharp
// PartyState uses mutable Apply methods:
public void Apply(PartyCreated e)
{
    Type = e.Type;
    Person = e.PersonDetails;
    Organization = e.OrganizationDetails;
}

// Handler should use immutable returns:
public PartyDetail? Apply(string partyId, IEventPayload @event, PartyDetail? state)
{
    return @event switch
    {
        PartyCreated e => HandlePartyCreated(partyId, e),
        PersonDetailsUpdated e when state is not null => state with { PersonDetails = e.PersonDetails, LastModifiedAt = DateTimeOffset.UtcNow },
        // ... other events
        _ => null, // Unrecognized event, no state change
    };
}
```

**Events the handler MUST process** (all implement `IEventPayload` from `Hexalith.EventStore.Contracts.Events`):

| Event | Source File | Action on PartyDetail |
|---|---|---|
| `PartyCreated` | `Events/PartyCreated.cs` | Create new PartyDetail with Type, PersonDetails/OrganizationDetails, IsActive=true, CreatedAt |
| `PartyDisplayNameDerived` | `Events/PartyDisplayNameDerived.cs` | Update DisplayName, SortName, LastModifiedAt |
| `PersonDetailsUpdated` | `Events/PersonDetailsUpdated.cs` | Update PersonDetails, LastModifiedAt |
| `OrganizationDetailsUpdated` | `Events/OrganizationDetailsUpdated.cs` | Update OrganizationDetails, LastModifiedAt |
| `ContactChannelAdded` | `Events/ContactChannelAdded.cs` | Append new ContactChannel to collection, LastModifiedAt |
| `ContactChannelUpdated` | `Events/ContactChannelUpdated.cs` | Update matching channel (by Id), LastModifiedAt |
| `ContactChannelRemoved` | `Events/ContactChannelRemoved.cs` | Remove matching channel (by Id), LastModifiedAt |
| `PreferredContactChannelChanged` | `Events/PreferredContactChannelChanged.cs` | Update IsPreferred flags for matching type, LastModifiedAt |
| `IdentifierAdded` | `Events/IdentifierAdded.cs` | Append new PartyIdentifier, LastModifiedAt |
| `IdentifierRemoved` | `Events/IdentifierRemoved.cs` | Remove matching identifier (by Id), LastModifiedAt |
| `PartyDeactivated` | `Events/PartyDeactivated.cs` | Set IsActive=false, LastModifiedAt |
| `PartyReactivated` | `Events/PartyReactivated.cs` | Set IsActive=true, LastModifiedAt |
| `IsNaturalPersonChanged` | `Events/IsNaturalPersonChanged.cs` | Ignore -- not stored in PartyDetail |
| `PartyMerged` | `Events/PartyMerged.cs` | Ignore -- v2 placeholder |

### Actor Pattern -- Thin DAPR Wrapper (D4)

The actor is a thin wrapper that:
1. Receives events via DAPR pub/sub subscription
2. Loads current `PartyDetail` state from DAPR state store
3. Delegates to `PartyDetailProjectionHandler.Apply()`
4. Saves updated state back to DAPR state store

**Actor ID format:** The actor ID encodes tenant and partyId. The state key follows `{tenant}:party-detail:{partyId}`.

**Actor implementation pattern:**
```csharp
namespace Hexalith.Parties.Projections.Actors;

public sealed class PartyDetailProjectionActor : Actor
{
    private readonly PartyDetailProjectionHandler _handler = new();

    public PartyDetailProjectionActor(ActorHost host) : base(host) { }

    public async Task HandleEventAsync(string partyId, IEventPayload @event)
    {
        PartyDetail? currentState = await StateManager.TryGetStateAsync<PartyDetail>("state", default);
        PartyDetail? newState = _handler.Apply(partyId, @event, currentState.HasValue ? currentState.Value : null);
        if (newState is not null)
        {
            await StateManager.SetStateAsync("state", newState, default);
        }
    }
}
```

### Existing Read Models (DO NOT MODIFY -- already exist in Contracts)

**PartyDetail** (`src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`):
```csharp
public sealed record PartyDetail
{
    public required string Id { get; init; }
    public required PartyType Type { get; init; }
    public bool IsActive { get; init; }
    [PersonalData] public required string DisplayName { get; init; }
    [PersonalData] public required string SortName { get; init; }
    public PersonDetails? PersonDetails { get; init; }
    public OrganizationDetails? OrganizationDetails { get; init; }
    public IReadOnlyList<ContactChannel> ContactChannels { get; init; } = [];
    public IReadOnlyList<PartyIdentifier> Identifiers { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastModifiedAt { get; init; }
}
```

### Event Signatures (DO NOT MODIFY -- already exist in Contracts)

All events are sealed records implementing `IEventPayload` (marker interface from `Hexalith.EventStore.Contracts.Events`). Key event fields:

- `PartyCreated`: Type, PersonDetails?, OrganizationDetails?
- `PartyDisplayNameDerived`: DisplayName, SortName
- `PersonDetailsUpdated`: PersonDetails
- `OrganizationDetailsUpdated`: OrganizationDetails
- `ContactChannelAdded`: ContactChannelId, Type, Value, IsPreferred
- `ContactChannelUpdated`: ContactChannelId, Type?, Value?, IsPreferred?
- `ContactChannelRemoved`: ContactChannelId
- `PreferredContactChannelChanged`: ContactChannelId
- `IdentifierAdded`: IdentifierId, Type, Value
- `IdentifierRemoved`: IdentifierId
- `PartyDeactivated`: (no payload)
- `PartyReactivated`: (no payload)

### ContactChannel Update Logic -- Follow PartyState Pattern

For `ContactChannelUpdated`, nullable fields mean "don't change if null". Follow the exact same pattern from `PartyState.Apply(ContactChannelUpdated)`:
```csharp
// From PartyState (reference):
ContactChannel existing = _contactChannels[idx];
_contactChannels[idx] = existing with
{
    Type = e.Type ?? existing.Type,
    Value = e.Value ?? existing.Value,
    IsPreferred = e.IsPreferred ?? existing.IsPreferred,
};
```

For the handler, use immutable list operations since `PartyDetail.ContactChannels` is `IReadOnlyList<ContactChannel>`:
```csharp
// Convert to mutable list, update, convert back
var channels = state.ContactChannels.ToList();
int idx = channels.FindIndex(c => c.Id == e.ContactChannelId);
if (idx >= 0)
{
    channels[idx] = channels[idx] with
    {
        Type = e.Type ?? channels[idx].Type,
        Value = e.Value ?? channels[idx].Value,
        IsPreferred = e.IsPreferred ?? channels[idx].IsPreferred,
    };
}
return state with { ContactChannels = channels, LastModifiedAt = DateTimeOffset.UtcNow };
```

### PreferredContactChannelChanged Logic -- Follow PartyState Pattern

Same logic as PartyState: update IsPreferred on all channels of the same type:
```csharp
// Reference from PartyState.Apply(PreferredContactChannelChanged):
ContactChannelType targetType = _contactChannels[targetIdx].Type;
for (int i = 0; i < _contactChannels.Count; i++)
{
    if (_contactChannels[i].Type == targetType)
    {
        _contactChannels[i] = _contactChannels[i] with
        {
            IsPreferred = _contactChannels[i].Id == e.ContactChannelId,
        };
    }
}
```

### Project Structure Notes

**New files (this story):**
```
src/Hexalith.Parties.Projections/
├── Handlers/
│   └── PartyDetailProjectionHandler.cs     <- NEW: Pure handler, zero DAPR refs
├── Actors/
│   └── PartyDetailProjectionActor.cs       <- NEW: Thin DAPR actor wrapper
tests/Hexalith.Parties.Projections.Tests/
└── Handlers/
    └── PartyDetailProjectionHandlerTests.cs <- NEW: Tier 1 handler tests
```

**No modifications to:**
- `src/Hexalith.Parties.Contracts/` -- all read models and events already exist
- `src/Hexalith.Parties.Server/` -- aggregate is not affected
- `src/Hexalith.Parties/` -- query endpoints are Story 3.3
- `src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj` -- already has Dapr.Client + Dapr.Actors + Contracts reference

**Verify test project has correct references:**
```xml
<!-- tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj -->
<!-- Should already reference: Projections project, Testing project, xUnit, Shouldly -->
```

### Architecture Compliance

- **D18 Pure Handler:** `PartyDetailProjectionHandler` has ZERO `using Dapr.*` statements. Receives events and returns state mutations only.
- **D4 Per-Party Actor:** One `PartyDetailProjectionActor` per party. State key: `{tenant}:party-detail:{partyId}`.
- **D1 DAPR State Store:** State persisted via DAPR actor state management (not direct state store access).
- **Immutable Read Models:** `PartyDetail` is a sealed record -- use `with` expressions for mutations.
- **Tenant Isolation:** Actor ID encodes tenant. State key includes tenant prefix. Cross-tenant access impossible at actor level.
- **Forward Compatibility:** Unrecognized events return null (no state change). Handler handles missing/null fields gracefully.
- **Idempotent Event Processing:** Handler produces same result when processing the same event twice on the same state.

### Testing Standards

- **Tier 1 tests only** for handler (pure unit tests, zero DAPR, zero infrastructure)
- **xUnit + Shouldly** for assertions
- **Test method naming:** `Apply_{EventType}_{Scenario}_{ExpectedResult}`
- **Test data:** Use `PartyTestData` helpers from `Hexalith.Parties.Testing` where applicable, create projection-specific helpers as needed
- **Multi-event sequence test:** `PartyCreated` -> `PartyDisplayNameDerived` -> `ContactChannelAdded` x3 -> `IdentifierAdded` x2 -> verify complete `PartyDetail` state
- **No actor tests in this story** -- actor integration tests (Tier 2) are part of Story 3.4

### Code Style Requirements

- File-scoped namespaces (`namespace X;`)
- `sealed` classes
- Allman brace style, 4-space indentation
- CRLF line endings, UTF-8 encoding
- One public type per file, file name = type name
- `TreatWarningsAsErrors = true` -- zero warnings allowed
- No positional record parameters

### Library & Framework Versions

| Package | Version |
|---|---|
| .NET SDK | 10.0.103 (net10.0) |
| Dapr.Client | 1.17.0 |
| Dapr.Actors | 1.17.0 |
| xUnit | 2.9.3 |
| Shouldly | 4.3.0 |

No new packages needed -- Projections project already has Dapr references.

### Anti-Patterns to Avoid

- **DO NOT** add DAPR references to `PartyDetailProjectionHandler` -- it must be pure (D18)
- **DO NOT** modify existing `Contracts/Models/PartyDetail.cs` -- read model already has all needed fields
- **DO NOT** modify existing event types in `Contracts/Events/`
- **DO NOT** create query endpoints -- that is Story 3.3
- **DO NOT** create index projection -- that is Story 3.2
- **DO NOT** create Tier 2 actor integration tests -- that is Story 3.4
- **DO NOT** mutate state -- use immutable `with` expressions on records
- **DO NOT** throw exceptions for unrecognized events -- return null for forward compatibility
- **DO NOT** add batch processing to the detail actor -- batch processing (D16) is for the index actor only (Story 3.2)
- **DO NOT** implement projection rebuild (D14) or corruption handling (D15) -- those are Story 3.4/operational readiness
- **DO NOT** use `First()` or `Single()` -- use `FindIndex()` for collection lookups (project convention)

### Previous Story Intelligence (Story 2.4 -- most recent)

- **113 tests pass** (21 contracts + 68 server + 2 integration + 22 CommandApi), zero regressions
- **All 11 Handle methods** complete in `PartyAggregate.cs`
- Build succeeds with `TreatWarningsAsErrors = true`
- Pre-existing ASPIRE004 build warning is unrelated -- ignore
- **Branch naming pattern:** `implement-story-3-1-party-detail-projection-handler-and-actor`
- **Commit message pattern:** `Implement Story 3.1: Party Detail Projection Handler & Actor`
- FluentValidation validators auto-discovered via assembly scanning
- `PartyState.Apply()` methods are the reference pattern for event handling logic
- `PartyTestData` provides factory methods for creating test states and commands

### Git Intelligence

**Recent commits:**
```
bd4d7c3 Merge pull request #12 -- Story 2.4: REST API Contact Channel & Identifier Endpoints
519306f Implement Story 2.4: REST API Contact Channel & Identifier Endpoints
7542827 Merge pull request #11 -- Story 2.3: Contact Channel & Identifier Unit Tests
468d6c8 Implement Story 2.3: Contact Channel & Identifier Unit Tests
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-3.1 -- Acceptance criteria and BDD requirements]
- [Source: _bmad-output/planning-artifacts/architecture.md -- D1, D4, D5, D14, D15, D16, D18 projection decisions]
- [Source: src/Hexalith.Parties.Contracts/Models/PartyDetail.cs -- Read model record definition]
- [Source: src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs -- Index entry record (context only)]
- [Source: src/Hexalith.Parties.Contracts/State/PartyState.cs -- Apply method patterns for all events]
- [Source: src/Hexalith.Parties.Contracts/Events/*.cs -- All 14 domain events implementing IEventPayload]
- [Source: src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj -- Existing project with Dapr refs]
- [Source: src/Hexalith.Parties.Testing/PartyTestData.cs -- Test data factory methods]
- [Source: _bmad-output/implementation-artifacts/2-4-rest-api-contact-channel-and-identifier-endpoints.md -- Previous story patterns]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

- CA1822 warning: `Apply` method marked `static` since it has no instance data access (all helper methods already static)
- CA2007 warning: Added `ConfigureAwait(false)` to async calls in actor

### Completion Notes List

- Created `PartyDetailProjectionHandler` as a pure static handler with zero DAPR references (D18 compliance)
- Handler uses C# switch expression with pattern matching on 14 event types
- Immutable state mutations via `with` expressions on sealed records
- `ContactChannelUpdated` preserves null fields (partial update pattern from PartyState)
- `PreferredContactChannelChanged` updates IsPreferred flags for all channels of the same type
- Unrecognized events (including `IsNaturalPersonChanged`, `PartyMerged`) return null for forward compatibility
- Created `PartyDetailProjectionActor` as thin DAPR actor wrapper delegating to static handler
- 18 unit tests covering all event handlers, multi-event sequence, null state, and unrecognized events
- Full solution: 131 tests pass (113 existing + 18 new), zero regressions, zero warnings
- Senior review fixes applied: actor now derives tenant/party from actor ID and persists under `{tenant}:party-detail:{partyId}` state key
- Senior review fixes applied: `PartyCreated` now initializes derived `DisplayName`/`SortName` from details
- Senior review fixes applied: no-op channel/preferred updates return unchanged (`null`) instead of mutating `LastModifiedAt`

### File List

- `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs` (NEW)
- `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs` (NEW)
- `tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerTests.cs` (NEW)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (UPDATED)

### Change Log

- 2026-03-05: Implemented Story 3.1 -- PartyDetailProjectionHandler (pure handler, 14 events) and PartyDetailProjectionActor (DAPR wrapper). Added 18 Tier 1 unit tests. 131 total tests pass.
- 2026-03-05: Senior AI code review fixes applied -- actor ID/state-key compliance, PartyCreated derived display name initialization, no-op update handling corrections. Story moved to done.

### Senior Developer Review (AI)

- Outcome: Changes requested issues fixed automatically.
- High issues fixed:
    - AC/task alignment for state key format `{tenant}:party-detail:{partyId}` in actor state persistence.
    - Tenant/party isolation reinforced by parsing actor id and validating incoming party id.
    - AC1 alignment: created projection now initializes display/sort names from party details.
- Medium issues fixed:
    - Prevented false `LastModifiedAt` updates for unmatched/no-op contact-channel and preferred-channel updates.
    - Updated story file list traceability to include sprint status synchronization artifact.
