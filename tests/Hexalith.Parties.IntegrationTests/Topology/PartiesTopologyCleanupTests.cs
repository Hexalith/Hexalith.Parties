extern alias apphost;

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Hexalith.Parties.IntegrationTests.Gateway;
using Hexalith.Parties.IntegrationTests.HealthChecks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Verifies cleanup runs after topology shutdown, including partial and failed startup.</summary>
[Collection("Non-parallel")]
public sealed class PartiesTopologyCleanupTests
{
    [Theory]
    [InlineData("")]
    [InlineData("stop")]
    [InlineData("application")]
    [InlineData("builder")]
    public async Task Cleanup_OrdersQuiescenceBeforeOwnedInfrastructureAndSurvivesFailures(string failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var events = new List<string>();
        Task Step(string name)
        {
            events.Add(name);
            return name == failure ? Task.FromException(new InvalidOperationException(name)) : Task.CompletedTask;
        }

        Task cleanup = PartiesTopologyCleanup.RunAsync(
            () => Step("stop"), () => Step("application"), () => Step("builder"), () => Step("control-plane"));
        if (failure.Length == 0)
        {
            await cleanup.ConfigureAwait(true);
        }
        else
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => cleanup)).Message.ShouldBe(failure);
        }

        events.ShouldBe(["stop", "application", "builder", "control-plane"]);
    }
    [Fact]
    public async Task Cleanup_WhenQuiescenceCannotBeConfirmed_PreservesTheControlPlane()
    {
        bool builderDisposed = false;
        bool controlPlaneRemoved = false;
        await Should.ThrowAsync<InvalidOperationException>(() => PartiesTopologyCleanup.RunAsync(
            () => Task.FromException(new InvalidOperationException("stop failed")),
            () => Task.FromException(new InvalidOperationException("disposal failed")),
            () => { builderDisposed = true; return Task.CompletedTask; },
            () => { controlPlaneRemoved = true; return Task.CompletedTask; })).ConfigureAwait(true);
        builderDisposed.ShouldBeTrue();
        controlPlaneRemoved.ShouldBeFalse();
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BootstrapDisposalFailure_StillCleansTopologyAndPreservesEveryFailure(bool topologyAlsoFails)
    {
        var events = new List<string>();
        var bootstrapFailure = new InvalidOperationException("bootstrap disposal failed");
        var topologyFailure = new InvalidOperationException("application disposal failed");
        Task Step(string name)
        {
            events.Add(name);
            return name == "application" && topologyAlsoFails
                ? Task.FromException(topologyFailure)
                : Task.CompletedTask;
        }
        Task cleanup = PartiesTopologyCleanup.RunAsync(
            () => Step("stop"), () => Step("application"), () => Step("builder"), () => Step("control-plane"),
            () => { events.Add("bootstrap"); return Task.FromException(bootstrapFailure); });
        if (topologyAlsoFails)
        {
            AggregateException failure = await Should.ThrowAsync<AggregateException>(() => cleanup).ConfigureAwait(true);
            failure.InnerExceptions.ShouldBe([bootstrapFailure, topologyFailure]);
        }
        else
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => cleanup).ConfigureAwait(true))
                .ShouldBeSameAs(bootstrapFailure);
        }
        events.ShouldBe(["bootstrap", "stop", "application", "builder", "control-plane"]);
    }

    [Fact]
    public async Task PendingDisposal_RetainsInfrastructureUntilTheAwaitedWorkerCompletesAsync()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool infrastructureRemoved = false;
        bool builderDisposed = false;
        Task cleanup = PartiesTopologyCleanup.RunAsync(
            () => Task.FromException(new OperationCanceledException("controlled stop cancellation")),
            async () => { entered.SetResult(); await release.Task.ConfigureAwait(false); },
            () => { builderDisposed = true; return Task.CompletedTask; },
            () => { infrastructureRemoved = true; return Task.CompletedTask; });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            cleanup.IsCompleted.ShouldBeFalse();
            builderDisposed.ShouldBeFalse();
            infrastructureRemoved.ShouldBeFalse();
        }
        finally
        {
            release.TrySetResult();
            await Should.ThrowAsync<OperationCanceledException>(() => cleanup.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        builderDisposed.ShouldBeTrue();
        infrastructureRemoved.ShouldBeTrue();
    }

    [Fact]
    public async Task FreshWorkerObservation_RecognizesExitAndRejectsAReusedPidIdentityAsync()
    {
        await WithControlledWorkerAsync(async child =>
        {
            int workerId = int.Parse((await child.StandardError.ReadLineAsync(TestContext.Current.CancellationToken).ConfigureAwait(true))!, CultureInfo.InvariantCulture);
            // DCP's workers are also non-child processes to .NET. Hold a cached
            // handle across exit while observations use the kernel identity.
            using Process captured = Process.GetProcessById(workerId);
            _ = captured.Handle;
            long started = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(workerId)!.Value;
            captured.HasExited.ShouldBeFalse();
            PartiesAspireTopologyFixture.IsWorkerRunning(captured.Id, started).ShouldBeTrue();
            PartiesAspireTopologyFixture.IsWorkerRunning(captured.Id, started + 1).ShouldBeFalse();
            child.StandardInput.Close();
            using var exit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await child.WaitForExitAsync(exit.Token).ConfigureAwait(true);
            PartiesAspireTopologyFixture.IsWorkerRunning(captured.Id, started).ShouldBeFalse();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ControlledWorker_SetupCancellationOrFailureStillReapsOwnedWorkersAsync(bool canceled)
    {
        int workerId = 0;
        long started = 0;
        Task run = WithControlledWorkerAsync(async child =>
        {
            workerId = int.Parse((await child.StandardError.ReadLineAsync(TestContext.Current.CancellationToken).ConfigureAwait(true))!, CultureInfo.InvariantCulture);
            started = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(workerId)!.Value;
            if (!canceled) { throw new InvalidOperationException("controlled setup failure"); }
            using var setup = new CancellationTokenSource();
            Task<string?> pendingRead = child.StandardOutput.ReadLineAsync(setup.Token).AsTask();
            setup.Cancel();
            await pendingRead.ConfigureAwait(true);
        });
        if (canceled) { await Should.ThrowAsync<OperationCanceledException>(() => run); }
        else { await Should.ThrowAsync<InvalidOperationException>(() => run); }
        workerId.ShouldBeGreaterThan(0);
        PartiesAspireTopologyFixture.IsWorkerRunning(workerId, started).ShouldBeFalse();
    }

    private static async Task WithControlledWorkerAsync(Func<Process, Task> action)
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("The controlled worker uses the Linux topology prerequisite bash."); }
        var start = new ProcessStartInfo("bash")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("""sleep 3600 & worker=$!; echo "$worker" >&2; read -r; kill "$worker"; wait "$worker" 2>/dev/null || true""");
        using Process child = Process.Start(start) ?? throw new InvalidOperationException("The controlled worker parent could not start.");
        try
        {
            await action(child).ConfigureAwait(false);
        }
        finally
        {
            // Ownership begins immediately after launch, including setup cancellation.
            child.StandardInput.Close();
            using var graceful = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await child.WaitForExitAsync(graceful.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (graceful.IsCancellationRequested)
            {
                await ReapOwnedProcessAsync(child, terminate: true).ConfigureAwait(false);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingStartup_BlocksQuiescenceUntilDelayedWorkerIsStoppableOrCanceledAsync(bool canceled)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        IResourceBuilder<ExecutableResource> worker = builder.AddExecutable("delayed-worker", "unused", ".");
        await using DistributedApplication application = builder.Build();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Task barrier = PartiesAspireTopologyFixture.WaitForPendingStartsAsync(application.ResourceNotifications, [worker.Resource.Name], deadline.Token);
        try
        {
            barrier.IsCompleted.ShouldBeFalse("An absent snapshot can still represent a queued launch.");
            foreach (string state in new[] { KnownResourceStates.Waiting, KnownResourceStates.Starting, KnownResourceStates.Running })
            {
                await application.ResourceNotifications.PublishUpdateAsync(worker.Resource, snapshot => snapshot with
                {
                    State = new ResourceStateSnapshot(state, null),
                    Commands = [new ResourceCommandSnapshot(KnownResourceCommands.StopCommand, ResourceCommandState.Disabled, "Stop", null, null, null, null, null, false)],
                });
                await Should.ThrowAsync<TimeoutException>(() => barrier.WaitAsync(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken)).ConfigureAwait(true);
                barrier.IsCompleted.ShouldBeFalse("Pending or disabled-stop workers must retain backing ownership.");
            }
            if (canceled)
            {
                deadline.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => barrier);
            }
            else
            {
                await application.ResourceNotifications.PublishUpdateAsync(worker.Resource, snapshot => snapshot with
                {
                    State = new ResourceStateSnapshot(KnownResourceStates.Running, null),
                    Commands = [new ResourceCommandSnapshot(KnownResourceCommands.StopCommand, ResourceCommandState.Enabled, "Stop", null, null, null, null, null, false)],
                });
                await barrier.ConfigureAwait(true);
            }
        }
        finally
        {
            deadline.Cancel();
            try { await barrier.ConfigureAwait(true); }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoppedUnknownWorker_ObserverFailureRetainsStopProofAcrossAutomaticAndExplicitRetriesAsync(bool siblingFails)
    {
        await WithControlledWorkerAsync(async child =>
        {
            int workerId = int.Parse((await child.StandardError.ReadLineAsync(TestContext.Current.CancellationToken).ConfigureAwait(true))!, CultureInfo.InvariantCulture);
            long started = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(workerId)!.Value;
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
            IResourceBuilder<ExecutableResource> worker = builder.AddResource(new ExecutableResource("owned-worker", "unused", "."));
            DistributedApplication? application = null;
            int stops = 0;
            _ = worker.WithCommand(KnownResourceCommands.StopCommand, "Stop", async _ =>
            {
                stops++;
                child.StandardInput.Close();
                using var exit = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await child.WaitForExitAsync(exit.Token).ConfigureAwait(false);
                await application!.ResourceNotifications.PublishUpdateAsync(worker.Resource, snapshot => snapshot with
                {
                    State = new ResourceStateSnapshot("Unknown", null),
                    Commands = [new ResourceCommandSnapshot(KnownResourceCommands.StopCommand, ResourceCommandState.Disabled, "Stop", null, null, null, null, null, false)],
                }).ConfigureAwait(false);
                // Reproduce DCP's failed result after the exact owned process exited.
                return CommandResults.Failure("controlled post-exit DCP status failure");
            }, new CommandOptions { UpdateState = _ => stops == 0 ? ResourceCommandState.Enabled : ResourceCommandState.Disabled });
            int siblingStops = 0;
            IResourceBuilder<ExecutableResource>? sibling = null;
            if (siblingFails)
            {
                sibling = builder.AddResource(new ExecutableResource("failed-sibling", "unused", "."));
                _ = sibling.WithCommand(KnownResourceCommands.StopCommand, "Stop", async _ =>
                {
                    if (++siblingStops == 1) { return CommandResults.Failure("controlled stop failure without exit proof"); }
                    await application!.ResourceNotifications.PublishUpdateAsync(sibling.Resource, snapshot => snapshot with
                    {
                        State = new ResourceStateSnapshot(KnownResourceStates.Finished, null),
                        Commands = [new ResourceCommandSnapshot(KnownResourceCommands.StopCommand, ResourceCommandState.Disabled, "Stop", null, null, null, null, null, false)],
                    }).ConfigureAwait(false);
                    return CommandResults.Success();
                }, new CommandOptions { UpdateState = _ => siblingStops < 2 ? ResourceCommandState.Enabled : ResourceCommandState.Disabled });
            }
            application = builder.Build();
            await using var applicationLifetime = application.ConfigureAwait(true);
            await application.ResourceNotifications.PublishUpdateAsync(worker.Resource, snapshot => snapshot with
            {
                State = new ResourceStateSnapshot(KnownResourceStates.Running, null),
                Properties = [new ResourcePropertySnapshot("executable.pid", workerId)],
                Commands = [new ResourceCommandSnapshot(KnownResourceCommands.StopCommand, ResourceCommandState.Enabled, "Stop", null, null, null, null, null, false)],
            }).ConfigureAwait(true);
            if (sibling is not null)
            {
                await application.ResourceNotifications.PublishUpdateAsync(sibling.Resource, snapshot => snapshot with
                {
                    State = new ResourceStateSnapshot(KnownResourceStates.Running, null),
                    Commands = [new ResourceCommandSnapshot(KnownResourceCommands.StopCommand, ResourceCommandState.Enabled, "Stop", null, null, null, null, null, false)],
                }).ConfigureAwait(true);
            }
            var fixture = new PartiesAspireTopologyFixture();
            await using var fixtureLifetime = fixture.ConfigureAwait(true);
            int observations = 0;
            bool backingRemoved = false;
            fixture.ObserveQuiescenceAsync = (_, _) =>
            {
                observations++;
                PartiesAspireTopologyFixture.IsWorkerRunning(workerId, started).ShouldBeFalse();
                return Task.FromException(new InvalidOperationException("controlled observer failure"));
            };
            if (siblingFails)
            {
                using var failedStop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                (await Should.ThrowAsync<InvalidOperationException>(() => fixture.QuiesceStartedWorkersAsync(application, failedStop.Token)).ConfigureAwait(true))
                    .Message.ShouldContain("An owned worker stop command failed");
                observations.ShouldBe(0);
                stops.ShouldBe(1, "A sibling failure must not discard an already completed stop.");
            }
            using var attempt = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            (await Should.ThrowAsync<InvalidOperationException>(() => PartiesTopologyCleanup.RunAsync(
                () => fixture.QuiesceStartedWorkersAsync(application, attempt.Token),
                () => fixture.QuiesceStartedWorkersAsync(application, attempt.Token),
                () => Task.CompletedTask,
                () => { backingRemoved = true; return Task.CompletedTask; })).ConfigureAwait(true)).Message.ShouldBe("controlled observer failure");
            observations.ShouldBe(2, "The automatic fallback must reuse the completed stop proof.");
            stops.ShouldBe(1);
            backingRemoved.ShouldBeFalse();
            application.ResourceNotifications.TryGetCurrentState(worker.Resource.Name, out ResourceEvent? stopped).ShouldBeTrue();
            stopped!.Snapshot.State!.Text.ShouldBe("Unknown");
            stopped.Snapshot.Commands.Single(command => command.Name == KnownResourceCommands.StopCommand).State.ShouldBe(ResourceCommandState.Disabled);
            fixture.ObserveQuiescenceAsync = (_, _) => { observations++; return Task.CompletedTask; };
            using var retry = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await fixture.QuiesceStartedWorkersAsync(application, retry.Token).ConfigureAwait(true);
            observations.ShouldBe(3, "An explicit retry must reuse stop proof for the same application.");
            stops.ShouldBe(1);
            siblingStops.ShouldBe(siblingFails ? 2 : 0);
            PartiesAspireTopologyFixture.IsWorkerRunning(workerId, started).ShouldBeFalse();
        });
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("canceled")]
    [InlineData("failed")]
    [InlineData("cleanup-failed")]
    public async Task LiveShutdown_NormalCanceledAndFailedStartupStopsOwnedWorkersWithinObservationDeadlineAsync(string mode)
    {
        EventStoreGatewayE2ETests.RequireRuntimeProfile();
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var workers = new List<(int Id, long Started)>();
        var containers = new List<string>();
        bool reachedLiveWorkers = false;
        bool observedWorkersBeforeInfrastructureRemoval = false;
        TimeSpan hostShutdown = TimeSpan.Zero;
        Stopwatch? interruptedCleanup = null;
        void RecordLiveOwnership(DistributedApplication application)
        {
            workers.Clear();
            containers.Clear();
            DistributedApplicationModel model = application.Services.GetRequiredService<DistributedApplicationModel>();
            string[] names = model.Resources.Select(resource => resource.Name)
                .Concat(PartiesAspireTopologyFixture.CompleteSidecarIds.Select(name => name + "-dapr-cli"))
                .Distinct(StringComparer.Ordinal).ToArray();
            foreach (string name in names)
            {
                if (!application.ResourceNotifications.TryGetCurrentState(name, out ResourceEvent? state)) { continue; }
                object? pid = state.Snapshot.Properties.FirstOrDefault(property => property.Name == "executable.pid")?.Value;
                if (pid is not null && int.TryParse(Convert.ToString(pid, CultureInfo.InvariantCulture), out int id))
                {
                    if (PartiesAspireTopologyFixture.ReadWorkerStartIdentity(id) is long started) { workers.Add((id, started)); }
                }
                if (state.Resource is ContainerResource) { containers.Add(state.ResourceId); }
            }
        }
        var fixture = new PartiesAspireTopologyFixture(
            async (session, token) =>
            {
                IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder.CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
                    session.Arguments(true, true), token).ConfigureAwait(false);
                builder.Configuration["DcpPublisher:WaitForResourceCleanup"].ShouldBe("true");
                return builder;
            },
            async (application, token) =>
            {
                hostShutdown = application.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout;
                hostShutdown.ShouldBeGreaterThan(TimeSpan.Zero);
                hostShutdown.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
                await application.StartAsync(token).ConfigureAwait(false);
                await application.ResourceNotifications.WaitForResourceHealthyAsync("parties", token).ConfigureAwait(false);
                RecordLiveOwnership(application);
                workers.Count.ShouldBeGreaterThanOrEqualTo(4);
                containers.ShouldContain(name => name.StartsWith("fixture-redis-", StringComparison.Ordinal));
                reachedLiveWorkers = true;
                if (mode == "failed")
                {
                    interruptedCleanup = Stopwatch.StartNew();
                    throw new InvalidOperationException("controlled live startup failure");
                }
                if (mode == "canceled")
                {
                    interruptedCleanup = Stopwatch.StartNew();
                    startup.Cancel();
                    token.ThrowIfCancellationRequested();
                }
            },
            prepareCiBootstrap: true,
            completeMtlsProfile: true);
        fixture.ObserveQuiescenceAsync = async (_, token) =>
        {
            foreach ((int id, long started) in workers)
            {
                PartiesAspireTopologyFixture.IsWorkerRunning(id, started).ShouldBeFalse("A worker was active at the infrastructure-removal boundary.");
            }
            foreach (string container in containers)
            {
                (await DockerOutputAsync(token, "ps", "-a", "--filter", $"name=^{container}$", "--format", "{{.ID}}").ConfigureAwait(false))
                    .ShouldNotBeEmpty("A backing container was removed before worker quiescence.");
            }
            Directory.Exists(fixture.Session.CertificateDirectory).ShouldBeTrue();
            observedWorkersBeforeInfrastructureRemoval = true;
        };
        await using var lifetime = fixture.ConfigureAwait(true);
        var elapsed = Stopwatch.StartNew();
        Task initialization = fixture.InitializeAsync(startup.Token).AsTask();
        try
        {
            await initialization.WaitAsync(TimeSpan.FromMinutes(6), TestContext.Current.CancellationToken);
            reachedLiveWorkers.ShouldBeTrue(fixture.UnavailableReason);
            if (mode is "normal" or "cleanup-failed")
            {
                fixture.IsAvailable.ShouldBeTrue(fixture.UnavailableReason);
                // Initialization explicitly starts the admin UI and waits for all eight sidecars.
                // Capture again so the shutdown proof includes those later workers as well.
                RecordLiveOwnership(fixture.App);
                workers.Count.ShouldBeGreaterThanOrEqualTo(PartiesAspireTopologyFixture.CompleteSidecarIds.Count * 2);
                containers.Count.ShouldBe(3);
            }
            else
            {
                fixture.IsAvailable.ShouldBeFalse();
                fixture.UnavailableReason.ShouldNotBeNull().ShouldContain(mode == "failed" ? "controlled live startup failure" : "CanceledException");
                interruptedCleanup.ShouldNotBeNull().Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(60));
            }
            if (mode == "cleanup-failed")
            {
                Func<DistributedApplication, CancellationToken, Task>? observe = fixture.ObserveQuiescenceAsync;
                try
                {
                    fixture.ObserveQuiescenceAsync = (_, _) => Task.FromException(new InvalidOperationException("controlled quiescence observation failure"));
                    elapsed.Restart();
                    Task attemptedCleanup = fixture.DisposeAsync().AsTask();
                    (await Should.ThrowAsync<InvalidOperationException>(async () =>
                    {
                        try
                        {
                            await attemptedCleanup.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            await attemptedCleanup.ConfigureAwait(false);
                        }
                    })).Message.ShouldBe("controlled quiescence observation failure");
                    elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(60));
                    Directory.Exists(fixture.Session.CertificateDirectory).ShouldBeTrue();
                    foreach (string container in containers)
                    {
                        (await DockerOutputAsync("ps", "-a", "--filter", $"name=^{container}$", "--format", "{{.ID}}"))
                            .ShouldNotBeEmpty("A failed quiescence observation must retain backing resources for retry.");
                    }
                }
                finally
                {
                    fixture.ObserveQuiescenceAsync = observe;
                }
            }
            elapsed.Restart();
            Task disposal = fixture.DisposeAsync().AsTask();
            try
            {
                await disposal.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
            }
            finally
            {
                // A failed observation must not abandon the underlying cleanup operation.
                await disposal.ConfigureAwait(true);
            }
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(60));
            observedWorkersBeforeInfrastructureRemoval.ShouldBeTrue();
            foreach ((int id, long started) in workers)
            {
                PartiesAspireTopologyFixture.IsWorkerRunning(id, started).ShouldBeFalse($"Owned worker {id} survived cleanup.");
            }
            foreach (string container in containers)
            {
                (await DockerOutputAsync("ps", "-a", "--filter", $"name=^{container}$", "--format", "{{.ID}}"))
                    .ShouldBeEmpty("An owned backing container survived cleanup.");
            }
            (await DockerOutputAsync("network", "ls", "--filter", $"name=^hexalith-parties-dapr-mtls-{fixture.Session.Id}$", "--format", "{{.ID}}"))
                .ShouldBeEmpty();
            Directory.Exists(fixture.Session.Root).ShouldBeFalse();
            foreach (int port in fixture.Session.InternalGrpcPorts.Values.Append(fixture.Session.SentryPort)
                .Append(fixture.Session.PlacementPort).Append(fixture.Session.SchedulerPort))
            {
                var listener = new TcpListener(IPAddress.Loopback, port);
                try { listener.Start(); }
                finally { listener.Stop(); }
            }
            Console.WriteLine($"Live cleanup {mode}: {workers.Count} workers exited, {containers.Count} backing containers removed; "
                + $"host shutdown budget {hostShutdown.TotalSeconds}s; observed cleanup {(interruptedCleanup?.Elapsed ?? elapsed.Elapsed).TotalSeconds:F3}s.");
        }
        finally
        {
            startup.Cancel();
            await initialization.ConfigureAwait(true);
        }
    }

    private static Task<string> DockerOutputAsync(params string[] arguments)
        => DockerOutputAsync(TestContext.Current.CancellationToken, arguments);

    private static async Task<string> DockerOutputAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        using var inspection = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        inspection.CancelAfter(TimeSpan.FromSeconds(5));
        CancellationToken token = inspection.Token;
        token.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Docker inspection could not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(token);
        Task<string> error = process.StandardError.ReadToEndAsync(token);
        try
        {
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            await Task.WhenAll(output, error).ConfigureAwait(false);
            process.ExitCode.ShouldBe(0, await error.ConfigureAwait(false));
            return (await output.ConfigureAwait(false)).Trim();
        }
        finally
        {
            await ReapOwnedProcessAsync(process, terminate: !process.HasExited).ConfigureAwait(false);
            try { await Task.WhenAll(output, error).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }
    }

    private static async Task ReapOwnedProcessAsync(Process process, bool terminate)
    {
        if (terminate)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { }
        }
        using var reap = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await process.WaitForExitAsync(reap.Token).ConfigureAwait(false);
    }

}
