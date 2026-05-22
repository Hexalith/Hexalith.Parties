namespace Hexalith.Parties.Contracts.Security;

public interface ITenantKeyRotationService
{
    Task<TenantKeyRotationStatus> RotateAsync(TenantKeyRotationRequest request, CancellationToken cancellationToken = default);

    Task<TenantKeyRotationStatus?> GetStatusAsync(string tenantId, string operationId, CancellationToken cancellationToken = default);
}
