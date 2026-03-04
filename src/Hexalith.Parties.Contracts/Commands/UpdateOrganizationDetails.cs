using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

public sealed record UpdateOrganizationDetails
{
    public required string PartyId { get; init; }

    public required OrganizationDetails OrganizationDetails { get; init; }
}
