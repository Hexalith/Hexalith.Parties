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

#if HEXALITH_MEMORIES_SOURCE_REFRESH
    [Fact]
    public async Task OriginalRefreshBindingSurvivesCanonicalSerializationAndChangedDeliveryContext()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRestHandler { SubmissionBody = "{}" };
        var original = PartyMemoryRecoveryTests.Entry() with
        {
            SearchableContactChannels = [new Hexalith.Parties.Contracts.ValueObjects.ContactChannel
                { Id = "contact", Type = Hexalith.Parties.Contracts.ValueObjects.ContactChannelType.Email, Value = "synthetic@example.test" }],
        };
        var context = PartyMemoryRecoveryTests.Context() with { SequenceNumber = 19 };
        handler.Recovery.InventoryRequest = (_, _) =>
        {
            var submitted = handler.Submissions.Single();
            var digest = SubmittedBinding(submitted);
            string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(
                submitted.GetProperty("idempotencyToken").GetString()!)));
            return Task.FromResult(PartyMemorySourceInventoryTests.Page([new { caseId = "case", workflowInstanceId = "workflow",
                refreshProtocolVersion = Hexalith.Memories.Contracts.V1.SourceRefreshProtocol.Version, sourceRevisionNumber = 19,
                refreshIdentityDigest = digest, idempotencyTokenHash = hash, state = "Admitted" }], null));
        };
        handler.Recovery.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed() with { RuntimeStatus = "Running", MemoryUnitId = null, MemoryUnitStatus = null };
        using var http = PartyMemoryRecoveryTests.Http(handler);
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(original, context, Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        var persisted = (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem();
        persisted.SourceRevisionNumber.ShouldBe(19);
        persisted.RefreshIdentityDigest.ShouldNotBeNull();
        var redelivered = System.Text.Json.JsonSerializer.Deserialize<Hexalith.Parties.Contracts.Models.PartyIndexEntry>(System.Text.Json.JsonSerializer.Serialize(original))!;
        redelivered.SearchableContactChannels.ShouldBeEmpty();
        var changed = context with { EventType = "Reconciled", Timestamp = DateTimeOffset.UtcNow, CorrelationId = "redelivery" };
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(redelivered, changed, Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        handler.Recovery.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(redelivered, changed, Token)).ShouldNotBeNull().Indexed.ShouldBeTrue();
        handler.Submissions.ShouldHaveSingleItem();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().RefreshIdentityDigest.ShouldBe(persisted.RefreshIdentityDigest);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyAcknowledgedOutputWithoutRefreshProofCannotClaimIndexing(bool hasAuthority)
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRestHandler();
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var entry = PartyMemoryRecoveryTests.Entry(); var context = PartyMemoryRecoveryTests.Context();
        PartyMemoryUnit unit = PartyMemoryUnitMapper.Map(entry, context)!;
        byte[] revision = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { Entry = entry, unit.TenantId, unit.CaseId, unit.PartyId, SequenceNumber = context.SequenceNumber!.Value });
        string attempt = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(revision));
        var legacy = new PartyMemoryIngestionIntent(attempt, unit.CaseId, unit.SourceUri, "workflow", "canonical",
            ProducerAuthority: hasAuthority ? PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress) : null);
        await PartyMemoryRecoveryTests.Intents(state).RegisterAsync("tenant", "party", legacy, Token);
        handler.Recovery.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        for (int restart = 0; restart < 2; restart++)
        { (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(entry, context, Token)).ShouldNotBeNull().Indexed.ShouldBeFalse(); }
        handler.Submissions.ShouldBeEmpty(); handler.Recovery.Requests.ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().ShouldBe(legacy);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SupersededRefreshRedeliveryRegistersAndPublishesStableSuccessor()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRestHandler();
        handler.Recovery.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed() with
            { MemoryUnitId = "discarded", MemoryUnitStatus = Hexalith.Memories.Contracts.V1.MemoryUnitStatus.Failed, FailureSummary = "source-refresh-superseded" };
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var original = PartyMemoryRecoveryTests.Entry() with
        { SearchableContactChannels = [new Hexalith.Parties.Contracts.ValueObjects.ContactChannel
            { Id = "contact", Type = Hexalith.Parties.Contracts.ValueObjects.ContactChannelType.Email, Value = "synthetic@example.test" }] };
        var redelivered = System.Text.Json.JsonSerializer.Deserialize<Hexalith.Parties.Contracts.Models.PartyIndexEntry>(System.Text.Json.JsonSerializer.Serialize(original))!;
        var changedContext = PartyMemoryRecoveryTests.Context() with { EventType = "Reconciled", CorrelationId = "successor-redelivery" };
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(original, PartyMemoryRecoveryTests.Context(), Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        var intents = await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token);
        string successor = intents.Single(item => item.WorkflowInstanceId == "workflow").SuccessorAttemptId.ShouldNotBeNull();
        intents.Single(item => item.AttemptId == successor).SubmissionRejected.ShouldBeTrue();
        handler.SubmissionBody = "{\"instanceId\":\"successor-workflow\"}";
        handler.Recovery.Workflows["successor-workflow"] = PartyMemoryRecoveryHandler.Completed("successor-workflow", "successor-output") with
            { RuntimeStatus = "Running", MemoryUnitId = null, MemoryUnitStatus = null };
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(redelivered, changedContext, Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        handler.Submissions.Count.ShouldBe(2);
        SubmittedBinding(handler.Submissions[1]).ShouldNotBe(SubmittedBinding(handler.Submissions[0]));
        handler.Submissions[1].GetProperty("idempotencyToken").GetString().ShouldBe(successor);
        handler.Recovery.Workflows["successor-workflow"] = PartyMemoryRecoveryHandler.Completed("successor-workflow", "successor-output");
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(redelivered, changedContext, Token)).ShouldNotBeNull().Indexed.ShouldBeTrue();
        handler.Submissions.Count.ShouldBe(2);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("successor-output");
    }

    [Fact]
    public async Task MissingRevisionFailsBeforeReservationThenCanonicalRedeliverySucceeds()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRestHandler();
        handler.Recovery.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var context = PartyMemoryRecoveryTests.Context() with { SequenceNumber = null };
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(PartyMemoryRecoveryTests.Entry(), context, Token))
            .ShouldNotBeNull().Indexed.ShouldBeFalse();
        handler.Submissions.ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(PartyMemoryRecoveryTests.Entry(), context with { SequenceNumber = 8 }, Token))
            .ShouldNotBeNull().Indexed.ShouldBeTrue();
        handler.Submissions.ShouldHaveSingleItem().GetProperty("sourceRevisionNumber").GetInt64().ShouldBe(8);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LostAcknowledgementRequiresExactExplicitRefreshAdmissionAcrossSerializedRestart(bool supported)
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRestHandler { SubmissionBody = "{}" };
        bool completed = false;
        handler.Recovery.InventoryRequest = (request, _) =>
        {
            string digest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(
                handler.Submissions.Single().GetProperty("idempotencyToken").GetString()!)));
            PartyMemoryUnit unit = PartyMemoryUnitMapper.Map(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context())!;
            string binding = Hexalith.Memories.Contracts.V1.SourceRefreshProtocol.CreateIdentityDigest(unit.SourceUri, "text/plain", 0, unit.Metadata, Encoding.UTF8.GetBytes(unit.Content));
            return Task.FromResult(PartyMemorySourceInventoryTests.Page([new { caseId = "case", workflowInstanceId = "workflow",
                refreshProtocolVersion = supported ? Hexalith.Memories.Contracts.V1.SourceRefreshProtocol.Version : null,
                sourceRevisionNumber = supported ? (long?)0 : null, refreshIdentityDigest = supported ? binding : null,
                idempotencyTokenHash = digest, memoryUnitId = completed ? "canonical" : null,
                state = completed ? "Completed" : "Admitted", terminalVerified = completed }], null));
        };
        handler.Recovery.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed() with
            { RuntimeStatus = "Running", MemoryUnitId = null, MemoryUnitStatus = null };
        using var http = PartyMemoryRecoveryTests.Http(handler);
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBeFalse();
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBeFalse();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().WorkflowInstanceId.ShouldBe(supported ? "workflow" : null);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
        completed = true;
        handler.Recovery.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBe(supported);
        handler.Submissions.ShouldHaveSingleItem();
        if (!supported)
        {
            (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
            return;
        }
        state.SerializedValues.ShouldContain(value => value.Contains("WorkflowInstanceId", StringComparison.Ordinal)
            && value.Contains("workflow", StringComparison.Ordinal) && value.Contains("canonical", StringComparison.Ordinal));
    }
