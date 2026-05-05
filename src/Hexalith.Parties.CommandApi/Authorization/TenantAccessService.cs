using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Contracts.Enums;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.CommandApi.Authorization;

public sealed class TenantAccessService : ITenantAccessService {
    private readonly ITenantProjectionStore _projectionStore;
    private readonly ILogger<TenantAccessService> _logger;

    public TenantAccessService(
        ITenantProjectionStore projectionStore,
        ILogger<TenantAccessService> logger) {
        ArgumentNullException.ThrowIfNull(projectionStore);
        ArgumentNullException.ThrowIfNull(logger);
        _projectionStore = projectionStore;
        _logger = logger;
    }

    public async Task<TenantAccessDecision> CheckAccessAsync(
        string? tenantId,
        string? userId,
        TenantAccessRequirement requirement,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(tenantId)) {
            return TenantAccessDecision.Denied(TenantAccessDenialReason.MissingTenantId);
        }

        if (string.IsNullOrWhiteSpace(userId)) {
            return TenantAccessDecision.Denied(TenantAccessDenialReason.MissingUserId);
        }

        TenantLocalState? state;
        try {
            state = await _projectionStore.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            // Fail closed: classify as stale state, but log so operators can diagnose
            // a real projection-store outage instead of seeing only blanket 403s.
            // No claim sets, member dictionaries, or party PII in the log scope.
            _logger.LogError(
                ex,
                "Tenant access state lookup failed; failing closed. TenantId={TenantId}, Requirement={Requirement}",
                tenantId,
                requirement);
            return TenantAccessDecision.Denied(
                TenantAccessDenialReason.TenantStateStale,
                "Tenant access state is unavailable.");
        }

        if (state is null) {
            return TenantAccessDecision.Denied(TenantAccessDenialReason.UnknownTenant);
        }

        if (state.Status != TenantStatus.Active) {
            return TenantAccessDecision.Denied(TenantAccessDenialReason.DisabledTenant);
        }

        if (!state.Members.TryGetValue(userId, out TenantRole role)) {
            return TenantAccessDecision.Denied(TenantAccessDenialReason.MissingMember);
        }

        return HasPermission(role, requirement)
            ? TenantAccessDecision.Allowed
            : TenantAccessDecision.Denied(TenantAccessDenialReason.InsufficientRole);
    }

    private static bool HasPermission(TenantRole role, TenantAccessRequirement requirement)
        => role switch {
            TenantRole.TenantReader => requirement == TenantAccessRequirement.Read,
            TenantRole.TenantContributor => requirement is TenantAccessRequirement.Read or TenantAccessRequirement.Write,
            TenantRole.TenantOwner => requirement is TenantAccessRequirement.Read or TenantAccessRequirement.Write or TenantAccessRequirement.Admin,
            _ => false,
        };
}
