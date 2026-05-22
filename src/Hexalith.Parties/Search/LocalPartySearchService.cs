using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;

namespace Hexalith.Parties.Search;

internal sealed class LocalPartySearchService(IPartySearchProvider localSearchProvider) : IPartySearchService
{
    internal const string UnsupportedCapabilityReason =
        "The requested party search capability is reserved for future compatibility and is not available in MVP.";

    // P41: The previously-declared `LocalFallbackReason` constant was dead code; AC4 was
    // reconciled in the third-pass review (spec line 532) to NOT populate `DegradedReason`
    // when the response is `Status=LocalOnly` (Memories integration disabled, not a runtime
    // outage). Runtime degrades populate the reason from `MemoriesPartySearchService`.

    public Task<PartySearchResponse> SearchAsync(
        PartySearchRequest request,
        IEnumerable<PartyIndexEntry> entries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(entries);

        cancellationToken.ThrowIfCancellationRequested();

        if (request.Mode != PartySearchMode.Lexical)
        {
            return Task.FromResult(UnsupportedCapability(request));
        }

        request = NormalizeRequest(request);

        // Materialize once at the boundary so the filter chain below does not re-enumerate a
        // yield-iterator (which can re-invoke RPCs against actor state). Subsequent `Where`
        // operators run against the in-memory list.
        IReadOnlyList<PartyIndexEntry> materialized = entries as IReadOnlyList<PartyIndexEntry> ?? [.. entries];

        // Apply the same gate the Memories path enforces: drop erased entries, then enforce
        // AuthorizedPartyIds when the caller scopes the request. Tenant/case filtering is
        // not the local provider's responsibility because the controller already passes a
        // tenant-scoped collection, but we still respect AuthorizedPartyIds so that local-mode
        // and Memories-mode behave identically for callers that pass it.
        IReadOnlyList<PartyIndexEntry> filtered;
        if (request.AuthorizedPartyIds is { } authorized)
        {
            // Re-key the authorized set into an Ordinal HashSet so comparison is symmetric
            // with internal id collections (which all use Ordinal). Callers passing an
            // OrdinalIgnoreCase set previously slipped through the Contains check then failed
            // the entriesById lookup with no diagnostic. Materializing the set with a known
            // comparer also de-aliases mutable caller state.
            // P26: Filter null/whitespace ids so a buggy caller passing [null, "p1"]
            // does not produce the silent drop the comment is trying to prevent.
            HashSet<string> authorizedById = new(StringComparer.Ordinal);
            foreach (string id in authorized)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    _ = authorizedById.Add(id);
                }
            }

            List<PartyIndexEntry> filteredList = [];
            foreach (PartyIndexEntry entry in materialized)
            {
                if (entry.IsErased)
                {
                    continue;
                }

                if (authorizedById.Contains(entry.Id))
                {
                    filteredList.Add(entry);
                }
            }

            filtered = filteredList;
        }
        else
        {
            List<PartyIndexEntry> filteredList = [];
            foreach (PartyIndexEntry entry in materialized)
            {
                if (!entry.IsErased)
                {
                    filteredList.Add(entry);
                }
            }

            filtered = filteredList;
        }

        PagedResult<PartySearchResult> results = localSearchProvider.Search(
            filtered,
            request.Query,
            request.TypeFilter,
            request.ActiveFilter,
            request.Page,
            request.PageSize,
            cancellationToken);

        // Score/Source metadata are aligned 1:1 with the current page (`results.Items`), not
        // with `results.TotalCount`. Consumers iterating these arrays must use them in lockstep
        // with `Items` and re-fetch them with each page request.
        IReadOnlyList<PartySearchScoreMetadata> scores =
        [
            .. results.Items.Select(result => new PartySearchScoreMetadata(
                PartyId: result.Party.Id,
                RelevanceScore: SanitizeScore(result.RelevanceScore),
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

    private static PartySearchResponse UnsupportedCapability(PartySearchRequest request)
    {
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        return new PartySearchResponse(
            new PagedResult<PartySearchResult>
            {
                Items = [],
                Page = page,
                PageSize = pageSize,
                TotalCount = 0,
                TotalPages = 1,
            },
            PartySearchExecutionStatus.Unsupported,
            UnsupportedCapabilityReason,
            ScoreMetadata: [],
            SourceMetadata: []);
    }

    private static PartySearchRequest NormalizeRequest(PartySearchRequest request)
    {
        if (request.AuthorizedPartyIds is null)
        {
            // ArgumentException maps cleanly to a 400 ProblemDetails via the global validation
            // handler; the previous InvalidOperationException surfaced as 500.
            throw new ArgumentException(
                "Party search requires an explicit AuthorizedPartyIds set.",
                nameof(request));
        }

        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        return request with { Page = page, PageSize = pageSize };
    }

    private static double? SanitizeScore(double score)
    {
        // Treat NaN/Infinity scores as missing to avoid non-deterministic ordering and
        // JsonSerializer exceptions mid-response.
        if (double.IsNaN(score) || double.IsInfinity(score))
        {
            return null;
        }

        return score;
    }
}
