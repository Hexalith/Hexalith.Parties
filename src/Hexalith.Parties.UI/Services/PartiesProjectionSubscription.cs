using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// The architecture-named D6 service (<c>architecture.md:572, 646</c>): a per-circuit wrapper over the
/// EventStore SignalR transport (<see cref="IProjectionStream"/>) that subscribes to projection-change
/// signals keyed by <c>(projectionType, tenant)</c> and surfaces a Blazor-friendly
/// <see cref="IDisposable"/> handle per subscription (Story 1.7, AC2/AC3/AC4).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reconnect is the transport's job.</strong> The wrapper relies on the client's
/// <c>WithAutomaticReconnect</c> + auto-rejoin (FR59) — it holds <em>no</em> manual re-subscribe logic. A
/// late confirm that arrives after reconnect simply re-invokes the still-registered callback; idempotent
/// re-read + the primitive's one-shot guard prevent any duplicate application (AC3).
/// </para>
/// <para>
/// <strong>Lifetime.</strong> Scoped (per circuit — ADR-030). <see cref="DisposeAsync"/> tears down every
/// live subscription on circuit teardown.
/// </para>
/// <para>
/// <strong>PII hygiene.</strong> Never logs <c>projectionType</c> / <c>tenant</c> / party values — only
/// coarse connection-state / deferral transitions.
/// </para>
/// </remarks>
public sealed class PartiesProjectionSubscription : IAsyncDisposable
{
    private readonly IProjectionStream _stream;
    private readonly ILogger<PartiesProjectionSubscription> _logger;
    private readonly ConcurrentDictionary<Guid, Registration> _registrations = new();
    private bool _disposed;

    public PartiesProjectionSubscription(IProjectionStream stream, ILogger<PartiesProjectionSubscription> logger)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(logger);
        _stream = stream;
        _logger = logger;
    }

    /// <summary>Gets whether the underlying stream is connected (drives the polling fallback).</summary>
    public bool IsConnected => _stream.IsConnected;

    /// <summary>Idempotently starts the underlying stream (no-op when no hub URL is configured).</summary>
    public Task EnsureStartedAsync(CancellationToken ct = default) => _stream.EnsureStartedAsync(ct);

    /// <summary>
    /// Registers <paramref name="onConfirmed"/> for projection-change signals on
    /// <c>(projectionType, tenant)</c> and returns a handle whose <see cref="IDisposable.Dispose"/>
    /// unsubscribes. Triggers a lazy connect (<see cref="EnsureStartedAsync"/>) on first use; if the stream
    /// is unconfigured/disconnected the subscription still registers (so it auto-joins on a later connect)
    /// while the polling fallback owns refresh meanwhile.
    /// </summary>
    public IDisposable Subscribe(string projectionType, string tenant, Action onConfirmed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentNullException.ThrowIfNull(onConfirmed);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var registration = new Registration(this, projectionType, tenant, onConfirmed);
        _registrations[registration.Id] = registration;
        _ = RegisterCoreAsync(registration);
        return registration;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (Registration registration in _registrations.Values)
        {
            await RemoveAsync(registration).ConfigureAwait(false);
        }
    }

    private async Task RegisterCoreAsync(Registration registration)
    {
        try
        {
            await _stream.EnsureStartedAsync().ConfigureAwait(false); // lazy connect on first Subscribe
            await _stream.SubscribeAsync(registration.ProjectionType, registration.Tenant, registration.Callback)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Coarse only — PII hygiene forbids logging projectionType/tenant. The registration is kept so
            // it auto-joins on a later connect; the polling fallback owns refresh meanwhile.
            _logger.LogWarning(ex, "Projection subscription registration deferred (stream unavailable).");
        }
    }

    private async Task RemoveAsync(Registration registration)
    {
        if (!_registrations.TryRemove(registration.Id, out _))
        {
            return;
        }

        try
        {
            await _stream.UnsubscribeAsync(registration.ProjectionType, registration.Tenant, registration.Callback)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Projection unsubscribe deferred (stream unavailable).");
        }
    }

    private sealed class Registration(PartiesProjectionSubscription owner, string projectionType, string tenant, Action callback)
        : IDisposable
    {
        private int _disposed;

        public Guid Id { get; } = Guid.NewGuid();

        public string ProjectionType { get; } = projectionType;

        public string Tenant { get; } = tenant;

        public Action Callback { get; } = callback;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _ = owner.RemoveAsync(this);
        }
    }
}
