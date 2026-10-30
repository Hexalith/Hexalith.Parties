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

    private bool _connected;

    public long RecoveryGeneration { get; private set; }

    public bool IsConnected
    {
        get => _connected;
        set
        {
            if (_connected != value)
            {
                RecoveryGeneration++;
                _connected = value;
            }
        }
    }

    public bool SupportsCancellation { get; set; }

    public CancellationToken RemovalToken { get; private set; }

    public async Task SubscribeAsync(string projectionType, string tenantId, Action onChanged, CancellationToken cancellationToken)
    {
        if (!SupportsCancellation)
        {
            await SubscribeAsync(projectionType, tenantId, onChanged).ConfigureAwait(false);
            return;
        }
        await RegistrationDelay.WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        _subscriptions.Add(new Registration(projectionType, tenantId, onChanged));
    }

    public async Task UnsubscribeAsync(string projectionType, string tenantId, Action onChanged, CancellationToken cancellationToken)
    {
        if (!SupportsCancellation)
        {
            await UnsubscribeAsync(projectionType, tenantId, onChanged).ConfigureAwait(false);
            return;
        }
        RemovalToken = cancellationToken;
        _ = _subscriptions.RemoveAll(s => s.ProjectionType == projectionType && s.Tenant == tenantId && s.OnChanged == onChanged);
        UnsubscriptionStarted.TrySetResult();
        await UnsubscriptionDelay.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public Exception? StartFailure { get; set; }

    public Task RegistrationDelay { get; set; } = Task.CompletedTask;

    public Task UnsubscriptionDelay { get; set; } = Task.CompletedTask;

    public TaskCompletionSource UnsubscriptionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int EnsureStartedCalls { get; private set; }

    public IReadOnlyList<Registration> Subscriptions => _subscriptions;

    public List<Registration> Unsubscribed { get; } = [];

    public Task EnsureStartedAsync(CancellationToken ct = default)
    {
        EnsureStartedCalls++;
        return StartFailure is { } failure ? Task.FromException(failure) : Task.CompletedTask;
    }

    public async Task SubscribeAsync(string projectionType, string tenantId, Action onChanged)
    {
        await RegistrationDelay.ConfigureAwait(false);
        _subscriptions.Add(new Registration(projectionType, tenantId, onChanged));
    }

    public async Task UnsubscribeAsync(string projectionType, string tenantId, Action onChanged)
    {
        UnsubscriptionStarted.TrySetResult();
        await UnsubscriptionDelay.ConfigureAwait(false);
        var registration = new Registration(projectionType, tenantId, onChanged);
        Unsubscribed.Add(registration);
        _ = _subscriptions.RemoveAll(s => s.ProjectionType == projectionType && s.Tenant == tenantId && s.OnChanged == onChanged);
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
