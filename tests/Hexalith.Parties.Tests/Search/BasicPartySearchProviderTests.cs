using Hexalith.Parties.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

// 7.15 — backward compatibility with existing search behavior
public class BasicPartySearchProviderTests
{
    private readonly BasicPartySearchProvider _provider = new();
    private readonly List<PartyIndexEntry> _entries = PartyTestData.CreateSearchScenarioEntries();

    [Fact]
    public void Search_ExactMatch_ReturnsResultsConsistentWithBuilder()
    {
        PagedResult<PartySearchResult> providerResult = _provider.Search(
            _entries.Where(e => !e.IsErased), "Jean Dupont", null, null, 1, 20);

        PagedResult<PartySearchResult> builderResult = PartySearchResultsBuilder.BuildSearchResults(
            _entries.Where(e => !e.IsErased), "Jean Dupont", null, null, 1, 20);

        providerResult.TotalCount.ShouldBe(builderResult.TotalCount);
        providerResult.Items.Count.ShouldBe(builderResult.Items.Count);

        for (int i = 0; i < providerResult.Items.Count; i++)
        {
            providerResult.Items[i].Party.Id.ShouldBe(builderResult.Items[i].Party.Id);
        }
    }

    [Fact]
    public void Search_ReturnsResults_WithDefaultRelevanceScore()
    {
        PagedResult<PartySearchResult> result = _provider.Search(
            _entries.Where(e => !e.IsErased), "Jean Dupont", null, null, 1, 20);

        result.Items.ShouldNotBeEmpty();
        // BasicPartySearchProvider delegates to PartySearchResultsBuilder which doesn't set RelevanceScore
        // so it should be 0.0 (default)
        result.Items.ShouldAllBe(r => r.RelevanceScore == 0.0);
    }

    [Fact]
    public void Search_ReturnsResults_WithNullMatchScore()
    {
        PagedResult<PartySearchResult> result = _provider.Search(
            _entries.Where(e => !e.IsErased), "Jean", null, null, 1, 20);

        result.Items.ShouldNotBeEmpty();
        // BasicPartySearchProvider delegates to PartySearchResultsBuilder which doesn't set Score
        foreach (PartySearchResult item in result.Items)
        {
            item.Matches.ShouldAllBe(m => m.Score == null);
        }
    }

    [Fact]
    public void BuildPagedList_UsesSortNameWhenPresent()
    {
        List<PartyIndexEntry> entries =
        [
            new PartyIndexEntry
            {
                Id = "p-alpha-display",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Alpha Zed",
                SortName = "Zed, Alpha",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
                LastModifiedAt = DateTimeOffset.UtcNow.AddDays(-1),
            },
            new PartyIndexEntry
            {
                Id = "p-zed-display",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Zed Alpha",
                SortName = "Alpha, Zed",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
                LastModifiedAt = DateTimeOffset.UtcNow.AddDays(-1),
            },
        ];

        PagedResult<PartyIndexEntry> result = PartySearchResultsBuilder.BuildPagedList(entries, null, null, 1, 20);

        result.Items.Select(e => e.Id).ShouldBe(["p-zed-display", "p-alpha-display"]);
    }
}
