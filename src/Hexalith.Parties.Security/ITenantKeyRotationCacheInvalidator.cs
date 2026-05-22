namespace Hexalith.Parties.Security;

public interface ITenantKeyRotationCacheInvalidator
{
    ValueTask InvalidatePartyAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);
}
