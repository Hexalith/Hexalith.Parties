using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Contracts.Enums;

namespace Hexalith.Parties.CommandApi.Authorization;

public sealed class TenantAccessService : ITenantAccessService {
    private readonly ITenantProjectionStore _projectionStore;

    public TenantAccessService(ITenantProjectionStore projectionStore) {
        ArgumentNullException.ThrowIfNull(projectionStore);
        _projectionStore = projectionStore;
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

        TenantLocalState? state = await _projectionStore.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
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
