using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Search;

/// <summary>
/// Pluggable search provider abstraction for party search.
/// Default implementation uses enhanced token-based fuzzy matching over DAPR actor state.
/// Can be swapped for Elasticsearch/OpenSearch in v2 via DI registration.
/// </summary>
public interface IPartySearchProvider
{
    PagedResult<PartySearchResult> Search(
        IEnumerable<PartyIndexEntry> entries,
        string query,
        PartyType? typeFilter,
        bool? activeFilter,
        int page,
        int pageSize);
}
