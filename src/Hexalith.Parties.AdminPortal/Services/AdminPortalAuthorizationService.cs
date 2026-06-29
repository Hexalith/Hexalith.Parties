using System.Security.Claims;

using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Contracts.Enums;

using Microsoft.AspNetCore.Components.Authorization;

namespace Hexalith.Parties.AdminPortal.Services;

public sealed class AdminPortalAuthorizationService(
    AuthenticationStateProvider authenticationStateProvider,
    ITenantProjectionStore tenantProjectionStore) : IAdminPortalAuthorizationService
{
    public async Task<AdminPortalAuthorizationState> GetAuthorizationStateAsync(CancellationToken cancellationToken = default)
    {
        AuthenticationState authenticationState = await authenticationStateProvider
            .GetAuthenticationStateAsync()
            .ConfigureAwait(false);
        ClaimsPrincipal principal = authenticationState.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return AdminPortalAuthorizationState.Unauthenticated;
        }

        string? tenantId = principal.TryGetTenantId().Value;
        string? userId = principal.TryGetUserId().Value;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return new(true, false, false, "authenticated:no-tenant");
        }

        TenantLocalState? tenant = await tenantProjectionStore
            .GetAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null || tenant.Status != TenantStatus.Active)
        {
            return new(true, false, false, $"tenant:{tenantId}:unavailable");
        }

        bool isAdmin = !string.IsNullOrWhiteSpace(userId)
            && tenant.Members.TryGetValue(userId, out TenantRole role)
            && role == TenantRole.TenantOwner;

        string roleSignature = isAdmin ? "admin" : "not-admin";
        return new(true, true, isAdmin, $"tenant:{tenantId}:user:{userId ?? "<missing>"}:{roleSignature}");
    }
}
