using Hexalith.Parties.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

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
                PageSize: 20,
                AuthorizedPartyIds: entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal)),
            entries,
            CancellationToken.None);

        response.Status.ShouldBe(PartySearchExecutionStatus.LocalOnly);
        response.DegradedReason.ShouldBeNull();
        response.Results.Items.ShouldContain(r => r.Party.Id == "p1");
        response.Results.Items.ShouldAllBe(r => r.Matches.All(m => m.MatchType != "semantic" && m.MatchType != "graph"));
    }

    [Fact]
    public async Task LocalPartySearchServiceReturnsNoResultsForEmptyAuthorizedPartySet()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        var service = new LocalPartySearchService(provider);
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest(
                TenantId: "tenant-b",
                Query: "Jean",
                Mode: PartySearchMode.Hybrid,
                TypeFilter: null,
                ActiveFilter: null,
                Page: 1,
                PageSize: 20,
                AuthorizedPartyIds: new HashSet<string>(StringComparer.Ordinal)),
            entries,
            CancellationToken.None);

        response.Results.Items.ShouldBeEmpty();
        response.ScoreMetadata.ShouldBeEmpty();
        response.SourceMetadata.ShouldBeEmpty();
    }

    [Fact]
    public async Task LocalPartySearchServiceRequiresExplicitAuthorizedPartyIds()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        var service = new LocalPartySearchService(provider);
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();

        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(() => service.SearchAsync(
            new PartySearchRequest(
                TenantId: "tenant-a",
                Query: "Jean",
                Mode: PartySearchMode.Hybrid,
                TypeFilter: null,
                ActiveFilter: null,
                Page: 1,
                PageSize: 20,
                AuthorizedPartyIds: null),
            entries,
            CancellationToken.None));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task LocalPartySearchServiceAlignsMetadataWithCurrentPage()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        var service = new LocalPartySearchService(provider);
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest(
                TenantId: "tenant-a",
                Query: "person",
                Mode: PartySearchMode.Hybrid,
                TypeFilter: null,
                ActiveFilter: null,
                Page: 2,
                PageSize: 1,
                AuthorizedPartyIds: entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal)),
            entries,
            CancellationToken.None);

        response.Results.Items.Count.ShouldBe(1);
        response.ScoreMetadata.Count.ShouldBe(1);
        response.SourceMetadata.Count.ShouldBe(1);
        response.ScoreMetadata.Single().PartyId.ShouldBe(response.Results.Items.Single().Party.Id);
        response.SourceMetadata.Single().PartyId.ShouldBe(response.Results.Items.Single().Party.Id);
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
    public void SearchMetadataModelsKeepScoresAndSourceDetailsInsidePartiesServiceBoundary()
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
