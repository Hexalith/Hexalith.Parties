using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.Parties.IntegrationTests.HealthChecks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Opt-in live DCP stop timing for the ordinary profile's <c>dapr-subreaper</c> CLI launcher.</summary>
/// <remarks>Never enabled in CI: it starts the default ordinary fixture under the local Aspire DCP, then records how
/// each <c>*-dapr-cli</c> launcher, its CLI and its runtime exit when the fixture stops them, against the fixture's
/// 10 s launcher grace and 30 s quiescence budget. A readiness failure still exercises the same owned stop.</remarks>
[Collection(NonParallelCollection.Name)]
public sealed class PartiesOrdinaryLauncherStopTimingTests
{
    private const string OptInVariable = "PARTIES_ORDINARY_TOPOLOGY_TESTS";
    private const string TimingPathVariable = "PARTIES_ORDINARY_TOPOLOGY_TIMING_PATH";
    private static readonly TimeSpan s_launcherGrace = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_quiescenceBudget = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_samplingInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>Stops a live ordinary topology and records per-worker exit timing and the launcher exit status.</summary>
    /// <returns>A task that completes after the owned topology has been stopped and its timing recorded.</returns>
    [Fact]
    public async Task OrdinaryLauncher_LiveDcpStop_RecordsWorkerExitTimingWithinQuiescenceBudgetAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Skip($"Set {OptInVariable}=true to record ordinary-profile launcher stop timing under the local DCP; it is never set in CI.");
        }

        // An explicit opt-in must never become a qualifying skip.
        OperatingSystem.IsLinux().ShouldBeTrue("The dapr-subreaper launcher and its process observations are Linux-only.");

        var fixture = new PartiesAspireTopologyFixture();
        var workers = new ConcurrentDictionary<int, (long Started, string Role, string Resource)>();
        var launchers = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var skipped = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        var exits = new ConcurrentDictionary<int, double>();
        var stopping = new ConcurrentDictionary<string, double>(StringComparer.Ordinal);
        var terminal = new ConcurrentDictionary<string, (string? State, int? ExitCode, double ObservedMs)>(StringComparer.Ordinal);
        using var observation = new CancellationTokenSource();
        Stopwatch? stop = null;
        Task? sampler = null;
        Task? watcher = null;
        Task? disposal = null;
        string? daprPath = null;
        string? initializationFailure = null;
        string? observerFailure = null;
        string? runFailure = null;
        double? quiescedMs = null;
        double? totalStopMs = null;
        bool available = false;
        string? unavailableReason = null;
        string initializationPhase = fixture.InitializationPhase;

        void BeginStop(DistributedApplication application)
        {
            if (stop is not null) { return; }
            daprPath = application.Services.GetService<IOptions<DaprOptions>>()?.Value.DaprPath;
            CaptureLaunchers(application, daprPath, workers, launchers, skipped);
            stop = Stopwatch.StartNew();
            Stopwatch clock = stop;
            sampler = SampleExitsAsync(workers, exits, clock, observation.Token);
            watcher = WatchCliStatesAsync(application, stopping, terminal, clock, observation.Token);
        }

        fixture.ObserveInitializationFailureAsync = failure =>
        {
            initializationFailure = failure.ToString();
            // The fixture tears down a partial startup right after this observation, and a throwing observer would
            // skip that teardown, so nothing may escape; time that stop the same way when the model was built.
            try { BeginStop(fixture.App); }
            catch (Exception exception) { observerFailure = exception.ToString(); }
            return Task.CompletedTask;
        };
        fixture.ObserveQuiescenceAsync = (_, _) =>
        {
            quiescedMs = stop?.Elapsed.TotalMilliseconds;
            return Task.CompletedTask;
        };

        await using var lifetime = fixture.ConfigureAwait(true);
        try
        {
            Task initialization = fixture.InitializeAsync(TestContext.Current.CancellationToken).AsTask();
            await initialization.WaitAsync(TimeSpan.FromMinutes(7), TestContext.Current.CancellationToken).ConfigureAwait(true);
            available = fixture.IsAvailable;
            unavailableReason = fixture.UnavailableReason;
            initializationPhase = fixture.InitializationPhase;
            if (available)
            {
                BeginStop(fixture.App);
                disposal = fixture.DisposeAsync().AsTask();
                await disposal.WaitAsync(TimeSpan.FromSeconds(120), TestContext.Current.CancellationToken).ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            runFailure = exception.ToString();
            throw;
        }
        finally
        {
            // Evidence is written on every path, including a failed or timed-out stop, before any further wait.
            totalStopMs = stop?.Elapsed.TotalMilliseconds;
            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(true);
            await observation.CancelAsync().ConfigureAwait(true);
            if (sampler is not null) { await sampler.ConfigureAwait(true); }
            if (watcher is not null) { await watcher.ConfigureAwait(true); }
            string timing = JsonSerializer.Serialize(BuildRecord(available, unavailableReason, initializationFailure, observerFailure, runFailure,
                initializationPhase, daprPath, workers, launchers, skipped, exits, stopping, terminal, totalStopMs, quiescedMs),
                new JsonSerializerOptions { WriteIndented = true });
            TestContext.Current.TestOutputHelper?.WriteLine(timing);
            Console.WriteLine(timing);
            if (Environment.GetEnvironmentVariable(TimingPathVariable) is { Length: > 0 } timingPath)
            {
                await File.WriteAllTextAsync(timingPath, timing + "\n", CancellationToken.None).ConfigureAwait(true);
            }

            // A failed observation must never abandon the owned cleanup.
            if (disposal is not null) { await disposal.ConfigureAwait(true); }
        }

        stop.ShouldNotBeNull($"The ordinary topology never reached an owned stop: {unavailableReason}");
        PartiesDaprCliSubreaper.IsLauncher(daprPath).ShouldBeTrue($"The ordinary profile's Dapr path is not the subreaper launcher: {daprPath}");
        launchers.ShouldNotBeEmpty($"No *-dapr-cli launcher was running when the stop began: {unavailableReason}");
        if (available)
        {
            // An uncaptured launcher would escape the survivor, exit-status and budget assertions below.
            skipped.ShouldBeEmpty("Modeled *-dapr-cli launchers were not captured: "
                + string.Join("; ", skipped.Select(item => item.Key + ": " + item.Value)));
        }

        foreach ((int id, (long started, string role, string resource)) in workers)
        {
            PartiesAspireTopologyFixture.IsWorkerRunning(id, started).ShouldBeFalse($"Owned {role} {id} of {resource} survived the stop.");
            exits.TryGetValue(id, out double exited).ShouldBeTrue($"No exit was observed for owned {role} {id} of {resource}.");
            TimeSpan.FromMilliseconds(exited).ShouldBeLessThanOrEqualTo(s_quiescenceBudget,
                $"Owned {role} {id} of {resource} exited after the quiescence budget.");
        }

        foreach (string resource in launchers.Keys)
        {
            // 143 is the launcher's own exit after forwarding SIGTERM and reaping every child; 137 is DCP's SIGKILL.
            terminal.TryGetValue(resource, out (string? State, int? ExitCode, double ObservedMs) reported).ShouldBeTrue(
                $"DCP reported no exit for launcher {resource}.");
            reported.ExitCode.ShouldBe(143, $"Launcher {resource} did not exit after forwarding SIGTERM and reaping its children.");
        }

        quiescedMs.ShouldNotBeNull("The fixture reached a stop but never observed owned-worker quiescence.");
        TimeSpan.FromMilliseconds(quiescedMs.Value).ShouldBeLessThanOrEqualTo(s_quiescenceBudget);
    }

    private static void CaptureLaunchers(DistributedApplication application, string? daprPath,
        ConcurrentDictionary<int, (long Started, string Role, string Resource)> workers, ConcurrentDictionary<string, int> launchers,
        ConcurrentDictionary<string, string> skipped)
    {
        DistributedApplicationModel model = application.Services.GetRequiredService<DistributedApplicationModel>();
        IEnumerable<string> names = model.Resources.SelectMany(resource => resource.Annotations.OfType<DaprSidecarAnnotation>())
            .Select(sidecar => sidecar.Sidecar.Name + "-cli").Distinct(StringComparer.Ordinal);
        foreach (string name in names)
        {
            if (!application.ResourceNotifications.TryGetCurrentState(name, out ResourceEvent? state))
            {
                skipped[name] = "no resource state";
                continue;
            }

            object? value = state.Snapshot.Properties.FirstOrDefault(property => property.Name == "executable.pid")?.Value;
            if (value is null || !int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out int id))
            {
                skipped[name] = "no executable.pid in state " + (state.Snapshot.State?.Text ?? "<none>");
                continue;
            }

            if (PartiesAspireTopologyFixture.ReadWorkerStartIdentity(id) is not long started)
            {
                skipped[name] = $"pid {id} has no live start identity";
                continue;
            }

            if (daprPath is null || !PartiesDaprCliSubreaper.IsRunning(id, daprPath))
            {
                skipped[name] = $"pid {id} does not run the configured launcher {daprPath ?? "<none>"}";
                continue;
            }

            launchers[name] = id;
            workers[id] = (started, "launcher", name);
            CaptureDescendants(id, name, workers);
        }
    }

    private static void CaptureDescendants(int parent, string resource, ConcurrentDictionary<int, (long Started, string Role, string Resource)> workers)
    {
        string[] tasks;
        try { tasks = Directory.GetDirectories($"/proc/{parent}/task"); }
        catch (IOException) { return; }

        foreach (string task in tasks)
        {
            string[] children;
            try { children = File.ReadAllText(Path.Combine(task, "children")).Split(' ', StringSplitOptions.RemoveEmptyEntries); }
            catch (IOException) { continue; }

            foreach (string child in children)
            {
                int id = int.Parse(child, CultureInfo.InvariantCulture);
                try
                {
                    if (PartiesAspireTopologyFixture.ReadWorkerStartIdentity(id) is not long started) { continue; }
                    string name = File.ReadAllText($"/proc/{id}/comm").Trim();
                    workers[id] = (started, name switch { "daprd" => "daprd", "dapr" => "cli", _ => name }, resource);
                }
                catch (IOException)
                {
                    // This child exited mid-walk (ENOENT or ESRCH); keep walking its siblings.
                    continue;
                }

                CaptureDescendants(id, resource, workers);
            }
        }
    }

    private static async Task SampleExitsAsync(ConcurrentDictionary<int, (long Started, string Role, string Resource)> workers,
        ConcurrentDictionary<int, double> exits, Stopwatch clock, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(s_samplingInterval);
        try
        {
            do
            {
                foreach ((int id, (long started, _, _)) in workers)
                {
                    if (!exits.ContainsKey(id) && !PartiesAspireTopologyFixture.IsWorkerRunning(id, started))
                    {
                        exits.TryAdd(id, clock.Elapsed.TotalMilliseconds);
                    }
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private static async Task WatchCliStatesAsync(DistributedApplication application, ConcurrentDictionary<string, double> stopping,
        ConcurrentDictionary<string, (string? State, int? ExitCode, double ObservedMs)> terminal, Stopwatch clock, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (ResourceEvent update in application.ResourceNotifications.WatchAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!update.Resource.Name.EndsWith("-dapr-cli", StringComparison.Ordinal)) { continue; }
                string? state = update.Snapshot.State?.Text;
                if (state == KnownResourceStates.Stopping) { stopping.TryAdd(update.Resource.Name, clock.Elapsed.TotalMilliseconds); }
                if (state == KnownResourceStates.Exited || state == KnownResourceStates.Finished || update.Snapshot.ExitCode is not null)
                {
                    terminal[update.Resource.Name] = (state, update.Snapshot.ExitCode, clock.Elapsed.TotalMilliseconds);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) { }
    }

    private static object BuildRecord(bool available, string? unavailableReason, string? initializationFailure, string? observerFailure,
        string? runFailure, string initializationPhase, string? daprPath,
        ConcurrentDictionary<int, (long Started, string Role, string Resource)> workers, ConcurrentDictionary<string, int> launchers,
        ConcurrentDictionary<string, string> skipped, ConcurrentDictionary<int, double> exits, ConcurrentDictionary<string, double> stopping,
        ConcurrentDictionary<string, (string? State, int? ExitCode, double ObservedMs)> terminal, double? totalStopMs, double? quiescedMs)
    {
        static double? Exit(ConcurrentDictionary<int, double> exits, int id) => exits.TryGetValue(id, out double value) ? Math.Round(value, 1) : null;
        static string? Bounded(string? text) => text is { Length: > 8000 } ? text[..8000] : text;

        var resources = launchers.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair =>
        {
            int launcher = pair.Value;
            double? launcherExit = Exit(exits, launcher);
            double? stopIssued = stopping.TryGetValue(pair.Key, out double issued) ? Math.Round(issued, 1) : null;
            bool reported = terminal.TryGetValue(pair.Key, out (string? State, int? ExitCode, double ObservedMs) observed);
            string? state = reported ? observed.State : null;
            int? exitCode = reported ? observed.ExitCode : null;
            var descendants = workers.Where(worker => worker.Value.Resource == pair.Key && worker.Key != launcher)
                .OrderBy(worker => worker.Key)
                .Select(worker => new { role = worker.Value.Role, pid = worker.Key, exitMs = Exit(exits, worker.Key) })
                .ToArray();
            double?[] runtimeExits = [.. descendants.Where(item => item.role == "daprd").Select(item => item.exitMs)];
            return new
            {
                resource = pair.Key,
                launcherPid = launcher,
                stopIssuedMs = stopIssued,
                launcherExitMs = launcherExit,
                launcherExitAfterStopIssuedMs = launcherExit is double exited && stopIssued is double started ? Math.Round(exited - started, 1) : (double?)null,
                dcpState = state,
                launcherExitCode = exitCode,
                launcherExit = exitCode switch
                {
                    143 => "143 (SIGTERM forwarded, every child reaped)",
                    137 => "137 (SIGKILL)",
                    null => "not reported",
                    _ => exitCode.Value.ToString(CultureInfo.InvariantCulture),
                },
                descendants,
                // Unknown unless every exit was observed; true only when a runtime exited after its launcher.
                daprdOutlivedLauncher = runtimeExits.Length == 0 || launcherExit is null || runtimeExits.Any(runtime => runtime is null)
                    ? (bool?)null
                    : runtimeExits.Any(runtime => runtime > launcherExit),
            };
        }).ToArray();
        return new
        {
            schema = "parties.ordinary-launcher-stop-timing.v1",
            observedAtUtc = DateTimeOffset.UtcNow,
            startup = new
            {
                available,
                phase = initializationPhase,
                reason = unavailableReason,
                failure = Bounded(initializationFailure),
                observerFailure = Bounded(observerFailure),
            },
            runFailure = Bounded(runFailure),
            daprPathIsLauncher = PartiesDaprCliSubreaper.IsLauncher(daprPath),
            launcherGraceSeconds = s_launcherGrace.TotalSeconds,
            quiescenceBudgetSeconds = s_quiescenceBudget.TotalSeconds,
            exitSamplingResolutionMs = s_samplingInterval.TotalMilliseconds,
            totalStopMs = totalStopMs is double total ? Math.Round(total, 1) : (double?)null,
            quiescedMs = quiescedMs is double quiesced ? Math.Round(quiesced, 1) : (double?)null,
            lastWorkerExitMs = exits.IsEmpty ? (double?)null : Math.Round(exits.Values.Max(), 1),
            skippedLaunchers = skipped.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new { resource = item.Key, reason = item.Value }).ToArray(),
            survivors = workers.Where(worker => PartiesAspireTopologyFixture.IsWorkerRunning(worker.Key, worker.Value.Started))
                .Select(worker => new { role = worker.Value.Role, pid = worker.Key, resource = worker.Value.Resource }).ToArray(),
            resources,
        };
    }
}
