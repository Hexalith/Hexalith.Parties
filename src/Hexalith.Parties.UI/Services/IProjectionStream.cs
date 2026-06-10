namespace Hexalith.Parties.UI.Services;

/// <summary>
/// The thin, testable seam over <c>Hexalith.EventStore.SignalR.EventStoreSignalRClient</c> (Story 1.7,
/// AR-D6). The concrete client is a <c>sealed</c> class with no interface, so the subscription wrapper
/// (<see cref="PartiesProjectionSubscription"/>) and the optimistic-reconcile primitive
/// (<see cref="OptimisticReconcile"/>) depend on <em>this</em> seam — never the concrete client — which
/// lets the tests exercise subscribe / reconnect / inert-when-no-hub-URL behaviour <strong>without a live
/// hub</strong> (AC6).
/// </summary>
/// <remarks>
/// The method shapes are deliberately <strong>1:1 with <c>EventStoreSignalRClient</c></strong>
/// (<c>SubscribeAsync(projectionType, tenantId, Action)</c> / <c>UnsubscribeAsync(...)</c> /
/// <c>IsConnected</c> / <c>StartAsync</c>), so the production adapter
/// (<see cref="EventStoreSignalRProjectionStream"/>) is a straight forward and adds no behaviour the
/// transport does not already provide (auto-reconnect + group auto-rejoin, FR59).
/// </remarks>
public interface IProjectionStream
{
    /// <summary>
    /// Gets a value indicating whether the underlying SignalR hub connection is currently
    /// <c>Connected</c>. Drives the polling fallback: when this is <see langword="false"/> the
    /// reconcile path polls a freshness re-read instead of awaiting a SignalR confirm.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Idempotently starts the underlying connection (joining any pre-registered groups). A
    /// <strong>no-op when no hub URL is configured</strong> (degraded / test boot): the stream stays
    /// inert and <see cref="IsConnected"/> stays <see langword="false"/>.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    Task EnsureStartedAsync(CancellationToken ct = default);

    /// <summary>
    /// Registers <paramref name="onChanged"/> for projection-change signals keyed by
    /// <c>(projectionType, tenantId)</c>. Registration succeeds even while disconnected (the group
    /// auto-joins on a later connect / reconnect).
    /// </summary>
    Task SubscribeAsync(string projectionType, string tenantId, Action onChanged);

    /// <summary>
    /// Removes a single previously-registered <paramref name="onChanged"/> callback (ref-equality) for
    /// <c>(projectionType, tenantId)</c>.
    /// </summary>
    Task UnsubscribeAsync(string projectionType, string tenantId, Action onChanged);
}
