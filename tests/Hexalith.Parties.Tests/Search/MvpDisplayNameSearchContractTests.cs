using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Search;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

// ATDD tests for Story 2.5 — Search Parties by Display Name with Match Metadata.
// Test-design risk references: R-06 SortName additive contract, R-08 MVP search scope creep,
// R-21 pagination tie-break determinism.
public sealed class MvpDisplayNameSearchContractTests
{
    // AC2/AC4 — Exact display-name match emits MatchedField="displayName" / MatchType="exact" and
    // the MVP PartySearch path never emits future-reserved fields (email/contactChannel/identifier/
    // type/semantic/graph/duplicate). Reference: 2.5-UNIT-070, 2.5-FIT-072.
    [Fact]
    public void Search_MvpExactDisplayName_OnlyEmitsDisplayNameMatchedField()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();

        PagedResult<PartySearchResult> result = provider.Search(entries, "Jean Dupont", null, null, 1, 20);

        PartySearchResult exact = result.Items.ShouldHaveSingleItem();
        exact.Matches.ShouldAllBe(m => m.MatchedField == "displayName");
        exact.Matches.ShouldContain(m => m.MatchType == "exact");
        HashSet<string> reservedFields = new(StringComparer.Ordinal)
        {
            "email", "contactChannel", "identifier", "type", "semantic", "graph", "duplicate", "partyType",
        };
        exact.Matches.ShouldNotContain(m => reservedFields.Contains(m.MatchedField));
    }

    // AC4 — MVP PartySearch must not emit contact-channel match metadata even when contact
    // values would match. The legacy LocalFuzzyPartySearchProvider currently broadens to contact
    // channels and identifiers; the MVP path must constrain that. Reference: 2.5-FIT-073.
    [Fact]
    public void Search_MvpContactValueQuery_DoesNotEmitContactChannelMatch()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();

        // A query that hits a contact channel value in the broader fuzzy provider must not
        // produce a contactChannel-typed match through the MVP PartySearch boundary.
        PagedResult<PartySearchResult> result = provider.Search(entries, "jean@example.com", null, null, 1, 20);

        result.Items.SelectMany(r => r.Matches)
              .ShouldAllBe(m => m.MatchedField != "contactChannel" && m.MatchedField != "email");
    }

    // AC4 — Identifier-only query must not yield a match in the MVP path.
    // Reference: 2.5-FIT-072 (future-field reservation).
    [Fact]
    public void Search_MvpIdentifierValueQuery_DoesNotEmitIdentifierMatch()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();

        PagedResult<PartySearchResult> result = provider.Search(entries, "synthetic-siret-value", null, null, 1, 20);

        result.Items.SelectMany(r => r.Matches)
              .ShouldAllBe(m => m.MatchedField != "identifier" && m.MatchedField != "type");
    }

    // AC3 — Prefix matches must rank below exact, and tie-break deterministically by normalized
    // display name then party id. Reference: 2.5-UNIT-071, 2.5-UNIT-200 (R-21 closes Story 2.2 defer).
    [Fact]
    public void Search_ExactAndPrefixCollision_RanksExactAheadOfPrefixThenByIdTieBreaker()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        List<PartyIndexEntry> entries =
        [
            Entry("p-b", "Acme"),
            Entry("p-a", "Acme"),
            Entry("p-prefix", "Acme Corporation"),
            Entry("p-contains", "The Acme Trust"),
        ];

        PagedResult<PartySearchResult> result = provider.Search(entries, "Acme", null, null, 1, 20);

        result.Items.Select(static r => r.Party.Id).ShouldBe(["p-a", "p-b", "p-prefix", "p-contains"]);
        result.Items.Select(static r => r.Matches.Single().MatchType).ShouldBe(["exact", "exact", "prefix", "contains"]);
    }

    // AC5 — Empty/whitespace queries return bounded empty result without leaking metadata or
    // probing cross-tenant state. Reference: 2.5-UNIT-052.
    [Fact]
    public async Task SearchAsync_WhitespaceQuery_ReturnsBoundedEmptyResult()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        var service = new LocalPartySearchService(provider);
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest(
                TenantId: "tenant-a",
                Query: "   ",
                Mode: PartySearchMode.Hybrid,
                TypeFilter: null,
                ActiveFilter: null,
                Page: 1,
                PageSize: 20,
                AuthorizedPartyIds: entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal)),
            entries,
            CancellationToken.None);

        response.Results.Items.ShouldBeEmpty();
        response.Results.TotalCount.ShouldBe(0);
        response.ScoreMetadata.ShouldBeEmpty();
        response.SourceMetadata.ShouldBeEmpty();
    }

    // AC6 — Erased entries must be excluded before match/score/source/page metadata is computed.
    // Reference: 2.5-GTW-082 / 2.6-INT-033..034 (cross-cutting with Story 2.6).
    //
    // P10: Strengthened assertion. Adds a non-erased twin entry sharing the same display name
    // so TotalCount=1 (matched-and-non-erased) proves the erased entry was excluded BEFORE the
    // matching/metadata calculation, not coincidentally after. The previous assertion
    // `TotalCount.ShouldNotBe(entries.Count)` passed trivially when the result set was empty.
    [Fact]
    public async Task SearchAsync_ErasedEntryInScope_ExcludedBeforeMetadataCalculation()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        var service = new LocalPartySearchService(provider);
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();
        string sharedDisplayName = entries[0].DisplayName;
        string erasedId = entries[0].Id;
        // Mark the original entry as erased and add a non-erased twin sharing the same display
        // name. The query must surface exactly the non-erased twin.
        entries[0] = entries[0] with { IsErased = true };
        PartyIndexEntry twin = entries[0] with { Id = "p-twin", IsErased = false };
        entries.Add(twin);

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest(
                TenantId: "tenant-a",
                Query: sharedDisplayName,
                Mode: PartySearchMode.Hybrid,
                TypeFilter: null,
                ActiveFilter: null,
                Page: 1,
                PageSize: 20,
                AuthorizedPartyIds: entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal)),
            entries,
            CancellationToken.None);

        response.Results.Items.ShouldNotContain(r => r.Party.Id == erasedId);
        response.Results.Items.ShouldContain(r => r.Party.Id == twin.Id);
        response.Results.TotalCount.ShouldBe(1);
    }

    // AC6 — Cancellation must be terminal: no fallback aggregate replay, Memories expansion, or
    // retry after the token fires. Reference: 2.5-UNIT-050 (cancellation propagation).
    [Fact]
    public async Task SearchAsync_CancellationRequested_PropagatesWithoutFallbackWork()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        var service = new LocalPartySearchService(provider);
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        PartySearchRequest request = new(
            TenantId: "tenant-a",
            Query: "Jean",
            Mode: PartySearchMode.Hybrid,
            TypeFilter: null,
            ActiveFilter: null,
            Page: 1,
            PageSize: 20,
            AuthorizedPartyIds: entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal));

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.SearchAsync(request, entries, cts.Token));
    }

    // AC2/AC4 — Match metadata MatchType allowlist for the MVP path: {exact, prefix, contains}
    // plus optional {fuzzy} for display-name-only fuzzy. Anything else is a contract violation.
    // Reference: 2.5-FIT-072 (allowlist), 2.5-FIT-073 (provider reachability).
    [Fact]
    public void Search_MvpResults_OnlyEmitAllowedMatchTypeVocabulary()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();
        HashSet<string> allowedMatchTypes = new(StringComparer.Ordinal) { "exact", "prefix", "contains", "fuzzy" };

        PagedResult<PartySearchResult> result = provider.Search(entries, "Acme", null, null, 1, 20);

        IEnumerable<MatchMetadata> allMatches = result.Items.SelectMany(r => r.Matches);
        allMatches.ShouldAllBe(m => allowedMatchTypes.Contains(m.MatchType));
    }

    // P6 — Fuzzy display-name matches MUST rank below exact/prefix/contains matches. The spec's
    // Required Test Matrix row "Optional fuzzy display-name match" calls for this ordering proof
    // explicitly. Without this, a future score-table tweak could silently invert the ranking.
    [Fact]
    public void Search_FuzzyMatchAgainstExactPrefixContains_RanksFuzzyLast()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        List<PartyIndexEntry> entries =
        [
            Entry("p-exact", "Acme"),
            Entry("p-prefix", "Acme Corporation"),
            Entry("p-contains", "The Acme Trust"),
            // Jaro-Winkler similarity("Acme", "Acne") >= 0.85 — produces a fuzzy match candidate.
            Entry("p-fuzzy", "Acne"),
        ];

        PagedResult<PartySearchResult> result = provider.Search(entries, "Acme", null, null, 1, 20);

        result.Items.Select(static r => r.Party.Id).ShouldBe(["p-exact", "p-prefix", "p-contains", "p-fuzzy"]);
        result.Items[^1].Matches.ShouldContain(m => m.MatchType == "fuzzy" && m.MatchedField == "displayName");
        // The fuzzy result must not introduce any future-reserved match metadata.
        result.Items[^1].Matches.ShouldAllBe(m => m.MatchedField == "displayName");
    }

    // P7 — Digit-heavy tokens must not trigger fuzzy matching. The provider short-circuits at
    // `if (token.Any(char.IsDigit)) return false;` to avoid Jaro-Winkler false positives between
    // identifier-like strings (e.g., Entry-50000 vs. Entry-10000). Pins that contract.
    [Theory]
    [InlineData("Entry-50000", "Entry-10000")]
    [InlineData("AB-99-XY", "AB-11-XY")]
    public void Search_DigitHeavyTokens_DoNotProduceFuzzyMatch(string queryDisplayName, string entryDisplayName)
    {
        var provider = new LocalFuzzyPartySearchProvider();
        List<PartyIndexEntry> entries = [Entry("p-target", entryDisplayName)];

        PagedResult<PartySearchResult> result = provider.Search(entries, queryDisplayName, null, null, 1, 20);

        result.Items.ShouldBeEmpty();
    }

    private static PartyIndexEntry Entry(string id, string displayName)
        => new()
        {
            Id = id,
            Type = PartyType.Organization,
            IsActive = true,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        };
}
