using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.CommandApi.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Search;

public class MemoriesPartySearchServiceTests
{
    [Fact]
    public async Task DefaultRichSearchUsesHybridSearchAndHydratesAuthorizedParty()
    {
        var client = new RecordingMemoriesClient
        {
            HybridResult = new HybridSearchResult
            {
                Results =
                [
                    new FusedScoredResult
                    {
                        MemoryUnitId = "memory-1",
                        CompositeScore = 0.91,
                        SyntacticScore = 0.8,
                        SemanticScore = 0.95,
                        GraphScore = 0.7,
                        ContentSnippet = "Jean Dupont",
                        SourceUri = "urn:hexalith:parties:tenant-a:party:p1",
                        SourceType = SourceType.Event,
                        CaseId = "case-a",
                    },
                ],
                TotalCount = 1,
                Degraded = false,
                UnavailableAxes = [],
                Query = "jean",
            },
        };
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest("tenant-a", "jean", PartySearchMode.Hybrid, null, null, 1, 20, CaseId: "case-a", AuthorizedPartyIds: new HashSet<string> { "p1" }),
            [CreateEntry("p1", "Jean Dupont")],
            CancellationToken.None).ConfigureAwait(true);

        client.HybridCalls.ShouldBe(1);
        response.Status.ShouldBe(PartySearchExecutionStatus.Rich);
        response.Results.Items.Single().Party.Id.ShouldBe("p1");
        response.ScoreMetadata.Single().CompositeScore.ShouldBe(0.91);
        response.ScoreMetadata.Single().LexicalScore.ShouldBe(0.8);
        response.ScoreMetadata.Single().SemanticScore.ShouldBe(0.95);
        response.ScoreMetadata.Single().GraphScore.ShouldBe(0.7);
        response.SourceMetadata.Single().MemoryUnitId.ShouldBe("memory-1");
    }

    [Fact]
    public Task LexicalOnlySearchUsesSyntacticAxis()
        => ExplicitSingleAxisSearchUsesRequestedAxisAsync(PartySearchMode.Lexical, "syntactic");

    [Fact]
    public Task SemanticOnlySearchUsesSemanticAxis()
        => ExplicitSingleAxisSearchUsesRequestedAxisAsync(PartySearchMode.Semantic, "semantic");

    private static async Task ExplicitSingleAxisSearchUsesRequestedAxisAsync(PartySearchMode mode, string expectedAxis)
    {
        var client = new RecordingMemoriesClient
        {
            SearchResult = new SearchResult
            {
                Results =
                [
                    new ScoredResult
                    {
                        MemoryUnitId = "memory-1",
                        Score = 0.75,
                        ContentSnippet = "Jean Dupont",
                        SourceUri = "urn:hexalith:parties:tenant-a:party:p1",
                        SourceType = SourceType.Event,
                        Axis = expectedAxis,
                        CaseId = "case-a",
                    },
                ],
                TotalCount = 1,
                HasIndexedMemoryUnits = true,
                Query = "jean",
            },
        };
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest("tenant-a", "jean", mode, null, null, 1, 20, CaseId: "case-a"),
            [CreateEntry("p1", "Jean Dupont")],
            CancellationToken.None).ConfigureAwait(true);

        client.LastAxis.ShouldBe(expectedAxis);
        response.Results.Items.Single().Party.Id.ShouldBe("p1");
        response.ScoreMetadata.Single().RelevanceScore.ShouldBe(0.75);
    }

    [Fact]
    public async Task GraphAssistedSearchTraversesContextAndHydratesRelatedParties()
    {
        var client = new RecordingMemoriesClient
        {
            TraversalResult = new TraversalResult(
                "memory-start",
                2,
                [
                    new TraversalNode(
                        "memory-related",
                        "Acme",
                        "urn:hexalith:parties:tenant-a:party:p2",
                        SourceType.Event,
                        DateTimeOffset.UtcNow,
                        1,
                        []),
                ],
                1),
        };
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest("tenant-a", "related", PartySearchMode.Graph, null, null, 1, 20, CaseId: "case-a", GraphContextMemoryUnitId: "memory-start"),
            [CreateEntry("p2", "Acme")],
            CancellationToken.None).ConfigureAwait(true);

        client.TraverseCalls.ShouldBe(1);
        response.Results.Items.Single().Party.Id.ShouldBe("p2");
        response.SourceMetadata.Single().MemoryUnitId.ShouldBe("memory-related");
    }

    [Fact]
    public async Task HydrationOmitsStaleErasedUnauthorizedAndWrongTenantHits()
    {
        var client = new RecordingMemoriesClient
        {
            HybridResult = new HybridSearchResult
            {
                Results =
                [
                    CreateHit("memory-stale", "urn:hexalith:parties:tenant-a:party:missing"),
                    CreateHit("memory-erased", "urn:hexalith:parties:tenant-a:party:erased"),
                    CreateHit("memory-unauthorized", "urn:hexalith:parties:tenant-a:party:unauthorized"),
                    CreateHit("memory-wrong-tenant", "urn:hexalith:parties:tenant-b:party:allowed"),
                    CreateHit("memory-allowed", "urn:hexalith:parties:tenant-a:party:allowed"),
                ],
                TotalCount = 5,
                Degraded = false,
                UnavailableAxes = [],
                Query = "party",
            },
        };
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest("tenant-a", "party", PartySearchMode.Hybrid, null, null, 1, 20, AuthorizedPartyIds: new HashSet<string> { "allowed" }),
            [
                CreateEntry("erased", "Erased") with { IsErased = true },
                CreateEntry("unauthorized", "Unauthorized"),
                CreateEntry("allowed", "Allowed"),
            ],
            CancellationToken.None).ConfigureAwait(true);

        response.Results.Items.Select(r => r.Party.Id).ShouldBe(["allowed"]);
    }

    [Fact]
    public async Task DuplicateMemoryHitsCollapseToOnePartyResult()
    {
        var client = new RecordingMemoriesClient
        {
            HybridResult = new HybridSearchResult
            {
                Results =
                [
                    CreateHit("memory-1", "urn:hexalith:parties:tenant-a:party:p1", 0.4),
                    CreateHit("memory-2", "urn:hexalith:parties:tenant-a:party:p1", 0.9),
                ],
                TotalCount = 2,
                Degraded = false,
                UnavailableAxes = [],
                Query = "party",
            },
        };
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest("tenant-a", "party", PartySearchMode.Hybrid, null, null, 1, 20),
            [CreateEntry("p1", "Jean Dupont")],
            CancellationToken.None).ConfigureAwait(true);

        response.Results.Items.Count.ShouldBe(1);
        response.ScoreMetadata.Single().CompositeScore.ShouldBe(0.9);
        response.SourceMetadata.Single().MemoryUnitId.ShouldBe("memory-2");
    }

    [Fact]
    public async Task UnavailableMemoriesReturnsDegradedDisplayNameFallback()
    {
        var client = new ThrowingMemoriesClient(new HttpRequestException("connection refused"));
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest("tenant-a", "Dupont", PartySearchMode.Hybrid, null, null, 1, 20),
            [CreateEntry("p1", "Jean Dupont")],
            CancellationToken.None).ConfigureAwait(true);

        response.Status.ShouldBe(PartySearchExecutionStatus.Degraded);
        response.DegradedReason.ShouldNotBeNull();
        response.DegradedReason.ShouldContain("HttpRequestException");
        response.Results.Items.ShouldContain(r => r.Party.Id == "p1");
        // Fallback path must not advertise rich-search axes
        response.ScoreMetadata.ShouldAllBe(s => s.LexicalScore == null && s.SemanticScore == null && s.GraphScore == null && s.CompositeScore == null);
    }

    [Fact]
    public async Task FallbackSearchDoesNotAdvertiseSemanticOrGraphScores()
    {
        var client = new ThrowingMemoriesClient(new HttpRequestException("memories unreachable"));
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest("tenant-a", "Dupont", PartySearchMode.Hybrid, null, null, 1, 20),
            [CreateEntry("p1", "Jean Dupont")],
            CancellationToken.None).ConfigureAwait(true);

        response.Status.ShouldBe(PartySearchExecutionStatus.Degraded);
        foreach (PartySearchScoreMetadata metadata in response.ScoreMetadata)
        {
            metadata.LexicalScore.ShouldBeNull();
            metadata.SemanticScore.ShouldBeNull();
            metadata.GraphScore.ShouldBeNull();
            metadata.CompositeScore.ShouldBeNull();
        }

        foreach (PartySearchResult result in response.Results.Items)
        {
            result.Matches.ShouldAllBe(m => m.MatchType != "semantic" && m.MatchType != "graph" && m.MatchType != "hybrid");
        }
    }

    [Fact]
    public async Task DisabledAxisFallsBackToLocalDisplayNameSearch()
    {
        var client = new RecordingMemoriesClient();
        var service = CreateService(client, options: new PartyMemorySearchOptions
        {
            Enabled = true,
            EnabledAxes = ["hybrid"], // semantic disabled
            TenantId = "tenant-a",
            CaseId = "case-a",
            Endpoint = new Uri("https://memories.example"),
            ApiToken = "x",
        });

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest("tenant-a", "Dupont", PartySearchMode.Semantic, null, null, 1, 20),
            [CreateEntry("p1", "Jean Dupont")],
            CancellationToken.None).ConfigureAwait(true);

        client.HybridCalls.ShouldBe(0);
        client.LastAxis.ShouldBeNull();
        response.Status.ShouldBe(PartySearchExecutionStatus.Degraded);
        response.DegradedReason.ShouldNotBeNull();
        response.DegradedReason.ShouldContain("disabled");
    }

    [Fact]
    public async Task GraphSearchWithoutContextFallsBackToLocalDisplayNameSearch()
    {
        var client = new RecordingMemoriesClient();
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest("tenant-a", "Dupont", PartySearchMode.Graph, null, null, 1, 20),
            [CreateEntry("p1", "Jean Dupont")],
            CancellationToken.None).ConfigureAwait(true);

        client.TraverseCalls.ShouldBe(0);
        response.Status.ShouldBe(PartySearchExecutionStatus.Degraded);
        response.DegradedReason.ShouldNotBeNull();
        response.DegradedReason.ShouldContain("graph context");
    }

    [Fact]
    public async Task CaseScopedRequestDropsCandidatesFromOtherCases()
    {
        var client = new RecordingMemoriesClient
        {
            HybridResult = new HybridSearchResult
            {
                Results =
                [
                    CreateHit("memory-other", "urn:hexalith:parties:tenant-a:party:p1", 0.9, caseId: "case-other"),
                    CreateHit("memory-allowed", "urn:hexalith:parties:tenant-a:party:p2", 0.8, caseId: "case-a"),
                ],
                TotalCount = 2,
                Degraded = false,
                UnavailableAxes = [],
                Query = "party",
            },
        };
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest("tenant-a", "party", PartySearchMode.Hybrid, null, null, 1, 20, CaseId: "case-a"),
            [CreateEntry("p1", "Jean"), CreateEntry("p2", "Acme")],
            CancellationToken.None).ConfigureAwait(true);

        response.Results.Items.Select(r => r.Party.Id).ShouldBe(["p2"]);
    }

    [Fact]
    public async Task PartyIdContainingColonRoundTripsThroughEncodedUrn()
    {
        string awkwardId = "tenant:party:1";
        var client = new RecordingMemoriesClient
        {
            HybridResult = new HybridSearchResult
            {
                Results =
                [
                    CreateHit("memory-1", PartyMemoryUrn.Build("tenant-a", awkwardId)),
                ],
                TotalCount = 1,
                Degraded = false,
                UnavailableAxes = [],
                Query = "party",
            },
        };
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest("tenant-a", "party", PartySearchMode.Hybrid, null, null, 1, 20),
            [CreateEntry(awkwardId, "Awkward")],
            CancellationToken.None).ConfigureAwait(true);

        response.Results.Items.Single().Party.Id.ShouldBe(awkwardId);
    }

    private static FusedScoredResult CreateHit(string memoryUnitId, string sourceUri, double score = 0.8, string? caseId = null)
        => new()
        {
            MemoryUnitId = memoryUnitId,
            CompositeScore = score,
            ContentSnippet = memoryUnitId,
            SourceUri = sourceUri,
            SourceType = SourceType.Event,
            CaseId = caseId,
        };

    private static PartyIndexEntry CreateEntry(string id, string displayName)
        => new()
        {
            Id = id,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
            IsErased = false,
        };

    private static MemoriesPartySearchService CreateService(MemoriesClient client, PartyMemorySearchOptions? options = null)
    {
        LocalPartySearchService localFallback = new(new LocalFuzzyPartySearchProvider());
        return new MemoriesPartySearchService(
            client,
            localFallback,
            new StubOptionsMonitor(options ?? new PartyMemorySearchOptions
            {
                Enabled = true,
                EnabledAxes = ["hybrid", "syntactic", "semantic", "graph"],
                TenantId = "tenant-a",
                CaseId = "case-a",
                Endpoint = new Uri("https://memories.example"),
                ApiToken = "x",
            }),
            NullLogger<MemoriesPartySearchService>.Instance);
    }

    private sealed class StubOptionsMonitor(PartyMemorySearchOptions value) : IOptionsMonitor<PartyMemorySearchOptions>
    {
        public PartyMemorySearchOptions CurrentValue { get; } = value;

        public PartyMemorySearchOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<PartyMemorySearchOptions, string?> listener) => null;
    }

    private sealed class RecordingMemoriesClient()
        : MemoriesClient(
            new HttpClient { BaseAddress = new Uri("https://memories.example") },
            Options.Create(new MemoriesClientOptions()),
            NullLogger<MemoriesClient>.Instance)
    {
        public int HybridCalls { get; private set; }

        public int TraverseCalls { get; private set; }

        public string? LastAxis { get; private set; }

        public HybridSearchResult HybridResult { get; init; } = new()
        {
            Results = [],
            TotalCount = 0,
            Degraded = false,
            UnavailableAxes = [],
            Query = string.Empty,
        };

        public SearchResult SearchResult { get; init; } = new()
        {
            Results = [],
            TotalCount = 0,
            HasIndexedMemoryUnits = true,
            Query = string.Empty,
        };

        public TraversalResult TraversalResult { get; init; } = new("start", 1, [], 0);

        public override Task<HybridSearchResult> HybridSearchAsync(HybridSearchRequest request, CancellationToken ct)
        {
            HybridCalls++;
            return Task.FromResult(HybridResult);
        }

        public override Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken ct)
        {
            LastAxis = request.Axis;
            return Task.FromResult(SearchResult);
        }

        public override Task<TraversalResult> TraverseAsync(
            string tenantId,
            string startNodeId,
            int depth = 2,
            string? caseId = null,
            IReadOnlyList<EdgeType>? edgeTypes = null,
            CancellationToken ct = default,
            int? tokenBudget = null)
        {
            TraverseCalls++;
            return Task.FromResult(TraversalResult);
        }
    }

    private sealed class ThrowingMemoriesClient(Exception ex)
        : MemoriesClient(
            new HttpClient { BaseAddress = new Uri("https://memories.example") },
            Options.Create(new MemoriesClientOptions()),
            NullLogger<MemoriesClient>.Instance)
    {
        public override Task<HybridSearchResult> HybridSearchAsync(HybridSearchRequest request, CancellationToken ct)
            => Task.FromException<HybridSearchResult>(ex);

        public override Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken ct)
            => Task.FromException<SearchResult>(ex);

        public override Task<TraversalResult> TraverseAsync(
            string tenantId,
            string startNodeId,
            int depth = 2,
            string? caseId = null,
            IReadOnlyList<EdgeType>? edgeTypes = null,
            CancellationToken ct = default,
            int? tokenBudget = null)
            => Task.FromException<TraversalResult>(ex);
    }
}
