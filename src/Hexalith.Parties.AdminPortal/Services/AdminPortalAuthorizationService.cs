using System.Security.Claims;

using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Contracts.Enums;

using Microsoft.AspNetCore.Components.Authorization;

namespace Hexalith.Parties.AdminPortal.Services;

public sealed class AdminPortalAuthorizationService(
    AuthenticationStateProvider authenticationStateProvider,
    ITenantProjectionStore tenantProjectionStore) : IAdminPortalAuthorizationService
{
    private const string TenantClaimType = "eventstore:tenant";
    private const string SubjectClaimType = "sub";
    private const string ObjectIdClaimType = "oid";

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

        string? tenantId = ExtractTenant(principal);
        string? userId = ExtractUserId(principal);
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

    private static string? ExtractTenant(ClaimsPrincipal principal)
        => principal.FindAll(TenantClaimType)
            .Select(claim => claim.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? ExtractUserId(ClaimsPrincipal principal)
    {
        string? userId = principal.FindFirst(SubjectClaimType)?.Value
            ?? principal.FindFirst(ObjectIdClaimType)?.Value;
        return string.IsNullOrWhiteSpace(userId) ? null : userId;
    }
}
