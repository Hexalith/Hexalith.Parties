using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Json;
using System.Text;

using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Legacy inventory compatibility and strict cleanup-route evidence.</summary>
public sealed class PartyMemoryCleanupServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(404, "INGESTION_STATUS_NOT_FOUND")]
    [InlineData(410, "INGESTION_STATUS_EXPIRED")]
    [InlineData(503, "SERVICE_UNAVAILABLE")]
    [InlineData(0, "transport")]
    public async Task MissingExpiredOrUnavailableSupersededStatusRetainsOnlyItsUncertainObligation(int statusCode, string code)
    {
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Workflows["older"] = PartyMemoryRecoveryHandler.Completed("older", "older-unit") with
        { RuntimeStatus = "Running", MemoryUnitId = null, MemoryUnitStatus = null };
        remote.Workflows["newer"] = PartyMemoryRecoveryHandler.Completed("newer", "newer-unit");
        remote.Units.UnionWith(["older-unit", "newer-unit"]);
        using var http = PartyMemoryRecoveryTests.Http(remote);
        (await PartyMemoryRecoveryTests.Indexer(state, new PartyMemoryRecoveryClient(http) { Submit = _ => Task.FromResult("older") })
            .IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context() with { SequenceNumber = 1 }, Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        (await PartyMemoryRecoveryTests.Indexer(state, new PartyMemoryRecoveryClient(http) { Submit = _ => Task.FromResult("newer") })
            .IndexAsync(PartyMemoryRecoveryTests.Entry() with { DisplayName = "newer" }, PartyMemoryRecoveryTests.Context() with { SequenceNumber = 2 }, Token)).ShouldNotBeNull().Indexed.ShouldBeTrue();
        PartyMemoryIngestionIntent original = (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token))
            .Single(item => item.WorkflowInstanceId == "older");
        remote.Workflows.Remove("older");
        if (statusCode == 0)
        {
            remote.BeforeRequest = (request, _) => request.RequestUri!.AbsolutePath.EndsWith("/older", StringComparison.Ordinal)
                ? Task.FromException(new HttpRequestException("Status unavailable.")) : Task.CompletedTask;
        }
        else
        {
            remote.WorkflowErrors["older"] = ((HttpStatusCode)statusCode, "{\"code\":\"" + code + "\"}");
        }

        for (int restart = 0; restart < 2; restart++)
        {
            (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeFalse();
            (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldBe([original]);
            (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
            remote.Units.ShouldBe(["older-unit"]);
        }

        remote.BeforeRequest = null;
        remote.WorkflowErrors.Clear();
        remote.Workflows["older"] = PartyMemoryRecoveryHandler.Completed("older", "older-unit");
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        remote.Units.ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("case")]
    public async Task LegacyInventoryUsesCapturedCaseOrFallbackAndResolvesCanonicalOutput(string? capturedCase)
    {
        var state = new PartyMemoryTestReadModelStore();
        var original = new PartyMemoryUnitMappingEntry("workflow", "urn:legacy", capturedCase);
        await state.TrySaveAsync("statestore", PartyMemoryUnitMappingStore.BuildKey("tenant", "party"), new List<PartyMemoryUnitMappingEntry> { original }, string.Empty, Token);
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        handler.Units.Add("canonical");
        using var http = PartyMemoryRecoveryTests.Http(handler);

        PartyMemoryCleanupResult result = await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token);

        result.Cleaned.ShouldBeTrue();
        handler.Requests.ShouldBe([
            (HttpMethod.Get, "/gateway/api/v1/ingest/workflow"),
            (HttpMethod.Delete, "/gateway/api/v1/tenants/tenant/cases/case/memory-units/canonical"),
            (HttpMethod.Get, "/gateway/api/v1/tenants/tenant/cases/case/memory-units/canonical"),
        ]);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
        state.SerializedValues.ShouldContain(json => json == "[]");
    }

    [Fact]
    public async Task MissingCaseBlocksLegacyCleanupUntilFallbackReturns()
    {
        var state = new PartyMemoryTestReadModelStore();
        await state.TrySaveAsync("statestore", PartyMemoryUnitMappingStore.BuildKey("tenant", "party"),
            new List<PartyMemoryUnitMappingEntry> { new("workflow", "urn:legacy") }, string.Empty, Token);
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var http = PartyMemoryRecoveryTests.Http(handler);

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "", "party", Token)).Cleaned.ShouldBeFalse();
        handler.Requests.ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).Count.ShouldBe(1);

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task LegacyMissingStatusRetainsInventoryEvenWhenUnitWouldBeAbsent()
    {
        var state = new PartyMemoryTestReadModelStore();
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "unknown", "urn:legacy", "case", Token);
        var handler = new PartyMemoryRecoveryHandler();
        using var http = PartyMemoryRecoveryTests.Http(handler);

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeFalse();

        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrentMappingArrivalSurvivesExactCleanupSnapshot()
    {
        var state = new PartyMemoryTestReadModelStore();
        PartyMemoryUnitMappingStore mapping = PartyMemoryRecoveryTests.Mappings(state);
        await mapping.RecordMappingAsync("tenant", "party", "workflow", "urn:original", "case", Token);
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        handler.Workflows["new-workflow"] = PartyMemoryRecoveryHandler.Completed("new-workflow", "new-canonical");
        bool added = false;
        handler.BeforeRequest = async (request, token) =>
        {
            if (!added && request.Method == HttpMethod.Delete)
            {
                added = true;
                await mapping.RecordMappingAsync("tenant", "party", "new-workflow", "urn:new", "case", token).ConfigureAwait(false);
            }
        };
        using var http = PartyMemoryRecoveryTests.Http(handler);

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeFalse();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBe([new("new-workflow", "urn:new", "case")]);
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
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
    public async Task ResponseWithoutQualifiedUnitAbsenceCannotProveDeletion(int status, string body)
    {
        using var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        (await PartyMemoryCleanupResponse.IsAbsentAsync(response, Token)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(200)]
    [InlineData(202)]
    [InlineData(204)]
    public async Task SuccessfulDeleteIsRouteAcceptanceAndNeverAbsenceProof(int code)
    {
        using var response = new HttpResponseMessage((HttpStatusCode)code);
        (await PartyMemoryCleanupResponse.IsAcceptedAsync(response, Token)).ShouldBeTrue();
        (await PartyMemoryCleanupResponse.IsAbsentAsync(response, Token)).ShouldBeFalse();
    }

    [Fact]
    public async Task QualifiedMissingUnitIsAcceptedAndProvesScopedAbsence()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        { Content = JsonContent.Create(new { code = "MEMORY_UNIT_NOT_FOUND" }) };
        (await PartyMemoryCleanupResponse.IsAcceptedAsync(response, Token)).ShouldBeTrue();
        using var observation = new HttpResponseMessage(HttpStatusCode.NotFound)
        { Content = JsonContent.Create(new { code = "MEMORY_UNIT_NOT_FOUND" }) };
        (await PartyMemoryCleanupResponse.IsAbsentAsync(observation, Token)).ShouldBeTrue();
    }

    [Fact]
    public async Task InvalidErrorCharsetIsNeverAbsenceProof()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        { Content = new StringContent("{\"code\":\"MEMORY_UNIT_NOT_FOUND\"}", Encoding.UTF8, "application/json") };
        response.Content.Headers.ContentType!.CharSet = "invalid-charset";
        (await PartyMemoryCleanupResponse.IsAbsentAsync(response, Token)).ShouldBeFalse();
    }

    [Fact]
    public async Task MissingUnitEnvelopeReadPropagatesCancellation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var pipe = new Pipe();
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StreamContent(pipe.Reader.AsStream()) };
        try
        {
            Task<bool> reading = PartyMemoryCleanupResponse.IsAbsentAsync(response, cancellation.Token);
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
    [InlineData(200, "", true)]
    [InlineData(204, "", true)]
    [InlineData(404, "{\"code\":\"MEMORY_UNIT_NOT_FOUND\"}", true)]
    [InlineData(404, "", false)]
    [InlineData(403, "", false)]
    public async Task ProbeRetainsReachabilityMeaningWithoutClaimingErasure(int code, string body, bool reachable)
    {
        ArgumentNullException.ThrowIfNull(body);
        using var http = PartyMemoryRecoveryTests.Http(new ProbeHandler(code, body));
        PartyMemoryCleanupProbeResult probe = await PartyMemoryRecoveryTests.Cleanup(new PartyMemoryTestReadModelStore(), http)
            .ProbeCleanupRouteAsync("tenant/a", "case a", Token);
        probe.Reachable.ShouldBe(reachable);
        probe.StatusCode.ShouldBe(code);
    }

    [Fact]
    public async Task EmptyInventoriesReportCleanedWithoutCallingMemoriesAndFenceFutureAdmission()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        using var http = PartyMemoryRecoveryTests.Http(handler);
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        handler.Requests.ShouldBeEmpty();
        await Should.ThrowAsync<InvalidOperationException>(() => PartyMemoryRecoveryTests.Intents(state)
            .RegisterAsync("tenant", "party", new("future", "case", "urn:source"), Token));
    }

    [Fact]
    public void AuthorizationConfiguresBearerTokenAndIgnoresMalformedHeader()
    {
        using var http = new HttpClient();
        PartyMemoryCleanupService.ConfigureAuthorization(http, "token");
        http.DefaultRequestHeaders.Authorization!.ToString().ShouldBe("Bearer token");
        PartyMemoryCleanupService.ConfigureAuthorization(http, "bad\r\ntoken", NullLogger.Instance);
        http.DefaultRequestHeaders.Authorization!.ToString().ShouldBe("Bearer token");
    }
}
