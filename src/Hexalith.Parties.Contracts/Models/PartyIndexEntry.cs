using System.Text.Json.Serialization;

using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Models;

public sealed record PartyIndexEntry
{
    public required string Id { get; init; }

    public required PartyType Type { get; init; }

    public bool IsActive { get; init; }

    [PersonalData]
    public required string DisplayName { get; init; }

    [JsonIgnore]
    public IReadOnlyList<ContactChannel> SearchableContactChannels { get; init; } = [];

    [JsonIgnore]
    public IReadOnlyList<PartyIdentifier> SearchableIdentifiers { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset LastModifiedAt { get; init; }
}
