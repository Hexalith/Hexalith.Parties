using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.Parties.IntegrationTests.HealthChecks;
using Shouldly;
using StackExchange.Redis;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Interrupts six disposable real runners after authenticated persisted checkpoints and independently audits disposal.</summary>
[Collection("Non-parallel")]
public sealed class PartyMemoryRunnerInterruptionTests
{
    /// <summary>Uses SIGINT against a verified pidfd; an interrupted child never becomes a successful business execution.</summary>
    [Fact(Timeout = 600000)]
    public Task SigintAtAdmittedEmptyMappings() => RunAsync("admitted-empty-mappings", TestContext.Current.CancellationToken);

    /// <summary>Interrupts actual nonterminal ingestion after output persistence.</summary>
    [Fact(Timeout = 600000)]
    public Task SigintAtOutputKnown() => RunAsync("output-known", TestContext.Current.CancellationToken);

    /// <summary>Interrupts an accepted, independently observed asynchronous deletion.</summary>
    [Fact(Timeout = 600000)]
    public Task SigintAtAcceptedDeletion() => RunAsync("accepted-deletion", TestContext.Current.CancellationToken);

    /// <summary>Retains original same-URI revision evidence during interruption.</summary>
    [Fact(Timeout = 600000)]
    public Task SigintAtSameUriRefresh() => RunAsync("same-uri-refresh", TestContext.Current.CancellationToken);

    /// <summary>Retains exact-unit and source evidence without granting historical completeness.</summary>
    [Fact(Timeout = 600000)]
    public Task SigintAtCrossSourceAuthority() => RunAsync("cross-source-authority", TestContext.Current.CancellationToken);

    /// <summary>Retains incomplete scoped historical discovery outside topology disposal.</summary>
    [Fact(Timeout = 600000)]
    public Task SigintAtBoundedLegacyDiscovery() => RunAsync("bounded-legacy-discovery", TestContext.Current.CancellationToken);

