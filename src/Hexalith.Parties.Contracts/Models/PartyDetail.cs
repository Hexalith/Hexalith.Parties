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

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset LastModifiedAt { get; init; }
}
