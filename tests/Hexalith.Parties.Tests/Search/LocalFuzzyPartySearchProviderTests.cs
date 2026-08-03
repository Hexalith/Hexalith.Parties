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

    // 7.4 — type text is not searched in the MVP display-name path
    [Fact]
    public void Search_TypeTextMatch_DoesNotFindOrganizationParties()
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "company", null, null, 1, 20);

        result.Items.ShouldBeEmpty();
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

    [Fact]
    public void Search_ExactMultiTokenQuery_RetainsResultMetadataAndPaging()
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "Jean Dupont", null, null, 1, 1);

        PartySearchResult match = result.Items.ShouldHaveSingleItem();
        match.Party.Id.ShouldBe("p1");
        match.Matches.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            metadata => metadata.MatchedField.ShouldBe("displayName"),
            metadata => metadata.MatchType.ShouldBe("exact"),
            metadata => metadata.Score.ShouldBe(1.0));
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(1);
        result.TotalCount.ShouldBe(1);
        result.TotalPages.ShouldBe(1);
    }

    [Fact]
    public void Search_MisspelledMultiTokenQuery_RetainsTokenFuzzyMatchAndMetadata()
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "Jena Dupnt", null, null, 1, 1);

        PartySearchResult match = result.Items.ShouldHaveSingleItem();
        match.Party.Id.ShouldBe("p1");
        match.Matches.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            metadata => metadata.MatchedField.ShouldBe("displayName"),
            metadata => metadata.MatchType.ShouldBe("fuzzy"),
            metadata => metadata.Score.ShouldBe(0.4));
        match.RelevanceScore.ShouldBe(0.46, 0.000_001);
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(1);
        result.TotalCount.ShouldBe(1);
        result.TotalPages.ShouldBe(1);
    }

    [Fact]
    public void Search_MultiTokenPhraseMatches_RetainOrderingAndPageBoundaries()
    {
        List<PartyIndexEntry> entries =
        [
            _entries[0],
            _entries[0] with
            {
                Id = "p6",
                DisplayName = "The Jean Du Society",
                SortName = "Jean Du Society, The",
            },
        ];

        PagedResult<PartySearchResult> firstPage = _provider.Search(entries, "Jean Du", null, null, 1, 1);
        PagedResult<PartySearchResult> secondPage = _provider.Search(entries, "Jean Du", null, null, 2, 1);

        PartySearchResult prefixMatch = firstPage.Items.ShouldHaveSingleItem();
        prefixMatch.Party.Id.ShouldBe("p1");
        prefixMatch.Matches.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            metadata => metadata.MatchType.ShouldBe("prefix"),
            metadata => metadata.Score.ShouldBe(0.8));
        prefixMatch.RelevanceScore.ShouldBe(0.87, 0.000_001);

        PartySearchResult containsMatch = secondPage.Items.ShouldHaveSingleItem();
        containsMatch.Party.Id.ShouldBe("p6");
        containsMatch.Matches.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            metadata => metadata.MatchType.ShouldBe("contains"),
            metadata => metadata.Score.ShouldBe(0.6));
        containsMatch.RelevanceScore.ShouldBe(0.69, 0.000_001);

        firstPage.ShouldSatisfyAllConditions(
            page => page.Page.ShouldBe(1),
            page => page.PageSize.ShouldBe(1),
            page => page.TotalCount.ShouldBe(2),
            page => page.TotalPages.ShouldBe(2));
        secondPage.ShouldSatisfyAllConditions(
            page => page.Page.ShouldBe(2),
            page => page.PageSize.ShouldBe(1),
            page => page.TotalCount.ShouldBe(2),
            page => page.TotalPages.ShouldBe(2));
    }

    // 7.6 — contact channel values are not searched by the MVP display-name path
    [Fact]
    public void Search_ContactChannelValues_AreNotSearched()
    {
        PagedResult<PartySearchResult> result = _provider.Search(_entries, "Paris", null, null, 1, 20);

        result.Items.ShouldBeEmpty();
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

    [Fact]
    public void Search_ChangedDisplayName_DoesNotMatchStaleDisplayName()
    {
        List<PartyIndexEntry> entries =
        [
            _entries[0] with
            {
                DisplayName = "Renamed Person",
                SortName = "Person, Renamed",
                SearchableContactChannels = [],
                SearchableIdentifiers = [],
            },
        ];

        PagedResult<PartySearchResult> staleResult = _provider.Search(entries, "Jean", null, null, 1, 20);
        PagedResult<PartySearchResult> currentResult = _provider.Search(entries, "Renamed", null, null, 1, 20);

        staleResult.Items.ShouldBeEmpty();
        currentResult.Items.Single().Party.Id.ShouldBe("p1");
    }

    [Fact]
    public void Search_TypeAndActiveFilters_ApplyBeforePagination()
    {
        PagedResult<PartySearchResult> result = _provider.Search(
            _entries,
            "person",
            PartyType.Person,
            false,
            1,
            1);

        result.Items.Single().Party.Id.ShouldBe("p4");
        result.TotalCount.ShouldBe(1);
    }

    [Fact]
    public void Search_ContactAndIdentifierValues_DoNotMatchMvpDisplayNameSearch()
    {
        const string contactQuery = "example.com";
        const string identifierQuery = "synthetic-siret-value";

        PagedResult<PartySearchResult> contactResult = _provider.Search(_entries, contactQuery, null, null, 1, 20);
        PagedResult<PartySearchResult> identifierResult = _provider.Search(_entries, identifierQuery, null, null, 1, 20);

        contactResult.Items.ShouldBeEmpty();
        identifierResult.Items.ShouldBeEmpty();
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
    [InlineData("re\u0301sume\u0301", "resume")]
    [InlineData("naïve", "naive")]
    public void NormalizeDiacritics_RemovesAccents(string input, string expected)
    {
        string result = LocalFuzzyPartySearchProvider.NormalizeDiacritics(input);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Search_DiacriticInsensitiveQuery_MatchesDisplayName()
    {
        List<PartyIndexEntry> entries =
        [
            new PartyIndexEntry
            {
                Id = "p-accented",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Renée Faure",
                SortName = "Faure, Renée",
                CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                LastModifiedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            },
        ];

        PagedResult<PartySearchResult> result = _provider.Search(entries, "Renee", null, null, 1, 20);

        result.Items.Single().Party.Id.ShouldBe("p-accented");
        result.Items.Single().Matches.ShouldContain(m => m.MatchType == "prefix");
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

    [Fact]
    public void Search_DigitContainingIdentifierTokens_DoNotFuzzyMatchNearMissIds()
    {
        List<PartyIndexEntry> entries =
        [
            new()
            {
                Id = "p-50000",
                Type = PartyType.Organization,
                DisplayName = "Entry-50000",
                SortName = "entry-50000",
                IsActive = true,
            },
            new()
            {
                Id = "p-10000",
                Type = PartyType.Organization,
                DisplayName = "Entry-10000",
                SortName = "entry-10000",
                IsActive = true,
            },
        ];

        PagedResult<PartySearchResult> result = _provider.Search(entries, "Entry-50000", null, null, 1, 20);

        PartySearchResult match = result.Items.ShouldHaveSingleItem();
        match.Party.Id.ShouldBe("p-50000");
        match.Matches.ShouldContain(m => m.MatchType == "exact");
        result.Items.ShouldNotContain(r => r.Party.Id == "p-10000");
    }

    [Fact]
    public void Search_MultiTokenNearMissFullPhrase_DoesNotFuzzyMatchWhenTokensMiss()
    {
        // Multi-token queries include the joined full phrase as a candidate; fuzzy is gated
        // off for that phrase. Individual tokens here are not exact/prefix/contains hits and
        // should not fuzzy-match the single-word display name either.
        List<PartyIndexEntry> entries =
        [
            new()
            {
                Id = "p-1",
                Type = PartyType.Organization,
                DisplayName = "Abcdefghij",
                SortName = "abcdefghij",
                IsActive = true,
            },
        ];

        PagedResult<PartySearchResult> result = _provider.Search(entries, "Abcx Efgy", null, null, 1, 20);

        result.Items.ShouldBeEmpty();
    }
}
