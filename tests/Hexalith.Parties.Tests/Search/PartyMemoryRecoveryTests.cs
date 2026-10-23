using System.Net;

using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Persisted end-state coverage for acknowledgement gaps, delayed producers and restart.</summary>
public sealed class PartyMemoryRecoveryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task IntentPersistenceFailurePreventsRemoteSubmission()
    {
        var state = new PartyMemoryTestReadModelStore { SaveFailure = new InvalidOperationException("unavailable") };
        using var http = Http(new PartyMemoryRecoveryHandler());
        var client = new PartyMemoryRecoveryClient(http);

        PartyMemoryIndexingResult? result = await Indexer(state, client).IndexAsync(Entry(), Context(), Token);

        result.ShouldNotBeNull().Indexed.ShouldBeFalse();
        client.Submissions.ShouldBe(0);
        state.SerializedValues.ShouldBeEmpty();
    }

    [Fact]
    public async Task MalformedWorkflowJsonCannotCertifyIndexingOrCleanup()
    {
        var state = new PartyMemoryTestReadModelStore();
        using var http = Http(new ProbeHandler(200, "{"));

        (await Indexer(state, new PartyMemoryRecoveryClient(http)).IndexAsync(Entry(), Context(), Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        (await Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeFalse();

        (await Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().WorkflowInstanceId.ShouldBe("workflow");
        (await Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task PendingWorkflowSurvivesRestartAndCanonicalRedeliveryWithoutResubmission()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed() with { RuntimeStatus = "Running", MemoryUnitId = null, MemoryUnitStatus = null };
        using var http = Http(handler);
        var client = new PartyMemoryRecoveryClient(http);
        PartyMemoryIndexingResult? first = await Indexer(state, client).IndexAsync(Entry(), Context(), Token);
        first.ShouldNotBeNull().Indexed.ShouldBeFalse();
        (await Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().WorkflowInstanceId.ShouldBe("workflow");
        (await Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();

        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        PartyMemoryIndexingResult? retry = await Indexer(state, client).IndexAsync(Entry(), Context(), Token);

        retry.ShouldNotBeNull().Indexed.ShouldBeTrue();
        client.Submissions.ShouldBe(1);
        client.Token.ShouldNotBeNull().Length.ShouldBe(64);
        (await Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBe([new("canonical", Source(), "case")]);
        (await Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
        state.SerializedValues.ShouldAllBe(json => !json.Contains("private-personal-name", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task InvalidOrNonterminalWorkflowEvidenceRetainsObligations(int variant)
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        IngestionWorkflowStatus completed = PartyMemoryRecoveryHandler.Completed();
        if (variant != 7)
        {
            handler.Workflows["workflow"] = variant switch
            {
                0 => completed with { InstanceId = "other" },
                1 => completed with { TenantId = "other" },
                2 => completed with { CaseId = "other" },
                3 => completed with { RuntimeStatus = "Running" },
                4 => completed with { RuntimeStatus = "Failed", MemoryUnitStatus = MemoryUnitStatus.Failed },
                5 => completed with { MemoryUnitId = " " },
                _ => completed with { MemoryUnitStatus = null },
            };
        }

        using var http = Http(handler);
        PartyMemoryIndexingResult? indexed = await Indexer(state, new PartyMemoryRecoveryClient(http)).IndexAsync(Entry(), Context(), Token);
        PartyMemoryCleanupResult cleaned = await Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token);

        indexed.ShouldNotBeNull().Indexed.ShouldBeFalse();
        cleaned.Cleaned.ShouldBeFalse();
        (await Intents(state).GetAsync("tenant", "party", Token)).Count.ShouldBe(1);
        handler.Requests.ShouldAllBe(request => request.Method != HttpMethod.Delete);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostWorkflowAcknowledgementOrPersistenceNeverCertifiesCleanup(bool lostPersistence)
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var http = Http(handler);
        var client = new PartyMemoryRecoveryClient(http)
        {
            Submit = _ =>
            {
                if (lostPersistence)
                {
                    state.SaveFailure = new InvalidOperationException("persistence unavailable");
                    return Task.FromResult("workflow");
                }

                return Task.FromException<string>(new HttpRequestException("lost acknowledgement"));
            },
        };
        (await Indexer(state, client).IndexAsync(Entry(), Context(), Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        state.SaveFailure = null;

        var restartedClient = new PartyMemoryRecoveryClient(http);
        (await Indexer(state, restartedClient).IndexAsync(Entry(), Context(), Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        (await Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeFalse();

        restartedClient.Submissions.ShouldBe(0);
        (await Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().WorkflowInstanceId.ShouldBeNull();
        handler.Requests.ShouldAllBe(request => !request.Path.Contains("by-source-uri", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CanonicalDuplicateIdentitySurvivesDoubleFailureAndRestartedCleanup()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler { DeleteStatus = 503 };
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        handler.Units.Add("canonical");
        using var http = Http(handler);
        IPartyMemoryUnitMappingStore failingMapping = new IndexingCancellationMappingStore(
            (_, _, _, _, _, _) => Task.FromException(new InvalidOperationException("mapping failure")));

        (await Indexer(state, new PartyMemoryRecoveryClient(http), failingMapping).IndexAsync(Entry(), Context(), Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();

        (await Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
        handler.DeleteStatus = 204;
        (await Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        handler.Units.ShouldBeEmpty();
        (await Intents(state).GetAsync("tenant", "party", Token)).ShouldBeEmpty();
        handler.Requests.Where(request => request.Method == HttpMethod.Delete).ShouldAllBe(request => request.Path.EndsWith("/canonical", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AcceptedDeleteRetainsObligationsUntilUnitIsObservablyAbsent()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler { DelayDeletion = true };
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        handler.Units.Add("canonical");
        using var http = Http(handler);
        (await Indexer(state, new PartyMemoryRecoveryClient(http)).IndexAsync(Entry(), Context(), Token)).ShouldNotBeNull().Indexed.ShouldBeTrue();

        (await Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeFalse();
        (await Intents(state).GetAsync("tenant", "party", Token)).Count.ShouldBe(1);
        (await Mappings(state).GetMappingsAsync("tenant", "party", Token)).Count.ShouldBe(1);
        handler.DelayDeletion = false;

        (await Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        (await Intents(state).GetAsync("tenant", "party", Token)).ShouldBeEmpty();
        (await Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
        state.SerializedValues.ShouldContain(json => json == "[]");
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"code\":\"CASE_NOT_FOUND\"}")]
    [InlineData("{\"code\":\"TENANT_NOT_FOUND\"}")]
    public async Task ArbitraryReadMissCannotDischargeTerminalObligation(string body)
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler { AbsenceBody = body };
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var http = Http(handler);
        await Indexer(state, new PartyMemoryRecoveryClient(http)).IndexAsync(Entry(), Context(), Token);

        (await Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeFalse();

        (await Intents(state).GetAsync("tenant", "party", Token)).Count.ShouldBe(1);
        (await Mappings(state).GetMappingsAsync("tenant", "party", Token)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task RegistrationRacingCleanupIsFencedBeforeSubmission()
    {
        var state = new PartyMemoryTestReadModelStore();
        state.BeforeSave = async () => { _ = await Intents(state).BeginCleanupAsync("tenant", "party", Token).ConfigureAwait(false); };
        using var http = Http(new PartyMemoryRecoveryHandler());
        var client = new PartyMemoryRecoveryClient(http);

        (await Indexer(state, client).IndexAsync(Entry(), Context(), Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();

        client.Submissions.ShouldBe(0);
        (await Intents(state).GetAsync("tenant", "party", Token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task IntentConflictsExhaustFiveAttemptsWithoutSubmittingOrEvicting()
    {
        var state = new PartyMemoryTestReadModelStore { RejectWrites = true };
        using var http = Http(new PartyMemoryRecoveryHandler());
        var client = new PartyMemoryRecoveryClient(http);

        (await Indexer(state, client).IndexAsync(Entry(), Context(), Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();

        state.SaveAttempts.ShouldBe(5);
        client.Submissions.ShouldBe(0);
        state.SerializedValues.ShouldBeEmpty();
    }

    [Fact]
    public async Task CapacityExhaustionPreservesEveryObligationAndBlocksSubmission()
    {
        var state = new PartyMemoryTestReadModelStore();
        PartyMemoryIngestionIntentStore intents = Intents(state);
        for (int i = 0; i < PartyMemoryIngestionIntentStore.Capacity; i++)
        {
            await intents.RegisterAsync("tenant", "party", new(i.ToString(System.Globalization.CultureInfo.InvariantCulture), "case", Source()), Token);
        }

        using var http = Http(new PartyMemoryRecoveryHandler());
        var client = new PartyMemoryRecoveryClient(http);
        (await Indexer(state, client).IndexAsync(Entry(), Context(), Token)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        client.Submissions.ShouldBe(0);
        (await Intents(state).GetAsync("tenant", "party", Token)).Count.ShouldBe(PartyMemoryIngestionIntentStore.Capacity);
    }

    [Fact]
    public async Task ConditionalIntentRemovalPreservesChangedAndConcurrentAttempts()
    {
        var state = new PartyMemoryTestReadModelStore();
        var first = new PartyMemoryIngestionIntent("first", "case", Source(), "workflow");
        await Intents(state).RegisterAsync("tenant", "party", first, Token);
        state.BeforeSave = async () =>
        {
            await Intents(state).UpdateAsync("tenant", "party", first with { MemoryUnitId = "canonical" }, Token).ConfigureAwait(false);
            await Intents(state).RegisterAsync("tenant", "party", new("second", "other-case", Source()), Token).ConfigureAwait(false);
        };

        (await Intents(state).RemoveAsync("tenant", "party", [first], Token)).ShouldBe(2);

        (await Intents(state).GetAsync("tenant", "party", Token)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task ChangedMappingRetainsCanonicalWorkflowEvidenceForCleanupRetry()
    {
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var http = Http(handler);
        await Indexer(state, new PartyMemoryRecoveryClient(http)).IndexAsync(Entry(), Context(), Token);
        PartyMemoryIngestionIntent accepted = (await Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem();
        bool changed = false;
        handler.BeforeRequest = async (request, token) =>
        {
            if (!changed && request.Method == HttpMethod.Delete)
            {
                changed = true;
                await Mappings(state).RecordMappingAsync("tenant", "party", "canonical", "urn:changed", "case", token).ConfigureAwait(false);
            }
        };

        (await Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeFalse();
        (await Intents(state).GetAsync("tenant", "party", Token)).ShouldBeEmpty();
        PartyMemoryIngestionIntent receipt = (await Intents(state).BeginCleanupAsync("tenant", "party", Token)).ShouldHaveSingleItem();
        receipt.ShouldBe(accepted with { CleanupSettled = true });
        handler.Workflows.Clear();
        (await Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        (await Intents(state).GetAsync("tenant", "party", Token)).ShouldBeEmpty();
        (await Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
    }

    private static string Source() => PartyMemoryUrn.Build("tenant", "party");

    internal static PartyIndexEntry Entry() => new() { Id = "party", Type = Hexalith.Parties.Contracts.ValueObjects.PartyType.Person, DisplayName = "private-personal-name", IsActive = true };

    internal static PartyMemoryUnitMappingContext Context() => new("tenant", "case", "PartyCreated");

    internal static PartyMemoryIngestionIntentStore Intents(PartyMemoryTestReadModelStore state) => new(state, Options.Create(new PartyMemoryUnitMappingStoreOptions()));

    internal static PartyMemoryUnitMappingStore Mappings(PartyMemoryTestReadModelStore state) => new(state, Options.Create(new PartyMemoryUnitMappingStoreOptions()), NullLogger<PartyMemoryUnitMappingStore>.Instance);

    internal static PartyMemoryCleanupService Cleanup(PartyMemoryTestReadModelStore state, HttpClient http) => new(http, Mappings(state), NullLogger<PartyMemoryCleanupService>.Instance, Intents(state));

    internal static HttpClient Http(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://127.0.0.1:1/gateway/") };

    internal static PartyMemoryIndexingService Indexer(PartyMemoryTestReadModelStore state, Hexalith.Memories.Client.Rest.MemoriesClient client, IPartyMemoryUnitMappingStore? mappings = null)
    {
        IOptionsMonitor<PartyMemorySearchOptions> monitor = new PartyMemoryTestOptionsMonitor(new PartyMemorySearchOptions());
        return new(client, mappings ?? Mappings(state), monitor, NullLogger<PartyMemoryIndexingService>.Instance, Intents(state));
    }
}
