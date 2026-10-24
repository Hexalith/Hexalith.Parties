using System.Net;

using Hexalith.Parties.Search;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Cancelled cleanup retains producer evidence and independently persists verified progress.</summary>
public sealed class PartyMemoryCleanupCancellationTests
{
    [Fact]
    public async Task CancelledObservationRetainsMappingsAndCanonicalEvidenceAcrossRestart()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        handler.Units.Add("canonical");
        using var http = PartyMemoryRecoveryTests.Http(handler);
        await PartyMemoryRecoveryTests.Indexer(state, new PartyMemoryRecoveryClient(http)).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), token);
        handler.BeforeRequest = (request, _) =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/canonical", StringComparison.Ordinal))
            {
                caller.Cancel();
                return Task.FromCanceled(caller.Token);
            }

            return Task.CompletedTask;
        };

        await Should.ThrowAsync<OperationCanceledException>(() => PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", caller.Token));

        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token)).Count.ShouldBe(1);
        handler.BeforeRequest = null;
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeTrue();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CancellationAfterFirstTerminalDeletionKeepsEvidenceForUnvisitedMappings()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        var state = new PartyMemoryTestReadModelStore();
        var first = new PartyMemoryIngestionIntent("first", "case", "urn:first", "workflow-first", "canonical-first");
        var second = new PartyMemoryIngestionIntent("second", "case", "urn:second", "workflow-second", "canonical-second");
        foreach (PartyMemoryIngestionIntent intent in new[] { first, second })
        {
            await PartyMemoryRecoveryTests.Intents(state).RegisterAsync("tenant", "party", intent, token);
            await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", intent.MemoryUnitId!, intent.SourceUri, intent.CaseId, token);
        }

        var handler = new PartyMemoryRecoveryHandler();
        handler.BeforeRequest = (request, _) =>
        {
            if (request.Method == HttpMethod.Delete && request.RequestUri!.AbsolutePath.EndsWith("/canonical-second", StringComparison.Ordinal))
            {
                caller.Cancel();
                return Task.FromCanceled(caller.Token);
            }

            return Task.CompletedTask;
        };
        using var http = PartyMemoryRecoveryTests.Http(handler);

        await Should.ThrowAsync<OperationCanceledException>(() => PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", caller.Token));

        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldHaveSingleItem().ShouldBe(second);
        IReadOnlyList<PartyMemoryIngestionIntent> retained = await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync("tenant", "party", token);
        retained.Count.ShouldBe(2);
        retained.ShouldContain(first with { CleanupSettled = true, AbsenceAuthority = PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress) });
        retained.ShouldContain(second);
        handler.BeforeRequest = null;
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeTrue();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CancelledCleanupPersistsVerifiedLegacyProgressWithIndependentBudget()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        var state = new PartyMemoryTestReadModelStore();
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "first", "urn:first", "case", token);
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "second", "urn:second", "case", token);
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["first"] = PartyMemoryRecoveryHandler.Completed("first", "canonical-first");
        handler.Workflows["second"] = PartyMemoryRecoveryHandler.Completed("second", "canonical-second");
        handler.BeforeRequest = (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/ingest/second", StringComparison.Ordinal))
            {
                caller.Cancel();
                return Task.FromCanceled(caller.Token);
            }

            return Task.CompletedTask;
        };
        using var http = PartyMemoryRecoveryTests.Http(handler);

        await Should.ThrowAsync<OperationCanceledException>(() => PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", caller.Token));

        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token)).ShouldBe([new("second", "urn:second", "case")]);
    }

    [Fact]
    public async Task CancellationAfterVerifiedAbsencePersistsReceiptBeforeUnvisitedMappingSubtraction()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        var state = new PartyMemoryTestReadModelStore();
        var intent = new PartyMemoryIngestionIntent("attempt", "case", "urn:source", "workflow", "canonical");
        await PartyMemoryRecoveryTests.Intents(state).RegisterAsync("tenant", "party", intent, token);
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "canonical", intent.SourceUri, "case", token);
        var mapping = (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token)).ShouldHaveSingleItem();
        List<HttpMethod> requests = [];
        using var transport = new PartySearchErasureHttpHandler((request, _) =>
        {
            requests.Add(request.Method);
            return Task.FromResult(request.Method == HttpMethod.Delete
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new PartyMemoryCancellationContent(caller.Cancel) });
        });
        using var http = PartyMemoryRecoveryTests.Http(transport);
        (await Should.ThrowAsync<OperationCanceledException>(() => PartyMemoryRecoveryTests.Cleanup(state, http)
            .DeleteByPartyAsync("tenant", "case", "party", caller.Token))).CancellationToken.ShouldBe(caller.Token);
        requests.ShouldBe([HttpMethod.Delete, HttpMethod.Get]);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token)).ShouldBe([mapping]);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync("tenant", "party", token)).ShouldHaveSingleItem()
            .ShouldBe(intent with { CleanupSettled = true, AbsenceAuthority = PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress) });

        var denied = new ProbeHandler(401, "{}");
        using var deniedHttp = PartyMemoryRecoveryTests.Http(denied);
        (await PartyMemoryRecoveryTests.Cleanup(state, deniedHttp).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();
        denied.Requests.ShouldBeGreaterThan(0);
        (await PartyMemoryRecoveryTests.Intents(state).GetSourceClosureVersionAsync("tenant", "party", token)).ShouldBeNull();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token)).ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync("tenant", "party", token)).ShouldHaveSingleItem()
            .ShouldBe(intent with { CleanupSettled = true, AbsenceAuthority = PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress) });
    }

    [Fact]
    public async Task CallerCancelledBeforeInventoryReadCannotReportCleaned()
    {
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        using var http = PartyMemoryRecoveryTests.Http(new PartyMemoryRecoveryHandler());
        await Should.ThrowAsync<OperationCanceledException>(() => PartyMemoryRecoveryTests.Cleanup(new PartyMemoryTestReadModelStore(), http)
            .DeleteByPartyAsync("tenant", "case", "party", caller.Token));
    }
}
