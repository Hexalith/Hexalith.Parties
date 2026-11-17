using System.Diagnostics;
using System.Text.Json;

using Hexalith.Parties.IntegrationTests.HealthChecks;
using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Exercises durable partial custody, exact disappearance and aggregate scan cancellation.</summary>
[Collection("Non-parallel")]
public sealed class PartyMemoryRunnerInterruptionDiscoveryTests
{
    /// <summary>Failure to publish a captured birth must propagate before another discovery command can run.</summary>
    [Fact]
    public async Task DurablePublisherFailureCannotBeMistakenForATransientProcRace()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux durable process custody control.");
        string marker = Guid.NewGuid().ToString("N");
        using Process worker = Launch(marker);
        JsonElement authority = PartyMemoryRunnerInterruptionAudit.Authorize("publisher", Path.Combine(Path.GetTempPath(), "unused-publisher-root"), marker, string.Empty);
        int commands = 0;
        try
        {
            IOException failure = await Should.ThrowAsync<IOException>(() => PartyMemoryRunnerInterruptionAudit.DiscoverAsync(authority,
                _ => throw new IOException("controlled durable publisher failure"), TestContext.Current.CancellationToken,
                (_, _) => { commands++; return Task.FromResult(string.Empty); })).ConfigureAwait(true);
            failure.Message.ShouldBe("controlled durable publisher failure");
            commands.ShouldBe(0);
        }
        finally { await ReapAsync(worker).ConfigureAwait(true); }
    }

    /// <summary>A late failed network scan cannot discard an earlier container, volume or process birth.</summary>
    [Fact]
    public async Task PartialDiscoverySurvivesLaterFailureAndContainerDisappearance()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux process custody control.");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PARTIES_MEMORIES_OWNED_RUNTIME") == "1", "Enable owned Docker interruption controls.");
        string session = Guid.NewGuid().ToString("N")[..12], marker = Guid.NewGuid().ToString("N");
        string root = Path.Combine(Path.GetTempPath(), "hexalith-parties-topology-" + session);
        Directory.CreateDirectory(root);
        var custody = PartyMemoryRunnerInterruptionCustody.Authorize(session, root, marker, string.Empty);
        using Process worker = Launch(marker);
        using Process foreign = Launch(Guid.NewGuid().ToString("N"));
        string container = string.Empty;
        try
        {
            container = (await PartyMemoryRunnerInterruptionAudit.DockerAsync("run", "-d", "--label", "hexalith.parties.test.owner=" + marker,
                "--label", "hexalith.parties.test.session=" + session, "redis:7.4-alpine").ConfigureAwait(true)).Trim();
            await Should.ThrowAsync<IOException>(() => custody.RefreshAsync(TestContext.Current.CancellationToken, (token, args) =>
                args[0] == "network" ? throw new IOException("controlled later discovery failure") : PartyMemoryRunnerInterruptionAudit.DockerAsync(token, args))).ConfigureAwait(true);
            custody.Topology.GetProperty("Processes").EnumerateArray().ShouldContain(item => item.GetProperty("Pid").GetInt32() == worker.Id);
            custody.Topology.GetProperty("Containers").EnumerateArray().Single().GetProperty("Id").GetString().ShouldBe(container);
            string volume = custody.Topology.GetProperty("AnonymousVolumes").EnumerateArray().Single().GetProperty("Name").GetString()!;
            _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("rm", "-f", container).ConfigureAwait(true);
            await custody.RefreshAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            custody.Topology.GetProperty("AnonymousVolumes").EnumerateArray().Single().GetProperty("Name").GetString().ShouldBe(volume);
            await custody.RecoverAsync().ConfigureAwait(true);
            JsonElement closed = await PartyMemoryRunnerInterruptionAudit.VerifyClosedAsync(custody.Topology).ConfigureAwait(true);
            closed.GetProperty("Closed").GetBoolean().ShouldBeTrue();
            foreign.HasExited.ShouldBeFalse();
        }
        finally
        {
            await custody.RecoverAsync().ConfigureAwait(true);
            await ReapAsync(worker).ConfigureAwait(true);
            await ReapAsync(foreign).ConfigureAwait(true);
        }
    }

    /// <summary>Only a fresh exact absence query may resolve a failed inspection.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ListedObjectDisappearanceIsDistinctFromInspectionFailure(bool disappear)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux Docker custody control.");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PARTIES_MEMORIES_OWNED_RUNTIME") == "1", "Enable owned Docker interruption controls.");
        string session = Guid.NewGuid().ToString("N")[..12], marker = Guid.NewGuid().ToString("N");
        string root = Path.Combine(Path.GetTempPath(), "hexalith-parties-topology-" + session);
        var custody = PartyMemoryRunnerInterruptionCustody.Authorize(session, root, marker, string.Empty);
        string container = (await PartyMemoryRunnerInterruptionAudit.DockerAsync("run", "-d", "--label", "hexalith.parties.test.owner=" + marker,
            "--label", "hexalith.parties.test.session=" + session, "redis:7.4-alpine").ConfigureAwait(true)).Trim();
        try
        {
            async Task<string> CommandAsync(CancellationToken token, string[] args)
            {
                if (args[0] == "inspect" && args[1] == container)
                {
                    if (disappear) { _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync(token, "rm", "-f", "-v", container).ConfigureAwait(false); }
                    throw new IOException("controlled inspection race");
                }
                return await PartyMemoryRunnerInterruptionAudit.DockerAsync(token, args).ConfigureAwait(false);
            }
            if (disappear) { await custody.RefreshAsync(TestContext.Current.CancellationToken, CommandAsync).ConfigureAwait(true); }
            else { await Should.ThrowAsync<InvalidDataException>(() => custody.RefreshAsync(TestContext.Current.CancellationToken, CommandAsync)).ConfigureAwait(true); }
        }
        finally
        {
            string present = await PartyMemoryRunnerInterruptionAudit.DockerAsync("ps", "-aq", "--no-trunc").ConfigureAwait(true);
            if (present.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(container, StringComparer.Ordinal))
            { _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("rm", "-f", "-v", container).ConfigureAwait(true); }
        }
    }

    /// <summary>Cancellation reaches a slow command, reaps its supervisor and retains already captured process custody.</summary>
    [Fact]
    public async Task LinkedScanCancellationAwaitsSlowCommandAndKeepsPartialCustody()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux bounded scan control.");
        string marker = Guid.NewGuid().ToString("N");
        using Process worker = Launch(marker);
        var custody = PartyMemoryRunnerInterruptionCustody.Authorize("scan", Path.Combine(Path.GetTempPath(), "unused-scan-root"), marker, string.Empty);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int helper = 0; long birth = 0;
        var elapsed = Stopwatch.StartNew();
        try
        {
            await Should.ThrowAsync<AggregateException>(() => custody.RefreshAsync(cancellation.Token, (token, _) =>
            {
                var command = new ProcessStartInfo("python3") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                command.ArgumentList.Add("-c"); command.ArgumentList.Add("import time; time.sleep(120)");
                return PartyMemoryRunnerInterruptionProcess.RunAsync(command, TimeSpan.FromSeconds(10), child =>
                {
                    helper = child.Id; birth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(helper)!.Value;
                    cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
                }, token: token);
            })).ConfigureAwait(true);
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
            PartiesAspireTopologyFixture.IsWorkerRunning(helper, birth).ShouldBeFalse();
            custody.Topology.GetProperty("Processes").EnumerateArray().ShouldContain(item => item.GetProperty("Pid").GetInt32() == worker.Id);
        }
        finally { await ReapAsync(worker).ConfigureAwait(true); }
    }

    /// <summary>A Docker discovery outage cannot bypass cleanup of an authenticated worker.</summary>
    [Fact]
    public async Task RecoveryQuiescesKnownWorkersWhenBothDiscoveryScansFail()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux failed discovery recovery control.");
        string marker = Guid.NewGuid().ToString("N");
        using Process worker = Launch(marker);
        using Process foreign = Launch(Guid.NewGuid().ToString("N"));
        long birth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(worker.Id)!.Value;
        JsonElement authority = PartyMemoryRunnerInterruptionAudit.Authorize("outage", Path.Combine(Path.GetTempPath(), "unused-outage-root"), marker, string.Empty);
        try
        {
            await Should.ThrowAsync<AggregateException>(() => PartyMemoryRunnerInterruptionAudit.RecoverAsync(authority,
                command: (_, _) => throw new IOException("controlled Docker outage"))).ConfigureAwait(true);
            PartiesAspireTopologyFixture.IsWorkerRunning(worker.Id, birth).ShouldBeFalse();
            foreign.HasExited.ShouldBeFalse();
        }
        finally { await ReapAsync(worker).ConfigureAwait(true); await ReapAsync(foreign).ConfigureAwait(true); }
    }

    /// <summary>Recovery reconciles exact disappearance at inspection/removal and retains errors for still-present objects.</summary>
    [Theory(Timeout = 60000)]
    [InlineData("container", false, true)]
    [InlineData("container", true, true)]
    [InlineData("volume", false, true)]
    [InlineData("volume", true, true)]
    [InlineData("network", false, true)]
    [InlineData("network", true, true)]
    [InlineData("container", false, false)]
    [InlineData("container", true, false)]
    [InlineData("volume", false, false)]
    [InlineData("volume", true, false)]
    [InlineData("network", false, false)]
    [InlineData("network", true, false)]
    public async Task RecoveryReconcilesObjectsDisappearingAfterItsFinalInventory(string kind, bool duringRemoval, bool disappear)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux recovery custody control.");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PARTIES_MEMORIES_OWNED_RUNTIME") == "1", "Enable owned Docker interruption controls.");
        string session = Guid.NewGuid().ToString("N")[..12], marker = Guid.NewGuid().ToString("N");
        string root = Path.Combine(Path.GetTempPath(), "hexalith-parties-topology-" + session);
        Directory.CreateDirectory(root);
        var custody = PartyMemoryRunnerInterruptionCustody.Authorize(session, root, marker, string.Empty);
        using Process worker = Launch(marker);
        using Process foreign = Launch(Guid.NewGuid().ToString("N"));
        long birth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(worker.Id)!.Value;
        string named = "recovery-preserved-" + session;
        bool recovery = false, injected = false;
        try
        {
            string network = (await PartyMemoryRunnerInterruptionAudit.DockerAsync("network", "create", "--label", "hexalith.parties.test.owner=" + marker,
                "--label", "hexalith.parties.test.session=" + session, "recovery-" + session).ConfigureAwait(true)).Trim();
            _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("run", "-d", "--network", network, "--mount", "type=volume,src=" + named + ",dst=/preserved",
                "--label", "hexalith.parties.test.owner=" + marker, "--label", "hexalith.parties.test.session=" + session, "redis:7.4-alpine").ConfigureAwait(true);
            await custody.RefreshAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            string container = custody.Topology.GetProperty("Containers").EnumerateArray().Single().GetProperty("Id").GetString()!;
            string volume = custody.Topology.GetProperty("AnonymousVolumes").EnumerateArray().Single().GetProperty("Name").GetString()!;
            async Task<string> CommandAsync(CancellationToken token, string[] args)
            {
                if (args[0] == "ps" && !args.Contains("--filter")) { recovery = true; }
                bool target = kind switch
                {
                    "container" => args[0] == (duringRemoval ? "rm" : "inspect") && args[^1] == container,
                    _ => args.Length > 2 && args[0] == kind && args[1] == (duringRemoval ? "rm" : "inspect") && args[^1] == (kind == "volume" ? volume : network),
                };
                if (recovery && target && !injected)
                {
                    injected = true;
                    PartiesAspireTopologyFixture.IsWorkerRunning(worker.Id, birth).ShouldBeFalse("Workers must be quiesced before backing cleanup.");
                    if (disappear)
                    {
                        string[] remove = kind switch
                        {
                            "container" => ["rm", "-f", "-v", container],
                            "volume" => ["volume", "rm", volume],
                            _ => ["network", "rm", network],
                        };
                        _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync(token, remove).ConfigureAwait(false);
                    }
                    throw new IOException("controlled recovery operation race");
                }
                // Model an already-issued container removal that leaves its captured anonymous volume behind.
                if (kind == "volume" && recovery && args[0] == "rm" && args[^1] == container)
                { return await PartyMemoryRunnerInterruptionAudit.DockerAsync(token, "rm", "-f", container).ConfigureAwait(false); }
                return await PartyMemoryRunnerInterruptionAudit.DockerAsync(token, args).ConfigureAwait(false);
            }
            if (disappear)
            {
                await PartyMemoryRunnerInterruptionAudit.RecoverAsync(custody.Topology, command: CommandAsync).ConfigureAwait(true);
                (await PartyMemoryRunnerInterruptionAudit.VerifyClosedAsync(custody.Topology).ConfigureAwait(true)).GetProperty("Closed").GetBoolean().ShouldBeTrue();
            }
            else
            {
                InvalidDataException failure = await Should.ThrowAsync<InvalidDataException>(() => PartyMemoryRunnerInterruptionAudit.RecoverAsync(
                    custody.Topology, command: CommandAsync)).ConfigureAwait(true);
                failure.InnerException.ShouldBeOfType<IOException>().Message.ShouldBe("controlled recovery operation race");
                Directory.Exists(root).ShouldBeTrue("A real cleanup failure must remain visible.");
            }
            injected.ShouldBeTrue("The race must happen during recovery itself, after both discovery scans.");
            foreign.HasExited.ShouldBeFalse();
            _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("volume", "inspect", named).ConfigureAwait(true);
        }
        finally
        {
            try { await custody.RecoverAsync().ConfigureAwait(true); }
            finally
            {
                await ReapAsync(worker).ConfigureAwait(true);
                await ReapAsync(foreign).ConfigureAwait(true);
                _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("volume", "rm", named).ConfigureAwait(true);
            }
        }
    }

    /// <summary>A captured zombie retains its kernel lifetime until its actual parent reaps it.</summary>
    [Fact(Timeout = 15000)]
    public async Task ClosureRejectsCapturedUnreapedZombieUntilActualReaping()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux exact kernel lifetime control.");
        var start = new ProcessStartInfo("python3") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("""
            import json,os,pathlib,select,sys,time
            pid=os.fork()
            if pid==0: os._exit(23)
            while True:
                raw=pathlib.Path('/proc/'+str(pid)+'/stat').read_text()
                fields=raw[raw.rfind(')')+2:].split()
                if fields[0]=='Z': break
                time.sleep(.01)
            print(json.dumps({'Pid':pid,'Start':int(fields[19])}),flush=True)
            if select.select([sys.stdin],[],[],10)[0]: sys.stdin.readline()
            reaped,status=os.waitpid(pid,0)
            assert reaped==pid and os.waitstatus_to_exitcode(status)==23
            print('reaped',flush=True)
            """);
        using Process parent = Process.Start(start) ?? throw new InvalidOperationException("Zombie owner did not start.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            JsonElement child = JsonSerializer.Deserialize<JsonElement>((await parent.StandardOutput.ReadLineAsync(deadline.Token).ConfigureAwait(true))!);
            int pid = child.GetProperty("Pid").GetInt32(); long birth = child.GetProperty("Start").GetInt64();
            PartiesAspireTopologyFixture.IsWorkerRunning(pid, birth).ShouldBeFalse("Live-worker semantics must still exclude zombies.");
            JsonElement authority = PartyMemoryRunnerInterruptionAudit.Authorize("zombie", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), Guid.NewGuid().ToString("N"), string.Empty);
            Dictionary<string, JsonElement> fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(authority.GetRawText())!;
            fields["Processes"] = JsonSerializer.SerializeToElement(new[] { new { Pid = pid, Start = birth } });
            JsonElement topology = JsonSerializer.SerializeToElement(fields);
            JsonElement before = await PartyMemoryRunnerInterruptionAudit.VerifyClosedAsync(topology, (_, _) => Task.FromResult(string.Empty)).ConfigureAwait(true);
            before.GetProperty("Processes")[0].GetProperty("Exited").GetBoolean().ShouldBeFalse();
            before.GetProperty("Closed").GetBoolean().ShouldBeFalse();
            await parent.StandardInput.WriteLineAsync("reap").ConfigureAwait(true);
            (await parent.StandardOutput.ReadLineAsync(deadline.Token).ConfigureAwait(true)).ShouldBe("reaped");
            await parent.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            parent.ExitCode.ShouldBe(0);
            File.Exists($"/proc/{pid}/stat").ShouldBeFalse("The actual kernel lifetime must be reaped.");
            (await PartyMemoryRunnerInterruptionAudit.VerifyClosedAsync(topology, (_, _) => Task.FromResult(string.Empty)).ConfigureAwait(true))
                .GetProperty("Closed").GetBoolean().ShouldBeTrue();
        }
        finally
        {
            // Let the sole parent reap even when an earlier assertion fails; never abandon the zombie.
            if (!parent.HasExited) { await parent.StandardInput.WriteLineAsync("reap").ConfigureAwait(true); }
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            await parent.WaitForExitAsync(cleanup.Token).ConfigureAwait(true);
        }
    }

    private static Process Launch(string marker)
    {
        var command = new ProcessStartInfo("python3") { UseShellExecute = false };
        command.ArgumentList.Add("-c"); command.ArgumentList.Add("import time; time.sleep(120)");
        command.Environment[PartiesAspireTopologyFixture.WorkerOwnershipVariable] = marker;
        return Process.Start(command) ?? throw new InvalidOperationException("Owned discovery worker did not start.");
    }

    private static async Task ReapAsync(Process process)
    {
        if (!process.HasExited) { process.Kill(); }
        using var reap = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await process.WaitForExitAsync(reap.Token).ConfigureAwait(false);
    }
}
