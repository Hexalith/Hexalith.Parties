using Hexalith.Parties.CommandApi.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Search;

public class PartySearchServiceBoundaryTests
{
    [Fact]
    public async Task LocalPartySearchServiceReturnsLocalOnlyFallbackMetadata()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        var service = new LocalPartySearchService(provider);
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest(
                TenantId: "tenant-a",
                Query: "Dupnt",
                Mode: PartySearchMode.Hybrid,
                TypeFilter: null,
                ActiveFilter: null,
                Page: 1,
                PageSize: 20),
            entries,
            CancellationToken.None);

        response.Status.ShouldBe(PartySearchExecutionStatus.LocalOnly);
        response.DegradedReason.ShouldBe("Hexalith.Memories rich search is not configured; local display-name fallback was used.");
        response.Results.Items.ShouldContain(r => r.Party.Id == "p1");
        response.Results.Items.ShouldAllBe(r => r.Matches.All(m => m.MatchType != "semantic" && m.MatchType != "graph"));
    }

    [Fact]
    public void SearchRequestCapturesIntentModeAndHydrationScope()
    {
        PartySearchRequest request = new(
            TenantId: "tenant-a",
            Query: "jean",
            Mode: PartySearchMode.Graph,
            TypeFilter: null,
            ActiveFilter: true,
            Page: 2,
            PageSize: 10,
            CaseId: "case-42",
            GraphContextPartyId: "party-1");

        request.TenantId.ShouldBe("tenant-a");
        request.Mode.ShouldBe(PartySearchMode.Graph);
        request.CaseId.ShouldBe("case-42");
        request.GraphContextPartyId.ShouldBe("party-1");
    }

    [Fact]
    public void SearchMetadataModelsKeepScoresAndSourceDetailsInsideCommandApiBoundary()
    {
        PartySearchResponse response = new(
            new PagedResult<PartySearchResult>
            {
                Items = [],
                Page = 1,
                PageSize = 20,
                TotalCount = 0,
                TotalPages = 1,
            },
            PartySearchExecutionStatus.Rich,
            DegradedReason: null,
            ScoreMetadata:
            [
                new PartySearchScoreMetadata(
                    PartyId: "party-1",
                    RelevanceScore: 0.91,
                    LexicalScore: 0.8,
                    SemanticScore: 0.95,
                    GraphScore: 0.7,
                    CompositeScore: 0.91)
            ],
            SourceMetadata:
            [
                new PartySearchSourceMetadata(
                    PartyId: "party-1",
                    SourceSystem: "Hexalith.Memories",
                    SourceUri: "urn:hexalith:parties:tenant-a:party:party-1",
                    MemoryUnitId: "memory-1",
                    EventType: "PartyCreated")
            ]);

        response.ScoreMetadata.Single().SemanticScore.ShouldBe(0.95);
        response.SourceMetadata.Single().SourceSystem.ShouldBe("Hexalith.Memories");
    }
}
