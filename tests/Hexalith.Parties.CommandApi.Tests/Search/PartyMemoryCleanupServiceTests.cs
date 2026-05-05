using System.Net;

using Hexalith.Parties.CommandApi.Search;

using Microsoft.Extensions.Logging.Abstractions;

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
        var service = new PartyMemoryCleanupService(
            new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") },
            new StubMappingStore(),
            NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service
            .DeleteMemoryUnitAsync("tenant-a", "case-a", "party-1", "memory-1", CancellationToken.None)
            .ConfigureAwait(true);

        result.Cleaned.ShouldBeFalse();
        result.BlockedReason.ShouldNotBeNull();
        result.BlockedReason.ShouldContain("503");
        handler.LastRequestUri!.ToString().ShouldBe("https://memories.example/api/tenants/tenant-a/cases/case-a/memory-units/memory-1");
    }

    [Fact]
    public async Task DeleteByPartyAsyncReportsBlockedReasonOnTransportFailure()
    {
        var handler = new ThrowingHandler(new HttpRequestException("DNS failure"));
        var mappingStore = new StubMappingStore();
        // Seed a recorded mapping so DeleteByPartyAsync iterates and hits the throwing handler.
        // Without a recorded mapping the new per-unit cleanup contract returns Cleaned=true
        // (nothing to delete), which is the legitimate "party was never indexed" case.
        await mappingStore.RecordMappingAsync("tenant-a", "party-1", "memory-1", "urn:hexalith:parties:tenant-a:party:party-1", CancellationToken.None);
        var service = new PartyMemoryCleanupService(
            new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") },
            mappingStore,
            NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service
            .DeleteByPartyAsync("tenant-a", "case-a", "party-1", CancellationToken.None)
            .ConfigureAwait(true);

        result.Cleaned.ShouldBeFalse();
        result.BlockedReason.ShouldNotBeNull();
        result.BlockedReason.ShouldContain("transport error");
    }

    [Fact]
    public async Task DeleteByPartyAsyncReportsBlockedReasonWhenBaseAddressIsMissing()
    {
        var service = new PartyMemoryCleanupService(
            new HttpClient(),
            new StubMappingStore(),
            NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service
            .DeleteByPartyAsync("tenant-a", "case-a", "party-1", CancellationToken.None)
            .ConfigureAwait(true);

        result.Cleaned.ShouldBeFalse();
        result.BlockedReason.ShouldNotBeNull();
        result.BlockedReason.ShouldContain("BaseAddress");
    }

    [Fact]
    public async Task DeleteByPartyAsyncTreatsAlreadyMissingUnitsAsCleaned()
    {
        var handler = new TestHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            ReasonPhrase = "Not Found",
        });
        var mappingStore = new StubMappingStore();
        await mappingStore.RecordMappingAsync("tenant-a", "party-1", "memory-1", "urn:hexalith:parties:tenant-a:party:party-1", CancellationToken.None);
        var service = new PartyMemoryCleanupService(
            new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") },
            mappingStore,
            NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service
            .DeleteByPartyAsync("tenant-a", "case-a", "party-1", CancellationToken.None)
            .ConfigureAwait(true);

        result.Cleaned.ShouldBeTrue();
        result.BlockedReason.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteByPartyAsyncWithNoRecordedMappingsReportsCleaned()
    {
        var handler = new TestHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var service = new PartyMemoryCleanupService(
            new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") },
            new StubMappingStore(),
            NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service
            .DeleteByPartyAsync("tenant-a", "case-a", "party-1", CancellationToken.None)
            .ConfigureAwait(true);

        result.Cleaned.ShouldBeTrue();
        result.BlockedReason.ShouldBeNull();
        handler.LastRequestUri.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteByPartyAsyncIteratesEveryRecordedMapping()
    {
        // Return a fresh HttpResponseMessage per call — the response body is disposed after
        // each HttpClient.SendAsync, so reusing a single instance breaks subsequent iterations.
        var handler = new RecordingHandler(() => new HttpResponseMessage(HttpStatusCode.OK));
        var mappingStore = new StubMappingStore();
        await mappingStore.RecordMappingAsync("tenant-a", "party-1", "memory-1", "urn:source-1", CancellationToken.None);
        await mappingStore.RecordMappingAsync("tenant-a", "party-1", "memory-2", "urn:source-2", CancellationToken.None);
        var service = new PartyMemoryCleanupService(
            new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") },
            mappingStore,
            NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service
            .DeleteByPartyAsync("tenant-a", "case-a", "party-1", CancellationToken.None)
            .ConfigureAwait(true);

        result.Cleaned.ShouldBeTrue();
        handler.RequestUris.Count.ShouldBe(2);
        // P22: Assert URI set + path shape includes /cases/ so a regression that drops the
        // case segment (e.g., dispatching to /api/tenants/tenant-a/memory-units/memory-1)
        // fails the test instead of slipping past a permissive EndsWith match.
        HashSet<string> requestedAbsolutePaths = [.. handler.RequestUris.Select(u => u.AbsolutePath)];
        requestedAbsolutePaths.ShouldContain("/api/tenants/tenant-a/cases/case-a/memory-units/memory-1");
        requestedAbsolutePaths.ShouldContain("/api/tenants/tenant-a/cases/case-a/memory-units/memory-2");
        // Mappings are cleared on full success so a recreated party with the same id starts fresh.
        IReadOnlyList<PartyMemoryUnitMappingEntry> remaining = await mappingStore.GetMappingsAsync("tenant-a", "party-1", CancellationToken.None);
        remaining.ShouldBeEmpty();
    }

    private sealed class StubMappingStore : IPartyMemoryUnitMappingStore
    {
        private readonly Dictionary<string, List<PartyMemoryUnitMappingEntry>> _mappings = new(StringComparer.Ordinal);

        public Task RecordMappingAsync(string tenantId, string partyId, string memoryUnitId, string sourceUri, CancellationToken cancellationToken)
        {
            string key = $"{tenantId}:{partyId}";
            if (!_mappings.TryGetValue(key, out List<PartyMemoryUnitMappingEntry>? list))
            {
                list = [];
                _mappings[key] = list;
            }

            list.Add(new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
        {
            string key = $"{tenantId}:{partyId}";
            return Task.FromResult<IReadOnlyList<PartyMemoryUnitMappingEntry>>(
                _mappings.TryGetValue(key, out List<PartyMemoryUnitMappingEntry>? list) ? list : []);
        }

        public Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
        {
            _mappings.Remove($"{tenantId}:{partyId}");
            return Task.CompletedTask;
        }

        public Task ReplaceMappingsAsync(string tenantId, string partyId, IReadOnlyList<PartyMemoryUnitMappingEntry> entries, CancellationToken cancellationToken)
        {
            string key = $"{tenantId}:{partyId}";
            if (entries.Count == 0)
            {
                _mappings.Remove(key);
            }
            else
            {
                _mappings[key] = [.. entries];
            }

            return Task.CompletedTask;
        }
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

    private sealed class RecordingHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
            {
                RequestUris.Add(request.RequestUri);
            }

            return Task.FromResult(responseFactory());
        }
    }

    private sealed class ThrowingHandler(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(ex);
    }
}
