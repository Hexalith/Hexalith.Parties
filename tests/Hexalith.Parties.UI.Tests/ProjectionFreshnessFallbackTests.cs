using Hexalith.Parties.UI.Services;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.7 AC2 — the polling fallback coordinator: while the stream is disconnected, each elapsed interval
/// re-invokes the reconcile callback; once the stream reconnects, ticking stops. Driven off an injected
/// (fake) <see cref="TimeProvider"/> — never <c>Task.Delay</c>/<c>DateTime.UtcNow</c>.
/// </summary>
public sealed class ProjectionFreshnessFallbackTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task PollsWhileDisconnected_StopsOnReconnect()
    {
        var time = new ManualTimeProvider();
        var stream = new FakeProjectionStream { IsConnected = false };
        var fallback = new ProjectionFreshnessFallback(
            stream,
            time,
            Options.Create(new ProjectionFreshnessOptions { PollingIntervalSeconds = 30 }));

        int polls = 0;
        Task run = fallback.RunAsync(_ => { polls++; return Task.CompletedTask; }, () => false, CancellationToken.None);

        polls.ShouldBe(1); // immediate tick

        time.Advance(Interval);
        polls.ShouldBe(2);

        time.Advance(Interval);
        polls.ShouldBe(3);

        // SignalR resumed → the next elapsed interval stops the loop without another poll.
        stream.IsConnected = true;
        time.Advance(Interval);

        polls.ShouldBe(3);
        run.IsCompleted.ShouldBeTrue();
        await run.ConfigureAwait(true);
    }

    [Fact]
    public async Task DoesNotPoll_WhenAlreadyConnected()
    {
        var time = new ManualTimeProvider();
        var stream = new FakeProjectionStream { IsConnected = true };
        var fallback = new ProjectionFreshnessFallback(
            stream,
            time,
            Options.Create(new ProjectionFreshnessOptions { PollingIntervalSeconds = 30 }));

        int polls = 0;
        await fallback.RunAsync(_ => { polls++; return Task.CompletedTask; }, () => false, CancellationToken.None);

        polls.ShouldBe(0);
    }

    [Fact]
    public async Task Cancellation_StopsThePollLoopCleanly()
    {
        var time = new ManualTimeProvider();
        var stream = new FakeProjectionStream { IsConnected = false };
        var fallback = new ProjectionFreshnessFallback(
            stream,
            time,
            Options.Create(new ProjectionFreshnessOptions { PollingIntervalSeconds = 30 }));

        using var cts = new CancellationTokenSource();
        int polls = 0;
        Task run = fallback.RunAsync(_ => { polls++; return Task.CompletedTask; }, () => false, cts.Token);

        polls.ShouldBe(1);              // immediate tick, then parked on the interval timer
        run.IsCompleted.ShouldBeFalse();

        // Circuit teardown: cancelling completes the wait and stops the loop without another poll.
        await cts.CancelAsync().ConfigureAwait(true);
        await run.ConfigureAwait(true);

        polls.ShouldBe(1);
        run.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task StopsImmediately_WhenAlreadyDone()
    {
        var time = new ManualTimeProvider();
        var stream = new FakeProjectionStream { IsConnected = false };
        var fallback = new ProjectionFreshnessFallback(
            stream,
            time,
            Options.Create(new ProjectionFreshnessOptions { PollingIntervalSeconds = 30 }));

        int polls = 0;
        await fallback.RunAsync(_ => { polls++; return Task.CompletedTask; }, () => true, CancellationToken.None);

        polls.ShouldBe(0);
    }
}
