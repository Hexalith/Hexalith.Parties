namespace Hexalith.Parties.Authorization;

public interface ITenantAccessService {
    Task<TenantAccessDecision> CheckAccessAsync(
        string? tenantId,
        string? userId,
        TenantAccessRequirement requirement,
        CancellationToken cancellationToken = default);
}
