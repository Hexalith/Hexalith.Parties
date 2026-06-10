using Hexalith.Parties.UI.Services;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// A controllable <see cref="IProjectionStream"/> test double — records subscribe/unsubscribe calls,
/// exposes a settable <see cref="IsConnected"/>, and lets a test raise the registered <c>onChanged</c>
/// callback. Lets the Story 1.7 wrapper/primitive/fallback tests run with <strong>no live hub</strong>.
/// </summary>
internal sealed class FakeProjectionStream : IProjectionStream
{
    private readonly List<Registration> _subscriptions = [];

    public bool IsConnected { get; set; }

    public int EnsureStartedCalls { get; private set; }

    public IReadOnlyList<Registration> Subscriptions => _subscriptions;

    public List<Registration> Unsubscribed { get; } = [];

    public Task EnsureStartedAsync(CancellationToken ct = default)
    {
        EnsureStartedCalls++;
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string projectionType, string tenantId, Action onChanged)
    {
        _subscriptions.Add(new Registration(projectionType, tenantId, onChanged));
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string projectionType, string tenantId, Action onChanged)
    {
        var registration = new Registration(projectionType, tenantId, onChanged);
        Unsubscribed.Add(registration);
        _ = _subscriptions.RemoveAll(s => s.ProjectionType == projectionType && s.Tenant == tenantId && s.OnChanged == onChanged);
        return Task.CompletedTask;
    }

    /// <summary>Invokes every callback registered for <c>(projectionType, tenant)</c> (a SignalR confirm).</summary>
    public void RaiseChanged(string projectionType, string tenant)
    {
        foreach (Registration registration in _subscriptions
            .Where(s => s.ProjectionType == projectionType && s.Tenant == tenant)
            .ToArray())
        {
            registration.OnChanged();
        }
    }

    internal sealed record Registration(string ProjectionType, string Tenant, Action OnChanged);
}
