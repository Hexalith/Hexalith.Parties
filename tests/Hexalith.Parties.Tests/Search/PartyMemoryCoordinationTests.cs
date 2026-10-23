using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Extensions;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Search;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Durable canonical provenance across delayed publication and supported live rebuild recovery.</summary>
public sealed class PartyMemoryCoordinationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FirstSuccessfulSubmissionAfterRestartIncludesAllContactAndIdentifierContent()
    {
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var http = PartyMemoryRecoveryTests.Http(remote);
        var options = new PartyMemoryTestOptionsMonitor(new PartyMemorySearchOptions { Enabled = true, CaseId = "case" });
        var projectionOptions = Options.Create(new PartySdkReadModelOptions());
        PartyIndexSdkProjectionHandler Projection(PartyMemoryRecoveryClient client) => new(state, projectionOptions,
            new PartyMemoryIndexEntrySearchIndexer(PartyMemoryRecoveryTests.Indexer(state, client), PartyMemoryRecoveryTests.Cleanup(state, http),
                options, NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance, state, projectionOptions));
        ProjectionRequest replay = new("tenant", "party", "party",
        [
            Event(new PartyCreated { Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = "Synthetic", LastName = "Complete" } }, 1),
            Event(new ContactChannelAdded { ContactChannelId = "email", Type = ContactChannelType.Email, Value = "complete@example.invalid" }, 2),
            Event(new ContactChannelAdded { ContactChannelId = "phone", Type = ContactChannelType.Phone, Value = "+33123456789" }, 3),
            Event(new IdentifierAdded { IdentifierId = "tax", Type = IdentifierType.Other, Value = "synthetic-tax-id" }, 4),
        ]);
        var rejected = new PartyMemoryRecoveryClient(http)
        {
            Submit = _ => Task.FromException<string>(new MemoriesRemoteException(HttpStatusCode.Forbidden,
                new ErrorResponse("AUTHORIZATION_FAILED", "Rejected before scheduling.", "Repair credentials."))),
        };
        (await Projection(rejected).ProjectAsync(replay, "first-rejected", Token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        PartyIndexSdkReadModel persisted = (await state.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant"), Token)).Value.ShouldNotBeNull();
        persisted.Entries["party"].SearchableContactChannels.ShouldBeEmpty();
        persisted.Entries["party"].SearchableIdentifiers.ShouldBeEmpty();
        var recreated = new PartyMemoryRecoveryClient(http);
        (await Projection(recreated).ProjectAsync(replay with { Events = replay.Events[1..] }, "partial", Token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        recreated.Submissions.ShouldBe(0);
        (await Projection(recreated).ProjectAsync(replay, "complete-retry", Token)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        recreated.Submissions.ShouldBe(1);
        Encoding.UTF8.GetString(recreated.Content.ShouldNotBeNull()).ShouldContain("complete@example.invalid");
        Encoding.UTF8.GetString(recreated.Content!).ShouldContain("+33123456789");
        Encoding.UTF8.GetString(recreated.Content!).ShouldContain("synthetic-tax-id");
        recreated.Token.ShouldBe(rejected.Token);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
    }

    [Fact]
    public async Task ChangedRevisionAfterRestartRetainsDistinctCanonicalUnitsSharingOneSource()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow-one"] = PartyMemoryRecoveryHandler.Completed("workflow-one", "unit-one");
        handler.Workflows["workflow-two"] = PartyMemoryRecoveryHandler.Completed("workflow-two", "unit-two");
        handler.Units.UnionWith(["unit-one", "unit-two"]);
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var first = new PartyMemoryRecoveryClient(http) { Submit = _ => Task.FromResult("workflow-one") };
        var second = new PartyMemoryRecoveryClient(http) { Submit = _ => Task.FromResult("workflow-two") };
        (await PartyMemoryRecoveryTests.Indexer(state, first).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBeTrue();
        (await PartyMemoryRecoveryTests.Indexer(state, second).IndexAsync(PartyMemoryRecoveryTests.Entry() with { DisplayName = "changed revision", LastModifiedAt = DateTimeOffset.UnixEpoch.AddMinutes(1) }, PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBeTrue();

        first.Token.ShouldNotBe(second.Token);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).Count.ShouldBe(2);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token))
            .Select(item => item.MemoryUnitId).ShouldBe(["unit-one", "unit-two"]);
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        handler.Units.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task DefiniteSubmissionRejectionCanRetryAfterCredentialRepairAcrossRestart(int code)
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var rejected = new PartyMemoryRecoveryClient(http)
        {
            Submit = _ => Task.FromException<string>(new MemoriesRemoteException((HttpStatusCode)code,
                new ErrorResponse("AUTHORIZATION_FAILED", "Rejected before scheduling.", "Repair credentials."))),
        };
        (await PartyMemoryRecoveryTests.Indexer(state, rejected).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBeFalse();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().SubmissionRejected.ShouldBeTrue();
        var repaired = new PartyMemoryRecoveryClient(http);
        (await PartyMemoryRecoveryTests.Indexer(state, repaired).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBeTrue();
        rejected.Token.ShouldBe(repaired.Token);
        repaired.Submissions.ShouldBe(1);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
    }

    [Theory]
    [InlineData(404, "TENANT_NOT_FOUND")]
    [InlineData(409, "TENANT_PROVISIONING")]
    [InlineData(409, "TENANT_DELETING")]
    [InlineData(409, "TENANT_FAILED")]
    [InlineData(409, "TENANT_UNAVAILABLE")]
    [InlineData(400, "INVALID_INPUT")]
    [InlineData(429, "RATE_LIMIT_EXCEEDED")]
    public async Task VerifiedPreflightRejectionAllowsRepairedTenantSubmissionAfterRestart(int code, string errorCode)
    {
        var state = new PartyMemoryTestReadModelStore();
        using (var rejectedHttp = PartyMemoryRecoveryTests.Http(new ProbeHandler(code, "{}")))
        {
            var rejected = new PartyMemoryRecoveryClient(rejectedHttp)
            {
                Submit = _ => Task.FromException<string>(new MemoriesRemoteException((HttpStatusCode)code,
                    new ErrorResponse(errorCode, "Rejected before reservation and scheduling.", "Repair provisioning."))),
            };
            (await PartyMemoryRecoveryTests.Indexer(state, rejected).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
                .ShouldNotBeNull().Indexed.ShouldBeFalse();
            (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().SubmissionRejected.ShouldBeTrue();
        }

        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var repairedHttp = PartyMemoryRecoveryTests.Http(handler);
        var repaired = new PartyMemoryRecoveryClient(repairedHttp);
        (await PartyMemoryRecoveryTests.Indexer(state, repaired).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBeTrue();
        repaired.Submissions.ShouldBe(1);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token))
            .ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
    }

    [Theory]
    [InlineData(409, "UNKNOWN_CONFLICT")]
    [InlineData(404, "CASE_NOT_FOUND")]
    [InlineData(400, "UNKNOWN_ERROR")]
    [InlineData(404, "TENANT_PROVISIONING")]
    [InlineData(429, "UNKNOWN_RATE_LIMIT")]
    [InlineData(503, "RATE_LIMIT_EXCEEDED")]
    public async Task UnqualifiedSubmissionErrorPreservesUncertainAcceptanceWithoutResubmission(int code, string errorCode)
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var rejected = new PartyMemoryRecoveryClient(http)
        {
            Submit = _ => Task.FromException<string>(new MemoriesRemoteException((HttpStatusCode)code,
                new ErrorResponse(errorCode, "Unqualified response.", "Inspect the producer."))),
        };
        (await PartyMemoryRecoveryTests.Indexer(state, rejected).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBeFalse();
        var restarted = new PartyMemoryRecoveryClient(http);
        (await PartyMemoryRecoveryTests.Indexer(state, restarted).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBeFalse();
        restarted.Submissions.ShouldBe(0);
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeFalse();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().SubmissionRejected.ShouldBeFalse();
    }

    [Fact]
    public async Task StatusReadAuthorizationFailureNeverReenablesAcknowledgedSubmission()
    {
        var state = new PartyMemoryTestReadModelStore();
        using (var deniedHttp = PartyMemoryRecoveryTests.Http(new ProbeHandler(401, "{}")))
        {
            var accepted = new PartyMemoryRecoveryClient(deniedHttp);
            (await PartyMemoryRecoveryTests.Indexer(state, accepted).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
                .ShouldNotBeNull().Indexed.ShouldBeFalse();
            accepted.Submissions.ShouldBe(1);
        }

        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var repairedHttp = PartyMemoryRecoveryTests.Http(handler);
        var repaired = new PartyMemoryRecoveryClient(repairedHttp);
        (await PartyMemoryRecoveryTests.Indexer(state, repaired).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBeTrue();
        repaired.Submissions.ShouldBe(0);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token))
            .ShouldHaveSingleItem().SubmissionRejected.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateMappingWriterCannotReportIndexedOrLoseCanonicalProvenanceAfterCleanup(bool transient)
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        handler.Units.Add("canonical");
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new IndexingCancellationMappingStore(async (tenant, party, unit, source, caseId, token) =>
        {
            entered.TrySetResult();
            await resume.Task.WaitAsync(token).ConfigureAwait(false);
            await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync(tenant, party, unit, source, caseId, token).ConfigureAwait(false);
        });
        Task<PartyMemoryIndexingResult?> pending = PartyMemoryRecoveryTests.Indexer(state, new PartyMemoryRecoveryClient(http), delayed)
            .IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
            // Cleanup completes even if this writer's process dies and never releases anything.
            (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token, permanent: !transient)).Cleaned.ShouldBeTrue();
            (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldBeEmpty();
            state.SerializedValues.ShouldContain(json => json.Contains("CleanupReceipts", StringComparison.Ordinal) && json.Contains("canonical", StringComparison.Ordinal));
            handler.Workflows.Clear();
        }
        finally
        {
            resume.TrySetResult();
        }

        (await pending.ConfigureAwait(true)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token, permanent: !transient)).Cleaned.ShouldBeTrue();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task FreshProducerCanonicalIdentityAllowsNewGenerationAndPermanentErasureStaysFenced()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow-one"] = PartyMemoryRecoveryHandler.Completed("workflow-one", "unit-one");
        handler.Workflows["workflow-two"] = PartyMemoryRecoveryHandler.Completed("workflow-two", "unit-two");
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var client = new PartyMemoryRecoveryClient(http);
        List<string?> tokens = [];
        client.Submit = _ =>
        {
            tokens.Add(client.Token);
            string id = client.Submissions == 1 ? "unit-one" : "unit-two";
            handler.Units.Add(id);
            return Task.FromResult(client.Submissions == 1 ? "workflow-one" : "workflow-two");
        };
        var options = new PartyMemoryTestOptionsMonitor(new PartyMemorySearchOptions { Enabled = true, CaseId = "case" });
        var projectionOptions = Options.Create(new PartySdkReadModelOptions());
        PartyMemoryIndexEntrySearchIndexer Adapter() => new(PartyMemoryRecoveryTests.Indexer(state, client),
            PartyMemoryRecoveryTests.Cleanup(state, http), options, NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance, state, projectionOptions);
        PartyIndexSdkProjectionHandler Projection() => new(state, projectionOptions, Adapter());
        ProjectionRequest created = new("tenant", "party", "party", [Event(new PartyCreated
        {
            Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = "Synthetic", LastName = "Readd" },
        }, 1)]);
        (await Projection().ProjectAsync(created, "created", Token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);

        // A supported rebuild can temporarily remove the live target without an erasure checkpoint.
        string key = PartySdkReadModelAddresses.Index("tenant");
        var current = await state.GetAsync<PartyIndexSdkReadModel>("statestore", key, Token);
        (await state.TrySaveAsync("statestore", key, new PartyIndexSdkReadModel(), current.ETag!, Token)).ShouldBeTrue();
        (await Adapter().NotifyRemovedAsync("tenant", "party", Token)).ShouldBeTrue();
        handler.Units.ShouldBeEmpty();
        (await Projection().ProjectAsync(created, "live-readd", Token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        client.Submissions.ShouldBe(2);
        tokens[0].ShouldNotBe(tokens[1]);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("unit-two");

        ProjectionRequest erased = created with { Events = [Event(new PartyErased { PartyId = "party", TenantId = "tenant", ErasedAt = DateTimeOffset.UnixEpoch.AddMinutes(2) }, 2)] };
        (await Projection().ProjectAsync(erased, "erased", Token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        handler.Units.ShouldBeEmpty();
        (await Projection().ProjectAsync(created, "old-create-redelivery", Token)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        client.Submissions.ShouldBe(2);
        (await PartyMemoryRecoveryTests.Intents(state).TryReopenAsync("tenant", "party", Token)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransientFenceRequiresReadableLiveCanonicalEvidenceWithoutErasure(bool erased)
    {
        var state = new PartyMemoryTestReadModelStore();
        await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync("tenant", "party", Token, permanent: false);
        (await PartyMemoryRecoveryTests.Intents(state).TryReopenAsync("tenant", "party", Token)).ShouldBeFalse();
        string key = PartySdkReadModelAddresses.Index("tenant");
        var model = new PartyIndexSdkReadModel
        {
            Entries = new Dictionary<string, PartyIndexEntry> { ["party"] = PartyMemoryRecoveryTests.Entry() },
            ErasureSequenceNumbers = erased ? new Dictionary<string, long> { ["party"] = 3 } : new Dictionary<string, long>(),
        };
        (await state.TrySaveAsync("statestore", key, model, string.Empty, Token)).ShouldBeTrue();
        (await PartyMemoryRecoveryTests.Intents(state).TryReopenAsync("tenant", "party", Token)).ShouldBe(!erased);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    public async Task StatusReadProbeReportsAuthorizationAndUnqualifiedMissingRoutes(int code)
    {
        var state = new PartyMemoryTestReadModelStore();
        using var http = PartyMemoryRecoveryTests.Http(new ProbeHandler(code, "{}"));
        PartyMemoryCleanupProbeResult probe = await PartyMemoryRecoveryTests.Cleanup(state, http).ProbeIngestionStatusRouteAsync(Token);
        probe.Reachable.ShouldBeFalse();
        probe.StatusCode.ShouldBe(code);
        probe.Reason.ShouldNotBeNull().ShouldContain(code is 401 or 403 ? "authorization rejected" : "unqualified");
    }

    [Fact]
    public async Task CanonicalSerializedRedeliveryReconcilesTransientSearchContentWithoutSecondSubmission()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed() with { RuntimeStatus = "Running", MemoryUnitId = null, MemoryUnitStatus = null };
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var first = new PartyMemoryRecoveryClient(http);
        var options = new PartyMemoryTestOptionsMonitor(new PartyMemorySearchOptions { Enabled = true, CaseId = "case" });
        var projectionOptions = Options.Create(new PartySdkReadModelOptions());
        PartyIndexSdkProjectionHandler Projection(PartyMemoryRecoveryClient client) => new(state, projectionOptions,
            new PartyMemoryIndexEntrySearchIndexer(PartyMemoryRecoveryTests.Indexer(state, client),
                PartyMemoryRecoveryTests.Cleanup(state, http), options, NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance, state, projectionOptions));
        ProjectionRequest request = new("tenant", "party", "party", [
            Event(new PartyCreated { Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = "Synthetic", LastName = "Replay" } }, 1),
            Event(new ContactChannelAdded { ContactChannelId = "contact", Type = ContactChannelType.Email, Value = "synthetic@example.invalid" }, 2),
            Event(new IdentifierAdded { IdentifierId = "identifier", Type = IdentifierType.Other, Value = "synthetic-code" }, 3),
        ]);
        (await Projection(first).ProjectAsync(request, "first", Token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        Encoding.UTF8.GetString(first.Content.ShouldNotBeNull()).ShouldContain("synthetic@example.invalid");
        Encoding.UTF8.GetString(first.Content.ShouldNotBeNull()).ShouldContain("synthetic-code");
        PartyIndexSdkReadModel persisted = (await state.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant"), Token)).Value.ShouldNotBeNull();
        persisted.Entries["party"].SearchableContactChannels.ShouldBeEmpty();
        persisted.Entries["party"].SearchableIdentifiers.ShouldBeEmpty();

        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        var restarted = new PartyMemoryRecoveryClient(http);
        (await Projection(restarted).ProjectAsync(request, "redelivery", Token)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        first.Submissions.ShouldBe(1);
        restarted.Submissions.ShouldBe(0);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
    }

    [Fact]
    public async Task SelectedSourceDedupReturningDeletedCanonicalKeepsLiveReaddRetryableAcrossRestart()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["first-workflow"] = PartyMemoryRecoveryHandler.Completed("first-workflow", "old-canonical");
        handler.Workflows["dedup-workflow"] = PartyMemoryRecoveryHandler.Completed("dedup-workflow", "old-canonical");
        handler.Units.Add("old-canonical");
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var client = new PartyMemoryRecoveryClient(http) { Submit = _ => Task.FromResult("first-workflow") };
        var options = new PartyMemoryTestOptionsMonitor(new PartyMemorySearchOptions { Enabled = true, CaseId = "case" });
        var projectionOptions = Options.Create(new PartySdkReadModelOptions());
        PartyMemoryIndexEntrySearchIndexer Adapter(PartyMemoryRecoveryClient producer) => new(PartyMemoryRecoveryTests.Indexer(state, producer),
            PartyMemoryRecoveryTests.Cleanup(state, http), options, NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance, state, projectionOptions);
        PartyIndexSdkProjectionHandler Projection(PartyMemoryRecoveryClient producer) => new(state, projectionOptions, Adapter(producer));
        ProjectionRequest created = new("tenant", "party", "party", [Event(new PartyCreated
        {
            Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = "Synthetic", LastName = "Dedup" },
        }, 1)]);
        (await Projection(client).ProjectAsync(created, "first", Token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string key = PartySdkReadModelAddresses.Index("tenant");
        var current = await state.GetAsync<PartyIndexSdkReadModel>("statestore", key, Token);
        (await state.TrySaveAsync("statestore", key, new PartyIndexSdkReadModel(), current.ETag!, Token)).ShouldBeTrue();
        (await Adapter(client).NotifyRemovedAsync("tenant", "party", Token)).ShouldBeTrue();
        handler.Units.ShouldBeEmpty();

        var dedup = new PartyMemoryRecoveryClient(http) { Submit = _ => Task.FromResult("dedup-workflow") };
        (await Projection(dedup).ProjectAsync(created, "readd", Token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        dedup.Submissions.ShouldBe(1);
        dedup.Token.ShouldNotBe(client.Token);
        var restarted = new PartyMemoryRecoveryClient(http);
        (await Projection(restarted).ProjectAsync(created, "redelivery", Token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        restarted.Submissions.ShouldBe(0);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("old-canonical");
    }

    [Theory]
    [InlineData(401, false)]
    [InlineData(401, true)]
    [InlineData(404, false)]
    [InlineData(404, true)]
    public async Task SettledReceiptsNeedNoRemoteWorkAndResolveLateMappingsAfterAuthorizationOrCaseLoss(int code, bool lateMapping)
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using (var http = PartyMemoryRecoveryTests.Http(handler))
        {
            (await PartyMemoryRecoveryTests.Indexer(state, new PartyMemoryRecoveryClient(http)).IndexAsync(
                PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token)).ShouldNotBeNull().Indexed.ShouldBeTrue();
            (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        }

        if (lateMapping)
        {
            await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "workflow", "urn:late", "case", Token);
            await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "canonical", "urn:late", "case", Token);
        }

        var unavailable = new ProbeHandler(code, "{\"code\":\"CASE_NOT_FOUND\"}");
        using var lostHttp = PartyMemoryRecoveryTests.Http(unavailable);
        (await PartyMemoryRecoveryTests.Cleanup(state, lostHttp).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        unavailable.Requests.ShouldBe(0);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync("tenant", "party", Token)).ShouldHaveSingleItem().CleanupSettled.ShouldBeTrue();
    }

    [Fact]
    public async Task ReceiptBearingCapacitySurvivesTransientReopenAndBlocksSubmissionWithoutEviction()
    {
        var state = new PartyMemoryTestReadModelStore();
        List<PartyMemoryIngestionIntent> receipts = [.. Enumerable.Range(0, PartyMemoryIngestionIntentStore.Capacity)
            .Select(index => new PartyMemoryIngestionIntent(index.ToString(System.Globalization.CultureInfo.InvariantCulture), "case", "urn:source",
                MemoryUnitId: "canonical-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), CleanupSettled: true))];
        string key = PartyMemoryUnitMappingStore.BuildKey("tenant", "party").Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal);
        (await state.TrySaveAsync("statestore", key, new PartyMemoryIngestionInventory(true, [], CleanupReceipts: receipts), string.Empty, Token)).ShouldBeTrue();
        (await state.TrySaveAsync("statestore", PartySdkReadModelAddresses.Index("tenant"), new PartyIndexSdkReadModel
        {
            Entries = new Dictionary<string, PartyIndexEntry> { ["party"] = PartyMemoryRecoveryTests.Entry() },
        }, string.Empty, Token)).ShouldBeTrue();
        (await PartyMemoryRecoveryTests.Intents(state).TryReopenAsync("tenant", "party", Token)).ShouldBeTrue();
        using var http = PartyMemoryRecoveryTests.Http(new PartyMemoryRecoveryHandler());
        var client = new PartyMemoryRecoveryClient(http);
        (await PartyMemoryRecoveryTests.Indexer(state, client).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token))
            .ShouldNotBeNull().Indexed.ShouldBeFalse();
        client.Submissions.ShouldBe(0);
        PartyMemoryIngestionInventory retained = (await state.GetAsync<PartyMemoryIngestionInventory>("statestore", key, Token)).Value.ShouldNotBeNull();
        retained.Generation.ShouldBe(1);
        retained.Intents.ShouldBeEmpty();
        retained.CleanupReceipts.ShouldNotBeNull().ShouldBe(receipts);
    }

    [Fact]
    public async Task LegacyCanonicalProvenanceSurvivesSubtractionFailureAndWorkflowExpiry()
    {
        var state = new PartyMemoryTestReadModelStore();
        var mappings = PartyMemoryRecoveryTests.Mappings(state);
        await mappings.RecordMappingAsync("tenant", "party", "legacy-workflow", "urn:legacy", "case", Token);
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["legacy-workflow"] = PartyMemoryRecoveryHandler.Completed("legacy-workflow", "legacy-canonical");
        handler.Units.Add("legacy-canonical");
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var failed = new InterceptedPartyMemoryMappingStore(mappings, (_, _, _, _) => Task.FromException<int>(new InvalidOperationException("subtraction-failed")));
        await Should.ThrowAsync<InvalidOperationException>(() => new PartyMemoryCleanupService(http, failed,
            NullLogger<PartyMemoryCleanupService>.Instance, PartyMemoryRecoveryTests.Intents(state)).DeleteByPartyAsync("tenant", "case", "party", Token));
        handler.Units.ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldBeEmpty();
        PartyMemoryIngestionIntent settled = (await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync("tenant", "party", Token)).ShouldHaveSingleItem();
        settled.ShouldBe(new PartyMemoryIngestionIntent(settled.AttemptId, "case", "urn:legacy",
            "legacy-workflow", "legacy-canonical", CleanupSettled: true));
        (await mappings.GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("legacy-workflow");
        handler.Workflows.Clear();
        handler.Requests.Clear();

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        handler.Requests.ShouldAllBe(request => !request.Path.Contains("/ingest/", StringComparison.Ordinal));
        (await mappings.GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync("tenant", "party", Token)).ShouldHaveSingleItem().CleanupSettled.ShouldBeTrue();
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("conflict")]
    [InlineData("capacity")]
    public async Task LegacyProvenancePersistenceMustSucceedBeforeDeletion(string mode)
    {
        var state = new PartyMemoryTestReadModelStore();
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "legacy-workflow", "urn:legacy", "case", Token);
        if (mode == "capacity")
        {
            List<PartyMemoryIngestionIntent> receipts = [.. Enumerable.Range(0, PartyMemoryIngestionIntentStore.Capacity)
                .Select(index => new PartyMemoryIngestionIntent(index.ToString(System.Globalization.CultureInfo.InvariantCulture), "other-case", "urn:source", CleanupSettled: true))];
            string key = PartyMemoryUnitMappingStore.BuildKey("tenant", "party").Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal);
            (await state.TrySaveAsync("statestore", key, new PartyMemoryIngestionInventory(false, [], CleanupReceipts: receipts), string.Empty, Token)).ShouldBeTrue();
        }

        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["legacy-workflow"] = PartyMemoryRecoveryHandler.Completed("legacy-workflow", "legacy-canonical");
        handler.Units.Add("legacy-canonical");
        handler.BeforeRequest = (_, _) =>
        {
            state.SaveFailure = mode == "failure" ? new InvalidOperationException("unavailable") : null;
            state.RejectWrites = mode == "conflict";
            return Task.CompletedTask;
        };
        using var http = PartyMemoryRecoveryTests.Http(handler);
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeFalse();
        handler.Units.ShouldContain("legacy-canonical");
        handler.Requests.ShouldAllBe(request => request.Method != HttpMethod.Delete);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("legacy-workflow");
    }

    [Fact]
    public async Task RealAdaptersKeepConfiguredMappingAndProjectionStoresDistinct()
    {
        var state = new PartyMemoryTestReadModelStore();
        var mappingOptions = Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = "mapping-store" });
        var projectionOptions = Options.Create(new PartySdkReadModelOptions { ReadModelStateStoreName = "projection-store" });
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var intents = new PartyMemoryIngestionIntentStore(state, mappingOptions, projectionOptions);
        var mappings = new PartyMemoryUnitMappingStore(state, mappingOptions, NullLogger<PartyMemoryUnitMappingStore>.Instance);
        var searchOptions = new PartyMemoryTestOptionsMonitor(new PartyMemorySearchOptions { Enabled = true, CaseId = "case" });
        var indexer = new PartyMemoryIndexingService(new PartyMemoryRecoveryClient(http), mappings, searchOptions, NullLogger<PartyMemoryIndexingService>.Instance, intents);
        var cleanup = new PartyMemoryCleanupService(http, mappings, NullLogger<PartyMemoryCleanupService>.Instance, intents);
        var adapter = new PartyMemoryIndexEntrySearchIndexer(indexer, cleanup, searchOptions,
            NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance, state, projectionOptions);
        string key = PartySdkReadModelAddresses.Index("tenant");
        var live = new PartyIndexSdkReadModel { Entries = new Dictionary<string, PartyIndexEntry> { ["party"] = PartyMemoryRecoveryTests.Entry() } };
        (await state.TrySaveAsync("statestore", key, live, string.Empty, Token)).ShouldBeTrue();
        await intents.BeginCleanupAsync("tenant", "party", Token, permanent: false);
        (await intents.TryReopenAsync("tenant", "party", Token)).ShouldBeFalse();
        (await state.TrySaveAsync("projection-store", key, live, string.Empty, Token)).ShouldBeTrue();
        (await adapter.NotifyIndexedAsync("tenant", PartyMemoryRecoveryTests.Entry(), "PartyCreated", DateTimeOffset.UnixEpoch, Token)).ShouldBeTrue();
        (await mappings.GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
        string mappingKey = PartyMemoryUnitMappingStore.BuildKey("tenant", "party");
        (await state.GetAsync<List<PartyMemoryUnitMappingEntry>>("statestore", mappingKey, Token)).Value.ShouldBeNull();
        (await state.GetAsync<List<PartyMemoryUnitMappingEntry>>("projection-store", mappingKey, Token)).Value.ShouldBeNull();
        var current = await state.GetAsync<PartyIndexSdkReadModel>("projection-store", key, Token);
        (await state.TrySaveAsync("projection-store", key, new PartyIndexSdkReadModel(), current.ETag!, Token)).ShouldBeTrue();
        (await adapter.NotifyRemovedAsync("tenant", "party", Token)).ShouldBeTrue();
        (await mappings.GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
        (await intents.BeginCleanupAsync("tenant", "party", Token, permanent: false)).ShouldHaveSingleItem().CleanupSettled.ShouldBeTrue();
    }

    [Fact]
    public async Task NewLegacyWorkflowSharingSettledCanonicalRetainsItsOwnAssociationBeforeSubtraction()
    {
        var state = new PartyMemoryTestReadModelStore();
        var intents = PartyMemoryRecoveryTests.Intents(state);
        var old = new PartyMemoryIngestionIntent("old", "case", "urn:old", "old-workflow", "canonical");
        await intents.RegisterAsync("tenant", "party", old, Token);
        await intents.BeginCleanupAsync("tenant", "party", Token);
        (await intents.RemoveAsync("tenant", "party", [old], Token)).ShouldBe(0);
        var mappings = PartyMemoryRecoveryTests.Mappings(state);
        await mappings.RecordMappingAsync("tenant", "party", "new-legacy-workflow", "urn:legacy", "case", Token);
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["new-legacy-workflow"] = PartyMemoryRecoveryHandler.Completed("new-legacy-workflow", "canonical");
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var failing = new InterceptedPartyMemoryMappingStore(mappings, (_, _, _, _) => Task.FromException<int>(new InvalidOperationException("subtraction-failed")));
        await Should.ThrowAsync<InvalidOperationException>(() => new PartyMemoryCleanupService(http, failing,
            NullLogger<PartyMemoryCleanupService>.Instance, intents).DeleteByPartyAsync("tenant", "case", "party", Token));
        IReadOnlyList<PartyMemoryIngestionIntent> receipts = await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync("tenant", "party", Token);
        receipts.Count.ShouldBe(2);
        receipts.ShouldContain(item => item.CleanupSettled && item.WorkflowInstanceId == "new-legacy-workflow" && item.MemoryUnitId == "canonical");
        handler.Requests.ShouldAllBe(request => request.Method != HttpMethod.Delete);
        handler.Workflows.Clear();
        handler.Requests.Clear();

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        handler.Requests.ShouldBeEmpty();
        (await mappings.GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ExistingInterfaceImplementationRetainsItsBehaviorWithCanonicalSequence()
    {
        IPartyIndexSearchIndexer legacy = new NoOpPartyIndexSearchIndexer();
        (await legacy.NotifyIndexedAsync("tenant", PartyMemoryRecoveryTests.Entry(), "PartyCreated", DateTimeOffset.UnixEpoch, 7, Token)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameTypeAndTimestampCanonicalApplicationsUseDistinctSequencesAndStableRedelivery(bool identifiers)
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow-one"] = PartyMemoryRecoveryHandler.Completed("workflow-one", "unit-one");
        handler.Workflows["workflow-two"] = PartyMemoryRecoveryHandler.Completed("workflow-two", "unit-two");
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var options = new PartyMemoryTestOptionsMonitor(new PartyMemorySearchOptions { Enabled = true, CaseId = "case" });
        var projectionOptions = Options.Create(new PartySdkReadModelOptions());
        PartyIndexSdkProjectionHandler Projection(PartyMemoryRecoveryClient client) => new(state, projectionOptions,
            new PartyMemoryIndexEntrySearchIndexer(PartyMemoryRecoveryTests.Indexer(state, client),
                PartyMemoryRecoveryTests.Cleanup(state, http), options, NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance, state, projectionOptions));
        object Change(string id) => identifiers
            ? new IdentifierAdded { IdentifierId = id, Type = IdentifierType.Other, Value = "synthetic-" + id }
            : new ContactChannelAdded { ContactChannelId = id, Type = ContactChannelType.Email, Value = id + "@example.invalid" };
        DateTimeOffset timestamp = DateTimeOffset.UnixEpoch.AddMinutes(10);
        ProjectionRequest firstRequest = new("tenant", "party", "party", [
            Event(new PartyCreated { Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = "Synthetic", LastName = "Sequence" } }, 1),
            Event(Change("one"), 2, timestamp),
        ]);
        var first = new PartyMemoryRecoveryClient(http) { Submit = _ => Task.FromResult("workflow-one") };
        (await Projection(first).ProjectAsync(firstRequest, "first", Token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string key = PartySdkReadModelAddresses.Index("tenant");
        PartyIndexSdkReadModel firstModel = (await state.GetAsync<PartyIndexSdkReadModel>("statestore", key, Token)).Value.ShouldNotBeNull();
        firstModel.LastIndexedEvents["party"].SequenceNumber.ShouldBe(2);
        string serializedEntry = JsonSerializer.Serialize(firstModel.Entries["party"], PartiesJsonOptions.Default);
        var redelivery = new PartyMemoryRecoveryClient(http);
        (await Projection(redelivery).ProjectAsync(firstRequest, "first-restart", Token)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        redelivery.Submissions.ShouldBe(0);

        ProjectionRequest secondRequest = firstRequest with { Events = [.. firstRequest.Events, Event(Change("two"), 3, timestamp)] };
        var second = new PartyMemoryRecoveryClient(http) { Submit = _ => Task.FromResult("workflow-two") };
        (await Projection(second).ProjectAsync(secondRequest, "second", Token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        first.Submissions.ShouldBe(1);
        second.Submissions.ShouldBe(1);
        second.Token.ShouldNotBe(first.Token);
        PartyIndexSdkReadModel secondModel = (await state.GetAsync<PartyIndexSdkReadModel>("statestore", key, Token)).Value.ShouldNotBeNull();
        secondModel.LastIndexedEvents["party"].SequenceNumber.ShouldBe(3);
        secondModel.LastIndexedEvents["party"].EventTypeName.ShouldBe(firstModel.LastIndexedEvents["party"].EventTypeName);
        secondModel.LastIndexedEvents["party"].Timestamp.ShouldBe(firstModel.LastIndexedEvents["party"].Timestamp);
        JsonSerializer.Serialize(secondModel.Entries["party"], PartiesJsonOptions.Default).ShouldBe(serializedEntry);
        var restarted = new PartyMemoryRecoveryClient(http);
        handler.Workflows.Clear();
        (await Projection(restarted).ProjectAsync(secondRequest, "second-restart", Token)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        restarted.Submissions.ShouldBe(0);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).Count.ShouldBe(2);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).Select(item => item.MemoryUnitId).ShouldBe(["unit-one", "unit-two"]);
    }

    [Fact]
    public async Task ProductionRegistrationPreservesTransientGenerationAndPermanentErasureFencing()
    {
        var state = new PartyMemoryTestReadModelStore();
        using var handler = new PartyMemoryRestHandler();
        handler.Recovery.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        handler.Recovery.Workflows["new-workflow"] = PartyMemoryRecoveryHandler.Completed("new-workflow", "new-canonical");
        handler.Recovery.Units.Add("canonical");
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Issuer"] = "hexalith-test",
            ["Authentication:JwtBearer:Audience"] = "hexalith-parties",
            ["Authentication:JwtBearer:SigningKey"] = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!",
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            ["Tenants:PubSubName"] = "pubsub",
            ["Tenants:TopicName"] = "system.tenants.events",
            ["Parties:MemoriesSearch:Enabled"] = "true",
            ["Parties:MemoriesSearch:Endpoint"] = "http://127.0.0.1:1/gateway/",
            ["Parties:MemoriesSearch:TenantId"] = "tenant",
            ["Parties:MemoriesSearch:CaseId"] = "case",
            ["Parties:MemoriesSearch:RequireApiToken"] = "false",
            ["Parties:MemoriesSearch:MappingStore:StateStoreName"] = "mapping-store",
            ["EventStore:Projections:ReadModelStateStoreName"] = "projection-store",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddParties(configuration);
        services.RemoveAll<IReadModelStore>();
        services.AddSingleton<IReadModelStore>(state);
        services.ConfigureAll<HttpClientFactoryOptions>(clientOptions => clientOptions.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler));
        using ServiceProvider provider = services.BuildServiceProvider();
        IPartyIndexSearchIndexer indexer = provider.GetRequiredService<IPartyIndexSearchIndexer>();
        indexer.ShouldBeOfType<PartyMemoryIndexEntrySearchIndexer>();
        var projectionOptions = provider.GetRequiredService<IOptions<PartySdkReadModelOptions>>();
        PartyIndexSdkProjectionHandler Projection() => new(state, projectionOptions, indexer);
        var intents = provider.GetRequiredService<IPartyMemoryIngestionIntentStore>();
        var mappings = provider.GetRequiredService<IPartyMemoryUnitMappingStore>();
        ProjectionRequest created = new("tenant", "party", "party", [Event(new PartyCreated
        {
            Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = "Synthetic", LastName = "Registered" },
        }, 1)]);
        (await Projection().ProjectAsync(created, "created", Token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string key = PartySdkReadModelAddresses.Index("tenant");
        var current = await state.GetAsync<PartyIndexSdkReadModel>("projection-store", key, Token);
        (await state.TrySaveAsync("projection-store", key, new PartyIndexSdkReadModel(), current.ETag!, Token)).ShouldBeTrue();
        (await indexer.NotifyRemovedAsync("tenant", "party", Token)).ShouldBeTrue();
        handler.Recovery.Units.ShouldBeEmpty();
        string inventoryKey = PartyMemoryUnitMappingStore.BuildKey("tenant", "party").Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal);
        PartyMemoryIngestionInventory removed = (await state.GetAsync<PartyMemoryIngestionInventory>("mapping-store", inventoryKey, Token)).Value.ShouldNotBeNull();
        removed.CleanupRequested.ShouldBeTrue();
        removed.PermanentCleanup.ShouldBeFalse();
        removed.CleanupReceipts.ShouldNotBeNull().ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");

        handler.SubmissionBody = "{\"instanceId\":\"new-workflow\"}";
        handler.Recovery.Units.Add("new-canonical");
        (await Projection().ProjectAsync(created, "readd", Token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        handler.Submissions.Count.ShouldBe(2);
        handler.Submissions[1].GetProperty("idempotencyToken").GetString().ShouldNotBe(handler.Submissions[0].GetProperty("idempotencyToken").GetString());
        (await mappings.GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("new-canonical");
        PartyMemoryIngestionInventory live = (await state.GetAsync<PartyMemoryIngestionInventory>("mapping-store", inventoryKey, Token)).Value.ShouldNotBeNull();
        live.Generation.ShouldBe(1);
        live.CleanupRequested.ShouldBeFalse();
        live.PermanentCleanup.ShouldBeFalse();
        live.Intents.ShouldHaveSingleItem().Generation.ShouldBe(1);
        live.CleanupReceipts.ShouldNotBeNull().ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");

        ProjectionRequest erased = created with { Events = [Event(new PartyErased { PartyId = "party", TenantId = "tenant", ErasedAt = DateTimeOffset.UnixEpoch.AddMinutes(2) }, 2)] };
        (await Projection().ProjectAsync(erased, "erased", Token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        handler.Recovery.Units.ShouldBeEmpty();
        (await mappings.GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
        (await intents.GetAsync("tenant", "party", Token)).ShouldBeEmpty();
        (await intents.TryReopenAsync("tenant", "party", Token)).ShouldBeFalse();
        (await Projection().ProjectAsync(created, "old-redelivery", Token)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        handler.Submissions.Count.ShouldBe(2);
        (await state.GetAsync<PartyMemoryIngestionInventory>("mapping-store", inventoryKey, Token)).Value.ShouldNotBeNull().PermanentCleanup.ShouldBeTrue();
    }

    [Theory]
    [InlineData(401, "AUTHORIZATION_FAILED")]
    [InlineData(403, "AUTHORIZATION_FAILED")]
    [InlineData(429, "RATE_LIMIT_EXCEEDED")]
    public async Task PersistedDefiniteRejectionCleansBeforeRepairAndRetainsPermanentFence(int code, string errorCode)
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRestHandler
        {
            SubmissionStatus = (HttpStatusCode)code,
            SubmissionBody = "{\"code\":\"" + errorCode + "\",\"message\":\"Rejected before scheduling.\"}",
        };
        using var http = PartyMemoryRecoveryTests.Http(handler);
        (await PartyMemoryRecoveryTests.Indexer(state, PartyMemoryRestClientRecoveryTests.Client(http)).IndexAsync(
            PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().SubmissionRejected.ShouldBeTrue();
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
        var receipt = (await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync("tenant", "party", Token)).ShouldHaveSingleItem();
        receipt.CleanupSettled.ShouldBeTrue();
        receipt.SubmissionRejected.ShouldBeTrue();
        receipt.WorkflowInstanceId.ShouldBeNull();
        receipt.MemoryUnitId.ShouldBeNull();
        (await state.TrySaveAsync("statestore", PartySdkReadModelAddresses.Index("tenant"), new PartyIndexSdkReadModel
        {
            Entries = new Dictionary<string, PartyIndexEntry> { ["party"] = PartyMemoryRecoveryTests.Entry() },
        }, string.Empty, Token)).ShouldBeTrue();
        (await PartyMemoryRecoveryTests.Intents(state).TryReopenAsync("tenant", "party", Token)).ShouldBeFalse();
        (await PartyMemoryRecoveryTests.Indexer(state, PartyMemoryRestClientRecoveryTests.Client(http)).IndexAsync(
            PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        handler.Submissions.ShouldHaveSingleItem();
        handler.Recovery.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(200, "<html>proxy</html>")]
    [InlineData(200, "")]
    [InlineData(204, "")]
    [InlineData(200, "{}")]
    [InlineData(200, "{\"ok\":true}")]
    [InlineData(200, "null")]
    [InlineData(200, "{\"instanceId\":\"other\",\"tenantId\":\"tenant\",\"caseId\":\"case\",\"runtimeStatus\":\"Running\",\"createdAt\":\"1970-01-01T00:00:00Z\",\"lastUpdatedAt\":\"1970-01-01T00:00:00Z\"}")]
    [InlineData(200, "{\"instanceId\":\"{instanceId}\",\"runtimeStatus\":\"Running\"}")]
    [InlineData(200, "{\"instanceId\":\"{instanceId}\",\"tenantId\":\"tenant\",\"caseId\":\"case\",\"runtimeStatus\":\"Unknown\",\"createdAt\":\"1970-01-01T00:00:00Z\",\"lastUpdatedAt\":\"1970-01-01T00:00:00Z\"}")]
    public async Task StatusProbeRejectsSuccessfulBodiesWithoutAMatchingValidStatusEnvelope(int code, string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        using var http = PartyMemoryRecoveryTests.Http(new ProbeHandler(code, body));
        (await PartyMemoryRecoveryTests.Cleanup(new PartyMemoryTestReadModelStore(), http).ProbeIngestionStatusRouteAsync(Token)).Reachable.ShouldBeFalse();
    }

    [Theory]
    [InlineData(404, "{\"code\":\"INGESTION_STATUS_NOT_FOUND\"}")]
    [InlineData(200, "{\"instanceId\":\"{instanceId}\",\"tenantId\":\"tenant\",\"caseId\":\"case\",\"runtimeStatus\":\"Running\",\"createdAt\":\"1970-01-01T00:00:00Z\",\"lastUpdatedAt\":\"1970-01-01T00:00:00Z\"}")]
    public async Task StatusProbeAcceptsOnlyQualifiedMissingStatusOrMatchingStatusEnvelope(int code, string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        using var http = PartyMemoryRecoveryTests.Http(new ProbeHandler(code, body));
        PartyMemoryCleanupProbeResult result = await PartyMemoryRecoveryTests.Cleanup(new PartyMemoryTestReadModelStore(), http).ProbeIngestionStatusRouteAsync(Token);
        result.Reachable.ShouldBeTrue();
        result.StatusCode.ShouldBe(code);
    }

    [Fact]
    public async Task PendingCanonicalSequenceReconcilesAcrossRebuildAndReconciledDeliveryMarkers()
    {
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed() with
        {
            RuntimeStatus = "Running", MemoryUnitId = null, MemoryUnitStatus = null,
        };
        using var http = PartyMemoryRecoveryTests.Http(remote);
        var options = new PartyMemoryTestOptionsMonitor(new PartyMemorySearchOptions { Enabled = true, CaseId = "case" });
        var projectionOptions = Options.Create(new PartySdkReadModelOptions());
        PartyMemoryIndexEntrySearchIndexer Adapter(PartyMemoryRecoveryClient client) => new(PartyMemoryRecoveryTests.Indexer(state, client),
            PartyMemoryRecoveryTests.Cleanup(state, http), options, NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance, state, projectionOptions);
        PartyIndexSdkProjectionHandler Projection(PartyMemoryRecoveryClient client) => new(state, projectionOptions, Adapter(client));
        ProjectionRequest request = new("tenant", "party", "party", [Event(new PartyCreated
        {
            Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = "Synthetic", LastName = "Markers" },
        }, 1, new DateTimeOffset(2026, 10, 23, 14, 0, 0, TimeSpan.FromHours(2)))]);
        var initial = new PartyMemoryRecoveryClient(http);
        (await Projection(initial).ProjectAsync(request, "incremental", Token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        string attempt = (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().AttemptId;
        var restarted = new PartyMemoryRecoveryClient(http);
        var rebuild = Projection(restarted);
        var identity = new DomainSharedProjectionRebuildIdentity("tenant", "party", PartyProjectionNames.Index, "rebuild", "catalog");
        var candidate = await rebuild.CreateEmptyCandidateAsync(identity, Token);
        candidate = await rebuild.AccumulateAsync(identity, candidate, request, Token);
        var plan = await rebuild.FinalizeAsync(identity, candidate, Token);
        // The canonical target is already persisted by the incremental application.
        (await rebuild.CompleteRebuildAsync(identity, candidate, plan.CompletionState, Token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        PartyIndexEntry persisted = (await state.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant"), Token)).Value.ShouldNotBeNull().Entries["party"];
        (await Adapter(restarted).NotifyIndexedAsync("tenant", persisted, "PartyProjectionReconciled", DateTimeOffset.UnixEpoch, 1, Token)).ShouldBeFalse();
        restarted.Submissions.ShouldBe(0);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().AttemptId.ShouldBe(attempt);

        remote.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        var terminal = new PartyMemoryRecoveryClient(http);
        (await Projection(terminal).ProjectAsync(request, "terminal-replay", Token)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        initial.Submissions.ShouldBe(1);
        terminal.Submissions.ShouldBe(0);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().AttemptId.ShouldBe(attempt);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
    }

    [Fact]
    public async Task DelayedTransientRemovalSkipsLiveTargetWithoutFencingOrDeletingItsProducer()
    {
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        remote.Workflows["next-workflow"] = PartyMemoryRecoveryHandler.Completed("next-workflow", "next-canonical");
        remote.Units.UnionWith(["canonical", "next-canonical"]);
        using var http = PartyMemoryRecoveryTests.Http(remote);
        var client = new PartyMemoryRecoveryClient(http);
        var options = new PartyMemoryTestOptionsMonitor(new PartyMemorySearchOptions { Enabled = true, CaseId = "case" });
        var projectionOptions = Options.Create(new PartySdkReadModelOptions());
        var adapter = new PartyMemoryIndexEntrySearchIndexer(PartyMemoryRecoveryTests.Indexer(state, client),
            PartyMemoryRecoveryTests.Cleanup(state, http), options, NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance, state, projectionOptions);
        var projection = new PartyIndexSdkProjectionHandler(state, projectionOptions, adapter);
        ProjectionRequest request = new("tenant", "party", "party", [Event(new PartyCreated
        {
            Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = "Synthetic", LastName = "StaleRemoval" },
        }, 1)]);
        (await projection.ProjectAsync(request, "live", Token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        remote.Requests.Clear();
        (await adapter.NotifyRemovedAsync("tenant", "party", Token)).ShouldBeFalse();
        remote.Requests.ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
        string inventoryKey = PartyMemoryUnitMappingStore.BuildKey("tenant", "party").Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal);
        var inventory = (await state.GetAsync<PartyMemoryIngestionInventory>("statestore", inventoryKey, Token)).Value.ShouldNotBeNull();
        inventory.CleanupRequested.ShouldBeFalse();
        inventory.PermanentCleanup.ShouldBeFalse();
        inventory.Generation.ShouldBe(0);
        client.Submit = _ => Task.FromResult("next-workflow");
        (await projection.ProjectAsync(request with { Events = [.. request.Events, Event(new PartyDeactivated(), 2)] }, "next-live", Token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        client.Submissions.ShouldBe(2);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).Select(item => item.MemoryUnitId).ShouldBe(["canonical", "next-canonical"]);
        remote.Requests.ShouldAllBe(item => item.Method != HttpMethod.Delete);

        string indexKey = PartySdkReadModelAddresses.Index("tenant");
        var current = await state.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, Token);
        (await state.TrySaveAsync("statestore", indexKey, current.Value! with { ErasureSequenceNumbers = new Dictionary<string, long> { ["party"] = 3 } }, current.ETag!, Token)).ShouldBeTrue();
        (await adapter.NotifyRemovedAsync("tenant", "party", Token)).ShouldBeTrue();
        remote.Units.ShouldBeEmpty();
        (await state.GetAsync<PartyMemoryIngestionInventory>("statestore", inventoryKey, Token)).Value.ShouldNotBeNull().PermanentCleanup.ShouldBeTrue();
    }

    private static ProjectionEventDto Event(object payload, long sequence, DateTimeOffset? timestamp = null)
        => new(payload.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), PartiesJsonOptions.Default),
            "json", sequence, timestamp ?? DateTimeOffset.UnixEpoch.AddMinutes(sequence), "synthetic-correlation");
}
