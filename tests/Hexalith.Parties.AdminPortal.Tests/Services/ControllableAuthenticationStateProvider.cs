using System.Reflection;

using Microsoft.AspNetCore.Components.Authorization;

namespace Hexalith.Parties.AdminPortal.Tests.Services;

/// <summary>Controls real provider notifications and observes subscription cleanup.</summary>
/// <param name="initialState">The initial authentication operation.</param>
internal sealed class ControllableAuthenticationStateProvider(Task<AuthenticationState> initialState) : AuthenticationStateProvider
{
    private static readonly FieldInfo SubscriptionField = typeof(AuthenticationStateProvider)
        .GetField(nameof(AuthenticationStateChanged), BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The real authentication event field is unavailable.");

    private Task<AuthenticationState> _state = initialState;

    /// <summary>Gets the number of actual event subscriptions.</summary>
    internal int SubscriberCount => ((Delegate?)SubscriptionField.GetValue(this))?.GetInvocationList().Length ?? 0;

    /// <inheritdoc/>
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state;

    /// <summary>Publishes an authoritative replacement authentication operation.</summary>
    /// <param name="state">The replacement operation.</param>
    internal void Publish(Task<AuthenticationState> state)
    {
        _state = state;
        NotifyAuthenticationStateChanged(state);
    }
}
