using Hexalith.Parties.Authorization;

namespace Hexalith.Parties.Tests.Authorization;

internal sealed class TestTenantAccessService : ITenantAccessService {
    private static readonly Func<string?, string?, TenantAccessRequirement, CancellationToken, Task<TenantAccessDecision>> s_allowAll =
        (tenant, user, _, _) => Task.FromResult(
            string.IsNullOrWhiteSpace(tenant)
                ? TenantAccessDecision.Denied(TenantAccessDenialReason.MissingTenantId)
                : string.IsNullOrWhiteSpace(user)
                    ? TenantAccessDecision.Denied(TenantAccessDenialReason.MissingUserId)
                    : TenantAccessDecision.Allowed);

    public TestTenantAccessService()
        : this(s_allowAll) {
    }

    public TestTenantAccessService(
        Func<string?, string?, TenantAccessRequirement, CancellationToken, Task<TenantAccessDecision>> handler) {
        Handler = handler;
    }

    public Func<string?, string?, TenantAccessRequirement, CancellationToken, Task<TenantAccessDecision>> Handler { get; set; }

    public void AllowAll() => Handler = s_allowAll;

    public Task<TenantAccessDecision> CheckAccessAsync(
        string? tenantId,
        string? userId,
        TenantAccessRequirement requirement,
        CancellationToken cancellationToken = default)
        => Handler(tenantId, userId, requirement, cancellationToken);
}
