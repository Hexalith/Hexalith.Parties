using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Hexalith.Parties.IntegrationTests.HealthChecks;
using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Exercises both removal gates with a real unmodeled orphan after failed consumer termination.</summary>
[Collection("Non-parallel")]
public sealed class PartiesMarkedConsumerCleanupTests
{
    /// <summary>Retains real backing/control-plane resources until the exact marked orphan exits, preserving a foreign process.</summary>
    [Theory(Timeout = 60000)]
    [InlineData("application")]
    [InlineData("control-plane")]
    [InlineData("stale-quiescence")]
    public async Task FailedConsumerTerminationRetainsBothRemovalGatesUntilExactExit(string gate)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux marker/orphan custody control.");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PARTIES_MEMORIES_OWNED_RUNTIME") == "1", "Enable owned Docker cleanup controls.");
        var fixture = new PartiesAspireTopologyFixture();
        await using var fixtureLifetime = fixture.ConfigureAwait(true);
        string directory = Path.Combine(Path.GetTempPath(), "parties-consumer-gates-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string identity = Path.Combine(directory, "orphan.json");
        string network = string.Empty, backing = string.Empty;
        Process? controller = null, foreign = null;
        int pid = 0; long birth = 0;
        bool backingRemoved = false, planeRemoved = false, retained = false, retried = false, builderDisposed = false;
        Exception? primary = null;
        string? evidenceDirectory = Environment.GetEnvironmentVariable("PARTIES_MEMORY_RUNNER_CONTROL_EVIDENCE");
        if (evidenceDirectory is not null) { Directory.CreateDirectory(evidenceDirectory); }
        try
        {
            network = (await PartyMemoryRunnerInterruptionAudit.DockerAsync("network", "create", "--label", "hexalith.parties.test.owner=" + fixture.WorkerOwnershipMarker,
                "--label", "hexalith.parties.test.session=" + fixture.Session.Id, "consumer-gates-" + fixture.Session.Id).ConfigureAwait(true)).Trim();
            backing = (await PartyMemoryRunnerInterruptionAudit.DockerAsync("run", "-d", "--network", network, "--label", "hexalith.parties.test.owner=" + fixture.WorkerOwnershipMarker,
                "--label", "hexalith.parties.test.session=" + fixture.Session.Id, "redis:7.4-alpine").ConfigureAwait(true)).Trim();
            foreign = Process.Start(Command("import time; time.sleep(120)")) ?? throw new InvalidOperationException("Foreign survivor did not start.");
            const string script = """
                import ctypes,json,os,pathlib,subprocess,sys
                if ctypes.CDLL(None,use_errno=True).prctl(36,1,0,0,0)!=0: raise OSError('owned subreaper unavailable')
                destination,marker=sys.argv[1:]
                source="import json,os,pathlib,subprocess,sys; env=os.environ.copy(); env['PARTIES_TEST_TOPOLOGY_OWNER']=sys.argv[2]; child=subprocess.Popen([sys.executable,'-c','import time; time.sleep(120)'],env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL); raw=pathlib.Path('/proc/'+str(child.pid)+'/stat').read_text(); fields=raw[raw.rfind(')')+2:].split(); path=pathlib.Path(sys.argv[1]+'.pending'); path.write_text(json.dumps({'Pid':child.pid,'Start':int(fields[19])})); path.replace(sys.argv[1]); os._exit(23)"
                child=subprocess.Popen([sys.executable,'-c',source,destination,marker])
                if child.wait()!=23: raise RuntimeError('the controlled consumer parent did not exit')
                while True:
                    try: os.waitpid(-1,0)
                    except ChildProcessError: break
                """;
            ProcessStartInfo start = Command(script);
            start.ArgumentList.Add(identity); start.ArgumentList.Add(fixture.WorkerOwnershipMarker);
            controller = Process.Start(start) ?? throw new InvalidOperationException("Scoped orphan reaper did not start.");
            using var ready = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!File.Exists(identity)) { await Task.Delay(10, ready.Token).ConfigureAwait(true); }
            JsonElement worker = PartyMemoryRunnerInterruptionHandoff.ReadBounded(identity);
            pid = worker.GetProperty("Pid").GetInt32(); birth = worker.GetProperty("Start").GetInt64();
            PartiesAspireTopologyFixture.IsWorkerRunning(pid, birth).ShouldBeTrue();
            if (evidenceDirectory is not null)
            {
                await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "consumer-gate-" + gate + "-" + fixture.Session.Id + "-custody.json"), JsonSerializer.Serialize(new
                { Gate = gate, fixture.Session.Id, Marker = fixture.WorkerOwnershipMarker, ConsumerPid = pid, ConsumerStart = birth,
                    ControllerPid = controller.Id, ControllerStart = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(controller.Id), BackingId = backing, NetworkId = network }), TestContext.Current.CancellationToken).ConfigureAwait(true);
            }
            // Deliberately omit direct registration: teardown must discover the exact marker after parent loss.
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
            IResourceBuilder<ExecutableResource> blocked = builder.AddExecutable("controlled-stop-failure", "unused", ".");
            bool allowStop = false;
            _ = blocked.WithCommand(KnownResourceCommands.StopCommand, "Stop", _ => Task.FromResult(allowStop ? CommandResults.Success() : CommandResults.Failure("controlled stop failure")));
            DistributedApplication app = builder.Build();
            await using var applicationLifetime = app.ConfigureAwait(true);
            await app.ResourceNotifications.PublishUpdateAsync(blocked.Resource, snapshot => snapshot with
            {
                State = new ResourceStateSnapshot(KnownResourceStates.Running, null),
                Commands = [new ResourceCommandSnapshot(KnownResourceCommands.StopCommand, ResourceCommandState.Enabled, "Stop", null, null, null, null, null, false)],
            }).ConfigureAwait(true);
            IDistributedApplicationTestingBuilder trackedBuilder = DispatchProxy.Create<IDistributedApplicationTestingBuilder, PartiesDisposalTrackingBuilder>();
            var tracker = (PartiesDisposalTrackingBuilder)trackedBuilder;
            tracker.Services = builder.Services;
            tracker.Dispose = async () => { builderDisposed = true; await RemoveBackingAsync().ConfigureAwait(true); };
            if (gate != "control-plane")
            {
                Field("_app").SetValue(fixture, app);
                Field("_builder").SetValue(fixture, trackedBuilder);
            }
            if (gate == "stale-quiescence") { WorkerFlag().SetValue(fixture, true); }
            var termination = new IOException("controlled consumer termination failure");
            using var failedAttempt = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            AggregateException failure = await Should.ThrowAsync<AggregateException>(() => PartyMemoryOwnedRuntimeTests.CompleteChildRunAsync(
                () => Task.FromException(termination),
                () => Task.FromException(new TimeoutException("controlled consumer reap failure")),
                () => Task.CompletedTask,
                () => Task.CompletedTask,
                async () =>
                {
                    if (gate != "control-plane") { await fixture.DisposeAsync().ConfigureAwait(true); }
                    else
                    {
                        await fixture.ConfirmOwnedWorkersStoppedAsync(failedAttempt.Token).ConfigureAwait(true);
                        await RemoveBackingAsync().ConfigureAwait(true);
                        await RemovePlaneAsync().ConfigureAwait(true);
                    }
                },
                () => Task.CompletedTask, primaryFailure: null)).ConfigureAwait(true);
            failure.Flatten().InnerExceptions.ShouldContain(termination);
            backingRemoved.ShouldBeFalse(); planeRemoved.ShouldBeFalse(); builderDisposed.ShouldBeFalse();
            if (gate != "control-plane")
            {
                fixture.CurrentApplication.ShouldBeSameAs(app, "The real fixture must retain the application while the marked consumer lives.");
                Field("_builder").GetValue(fixture).ShouldBeSameAs(trackedBuilder, "The real builder gate must retain the same application owner.");
            }
            ((bool)WorkerFlag().GetValue(fixture)!).ShouldBeFalse("A failed fresh check must revoke any prior quiescence proof.");
            PartiesAspireTopologyFixture.IsWorkerRunning(pid, birth).ShouldBeTrue();
            _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("inspect", backing).ConfigureAwait(true);
            _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("network", "inspect", network).ConfigureAwait(true);
            foreign.HasExited.ShouldBeFalse();
            retained = true;
            await PartyMemoryProducerRestartRuntime.QuiesceProcessAsync(pid, birth).ConfigureAwait(true);
            using var reaping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await controller.WaitForExitAsync(reaping.Token).ConfigureAwait(true);
            controller.ExitCode.ShouldBe(0, "The scoped reaper must await the exact orphan.");
            using var retry = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            allowStop = true;
            await app.ResourceNotifications.PublishUpdateAsync(blocked.Resource, snapshot => snapshot with
            { State = new ResourceStateSnapshot(KnownResourceStates.Exited, null), Commands = [] }).ConfigureAwait(true);
            await fixture.QuiesceStartedWorkersAsync(app, retry.Token).ConfigureAwait(true);
            await fixture.ConfirmOwnedWorkersStoppedAsync(retry.Token).ConfigureAwait(true);
            await fixture.DisposeAsync().ConfigureAwait(true);
            if (gate != "control-plane") { builderDisposed.ShouldBeTrue(); fixture.CurrentApplication.ShouldBeNull(); }
            await RemoveBackingAsync().ConfigureAwait(true); await RemovePlaneAsync().ConfigureAwait(true);
            retried = true;
            foreign.HasExited.ShouldBeFalse();
            string? evidence = Environment.GetEnvironmentVariable("PARTIES_MEMORY_RUNNER_CONTROL_EVIDENCE");
            if (evidence is not null)
            {
                Directory.CreateDirectory(evidence);
                await File.WriteAllTextAsync(Path.Combine(evidence, "consumer-gate-" + gate + "-" + fixture.Session.Id + ".json"), JsonSerializer.Serialize(new
                { Gate = gate, ConsumerPid = pid, ConsumerStart = birth, ParentExitedBeforeDiscovery = true, FailedTerminationPreserved = true,
                    BackingAndControlPlaneRetained = retained, BuilderRetainedUntilExactExit = retained, ActualFixtureDisposalExercised = gate != "control-plane", ExactConsumerExitedBeforeRemoval = !PartiesAspireTopologyFixture.IsWorkerRunning(pid, birth),
                    SuccessfulRetry = retried, ForeignSurvived = !foreign.HasExited }), TestContext.Current.CancellationToken).ConfigureAwait(true);
            }
        }
        catch (Exception failure) { primary = failure; throw; }
        finally
        {
            await PartyMemoryOwnedRuntimeTests.CompleteChildRunAsync(
                async () => { if (pid != 0) { await PartyMemoryProducerRestartRuntime.QuiesceProcessAsync(pid, birth).ConfigureAwait(true); } },
                async () =>
                {
                    if (pid != 0 && PartiesAspireTopologyFixture.IsWorkerRunning(pid, birth)) { throw new InvalidOperationException("Retain the scoped reaper while its exact child remains live."); }
                    if (controller is not null) { await ReapAsync(controller).ConfigureAwait(true); controller.Dispose(); }
                },
                async () => { if (foreign is not null) { await ReapAsync(foreign).ConfigureAwait(true); foreign.Dispose(); } },
                async () =>
                {
                    if (pid != 0 && PartiesAspireTopologyFixture.IsWorkerRunning(pid, birth)) { throw new InvalidOperationException("Backing retained for the live marked consumer."); }
                    if (backing.Length != 0 && (await PartyMemoryRunnerInterruptionAudit.DockerAsync("ps", "-aq", "--no-trunc").ConfigureAwait(true)).Split('\n').Contains(backing))
                    { await RemoveBackingAsync().ConfigureAwait(true); }
                },
                async () =>
                {
                    if (network.Length != 0 && (await PartyMemoryRunnerInterruptionAudit.DockerAsync("network", "ls", "-q", "--no-trunc").ConfigureAwait(true)).Split('\n').Contains(network))
                    { await RemovePlaneAsync().ConfigureAwait(true); }
                },
                () => { Directory.Delete(directory, recursive: true); return Task.CompletedTask; }, primary).ConfigureAwait(true);
        }

        async Task RemoveBackingAsync()
        {
            if (backingRemoved) { return; }
            _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("rm", "-f", "-v", backing).ConfigureAwait(true); backingRemoved = true;
        }
        async Task RemovePlaneAsync()
        {
            if (planeRemoved) { return; }
            _ = await PartyMemoryRunnerInterruptionAudit.DockerAsync("network", "rm", network).ConfigureAwait(true); planeRemoved = true;
        }
    }

    private static FieldInfo Field(string name) => typeof(PartiesAspireTopologyFixture).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static FieldInfo WorkerFlag() => Field("_workersQuiesced");

    private static ProcessStartInfo Command(string script)
    {
        var start = new ProcessStartInfo("python3") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-c"); start.ArgumentList.Add(script); return start;
    }
    private static async Task ReapAsync(Process process)
    {
        if (!process.HasExited) { process.Kill(entireProcessTree: true); }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await process.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
    }
}
