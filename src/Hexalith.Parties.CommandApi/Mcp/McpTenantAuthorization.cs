using Hexalith.Parties.CommandApi.Authorization;

namespace Hexalith.Parties.CommandApi.Mcp;

internal static class McpTenantAuthorization {
    internal static async Task<McpTenantAccessContext> RequireAccessAsync(
        IServiceProvider services,
        TenantAccessRequirement requirement,
        CancellationToken cancellationToken) {
        string? tenant = McpSessionContext.Tenant.Value;
        string? userId = McpSessionContext.UserId.Value;

        if (string.IsNullOrWhiteSpace(tenant)) {
            throw McpTenantAuthorizationException.FromDecision(
                TenantAccessDecision.Denied(TenantAccessDenialReason.MissingTenantId));
        }

        if (string.IsNullOrWhiteSpace(userId)) {
            throw McpTenantAuthorizationException.FromDecision(
                TenantAccessDecision.Denied(TenantAccessDenialReason.MissingUserId));
        }

        ITenantAccessService accessService = services.GetRequiredService<ITenantAccessService>();
        TenantAccessDecision decision = await accessService
            .CheckAccessAsync(tenant, userId, requirement, cancellationToken)
            .ConfigureAwait(false);

        if (!decision.IsAllowed) {
            throw McpTenantAuthorizationException.FromDecision(decision);
        }

        return new McpTenantAccessContext(tenant!, userId!);
    }
}

internal sealed record McpTenantAccessContext(string TenantId, string UserId);
