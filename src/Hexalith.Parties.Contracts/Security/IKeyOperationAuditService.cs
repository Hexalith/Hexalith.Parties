namespace Hexalith.Parties.Contracts.Security;

public interface IKeyOperationAuditService
{
    Task RecordOperationAsync(KeyOperationAuditEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KeyOperationAuditEntry>> GetAuditTrailAsync(string tenantId, string partyId, CancellationToken cancellationToken = default);
}
