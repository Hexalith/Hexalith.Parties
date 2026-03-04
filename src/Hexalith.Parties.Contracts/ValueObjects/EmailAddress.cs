namespace Hexalith.Parties.Contracts.ValueObjects;

public sealed record EmailAddress
{
    [PersonalData]
    public required string Address { get; init; }
}
