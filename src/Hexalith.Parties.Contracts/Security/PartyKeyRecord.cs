namespace Hexalith.Parties.Contracts.Security;

public sealed record PartyKeyRecord
{
    public required string TenantId { get; init; }

    public required string PartyId { get; init; }

    public required int Version { get; init; }

    public required string KeyPath { get; init; }
}
