using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Evidence persistence failures must not abandon an owned topology.</summary>
public sealed class PartyMemoryOwnedCleanupTests
{
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
