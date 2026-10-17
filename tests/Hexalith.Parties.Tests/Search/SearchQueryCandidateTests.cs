using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Search;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>
/// Verifies query preparation preserves search behavior and avoids per-entry query copies.
/// </summary>
/// <param name="output">The test output sink for allocation measurements.</param>
[Collection(LocalSearchPerformanceCollection.Name)]
public class SearchQueryCandidateTests(ITestOutputHelper output)
{
    /// <summary>
    /// Checks normalized phrase matching and deterministic ordering across pages.
    /// </summary>
    /// <param name="semantic">Whether to exercise the semantic provider instead of the local fallback.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Search_MultiTokenQuery_PreservesPhraseScoresMetadataAndPaging(bool semantic)
    {
        IPartySearchProvider provider = CreateProvider(semantic);
        PartyIndexEntry exact = new() { Id = "p1", Type = PartyType.Person, DisplayName = "Jean Dupont" };
        PartyIndexEntry[] entries =
        [
            exact,
            exact with { Id = "p2", DisplayName = "The Jean Du Society" },
            exact with { Id = "p0" },
        ];

        for (int page = 1; page <= 3; page++)
        {
            PagedResult<PartySearchResult> result = provider.Search(entries, "  Jéan\t\r\n Du  ", null, null, page, 1);

            PartySearchResult match = result.Items.ShouldHaveSingleItem();
            match.Party.Id.ShouldBe($"p{page - 1}");
            match.RelevanceScore.ShouldBe(page == 3 ? 0.69 : 0.87, 0.000_001);
            match.Matches.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                metadata => metadata.MatchedField.ShouldBe("displayName"),
                metadata => metadata.MatchType.ShouldBe(page == 3 ? "contains" : "prefix"),
                metadata => metadata.Score.ShouldBe(page == 3 ? 0.6 : 0.8));
            result.Page.ShouldBe(page);
            result.PageSize.ShouldBe(1);
            result.TotalCount.ShouldBe(3);
            result.TotalPages.ShouldBe(3);
        }
    }

    /// <summary>
    /// Checks repeated tokens retain the existing distinct-match coverage calculation.
    /// </summary>
    /// <param name="semantic">Whether to exercise the semantic provider instead of the local fallback.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Search_RepeatedTokens_PreservesOriginalCoverage(bool semantic)
    {
        IPartySearchProvider provider = CreateProvider(semantic);
        PartyIndexEntry entry = new() { Id = "p1", Type = PartyType.Person, DisplayName = "Jean" };

        PagedResult<PartySearchResult> result = provider.Search([entry], "Jéan Jean", null, null, 1, 20);

        PartySearchResult match = result.Items.ShouldHaveSingleItem();
        match.RelevanceScore.ShouldBe(0.95, 0.000_001);
        match.Matches.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            metadata => metadata.MatchType.ShouldBe("exact"),
            metadata => metadata.Score.ShouldBe(1.0));
    }

    /// <summary>
    /// Checks partial coverage uses the original count for queries with more than two tokens.
    /// </summary>
    /// <param name="semantic">Whether to exercise the semantic provider instead of the local fallback.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Search_ThreeTokenPartialMatch_PreservesOriginalCoverage(bool semantic)
    {
        IPartySearchProvider provider = CreateProvider(semantic);
        PartyIndexEntry entry = new() { Id = "p1", Type = PartyType.Person, DisplayName = "123" };

        PagedResult<PartySearchResult> result = provider.Search([entry], "123 456 789", null, null, 1, 20);

        PartySearchResult match = result.Items.ShouldHaveSingleItem();
        match.RelevanceScore.ShouldBe(0.933333333, 0.000_001);
        match.Matches.ShouldHaveSingleItem().MatchType.ShouldBe("exact");
    }

    /// <summary>
    /// Checks individual fuzzy tokens remain enabled while synthetic phrase fuzzy matching stays disabled.
    /// </summary>
    /// <param name="semantic">Whether to exercise the semantic provider instead of the local fallback.</param>
    /// <param name="query">The misspelled query to search for.</param>
    /// <param name="displayName">The candidate party name.</param>
    /// <param name="expectMatch">Whether individual tokens should match the candidate.</param>
    [Theory]
    [InlineData(false, "Jena Dupnt", "Jean Dupont", true)]
    [InlineData(true, "Jena Dupnt", "Jean Dupont", true)]
    [InlineData(false, "Abcx Efgy", "Abcdefghij", false)]
    [InlineData(true, "Abcx Efgy", "Abcdefghij", false)]
    public void Search_MultiTokenQuery_PreservesTokenFuzzyAndPhraseSuppression(
        bool semantic,
        string query,
        string displayName,
        bool expectMatch)
    {
        IPartySearchProvider provider = CreateProvider(semantic);
        PartyIndexEntry entry = new() { Id = "p1", Type = PartyType.Person, DisplayName = displayName };

        PagedResult<PartySearchResult> result = provider.Search([entry], query, null, null, 1, 20);

        if (expectMatch)
        {
            PartySearchResult match = result.Items.ShouldHaveSingleItem();
            match.RelevanceScore.ShouldBe(0.46, 0.000_001);
            match.Matches.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                metadata => metadata.MatchType.ShouldBe("fuzzy"),
                metadata => metadata.Score.ShouldBe(0.4));
        }
        else
        {
            result.Items.ShouldBeEmpty();
        }
    }

    /// <summary>
    /// Checks a query containing only combining marks remains an empty search.
    /// </summary>
    /// <param name="semantic">Whether to exercise the semantic provider instead of the local fallback.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Search_NormalizationRemovesAllTokens_ReturnsEmpty(bool semantic)
    {
        IPartySearchProvider provider = CreateProvider(semantic);
        PartyIndexEntry entry = new() { Id = "p1", Type = PartyType.Person, DisplayName = "Jean" };

        PagedResult<PartySearchResult> result = provider.Search([entry], "\u0301\u0300", null, null, 1, 20);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    /// <summary>
    /// Checks the semantic provider retains normalized phrase matching on structured fields.
    /// </summary>
    [Fact]
    public void Search_SemanticMultiTokenQuery_PreservesStructuredFieldPhraseMatching()
    {
        SemanticPartySearchProvider provider = new();
        PartyIndexEntry entry = new()
        {
            Id = "p1",
            Type = PartyType.Person,
            DisplayName = "Unrelated",
            SearchableContactChannels =
            [
                new ContactChannel { Id = "c1", Type = ContactChannelType.Email, Value = "1234 5678" },
            ],
        };

        PagedResult<PartySearchResult> result = provider.Search([entry], "1234\t5678", null, null, 1, 20);

        PartySearchResult match = result.Items.ShouldHaveSingleItem();
        match.RelevanceScore.ShouldBe(0.96, 0.000_001);
        match.Matches.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            metadata => metadata.MatchedField.ShouldBe("email"),
            metadata => metadata.MatchType.ShouldBe("exact"),
            metadata => metadata.Score.ShouldBe(0.9));
    }

    /// <summary>
    /// Checks semantic coverage combines distinct fields and retains weighted metadata ordering.
    /// </summary>
    [Fact]
    public void Search_SemanticThreeTokenQuery_PreservesCrossFieldCoverageAndMetadata()
    {
        SemanticPartySearchProvider provider = new();
        PartyIndexEntry entry = new()
        {
            Id = "p1",
            Type = PartyType.Person,
            DisplayName = "123",
            SearchableContactChannels =
            [
                new ContactChannel { Id = "c1", Type = ContactChannelType.Email, Value = "456" },
            ],
            SearchableIdentifiers =
            [
                new PartyIdentifier { Id = "i1", Type = IdentifierType.VAT, Value = "789" },
            ],
        };

        PagedResult<PartySearchResult> result = provider.Search([entry], "123 456 789", null, null, 1, 20);

        PartySearchResult match = result.Items.ShouldHaveSingleItem();
        match.RelevanceScore.ShouldBe(0.98, 0.000_001);
        match.Matches.Select(m => m.MatchedField).ShouldBe(["displayName", "email", "identifier"]);
        match.Matches.Select(m => m.Score).ShouldBe([1.0, 0.9, 0.8]);
        match.Matches.ShouldAllBe(m => m.MatchType == "exact");
    }

    /// <summary>
    /// Checks query-length allocation growth stays bounded independently of entry count.
    /// </summary>
    /// <param name="semantic">Whether to exercise the semantic provider instead of the local fallback.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Search_10KEntries_LongerQueryDoesNotAllocatePerEntryQueryCopies(bool semantic)
    {
        IPartySearchProvider provider = CreateProvider(semantic);
        PartyIndexEntry[] entries = Enumerable.Range(0, 10_000)
            .Select(i => new PartyIndexEntry { Id = $"p{i}", Type = PartyType.Person, DisplayName = "Unrelated" })
            .ToArray();
        const string shortQuery = "123 456";
        string longQuery = $"{new string('1', 64)} {new string('2', 64)}";

        // Digits disable fuzzy work; both queries return no results. Warm both paths before
        // comparing allocations so query length is the only changing input during measurement.
        _ = provider.Search(entries, shortQuery, null, null, 1, 20);
        _ = provider.Search(entries, longQuery, null, null, 1, 20);

        long before = GC.GetAllocatedBytesForCurrentThread();
        PagedResult<PartySearchResult> shortResult = provider.Search(entries, shortQuery, null, null, 1, 20);
        long shortBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        PagedResult<PartySearchResult> longResult = provider.Search(entries, longQuery, null, null, 1, 20);
        long longBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        output.WriteLine($"10K entries: short query {shortBytes} bytes; long query {longBytes} bytes; growth {longBytes - shortBytes} bytes");
        shortResult.TotalCount.ShouldBe(0);
        longResult.TotalCount.ShouldBe(0);
        // Query-sized setup is allowed. Copying the extra 122 characters for each entry
        // would add over 2 MB; 64 KiB leaves generous room for constant setup overhead.
        (longBytes - shortBytes).ShouldBeLessThan(64 * 1024);
    }

    /// <summary>
    /// Checks candidate-array allocation does not grow with entry count when token count increases.
    /// </summary>
    /// <param name="semantic">Whether to exercise the semantic provider instead of the local fallback.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Search_10KEntries_MoreTokensDoesNotAllocatePerEntryCandidateArrays(bool semantic)
    {
        IPartySearchProvider provider = CreateProvider(semantic);
        // Empty names short-circuit matching so per-token fuzzy work cannot affect this
        // measurement of query preparation. No channels or identifiers are present either.
        PartyIndexEntry[] entries = Enumerable.Range(0, 10_000)
            .Select(i => new PartyIndexEntry { Id = $"p{i}", Type = PartyType.Person, DisplayName = string.Empty })
            .ToArray();
        const string shortQuery = "123 456";
        string manyTokenQuery = string.Join(' ', Enumerable.Repeat("123", 32));

        _ = provider.Search(entries, shortQuery, null, null, 1, 20);
        _ = provider.Search(entries, manyTokenQuery, null, null, 1, 20);

        long before = GC.GetAllocatedBytesForCurrentThread();
        PagedResult<PartySearchResult> shortResult = provider.Search(entries, shortQuery, null, null, 1, 20);
        long shortBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        PagedResult<PartySearchResult> manyTokenResult = provider.Search(entries, manyTokenQuery, null, null, 1, 20);
        long manyTokenBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        output.WriteLine($"10K entries: two tokens {shortBytes} bytes; 32 tokens {manyTokenBytes} bytes; growth {manyTokenBytes - shortBytes} bytes");
        shortResult.TotalCount.ShouldBe(0);
        manyTokenResult.TotalCount.ShouldBe(0);
        // Thirty extra candidate references per entry would allocate over 2 MB even if
        // the joined phrase were already cached. Constant per-query setup stays far below this.
        (manyTokenBytes - shortBytes).ShouldBeLessThan(64 * 1024);
    }

    private static IPartySearchProvider CreateProvider(bool semantic)
        => semantic ? new SemanticPartySearchProvider() : new LocalFuzzyPartySearchProvider();
}
