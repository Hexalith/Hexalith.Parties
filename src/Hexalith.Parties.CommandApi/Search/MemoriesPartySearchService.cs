using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.CommandApi.Search;

internal sealed class MemoriesPartySearchService(MemoriesClient memoriesClient) : IPartySearchService
{
    public async Task<PartySearchResponse> SearchAsync(
        PartySearchRequest request,
        IEnumerable<PartyIndexEntry> entries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(entries);

        IReadOnlyList<MemoryCandidate> candidates;
        PartySearchExecutionStatus status = PartySearchExecutionStatus.Rich;
        string? degradedReason = null;

        switch (request.Mode)
        {
            case PartySearchMode.Lexical:
                SearchResult lexical = await memoriesClient.SearchAsync(
                    new SearchRequest(request.TenantId, "syntactic", request.Query, request.CaseId, request.PageSize, Explain: true),
                    cancellationToken).ConfigureAwait(false);
                candidates = [.. lexical.Results.Select(ToCandidate)];
                if (lexical.Degraded)
                {
                    status = PartySearchExecutionStatus.Degraded;
                    degradedReason = BuildDegradedReason(lexical.UnavailableAxes);
                }

                break;

            case PartySearchMode.Semantic:
                SearchResult semantic = await memoriesClient.SearchAsync(
                    new SearchRequest(request.TenantId, "semantic", request.Query, request.CaseId, request.PageSize, Explain: true),
                    cancellationToken).ConfigureAwait(false);
                candidates = [.. semantic.Results.Select(ToCandidate)];
                if (semantic.Degraded)
                {
                    status = PartySearchExecutionStatus.Degraded;
                    degradedReason = BuildDegradedReason(semantic.UnavailableAxes);
                }

                break;

            case PartySearchMode.Graph:
                string startNodeId = request.GraphContextMemoryUnitId ?? request.GraphContextPartyId ?? request.Query;
                TraversalResult traversal = await memoriesClient.TraverseAsync(
                    request.TenantId,
                    startNodeId,
                    depth: 2,
                    caseId: request.CaseId,
                    ct: cancellationToken).ConfigureAwait(false);
                candidates = [.. traversal.Nodes.Select(ToCandidate)];
                if (traversal.Degraded)
                {
                    status = PartySearchExecutionStatus.Degraded;
                    degradedReason = BuildDegradedReason(traversal.UnavailableAxes);
                }

                break;

            default:
                HybridSearchResult hybrid = await memoriesClient.HybridSearchAsync(
                    new HybridSearchRequest(request.TenantId, request.Query, request.CaseId, request.PageSize, Explain: true),
                    cancellationToken).ConfigureAwait(false);
                candidates = [.. hybrid.Results.Select(ToCandidate)];
                if (hybrid.Degraded)
                {
                    status = PartySearchExecutionStatus.Degraded;
                    degradedReason = BuildDegradedReason(hybrid.UnavailableAxes);
                }

                break;
        }

        return Hydrate(request, entries, candidates, status, degradedReason);
    }

    private static PartySearchResponse Hydrate(
        PartySearchRequest request,
        IEnumerable<PartyIndexEntry> entries,
        IReadOnlyList<MemoryCandidate> candidates,
        PartySearchExecutionStatus status,
        string? degradedReason)
    {
        Dictionary<string, PartyIndexEntry> entriesById = entries
            .Where(e => !e.IsErased)
            .ToDictionary(e => e.Id, StringComparer.Ordinal);

        List<HydratedCandidate> hydrated = [];
        foreach (MemoryCandidate candidate in candidates)
        {
            if (!TryExtractPartyId(candidate.SourceUri, out string tenantId, out string partyId)
                || !string.Equals(tenantId, request.TenantId, StringComparison.Ordinal)
                || (candidate.CaseId is not null && request.CaseId is not null && !string.Equals(candidate.CaseId, request.CaseId, StringComparison.Ordinal))
                || request.AuthorizedPartyIds is not null && !request.AuthorizedPartyIds.Contains(partyId)
                || !entriesById.TryGetValue(partyId, out PartyIndexEntry? entry)
                || request.TypeFilter is not null && entry.Type != request.TypeFilter.Value
                || request.ActiveFilter is not null && entry.IsActive != request.ActiveFilter.Value)
            {
                continue;
            }

            hydrated.Add(new HydratedCandidate(entry, candidate));
        }

        List<HydratedCandidate> collapsed =
        [
            .. hydrated
                .GroupBy(h => h.Entry.Id, StringComparer.Ordinal)
                .Select(g => g.OrderByDescending(h => h.Candidate.CompositeScore ?? h.Candidate.Score ?? 0).First())
                .OrderByDescending(h => h.Candidate.CompositeScore ?? h.Candidate.Score ?? 0)
                .ThenBy(h => h.Entry.DisplayName, StringComparer.OrdinalIgnoreCase),
        ];

        List<PartySearchResult> items =
        [
            .. collapsed
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(ToSearchResult),
        ];

        int totalCount = collapsed.Count;
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / request.PageSize);

