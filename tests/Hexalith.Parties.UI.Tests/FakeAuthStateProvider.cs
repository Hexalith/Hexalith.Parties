using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;

namespace Hexalith.Parties.UI.Tests;

/// <summary>Controls the actual provider notification protocol for self-scoped client tests.</summary>
internal sealed class FakeAuthStateProvider : AuthenticationStateProvider
{
    private Task<AuthenticationState> _state;

    /// <summary>Initializes a provider with a completed principal.</summary>
    /// <param name="user">The initial principal.</param>
    internal FakeAuthStateProvider(ClaimsPrincipal user)
        : this(Task.FromResult(new AuthenticationState(user)))
    {
    }

    /// <summary>Initializes a provider with a controlled authentication operation.</summary>
    /// <param name="state">The initial authentication operation.</param>
    internal FakeAuthStateProvider(Task<AuthenticationState> state) => _state = state;

    /// <inheritdoc/>
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state;

    /// <summary>Publishes a replacement authentication operation.</summary>
    /// <param name="state">The new authoritative authentication operation.</param>
    internal void Publish(Task<AuthenticationState> state)
    {
        _state = state;
        NotifyAuthenticationStateChanged(state);
    }
}