    /// <summary>Runs one owned checkpoint, optionally proving that changed persisted state refuses the actual runner signal.</summary>
    internal static async Task RunAsync(string phase, CancellationToken token, bool mutatePersistedState = false)
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PARTIES_MEMORIES_OWNED_RUNTIME") == "1", "Enable the owned real runtime qualification.");
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Actual runner interruption requires Linux pidfd support.");
        string directory = Path.Combine(Path.GetTempPath(), "runner-interruption-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsLinux()) { File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), nonce = Guid.NewGuid().ToString("N");
        string session = Guid.NewGuid().ToString("N")[..12], marker = Guid.NewGuid().ToString("N"), lineage = Guid.NewGuid().ToString("N");
        string sessionRoot = Path.Combine(Path.GetTempPath(), "hexalith-parties-topology-" + session);
        // This is recovery authority even if the runner crashes before writing its first receipt.
        PartyMemoryRunnerInterruptionCustody custody = PartyMemoryRunnerInterruptionCustody.Authorize(session, sessionRoot, marker, lineage);
        string configuration = Path.Combine(directory, "configuration.json");
        await File.WriteAllTextAsync(configuration, JsonSerializer.Serialize(new
        { Phase = phase, Secret = secret, Nonce = nonce, Directory = directory, SessionId = session, SessionRoot = sessionRoot, WorkerMarker = marker, LineageMarker = lineage }), token).ConfigureAwait(true);
        var sentinelStart = new ProcessStartInfo("python3") { UseShellExecute = false };
        sentinelStart.ArgumentList.Add("-c"); sentinelStart.ArgumentList.Add("import time; time.sleep(900)");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { typeof(PartyMemoryOwnedRuntimeTests).Assembly.Location, "-method",
            "*ActualCurrentSourceMemoriesRestartsAcrossFiveRecoveryBoundaries", "-parallelMode", "none", "-failSkips", "-result-xml", Path.Combine(directory, "interrupted-runner.xml") })
        { start.ArgumentList.Add(argument); }
        start.Environment[PartyMemoryRunnerInterruptionHandoff.ConfigurationVariable] = configuration;
        start.Environment[PartyMemoryRunnerInterruptionAudit.LineageVariable] = lineage;
        Process? sentinel = null, runner = null;
        int? runnerId = null, sentinelId = null, exitCode = null;
        long sentinelBirth = 0, birth = 0;
        using var outputCancellation = new CancellationTokenSource();
        Task<string> output = Task.FromResult(string.Empty), error = Task.FromResult(string.Empty);
        JsonElement? checkpoint = null, closure = null, emergencyClosure = null, liveAudit = null;
        var failures = new List<Exception>();
        bool signaled = false, recovered = false, sentinelUnaffected = false, changedPersistedStateRefused = false;
        var elapsed = Stopwatch.StartNew();
        try
        {
            sentinel = Process.Start(sentinelStart) ?? throw new InvalidOperationException("Unrelated sentinel did not start.");
            sentinelId = sentinel.Id;
            sentinelBirth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(sentinel.Id)!.Value;
            runner = Process.Start(start) ?? throw new InvalidOperationException("Disposable real runner did not start.");
            runnerId = runner.Id;
            output = runner.StandardOutput.ReadToEndAsync(outputCancellation.Token);
            error = runner.StandardError.ReadToEndAsync(outputCancellation.Token);
            birth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(runner.Id)!.Value;
            using var ready = CancellationTokenSource.CreateLinkedTokenSource(token);
            ready.CancelAfter(TimeSpan.FromMinutes(7));
            string path = Path.Combine(directory, "checkpoint.json");
            while (!File.Exists(path))
            {
                await custody.RefreshAsync(ready.Token).ConfigureAwait(true);
                if (runner.HasExited) { throw new InvalidOperationException("The real runner exited before its persisted checkpoint: " + directory); }
                await Task.Delay(100, ready.Token).ConfigureAwait(true);
            }
            checkpoint = PartyMemoryRunnerInterruptionHandoff.ReadBounded(path);
            PartyMemoryRunnerInterruptionHandoff.Validate(checkpoint.Value, phase, secret, nonce, runner.Id, birth, session);
            foreach (string identity in new[] { "Marker", "Root", "LineageMarker" })
            { checkpoint.Value.GetProperty("Topology").GetProperty(identity).GetString().ShouldBe(custody.Topology.GetProperty(identity).GetString()); }
            await custody.RefreshAsync(ready.Token).ConfigureAwait(true);
            liveAudit = await PartyMemoryRunnerInterruptionAudit.ValidateLiveAsync(checkpoint.Value, ready.Token).ConfigureAwait(true);
            if (mutatePersistedState)
            {
                JsonElement observed = checkpoint.Value.GetProperty("Observation").GetProperty("RequestEvidence");
                JsonElement config = PartyMemoryRunnerInterruptionHandoff.ReadBounded(Path.Combine(checkpoint.Value.GetProperty("PrivateRoot").GetString()!, "runtime.json"));
                using IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(config.GetProperty("ownershipRedisEndpoint").GetString()!).ConfigureAwait(true);
                IDatabase database = connection.GetDatabase();
                string key = "source-inventory:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[]
                    { observed.GetProperty("tenant").GetString()!, observed.GetProperty("source").GetString()! }))));
                byte[] original = (byte[])(await database.StringGetAsync(key).ConfigureAwait(true))!;
                original.ShouldNotBeEmpty();
                (await database.KeyTimeToLiveAsync(key).ConfigureAwait(true)).ShouldBeNull("The owned source inventory is durable before the mutation control.");
                JsonElement inventory = JsonSerializer.Deserialize<JsonElement>(original);
                var altered = inventory.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value.Clone(), StringComparer.Ordinal);
                altered.Add("runnerInterruptionMutation", nonce);
                try
                {
                    (await database.StringSetAsync(key, JsonSerializer.SerializeToUtf8Bytes(altered)).ConfigureAwait(true)).ShouldBeTrue();
                    InvalidDataException rejection = await Should.ThrowAsync<InvalidDataException>(() => PartyMemoryRunnerInterruptionHandoff.SignalRunnerAsync(
                        checkpoint.Value, phase, secret, nonce, runner.Id, birth, session, ready.Token)).ConfigureAwait(true);
                    rejection.Message.ShouldContain("Frozen persisted phase changed");
                    runner.HasExited.ShouldBeFalse();
                    PartiesAspireTopologyFixture.ReadWorkerStartIdentity(runner.Id).ShouldBe(birth);
                    changedPersistedStateRefused = true;
                }
                finally
                {
                    (await database.StringSetAsync(key, original).ConfigureAwait(true)).ShouldBeTrue();
                    ((byte[])(await database.StringGetAsync(key).ConfigureAwait(true))!).ShouldBe(original);
                }
            }
            await PartyMemoryRunnerInterruptionHandoff.SignalRunnerAsync(checkpoint.Value, phase, secret, nonce, runner.Id, birth, session, ready.Token).ConfigureAwait(true);
            signaled = true;
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            await runner.WaitForExitAsync(cleanup.Token).ConfigureAwait(true);
            runner.ExitCode.ShouldNotBe(0, "Interruption must remain visible in the actual runner result.");
            string evidence = checkpoint.Value.GetProperty("PrivateRoot").GetString()!;
            JsonElement ownership = PartyMemoryRunnerInterruptionHandoff.ReadBounded(Path.Combine(evidence, "ownership.json"));
            ownership.GetProperty("OwnedTopologyDisposed").GetBoolean().ShouldBeTrue();
            ownership.GetProperty("CleanupFailure").ValueKind.ShouldBe(JsonValueKind.Null);
            if (phase == "admitted-empty-mappings")
            {
                liveAudit.Value.GetProperty("PendingReplacementVerified").GetBoolean().ShouldBeTrue();
                JsonElement quiescence = PartyMemoryRunnerInterruptionHandoff.ReadBounded(Path.Combine(evidence, "incomplete-replacement-quiescence.json"));
                JsonElement pending = checkpoint.Value.GetProperty("Observation").GetProperty("PendingReplacement");
                quiescence.GetProperty("Pid").GetInt32().ShouldBe(pending.GetProperty("ReplacementPid").GetInt32());
                quiescence.GetProperty("Start").GetInt64().ShouldBe(pending.GetProperty("ReplacementStart").GetInt64());
                quiescence.GetProperty("Exited").GetBoolean().ShouldBeTrue();
                quiescence.GetProperty("BeforeBackingRemoval").GetBoolean().ShouldBeTrue();
            }
            string primary = ownership.GetProperty("PrimaryFailure").GetString()!;
            (primary.Contains("OperationCanceledException", StringComparison.Ordinal) || primary.Contains("TaskCanceledException", StringComparison.Ordinal)).ShouldBeTrue();
            closure = await PartyMemoryRunnerInterruptionAudit.VerifyClosedAsync(custody.Topology).ConfigureAwait(true);
            closure.Value.GetProperty("Closed").GetBoolean().ShouldBeTrue("Independent closure receipt: " + directory);
            PartiesAspireTopologyFixture.IsWorkerRunning(sentinel.Id, sentinelBirth).ShouldBeTrue();
        }
        catch (Exception failure) { failures.Add(failure); }
        finally
        {
            // Each action has its own failure boundary; termination/reaping can never skip recovery, logs, sentinel disposal or receipts.
            await AttemptAsync(async () =>
            {
                if (runner is not null && !runner.HasExited)
                {
                    if (!signaled) { await PartyMemoryProducerRestartRuntime.SignalAsync(runner.Id, birth, "INT", CancellationToken.None).ConfigureAwait(true); }
                    using var grace = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await runner.WaitForExitAsync(grace.Token).ConfigureAwait(true);
                }
            }).ConfigureAwait(true);
            await AttemptAsync(async () =>
            {
                if (runner is not null && !runner.HasExited)
                { await PartyMemoryProducerRestartRuntime.SignalAsync(runner.Id, birth, "KILL", CancellationToken.None).ConfigureAwait(true); }
            }).ConfigureAwait(true);
            await AttemptAsync(async () =>
            {
                if (runner is not null)
                {
                    using var reap = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await runner.WaitForExitAsync(reap.Token).ConfigureAwait(true);
                    exitCode = runner.ExitCode;
                }
            }).ConfigureAwait(true);
            await AttemptAsync(() => custody.RefreshAsync()).ConfigureAwait(true);
            if (failures.Count != 0)
            {
                await AttemptAsync(async () =>
                {
                    await custody.RecoverAsync().ConfigureAwait(true);
                    recovered = true;
                    emergencyClosure = await PartyMemoryRunnerInterruptionAudit.VerifyClosedAsync(custody.Topology).ConfigureAwait(true);
                    emergencyClosure.Value.GetProperty("Closed").GetBoolean().ShouldBeTrue("Emergency reconciliation could not close this failed owned run.");
                }).ConfigureAwait(true);
            }
            await AttemptAsync(async () =>
            {
                if (runner is null) { return; }
                string stdout = await PartyMemoryRunnerInterruptionProcess.DrainAsync(runner, output, error, outputCancellation, TimeSpan.FromSeconds(3)).ConfigureAwait(true);
                await File.WriteAllTextAsync(Path.Combine(directory, "runner.log"), stdout + await error.ConfigureAwait(true), CancellationToken.None).ConfigureAwait(true);
            }).ConfigureAwait(true);
            sentinelUnaffected = sentinel is not null && PartiesAspireTopologyFixture.IsWorkerRunning(sentinel.Id, sentinelBirth);
            await AttemptAsync(async () =>
            {
                if (sentinel is not null)
                {
                    if (!sentinel.HasExited) { sentinel.Kill(); }
                    using var reap = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await sentinel.WaitForExitAsync(reap.Token).ConfigureAwait(true);
                }
            }).ConfigureAwait(true);
            sentinel?.Dispose(); runner?.Dispose();
            await AttemptAsync(() => File.WriteAllTextAsync(Path.Combine(directory, "external-custody.json"), JsonSerializer.Serialize(custody.Topology), CancellationToken.None)).ConfigureAwait(true);
            await AttemptAsync(() => File.WriteAllTextAsync(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new
            {
                Phase = phase, RunnerPid = runnerId, RunnerStart = birth, ExitCode = exitCode,
                Signal = signaled ? "SIGINT via verified held pidfd" : null, ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
                Qualified = failures.Count == 0, InterruptedExecution = signaled && exitCode is not null && exitCode != 0,
                LiveAudit = liveAudit, Closure = closure, SentinelPid = sentinelId, SentinelStart = sentinelBirth,
                SentinelUnaffected = sentinelUnaffected, Failures = failures.Select(failure => failure.ToString()).ToArray(),
                EmergencyRecovery = recovered, EmergencyClosure = emergencyClosure,
                ChangedPersistedStateRefusedRunnerSignal = changedPersistedStateRefused,
            }), CancellationToken.None)).ConfigureAwait(true);
        }
        if (failures.Count != 0) { throw new AggregateException("Runner interruption qualification failed; emergency cleanup never grants acceptance.", failures); }

        async Task AttemptAsync(Func<Task> operation)
        {
            try { await operation().ConfigureAwait(true); }
            catch (Exception failure) { failures.Add(failure); }
        }
    }
}