        return new PartySearchResponse(
            new PagedResult<PartySearchResult>
            {
                Items = items,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
            },
            status,
            degradedReason,
            [.. collapsed.Select(ToScoreMetadata)],
            [.. collapsed.Select(ToSourceMetadata)]);
    }

    private static PartySearchResult ToSearchResult(HydratedCandidate hydrated)
    {
        double score = hydrated.Candidate.CompositeScore ?? hydrated.Candidate.Score ?? 0;
        return new PartySearchResult
        {
            Party = hydrated.Entry,
            RelevanceScore = score,
            Matches =
            [
                new MatchMetadata
                {
                    MatchedField = "memories",
                    MatchType = hydrated.Candidate.Axis,
                    Score = score,
                },
            ],
        };
    }

    private static PartySearchScoreMetadata ToScoreMetadata(HydratedCandidate hydrated)
        => new(
            hydrated.Entry.Id,
            hydrated.Candidate.Score,
            hydrated.Candidate.SyntacticScore,
            hydrated.Candidate.SemanticScore,
            hydrated.Candidate.GraphScore,
            hydrated.Candidate.CompositeScore);

    private static PartySearchSourceMetadata ToSourceMetadata(HydratedCandidate hydrated)
        => new(
            hydrated.Entry.Id,
            "Hexalith.Memories",
            hydrated.Candidate.SourceUri,
            hydrated.Candidate.MemoryUnitId,
            SourceType.Event.ToString());

    private static MemoryCandidate ToCandidate(FusedScoredResult result)
        => new(
            result.MemoryUnitId,
            result.SourceUri,
            result.CaseId,
            "hybrid",
            Score: null,
            result.SyntacticScore,
            result.SemanticScore,
            result.GraphScore,
            result.CompositeScore);

    private static MemoryCandidate ToCandidate(ScoredResult result)
        => new(
            result.MemoryUnitId,
            result.SourceUri,
            result.CaseId,
            result.Axis ?? "single-axis",
            result.Score,
            SyntacticScore: string.Equals(result.Axis, "syntactic", StringComparison.OrdinalIgnoreCase) ? result.Score : null,
            SemanticScore: string.Equals(result.Axis, "semantic", StringComparison.OrdinalIgnoreCase) ? result.Score : null,
            GraphScore: string.Equals(result.Axis, "graph", StringComparison.OrdinalIgnoreCase) ? result.Score : null,
            CompositeScore: null);

    private static MemoryCandidate ToCandidate(TraversalNode node)
        => new(
            node.MemoryUnitId,
            node.SourceUri,
            CaseId: null,
            Axis: "graph",
            Score: 1.0 / Math.Max(1, node.HopDistance),
            SyntacticScore: null,
            SemanticScore: null,
            GraphScore: 1.0 / Math.Max(1, node.HopDistance),
            CompositeScore: null);

    private static bool TryExtractPartyId(string sourceUri, out string tenantId, out string partyId)
    {
        tenantId = string.Empty;
        partyId = string.Empty;

        string[] parts = sourceUri.Split(':', StringSplitOptions.None);
        if (parts.Length != 6
            || !string.Equals(parts[0], "urn", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[1], "hexalith", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[2], "parties", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[4], "party", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        tenantId = parts[3];
        partyId = parts[5];
        return !string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(partyId);
    }

    private static string BuildDegradedReason(IReadOnlyList<string>? unavailableAxes)
        => unavailableAxes is { Count: > 0 }
            ? $"Hexalith.Memories search degraded; unavailable axes: {string.Join(", ", unavailableAxes)}."
            : "Hexalith.Memories search degraded.";

    private sealed record MemoryCandidate(
        string MemoryUnitId,
        string SourceUri,
        string? CaseId,
        string Axis,
        double? Score,
        double? SyntacticScore,
        double? SemanticScore,
        double? GraphScore,
        double? CompositeScore);

    private sealed record HydratedCandidate(PartyIndexEntry Entry, MemoryCandidate Candidate);
}
