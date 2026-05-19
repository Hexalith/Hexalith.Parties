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

// ATDD red-phase scaffold for Story 2.5 — Search Parties by Display Name with Match Metadata.
// Test-design risk references: R-06 SortName additive contract, R-08 MVP search scope creep,
// R-21 pagination tie-break determinism.
// Each [Fact(Skip = "...")] is intentionally skipped until the dev-story activates it.
public sealed class MvpDisplayNameSearchContractTests
{
    // AC2/AC4 — Exact display-name match emits MatchedField="displayName" / MatchType="exact" and
    // the MVP PartySearch path never emits future-reserved fields (email/contactChannel/identifier/
    // type/semantic/graph/duplicate). Reference: 2.5-UNIT-070, 2.5-FIT-072.
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.5 / R-08 — activate in dev-story")]
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
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.5 / R-08 — activate in dev-story")]
    public void Search_MvpContactValueQuery_DoesNotEmitContactChannelMatch()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();

        // A query that hits a contact channel value in the broader fuzzy provider must not
        // produce a contactChannel-typed match through the MVP PartySearch boundary.
        PagedResult<PartySearchResult> result = provider.Search(entries, "jean.dupont@example.test", null, null, 1, 20);

        result.Items.SelectMany(r => r.Matches)
              .ShouldAllBe(m => m.MatchedField != "contactChannel" && m.MatchedField != "email");
    }

    // AC4 — Identifier-only query must not yield a match in the MVP path.
    // Reference: 2.5-FIT-072 (future-field reservation).
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.5 / R-08 — activate in dev-story")]
    public void Search_MvpIdentifierValueQuery_DoesNotEmitIdentifierMatch()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();

        PagedResult<PartySearchResult> result = provider.Search(entries, "SIREN-12345", null, null, 1, 20);

        result.Items.SelectMany(r => r.Matches)
              .ShouldAllBe(m => m.MatchedField != "identifier" && m.MatchedField != "type");
    }

    // AC3 — Prefix matches must rank below exact, and tie-break deterministically by normalized
    // display name then party id. Reference: 2.5-UNIT-071, 2.5-UNIT-200 (R-21 closes Story 2.2 defer).
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.5 / R-21 — activate in dev-story")]
    public void Search_ExactAndPrefixCollision_RanksExactAheadOfPrefixThenByIdTieBreaker()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();

        PagedResult<PartySearchResult> result = provider.Search(entries, "Acme", null, null, 1, 20);

        List<PartySearchResult> ordered = result.Items.ToList();
        // Exact must precede prefix in ordering
        int firstPrefixIndex = ordered.FindIndex(r => r.Matches.Any(m => m.MatchType == "prefix"));
        int firstExactIndex = ordered.FindIndex(r => r.Matches.Any(m => m.MatchType == "exact"));
        if (firstExactIndex >= 0 && firstPrefixIndex >= 0)
        {
            firstExactIndex.ShouldBeLessThan(firstPrefixIndex);
        }

        // Same match strength must order by party id ascending as the final tie-breaker.
        IEnumerable<IGrouping<string, PartySearchResult>> sameStrengthGroups = ordered
            .GroupBy(r => string.Join(",", r.Matches.Select(m => m.MatchType).OrderBy(t => t)))
            .Where(g => g.Count() > 1);
        foreach (IGrouping<string, PartySearchResult> group in sameStrengthGroups)
        {
            IReadOnlyList<string> ids = group.Select(g => g.Party.Id).ToList();
            ids.ShouldBe(ids.OrderBy(id => id, StringComparer.Ordinal).ToList());
        }
    }

    // AC5 — Empty/whitespace queries return bounded empty result without leaking metadata or
    // probing cross-tenant state. Reference: 2.5-UNIT-052.
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.5 — activate in dev-story")]
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
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.5 / R-04 — activate in dev-story")]
    public async Task SearchAsync_ErasedEntryInScope_ExcludedBeforeMetadataCalculation()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        var service = new LocalPartySearchService(provider);
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();
        // Mark one entry as erased; the search must exclude it before TotalCount calculation.
        entries[0] = entries[0] with { IsErased = true };

        PartySearchResponse response = await service.SearchAsync(
            new PartySearchRequest(
                TenantId: "tenant-a",
                Query: entries[0].DisplayName,
                Mode: PartySearchMode.Hybrid,
                TypeFilter: null,
                ActiveFilter: null,
                Page: 1,
                PageSize: 20,
                AuthorizedPartyIds: entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal)),
            entries,
            CancellationToken.None);

        response.Results.Items.ShouldNotContain(r => r.Party.Id == entries[0].Id);
        response.Results.TotalCount.ShouldNotBe(entries.Count); // erased excluded from TotalCount
    }

    // AC6 — Cancellation must be terminal: no fallback aggregate replay, Memories expansion, or
    // retry after the token fires. Reference: 2.5-UNIT-050 (cancellation propagation).
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.5 / R-15 — activate in dev-story")]
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
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.5 / R-08 — activate in dev-story")]
    public void Search_MvpResults_OnlyEmitAllowedMatchTypeVocabulary()
    {
        var provider = new LocalFuzzyPartySearchProvider();
        List<PartyIndexEntry> entries = PartyTestData.CreateSearchScenarioEntries();
        HashSet<string> allowedMatchTypes = new(StringComparer.Ordinal) { "exact", "prefix", "contains", "fuzzy" };

        PagedResult<PartySearchResult> result = provider.Search(entries, "Acme", null, null, 1, 20);

        IEnumerable<MatchMetadata> allMatches = result.Items.SelectMany(r => r.Matches);
        allMatches.ShouldAllBe(m => allowedMatchTypes.Contains(m.MatchType));
    }
}
