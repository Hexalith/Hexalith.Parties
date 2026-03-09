using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.CommandApi.Search;

/// <summary>
/// Backward-compatible search provider that delegates to existing
/// <see cref="PartySearchResultsBuilder"/> static methods.
/// DI registration can swap between this and <see cref="SemanticPartySearchProvider"/>.
/// </summary>
internal sealed class BasicPartySearchProvider : IPartySearchProvider
{
    public PagedResult<PartySearchResult> Search(
        IEnumerable<PartyIndexEntry> entries,
        string query,
        PartyType? typeFilter,
        bool? activeFilter,
        int page,
        int pageSize)
    {
        return PartySearchResultsBuilder.BuildSearchResults(entries, query, typeFilter, activeFilter, page, pageSize);
    }
}
