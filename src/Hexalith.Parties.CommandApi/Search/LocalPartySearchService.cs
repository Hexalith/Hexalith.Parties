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

        PagedResult<PartySearchResult> results = localSearchProvider.Search(
            entries.Where(e => !e.IsErased),
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
                SourceUri: $"urn:hexalith:parties:{request.TenantId}:party:{result.Party.Id}",
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
