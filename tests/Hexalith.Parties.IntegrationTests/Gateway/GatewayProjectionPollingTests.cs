using System.Net;

using Hexalith.Parties.Client;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Gateway;

/// <summary>Checks bounded projection polling diagnostics independently of the external topology.</summary>
public sealed class GatewayProjectionPollingTests
{
    /// <summary>The polling deadline retains the last structured gateway refusal and correlation.</summary>
    /// <param name="status">The transient gateway status.</param>
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task OwnDeadlineRetainsStructuredGatewayRefusalAsync(HttpStatusCode status)
    {
        using var client = new HttpClient(new QueryRefusalHandler(status)) { BaseAddress = new Uri("https://gateway.test/") };
        TimeoutException failure = await Should.ThrowAsync<TimeoutException>(() => EventStoreGatewayE2ETests.PollProjectionAsync(
            client, "party-1", "Synthetic", TestContext.Current.CancellationToken, TimeSpan.FromMilliseconds(500))).ConfigureAwait(true);
        failure.Message.ShouldContain($"HTTP {(int)status}");
        failure.Message.ShouldContain("Catalog unavailable");
        failure.Message.ShouldContain("https://hexalith.com/problems/catalog-unavailable");
        failure.Message.ShouldContain("Routing catalog unavailable.");
        failure.Message.ShouldContain("query-correlation-1");
        failure.InnerException.ShouldBeOfType<PartiesClientException>().CorrelationId.ShouldBe("query-correlation-1");
    }

    /// <summary>Caller cancellation remains cancellation after the client has observed a refusal.</summary>
    [Fact]
    public async Task ExternalCancellationRemainsCancellationAsync()
    {
        using var external = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new QueryRefusalHandler(HttpStatusCode.ServiceUnavailable, () => observed.TrySetResult()))
        {
            BaseAddress = new Uri("https://gateway.test/"),
        };
        Task polling = EventStoreGatewayE2ETests.PollProjectionAsync(client, "party-1", "Synthetic", external.Token);
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
        await external.CancelAsync().ConfigureAwait(true);
        await Should.ThrowAsync<OperationCanceledException>(() => polling).ConfigureAwait(true);
    }
}
