using System.Text.Json;

using Hexalith.Parties.IntegrationTests.HealthChecks;
using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Evidence persistence failures must not abandon an owned topology.</summary>
public sealed class PartyMemoryOwnedCleanupTests
{
    [Fact]
    public async Task RejectedPhaseReceiptCannotChooseEmergencyRecoveryTargets()
    {
        const string phase = "same-uri-refresh", secret = "owned-private", nonce = "owned-nonce";
        int pid = Environment.ProcessId;
        long birth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(pid)!.Value;
        JsonElement startup = JsonSerializer.SerializeToElement(new
        {
            Phase = phase, Secret = secret, Nonce = nonce, RunnerPid = pid, RunnerStart = birth,
            Observation = new { Phase = "startup-custody-only" }, Topology = new { Id = "owned-session", Marker = "owned-marker", Root = "owned-root" },
        });
        PartyMemoryRunnerInterruptionCustody custody = PartyMemoryRunnerInterruptionCustody.Authenticate(startup, phase, secret, nonce, pid, birth);
        JsonElement rejected = JsonSerializer.SerializeToElement(new
        {
            Phase = phase, Secret = secret, Nonce = nonce, RunnerPid = pid, RunnerStart = birth, CreatedUtc = DateTimeOffset.UtcNow,
            Observation = new { Phase = phase }, Topology = new { Id = "foreign-session", Marker = "foreign-marker", Root = "foreign-root" },
        });
        Should.Throw<InvalidDataException>(() => PartyMemoryRunnerInterruptionHandoff.Validate(rejected, phase, secret, nonce, pid, birth, custody.Session));
        Should.Throw<InvalidDataException>(() => PartyMemoryRunnerInterruptionCustody.Authenticate(rejected, phase, secret, nonce, pid, birth));
        bool recovered = false;
        await custody.RecoverAsync(topology =>
        {
            topology.GetProperty("Id").GetString().ShouldBe("owned-session");
            topology.GetProperty("Marker").GetString().ShouldBe("owned-marker");
            topology.GetProperty("Root").GetString().ShouldBe("owned-root");
            recovered = true;
            return Task.CompletedTask;
        }).ConfigureAwait(true);
        recovered.ShouldBeTrue();
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("nonce")]
    [InlineData("birth")]
    [InlineData("phase")]
    [InlineData("session")]
    public void ForeignOrStaleCheckpointCannotAuthorizeRunnerInterruption(string fault)
    {
        string nonce = new('a', 32);
        long start = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(Environment.ProcessId)!.Value;
        JsonElement checkpoint = JsonSerializer.SerializeToElement(new
        {
            Phase = fault == "phase" ? "output-known" : "same-uri-refresh", Secret = "owned-private", Nonce = fault == "nonce" ? new string('b', 32) : nonce,
            RunnerPid = Environment.ProcessId, RunnerStart = fault == "birth" ? start + 1 : start,
            CreatedUtc = DateTimeOffset.UtcNow.AddSeconds(fault == "stale" ? -30 : 0), Observation = new { Phase = "same-uri-refresh" },
            Topology = new { Id = fault == "session" ? "foreign-session" : "owned-session" },
        });
        Should.Throw<InvalidDataException>(() => PartyMemoryRunnerInterruptionHandoff.Validate(checkpoint, "same-uri-refresh", "owned-private", nonce, Environment.ProcessId, start, "owned-session"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EvidenceWriteFailureAlwaysDisposesAndPreservesEveryFailure(bool failDispose)
    {
        var primary = new InvalidOperationException("primary failure");
        var capture = new IOException("capture write failed");
        var cleanup = new IOException("disposal failed");
        var receipt = new IOException("receipt write failed");
        bool disposed = false, observedReceipt = false;
        AggregateException failure = await Should.ThrowAsync<AggregateException>(() => PartyMemoryOwnedRuntimeTests.CompleteOwnedRunAsync(
            () => Task.FromException(capture),
            () => { disposed = true; return failDispose ? Task.FromException(cleanup) : Task.CompletedTask; },
            (completed, evidenceFailure) =>
            {
                observedReceipt = true;
                completed.ShouldBe(!failDispose);
                evidenceFailure.ShouldNotBeNull();
                return Task.FromException(receipt);
            }, primary));
        disposed.ShouldBeTrue(); observedReceipt.ShouldBeTrue();
        failure.InnerExceptions.ShouldContain(primary); failure.InnerExceptions.ShouldContain(capture); failure.InnerExceptions.ShouldContain(receipt);
        if (failDispose) { failure.InnerExceptions.ShouldContain(cleanup); }
    }
    [Fact]
    public async Task ChildTerminationFailureStillSettlesCoordinationQuiescesAndRetainsAllFailures()
    {
        using var cancellation = new CancellationTokenSource();
        var primary = new InvalidOperationException("synthetic child qualification failure");
        var termination = new IOException("synthetic child termination failure");
        var quiescence = new IOException("synthetic quiescence evidence failure");
        bool reaped = false, settled = false, quiesced = false, persisted = false;
        Task coordination = WaitForCancellationAsync();
        try
        {
            AggregateException failure = await Should.ThrowAsync<AggregateException>(() => PartyMemoryOwnedRuntimeTests.CompleteChildRunAsync(
                () => Task.FromException(termination),
                () => { reaped = true; return Task.CompletedTask; },
                () => cancellation.CancelAsync(),
                () => coordination,
                () => { quiesced = true; return Task.FromException(quiescence); },
                () => { persisted = true; return Task.CompletedTask; }, primary));
            failure.InnerExceptions.ShouldContain(primary);
            failure.InnerExceptions.ShouldContain(termination);
            failure.InnerExceptions.ShouldContain(quiescence);
            reaped.ShouldBeTrue(); settled.ShouldBeTrue(); quiesced.ShouldBeTrue(); persisted.ShouldBeTrue();
            coordination.IsCompleted.ShouldBeTrue();
        }
        finally { await cancellation.CancelAsync(); await coordination.ConfigureAwait(true); }
        async Task WaitForCancellationAsync()
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token).ConfigureAwait(true); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            finally { settled = true; }
        }
    }
}
