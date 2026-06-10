using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.ConsumerPortal.Services;

public sealed record ConsumerProfileUpdateRequest
{
    public PersonDetails? PersonDetails { get; init; }

    public OrganizationDetails? OrganizationDetails { get; init; }
}
