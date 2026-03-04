# Story 1.2: Domain Contracts — Complete Type Definitions

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want all party domain types (commands, events, value objects, state, models, results, enums) defined in the Contracts project,
so that the domain model contracts are stable before aggregate and projection implementation begins.

## Acceptance Criteria

1. **Command types exist as sealed records with `{ get; init; }` properties:**
    - `CreateParty`, `CreatePartyComposite`, `UpdatePartyComposite`
    - `UpdatePersonDetails`, `UpdateOrganizationDetails`, `SetIsNaturalPerson`
    - `AddContactChannel`, `UpdateContactChannel`, `RemoveContactChannel`
    - `AddIdentifier`, `RemoveIdentifier`
    - `DeactivateParty`, `ReactivateParty`
2. **Commands carry `PartyId` (aggregate ID) but NOT `TenantId`** (TenantId extracted from request context)
3. **Entity IDs (ContactChannelId, IdentifierId) are client-generated UUIDs** — commands carry the ID, events echo it
4. **Event types exist as sealed records implementing `IEventPayload`:**
    - `PartyCreated`, `PersonDetailsUpdated`, `OrganizationDetailsUpdated`
    - `ContactChannelAdded`, `ContactChannelUpdated`, `ContactChannelRemoved`, `PreferredContactChannelChanged`
    - `IdentifierAdded`, `IdentifierRemoved`
    - `IsNaturalPersonChanged`
    - `PartyDeactivated`, `PartyReactivated`
    - `PartyDisplayNameDerived`
    - `PartyMerged` (v2 forward-compatibility placeholder — FR37)
5. **Rejection events exist implementing `IRejectionEvent`:**
    - `PartyCannotBeCreatedWithoutType`, `PartyCannotAddDuplicateChannel` (and other rejection scenarios)
6. **Value objects exist as sealed records:**
    - `PersonDetails`, `OrganizationDetails`, `ContactChannel`, `PartyIdentifier`
    - `PostalAddress`, `EmailAddress`, `PhoneNumber`, `SocialMediaHandle`
7. **Enums exist:** `PartyType` (Person, Organization), `ContactChannelType`, `IdentifierType`
8. **`PartyState` exists as a sealed class** with `{ get; private set; }` properties, `Apply` methods for ALL event types, and private list backing fields for collections
9. **Query models exist:** `PartyDetail` (full party view), `PartyIndexEntry` (lightweight summary with CreatedAt, LastModifiedAt)
10. **`CompositeCommandResult` extends `DomainResult`** with Applied/Skipped/Rejected collections
11. **`[PersonalData]` attributes applied to:** PersonDetails fields (first name, last name, DOB, prefix, suffix), all contact channel payloads, identifier values, and derived fields (display name, sort name) — following D6 type-dependent scope
12. **`IsNaturalPerson` boolean** exists on OrganizationDetails or PartyState — when true, elevates org to person-level encryption scope
13. **Contracts project has zero runtime dependencies beyond EventStore.Contracts** (which itself has zero deps — FR33 spirit preserved)
14. **No positional record parameters** (anti-pattern)
15. **One public type per file**, file name = type name

## Tasks / Subtasks

