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

        IReadOnlyList<PartySearchSourceMetadata> sources =
        [
            .. results.Items.Select(result => new PartySearchSourceMetadata(
                PartyId: result.Party.Id,
                SourceSystem: "Hexalith.Parties.LocalFallback",
                SourceUri: PartyMemoryUrn.Build(request.TenantId, result.Party.Id),
                MemoryUnitId: null,
                EventType: null)),
        ];

        return Task.FromResult(new PartySearchResponse(
            results,
            PartySearchExecutionStatus.LocalOnly,
            LocalFallbackReason,
            scores,
            sources));
    }
}
