using Microsoft.Extensions.Options;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// The shared polling coordinator (Story 1.7, AC2). Witnessed operations retry while connected or
/// disconnected using <c>Parties:Freshness:PollingIntervalSeconds</c> (default 30&#160;s). Durable transport
/// generations retain brief outages. The legacy <see cref="RunAsync"/> entry point retains disconnected-only
/// polling. Both drivers stop when the operation completes or its token cancels.
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
    /// <summary>Gets the caller's deterministic retry clock.</summary>
    public TimeProvider Clock => timeProvider;

    /// <summary>Gets the configured retry interval.</summary>
    public TimeSpan Interval => TimeSpan.FromSeconds(Math.Max(1, options.Value.PollingIntervalSeconds));

    /// <summary>Retries accepted operations until witnessed, even while connected. Durable generations retain transitions occurring before this driver starts.</summary>
    public async Task RunUntilWitnessedAsync(Func<CancellationToken, Task> onPoll, Func<bool> isDone, long generation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(onPoll);
        ArgumentNullException.ThrowIfNull(isDone);
        if (ct.IsCancellationRequested || isDone())
        {
            return;
        }
        if (!stream.IsConnected || stream.RecoveryGeneration != generation)
        {
            await onPoll(ct).ConfigureAwait(false);
        }
        while (!ct.IsCancellationRequested && !isDone())
        {
            if (!await TryWaitIntervalAsync(Interval, ct).ConfigureAwait(false))
            {
                return;
            }
            await onPoll(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Polls <paramref name="onPoll"/> every interval while the stream is disconnected, starting with an
    /// <strong>immediate</strong> first poll. Returns when <paramref name="isDone"/> is satisfied or
    /// <paramref name="ct"/> cancels. Connected intervals observe connectivity without reading.
    /// </summary>
    /// <param name="onPoll">The reconcile re-read to run each tick.</param>
    /// <param name="isDone">Returns <see langword="true"/> once the caller has reconciled (stop polling).</param>
    /// <param name="ct">Cancellation (e.g. circuit teardown).</param>
    public async Task RunAsync(Func<CancellationToken, Task> onPoll, Func<bool> isDone, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(onPoll);
        ArgumentNullException.ThrowIfNull(isDone);

        TimeSpan interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollingIntervalSeconds));

        while (!ct.IsCancellationRequested && !isDone())
        {
            if (!stream.IsConnected)
            {
                await onPoll(ct).ConfigureAwait(false);
            }

            if (isDone())
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
