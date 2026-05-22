using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Search;

/// <summary>
/// Pluggable party search provider abstraction.
/// </summary>
/// <remarks>
/// The MVP provider performs local display-name matching only. Semantic, graph, hybrid, email,
/// and identifier search are reserved for future compatibility and require an explicit future
/// provider; this contracts package must not require a semantic backend, embedding model,
/// vector store, graph provider, temporal database, or infrastructure dependency.
/// </remarks>
public interface IPartySearchProvider
{
    PagedResult<PartySearchResult> Search(
        IEnumerable<PartyIndexEntry> entries,
        string query,
        PartyType? typeFilter,
        bool? activeFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
