using System.Text.Json.Serialization;

using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

public sealed record CreateParty
{
    public required string PartyId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<PartyType>))]
    public required PartyType Type { get; init; }

    public PersonDetails? PersonDetails { get; init; }

    public OrganizationDetails? OrganizationDetails { get; init; }
}
