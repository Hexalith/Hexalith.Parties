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
    private readonly ConcurrentDictionary<Guid, PartiesProjectionRegistration> _registrations = new();
    private readonly object _gate = new();
    private bool _disposed;
    private Task? _disposal;

    public PartiesProjectionSubscription(IProjectionStream stream, ILogger<PartiesProjectionSubscription> logger)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(logger);
        _stream = stream;
        _logger = logger;
    }

    /// <summary>Gets the producer transition/rejoin generation.</summary>
    public long RecoveryGeneration => _stream.RecoveryGeneration;

    /// <summary>Gets whether the underlying stream is connected.</summary>
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
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var registration = new PartiesProjectionRegistration(_stream, _logger, projectionType, tenant, onConfirmed,
                id => _registrations.TryRemove(id, out _));
            _registrations[registration.Id] = registration;
            registration.Start();
            return registration;
        }
    }

    /// <summary>Waits for this transport registration before its command can emit a confirmation.</summary>
    /// <param name="handle">A lifetime handle returned by <see cref="Subscribe"/>.</param>
    /// <param name="cancellationToken">The operation lifetime.</param>
    /// <returns>The asynchronous registration readiness.</returns>
    public static Task WaitUntilReadyAsync(IDisposable handle, CancellationToken cancellationToken)
        => handle is PartiesProjectionRegistration registration
            ? registration.WaitUntilReadyAsync(cancellationToken)
            : throw new ArgumentException("The handle is not a Parties projection registration.", nameof(handle));

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            return new ValueTask(_disposal ??= DisposeCoreAsync());
        }
    }

    private async Task DisposeCoreAsync()
    {
        _disposed = true;
        foreach (PartiesProjectionRegistration registration in _registrations.Values)
        {
            await registration.DisposeAsync().ConfigureAwait(false);
        }
    }
}
