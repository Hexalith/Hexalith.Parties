using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Models;

public sealed record PartyIndexEntry
{
    public required string Id { get; init; }

    public required PartyType Type { get; init; }

    public bool IsActive { get; init; }

    [PersonalData]
    public required string DisplayName { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset LastModifiedAt { get; init; }
}
