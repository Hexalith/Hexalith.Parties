using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.CommandApi.Search;

/// <summary>
/// Backward-compatible search provider that delegates to existing
/// <see cref="PartySearchResultsBuilder"/> static methods.
/// DI registration can swap between this and <see cref="LocalFuzzyPartySearchProvider"/>.
/// </summary>
internal sealed class BasicPartySearchProvider : IPartySearchProvider
{
    public PagedResult<PartySearchResult> Search(
        IEnumerable<PartyIndexEntry> entries,
        string query,
        PartyType? typeFilter,
        bool? activeFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // P29: Materialize so the downstream non-cancellable LINQ pipeline cannot redeem a
        // yield-iterator over a long actor-state collection. Cancellation cannot be observed
        // mid-pipeline because PartySearchResultsBuilder.BuildSearchResults uses static LINQ
        // helpers without a CT — but materializing under the boundary's CT check at least
        // bounds the duration where cancellation is unobservable to the pipeline runtime
        // rather than network round-trip time.
        IReadOnlyList<PartyIndexEntry> materialized = entries as IReadOnlyList<PartyIndexEntry> ?? [.. entries];
        cancellationToken.ThrowIfCancellationRequested();
        return PartySearchResultsBuilder.BuildSearchResults(materialized, query, typeFilter, activeFilter, page, pageSize);
    }
}