- [x] Task 1: Add EventStore.Contracts ProjectReference to Contracts .csproj (AC: #13)
    - [x] 1.1: Verify EventStore.Contracts TargetFramework — run `dotnet build Hexalith.EventStore/src/Hexalith.EventStore.Contracts/` and confirm it compiles to a TFM compatible with net10.0 (net10.0 or netstandard2.0/2.1 are both compatible)
    - [x] 1.2: Add ProjectReference to `../../Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj`
    - [x] 1.3: Verify `dotnet restore` succeeds with the new reference
- [x] Task 2: Create custom `[PersonalData]` attribute (AC: #11, #13)
    - [x] 2.1: Create `src/Hexalith.Parties.Contracts/PersonalDataAttribute.cs` — custom attribute to avoid ASP.NET Core Identity dependency
- [x] Task 3: Create enums in `ValueObjects/` folder (AC: #7)
    - [x] 3.1: `PartyType.cs` — Person, Organization
    - [x] 3.2: `ContactChannelType.cs` — Email, Phone, PostalAddress, SocialMedia (extensible)
    - [x] 3.3: `IdentifierType.cs` — VAT, SIRET, NationalId, etc. (extensible)
- [x] Task 4: Create value objects in `ValueObjects/` folder (AC: #6)
    - [x] 4.1: `PostalAddress.cs` — sealed record (Street, City, Region, PostalCode, Country)
    - [x] 4.2: `EmailAddress.cs` — sealed record (Address)
    - [x] 4.3: `PhoneNumber.cs` — sealed record (Number, CountryCode?)
    - [x] 4.4: `SocialMediaHandle.cs` — sealed record (Platform, Handle)
    - [x] 4.5: `PersonDetails.cs` — sealed record (FirstName, LastName, DateOfBirth?, Prefix?, Suffix?) with `[PersonalData]`
    - [x] 4.6: `OrganizationDetails.cs` — sealed record (LegalName, TradingName?, LegalForm?, RegistrationNumber?, IsNaturalPerson)
    - [x] 4.7: `ContactChannel.cs` — sealed record (Id, Type, Value, IsPreferred) with `[PersonalData]` on Value
    - [x] 4.8: `PartyIdentifier.cs` — sealed record (Id, Type, Value, Jurisdiction?) with `[PersonalData]` on Value
- [x] Task 5: Create command types in `Commands/` folder (AC: #1, #2, #3, #14)
    - [x] 5.1: `CreateParty.cs` — PartyId, PartyType, PersonDetails?, OrganizationDetails?
    - [x] 5.2: `CreatePartyComposite.cs` — PartyId, PartyType, PersonDetails?, OrganizationDetails?, ContactChannels[], Identifiers[]
    - [x] 5.3: `UpdatePartyComposite.cs` — PartyId + explicit add/update/remove lists per D9
    - [x] 5.4: `UpdatePersonDetails.cs` — PartyId, PersonDetails
    - [x] 5.5: `UpdateOrganizationDetails.cs` — PartyId, OrganizationDetails
    - [x] 5.6: `SetIsNaturalPerson.cs` — PartyId, IsNaturalPerson
    - [x] 5.7: `AddContactChannel.cs` — PartyId, ContactChannelId, Type, Value
    - [x] 5.8: `UpdateContactChannel.cs` — PartyId, ContactChannelId, Type?, Value?
    - [x] 5.9: `RemoveContactChannel.cs` — PartyId, ContactChannelId
    - [x] 5.10: `AddIdentifier.cs` — PartyId, IdentifierId, Type, Value
    - [x] 5.11: `RemoveIdentifier.cs` — PartyId, IdentifierId
    - [x] 5.12: `DeactivateParty.cs` — PartyId
    - [x] 5.13: `ReactivateParty.cs` — PartyId
- [x] Task 6: Create event types in `Events/` folder (AC: #4, #14)
    - [x] 6.1: `PartyCreated.cs` : IEventPayload — PartyType, PersonDetails?, OrganizationDetails?
    - [x] 6.2: `PersonDetailsUpdated.cs` : IEventPayload — PersonDetails
    - [x] 6.3: `OrganizationDetailsUpdated.cs` : IEventPayload — OrganizationDetails
    - [x] 6.4: `ContactChannelAdded.cs` : IEventPayload — ContactChannelId, Type, Value
    - [x] 6.5: `ContactChannelUpdated.cs` : IEventPayload — ContactChannelId, Type?, Value?
    - [x] 6.6: `ContactChannelRemoved.cs` : IEventPayload — ContactChannelId
    - [x] 6.7: `PreferredContactChannelChanged.cs` : IEventPayload — ContactChannelId
    - [x] 6.8: `IdentifierAdded.cs` : IEventPayload — IdentifierId, Type, Value
    - [x] 6.9: `IdentifierRemoved.cs` : IEventPayload — IdentifierId
    - [x] 6.10: `IsNaturalPersonChanged.cs` : IEventPayload — IsNaturalPerson
    - [x] 6.11: `PartyDeactivated.cs` : IEventPayload
    - [x] 6.12: `PartyReactivated.cs` : IEventPayload
    - [x] 6.13: `PartyDisplayNameDerived.cs` : IEventPayload — DisplayName, SortName
    - [x] 6.14: `PartyMerged.cs` : IEventPayload — v2 placeholder (SurvivorPartyId, MergedPartyId)
- [x] Task 7: Create rejection events in `Events/` folder (AC: #5)
    - [x] 7.1: `PartyCannotBeCreatedWithoutType.cs` : IRejectionEvent
    - [x] 7.2: `PartyCannotAddDuplicateChannel.cs` : IRejectionEvent
    - [x] 7.3: `PartyCannotAddDuplicateIdentifier.cs` : IRejectionEvent
    - [x] 7.4: `PartyNotFound.cs` : IRejectionEvent
    - [x] 7.5: `PartyTypeMismatch.cs` : IRejectionEvent
    - [x] 7.6: `PartyCannotBeDeactivatedWhenInactive.cs` : IRejectionEvent
    - [x] 7.7: `PartyCannotBeReactivatedWhenActive.cs` : IRejectionEvent
    - [x] 7.8: `ContactChannelNotFound.cs` : IRejectionEvent
    - [x] 7.9: `IdentifierNotFound.cs` : IRejectionEvent
    - [x] 7.10: `CompositeOperationConflict.cs` : IRejectionEvent
- [x] Task 8: Create `PartyState` in `State/` folder (AC: #8)
    - [x] 8.1: `PartyState.cs` — sealed class with all properties and Apply methods for ALL event types
- [x] Task 9: Create query models in `Models/` folder (AC: #9)
    - [x] 9.1: `PartyDetail.cs` — full party view query result
    - [x] 9.2: `PartyIndexEntry.cs` — lightweight summary with CreatedAt, LastModifiedAt
- [x] Task 10: Create `CompositeCommandResult` in `Results/` folder (AC: #10)
    - [x] 10.1: `CompositeCommandResult.cs` — extends DomainResult with Applied/Skipped/Rejected
- [x] Task 11: Verify build and tests (AC: #13, #15)
    - [x] 11.1: Run `dotnet restore Hexalith.Parties.slnx` — zero errors
    - [x] 11.2: Run `dotnet build Hexalith.Parties.slnx` — zero errors
    - [x] 11.3: Run `dotnet test` on all test projects — zero failures (adding ProjectReference must not break existing test resolution)

## Dev Notes

### CRITICAL: EventStore.Contracts Dependency Strategy

**The Hexalith.EventStore.Contracts NuGet package is NOT yet published on NuGet.org.** Use a ProjectReference to the submodule:

```xml
<!-- In src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\Hexalith.EventStore\src\Hexalith.EventStore.Contracts\Hexalith.EventStore.Contracts.csproj" />
  </ItemGroup>
</Project>
```

**Why ProjectReference:** EventStore.Contracts provides `IEventPayload`, `IRejectionEvent`, and `DomainResult` — required base types. The NuGet package does not exist on any feed yet. When published, replace ProjectReference with PackageReference.

**FR33 compliance:** EventStore.Contracts itself has zero runtime dependencies (empty .csproj, just SDK). So the Parties Contracts transitive dependency chain remains clean — no Dapr, MediatR, or ASP.NET Core pulled in.

### Custom [PersonalData] Attribute

The ASP.NET Core `[PersonalData]` attribute lives in `Microsoft.AspNetCore.Identity` — adding that dependency would violate FR33. Define a custom attribute:

```csharp
namespace Hexalith.Parties.Contracts;

/// <summary>
/// Marks a property as containing personal data subject to GDPR protections.
/// Used by crypto-shredding infrastructure (v1.1) and log sanitization (MVP).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class PersonalDataAttribute : Attribute { }
```

Place at: `src/Hexalith.Parties.Contracts/PersonalDataAttribute.cs`

### Architecture Patterns and Constraints

**Type Declaration Rules (from architecture enforcement guidelines):**

| Type | Pattern | Naming | Location |
|------|---------|--------|----------|
| Commands | `sealed record` with `{ get; init; }` | imperative verb + entity (no suffix) | `Commands/` |
| Events | `sealed record : IEventPayload` with `{ get; init; }` | entity + past participle (no suffix) | `Events/` |
| Rejections | `sealed record : IRejectionEvent` | entity + `Cannot` + reason | `Events/` |
| Value Objects | `sealed record` with `{ get; init; }` | descriptive noun (no suffix) | `ValueObjects/` |
| Enums | `enum` | type name | `ValueObjects/` |
| State | `sealed class` with `{ get; private set; }` | `{Domain}State` | `State/` |
| Models | `sealed record` with `{ get; init; }` | descriptive noun | `Models/` |
| Results | `sealed record` extending `DomainResult` | descriptive noun | `Results/` |

**FORBIDDEN — Anti-Patterns:**
1. **Positional record parameters** — breaks binary compat on reorder. Use `{ get; init; }` always
2. **V2 event types** (`PartyCreatedV2`) — use additive optional properties instead
3. **Command suffix** (`CreatePartyCommand`) — naming is just `CreateParty`
4. **Event suffix** (`PartyCreatedEvent`) — naming is just `PartyCreated`
5. **TenantId on command records** — extracted from request context, never on commands
6. **Multiple public types per file** — one type per file, file name = type name
7. **Task<DomainResult> return from aggregate Handle** — all Handle methods are synchronous
8. **PartyId/AggregateId on event payloads** — aggregate identity lives in the EventEnvelope metadata, NEVER on event records. Do NOT "helpfully" add PartyId to events

**Namespace Convention:** `Hexalith.Parties.Contracts.{SubFolder}` — folders match namespaces.
- Commands: `Hexalith.Parties.Contracts.Commands`
- Events: `Hexalith.Parties.Contracts.Events`
- ValueObjects: `Hexalith.Parties.Contracts.ValueObjects`
- State: `Hexalith.Parties.Contracts.State`
- Models: `Hexalith.Parties.Contracts.Models`
- Results: `Hexalith.Parties.Contracts.Results`

### [PersonalData] Attribute Placement (D6)

**Person parties — all PII encrypted:**
- `PersonDetails.FirstName` → `[PersonalData]`
- `PersonDetails.LastName` → `[PersonalData]`
- `PersonDetails.DateOfBirth` → `[PersonalData]`
- `PersonDetails.Prefix` → `[PersonalData]`
- `PersonDetails.Suffix` → `[PersonalData]`

**Organization parties — entity fields NOT encrypted, contact data IS:**
- `OrganizationDetails.LegalName` → NO attribute (entity data, not personal)
- `OrganizationDetails.TradingName` → NO attribute
- `OrganizationDetails.LegalForm` → NO attribute
- `OrganizationDetails.RegistrationNumber` → NO attribute

**All party types — always encrypted:**
- `ContactChannel.Value` → `[PersonalData]`
- `PartyIdentifier.Value` → `[PersonalData]`
- `PostalAddress` (all fields) → `[PersonalData]`
- `EmailAddress.Address` → `[PersonalData]`
- `PhoneNumber.Number` → `[PersonalData]`
- `SocialMediaHandle.Handle` → `[PersonalData]`

**Derived fields:**
- `PartyState.DisplayName` → `[PersonalData]`
- `PartyState.SortName` → `[PersonalData]`
- `PartyDetail.DisplayName` → `[PersonalData]`
- `PartyDetail.SortName` → `[PersonalData]`

**IsNaturalPerson escalation:** When `OrganizationDetails.IsNaturalPerson = true`, the org gets person-level encryption scope at v1.1. The attribute is not on `IsNaturalPerson` itself — it's a flag that changes the encryption policy.

### Exact Type Definitions

#### Commands — Detailed Property Specifications

```csharp
// Simple party creation (single command, no channels/identifiers)
public sealed record CreateParty
{
    public required string PartyId { get; init; }
    public required PartyType Type { get; init; }
    public PersonDetails? PersonDetails { get; init; }
    public OrganizationDetails? OrganizationDetails { get; init; }
}

// Composite creation (party + channels + identifiers in single actor turn)
public sealed record CreatePartyComposite
{
    public required string PartyId { get; init; }
    public required PartyType Type { get; init; }
    public PersonDetails? PersonDetails { get; init; }
    public OrganizationDetails? OrganizationDetails { get; init; }
    public IReadOnlyList<AddContactChannel> ContactChannels { get; init; } = [];
    public IReadOnlyList<AddIdentifier> Identifiers { get; init; } = [];
}

// Composite update with explicit add/update/remove lists (D9)
public sealed record UpdatePartyComposite
{
    public required string PartyId { get; init; }
    public PersonDetails? PersonDetails { get; init; }          // present = replace, absent = no change
    public OrganizationDetails? OrganizationDetails { get; init; }
    public IReadOnlyList<AddContactChannel> AddContactChannels { get; init; } = [];
    public IReadOnlyList<UpdateContactChannel> UpdateContactChannels { get; init; } = [];
    public IReadOnlyList<string> RemoveContactChannelIds { get; init; } = [];
    public IReadOnlyList<AddIdentifier> AddIdentifiers { get; init; } = [];
    public IReadOnlyList<string> RemoveIdentifierIds { get; init; } = [];
}

// Simple single-concern commands
public sealed record UpdatePersonDetails
{
    public required string PartyId { get; init; }
    public required PersonDetails PersonDetails { get; init; }
}

public sealed record UpdateOrganizationDetails
{
    public required string PartyId { get; init; }
    public required OrganizationDetails OrganizationDetails { get; init; }
}

public sealed record SetIsNaturalPerson
{
    public required string PartyId { get; init; }
    public required bool IsNaturalPerson { get; init; }
}

public sealed record AddContactChannel
{
    public required string PartyId { get; init; }
    public required string ContactChannelId { get; init; }   // Client-generated UUID
    public required ContactChannelType Type { get; init; }
    public required string Value { get; init; }
    public bool IsPreferred { get; init; }
}

public sealed record UpdateContactChannel
{
    public required string PartyId { get; init; }
    public required string ContactChannelId { get; init; }
    public ContactChannelType? Type { get; init; }
    public string? Value { get; init; }
    public bool? IsPreferred { get; init; }
}

public sealed record RemoveContactChannel
{
    public required string PartyId { get; init; }
    public required string ContactChannelId { get; init; }
}

public sealed record AddIdentifier
{
    public required string PartyId { get; init; }
    public required string IdentifierId { get; init; }       // Client-generated UUID
    public required IdentifierType Type { get; init; }
    public required string Value { get; init; }
}

public sealed record RemoveIdentifier
{
    public required string PartyId { get; init; }
    public required string IdentifierId { get; init; }
}

public sealed record DeactivateParty
{
    public required string PartyId { get; init; }
}

public sealed record ReactivateParty
{
    public required string PartyId { get; init; }
}
```

#### Events — All events implement IEventPayload

Events echo the data that was applied. Events do NOT carry PartyId — that's the aggregate identity in the envelope metadata.

```csharp
public sealed record PartyCreated : IEventPayload
{
    public required PartyType Type { get; init; }
    public PersonDetails? PersonDetails { get; init; }
    public OrganizationDetails? OrganizationDetails { get; init; }
}

public sealed record PersonDetailsUpdated : IEventPayload
{
    public required PersonDetails PersonDetails { get; init; }
}

public sealed record OrganizationDetailsUpdated : IEventPayload
{
    public required OrganizationDetails OrganizationDetails { get; init; }
}

public sealed record ContactChannelAdded : IEventPayload
{
    public required string ContactChannelId { get; init; }
    public required ContactChannelType Type { get; init; }
    public required string Value { get; init; }
    public bool IsPreferred { get; init; }
}

public sealed record ContactChannelUpdated : IEventPayload
{
    public required string ContactChannelId { get; init; }
    public ContactChannelType? Type { get; init; }
    public string? Value { get; init; }
    public bool? IsPreferred { get; init; }
}

public sealed record ContactChannelRemoved : IEventPayload
{
    public required string ContactChannelId { get; init; }
}

public sealed record PreferredContactChannelChanged : IEventPayload
{
    public required string ContactChannelId { get; init; }
}

public sealed record IdentifierAdded : IEventPayload
{
    public required string IdentifierId { get; init; }
    public required IdentifierType Type { get; init; }
    public required string Value { get; init; }
}

public sealed record IdentifierRemoved : IEventPayload
{
    public required string IdentifierId { get; init; }
}

public sealed record IsNaturalPersonChanged : IEventPayload
{
    public required bool IsNaturalPerson { get; init; }
}

public sealed record PartyDeactivated : IEventPayload;

public sealed record PartyReactivated : IEventPayload;

public sealed record PartyDisplayNameDerived : IEventPayload
{
    public required string DisplayName { get; init; }
    public required string SortName { get; init; }
}

// v2 forward-compatibility placeholder (FR37) — additive fields only when activated
public sealed record PartyMerged : IEventPayload
{
    public required string SurvivorPartyId { get; init; }
    public required string MergedPartyId { get; init; }
}
```

#### Rejection Events — Implement IRejectionEvent

Rejection events that need diagnostic context carry an optional `Message` property (FR30: "human-readable message and corrective action"). Simple state-guard rejections remain parameterless.

```csharp
// Parameterless — the type name alone communicates the rejection
public sealed record PartyCannotBeCreatedWithoutType : IRejectionEvent;
public sealed record PartyCannotAddDuplicateChannel : IRejectionEvent;
public sealed record PartyCannotAddDuplicateIdentifier : IRejectionEvent;
public sealed record PartyCannotBeDeactivatedWhenInactive : IRejectionEvent;
public sealed record PartyCannotBeReactivatedWhenActive : IRejectionEvent;

// Diagnostic — carry optional Message for caller context
public sealed record PartyNotFound : IRejectionEvent
{
    public string? Message { get; init; }
}

public sealed record PartyTypeMismatch : IRejectionEvent
{
    public string? Message { get; init; }
}

public sealed record ContactChannelNotFound : IRejectionEvent
{
    public string? Message { get; init; }
}

public sealed record IdentifierNotFound : IRejectionEvent
{
    public string? Message { get; init; }
}

public sealed record CompositeOperationConflict : IRejectionEvent
{
    public string? Message { get; init; }
}
```

#### Value Objects

```csharp
public sealed record PersonDetails
{
    [PersonalData] public required string FirstName { get; init; }
    [PersonalData] public required string LastName { get; init; }
    [PersonalData] public DateTimeOffset? DateOfBirth { get; init; }
    [PersonalData] public string? Prefix { get; init; }
    [PersonalData] public string? Suffix { get; init; }
}

public sealed record OrganizationDetails
{
    public required string LegalName { get; init; }
    public string? TradingName { get; init; }
    public string? LegalForm { get; init; }
    public string? RegistrationNumber { get; init; }
    public bool IsNaturalPerson { get; init; }
}

public sealed record ContactChannel
{
    public required string Id { get; init; }
    public required ContactChannelType Type { get; init; }
    [PersonalData] public required string Value { get; init; }
    public bool IsPreferred { get; init; }
}

public sealed record PartyIdentifier
{
    public required string Id { get; init; }
    public required IdentifierType Type { get; init; }
    [PersonalData] public required string Value { get; init; }
    public string? Jurisdiction { get; init; }
}

public sealed record PostalAddress
{
    [PersonalData] public string? Street { get; init; }
    [PersonalData] public string? City { get; init; }
    [PersonalData] public string? Region { get; init; }
    [PersonalData] public string? PostalCode { get; init; }
    [PersonalData] public string? Country { get; init; }
}

public sealed record EmailAddress
{
    [PersonalData] public required string Address { get; init; }
}

public sealed record PhoneNumber
{
    [PersonalData] public required string Number { get; init; }
    public string? CountryCode { get; init; }
}

public sealed record SocialMediaHandle
{
    public required string Platform { get; init; }
    [PersonalData] public required string Handle { get; init; }
}
```

**ContactChannel.Value vs Typed Value Objects — Design Intent:**
`ContactChannel.Value` is a **flat string representation** (e.g., `"john@example.com"`, `"+33612345678"`, `"123 Rue de Paris, 75001 Paris"`). The typed value objects (`PostalAddress`, `EmailAddress`, `PhoneNumber`, `SocialMediaHandle`) are **forward-compatible contracts** for future stories where structured data is needed (API validation in Story 1.6, MCP input normalization in Epic 5). They are NOT wired to `ContactChannel` in this story. Do NOT attempt to embed typed VOs inside `ContactChannel` — that coupling is a future design decision.

#### Enums

```csharp
public enum PartyType
{
    Person,
    Organization
}

public enum ContactChannelType
{
    Email,
    Phone,
    PostalAddress,
    SocialMedia
}

public enum IdentifierType
{
    VAT,
    SIRET,
    NationalId,
    CompanyRegistration,
    TaxId,
    Other
}
```

#### PartyState — Sealed class with Apply methods

```csharp
public sealed class PartyState
{
    public PartyType Type { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsNaturalPerson { get; private set; }
    [PersonalData] public string DisplayName { get; private set; } = string.Empty;
    [PersonalData] public string SortName { get; private set; } = string.Empty;
    public PersonDetails? Person { get; private set; }
    public OrganizationDetails? Organization { get; private set; }

    private readonly List<ContactChannel> _contactChannels = [];
    public IReadOnlyList<ContactChannel> ContactChannels => _contactChannels;

    private readonly List<PartyIdentifier> _identifiers = [];
    public IReadOnlyList<PartyIdentifier> Identifiers => _identifiers;

    // Apply methods for EVERY event type — one per event
    public void Apply(PartyCreated e) { ... }
    public void Apply(PersonDetailsUpdated e) { ... }
    public void Apply(OrganizationDetailsUpdated e) { ... }
    public void Apply(ContactChannelAdded e) { ... }
    public void Apply(ContactChannelUpdated e) { ... }
    public void Apply(ContactChannelRemoved e) { ... }
    public void Apply(PreferredContactChannelChanged e) { ... }
    public void Apply(IdentifierAdded e) { ... }
    public void Apply(IdentifierRemoved e) { ... }
    public void Apply(IsNaturalPersonChanged e) { ... }
    public void Apply(PartyDeactivated e) { ... }
    public void Apply(PartyReactivated e) { ... }
    public void Apply(PartyDisplayNameDerived e) { ... }
    public void Apply(PartyMerged e) { /* no-op placeholder for v2 */ }
}
```

**Apply method implementation rules:**
- `Apply(PartyCreated e)` → set Type, Person/Organization details. Copy `IsNaturalPerson` from `e.OrganizationDetails?.IsNaturalPerson ?? false` to `PartyState.IsNaturalPerson`. Does NOT set `IsActive` — the property initializer default of `true` applies (new parties are active by default)
- `Apply(ContactChannelAdded e)` → add to `_contactChannels` list. Does NOT clear other channels' `IsPreferred` — the aggregate (Story 1.3+) is responsible for emitting a separate `PreferredContactChannelChanged` event if preferred status needs coordination
- `Apply(ContactChannelUpdated e)` → find by Id in `_contactChannels`, merge non-null fields using `with` expression on the immutable record:
  ```csharp
  var idx = _contactChannels.FindIndex(c => c.Id == e.ContactChannelId);
  if (idx >= 0)
  {
      var existing = _contactChannels[idx];
      _contactChannels[idx] = existing with
      {
          Type = e.Type ?? existing.Type,
          Value = e.Value ?? existing.Value,
          IsPreferred = e.IsPreferred ?? existing.IsPreferred,
      };
  }
  ```
- `Apply(ContactChannelRemoved e)` → remove from `_contactChannels` by Id
- `Apply(PreferredContactChannelChanged e)` → set `IsPreferred = true` on matching channel by Id, set `IsPreferred = false` on ALL other channels. This is the only Apply method that coordinates across multiple channels
- `Apply(IdentifierAdded e)` → add to `_identifiers` list
- `Apply(IdentifierRemoved e)` → remove from `_identifiers` by Id
- `Apply(PartyDeactivated e)` → set `IsActive = false`
- `Apply(PartyReactivated e)` → set `IsActive = true`
- `Apply(PartyDisplayNameDerived e)` → set DisplayName and SortName
- `Apply(IsNaturalPersonChanged e)` → set `PartyState.IsNaturalPerson = e.IsNaturalPerson` (updates the state-level flag independently of `OrganizationDetails.IsNaturalPerson` which is the original input value)

#### Query Models

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

public sealed record PartyIndexEntry
{
    public required string Id { get; init; }
    public required PartyType Type { get; init; }
    public bool IsActive { get; init; }
    [PersonalData] public required string DisplayName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastModifiedAt { get; init; }
}
```

#### CompositeCommandResult

`DomainResult` is a `record` with constructor `DomainResult(IReadOnlyList<IEventPayload> events)`. Extending it requires passing events to the base constructor:

```csharp
/// <summary>
/// Extends DomainResult with per-sub-operation outcome tracking for composite commands.
/// Applied/Skipped/Rejected contain human-readable descriptions of each sub-operation outcome.
/// </summary>
public sealed record CompositeCommandResult : DomainResult
{
    public CompositeCommandResult(
        IReadOnlyList<IEventPayload> events,
        IReadOnlyList<string> applied,
        IReadOnlyList<string> skipped,
        IReadOnlyList<string> rejected)
        : base(events)
    {
        Applied = applied;
        Skipped = skipped;
        Rejected = rejected;
    }

    /// <summary>Gets descriptions of sub-operations that were successfully applied.</summary>
    public IReadOnlyList<string> Applied { get; }

    /// <summary>Gets descriptions of sub-operations that were skipped (e.g., duplicate additions).</summary>
    public IReadOnlyList<string> Skipped { get; }

    /// <summary>Gets descriptions of sub-operations that were rejected (e.g., invalid IDs).</summary>
    public IReadOnlyList<string> Rejected { get; }
}
```

**Constructor notes:** The base `DomainResult(events)` validates that events don't mix regular and rejection types. For a successful composite, pass all emitted events + Applied/Skipped lists. For a rejected composite, pass rejection events + the Rejected list. The `Skipped` list is for idempotent duplicate detection (D10).

### EventStore Convention Reference

**IEventPayload and IRejectionEvent** are marker interfaces from `Hexalith.EventStore.Contracts.Events`:
```csharp
public interface IEventPayload;
public interface IRejectionEvent : IEventPayload;
```

**DomainResult** is from `Hexalith.EventStore.Contracts.Results`:
```csharp
DomainResult.Success(new IEventPayload[] { ... });
DomainResult.Rejection(new IRejectionEvent[] { ... });
DomainResult.NoOp();
```

**EventStore reflection discovery:**
- Apply methods discovered by name "Apply" and parameter type
- Handle methods discovered by name "Handle" and `(CommandType, TState?)` signature
- Both are convention-based — no attributes or registration needed

### Contracts Project File Structure

```
src/Hexalith.Parties.Contracts/
├── Hexalith.Parties.Contracts.csproj    # ProjectReference to EventStore.Contracts
├── PersonalDataAttribute.cs             # Custom [PersonalData] attribute
├── Commands/
│   ├── CreateParty.cs
│   ├── CreatePartyComposite.cs
│   ├── UpdatePartyComposite.cs
│   ├── UpdatePersonDetails.cs
│   ├── UpdateOrganizationDetails.cs
│   ├── SetIsNaturalPerson.cs
│   ├── AddContactChannel.cs
│   ├── UpdateContactChannel.cs
│   ├── RemoveContactChannel.cs
│   ├── AddIdentifier.cs
│   ├── RemoveIdentifier.cs
│   ├── DeactivateParty.cs
│   └── ReactivateParty.cs
├── Events/
│   ├── PartyCreated.cs
│   ├── PersonDetailsUpdated.cs
│   ├── OrganizationDetailsUpdated.cs
│   ├── ContactChannelAdded.cs
│   ├── ContactChannelUpdated.cs
│   ├── ContactChannelRemoved.cs
│   ├── PreferredContactChannelChanged.cs
│   ├── IdentifierAdded.cs
│   ├── IdentifierRemoved.cs
│   ├── IsNaturalPersonChanged.cs
│   ├── PartyDeactivated.cs
│   ├── PartyReactivated.cs
│   ├── PartyDisplayNameDerived.cs
│   ├── PartyMerged.cs
│   ├── PartyCannotBeCreatedWithoutType.cs
│   ├── PartyCannotAddDuplicateChannel.cs
│   ├── PartyCannotAddDuplicateIdentifier.cs
│   ├── PartyNotFound.cs
│   ├── PartyTypeMismatch.cs
│   ├── PartyCannotBeDeactivatedWhenInactive.cs
│   ├── PartyCannotBeReactivatedWhenActive.cs
│   ├── ContactChannelNotFound.cs
│   ├── IdentifierNotFound.cs
│   └── CompositeOperationConflict.cs
├── State/
│   └── PartyState.cs
├── ValueObjects/
│   ├── PartyType.cs
│   ├── ContactChannelType.cs
│   ├── IdentifierType.cs
│   ├── PersonDetails.cs
│   ├── OrganizationDetails.cs
│   ├── ContactChannel.cs
│   ├── PartyIdentifier.cs
│   ├── PostalAddress.cs
│   ├── EmailAddress.cs
│   ├── PhoneNumber.cs
│   └── SocialMediaHandle.cs
├── Models/
│   ├── PartyDetail.cs
│   └── PartyIndexEntry.cs
└── Results/
    └── CompositeCommandResult.cs
```

### Previous Story Intelligence (Story 1.1)

**Key learnings from Story 1.1:**
- All build configuration is in place — `Directory.Build.props` (net10.0, nullable, TreatWarningsAsErrors) and `Directory.Packages.props` (central package management)
- The .slnx solution file already includes the Contracts project
- EventStore submodule is at commit `6b9ddd8` — use as reference implementation for patterns
- The Contracts .csproj is currently empty (just SDK reference) — ready for the ProjectReference addition
- `dotnet restore` and `dotnet build` pass on the current solution — preserve this
- Story 1.1 explicitly deferred EventStore package references to Story 1.2+
- ASPIRE004 warning exists (CommandApi not yet executable) — expected, ignore
- The `.editorconfig` enforces Allman braces, file-scoped namespaces, 4-space indentation

**Code review findings from Story 1.1:**
- Strict compliance with ACs is critical — code review caught `global.json` version mismatch
- `.gitignore` merge was incomplete initially — thoroughness matters
- Dev Agent Record section must be accurate

### Git Intelligence

**Recent commit pattern:** Single large commit per story containing all scaffolding changes. Build verified before marking complete.

**Convention established:** All projects use the SDK-style .csproj format with minimal content, inheriting most settings from Directory.Build.props.

### Project Structure Notes

- All types go in `src/Hexalith.Parties.Contracts/` — this is the shared contracts package
- No types go in Server, Client, or other projects in this story
- The Contracts project is consumed by ALL other projects — its types are the domain language
- This story creates NO .cs files outside the Contracts project

### ANTI-PATTERNS TO AVOID

1. **Do NOT use positional record parameters** — use `{ get; init; }` properties only
2. **Do NOT add TenantId to command records** — extracted from request context
3. **Do NOT use `V2` event types** — use additive optional properties for evolution
4. **Do NOT add ASP.NET Core or Dapr dependencies to Contracts** — only EventStore.Contracts
5. **Do NOT add source files outside the Contracts project** — this story is contracts only
6. **Do NOT use `System.ComponentModel.DataAnnotations.PersonalDataAttribute`** — it doesn't exist there; use custom attribute
7. **Do NOT make PartyState a record** — it must be a sealed class (mutable via Apply)
8. **Do NOT use `IList<T>` or `List<T>` in public record properties** — use `IReadOnlyList<T>`
9. **Do NOT add Jurisdiction property on commands** — only on the PartyIdentifier value object
10. **Do NOT create abstract base classes for commands or events** — each type is standalone
11. **Do NOT reference `Microsoft.AspNetCore.Identity` for `[PersonalData]`** — use custom attribute defined in this project
12. **Do NOT add PartyId to event records** — aggregate identity is in EventEnvelope metadata, not on event payloads. This is an EventStore convention. Commands carry PartyId (to route to the aggregate), events do NOT

### References

- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Events/IEventPayload.cs] — marker interface for events
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Events/IRejectionEvent.cs] — marker interface for rejection events
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Results/DomainResult.cs] — base result type for Handle methods
- [Source: Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs] — reference for sealed class state with Apply methods
- [Source: Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs] — reference for Handle method signatures
- [Source: _bmad-output/planning-artifacts/architecture.md#Type-Declaration-Patterns] — command/event/state naming and structure rules
- [Source: _bmad-output/planning-artifacts/architecture.md#Namespace-Project-Organization] — folder structure and namespace conventions
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement-Guidelines] — 14 coding rules + 8 anti-patterns
- [Source: _bmad-output/planning-artifacts/architecture.md#D6] — PersonalData scope decision (type-dependent)
- [Source: _bmad-output/planning-artifacts/architecture.md#D8] — Composite aggregate command strategy
- [Source: _bmad-output/planning-artifacts/architecture.md#D9] — UpdatePartyComposite explicit add/update/remove lists
- [Source: _bmad-output/planning-artifacts/architecture.md#D10] — Sub-operation idempotency and conflict detection
- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.2] — Acceptance criteria and BDD scenarios

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- CA1062 errors (null parameter checks) on PartyState Apply methods — fixed by adding `ArgumentNullException.ThrowIfNull(e)` to all Apply methods
- CA1822 error on `Apply(PartyMerged)` no-op method — suppressed with pragma since method must be instance for EventStore convention discovery

### Completion Notes List

- Task 1: Added ProjectReference to `Hexalith.EventStore.Contracts` in Contracts .csproj; verified TFM compatibility (net10.0) and restore
- Task 2: Created custom `[PersonalData]` attribute at project root to avoid ASP.NET Core Identity dependency
- Task 3: Created 3 enums (`PartyType`, `ContactChannelType`, `IdentifierType`) in `ValueObjects/`
- Task 4: Created 8 value objects (`PostalAddress`, `EmailAddress`, `PhoneNumber`, `SocialMediaHandle`, `PersonDetails`, `OrganizationDetails`, `ContactChannel`, `PartyIdentifier`) with `[PersonalData]` attributes per D6 spec
- Task 5: Created 13 command types in `Commands/` — all sealed records with `{ get; init; }`, no positional params, no TenantId
- Task 6: Created 14 event types in `Events/` implementing `IEventPayload` — no PartyId on events per convention
- Task 7: Created 10 rejection events in `Events/` implementing `IRejectionEvent` — parameterless for state-guards, with optional Message for diagnostic rejections
- Task 8: Created `PartyState` sealed class with `{ get; private set; }` properties and Apply methods for all 14 event types
- Task 9: Created 2 query models (`PartyDetail`, `PartyIndexEntry`) in `Models/`
- Task 10: Created `CompositeCommandResult` extending `DomainResult` with Applied/Skipped/Rejected collections
- Task 11: Full solution builds with 0 errors; 21 unit tests pass (PartyState Apply methods + CompositeCommandResult)
- Code review fixes: Added `[PersonalData]` markers on command/event payload `Value` fields for contact channels and identifiers to align with AC #11
- Code review fixes: Corrected checklist markdown formatting and synchronized story metadata/status artifacts

### File List

**New files:**
- src/Hexalith.Parties.Contracts/PersonalDataAttribute.cs
- src/Hexalith.Parties.Contracts/Commands/CreateParty.cs
- src/Hexalith.Parties.Contracts/Commands/CreatePartyComposite.cs
- src/Hexalith.Parties.Contracts/Commands/UpdatePartyComposite.cs
- src/Hexalith.Parties.Contracts/Commands/UpdatePersonDetails.cs
- src/Hexalith.Parties.Contracts/Commands/UpdateOrganizationDetails.cs
- src/Hexalith.Parties.Contracts/Commands/SetIsNaturalPerson.cs
- src/Hexalith.Parties.Contracts/Commands/AddContactChannel.cs
- src/Hexalith.Parties.Contracts/Commands/UpdateContactChannel.cs
- src/Hexalith.Parties.Contracts/Commands/RemoveContactChannel.cs
- src/Hexalith.Parties.Contracts/Commands/AddIdentifier.cs
- src/Hexalith.Parties.Contracts/Commands/RemoveIdentifier.cs
- src/Hexalith.Parties.Contracts/Commands/DeactivateParty.cs
- src/Hexalith.Parties.Contracts/Commands/ReactivateParty.cs
- src/Hexalith.Parties.Contracts/Events/PartyCreated.cs
- src/Hexalith.Parties.Contracts/Events/PersonDetailsUpdated.cs
- src/Hexalith.Parties.Contracts/Events/OrganizationDetailsUpdated.cs
- src/Hexalith.Parties.Contracts/Events/ContactChannelAdded.cs
- src/Hexalith.Parties.Contracts/Events/ContactChannelUpdated.cs
- src/Hexalith.Parties.Contracts/Events/ContactChannelRemoved.cs
- src/Hexalith.Parties.Contracts/Events/PreferredContactChannelChanged.cs
- src/Hexalith.Parties.Contracts/Events/IdentifierAdded.cs
- src/Hexalith.Parties.Contracts/Events/IdentifierRemoved.cs
- src/Hexalith.Parties.Contracts/Events/IsNaturalPersonChanged.cs
- src/Hexalith.Parties.Contracts/Events/PartyDeactivated.cs
- src/Hexalith.Parties.Contracts/Events/PartyReactivated.cs
- src/Hexalith.Parties.Contracts/Events/PartyDisplayNameDerived.cs
- src/Hexalith.Parties.Contracts/Events/PartyMerged.cs
- src/Hexalith.Parties.Contracts/Events/PartyCannotBeCreatedWithoutType.cs
- src/Hexalith.Parties.Contracts/Events/PartyCannotAddDuplicateChannel.cs
- src/Hexalith.Parties.Contracts/Events/PartyCannotAddDuplicateIdentifier.cs
- src/Hexalith.Parties.Contracts/Events/PartyNotFound.cs
- src/Hexalith.Parties.Contracts/Events/PartyTypeMismatch.cs
- src/Hexalith.Parties.Contracts/Events/PartyCannotBeDeactivatedWhenInactive.cs
- src/Hexalith.Parties.Contracts/Events/PartyCannotBeReactivatedWhenActive.cs
- src/Hexalith.Parties.Contracts/Events/ContactChannelNotFound.cs
- src/Hexalith.Parties.Contracts/Events/IdentifierNotFound.cs
- src/Hexalith.Parties.Contracts/Events/CompositeOperationConflict.cs
- src/Hexalith.Parties.Contracts/ValueObjects/PartyType.cs
- src/Hexalith.Parties.Contracts/ValueObjects/ContactChannelType.cs
- src/Hexalith.Parties.Contracts/ValueObjects/IdentifierType.cs
- src/Hexalith.Parties.Contracts/ValueObjects/PostalAddress.cs
- src/Hexalith.Parties.Contracts/ValueObjects/EmailAddress.cs
- src/Hexalith.Parties.Contracts/ValueObjects/PhoneNumber.cs
- src/Hexalith.Parties.Contracts/ValueObjects/SocialMediaHandle.cs
- src/Hexalith.Parties.Contracts/ValueObjects/PersonDetails.cs
- src/Hexalith.Parties.Contracts/ValueObjects/OrganizationDetails.cs
- src/Hexalith.Parties.Contracts/ValueObjects/ContactChannel.cs
- src/Hexalith.Parties.Contracts/ValueObjects/PartyIdentifier.cs
- src/Hexalith.Parties.Contracts/State/PartyState.cs
- src/Hexalith.Parties.Contracts/Models/PartyDetail.cs
- src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs
- src/Hexalith.Parties.Contracts/Results/CompositeCommandResult.cs
- tests/Hexalith.Parties.Contracts.Tests/State/PartyStateTests.cs
- tests/Hexalith.Parties.Contracts.Tests/Results/CompositeCommandResultTests.cs

**Modified files:**
- src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj
- _bmad-output/implementation-artifacts/1-2-domain-contracts-complete-type-definitions.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

### Change Log

- 2026-03-04: Implemented all domain contract types for the Parties bounded context — 13 commands, 14 events, 10 rejection events, 8 value objects, 3 enums, PartyState sealed class, 2 query models, CompositeCommandResult. Added 21 unit tests for PartyState Apply methods and CompositeCommandResult.
- 2026-03-04: Senior review remediation — added missing `[PersonalData]` markers on command/event payload value fields, corrected Tasks/Subtasks markdown formatting, and synchronized story/sprint status to `done`.

## Senior Developer Review (AI)

### Reviewer

GitHub Copilot (GPT-5.3-Codex)

### Date

2026-03-04

### Outcome

Approved after fixes

### Findings Resolved

- High: AC #11 alignment fixed by marking contact channel/identifier payload value fields with `[PersonalData]` in command/event contracts.
- Medium: Story task checklist formatting corrected for markdown-compliant checkboxes.
- Medium: Story file list updated to include artifact files modified during implementation/review.
- Medium: Review record appended to story and status synchronized.
