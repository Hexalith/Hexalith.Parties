using Hexalith.Parties.UI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>Exercises the actual selected EventStore transport through the production Parties adapter.</summary>
public sealed class ProjectionTransportPackageTests
{
#if HES_CANCELLABLE_PROJECTIONS
    [Fact]
    public Task SelectedTransportCancelsAStalledJoinThroughTheProductionAdapter() => AssertCancellationAsync(leave: false);

    [Fact]
    public Task SelectedTransportCancelsAStalledLeaveThroughTheProductionAdapter() => AssertCancellationAsync(leave: true);

    private static async Task AssertCancellationAsync(bool leave)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var barrier = new PackageProjectionBarrier();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(barrier);
        builder.Services.AddSignalR();
        WebApplication app = builder.Build();
        await using var appLifetime = app.ConfigureAwait(false);
        app.MapHub<PackageProjectionHub>("/hub");
        await app.StartAsync(token).ConfigureAwait(true);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var concrete = new EventStoreSignalRProjectionStream(Options.Create(new ProjectionFreshnessOptions { HubUrl = address + "/hub" }),
            Substitute.For<IProjectionAccessTokenProvider>(), NullLoggerFactory.Instance);
        IProjectionStream stream = concrete;
        using var cancellation = new CancellationTokenSource();
        Action callback = () => { };
        try
        {
            await stream.EnsureStartedAsync(token).ConfigureAwait(true);
            if (leave)
            {
                barrier.Release.TrySetResult();
                await stream.SubscribeAsync("counter", "acme", callback, token).ConfigureAwait(true);
            }
            long generation = stream.RecoveryGeneration;
            Task operation = leave ? stream.UnsubscribeAsync("counter", "acme", callback, cancellation.Token)
                : stream.SubscribeAsync("counter", "acme", callback, cancellation.Token);
            await (leave ? barrier.LeaveEntered : barrier.Entered).Task.WaitAsync(TimeSpan.FromSeconds(5), token).ConfigureAwait(true);
            await cancellation.CancelAsync().ConfigureAwait(true);
            await Should.ThrowAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2), token)).ConfigureAwait(true);
            (leave ? barrier.LeaveFinished : barrier.Finished).Task.IsCompleted.ShouldBeFalse("The caller must settle before the server acknowledgement is released.");
            (leave ? barrier.LeaveRelease : barrier.Release).TrySetResult();
            await (leave ? barrier.LeaveFinished : barrier.Finished).Task.WaitAsync(TimeSpan.FromSeconds(5), token).ConfigureAwait(true);
            stream.RecoveryGeneration.ShouldBe(generation);
        }
        finally { barrier.Release.TrySetResult(); barrier.LeaveRelease.TrySetResult(); await concrete.DisposeAsync().ConfigureAwait(true); await app.StopAsync(token).ConfigureAwait(true); }
    }

    [Fact]
    public async Task SelectedTransportReconnectsRejoinsAndDeliversThroughTheProductionAdapter()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var barrier = new PackageProjectionBarrier();
        barrier.Release.TrySetResult(); barrier.LeaveRelease.TrySetResult();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(barrier);
        builder.Services.AddSignalR();
        WebApplication app = builder.Build();
        await using var appLifetime = app.ConfigureAwait(false);
        app.MapHub<PackageProjectionHub>("/hub");
        await app.StartAsync(token);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var concrete = new EventStoreSignalRProjectionStream(Options.Create(new ProjectionFreshnessOptions { HubUrl = address + "/hub" }),
            Substitute.For<IProjectionAccessTokenProvider>(), NullLoggerFactory.Instance);
        await using var streamLifetime = concrete.ConfigureAwait(false);
        IProjectionStream stream = concrete;
        var initial = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int delivered = 0;
        await stream.EnsureStartedAsync(token);
        await stream.SubscribeAsync("counter", "acme", () =>
        {
            if (Interlocked.Increment(ref delivered) == 1) { initial.TrySetResult(); }
            else { recovered.TrySetResult(); }
        }, token);
        IHubContext<PackageProjectionHub> hub = app.Services.GetRequiredService<IHubContext<PackageProjectionHub>>();
        await hub.Clients.Group("counter:acme").SendAsync("ProjectionChanged", "counter", "acme", token);
        await initial.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        string original = barrier.ConnectionId;
        long generation = stream.RecoveryGeneration;
        barrier.Disconnect();
        await barrier.Rejoined.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        barrier.ConnectionId.ShouldNotBe(original);
        stream.IsConnected.ShouldBeTrue();
        await hub.Clients.Group("counter:acme").SendAsync("ProjectionChanged", "counter", "acme", token);
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        stream.RecoveryGeneration.ShouldBeGreaterThan(generation);
        delivered.ShouldBe(2);
    }
#endif
}
