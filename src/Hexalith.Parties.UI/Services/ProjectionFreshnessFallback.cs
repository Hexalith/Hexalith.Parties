using Microsoft.Extensions.Options;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// The single shared polling-fallback coordinator (Story 1.7, AC2; <c>architecture.md:482</c> "No bespoke
/// per-screen polling"). The optimistic-reconcile primitive uses it while the SignalR stream is
/// <strong>not</strong> connected: it invokes the supplied reconcile re-read on a configurable interval
/// (<c>Parties:Freshness:PollingIntervalSeconds</c>, default 30&#160;s) and stops as soon as the operation
/// completes, the stream reconnects, or the token cancels.
/// </summary>
/// <remarks>
/// <strong>Testability.</strong> Time comes from the injected <see cref="TimeProvider"/> (a Singleton in the
/// FrontComposer graph) — never <c>Task.Delay</c>/<c>DateTime.UtcNow</c> — so a fake time source can assert
/// "polls while disconnected, stops on reconnect". Scoped (per circuit — ADR-030).
/// </remarks>
public sealed class ProjectionFreshnessFallback(
    IProjectionStream stream,
    TimeProvider timeProvider,
    IOptions<ProjectionFreshnessOptions> options)
{
    /// <summary>
    /// Polls <paramref name="onPoll"/> every interval while the stream is disconnected, starting with an
    /// <strong>immediate</strong> first poll. Returns when <paramref name="isDone"/> is satisfied, the
    /// stream reports <see cref="IProjectionStream.IsConnected"/>, or <paramref name="ct"/> cancels.
    /// </summary>
    /// <param name="onPoll">The reconcile re-read to run each tick.</param>
    /// <param name="isDone">Returns <see langword="true"/> once the caller has reconciled (stop polling).</param>
    /// <param name="ct">Cancellation (e.g. circuit teardown).</param>
    public async Task RunAsync(Func<CancellationToken, Task> onPoll, Func<bool> isDone, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(onPoll);
        ArgumentNullException.ThrowIfNull(isDone);

        TimeSpan interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollingIntervalSeconds));

        while (!ct.IsCancellationRequested && !isDone() && !stream.IsConnected)
        {
            await onPoll(ct).ConfigureAwait(false);

            if (isDone() || stream.IsConnected)
            {
                return;
            }

            if (!await TryWaitIntervalAsync(interval, ct).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Waits one <paramref name="interval"/> off the injected <see cref="TimeProvider"/>. Returns
    /// <see langword="false"/> when cancelled (so the caller stops cleanly). Uses a one-shot
    /// <see cref="ITimer"/> rather than <c>Task.Delay</c> so a fake time source drives ticks deterministically.
    /// </summary>
    private async Task<bool> TryWaitIntervalAsync(TimeSpan interval, CancellationToken ct)
    {
        var tick = new TaskCompletionSource();
        using ITimer timer = timeProvider.CreateTimer(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            tick,
            interval,
            Timeout.InfiniteTimeSpan);
        using CancellationTokenRegistration registration = ct.Register(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            tick);

        await tick.Task.ConfigureAwait(false);
        return !ct.IsCancellationRequested;
    }
}
