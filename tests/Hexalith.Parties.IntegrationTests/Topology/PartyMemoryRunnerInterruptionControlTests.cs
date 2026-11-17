using System.Diagnostics;
using System.Text.Json;

using Hexalith.Parties.IntegrationTests.HealthChecks;
using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Failure controls for final signal authority and custody before healthy startup.</summary>
[Collection("Non-parallel")]
public sealed class PartyMemoryRunnerInterruptionControlTests
{
    /// <summary>Mutates actual owned Redis inventory after publication and proves final signaling refuses it before restoring exact bytes.</summary>
    [Fact(Timeout = 600000)]
    public Task ChangedPersistedStateRefusesRunnerSignal()
        => PartyMemoryRunnerInterruptionTests.RunAsync("same-uri-refresh", TestContext.Current.CancellationToken, mutatePersistedState: true);

    /// <summary>A producer that starts listening after a negative live sample cannot attest pre-healthy startup.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FrozenStartupProofRejectsAListenerStartedAfterTheNegativeSample(bool listen)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux stopped-socket custody control.");
        string directory = Path.Combine(Path.GetTempPath(), "startup-socket-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var reserved = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        reserved.Start();
        int port = ((System.Net.IPEndPoint)reserved.LocalEndpoint).Port;
        reserved.Stop();
        string marker = Guid.NewGuid().ToString("N");
        string script = "import pathlib,socket,sys,time; root=pathlib.Path(sys.argv[1]); "
            + "\nwhile not (root/'start').exists(): time.sleep(.005)"
            + "\ns=socket.socket(); s.bind(('127.0.0.1',int(sys.argv[2]))); s.listen(); (root/'listening').touch(); time.sleep(120)";
        var command = Command(script);
        command.ArgumentList.Add(directory); command.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        command.Environment[PartiesAspireTopologyFixture.WorkerOwnershipVariable] = marker;
        using Process producer = Process.Start(command) ?? throw new InvalidOperationException("Owned socket producer did not start.");
        long birth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(producer.Id)!.Value;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            await Should.ThrowAsync<HttpRequestException>(() => http.GetAsync(new Uri("http://127.0.0.1:" + port + "/ready"), deadline.Token)).ConfigureAwait(true);
            if (listen)
            {
                await File.WriteAllTextAsync(Path.Combine(directory, "start"), string.Empty, deadline.Token).ConfigureAwait(true);
                while (!File.Exists(Path.Combine(directory, "listening"))) { await Task.Delay(10, deadline.Token).ConfigureAwait(true); }
            }
            await PartyMemoryProducerRestartRuntime.WhileStoppedAsync(producer.Id, birth, async () =>
            {
                Task<JsonElement> proof = PartyMemoryProducerRestartRuntime.ObserveBeforeHealthyAsync(producer.Id, birth, marker, deadline.Token);
                if (listen) { await Should.ThrowAsync<AggregateException>(proof).ConfigureAwait(true); }
                else { (await proof.ConfigureAwait(true)).GetProperty("OwnedListeningSocketsAbsent").GetBoolean().ShouldBeTrue(); }
                return true;
            }, deadline.Token).ConfigureAwait(true);
        }
        finally { await ReapAsync(producer).ConfigureAwait(true); Directory.Delete(directory, true); }
    }

    /// <summary>Admitted work can never qualify through the ordinary observation branch.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdmittedInterruptionRejectsAbsentAndNullReplacement(bool explicitNull)
    {
        JsonElement observation = JsonSerializer.SerializeToElement(explicitNull ? new Dictionary<string, object?> { ["PendingReplacement"] = null } : new Dictionary<string, object?>());
        Should.Throw<InvalidDataException>(() => PartyMemoryRunnerInterruptionAudit.RequireReplacement("admitted-empty-mappings", observation, new Dictionary<int, long>()));
    }

    /// <summary>Every producer must still be current, marker-bound, stopped and fresh after the slow audits.</summary>
    [Theory]
    [InlineData("wrong-birth")]
    [InlineData("wrong-marker")]
    [InlineData("resumed")]
    [InlineData("expired")]
    [InlineData("stale-observation")]
    [InlineData("valid")]
    public async Task FinalHeldHandleAuthorizationRechecksTheProducer(string fault)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux pidfd authority control.");
        string marker = Guid.NewGuid().ToString("N"), lineage = Guid.NewGuid().ToString("N");
        using Process runner = Launch("import time; time.sleep(120)", lineage, null);
        using Process producer = Launch("import time; time.sleep(120)", null, marker);
        long runnerBirth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(runner.Id)!.Value;
        long producerBirth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(producer.Id)!.Value;
        try
        {
            await PartyMemoryProducerRestartRuntime.WhileStoppedAsync(producer.Id, producerBirth, async () =>
            {
                if (fault == "resumed") { await PartyMemoryProducerRestartRuntime.SignalAsync(producer.Id, producerBirth, "CONT", TestContext.Current.CancellationToken).ConfigureAwait(true); }
                JsonElement observation = JsonSerializer.SerializeToElement(new
                {
                    ProducerPid = producer.Id, ProducerStart = producerBirth + (fault == "wrong-birth" ? 1 : 0),
                    ObservedAtUtc = DateTimeOffset.UtcNow.AddSeconds(fault == "stale-observation" ? -20 : 0),
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(fault == "expired" ? -1 : 30),
                });
                Task authorization = PartyMemoryRunnerInterruptionHandoff.AuthorizeSignalAsync(runner.Id, runnerBirth, lineage,
                    observation, fault == "wrong-marker" ? "foreign" : marker, DateTimeOffset.UtcNow, deliver: false);
                if (fault == "valid") { await authorization.ConfigureAwait(true); }
                else { await Should.ThrowAsync<AggregateException>(authorization).ConfigureAwait(true); }
                runner.HasExited.ShouldBeFalse();
                return true;
            }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        finally { await ReapAsync(producer).ConfigureAwait(true); await ReapAsync(runner).ConfigureAwait(true); }
    }

    /// <summary>A helper that exits while a child retains stdout cannot prevent later disposal.</summary>
    [Fact]
    public async Task InheritedOutputHandlesHaveAnIndependentFiniteDrainBudget()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux inherited-pipe control.");
        string directory = Path.Combine(Path.GetTempPath(), "interruption-pipe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string identity = Path.Combine(directory, "child.json");
        var command = Command("import json,os,pathlib,subprocess,sys; p=subprocess.Popen([sys.executable,'-c','import time; time.sleep(120)']); raw=pathlib.Path('/proc/'+str(p.pid)+'/stat').read_text(); fields=raw[raw.rfind(')')+2:].split(); pathlib.Path(sys.argv[1]).write_text(json.dumps({'Pid':p.pid,'Start':int(fields[19])})); os._exit(0)");
        command.ArgumentList.Add(identity);
        int pid = 0; long birth = 0;
        var elapsed = Stopwatch.StartNew();
        try
        {
            _ = await PartyMemoryRunnerInterruptionProcess.RunAsync(command, TimeSpan.FromSeconds(8)).ConfigureAwait(true);
            JsonElement receipt = PartyMemoryRunnerInterruptionHandoff.ReadBounded(identity);
            pid = receipt.GetProperty("Pid").GetInt32();
            birth = receipt.GetProperty("Start").GetInt64();
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
            PartiesAspireTopologyFixture.IsWorkerRunning(pid, birth).ShouldBeFalse();
        }
        finally
        {
            if (pid != 0) { await PartyMemoryProducerRestartRuntime.QuiesceProcessAsync(pid, birth).ConfigureAwait(true); }
            Directory.Delete(directory, true);
        }
    }

    /// <summary>An open writer cannot prevent bounded stream closure and later direct-child disposal.</summary>
    [Fact]
    public async Task OutputDrainTimeoutStillClosesReadersAndAllowsChildReaping()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux bounded output control.");
        using Process child = Launch("import time; time.sleep(120)", null, null);
        using var cancellation = new CancellationTokenSource();
        Task<string> output = child.StandardOutput.ReadToEndAsync(cancellation.Token);
        Task<string> error = child.StandardError.ReadToEndAsync(cancellation.Token);
        long birth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(child.Id)!.Value;
        var elapsed = Stopwatch.StartNew();
        try
        {
            await Should.ThrowAsync<TimeoutException>(() => PartyMemoryRunnerInterruptionProcess.DrainAsync(child, output, error,
                cancellation, TimeSpan.FromMilliseconds(100))).ConfigureAwait(true);
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
            output.IsCompleted.ShouldBeTrue();
            error.IsCompleted.ShouldBeTrue();
            child.HasExited.ShouldBeFalse();
        }
        finally { await ReapAsync(child).ConfigureAwait(true); }
        PartiesAspireTopologyFixture.IsWorkerRunning(child.Id, birth).ShouldBeFalse();
    }

    /// <summary>A command that exceeds execution time is terminated and reaped independently of output draining.</summary>
    [Fact]
    public async Task TimedOutAuditCommandReapsItsExactChild()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux bounded audit control.");
        int pid = 0; long birth = 0;
        await Should.ThrowAsync<AggregateException>(() => PartyMemoryRunnerInterruptionProcess.RunAsync(Command("import time; time.sleep(120)"),
            TimeSpan.FromMilliseconds(100), process => { pid = process.Id; birth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(pid)!.Value; })).ConfigureAwait(true);
        PartiesAspireTopologyFixture.IsWorkerRunning(pid, birth).ShouldBeFalse();
    }

    /// <summary>Failed termination cannot skip bounded reaping, stream closure or caller-owned recovery.</summary>
    [Fact]
    public async Task FailedAuditTerminationStillClosesStreamsAndAllowsIndependentRecovery()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux failed termination control.");
        int pid = 0; long birth = 0;
        bool attempted = false, recovered = false;
        var elapsed = Stopwatch.StartNew();
        try
        {
            AggregateException failure = await Should.ThrowAsync<AggregateException>(() => PartyMemoryRunnerInterruptionProcess.RunAsync(
                Command("import time; time.sleep(120)"), TimeSpan.FromMilliseconds(100),
                child => { pid = child.Id; birth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(pid)!.Value; },
                _ => { attempted = true; throw new IOException("controlled termination failure"); })).ConfigureAwait(true);
            failure.InnerExceptions.ShouldContain(error => error is IOException && error.Message == "controlled termination failure");
            attempted.ShouldBeTrue();
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(12));
        }
        finally
        {
            if (pid != 0)
            {
                await PartyMemoryProducerRestartRuntime.QuiesceProcessAsync(pid, birth).ConfigureAwait(true);
                recovered = true;
            }
        }
        recovered.ShouldBeTrue();
        PartiesAspireTopologyFixture.IsWorkerRunning(pid, birth).ShouldBeFalse();
    }

    /// <summary>Recovers real pre-health creation and orphaned consumers even when no full startup receipt was published.</summary>
    [Theory]
    [InlineData("crash")]
    [InlineData("cancel")]
    public async Task ExternalAuthorityRecoversBeforeFirstFullCustodyAndPreservesNamedVolumes(string mode)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux early ownership control.");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PARTIES_MEMORIES_OWNED_RUNTIME") == "1", "Enable owned Docker interruption controls.");
        string session = Guid.NewGuid().ToString("N")[..12], marker = Guid.NewGuid().ToString("N"), lineage = Guid.NewGuid().ToString("N");
        string root = Path.Combine(Path.GetTempPath(), "hexalith-parties-topology-" + session);
        PartyMemoryRunnerInterruptionCustody custody = PartyMemoryRunnerInterruptionCustody.Authorize(session, root, marker, lineage);
        Directory.CreateDirectory(root);
        string named = "parties-interruption-named-" + session;
        string foreign = string.Empty;
        Process? runner = null;
        JsonElement? captured = null;
        Exception? primary = null;
        string evidence = Environment.GetEnvironmentVariable("PARTIES_MEMORY_RUNNER_CONTROL_EVIDENCE") ?? Path.GetTempPath();
        Directory.CreateDirectory(evidence);
        await File.WriteAllTextAsync(Path.Combine(evidence, "early-" + session + "-authority.json"), JsonSerializer.Serialize(custody.Topology), TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("volume", "create", "--label", "hexalith.parties.test.sentinel=" + marker, named).ConfigureAwait(true);
            foreign = (await PartyMemoryRunnerInterruptionAudit.DockerAsync("run", "-d", "--label", "hexalith.parties.test.sentinel=" + marker, "--mount", "type=volume,source=" + named + ",target=/foreign", "redis:7.4-alpine").ConfigureAwait(true)).Trim();
            await File.WriteAllTextAsync(Path.Combine(evidence, "early-" + session + "-sentinel.json"), JsonSerializer.Serialize(new { Id = foreign, Volume = named, Marker = marker }), TestContext.Current.CancellationToken).ConfigureAwait(true);
            const string script = """
                import json,os,pathlib,subprocess,sys,time
                root,marker,session,named,mode=sys.argv[1:]
                cid=subprocess.check_output(['docker','run','-d','--label','hexalith.parties.test.owner='+marker,'--label','hexalith.parties.test.session='+session,'--mount','type=volume,source='+named+',target=/named','redis:7.4-alpine'],text=True).strip()
                env=os.environ.copy(); env['PARTIES_TEST_TOPOLOGY_OWNER']=marker
                child=subprocess.Popen([sys.executable,'-c','import time; time.sleep(120)'],env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
                raw=pathlib.Path('/proc/'+str(os.getpid())+'/stat').read_text(); fields=raw[raw.rfind(')')+2:].split()
                pathlib.Path(root,'created.json').write_text(json.dumps({'Container':cid,'Child':child.pid,'RunnerPid':os.getpid(),'RunnerBirth':int(fields[19])}))
                if mode=='crash': os._exit(23)
                time.sleep(120)
                """;
            string startupScript = Path.Combine(root, "startup.py");
            await File.WriteAllTextAsync(startupScript, script, TestContext.Current.CancellationToken).ConfigureAwait(true);
            const string reaper = """
                import ctypes,json,os,pathlib,subprocess,sys
                if ctypes.CDLL(None,use_errno=True).prctl(36,1,0,0,0)!=0: raise OSError('owned subreaper unavailable')
                script,root,marker,session,named,mode,lineage=sys.argv[1:]
                env=os.environ.copy(); env['PARTIES_TEST_RUNNER_OWNER']=lineage
                child=subprocess.Popen([sys.executable,script,root,marker,session,named,mode],env=env)
                code=child.wait()
                pathlib.Path(root,'runner-exited.json').write_text(json.dumps({'ExitCode':code}))
                while True:
                    try: os.waitpid(-1,0)
                    except ChildProcessError: break
                """;
            ProcessStartInfo start = Command(reaper);
            foreach (string value in new[] { startupScript, root, marker, session, named, mode, lineage }) { start.ArgumentList.Add(value); }
            runner = Process.Start(start) ?? throw new InvalidOperationException("Early startup runner did not start.");
            using var ready = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (!File.Exists(Path.Combine(root, "created.json"))) { await Task.Delay(20, ready.Token).ConfigureAwait(true); }
            JsonElement created = PartyMemoryRunnerInterruptionHandoff.ReadBounded(Path.Combine(root, "created.json"));
            if (mode == "cancel") { await PartyMemoryProducerRestartRuntime.SignalAsync(created.GetProperty("RunnerPid").GetInt32(), created.GetProperty("RunnerBirth").GetInt64(), "INT", ready.Token).ConfigureAwait(true); }
            while (!File.Exists(Path.Combine(root, "runner-exited.json"))) { await Task.Delay(20, ready.Token).ConfigureAwait(true); }
            PartyMemoryRunnerInterruptionHandoff.ReadBounded(Path.Combine(root, "runner-exited.json")).GetProperty("ExitCode").GetInt32().ShouldNotBe(0);
            // The first inventory is deliberately after parent exit. Inherited ownership still identifies the orphan.
            await custody.RefreshAsync().ConfigureAwait(true);
            captured = custody.Topology;
            await File.WriteAllTextAsync(Path.Combine(evidence, "early-" + session + "-custody.json"), JsonSerializer.Serialize(captured), TestContext.Current.CancellationToken).ConfigureAwait(true);
            captured.Value.GetProperty("OwnedWorkers").GetArrayLength().ShouldBe(1);
            captured.Value.GetProperty("AnonymousVolumes").GetArrayLength().ShouldBe(1);
            JsonElement container = captured.Value.GetProperty("Containers").EnumerateArray().Single();
            string id = container.GetProperty("Id").GetString()!;
            _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("stop", "-t", "0", id).ConfigureAwait(true);
            JsonElement stopped = PartyMemoryRunnerInterruptionAudit.ProjectContainer(JsonSerializer.Deserialize<JsonElement>(await PartyMemoryRunnerInterruptionAudit.DockerAsync("inspect", id).ConfigureAwait(true))[0]);
            Should.Throw<InvalidDataException>(() => PartyMemoryRunnerInterruptionAudit.RequireLiveContainer(container, stopped));
            await custody.RecoverAsync().ConfigureAwait(true);
            await runner.WaitForExitAsync(ready.Token).ConfigureAwait(true);
            runner.ExitCode.ShouldBe(0, "The scoped subreaper must await the orphan after external recovery.");
            (await PartyMemoryRunnerInterruptionAudit.VerifyClosedAsync(custody.Topology).ConfigureAwait(true)).GetProperty("Closed").GetBoolean().ShouldBeTrue();
            _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("inspect", foreign).ConfigureAwait(true);
            _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("volume", "inspect", named).ConfigureAwait(true);
        }
        catch (Exception failure) { primary = failure; throw; }
        finally
        {
            await PartyMemoryOwnedRuntimeTests.CompleteChildRunAsync(
                () => custody.RecoverAsync(),
                async () => { if (runner is not null) { await ReapAsync(runner).ConfigureAwait(true); runner.Dispose(); } },
                async () => { if (foreign.Length != 0) { _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("rm", "-f", "-v", foreign).ConfigureAwait(true); } },
                async () => { _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("volume", "rm", named).ConfigureAwait(true); },
                () => Task.CompletedTask,
                () => File.WriteAllTextAsync(Path.Combine(evidence, "early-" + session + "-result.json"), JsonSerializer.Serialize(new
                { Mode = mode, Failure = primary?.ToString(), Closed = !Directory.Exists(root) }), CancellationToken.None), primary).ConfigureAwait(true);
        }
    }

    private static ProcessStartInfo Command(string script)
    {
        var command = new ProcessStartInfo("python3") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        command.ArgumentList.Add("-c"); command.ArgumentList.Add(script);
        return command;
    }
    private static Process Launch(string script, string? lineage, string? marker)
    {
        ProcessStartInfo command = Command(script);
        if (lineage is not null) { command.Environment[PartyMemoryRunnerInterruptionAudit.LineageVariable] = lineage; }
        if (marker is not null) { command.Environment[PartiesAspireTopologyFixture.WorkerOwnershipVariable] = marker; }
        return Process.Start(command) ?? throw new InvalidOperationException("Owned signal control did not start.");
    }
    private static async Task ReapAsync(Process process)
    {
        if (!process.HasExited) { process.Kill(entireProcessTree: true); }
        using var reap = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await process.WaitForExitAsync(reap.Token).ConfigureAwait(true);
    }
}
