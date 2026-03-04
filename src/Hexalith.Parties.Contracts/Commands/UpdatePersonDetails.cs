using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

public sealed record UpdatePersonDetails
{
    public required string PartyId { get; init; }

    public required PersonDetails PersonDetails { get; init; }
}
