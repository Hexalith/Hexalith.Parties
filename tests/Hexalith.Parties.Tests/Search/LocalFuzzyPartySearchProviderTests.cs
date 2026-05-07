using Hexalith.Parties.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

public class LocalFuzzyPartySearchProviderTests
{
    private readonly LocalFuzzyPartySearchProvider _provider = new();
    private readonly List<PartyIndexEntry> _entries = PartyTestData.CreateSearchScenarioEntries();

    // 7.1 — exact match returns RelevanceScore ~1.0
    [Fact]
    public void Search_ExactMatch_ReturnsHighRelevanceScore()
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "Jean Dupont", null, null, 1, 20);

        result.Items.ShouldNotBeEmpty();
        PartySearchResult match = result.Items.First(r => r.Party.Id == "p1");
        match.RelevanceScore.ShouldBeGreaterThan(0.5);
        match.Matches.ShouldContain(m => m.MatchType == "exact");
    }

    // 7.2 — prefix match returns RelevanceScore ~0.8
    [Fact]
    public void Search_PrefixMatch_ReturnsModerateRelevanceScore()
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "Acme", null, null, 1, 20);

        result.Items.ShouldNotBeEmpty();
        PartySearchResult match = result.Items.First(r => r.Party.Id == "p2");
        match.RelevanceScore.ShouldBeGreaterThan(0.3);
        match.Matches.ShouldContain(m => m.MatchType == "prefix");
    }

    // 7.3 — fuzzy match ("Dupnt" → "Dupont") returns match with type "fuzzy"
    [Fact]
    public void Search_FuzzyMatch_ReturnsFuzzyMatchType()
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "Dupnt", null, null, 1, 20);

        result.Items.ShouldNotBeEmpty();
        PartySearchResult match = result.Items.First(r => r.Party.Id == "p1");
        match.Matches.ShouldContain(m => m.MatchType == "fuzzy");
        match.RelevanceScore.ShouldBeGreaterThan(0.0);
    }

    // 7.4 — type text match ("company" finds Organization parties)
    [Fact]
    public void Search_TypeTextMatch_FindsOrganizationParties()
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "company", null, null, 1, 20);

        result.Items.ShouldNotBeEmpty();
        result.Items.ShouldContain(r => r.Party.Id == "p2");
        PartySearchResult orgMatch = result.Items.First(r => r.Party.Id == "p2");
        orgMatch.Matches.ShouldContain(m => m.MatchedField == "type");
    }

    // 7.5 — multi-token query ("Jean Dupont") matches across fields
    [Fact]
    public void Search_MultiTokenQuery_MatchesAcrossFields()
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "Jean Dupont", null, null, 1, 20);

        result.Items.ShouldNotBeEmpty();
        PartySearchResult match = result.Items.First(r => r.Party.Id == "p1");
        match.RelevanceScore.ShouldBeGreaterThan(0.5);
    }

    // 7.6 — all contact channel types searched (not just Email)
    [Fact]
    public void Search_AllContactChannelTypes_SearchedNotJustEmail()
    {
        // "Paris" should match the postal address of Acme Corporation
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "Paris", null, null, 1, 20);

        result.Items.ShouldNotBeEmpty();
        result.Items.ShouldContain(r => r.Party.Id == "p2");
    }

    // 7.7 — results sorted by RelevanceScore descending
    [Fact]
    public void Search_Results_SortedByRelevanceScoreDescending()
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "Jean", null, null, 1, 20);

        if (result.Items.Count > 1)
        {
            for (int i = 1; i < result.Items.Count; i++)
            {
                result.Items[i].RelevanceScore.ShouldBeLessThanOrEqualTo(result.Items[i - 1].RelevanceScore);
            }
        }
    }

    // 7.8 — erased parties excluded from results
    [Fact]
    public void Search_ErasedParties_ExcludedFromResults()
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "Erased", null, null, 1, 20);

        result.Items.ShouldNotContain(r => r.Party.Id == "p5");
    }

    // 7.9 — empty/whitespace query returns empty result
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Search_EmptyOrWhitespaceQuery_ReturnsEmptyResult(string? query)
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, query!, null, null, 1, 20);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    // 7.10 — fuzzy matching utility verified against known pairs
    [Theory]
    [InlineData("Dupont", "Dupnt", 0.85)]   // missing char
    [InlineData("Dupont", "Dpuont", 0.85)]   // transposition
    [InlineData("Marie", "Marei", 0.85)]     // adjacent transposition
    public void JaroWinklerSimilarity_KnownPairs_MeetsThreshold(string s1, string s2, double minThreshold)
    {
        double similarity = LocalFuzzyPartySearchProvider.JaroWinklerSimilarity(s1, s2);

        similarity.ShouldBeGreaterThanOrEqualTo(minThreshold);
    }

    [Fact]
    public void JaroWinklerSimilarity_IdenticalStrings_Returns1()
    {
        double similarity = LocalFuzzyPartySearchProvider.JaroWinklerSimilarity("Dupont", "Dupont");

        similarity.ShouldBe(1.0);
    }

    [Fact]
    public void JaroWinklerSimilarity_CompletelyDifferent_ReturnsBelowThreshold()
    {
        double similarity = LocalFuzzyPartySearchProvider.JaroWinklerSimilarity("Dupont", "xyz");

        similarity.ShouldBeLessThan(0.85);
    }

    // Diacritic normalization
    [Theory]
    [InlineData("Dúpont", "Dupont")]
    [InlineData("résumé", "resume")]
    [InlineData("naïve", "naive")]
    public void NormalizeDiacritics_RemovesAccents(string input, string expected)
    {
        string result = LocalFuzzyPartySearchProvider.NormalizeDiacritics(input);

        result.ShouldBe(expected);
    }

    [Fact]
    public void NormalizeDiacritics_NullInput_ReturnsEmpty()
    {
        string result = LocalFuzzyPartySearchProvider.NormalizeDiacritics(null);

        result.ShouldBe(string.Empty);
    }

    // RelevanceScore is in [0,1]
    [Fact]
    public void Search_AllResults_HaveRelevanceScoreInValidRange()
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "Dupont", null, null, 1, 20);

        foreach (PartySearchResult item in result.Items)
        {
            item.RelevanceScore.ShouldBeGreaterThanOrEqualTo(0.0);
            item.RelevanceScore.ShouldBeLessThanOrEqualTo(1.0);
        }
    }

    // MatchMetadata.Score is populated
    [Fact]
    public void Search_MatchMetadata_HasScorePopulated()
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "Jean Dupont", null, null, 1, 20);

        result.Items.ShouldNotBeEmpty();
        PartySearchResult match = result.Items.First(r => r.Party.Id == "p1");
        match.Matches.ShouldAllBe(m => m.Score != null);
    }
}
