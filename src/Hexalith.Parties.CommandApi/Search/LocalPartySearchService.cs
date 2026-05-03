using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;

namespace Hexalith.Parties.CommandApi.Search;

internal sealed class LocalPartySearchService(IPartySearchProvider localSearchProvider) : IPartySearchService
{
    public const string LocalFallbackReason =
        "Hexalith.Memories rich search is not configured; local display-name fallback was used.";

    public Task<PartySearchResponse> SearchAsync(
        PartySearchRequest request,
        IEnumerable<PartyIndexEntry> entries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(entries);

        cancellationToken.ThrowIfCancellationRequested();
        request = NormalizeRequest(request);

        // Apply the same gate the Memories path enforces: drop erased entries, then enforce
        // AuthorizedPartyIds when the caller scopes the request. Tenant/case filtering is
        // not the local provider's responsibility because the controller already passes a
        // tenant-scoped collection, but we still respect AuthorizedPartyIds so that local-mode
        // and Memories-mode behave identically for callers that pass it.
        IEnumerable<PartyIndexEntry> filtered = entries.Where(e => !e.IsErased);
        if (request.AuthorizedPartyIds is not null)
        {
            IReadOnlySet<string> authorized = request.AuthorizedPartyIds;
            filtered = filtered.Where(e => authorized.Contains(e.Id));
        }

        PagedResult<PartySearchResult> results = localSearchProvider.Search(
            filtered,
            request.Query,
            request.TypeFilter,
            request.ActiveFilter,
            request.Page,
            request.PageSize);

        // Score/Source metadata are aligned 1:1 with the current page (`results.Items`), not
        // with `results.TotalCount`. Consumers iterating these arrays must use them in lockstep
        // with `Items` and re-fetch them with each page request.
        IReadOnlyList<PartySearchScoreMetadata> scores =
        [
            .. results.Items.Select(result => new PartySearchScoreMetadata(
                PartyId: result.Party.Id,
                RelevanceScore: result.RelevanceScore,
                LexicalScore: null,
                SemanticScore: null,
                GraphScore: null,
                CompositeScore: null)),
        ];

        // Local fallback has no Memories memory unit. Emit a null SourceUri so callers do not
        // mistakenly follow the URN against Memories (which would 404). The SourceSystem
        // "Hexalith.Parties.LocalFallback" is the authoritative signal that the row came from
        // the local index, not from Memories.
        IReadOnlyList<PartySearchSourceMetadata> sources =
        [
            .. results.Items.Select(result => new PartySearchSourceMetadata(
                PartyId: result.Party.Id,
                SourceSystem: "Hexalith.Parties.LocalFallback",
                SourceUri: null,
                MemoryUnitId: null,
                EventType: null)),
        ];

        return Task.FromResult(new PartySearchResponse(
            results,
            PartySearchExecutionStatus.LocalOnly,
            DegradedReason: null,
            scores,
            sources));
    }

    private static PartySearchRequest NormalizeRequest(PartySearchRequest request)
    {
        if (request.AuthorizedPartyIds is null)
        {
            throw new InvalidOperationException("Party search requires an explicit AuthorizedPartyIds set.");
        }

        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        return request with { Page = page, PageSize = pageSize };
    }
}
