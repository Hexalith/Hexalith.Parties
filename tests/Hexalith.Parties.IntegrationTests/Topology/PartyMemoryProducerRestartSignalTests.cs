using System.Diagnostics;

using Hexalith.Parties.IntegrationTests.HealthChecks;
using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Uses owned real processes to qualify pidfd identity refusal and unconditional resume before cleanup.</summary>
public sealed class PartyMemoryProducerRestartSignalTests
{
    [Fact]
    public async Task ForeignStartIdentityCannotInterruptAnUnrelatedIncarnation()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Runner interruption requires Linux pidfd support.");
        using Process child = StartChild();
        long start = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(child.Id)!.Value;
        try
        {
            await Should.ThrowAsync<InvalidOperationException>(() => PartyMemoryProducerRestartRuntime.SignalAsync(child.Id, start + 1, "INT", TestContext.Current.CancellationToken));
            PartiesAspireTopologyFixture.IsWorkerRunning(child.Id, start).ShouldBeTrue();
        }
        finally { await ReapAsync(child).ConfigureAwait(true); }
    }

    [Fact]
    public async Task ForeignStartIdentityCannotSignalTheOwnedNumericPid()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The private producer restart lane requires Linux pidfd support.");
        using Process child = StartChild();
        long start = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(child.Id)!.Value;
        bool operationEntered = false;
        try
        {
            await Should.ThrowAsync<InvalidOperationException>(() => PartyMemoryProducerRestartRuntime.WhileStoppedAsync(child.Id, start + 1,
                () => { operationEntered = true; return Task.FromResult(true); }, TestContext.Current.CancellationToken));
            operationEntered.ShouldBeFalse();
            State(child.Id).ShouldNotBe('T');
            PartiesAspireTopologyFixture.ReadWorkerStartIdentity(child.Id).ShouldBe(start);
        }
        finally { await ReapAsync(child).ConfigureAwait(true); }
        child.HasExited.ShouldBeTrue();
    }

    [Theory]
    [InlineData("cancellation")]
    [InlineData("evidence")]
    public async Task InterruptedEvidenceAlwaysResumesBeforeCallerReturnAndReapsChild(string failure)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The private producer restart lane requires Linux pidfd support.");
        using Process child = StartChild();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        long start = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(child.Id)!.Value;
        bool observedStopped = false;
        try
        {
            Task operation = PartyMemoryProducerRestartRuntime.WhileStoppedAsync<bool>(child.Id, start, () =>
            {
                State(child.Id).ShouldBe('T');
                observedStopped = true;
                if (failure == "cancellation") { cancellation.Cancel(); return Task.FromCanceled<bool>(cancellation.Token); }
                return Task.FromException<bool>(new IOException("synthetic persisted phase failure"));
            }, cancellation.Token);
            if (failure == "cancellation") { await Should.ThrowAsync<OperationCanceledException>(operation); }
            else { await Should.ThrowAsync<IOException>(operation); }
            observedStopped.ShouldBeTrue();
            using var resumed = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            while (State(child.Id) is 'T' or 't') { await Task.Delay(10, resumed.Token).ConfigureAwait(true); }
            PartiesAspireTopologyFixture.ReadWorkerStartIdentity(child.Id).ShouldBe(start);
            child.HasExited.ShouldBeFalse();
        }
        finally { await ReapAsync(child).ConfigureAwait(true); }
        child.HasExited.ShouldBeTrue();
    }

    [Fact]
    public async Task IncompleteReplacementQuiescenceObservesExitBeforeReturning()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The private producer restart lane requires Linux pidfd support.");
        using Process child = StartChild();
        long start = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(child.Id)!.Value;
        try
        {
            await PartyMemoryProducerRestartRuntime.QuiesceProcessAsync(child.Id, start).ConfigureAwait(true);
            await child.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            PartiesAspireTopologyFixture.ReadWorkerStartIdentity(child.Id).ShouldBeNull();
            child.HasExited.ShouldBeTrue();
        }
        finally { await ReapAsync(child).ConfigureAwait(true); }
    }

    [Fact]
    public async Task HungSignalHelperAfterAppliedStopIsReapedAndOriginalProcessResumes()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The private producer restart lane requires Linux pidfd support.");
        using Process child = StartChild();
        long start = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(child.Id)!.Value;
        int helperPid = 0;
        long? helperStart = null;
        bool entered = false;
        try
        {
            await Should.ThrowAsync<TimeoutException>(() => PartyMemoryProducerRestartRuntime.WhileStoppedAsync(child.Id, start,
                () => { entered = true; return Task.FromResult(true); }, TestContext.Current.CancellationToken, async (signal, token) =>
                {
                    await PartyMemoryProducerRestartRuntime.SignalAsync(child.Id, start, signal, token).ConfigureAwait(true);
                    if (signal != "STOP") { return; }
                    using var stopped = CancellationTokenSource.CreateLinkedTokenSource(token);
                    stopped.CancelAfter(TimeSpan.FromSeconds(2));
                    while (State(child.Id) is not ('T' or 't'))
                    {
                        PartiesAspireTopologyFixture.ReadWorkerStartIdentity(child.Id).ShouldBe(start);
                        await Task.Delay(10, stopped.Token).ConfigureAwait(true);
                    }
                    stopped.Token.ThrowIfCancellationRequested();
                    PartiesAspireTopologyFixture.ReadWorkerStartIdentity(child.Id).ShouldBe(start);
                    var command = new ProcessStartInfo("python3") { UseShellExecute = false, RedirectStandardError = true };
                    command.ArgumentList.Add("-c");
                    command.ArgumentList.Add("import time; time.sleep(120)");
                    _ = await PartyMemoryProducerRestartRuntime.RunSignalHelperAsync(command, TimeSpan.FromMilliseconds(200), pid =>
                    { helperPid = pid; helperStart = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(pid); }).ConfigureAwait(true);
                }));
            entered.ShouldBeFalse();
            helperPid.ShouldBeGreaterThan(0);
            helperStart.ShouldNotBeNull();
            PartiesAspireTopologyFixture.IsWorkerRunning(helperPid, helperStart!.Value).ShouldBeFalse();
            using var resumed = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            while (State(child.Id) is 'T' or 't') { await Task.Delay(10, resumed.Token).ConfigureAwait(true); }
            PartiesAspireTopologyFixture.ReadWorkerStartIdentity(child.Id).ShouldBe(start);
        }
        finally { await ReapAsync(child).ConfigureAwait(true); }
        child.HasExited.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledPhaseBeforePidObservationSettlesPendingStartAndQuiescesItsIncarnation(bool uncertainStart)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The private producer restart lane requires Linux pidfd support.");
        using var phase = new CancellationTokenSource();
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new IOException("synthetic uncertain start acknowledgement");
        Process? child = null;
        bool observedPending = false, quiesced = false;
        if (!uncertainStart) { requested.SetResult(); }
        phase.Cancel();
        Task launch = LaunchAsync();
        try
        {
            await Should.ThrowAsync<OperationCanceledException>(() => uncertainStart ? requested.Task.WaitAsync(phase.Token) : Task.Delay(25, phase.Token));
            Task settling = PartyMemoryProducerRestartRuntime.SettleReplacementAsync(requested.Task,
                () =>
                {
                    if (child is null) { observedPending = true; return (0, null); }
                    return (child.Id, PartiesAspireTopologyFixture.ReadWorkerStartIdentity(child.Id));
                }, (0, 0),
                async (pid, start) =>
                {
                    child.ShouldNotBeNull().Id.ShouldBe(pid);
                    await PartyMemoryProducerRestartRuntime.QuiesceProcessAsync(pid, start).ConfigureAwait(true);
                    quiesced = true;
                }, cleanup.Token);
            if (uncertainStart) { (await Should.ThrowAsync<IOException>(settling)).ShouldBe(failure); }
            else { await settling.ConfigureAwait(true); }
            await launch.ConfigureAwait(true);
            observedPending.ShouldBeTrue();
            quiesced.ShouldBeTrue();
            child.ShouldNotBeNull();
            await child.WaitForExitAsync(cleanup.Token).ConfigureAwait(true);
            child.HasExited.ShouldBeTrue();
        }
        finally
        {
            await launch.ConfigureAwait(true);
            if (child is not null) { await ReapAsync(child).ConfigureAwait(true); child.Dispose(); }
        }
        async Task LaunchAsync()
        {
            await Task.Delay(100, cleanup.Token).ConfigureAwait(true);
            if (uncertainStart)
            {
                requested.SetException(failure);
                await Task.Delay(100, cleanup.Token).ConfigureAwait(true);
            }
            child = StartChild();
        }
    }

    private static Process StartChild()
    {
        var start = new ProcessStartInfo("python3") { UseShellExecute = false };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import time; time.sleep(120)");
        return Process.Start(start) ?? throw new InvalidOperationException("The owned signal fixture did not start.");
    }

    private static char State(int pid)
    {
        string stat = File.ReadAllText($"/proc/{pid}/stat");
        return stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0][0];
    }

    private static async Task ReapAsync(Process process)
    {
        if (!process.HasExited) { process.Kill(entireProcessTree: true); }
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
