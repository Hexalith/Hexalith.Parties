using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Search;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>
/// Verifies full-phrase matches improve field matching without adding query-token coverage.
/// </summary>
public class SearchRelevanceCoverageTests
{
    /// <summary>
    /// Checks exact, prefix and contains scores use only real query-token coverage.
    /// </summary>
    /// <param name="semantic">Whether to exercise the semantic provider.</param>
    /// <param name="query">The query including any repeated tokens.</param>
    /// <param name="displayName">The candidate party name.</param>
    /// <param name="matchType">The expected strongest field match.</param>
    /// <param name="fieldScore">The expected field score.</param>
    /// <param name="relevance">The expected overall relevance.</param>
    [Theory]
    [InlineData(false, "123", "123", "exact", 1.0, 1.0)]
    [InlineData(true, "123", "123", "exact", 1.0, 1.0)]
    [InlineData(false, "123", "123 Name", "prefix", 0.8, 0.82)]
    [InlineData(true, "123", "123 Name", "prefix", 0.8, 0.82)]
    [InlineData(false, "123", "Name 123", "contains", 0.6, 0.64)]
    [InlineData(true, "123", "Name 123", "contains", 0.6, 0.64)]
    [InlineData(false, "123 456", "123 456", "exact", 1.0, 1.0)]
    [InlineData(true, "123 456", "123 456", "exact", 1.0, 1.0)]
    [InlineData(false, "123 456", "123 456 Name", "prefix", 0.8, 0.82)]
    [InlineData(true, "123 456", "123 456 Name", "prefix", 0.8, 0.82)]
    [InlineData(false, "123 456", "Name 123 456", "contains", 0.6, 0.64)]
    [InlineData(true, "123 456", "Name 123 456", "contains", 0.6, 0.64)]
    [InlineData(false, "123 456 789", "123 456 789 Name", "prefix", 0.8, 0.82)]
    [InlineData(true, "123 456 789", "123 456 789 Name", "prefix", 0.8, 0.82)]
    [InlineData(false, "123 456 789", "Name 123 456 789", "contains", 0.6, 0.64)]
    [InlineData(true, "123 456 789", "Name 123 456 789", "contains", 0.6, 0.64)]
    [InlineData(false, "123 123", "123 123", "exact", 1.0, 0.95)]
    [InlineData(true, "123 123", "123 123", "exact", 1.0, 0.95)]
    [InlineData(false, "123 123", "123 123 Name", "prefix", 0.8, 0.77)]
    [InlineData(true, "123 123", "123 123 Name", "prefix", 0.8, 0.77)]
    [InlineData(false, "ABC abc", "ABC abc Name", "prefix", 0.8, 0.82)]
    [InlineData(true, "ABC abc", "ABC abc Name", "prefix", 0.8, 0.82)]
    public void Search_FieldMatch_CountsOnlyRealTokens(
        bool semantic,
        string query,
        string displayName,
        string matchType,
        double fieldScore,
        double relevance)
    {
        IPartySearchProvider provider = CreateProvider(semantic);
        PartyIndexEntry entry = new() { Id = "p1", Type = PartyType.Person, DisplayName = displayName };

        PagedResult<PartySearchResult> result = provider.Search(
            [entry], query, null, null, 1, 20, TestContext.Current.CancellationToken);

        PartySearchResult match = result.Items.ShouldHaveSingleItem();
        match.RelevanceScore.ShouldBe(relevance, 0.000_001);
        match.Matches.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            metadata => metadata.MatchedField.ShouldBe("displayName"),
            metadata => metadata.MatchType.ShouldBe(matchType),
            metadata => metadata.Score.ShouldBe(fieldScore));
    }

    /// <summary>
    /// Checks equally covered phrase and token matches use the existing name ordering across pages.
    /// </summary>
    /// <param name="semantic">Whether to exercise the semantic provider.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Search_EqualFieldScoresAndTokenCoverage_PagesByName(bool semantic)
    {
        IPartySearchProvider provider = CreateProvider(semantic);
        PartyIndexEntry phrase = new() { Id = "phrase", Type = PartyType.Person, DisplayName = "123 456 Name" };
        PartyIndexEntry tokens = phrase with { Id = "tokens", DisplayName = "123 000 456 Name" };

        for (int page = 1; page <= 2; page++)
        {
            foreach (PartyIndexEntry[] entries in new[] { new[] { phrase, tokens }, new[] { tokens, phrase } })
            {
                PagedResult<PartySearchResult> result = provider.Search(
                    entries, "123 456", null, null, page, 1, TestContext.Current.CancellationToken);

                PartySearchResult match = result.Items.ShouldHaveSingleItem();
                match.Party.Id.ShouldBe(page == 1 ? "tokens" : "phrase");
                match.RelevanceScore.ShouldBe(0.82, 0.000_001);
                result.Page.ShouldBe(page);
                result.PageSize.ShouldBe(1);
                result.TotalCount.ShouldBe(2);
                result.TotalPages.ShouldBe(2);
            }
        }
    }

    /// <summary>
    /// Checks semantic phrase matching preserves contact weights without adding coverage.
    /// </summary>
    /// <param name="channelType">The channel type whose weight applies.</param>
    /// <param name="field">The expected metadata field.</param>
    /// <param name="weight">The expected weighted exact-match score.</param>
    /// <param name="relevance">The expected overall relevance.</param>
    [Theory]
    [InlineData(ContactChannelType.Email, "email", 0.9, 0.91)]
    [InlineData(ContactChannelType.Phone, "contactChannel", 0.7, 0.73)]
    public void Search_SemanticContactPhrase_PreservesFieldWeight(
        ContactChannelType channelType,
        string field,
        double weight,
        double relevance)
    {
        SemanticPartySearchProvider provider = new();
        PartyIndexEntry entry = new()
        {
            Id = "p1",
            Type = PartyType.Person,
            DisplayName = string.Empty,
            SearchableContactChannels =
            [
                new ContactChannel { Id = "c1", Type = channelType, Value = "123 456" },
            ],
        };

        PagedResult<PartySearchResult> result = provider.Search(
            [entry], "123 456", null, null, 1, 20, TestContext.Current.CancellationToken);

        PartySearchResult match = result.Items.ShouldHaveSingleItem();
        match.RelevanceScore.ShouldBe(relevance, 0.000_001);
        match.Matches.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            metadata => metadata.MatchedField.ShouldBe(field),
            metadata => metadata.MatchType.ShouldBe("exact"),
            metadata => metadata.Score.ShouldBe(weight));
    }

    /// <summary>
    /// Checks semantic identifier phrase matching retains exact metadata and its field weight.
    /// </summary>
    [Fact]
    public void Search_SemanticIdentifierPhrase_PreservesFieldWeight()
    {
        SemanticPartySearchProvider provider = new();
        PartyIndexEntry entry = new()
        {
            Id = "p1",
            Type = PartyType.Person,
            DisplayName = string.Empty,
            SearchableIdentifiers =
            [
                new PartyIdentifier { Id = "i1", Type = IdentifierType.VAT, Value = "123 456" },
            ],
        };

        PagedResult<PartySearchResult> result = provider.Search(
            [entry], "123 456", null, null, 1, 20, TestContext.Current.CancellationToken);

        PartySearchResult match = result.Items.ShouldHaveSingleItem();
        match.RelevanceScore.ShouldBe(0.82, 0.000_001);
        match.Matches.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            metadata => metadata.MatchedField.ShouldBe("identifier"),
            metadata => metadata.MatchType.ShouldBe("exact"),
            metadata => metadata.Score.ShouldBe(0.8));
    }

    /// <summary>
    /// Checks semantic sort names break corrected score ties before display names across pages.
    /// </summary>
    [Fact]
    public void Search_SemanticEqualScores_PagesBySortName()
    {
        SemanticPartySearchProvider provider = new();
        PartyIndexEntry phrase = new()
        {
            Id = "phrase",
            Type = PartyType.Person,
            DisplayName = "123 456 Name",
            SortName = "A",
        };
        PartyIndexEntry tokens = phrase with { Id = "tokens", DisplayName = "123 000 456 Name", SortName = "Z" };

        for (int page = 1; page <= 2; page++)
        {
            foreach (PartyIndexEntry[] entries in new[] { new[] { phrase, tokens }, new[] { tokens, phrase } })
            {
                PagedResult<PartySearchResult> result = provider.Search(
                    entries, "123 456", null, null, page, 1, TestContext.Current.CancellationToken);

                PartySearchResult match = result.Items.ShouldHaveSingleItem();
                match.Party.Id.ShouldBe(page == 1 ? "phrase" : "tokens");
                match.RelevanceScore.ShouldBe(0.82, 0.000_001);
                result.Page.ShouldBe(page);
                result.PageSize.ShouldBe(1);
                result.TotalCount.ShouldBe(2);
                result.TotalPages.ShouldBe(2);
            }
        }
    }

    /// <summary>
    /// Checks phrase hits across fields retain global token coverage and weighted metadata ordering.
    /// </summary>
    [Fact]
    public void Search_SemanticPhraseAcrossFields_CountsEachRealTokenOnce()
    {
        SemanticPartySearchProvider provider = new();
        PartyIndexEntry entry = new()
        {
            Id = "p1",
            Type = PartyType.Person,
            DisplayName = "123 456",
            SearchableContactChannels =
            [
                new ContactChannel { Id = "c1", Type = ContactChannelType.Email, Value = "123 456" },
            ],
            SearchableIdentifiers =
            [
                new PartyIdentifier { Id = "i1", Type = IdentifierType.VAT, Value = "123 456" },
            ],
        };

        PagedResult<PartySearchResult> result = provider.Search(
            [entry], "123 456", null, null, 1, 20, TestContext.Current.CancellationToken);

        PartySearchResult match = result.Items.ShouldHaveSingleItem();
        match.RelevanceScore.ShouldBe(0.98, 0.000_001);
        match.Matches.Select(m => m.MatchedField).ShouldBe(["displayName", "email", "identifier"]);
        match.Matches.Select(m => m.Score).ShouldBe([1.0, 0.9, 0.8]);
        match.Matches.ShouldAllBe(m => m.MatchType == "exact");
    }

    private static IPartySearchProvider CreateProvider(bool semantic)
        => semantic ? new SemanticPartySearchProvider() : new LocalFuzzyPartySearchProvider();
}
