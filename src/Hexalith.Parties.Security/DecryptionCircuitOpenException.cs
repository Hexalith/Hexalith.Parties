namespace Hexalith.Parties.Security;

/// <summary>
/// Thrown when a decryption attempt is rejected because the per-party circuit breaker is open.
/// </summary>
public sealed class DecryptionCircuitOpenException : InvalidOperationException
{
    public DecryptionCircuitOpenException(string tenantId, string partyId, DateTimeOffset breakExpiry)
        : base($"Decryption circuit breaker is open for party {tenantId}/{partyId}. Retry after {breakExpiry:O}.")
    {
        TenantId = tenantId;
        PartyId = partyId;
        BreakExpiry = breakExpiry;
    }

    public string TenantId { get; }

    public string PartyId { get; }

    public DateTimeOffset BreakExpiry { get; }
}
