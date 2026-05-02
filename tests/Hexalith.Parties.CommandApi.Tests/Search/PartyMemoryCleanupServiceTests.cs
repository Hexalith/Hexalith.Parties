using System.Net;

using Hexalith.Parties.CommandApi.Search;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Search;

public class PartyMemoryCleanupServiceTests
{
    [Fact]
    public async Task ErasureBlocksOrRecordsBlockedCompletionWhenMemoriesCleanupFails()
    {
        var handler = new TestHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            ReasonPhrase = "Unavailable",
        });
        var service = new PartyMemoryCleanupService(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://memories.example/"),
        });

        PartyMemoryCleanupResult result = await service
            .DeleteMemoryUnitAsync("tenant-a", "case-a", "party-1", "memory-1", CancellationToken.None)
            .ConfigureAwait(true);

        result.Cleaned.ShouldBeFalse();
        result.BlockedReason.ShouldNotBeNull();
        result.BlockedReason.ShouldContain("503");
        handler.LastRequestUri!.ToString().ShouldBe("https://memories.example/api/tenants/tenant-a/cases/case-a/memory-units/memory-1");
    }

    private sealed class TestHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(response);
        }
    }
}
