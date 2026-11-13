using System.Security.Claims;

using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Contracts.Enums;

using Microsoft.AspNetCore.Components.Authorization;

namespace Hexalith.Parties.AdminPortal.Services;

/// <summary>Evaluates tenant authorization against the current authentication state.</summary>
/// <param name="authenticationStateProvider">The circuit authentication state provider.</param>
/// <param name="tenantProjectionStore">The authoritative local tenant projection.</param>
public sealed class AdminPortalAuthorizationService(
    AuthenticationStateProvider authenticationStateProvider,
    ITenantProjectionStore tenantProjectionStore) : IAdminPortalAuthorizationService
{
    /// <inheritdoc/>
    public async Task<AdminPortalAuthorizationState> GetAuthorizationStateAsync(CancellationToken cancellationToken = default)
    {
        Task<AuthenticationState>? changedState = null;
        void OnAuthenticationStateChanged(Task<AuthenticationState> state)
            => Volatile.Write(ref changedState, state);

        authenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        try
        {
            Task<AuthenticationState> initialState = authenticationStateProvider.GetAuthenticationStateAsync();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Task<AuthenticationState> selectedState = Volatile.Read(ref changedState) ?? initialState;
                AuthenticationState authenticationState = await selectedState.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (!ReferenceEquals(selectedState, Volatile.Read(ref changedState) ?? initialState))
                {
                    continue;
                }

                AdminPortalAuthorizationState authorization = await GetAuthorizationStateAsync(
                    authenticationState.User, cancellationToken).ConfigureAwait(false);
                if (ReferenceEquals(selectedState, Volatile.Read(ref changedState) ?? initialState))
                {
                    return authorization;
                }
            }
        }
        finally
        {
            authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        }
    }

    private async Task<AdminPortalAuthorizationState> GetAuthorizationStateAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
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
