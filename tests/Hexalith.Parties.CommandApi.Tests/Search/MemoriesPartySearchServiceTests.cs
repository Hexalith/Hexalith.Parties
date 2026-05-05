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
            CreateRequest("jean", PartySearchMode.Hybrid, CaseId: "case-a", AuthorizedPartyIds: Authorized("p1")),
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
            CreateRequest("jean", mode, CaseId: "case-a", AuthorizedPartyIds: Authorized("p1")),
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
        client.MemoryUnits["memory-related"] = CreateMemoryUnit("memory-related", "p2", "case-a");
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            CreateRequest("related", PartySearchMode.Graph, CaseId: "case-a", GraphContextMemoryUnitId: "memory-start", AuthorizedPartyIds: Authorized("p2")),
            [CreateEntry("p2", "Acme")],
            CancellationToken.None).ConfigureAwait(true);

        client.TraverseCalls.ShouldBe(1);
        client.LastStartNodeId.ShouldBe("memory-start");
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
            CreateRequest("party", PartySearchMode.Hybrid, AuthorizedPartyIds: Authorized("allowed")),
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
            CreateRequest("party", PartySearchMode.Hybrid, AuthorizedPartyIds: Authorized("p1")),
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
            CreateRequest("Dupont", PartySearchMode.Hybrid, AuthorizedPartyIds: Authorized("p1")),
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
            CreateRequest("Dupont", PartySearchMode.Hybrid, AuthorizedPartyIds: Authorized("p1")),
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

        // P23: SourceSystem must announce that the row originated from the local fallback
        // path so consumers cannot mistake degraded local results for Memories-backed hits.
        // Source URI is intentionally null on local fallback so callers do not follow a URN
        // that would 404 against Memories.
        foreach (PartySearchSourceMetadata source in response.SourceMetadata)
        {
            source.SourceSystem.ShouldBe("Hexalith.Parties.LocalFallback");
            source.SourceUri.ShouldBeNull();
            source.MemoryUnitId.ShouldBeNull();
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
            CreateRequest("Dupont", PartySearchMode.Semantic, AuthorizedPartyIds: Authorized("p1")),
            [CreateEntry("p1", "Jean Dupont")],
            CancellationToken.None).ConfigureAwait(true);

        client.HybridCalls.ShouldBe(0);
        client.LastAxis.ShouldBeNull();
        // AC4 status semantics (resolved decision #3): operator-disabled axes return
        // LocalOnly so operations alarms on `Degraded` remain unambiguous (Degraded =
        // runtime outage, LocalOnly = intentional config). The reason text still mentions
        // "disabled" so callers can distinguish this LocalOnly cause from "Memories
        // never configured" (which has no reason text).
        response.Status.ShouldBe(PartySearchExecutionStatus.LocalOnly);
        response.DegradedReason.ShouldNotBeNull();
        response.DegradedReason.ShouldContain("disabled");
    }

    [Fact]
    public async Task GraphSearchWithoutContextFallsBackToLocalDisplayNameSearch()
    {
        var client = new RecordingMemoriesClient();
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            CreateRequest("Dupont", PartySearchMode.Graph, AuthorizedPartyIds: Authorized("p1")),
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
            CreateRequest("party", PartySearchMode.Hybrid, CaseId: "case-a", AuthorizedPartyIds: Authorized("p1", "p2")),
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
            CreateRequest("party", PartySearchMode.Hybrid, AuthorizedPartyIds: Authorized(awkwardId)),
            [CreateEntry(awkwardId, "Awkward")],
            CancellationToken.None).ConfigureAwait(true);

        response.Results.Items.Single().Party.Id.ShouldBe(awkwardId);
    }

    [Fact]
    public async Task GraphSearchWithPartyContextResolvesMemoryUnitBeforeTraversal()
    {
        var client = new RecordingMemoriesClient
        {
            SearchResult = new SearchResult
            {
                Results =
                [
                    new ScoredResult
                    {
                        MemoryUnitId = "memory-party-1",
                        Score = 1.0,
                        ContentSnippet = "Jean Dupont",
                        SourceUri = PartyMemoryUrn.Build("tenant-a", "p1"),
                        SourceType = SourceType.Event,
                        Axis = "syntactic",
                        CaseId = "case-a",
                    },
                ],
                TotalCount = 1,
                HasIndexedMemoryUnits = true,
                Query = PartyMemoryUrn.Build("tenant-a", "p1"),
            },
            TraversalResult = new TraversalResult(
                "memory-party-1",
                2,
                [
                    new TraversalNode("memory-related", "Acme", PartyMemoryUrn.Build("tenant-a", "p2"), SourceType.Event, DateTimeOffset.UtcNow, 1, []),
                ],
                1),
        };
        client.MemoryUnits["memory-related"] = CreateMemoryUnit("memory-related", "p2", "case-a");
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            CreateRequest("related", PartySearchMode.Graph, CaseId: "case-a", GraphContextPartyId: "p1", AuthorizedPartyIds: Authorized("p2")),
            [CreateEntry("p2", "Acme")],
            CancellationToken.None).ConfigureAwait(true);

        client.LastAxis.ShouldBe("syntactic");
        client.LastStartNodeId.ShouldBe("memory-party-1");
        response.Results.Items.Single().Party.Id.ShouldBe("p2");
    }

    [Fact]
    public async Task DefaultCaseScopeFallsBackToConfiguredMemoriesCase()
    {
        var client = new RecordingMemoriesClient
        {
            HybridResult = new HybridSearchResult
            {
                Results =
                [
                    CreateHit("memory-wrong", PartyMemoryUrn.Build("tenant-a", "p1"), 0.9, caseId: "case-other"),
                    CreateHit("memory-right", PartyMemoryUrn.Build("tenant-a", "p2"), 0.8, caseId: "case-a"),
                ],
                TotalCount = 2,
                Degraded = false,
                UnavailableAxes = [],
                Query = "party",
            },
        };
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            CreateRequest("party", PartySearchMode.Hybrid, AuthorizedPartyIds: Authorized("p1", "p2")),
            [CreateEntry("p1", "Wrong"), CreateEntry("p2", "Right")],
            CancellationToken.None).ConfigureAwait(true);

        response.Results.Items.Select(r => r.Party.Id).ShouldBe(["p2"]);
    }

    [Fact]
    public async Task InactivePartyHitIsHydratedWhenCallerExplicitlyRequestsInactive()
    {
        // AC1 + the search boundary's active-state filter: inactive parties are indexed and
        // hydrated when callers explicitly pass ActiveFilter=false. The previous behavior of
        // unconditionally dropping inactive entries hid them from operator/admin views that
        // legitimately want to see deactivated parties.
        var client = new RecordingMemoriesClient
        {
            HybridResult = new HybridSearchResult
            {
                Results = [CreateHit("memory-inactive", PartyMemoryUrn.Build("tenant-a", "p1"), 0.9, caseId: "case-a")],
                TotalCount = 1,
                Degraded = false,
                UnavailableAxes = [],
                Query = "party",
            },
        };
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            CreateRequest("party", PartySearchMode.Hybrid, CaseId: "case-a", ActiveFilter: false, AuthorizedPartyIds: Authorized("p1")),
            [CreateEntry("p1", "Inactive") with { IsActive = false }],
            CancellationToken.None).ConfigureAwait(true);

        response.Results.Items.Count.ShouldBe(1);
        response.Results.Items[0].Party.Id.ShouldBe("p1");
    }

    [Fact]
    public async Task InactivePartyHitIsFilteredOutWhenCallerRequestsActiveOnly()
    {
        var client = new RecordingMemoriesClient
        {
            HybridResult = new HybridSearchResult
            {
                Results = [CreateHit("memory-inactive", PartyMemoryUrn.Build("tenant-a", "p1"), 0.9, caseId: "case-a")],
                TotalCount = 1,
                Degraded = false,
                UnavailableAxes = [],
                Query = "party",
            },
        };
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            CreateRequest("party", PartySearchMode.Hybrid, CaseId: "case-a", ActiveFilter: true, AuthorizedPartyIds: Authorized("p1")),
            [CreateEntry("p1", "Inactive") with { IsActive = false }],
            CancellationToken.None).ConfigureAwait(true);

        response.Results.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchWithoutAuthorizationScopeThrows()
    {
        var service = CreateService(new RecordingMemoriesClient());

        // ArgumentException maps cleanly to a 400 ProblemDetails via the global validation
        // handler; the previous InvalidOperationException surfaced as 500.
        await Should.ThrowAsync<ArgumentException>(() => service.SearchAsync(
            new PartySearchRequest("tenant-a", "party", PartySearchMode.Hybrid, null, null, 1, 20, CaseId: "case-a"),
            [CreateEntry("p1", "Jean")],
            CancellationToken.None)).ConfigureAwait(true);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, -10, 1, 1)]
    [InlineData(1, 500, 1, 100)]
    public async Task ServiceBoundaryNormalizesPaging(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var client = new RecordingMemoriesClient();
        var service = CreateService(client);

        PartySearchResponse response = await service.SearchAsync(
            CreateRequest("party", PartySearchMode.Hybrid, Page: page, PageSize: pageSize, AuthorizedPartyIds: Authorized("p1")),
            [CreateEntry("p1", "Jean")],
            CancellationToken.None).ConfigureAwait(true);

        response.Results.Page.ShouldBe(expectedPage);
        response.Results.PageSize.ShouldBe(expectedPageSize);
    }

    private static FusedScoredResult CreateHit(string memoryUnitId, string sourceUri, double score = 0.8, string? caseId = "case-a")
        => new()
        {
            MemoryUnitId = memoryUnitId,
            CompositeScore = score,
            ContentSnippet = memoryUnitId,
            SourceUri = sourceUri,
            SourceType = SourceType.Event,
            CaseId = caseId,
        };

    private static PartySearchRequest CreateRequest(
        string query,
        PartySearchMode mode,
        int Page = 1,
        int PageSize = 20,
        string? CaseId = null,
        string? GraphContextPartyId = null,
        string? GraphContextMemoryUnitId = null,
        bool? ActiveFilter = null,
        IReadOnlySet<string>? AuthorizedPartyIds = null)
        => new(
            "tenant-a",
            query,
            mode,
            TypeFilter: null,
            ActiveFilter: ActiveFilter,
            Page,
            PageSize,
            CaseId,
            GraphContextPartyId,
            GraphContextMemoryUnitId,
            AuthorizedPartyIds ?? Authorized());

    private static HashSet<string> Authorized(params string[] ids)
        => ids.ToHashSet(StringComparer.Ordinal);

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

    private static MemoryUnit CreateMemoryUnit(string memoryUnitId, string partyId, string caseId)
        => new()
        {
            Id = memoryUnitId,
            TenantId = "tenant-a",
            CaseId = caseId,
            Content = partyId,
            ContentHash = "hash",
            SourceUri = PartyMemoryUrn.Build("tenant-a", partyId),
            SourceType = SourceType.Event,
            IngestedBy = "Hexalith.Parties",
            IngestedAt = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
            Status = MemoryUnitStatus.Indexed,
            Metadata = new Dictionary<string, MetadataField>(StringComparer.Ordinal)
            {
                ["eventType"] = new("PartyCreated", MetadataOrigin.Human, 1.0f),
            },
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

        public string? LastStartNodeId { get; private set; }

        public Dictionary<string, MemoryUnit> MemoryUnits { get; } = new(StringComparer.Ordinal);

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
            LastStartNodeId = startNodeId;
            return Task.FromResult(TraversalResult);
        }

        public override Task<MemoryUnit> GetMemoryUnitAsync(string tenantId, string caseId, string memoryUnitId, CancellationToken ct)
            => Task.FromResult(MemoryUnits[memoryUnitId]);
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
