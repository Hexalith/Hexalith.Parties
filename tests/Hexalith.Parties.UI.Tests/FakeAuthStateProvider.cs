using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// A minimal <see cref="AuthenticationStateProvider"/> test double that returns a fixed principal —
/// the simplest way to drive <see cref="Hexalith.Parties.UI.Services.SelfScopedPartiesClient"/> without
/// pulling in a real OIDC stack. Mirrors the per-circuit principal source the FrontComposer auth bridge
/// provides at runtime.
/// </summary>
internal sealed class FakeAuthStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(new AuthenticationState(user));
}
