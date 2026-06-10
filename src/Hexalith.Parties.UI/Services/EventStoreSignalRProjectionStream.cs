using Hexalith.EventStore.SignalR;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// The production <see cref="IProjectionStream"/> adapter (Story 1.7, AR-D6): owns <strong>one</strong>
/// per-circuit <see cref="EventStoreSignalRClient"/> and forwards <see cref="IsConnected"/> /
/// <c>Subscribe</c> / <c>Unsubscribe</c> 1:1. Reconnect + group auto-rejoin are <strong>entirely the
/// client's job</strong> (<c>WithAutomaticReconnect</c> + <c>OnReconnectedAsync → JoinAllGroupsAsync</c>,
/// FR59); this adapter adds <em>no</em> reconnect logic and <em>no</em> dedup — the signal-only re-query
/// design plus the primitive's one-shot guard cover "no duplicate application".
/// </summary>
/// <remarks>
/// <para>
/// <strong>Inert when unconfigured.</strong> When no hub URL is configured (degraded / test boot) the
/// client is never constructed: <see cref="IsConnected"/> stays <see langword="false"/>,
/// <see cref="EnsureStartedAsync"/> is a no-op, and <c>Subscribe</c>/<c>Unsubscribe</c> do nothing — the
/// polling fallback owns refresh.
/// </para>
/// <para>
/// <strong>Lifetime.</strong> Scoped (per circuit — ADR-030). Each circuit owns its own connection so a
/// process-wide Singleton can never share one tenant/token across users (the multi-tenant BFF reason the
/// admin-tool Singleton precedent does <em>not</em> apply here). <see cref="DisposeAsync"/> tears the
/// connection down on circuit teardown.
/// </para>
/// </remarks>
internal sealed class EventStoreSignalRProjectionStream : IProjectionStream, IAsyncDisposable
{
    private readonly EventStoreSignalRClient? _client;
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private bool _started;

    public EventStoreSignalRProjectionStream(
        IOptions<ProjectionFreshnessOptions> options,
        IProjectionAccessTokenProvider accessTokenProvider,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(accessTokenProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        string? hubUrl = options.Value.HubUrl;
        if (!string.IsNullOrWhiteSpace(hubUrl))
        {
            _client = new EventStoreSignalRClient(
                new EventStoreSignalRClientOptions
                {
                    HubUrl = hubUrl,
                    AccessTokenProvider = accessTokenProvider.GetAccessTokenAsync,

                    // A long-lived Blazor Server circuit must never permanently give up reconnecting
                    // (the client default disconnects after ~42 s). Open-question Q4: an infinite-retry
                    // policy for the circuit's lifetime.
                    RetryPolicy = new InfiniteRetryPolicy(),
                },
                loggerFactory.CreateLogger<EventStoreSignalRClient>());
        }
    }

    public bool IsConnected => _client?.IsConnected ?? false;

    public async Task EnsureStartedAsync(CancellationToken ct = default)
    {
        if (_client is null || _started)
        {
            return;
        }

        await _startGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_started)
            {
                return;
            }

            await _client.StartAsync(ct).ConfigureAwait(false);
            _started = true;
        }
        finally
        {
            _ = _startGate.Release();
        }
    }

    public Task SubscribeAsync(string projectionType, string tenantId, Action onChanged)
        => _client is null
            ? Task.CompletedTask
            : _client.SubscribeAsync(projectionType, tenantId, onChanged);

    public Task UnsubscribeAsync(string projectionType, string tenantId, Action onChanged)
        => _client is null
            ? Task.CompletedTask
            : _client.UnsubscribeAsync(projectionType, tenantId, onChanged);

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }

        _startGate.Dispose();
    }

    /// <summary>
    /// An <see cref="IRetryPolicy"/> that mirrors the client default ramp (<c>[0s, 2s, 10s, 30s]</c>) but
    /// then retries <strong>forever</strong> at 30&#160;s — appropriate for a circuit that should silently
    /// reconnect for its whole lifetime rather than surrendering.
    /// </summary>
    private sealed class InfiniteRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            ArgumentNullException.ThrowIfNull(retryContext);
            return retryContext.PreviousRetryCount switch
            {
                0 => TimeSpan.Zero,
                1 => TimeSpan.FromSeconds(2),
                2 => TimeSpan.FromSeconds(10),
                _ => TimeSpan.FromSeconds(30),
            };
        }
    }
}
