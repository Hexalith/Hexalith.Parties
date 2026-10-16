using System.Text.Json.Serialization;

using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

public sealed record AddIdentifier
{
    public required string PartyId { get; init; }

    public required string IdentifierId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<IdentifierType>))]
    public required IdentifierType Type { get; init; }

    [PersonalData]
    public required string Value { get; init; }
}
