using System.IO.Pipelines;
using System.Net;
using System.Text;

using Hexalith.Parties.Search;
using Hexalith.Parties.Testing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

public class PartyMemoryCleanupServiceTests
{
    [Fact]
    public async Task CleanupRetainsConcurrentMappingAndRequiresAnotherPass()
    {
        var original = new PartyMemoryUnitMappingEntry("memory-1", "urn:original", "case-original");
        var added = new PartyMemoryUnitMappingEntry("memory-2", "urn:added", "case-added");
        var state = new PartyMemoryMappingState(original);
        bool addDuringDelete = true;
        var handler = new RecordingHandler(() =>
        {
            if (addDuringDelete)
            {
                state.SetEntries([original, added]);
                addDuringDelete = false;
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") };
        var store = new PartyMemoryUnitMappingStore(
            state.Client,
            Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = "statestore" }),
            new RecordingLogger<PartyMemoryUnitMappingStore>());
        var service = new PartyMemoryCleanupService(client, store, NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult first = await service.DeleteByPartyAsync(
            "tenant", "case-current", "party", TestContext.Current.CancellationToken);

        first.Cleaned.ShouldBeFalse();
        first.BlockedReason.ShouldNotBeNull().ShouldContain("another cleanup pass");
        state.Entries.ShouldBe([added]);

        PartyMemoryCleanupResult second = await service.DeleteByPartyAsync(
            "tenant", "case-current", "party", TestContext.Current.CancellationToken);

        second.Cleaned.ShouldBeTrue();
        state.KeyExists.ShouldBeTrue();
        state.Entries.ShouldBeEmpty();
        handler.RequestUris.Select(uri => uri.AbsolutePath).ShouldBe([
            "/api/v1/tenants/tenant/cases/case-original/memory-units/memory-1",
            "/api/v1/tenants/tenant/cases/case-added/memory-units/memory-2",
        ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupPreservesFailedOtherCaseWithTheSameUnitId(bool concurrentAppend)
    {
        var cleaned = new PartyMemoryUnitMappingEntry("shared-unit", "urn:shared", "case-cleaned");
        var blocked = cleaned with { CaseId = "case-blocked" };
        var added = new PartyMemoryUnitMappingEntry("new-unit", "urn:new", "case-new");
        var state = new PartyMemoryMappingState(cleaned, blocked);
        var responses = new Queue<HttpStatusCode>([HttpStatusCode.NoContent, HttpStatusCode.ServiceUnavailable]);
        var handler = new RecordingHandler(() =>
        {
            if (concurrentAppend && responses.Count == 2)
            {
                state.SetEntries([cleaned, blocked, added]);
            }

            return new HttpResponseMessage(responses.Dequeue());
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") };
        var store = new PartyMemoryUnitMappingStore(
            state.Client,
            Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = "statestore" }),
            new RecordingLogger<PartyMemoryUnitMappingStore>());
        var service = new PartyMemoryCleanupService(client, store, NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service.DeleteByPartyAsync(
            "tenant", "case-current", "party", TestContext.Current.CancellationToken);

        result.Cleaned.ShouldBeFalse();
        result.BlockedReason.ShouldNotBeNull().ShouldContain("1 of 2 units deleted");
        result.MemoryUnitId.ShouldBe("shared-unit");
        state.Entries.ShouldBe(concurrentAppend ? [blocked, added] : [blocked]);
        handler.RequestUris.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CleanupDoesNotReportSuccessWhenItsConditionalInventoryUpdateFails()
    {
        var original = new PartyMemoryUnitMappingEntry("memory-1", "urn:original", "case-original");
        var state = new PartyMemoryMappingState(original) { RejectWrites = true };
        using var client = new HttpClient(new RecordingHandler(() => new HttpResponseMessage(HttpStatusCode.NoContent)))
        {
            BaseAddress = new Uri("https://memories.example/"),
        };
        var store = new PartyMemoryUnitMappingStore(
            state.Client,
            Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = "statestore" }),
            new RecordingLogger<PartyMemoryUnitMappingStore>());
        var service = new PartyMemoryCleanupService(client, store, NullLogger<PartyMemoryCleanupService>.Instance);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            service.DeleteByPartyAsync("tenant", "case-current", "party", TestContext.Current.CancellationToken));

        error.Message.ShouldBe("memory-unit-mapping-remove-conflict");
        state.Entries.ShouldBe([original]);
        state.DeleteAttempts.ShouldBe(0);
        state.SaveAttempts.ShouldBe(5);
    }

    [Fact]
    public async Task CleanupReconciliationReadFailureAfterHttpDeletionPropagatesSanitizedFailure()
    {
        const string providerMessage = "Ada Lovelace tenant-secret unit-secret urn:secret case-secret";
        var original = new PartyMemoryUnitMappingEntry("memory-1", "urn:original", "case-original");
        var state = new PartyMemoryMappingState(original);
        using var client = new HttpClient(new RecordingHandler(() =>
        {
            state.ReadFailure = new InvalidOperationException(providerMessage);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        })) { BaseAddress = new Uri("https://memories.example/") };
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = new PartyMemoryUnitMappingStore(
            state.Client, Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = "statestore" }), logger);
        var service = new PartyMemoryCleanupService(client, store, NullLogger<PartyMemoryCleanupService>.Instance);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            service.DeleteByPartyAsync("tenant", "case-current", "party", TestContext.Current.CancellationToken));

        error.Message.ShouldBe("memory-unit-mapping-read-failed");
        error.InnerException.ShouldBeNull();
        error.ToString().ShouldNotContain(providerMessage);
        state.Entries.ShouldBe([original]);
        logger.Records.ShouldHaveSingleItem().Exception.ShouldBeNull();
        logger.Records[0].Message.ShouldNotContain(providerMessage);
        (state.SaveAttempts + state.DeleteAttempts).ShouldBe(0);
    }

    [Fact]
    public async Task CleanupRetainsLegacyMappingAdoptedIntoACapturedCaseDuringReconciliation()
    {
        var legacy = new PartyMemoryUnitMappingEntry("memory-1", "urn:original");
        var state = new PartyMemoryMappingState(legacy);
        var handler = new RecordingHandler(() => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") };
        var store = new PartyMemoryUnitMappingStore(
            state.Client,
            Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = "statestore" }),
            new RecordingLogger<PartyMemoryUnitMappingStore>());
        state.BeforeMutation = () => store.RecordMappingAsync(
            "tenant", "party", legacy.MemoryUnitId, legacy.SourceUri, "case-captured", TestContext.Current.CancellationToken);
        var service = new PartyMemoryCleanupService(client, store, NullLogger<PartyMemoryCleanupService>.Instance);

        PartyMemoryCleanupResult result = await service.DeleteByPartyAsync(
            "tenant", "case-fallback", "party", TestContext.Current.CancellationToken);

        result.Cleaned.ShouldBeFalse();
        result.BlockedReason.ShouldNotBeNull().ShouldContain("another cleanup pass");
        state.Entries.ShouldBe([legacy with { CaseId = "case-captured" }]);
        handler.RequestUris.ShouldHaveSingleItem().AbsolutePath.ShouldBe(
            "/api/v1/tenants/tenant/cases/case-fallback/memory-units/memory-1");
    }

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
                _mappings.TryGetValue(key, out List<PartyMemoryUnitMappingEntry>? list) ? [.. list] : []);
        }

        public Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
        {
            _mappings.Remove($"{tenantId}:{partyId}");
            return Task.CompletedTask;
        }

        public Task<int> RemoveMappingsAsync(string tenantId, string partyId, IReadOnlyList<PartyMemoryUnitMappingEntry> entries, CancellationToken cancellationToken)
        {
            string key = $"{tenantId}:{partyId}";
            if (!_mappings.TryGetValue(key, out List<PartyMemoryUnitMappingEntry>? current))
            {
                return Task.FromResult(0);
            }

            _ = current.RemoveAll(entries.Contains);
            if (current.Count == 0)
            {
                _mappings.Remove(key);
            }

            return Task.FromResult(current.Count);
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

        public Task<int> RemoveMappingsAsync(
            string tenantId,
            string partyId,
            IReadOnlyList<PartyMemoryUnitMappingEntry> entries,
            CancellationToken cancellationToken)
            => Task.FromResult(0);
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
