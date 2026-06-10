using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.UI.Services;

public sealed record SelfScopedProfileUpdateRequest
{
    public PersonDetails? PersonDetails { get; init; }

    public OrganizationDetails? OrganizationDetails { get; init; }
}
