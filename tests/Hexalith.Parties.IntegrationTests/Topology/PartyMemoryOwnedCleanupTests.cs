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
}
