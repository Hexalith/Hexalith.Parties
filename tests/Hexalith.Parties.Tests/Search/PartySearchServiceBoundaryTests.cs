using Hexalith.Parties.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;
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
                Mode: PartySearchMode.Lexical,
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
                Mode: PartySearchMode.Lexical,
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
                Mode: PartySearchMode.Lexical,
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
                Query: "e",
                Mode: PartySearchMode.Lexical,
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
    public async Task LocalPartySearchServiceDisplayNameSearchKeepsFutureMetadataInert()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        var service = new LocalPartySearchService(provider);
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest(
                TenantId: "tenant-a",
                Query: "Jean",
                Mode: PartySearchMode.Lexical,
                TypeFilter: null,
                ActiveFilter: null,
                Page: 1,
                PageSize: 20,
                AuthorizedPartyIds: entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal)),
            entries,
            CancellationToken.None);

        response.Status.ShouldBe(PartySearchExecutionStatus.LocalOnly);
        response.DegradedReason.ShouldBeNull();
        response.ScoreMetadata.ShouldNotBeEmpty();
        response.ScoreMetadata.ShouldAllBe(metadata =>
            metadata.LexicalScore == null
            && metadata.SemanticScore == null
            && metadata.GraphScore == null
            && metadata.CompositeScore == null);
        response.SourceMetadata.ShouldAllBe(metadata =>
            string.Equals(metadata.SourceSystem, "Hexalith.Parties.LocalFallback", StringComparison.Ordinal)
            && metadata.SourceUri == null
            && metadata.MemoryUnitId == null
            && metadata.EventType == null);
        response.Results.Items
            .SelectMany(static item => item.Matches)
            .ShouldAllBe(match => match.MatchedField == "displayName");
    }

    [Fact]
    public async Task LocalPartySearchServiceAppliesAuthorizedIdsBeforeMatchAndMetadata()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        var service = new LocalPartySearchService(provider);
        List<PartyIndexEntry> entries =
        [
            Entry("tenant-a-party", "Shared Display Name"),
            Entry("tenant-b-party", "Shared Display Name"),
        ];

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest(
                TenantId: "tenant-b",
                Query: "Shared Display Name",
                Mode: PartySearchMode.Lexical,
                TypeFilter: null,
                ActiveFilter: null,
                Page: 1,
                PageSize: 20,
                AuthorizedPartyIds: new HashSet<string>(["tenant-b-party"], StringComparer.Ordinal)),
            entries,
            CancellationToken.None);

        response.Results.Items.Select(static r => r.Party.Id).ShouldBe(["tenant-b-party"]);
        response.Results.TotalCount.ShouldBe(1);
        response.ScoreMetadata.Select(static m => m.PartyId).ShouldBe(["tenant-b-party"]);
        response.SourceMetadata.Select(static m => m.PartyId).ShouldBe(["tenant-b-party"]);
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

    [Theory]
    [InlineData(PartySearchMode.Hybrid)]
    [InlineData(PartySearchMode.Semantic)]
    [InlineData(PartySearchMode.Graph)]
    public async Task LocalPartySearchServiceReturnsUnsupportedForReservedFutureModesWithoutCallingProvider(PartySearchMode mode)
    {
        var service = new LocalPartySearchService(new ThrowingPartySearchProvider());

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest(
                TenantId: "tenant-a",
                Query: "Jean Dupont",
                Mode: mode,
                TypeFilter: null,
                ActiveFilter: null,
                Page: 1,
                PageSize: 20,
                AuthorizedPartyIds: null),
            new ThrowingPartyIndexEntries(),
            CancellationToken.None);

        response.Status.ShouldBe(PartySearchExecutionStatus.Unsupported);
        response.DegradedReason.ShouldBe(LocalPartySearchService.UnsupportedCapabilityReason);
        response.Results.Items.ShouldBeEmpty();
        response.ScoreMetadata.ShouldBeEmpty();
        response.SourceMetadata.ShouldBeEmpty();
        string reason = response.DegradedReason.ShouldNotBeNull();
        reason.ShouldNotContain("tenant-a", Case.Insensitive);
        reason.ShouldNotContain("Jean", Case.Insensitive);
        reason.ShouldContain("not available in MVP", Case.Insensitive);
    }

    private static PartyIndexEntry Entry(string id, string displayName)
        => new()
        {
            Id = id,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        };

    private sealed class ThrowingPartyIndexEntries : IEnumerable<PartyIndexEntry>
    {
        public IEnumerator<PartyIndexEntry> GetEnumerator()
            => throw new InvalidOperationException("Unsupported search must not enumerate projection entries.");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    private sealed class ThrowingPartySearchProvider : IPartySearchProvider
    {
        public PagedResult<PartySearchResult> Search(
            IEnumerable<PartyIndexEntry> entries,
            string query,
            PartyType? typeFilter,
            bool? activeFilter,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unsupported search must not call a provider.");
    }
}