#endif

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
    [InlineData(409, "SOURCE_REOPEN_CONFLICT")]
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

#if HEXALITH_MEMORIES_SOURCE_REFRESH
    [Fact]
    public async Task OlderProducerIgnoringExplicitRefreshCannotPublishOrLoseItsUncertainAdmissionAcrossRestart()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRestHandler { AcknowledgeRefresh = false };
        handler.Recovery.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var http = PartyMemoryRecoveryTests.Http(handler);
        for (int restart = 0; restart < 2; restart++)
        {
            (await PartyMemoryRecoveryTests.Indexer(state, Client(http)).IndexAsync(PartyMemoryRecoveryTests.Entry(),
                PartyMemoryRecoveryTests.Context() with { SequenceNumber = 2 }, Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        }
        handler.Submissions.ShouldHaveSingleItem().GetProperty("refreshExistingSource").GetBoolean().ShouldBeTrue();
        var intent = (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem();
        intent.WorkflowInstanceId.ShouldBeNull(); intent.SubmissionRejected.ShouldBeFalse();
        handler.Recovery.Requests.ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
    }
#endif

#if HEXALITH_MEMORIES_SOURCE_REFRESH
    private static string SubmittedBinding(System.Text.Json.JsonElement submitted)
        => Hexalith.Memories.Contracts.V1.SourceRefreshProtocol.CreateIdentityDigest(submitted.GetProperty("sourceUri").GetString()!,
            submitted.GetProperty("contentType").GetString()!, submitted.GetProperty("sourceRevisionNumber").GetInt64(),
            System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Hexalith.Memories.Contracts.V1.MetadataField>>(
                submitted.GetProperty("metadata"), Hexalith.Memories.Contracts.V1.MemoriesJsonContext.Options)!,
            submitted.GetProperty("contentBytes").GetBytesFromBase64());
#endif

    internal static MemoriesClient Client(HttpClient http)
        => new(http, Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance);
}
