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

    // P12: List sort tie-break uses normalized display name then party id — unified with the
    // search provider's tie-break per spec ("normalized display name, then party id"). The
    // legacy SortName property is no longer consulted by the list path; the search and list
    // paths now produce identical ordering for the same dataset.
    [Fact]
    public void BuildPagedList_SortsByNormalizedDisplayName_IgnoringSortNameProperty()
    {
        List<PartyIndexEntry> entries =
        [
            new PartyIndexEntry
            {
                Id = "p-alpha-display",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Alpha Zed",
                // SortName is set to a value that would order this entry SECOND under the legacy
                // SortName-based comparator. Under the new contract it must be ignored.
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

        // "Alpha Zed" < "Zed Alpha" by normalized display name, regardless of SortName.
        result.Items.Select(e => e.Id).ShouldBe(["p-alpha-display", "p-zed-display"]);
    }

    [Fact]
    public void BuildPagedList_FiltersErasedTypeActiveAndDateRangesBeforeMetadata()
    {
        List<PartyIndexEntry> entries =
        [
            Entry("p-alpha", "Alpha Person", PartyType.Person, active: true, "2026-05-03T00:00:00Z", "2026-05-06T00:00:00Z"),
            Entry("p-beta", "Beta Person", PartyType.Person, active: true, "2026-05-04T00:00:00Z", "2026-05-07T00:00:00Z"),
            Entry("p-inactive", "Inactive Person", PartyType.Person, active: false, "2026-05-04T00:00:00Z", "2026-05-07T00:00:00Z"),
            Entry("p-org", "Org", PartyType.Organization, active: true, "2026-05-04T00:00:00Z", "2026-05-07T00:00:00Z"),
            Entry("p-created-before", "Created Before", PartyType.Person, active: true, "2026-04-30T00:00:00Z", "2026-05-07T00:00:00Z"),
            Entry("p-modified-after", "Modified After", PartyType.Person, active: true, "2026-05-04T00:00:00Z", "2026-06-01T00:00:00Z"),
            Entry("p-erased", "Erased Person", PartyType.Person, active: true, "2026-05-04T00:00:00Z", "2026-05-07T00:00:00Z") with { IsErased = true },
        ];

        PagedResult<PartyIndexEntry> result = PartySearchResultsBuilder.BuildPagedList(
            entries,
            PartyType.Person,
            activeFilter: true,
            createdAfter: DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            createdBefore: DateTimeOffset.Parse("2026-05-31T00:00:00Z"),
            modifiedAfter: DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            modifiedBefore: DateTimeOffset.Parse("2026-05-31T00:00:00Z"),
            page: 2,
            pageSize: 1);

        result.Items.Select(static e => e.Id).ShouldBe(["p-beta"]);
        result.TotalCount.ShouldBe(2);
        result.TotalPages.ShouldBe(2);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-10, -5, 1, 1)]
    [InlineData(1, 500, 1, 100)]
    public void BuildPagedList_NormalizesPageBounds(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        PagedResult<PartyIndexEntry> result = PartySearchResultsBuilder.BuildPagedList(
            [Entry("p-1", "Alpha Person", PartyType.Person, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z")],
            null,
            null,
            page,
            pageSize);

        result.Page.ShouldBe(expectedPage);
        result.PageSize.ShouldBe(expectedPageSize);
        result.TotalCount.ShouldBe(1);
        result.TotalPages.ShouldBe(1);
    }

    [Fact]
    public void BuildPagedList_UsesOverflowSafeSkipForLargePage()
    {
        PagedResult<PartyIndexEntry> result = PartySearchResultsBuilder.BuildPagedList(
            [Entry("p-1", "Alpha Person", PartyType.Person, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z")],
            null,
            null,
            int.MaxValue,
            100);

        result.Items.ShouldBeEmpty();
        result.Page.ShouldBe(int.MaxValue);
        result.TotalCount.ShouldBe(1);
    }

    // P7: Party-Mode clarification requires combined created/modified ranges to be test-pinned.
    // Each range constrains a different timestamp; an entry must satisfy both ranges to be returned.
    // Also exercises one-sided ranges (createdBefore null, modifiedAfter null) by composing two
    // half-open intervals across different columns.
    [Fact]
    public void BuildPagedList_AppliesCombinedCreatedAndModifiedRangeFiltersTogether()
    {
        List<PartyIndexEntry> entries =
        [
            // Satisfies BOTH ranges: created in [May 1, May 31], modified in [May 5, May 31].
            Entry("p-in-both-ranges", "Charlie Person", PartyType.Person, active: true, "2026-05-10T00:00:00Z", "2026-05-15T00:00:00Z"),
            // Satisfies created range only: modified on May 1 falls before the modifiedAfter bound (May 5).
            Entry("p-modified-too-early", "Modified Early", PartyType.Person, active: true, "2026-05-10T00:00:00Z", "2026-05-01T00:00:00Z"),
            // Satisfies modified range only: created on April 25 falls before the createdAfter bound (May 1).
            Entry("p-created-too-early", "Created Early", PartyType.Person, active: true, "2026-04-25T00:00:00Z", "2026-05-15T00:00:00Z"),
        ];

        PagedResult<PartyIndexEntry> result = PartySearchResultsBuilder.BuildPagedList(
            entries,
            typeFilter: null,
            activeFilter: null,
            createdAfter: DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            createdBefore: DateTimeOffset.Parse("2026-05-31T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            modifiedAfter: DateTimeOffset.Parse("2026-05-05T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            modifiedBefore: DateTimeOffset.Parse("2026-05-31T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            page: 1,
            pageSize: 20);

        result.Items.Select(static e => e.Id).ShouldBe(["p-in-both-ranges"]);
        result.TotalCount.ShouldBe(1);
    }

    // P7 (one-sided): Only createdAfter is specified; modifiedAfter, createdBefore, and
    // modifiedBefore are null. Entries created on or after the bound must be returned regardless
    // of LastModifiedAt.
    [Fact]
    public void BuildPagedList_AppliesOneSidedCreatedAfterRangeWithoutOtherDateBounds()
    {
        List<PartyIndexEntry> entries =
        [
            Entry("p-after-bound", "After Bound", PartyType.Person, active: true, "2026-05-10T00:00:00Z", "2024-01-01T00:00:00Z"),
            Entry("p-before-bound", "Before Bound", PartyType.Person, active: true, "2026-04-25T00:00:00Z", "2026-06-01T00:00:00Z"),
        ];

        PagedResult<PartyIndexEntry> result = PartySearchResultsBuilder.BuildPagedList(
            entries,
            typeFilter: null,
            activeFilter: null,
            createdAfter: DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            createdBefore: null,
            modifiedAfter: null,
            modifiedBefore: null,
            page: 1,
            pageSize: 20);

        result.Items.Select(static e => e.Id).ShouldBe(["p-after-bound"]);
        result.TotalCount.ShouldBe(1);
    }

    private static PartyIndexEntry Entry(
        string id,
        string displayName,
        PartyType type,
        bool active,
        string createdAt,
        string modifiedAt)
        => new()
        {
            Id = id,
            Type = type,
            IsActive = active,
            DisplayName = displayName,
            SortName = displayName,
            CreatedAt = DateTimeOffset.Parse(createdAt, System.Globalization.CultureInfo.InvariantCulture),
            LastModifiedAt = DateTimeOffset.Parse(modifiedAt, System.Globalization.CultureInfo.InvariantCulture),
        };
}
