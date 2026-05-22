using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Models;

public sealed record PartyDetail
{
    public required string Id { get; init; }

    public required PartyType Type { get; init; }

    public bool IsActive { get; init; }

    [PersonalData]
    public required string DisplayName { get; init; }

    [PersonalData]
    public required string SortName { get; init; }

    public PersonDetails? PersonDetails { get; init; }

    public OrganizationDetails? OrganizationDetails { get; init; }

    public IReadOnlyList<ContactChannel> ContactChannels { get; init; } = [];

    public IReadOnlyList<PartyIdentifier> Identifiers { get; init; } = [];

    public IReadOnlyList<ConsentRecord> ConsentRecords { get; init; } = [];

    [PersonalData]
    public IReadOnlyList<NameHistoryEntry> NameHistory { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset LastModifiedAt { get; init; }

    public bool IsRestricted { get; init; }

    public DateTimeOffset? RestrictedAt { get; init; }

    public bool IsErased { get; init; }

    public DateTimeOffset? ErasedAt { get; init; }

    public ProjectionFreshnessMetadata? Freshness { get; init; }
}
