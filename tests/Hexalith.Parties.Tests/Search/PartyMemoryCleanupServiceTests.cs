using System.IO.Pipelines;
using System.Net;
using System.Text;

using Hexalith.Parties.Search;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

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
        handler.LastRequestUri!.ToString().ShouldBe("https://memories.example/api/v1/tenants/tenant-a/cases/case-a/memory-units/memory-1");
    }

    [Fact]
    public async Task DeleteByPartyAsyncReportsBlockedReasonOnTransportFailure()
    {
        var handler = new ThrowingHandler(new HttpRequestException("DNS failure"));
        var mappingStore = new StubMappingStore();
        // Seed a recorded mapping so DeleteByPartyAsync iterates and hits the throwing handler.
        // Without a recorded mapping the new per-unit cleanup contract returns Cleaned=true
        // (nothing to delete), which is the legitimate "party was never indexed" case.
        await mappingStore.RecordMappingAsync("tenant-a", "party-1", "memory-1", "urn:hexalith:parties:tenant-a:party:party-1", "case-a", CancellationToken.None);
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
        var mappingStore = new StubMappingStore();
        await mappingStore.RecordMappingAsync("tenant-a", "party-1", "memory-1", "urn:source-1", "case-a", CancellationToken.None);
        var service = new PartyMemoryCleanupService(
            new HttpClient(),
            mappingStore,
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
            Content = new StringContent("{\"code\":\"MEMORY_UNIT_NOT_FOUND\"}", Encoding.UTF8, "application/json"),
        });
        var mappingStore = new StubMappingStore();
        await mappingStore.RecordMappingAsync("tenant-a", "party-1", "memory-1", "urn:hexalith:parties:tenant-a:party:party-1", "case-a", CancellationToken.None);
        var service = new PartyMemoryCleanupService(
            new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") },
            mappingStore,
            NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service
            .DeleteByPartyAsync("tenant-a", "case-a", "party-1", CancellationToken.None)
            .ConfigureAwait(true);

        result.Cleaned.ShouldBeTrue();
        result.BlockedReason.ShouldBeNull();
        IReadOnlyList<PartyMemoryUnitMappingEntry> remaining = await mappingStore.GetMappingsAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);
        remaining.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(404, "")]
    [InlineData(404, "<html>Route not found</html>")]
    [InlineData(404, "{")]
    [InlineData(404, "null")]
    [InlineData(404, "{}")]
    [InlineData(404, "{\"code\":17}")]
    [InlineData(404, "{\"code\":\"CASE_NOT_FOUND\"}")]
    [InlineData(404, "{\"code\":\"TENANT_NOT_FOUND\"}")]
    [InlineData(404, "{\"code\":\"memory_unit_not_found\"}")]
    [InlineData(403, "{\"code\":\"MEMORY_UNIT_NOT_FOUND\"}")]
    [InlineData(503, "{\"code\":\"MEMORY_UNIT_NOT_FOUND\"}")]
    public async Task DeleteByPartyRetainsMappingWhenResponseDoesNotProveUnitAbsence(int status, string body)
    {
        using var response = new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        var handler = new TestHandler(response);
        var mappingStore = new StubMappingStore();
        CancellationToken token = TestContext.Current.CancellationToken;
        await mappingStore.RecordMappingAsync("tenant-a", "party-1", "memory-1", "urn:source-1", "case-at-ingestion", token);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") };
        var service = new PartyMemoryCleanupService(client, mappingStore, NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service.DeleteByPartyAsync("tenant-a", "case-current", "party-1", token);

        result.Cleaned.ShouldBeFalse();
        result.BlockedReason.ShouldNotBeNull().ShouldContain($"HTTP {status}");
        result.MemoryUnitId.ShouldBe("memory-1");
        IReadOnlyList<PartyMemoryUnitMappingEntry> remaining = await mappingStore.GetMappingsAsync("tenant-a", "party-1", token);
        remaining.ShouldHaveSingleItem().ShouldBe(new PartyMemoryUnitMappingEntry("memory-1", "urn:source-1", "case-at-ingestion"));
        handler.LastRequestUri!.AbsolutePath.ShouldBe("/api/v1/tenants/tenant-a/cases/case-at-ingestion/memory-units/memory-1");
    }

    [Fact]
    public async Task DeleteMemoryUnitUsesEscapedVersionedPathAndEndpointPrefix()
    {
        var handler = new TestHandler(new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/gateway/") };
        var service = new PartyMemoryCleanupService(client, new StubMappingStore(), NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service.DeleteMemoryUnitAsync(
            "tenant/a", "case a", "party-1", "memory?1", TestContext.Current.CancellationToken);

        result.Cleaned.ShouldBeTrue();
        handler.LastRequestUri!.AbsoluteUri.ShouldBe("https://memories.example/gateway/api/v1/tenants/tenant%2Fa/cases/case%20a/memory-units/memory%3F1");
    }

    [Fact]
    public async Task InvalidErrorCharsetRetainsMappingsAndFailsCleanupProbe()
    {
        var handler = new RecordingHandler(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"code\":\"MEMORY_UNIT_NOT_FOUND\"}", Encoding.UTF8, "application/json"),
            };
            response.Content.Headers.ContentType!.CharSet = "invalid-charset";
            return response;
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") };
        var mappingStore = new StubMappingStore();
        CancellationToken token = TestContext.Current.CancellationToken;
        await mappingStore.RecordMappingAsync("tenant-a", "party-1", "memory-1", "urn:source-1", "case-a", token);
        var service = new PartyMemoryCleanupService(client, mappingStore, NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult cleanup = await service.DeleteByPartyAsync("tenant-a", "case-a", "party-1", token);
        PartyMemoryCleanupProbeResult probe = await service.ProbeCleanupRouteAsync("tenant-a", "case-a", token);

        cleanup.Cleaned.ShouldBeFalse();
        IReadOnlyList<PartyMemoryUnitMappingEntry> remaining = await mappingStore.GetMappingsAsync("tenant-a", "party-1", token);
        remaining.ShouldHaveSingleItem().MemoryUnitId.ShouldBe("memory-1");
        probe.Reachable.ShouldBeFalse();
        probe.StatusCode.ShouldBe(404);
        probe.Reason.ShouldNotBeNull().ShouldContain("unqualified HTTP 404");
    }

    [Fact]
    public async Task MixedCleanupRetainsOnlyUnqualifiedMissingUnitAndReportsPartialFailure()
    {
        var responses = new Queue<HttpResponseMessage>([
            new(HttpStatusCode.NoContent),
            new(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"code\":\"MEMORY_UNIT_NOT_FOUND\"}", Encoding.UTF8, "application/json"),
            },
            new(HttpStatusCode.NotFound),
        ]);
        using var client = new HttpClient(new RecordingHandler(responses.Dequeue)) { BaseAddress = new Uri("https://memories.example/") };
        var mappingStore = new StubMappingStore();
        CancellationToken token = TestContext.Current.CancellationToken;
        foreach (string id in new[] { "deleted", "absent", "blocked" })
        {
            await mappingStore.RecordMappingAsync("tenant-a", "party-1", id, $"urn:{id}", "case-a", token);
        }
        var service = new PartyMemoryCleanupService(client, mappingStore, NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service.DeleteByPartyAsync("tenant-a", "case-a", "party-1", token);

        result.Cleaned.ShouldBeFalse();
        result.MemoryUnitId.ShouldBe("blocked");
        result.BlockedReason.ShouldNotBeNull().ShouldContain("2 of 3 units deleted");
        IReadOnlyList<PartyMemoryUnitMappingEntry> remaining = await mappingStore.GetMappingsAsync("tenant-a", "party-1", token);
        remaining.ShouldHaveSingleItem().ShouldBe(new PartyMemoryUnitMappingEntry("blocked", "urn:blocked", "case-a"));
        responses.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingUnitEnvelopeReadPropagatesCancellation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pipe = new Pipe();
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StreamContent(pipe.Reader.AsStream()),
        };
        try
        {
            Task<bool> reading = PartyMemoryCleanupResponse.IsAcceptedAsync(response, cancellation.Token);
            reading.IsCompleted.ShouldBeFalse();
            cancellation.Cancel();

            await Should.ThrowAsync<OperationCanceledException>(() => reading);
        }
        finally
        {
            await pipe.Writer.CompleteAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupAndProbePropagateBodyReadCancellationWithoutDroppingMappings(bool probe)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pipe = new Pipe();
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StreamContent(pipe.Reader.AsStream()),
        };
        using var client = new HttpClient(new TestHandler(response)) { BaseAddress = new Uri("https://memories.example/") };
        var mappingStore = new StubMappingStore();
        await mappingStore.RecordMappingAsync("tenant-a", "party-1", "memory-1", "urn:source-1", "case-a", cancellation.Token);
        var service = new PartyMemoryCleanupService(client, mappingStore, NullLogger<PartyMemoryCleanupService>.Instance);
        try
        {
            Task reading = probe
                ? service.ProbeCleanupRouteAsync("tenant-a", "case-a", cancellation.Token)
                : service.DeleteByPartyAsync("tenant-a", "case-a", "party-1", cancellation.Token);
            reading.IsCompleted.ShouldBeFalse();
            cancellation.Cancel();

            await Should.ThrowAsync<OperationCanceledException>(() => reading);
            IReadOnlyList<PartyMemoryUnitMappingEntry> remaining = await mappingStore.GetMappingsAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);
            remaining.ShouldHaveSingleItem().MemoryUnitId.ShouldBe("memory-1");
        }
        finally
        {
            await pipe.Writer.CompleteAsync();
        }
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
    public async Task DeleteByPartyAsync_MappingReadFailureReportsFailedInsteadOfCleaned()
    {
        var service = new PartyMemoryCleanupService(
            new HttpClient(new TestHandler(new HttpResponseMessage(HttpStatusCode.OK)))
            {
                BaseAddress = new Uri("https://memories.example/"),
            },
            new ThrowingMappingStore(new InvalidOperationException("Ada Lovelace mapping unavailable")),
            NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service.DeleteByPartyAsync(
            "tenant-secret",
            "case-secret",
            "party-secret",
            TestContext.Current.CancellationToken);

        result.Cleaned.ShouldBeFalse();
        result.BlockedReason.ShouldBe("Memories cleanup could not verify its mapping inventory.");
        string blockedReason = result.BlockedReason!;
        blockedReason.ShouldNotContain("Ada Lovelace");
        blockedReason.ShouldNotContain("tenant-secret");
        blockedReason.ShouldNotContain("party-secret");
    }

    [Fact]
    public async Task DeleteByPartyAsync_MappingReadCancellationPropagates()
    {
        var service = new PartyMemoryCleanupService(
            new HttpClient(),
            new ThrowingMappingStore(new OperationCanceledException()),
            NullLogger<PartyMemoryCleanupService>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(() => service.DeleteByPartyAsync(
            "tenant-a",
            "case-a",
            "party-1",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteByPartyAsyncIteratesEveryRecordedMapping()
    {
        // Return a fresh HttpResponseMessage per call — the response body is disposed after
        // each HttpClient.SendAsync, so reusing a single instance breaks subsequent iterations.
        var handler = new RecordingHandler(() => new HttpResponseMessage(HttpStatusCode.OK));
        var mappingStore = new StubMappingStore();
        await mappingStore.RecordMappingAsync("tenant-a", "party-1", "memory-1", "urn:source-1", "case-a", CancellationToken.None);
        await mappingStore.RecordMappingAsync("tenant-a", "party-1", "memory-2", "urn:source-2", "case-a", CancellationToken.None);
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
        // case segment (e.g., dispatching to /api/v1/tenants/tenant-a/memory-units/memory-1)
        // fails the test instead of slipping past a permissive EndsWith match.
        HashSet<string> requestedAbsolutePaths = [.. handler.RequestUris.Select(u => u.AbsolutePath)];
        requestedAbsolutePaths.ShouldContain("/api/v1/tenants/tenant-a/cases/case-a/memory-units/memory-1");
        requestedAbsolutePaths.ShouldContain("/api/v1/tenants/tenant-a/cases/case-a/memory-units/memory-2");
        // Mappings are cleared on full success so a recreated party with the same id starts fresh.
        IReadOnlyList<PartyMemoryUnitMappingEntry> remaining = await mappingStore.GetMappingsAsync("tenant-a", "party-1", CancellationToken.None);
        remaining.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteByPartyAsync_UsesPersistedCaseIdInsteadOfCurrentFallback()
    {
        var handler = new RecordingHandler(() => new HttpResponseMessage(HttpStatusCode.NoContent));
        var mappingStore = new StubMappingStore();
        await mappingStore.RecordMappingAsync(
            "tenant-a",
            "party-1",
            "memory-old",
            "urn:source-old",
            "case-at-ingestion",
            CancellationToken.None);
        var service = new PartyMemoryCleanupService(
            new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") },
            mappingStore,
            NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service.DeleteByPartyAsync(
            "tenant-a",
            "case-current",
            "party-1",
            CancellationToken.None);

        result.Cleaned.ShouldBeTrue();
        handler.RequestUris.ShouldHaveSingleItem().AbsolutePath.ShouldContain("/cases/case-at-ingestion/");
    }

    [Fact]
    public async Task DeleteByPartyAsync_LegacyMappingUsesCurrentCaseIdFallback()
    {
        var handler = new RecordingHandler(() => new HttpResponseMessage(HttpStatusCode.NoContent));
        var mappingStore = new StubMappingStore();
        mappingStore.AddLegacyMapping("tenant-a", "party-1", "memory-legacy", "urn:source-legacy");
        var service = new PartyMemoryCleanupService(
            new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") },
            mappingStore,
            NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service.DeleteByPartyAsync(
            "tenant-a",
            "case-current",
            "party-1",
            CancellationToken.None);

        result.Cleaned.ShouldBeTrue();
        handler.RequestUris.ShouldHaveSingleItem().AbsolutePath.ShouldContain("/cases/case-current/");
    }

    private sealed class StubMappingStore : IPartyMemoryUnitMappingStore
    {
        private readonly Dictionary<string, List<PartyMemoryUnitMappingEntry>> _mappings = new(StringComparer.Ordinal);

        public void AddLegacyMapping(string tenantId, string partyId, string memoryUnitId, string sourceUri)
        {
            string key = $"{tenantId}:{partyId}";
            if (!_mappings.TryGetValue(key, out List<PartyMemoryUnitMappingEntry>? list))
            {
                list = [];
                _mappings[key] = list;
            }

            list.Add(new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri));
        }

        public Task RecordMappingAsync(
            string tenantId,
            string partyId,
            string memoryUnitId,
            string sourceUri,
            string caseId,
            CancellationToken cancellationToken)
        {
            string key = $"{tenantId}:{partyId}";
            if (!_mappings.TryGetValue(key, out List<PartyMemoryUnitMappingEntry>? list))
            {
                list = [];
                _mappings[key] = list;
            }

            list.Add(new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri, caseId));
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

    private sealed class ThrowingMappingStore(Exception exception) : IPartyMemoryUnitMappingStore
    {
        public Task RecordMappingAsync(
            string tenantId,
            string partyId,
            string memoryUnitId,
            string sourceUri,
            string caseId,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(
            string tenantId,
            string partyId,
            CancellationToken cancellationToken)
            => Task.FromException<IReadOnlyList<PartyMemoryUnitMappingEntry>>(exception);

        public Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ReplaceMappingsAsync(
            string tenantId,
            string partyId,
            IReadOnlyList<PartyMemoryUnitMappingEntry> entries,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
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
