using System.Net;
using System.Text;

using Hexalith.Memories.Client.Rest;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Durable recovery through the actual published REST ingestion request and acknowledgement parser.</summary>
public sealed class PartyMemoryRestClientRecoveryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PublishedAcknowledgementAndTokenSurviveRestartWithoutSecondSubmission()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRestHandler();
        handler.Recovery.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed() with
        {
            RuntimeStatus = "Running", MemoryUnitId = null, MemoryUnitStatus = null,
        };
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var context = PartyMemoryRecoveryTests.Context() with { SequenceNumber = 7 };
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(PartyMemoryRecoveryTests.Entry(), context, Token))
            .ShouldNotBeNull().Indexed.ShouldBeFalse();
        var request = handler.Submissions.ShouldHaveSingleItem();
        request.GetProperty("tenantId").GetString().ShouldBe("tenant");
        request.GetProperty("caseId").GetString().ShouldBe("case");
        request.GetProperty("sourceUri").GetString().ShouldBe(PartyMemoryUrn.Build("tenant", "party"));
        request.GetProperty("ingestedBy").GetString().ShouldBe("Hexalith.Parties");
        request.GetProperty("contentType").GetString().ShouldBe("text/plain");
        Encoding.UTF8.GetString(request.GetProperty("contentBytes").GetBytesFromBase64()).ShouldContain("private-personal-name");
        string attempt = (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().AttemptId;
        request.GetProperty("idempotencyToken").GetString().ShouldBe(attempt);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().WorkflowInstanceId.ShouldBe("workflow");
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();

        handler.Recovery.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(PartyMemoryRecoveryTests.Entry(), context, Token))
            .ShouldNotBeNull().Indexed.ShouldBeTrue();
        handler.Submissions.ShouldHaveSingleItem();
        var retained = (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem();
        retained.AttemptId.ShouldBe(attempt);
        retained.MemoryUnitId.ShouldBe("canonical");
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
    }

    [Theory]
    [InlineData(401, "AUTHORIZATION_FAILED")]
    [InlineData(403, "AUTHORIZATION_FAILED")]
    [InlineData(404, "TENANT_NOT_FOUND")]
    [InlineData(409, "TENANT_PROVISIONING")]
    [InlineData(409, "TENANT_DELETING")]
    [InlineData(409, "TENANT_FAILED")]
    [InlineData(409, "TENANT_UNAVAILABLE")]
    [InlineData(400, "INVALID_INPUT")]
    [InlineData(429, "RATE_LIMIT_EXCEEDED")]
    public async Task PublishedPreflightErrorAllowsRepairWithTheSameDurableToken(int code, string errorCode)
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRestHandler
        {
            SubmissionStatus = (HttpStatusCode)code,
            SubmissionBody = "{\"code\":\"" + errorCode + "\",\"message\":\"Rejected before scheduling.\",\"suggestion\":\"Repair provisioning.\"}",
        };
        using var http = PartyMemoryRecoveryTests.Http(handler);
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBeFalse();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().SubmissionRejected.ShouldBeTrue();
        handler.Recovery.Requests.ShouldBeEmpty();
        string token = handler.Submissions.ShouldHaveSingleItem().GetProperty("idempotencyToken").GetString().ShouldNotBeNull();

        handler.SubmissionStatus = HttpStatusCode.Accepted;
        handler.SubmissionBody = "{\"instanceId\":\"workflow\"}";
        handler.Recovery.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBeTrue();
        handler.Submissions.Count.ShouldBe(2);
        handler.Submissions[1].GetProperty("idempotencyToken").GetString().ShouldBe(token);
        var persisted = (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem();
        persisted.SubmissionRejected.ShouldBeFalse();
        persisted.WorkflowInstanceId.ShouldBe("workflow");
        persisted.MemoryUnitId.ShouldBe("canonical");
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
    }

    [Theory]
    [InlineData(202, "")]
    [InlineData(202, "{}")]
    [InlineData(202, "{\"instanceId\":\"\"}")]
    [InlineData(200, "<html>proxy</html>")]
    [InlineData(429, "{\"code\":\"UNKNOWN_RATE_LIMIT\"}")]
    [InlineData(503, "{\"code\":\"RATE_LIMIT_EXCEEDED\"}")]
    public async Task PublishedAmbiguousAcknowledgementRemainsUnresolvedWithoutResubmission(int code, string body)
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRestHandler { SubmissionStatus = (HttpStatusCode)code, SubmissionBody = body };
        using var http = PartyMemoryRecoveryTests.Http(handler);
        for (int restart = 0; restart < 2; restart++)
        {
            (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
                .ShouldNotBeNull().Indexed.ShouldBeFalse();
        }

        handler.Submissions.ShouldHaveSingleItem();
        var unresolved = (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem();
        unresolved.SubmissionRejected.ShouldBeFalse();
        unresolved.WorkflowInstanceId.ShouldBeNull();
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeFalse();
        handler.Recovery.Requests.ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
    }

    internal static MemoriesClient Client(HttpClient http)
        => new(http, Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance);
}
