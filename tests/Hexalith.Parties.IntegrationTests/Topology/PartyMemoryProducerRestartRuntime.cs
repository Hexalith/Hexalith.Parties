using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Hexalith.Parties.IntegrationTests.HealthChecks;
using Shouldly;
using StackExchange.Redis;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Replaces only the owned producer after freezing and independently validating its persisted interruption phase.</summary>
internal sealed class PartyMemoryProducerRestartRuntime(PartiesAspireTopologyFixture fixture, string redisEndpoint,
    Uri sidecar, string apiToken, string generation, string exclusion, string evidenceDirectory)
{
    private readonly List<object> _observations = [];
    private bool _restartPending;
    private Task? _replacementStart;
    private CancellationTokenSource? _replacementStartDeadline;
    private (int Pid, long Start) _original;

    /// <summary>Gets completed, independently observed process replacements.</summary>
    internal int Count => _observations.Count;

    /// <summary>Provides a private controlled cancellation boundary after exact replacement observation.</summary>
    internal Action<string>? ReplacementObserved { get; init; }

    /// <summary>Requires old-process exit, a distinct healthy replacement, and unchanged backing and authority custody.</summary>
    internal async Task RestartAsync(string phase, JsonElement evidence, CancellationToken token)
    {
        DateTimeOffset began = DateTimeOffset.UtcNow;
        var elapsed = Stopwatch.StartNew();
        if (!OperatingSystem.IsLinux()) { throw new PlatformNotSupportedException("Producer phase freezing requires the owned Linux process."); }
        ResourceEvent original = State("memories");
        int pid = Pid(original);
        long started = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(pid)
            ?? throw new InvalidOperationException("The original producer identity is unavailable.");
        File.ReadAllText($"/proc/{pid}/environ").Split('\0').ShouldContain(PartiesAspireTopologyFixture.WorkerOwnershipVariable + "=" + fixture.WorkerOwnershipMarker);
        Dictionary<string, string> custody = Custody();
        Dictionary<string, string> binaries = ProducerFiles(pid);
        string manifest = Environment.GetEnvironmentVariable("Parties__Testing__PreparedRuntimeManifest")
            ?? throw new InvalidOperationException("Producer restart requires its exact prepared manifest.");
        string preparedHash = MemoryRuntimeIdentity.Hash(manifest);
        _restartPending = true;
        JsonElement persisted;
        try
        {
            // A rendezvous alone cannot prevent progress. SIGSTOP interrupts the exact
            // observed incarnation before independent persisted-phase checks below.
            persisted = await WhileStoppedAsync(pid, started, async () =>
            {
                JsonElement observed = await AssertPersistedPhaseAsync(phase, evidence, token).ConfigureAwait(false);
                var stopped = await fixture.App.ResourceCommands.ExecuteCommandAsync(original.ResourceId, KnownResourceCommands.StopCommand, token).ConfigureAwait(false);
                stopped.Success.ShouldBeTrue("The owned producer stop command must succeed.");
                while (PartiesAspireTopologyFixture.IsWorkerRunning(pid, started)) { await Task.Delay(25, token).ConfigureAwait(false); }
                return observed;
            }, token).ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "producer-restart-failure.json"), JsonSerializer.Serialize(new
            { Phase = phase, OriginalPid = pid, OriginalStart = started, Failure = failure.ToString(), RequestEvidence = evidence }), CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        _original = (pid, started);
        _replacementStartDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _replacementStart = StartReplacementAsync(_replacementStartDeadline.Token);
        await _replacementStart.WaitAsync(token).ConfigureAwait(false);
        ResourceEvent current;
        long? replacementStart;
        do
        {
            await Task.Delay(25, token).ConfigureAwait(false);
            current = State("memories");
            replacementStart = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(Pid(current));
        }
        while (replacementStart is null || Pid(current) == pid && replacementStart == started);
        fixture.CaptureOwnedWorkers(fixture.App);
        ReplacementObserved?.Invoke(phase);
        token.ThrowIfCancellationRequested();
        await fixture.App.ResourceNotifications.WaitForResourceHealthyAsync("memories", token).ConfigureAwait(false);
        using var health = new HttpClient { BaseAddress = fixture.App.GetEndpoint("memories", "http"), Timeout = TimeSpan.FromSeconds(5) };
        foreach (string route in new[] { "alive", "ready" })
        {
            bool healthy = false;
            while (!healthy)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using HttpResponseMessage response = await health.GetAsync(route, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    healthy = response.IsSuccessStatusCode;
                }
                catch (HttpRequestException) { /* A new PID precedes its bound HTTP listener. */ }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                { /* Retry a single health request timeout within the bounded phase budget. */ }
                if (!healthy) { await Task.Delay(50, token).ConfigureAwait(false); }
            }
        }
        PartiesAspireTopologyFixture.ReadWorkerStartIdentity(Pid(current)).ShouldBe(replacementStart);
        PartiesAspireTopologyFixture.IsWorkerRunning(pid, started).ShouldBeFalse();
        Custody().OrderBy(item => item.Key).ShouldBe(custody.OrderBy(item => item.Key));
        MemoryRuntimeIdentity.Hash(manifest).ShouldBe(preparedHash);
        ProducerFiles(Pid(current)).OrderBy(item => item.Key).ShouldBe(binaries.OrderBy(item => item.Key));
        EnvironmentValue(current, "Ingestion__OwnershipGeneration").ShouldBe(generation);
        EnvironmentValue(current, "Ingestion__OwnershipWriterExclusionEvidenceId").ShouldBe(exclusion);
        using IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(redisEndpoint).ConfigureAwait(false);
        (await connection.GetDatabase().StringGetAsync("ingestion-ownership-generation:" + generation).ConfigureAwait(false)).ToString().ShouldBe(exclusion);
        _observations.Add(new { Phase = phase, OriginalPid = pid, OriginalStart = started, OriginalExited = true,
            StartedAtUtc = began, ReplacementHealthyAtUtc = DateTimeOffset.UtcNow, ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
            ReplacementPid = Pid(current), ReplacementStart = replacementStart, Healthy = true,
            PreparedManifestSha256 = preparedHash, ProducerFiles = binaries, Generation = generation, WriterExclusionEvidenceId = exclusion,
            UnchangedCustody = custody, PersistedInterruption = persisted, RequestEvidence = evidence });
        await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "producer-restarts.json"), JsonSerializer.Serialize(_observations), token).ConfigureAwait(false);
        _restartPending = false;
        _replacementStartDeadline?.Dispose();
        _replacementStartDeadline = null;
        _replacementStart = null;
    }

    private async Task StartReplacementAsync(CancellationToken token)
    {
        var replacement = await fixture.App.ResourceCommands.ExecuteCommandAsync("memories", KnownResourceCommands.StartCommand, token).ConfigureAwait(false);
        replacement.Success.ShouldBeTrue("The owned producer replacement command must succeed.");
    }

    /// <summary>Stops an incomplete replacement before ordinary fixture cleanup waits on its replay-gate startup.</summary>
    internal async Task QuiesceIncompleteAsync()
    {
        if (!_restartPending) { return; }
        (int Pid, long? Start) Observe()
        {
            ResourceEvent resource = State("memories");
            int observed = Pid(resource);
            return (observed, PartiesAspireTopologyFixture.ReadWorkerStartIdentity(observed));
        }
        async Task QuiesceAsync(int pid, long start)
        {
            File.ReadAllText($"/proc/{pid}/environ").Split('\0').ShouldContain(PartiesAspireTopologyFixture.WorkerOwnershipVariable + "=" + fixture.WorkerOwnershipMarker);
            fixture.CaptureOwnedWorkers(fixture.App);
            await QuiesceProcessAsync(pid, start).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "incomplete-replacement-quiescence.json"), JsonSerializer.Serialize(new
            { Pid = pid, Start = start, Exited = true, BeforeBackingRemoval = true }), CancellationToken.None).ConfigureAwait(false);
        }
        try
        {
            if (_replacementStart is not null)
            {
                // Phase cancellation cannot cancel the already-requested start or
                // make an absent/stale PID proof that it will never materialize.
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(35));
                await SettleReplacementAsync(_replacementStart, Observe, _original, QuiesceAsync, cleanup.Token).ConfigureAwait(false);
            }
            else
            {
                (int pid, long? start) = Observe();
                if (start is not null) { await QuiesceAsync(pid, start.Value).ConfigureAwait(false); }
            }
            _restartPending = false;
        }
        finally
        {
            _replacementStartDeadline?.Dispose();
            _replacementStartDeadline = null;
        }
    }

    /// <summary>Settles a requested start and waits for its distinct incarnation under an independent cleanup budget.</summary>
    internal static async Task SettleReplacementAsync(Task startCommand, Func<(int Pid, long? Start)> observe,
        (int Pid, long Start) original, Func<int, long, Task> quiesce, CancellationToken cleanup)
    {
        Exception? commandFailure = null;
        try { await startCommand.WaitAsync(cleanup).ConfigureAwait(false); }
        catch (Exception failure) { commandFailure = failure; }
        try
        {
            while (true)
            {
                (int pid, long? start) = observe();
                if (start is not null && (pid != original.Pid || start != original.Start))
                {
                    await quiesce(pid, start.Value).ConfigureAwait(false);
                    break;
                }
                await Task.Delay(25, cleanup).ConfigureAwait(false);
            }
        }
        catch (Exception failure) when (commandFailure is not null)
        { throw new AggregateException("The requested producer start and exact-incarnation cleanup failed.", commandFailure, failure); }
        if (commandFailure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(commandFailure).Throw(); }
    }

    /// <summary>Completes bounded termination of an exact owned incarnation without signaling a recycled numeric PID.</summary>
    internal static async Task QuiesceProcessAsync(int pid, long start)
    {
        if (!PartiesAspireTopologyFixture.IsWorkerRunning(pid, start)) { return; }
        await SignalAsync(pid, start, "TERM", CancellationToken.None).ConfigureAwait(false);
        var grace = Stopwatch.StartNew();
        while (PartiesAspireTopologyFixture.IsWorkerRunning(pid, start) && grace.Elapsed < TimeSpan.FromSeconds(10))
        { await Task.Delay(25, CancellationToken.None).ConfigureAwait(false); }
        if (PartiesAspireTopologyFixture.IsWorkerRunning(pid, start))
        { await SignalAsync(pid, start, "KILL", CancellationToken.None).ConfigureAwait(false); }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (PartiesAspireTopologyFixture.IsWorkerRunning(pid, start)) { await Task.Delay(25, deadline.Token).ConfigureAwait(false); }
    }

    private async Task<JsonElement> AssertPersistedPhaseAsync(string phase, JsonElement evidence, CancellationToken token)
    {
        string tenant = evidence.GetProperty("tenant").GetString()!;
        string source = evidence.GetProperty("source").GetString()!;
        string workflow = evidence.GetProperty("workflow").GetString()!;
        string unit = evidence.GetProperty("unit").GetString()!;
        using IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(redisEndpoint).ConfigureAwait(false);
        IDatabase db = connection.GetDatabase();
        string inventory = (await db.StringGetAsync("source-inventory:" + Digest(JsonSerializer.Serialize(new[] { tenant, source }))).ConfigureAwait(false)).ToString();
        using JsonDocument document = JsonDocument.Parse(inventory);
        JsonElement entry = document.RootElement.GetProperty("entries").EnumerateArray()
            .Single(item => item.GetProperty("workflowInstanceId").GetString() == workflow);
        string authority = (await db.StringGetAsync("ingestion-unit-authority:" + Digest(JsonSerializer.Serialize(new[] { tenant, unit }))).ConfigureAwait(false)).ToString();
        string provenance = (await db.StringGetAsync("publication-key-provenance:" + tenant + ":" + unit).ConfigureAwait(false)).ToString();
        if (phase == "bounded-legacy-discovery")
        {
            authority.ShouldBeEmpty();
            provenance.ShouldBeEmpty();
        }
        else
        {
            using JsonDocument owner = JsonDocument.Parse(authority);
            owner.RootElement.GetProperty("Tenant").GetString().ShouldBe(tenant);
            owner.RootElement.GetProperty("Unit").GetString().ShouldBe(unit);
            owner.RootElement.GetProperty("Generation").GetString().ShouldBe(generation);
            owner.RootElement.GetProperty("WriterExclusionEvidenceId").GetString().ShouldBe(exclusion);
            owner.RootElement.GetProperty("ProducerBinding").GetString().ShouldBe(Digest(JsonSerializer.Serialize(new[] { workflow, source, entry.GetProperty("caseId").GetString()! })));
            if (phase != "admitted-empty-mappings") { provenance.ShouldBe(authority); }
        }
        using var http = new HttpClient { BaseAddress = sidecar, Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.Add("dapr-api-token", apiToken);
        JsonElement state = await http.GetFromJsonAsync<JsonElement>("v1.0/workflows/dapr/" + Uri.EscapeDataString(workflow), token).ConfigureAwait(false);
        string runtime = state.GetProperty("runtimeStatus").GetString()!;
        JsonElement? deletionWorkflow = null;
        bool? deletionReceiptPresent = null, memoryUnitPresent = null;
        if (phase is "admitted-empty-mappings" or "output-known")
        {
            runtime.ShouldBe("Running", StringCompareShould.IgnoreCase);
            entry.GetProperty("terminalVerified").GetBoolean().ShouldBeFalse();
            if (phase == "admitted-empty-mappings")
            {
                entry.GetProperty("memoryUnitId").ValueKind.ShouldBe(JsonValueKind.Null);
                (await db.KeyExistsAsync(tenant + ":mu:" + unit).ConfigureAwait(false)).ShouldBeFalse();
                (await db.KeyExistsAsync(tenant + ":vec:" + unit).ConfigureAwait(false)).ShouldBeFalse();
            }
            else
            {
                entry.GetProperty("memoryUnitId").GetString().ShouldBe(unit);
                (await db.KeyExistsAsync(tenant + ":mu:" + unit).ConfigureAwait(false)).ShouldBeTrue();
            }
        }
        else if (phase == "accepted-deletion")
        {
            runtime.ShouldBe("Completed", StringCompareShould.IgnoreCase);
            JsonElement deletion = await http.GetFromJsonAsync<JsonElement>("v1.0/workflows/dapr/memory-unit-delete-" + Uri.EscapeDataString(unit), token).ConfigureAwait(false);
            deletion.GetProperty("runtimeStatus").GetString().ShouldBe("Running", StringCompareShould.IgnoreCase);
            deletionReceiptPresent = await db.KeyExistsAsync("source-inventory-deleted:" + Digest(JsonSerializer.Serialize(new[] { tenant, unit }))).ConfigureAwait(false);
            memoryUnitPresent = await db.KeyExistsAsync(tenant + ":mu:" + unit).ConfigureAwait(false);
            deletionReceiptPresent.Value.ShouldBeFalse();
            memoryUnitPresent.Value.ShouldBeTrue();
            deletionWorkflow = JsonSerializer.SerializeToElement(deletion.EnumerateObject()
                .Where(property => property.Name is "instanceID" or "workflowName" or "runtimeStatus" or "createdAt" or "lastUpdatedAt")
                .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal));
        }
        else { runtime.ShouldBe("Completed", StringCompareShould.IgnoreCase); }
        return JsonSerializer.SerializeToElement(new { Inventory = document.RootElement.Clone(), Workflow = state, Authority = authority, Provenance = provenance,
            DeletionWorkflow = deletionWorkflow, DeletionReceiptPresent = deletionReceiptPresent, MemoryUnitPresent = memoryUnitPresent });
    }

    private Dictionary<string, string> Custody()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string name in new[] { "fixture-redis", "fixture-memories-search", "fixture-memories-graph" })
        {
            ResourceEvent resource = State(name);
            resource.Snapshot.State!.Text.ShouldBe("Running");
            string id = Convert.ToString(resource.Snapshot.Properties.Single(property => property.Name == "container.id").Value, CultureInfo.InvariantCulture)!;
            id.ShouldNotBeNullOrWhiteSpace();
            result.Add(name, id + "|" + fixture.App.GetEndpoint(name, "tcp"));
        }
        foreach (string name in new[] { "eventstore", "memories-domain-service", "eventstore-dapr-cli", "memories-dapr-cli", "memories-domain-service-dapr-cli" })
        {
            ResourceEvent resource = State(name);
            int pid = Pid(resource);
            long started = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(pid)
                ?? throw new InvalidOperationException("Owned backing workload is no longer running: " + name);
            result.Add(name, pid.ToString(CultureInfo.InvariantCulture) + ":" + started.ToString(CultureInfo.InvariantCulture));
        }
        return result;
    }

    private ResourceEvent State(string name) => fixture.App.ResourceNotifications.TryGetCurrentState(name, out ResourceEvent? value)
        ? value : throw new InvalidOperationException("Missing owned resource snapshot: " + name);

    private static int Pid(ResourceEvent state) => int.TryParse(Convert.ToString(state.Snapshot.Properties.FirstOrDefault(property => property.Name == "executable.pid")?.Value,
        CultureInfo.InvariantCulture), out int pid) ? pid : 0;

    private static string? EnvironmentValue(ResourceEvent state, string name) => state.Snapshot.EnvironmentVariables.Single(item => item.Name == name).Value;

    private static Dictionary<string, string> ProducerFiles(int pid)
    {
        string executable = File.ResolveLinkTarget($"/proc/{pid}/exe", returnFinalTarget: true)!.FullName;
        string directory = File.ResolveLinkTarget($"/proc/{pid}/cwd", returnFinalTarget: true)!.FullName;
        string[] assemblies = File.ReadAllText($"/proc/{pid}/cmdline").Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(argument => argument.EndsWith(".dll", StringComparison.Ordinal)).Select(argument => Path.GetFullPath(argument, directory)).ToArray();
        string host = assemblies.FirstOrDefault(path => Path.GetFileName(path) == "Hexalith.Memories.Server.dll")
            ?? Path.ChangeExtension(executable, ".dll");
        File.Exists(host).ShouldBeTrue("The exact running producer assembly must be observable.");
        return new[] { host, Path.ChangeExtension(host, ".deps.json"), Path.ChangeExtension(host, ".runtimeconfig.json") }
            .ToDictionary(path => path, MemoryRuntimeIdentity.Hash, StringComparer.Ordinal);
    }

    /// <summary>Binds direct owner-contract calls to the actual observed source-host implementation.</summary>
    internal static string ServerAssemblyPath(ResourceEvent resource)
        => ProducerFiles(Pid(resource)).Keys.Single(path => Path.GetFileName(path) == "Hexalith.Memories.Server.dll");

    /// <summary>Resumes the exact owned incarnation before caller return on evidence failure or cancellation.</summary>
    internal static async Task<T> WhileStoppedAsync<T>(int pid, long start, Func<Task<T>> operation, CancellationToken token,
        Func<string, CancellationToken, Task>? signalOverride = null)
    {
        Exception? primary = null;
        try
        {
            await (signalOverride?.Invoke("STOP", token) ?? SignalAsync(pid, start, "STOP", token)).ConfigureAwait(false);
            while (File.ReadAllText($"/proc/{pid}/stat").Split(')')[^1].TrimStart()[0] is not ('T' or 't'))
            { await Task.Delay(10, token).ConfigureAwait(false); }
            return await operation().ConfigureAwait(false);
        }
        catch (Exception failure) { primary = failure; throw; }
        finally
        {
            if (PartiesAspireTopologyFixture.IsWorkerRunning(pid, start))
            {
                try { await (signalOverride?.Invoke("CONT", CancellationToken.None) ?? SignalAsync(pid, start, "CONT", CancellationToken.None)).ConfigureAwait(false); }
                catch (Exception resume) when (primary is not null)
                { throw new AggregateException("Producer interruption and owned resume failed.", primary, resume); }
            }
        }
    }

    /// <summary>Signals through an exact Linux process handle and settles its bounded owned helper before returning.</summary>
    internal static async Task SignalAsync(int pid, long start, string signal, CancellationToken token)
    {
        // Open the Linux process handle before checking its start identity. A PID
        // recycled after that check cannot redirect a signal through the held fd.
        const string script = """
            import os,pathlib,signal,sys
            pid,expected=int(sys.argv[1]),int(sys.argv[2])
            fd=os.pidfd_open(pid)
            try:
                raw=pathlib.Path('/proc/'+str(pid)+'/stat').read_text()
                fields=raw[raw.rfind(')')+2:].split()
                if fields[0] in ('Z','X') or int(fields[19])!=expected:
                    raise RuntimeError('owned process incarnation mismatch')
                signal.pidfd_send_signal(fd,getattr(signal,'SIG'+sys.argv[3]))
            finally:
                os.close(fd)
            """;
        var command = new ProcessStartInfo("python3") { UseShellExecute = false, RedirectStandardError = true };
        command.ArgumentList.Add("-c");
        command.ArgumentList.Add(script);
        command.ArgumentList.Add(pid.ToString(CultureInfo.InvariantCulture));
        command.ArgumentList.Add(start.ToString(CultureInfo.InvariantCulture));
        command.ArgumentList.Add(signal);
        (int exitCode, string error) = await RunSignalHelperAsync(command, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        if (exitCode != 0 && PartiesAspireTopologyFixture.IsWorkerRunning(pid, start))
        { throw new InvalidOperationException("Owned pidfd signal failed: " + error); }
        if (exitCode != 0 && signal == "STOP") { throw new InvalidOperationException("The producer incarnation changed before its pidfd signal."); }
        token.ThrowIfCancellationRequested();
    }

    /// <summary>Bounds helper execution and stderr, killing and reaping the owned helper before surfacing a timeout.</summary>
    internal static async Task<(int ExitCode, string Error)> RunSignalHelperAsync(ProcessStartInfo command, TimeSpan limit,
        Action<int>? observePid = null)
    {
        using Process process = Process.Start(command) ?? throw new InvalidOperationException("The owned pidfd helper did not start.");
        using var deadline = new CancellationTokenSource(limit);
        Task<string> error = process.StandardError.ReadToEndAsync(deadline.Token);
        Exception? primary = null;
        try
        {
            observePid?.Invoke(process.Id);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            return (process.ExitCode, await error.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            var failure = new TimeoutException("The owned pidfd helper exceeded its independent deadline.");
            primary = failure;
            throw failure;
        }
        catch (Exception failure) { primary = failure; throw; }
        finally
        {
            try
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); }
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception reap) when (primary is not null)
            { throw new AggregateException("Owned pidfd helper and reaping failed.", primary, reap); }
            finally
            {
                deadline.Cancel();
                process.StandardError.Dispose();
                try { _ = await error.ConfigureAwait(false); }
                catch (Exception failure) when (failure is OperationCanceledException or ObjectDisposedException or IOException) { }
            }
        }
    }

    private static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
