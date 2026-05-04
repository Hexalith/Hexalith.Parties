namespace Hexalith.Parties.CommandApi.Authorization;

public interface ITenantAccessService {
    Task<TenantAccessDecision> CheckAccessAsync(
        string? tenantId,
        string? userId,
        TenantAccessRequirement requirement,
        CancellationToken cancellationToken = default);
}
