using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Security;

public sealed class InMemoryPartyKeyRetrySchedulerTests
{
    [Fact]
    public async Task PendingStateTransitionsFromAbsentToMarkedToClearedAsync()
    {
        var scheduler = new InMemoryPartyKeyRetryScheduler();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        (await scheduler.IsPendingAsync("tenant-a", "party-1", cancellationToken)).ShouldBeFalse();

        await scheduler.MarkPendingAsync("tenant-a", "party-1", "transient key-store failure", cancellationToken);

        (await scheduler.IsPendingAsync("tenant-a", "party-1", cancellationToken)).ShouldBeTrue();

        await scheduler.ClearPendingAsync("tenant-a", "party-1", cancellationToken);

        (await scheduler.IsPendingAsync("tenant-a", "party-1", cancellationToken)).ShouldBeFalse();
    }
}
