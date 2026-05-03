using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Contracts.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.CommandApi.Search;

internal sealed class MemoriesPartySearchService(
    MemoriesClient memoriesClient,
    LocalPartySearchService localFallback,
    IOptionsMonitor<PartyMemorySearchOptions> options,
    ILogger<MemoriesPartySearchService> logger) : IPartySearchService
{
    private const string SyntacticAxis = "syntactic";
    private const string SemanticAxis = "semantic";
    private const string GraphAxis = "graph";
    private const string HybridAxis = "hybrid";

    public async Task<PartySearchResponse> SearchAsync(
        PartySearchRequest request,
        IEnumerable<PartyIndexEntry> entries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(entries);
        cancellationToken.ThrowIfCancellationRequested();

        // Materialize entries once so that fallback can reuse them without re-enumerating.
        IReadOnlyList<PartyIndexEntry> entryList = entries as IReadOnlyList<PartyIndexEntry> ?? [.. entries];

        PartyMemorySearchOptions current = options.CurrentValue;

        string requestedAxis = MapModeToAxis(request.Mode);
        if (!current.IsAxisEnabled(requestedAxis))
        {
            logger.LogInformation(
                "Memories axis {Axis} is disabled by configuration. Falling back to local display-name search for {TenantId}.",
                requestedAxis,
                request.TenantId);
            return await DegradeToLocalAsync(
                request,
                entryList,
                $"Hexalith.Memories axis '{requestedAxis}' is disabled by configuration.",
                cancellationToken).ConfigureAwait(false);
        }

        if (request.Mode == PartySearchMode.Graph
            && string.IsNullOrWhiteSpace(request.GraphContextMemoryUnitId)
            && string.IsNullOrWhiteSpace(request.GraphContextPartyId))
        {
            logger.LogInformation(
                "Graph search invoked without graph context for {TenantId}. Falling back to local display-name search.",
                request.TenantId);
            return await DegradeToLocalAsync(
                request,
                entryList,
                "Graph-assisted search requires a graph context (memory unit id or party id).",
                cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<MemoryCandidate> candidates;
        PartySearchExecutionStatus status = PartySearchExecutionStatus.Rich;
        string? degradedReason = null;
        // Memories must return enough candidates so that hydration can fill the requested page.
        // Request page * pageSize so that pagination after collapse can land on the right window
        // even when many candidates are filtered out by tenant/case/auth/active checks.
        int memoriesTopK = checked(request.Page * request.PageSize);

        try
        {
            switch (request.Mode)
            {
                case PartySearchMode.Lexical:
                    SearchResult lexical = await memoriesClient.SearchAsync(
                        new SearchRequest(request.TenantId, SyntacticAxis, request.Query, request.CaseId, memoriesTopK, Explain: true),
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
                        new SearchRequest(request.TenantId, SemanticAxis, request.Query, request.CaseId, memoriesTopK, Explain: true),
                        cancellationToken).ConfigureAwait(false);
                    candidates = [.. semantic.Results.Select(ToCandidate)];
                    if (semantic.Degraded)
                    {
                        status = PartySearchExecutionStatus.Degraded;
                        degradedReason = BuildDegradedReason(semantic.UnavailableAxes);
                    }

                    break;

                case PartySearchMode.Graph:
                    string startNodeId = request.GraphContextMemoryUnitId ?? request.GraphContextPartyId!;
                    TraversalResult traversal = await memoriesClient.TraverseAsync(
                        request.TenantId,
                        startNodeId,
                        depth: 2,
                        caseId: request.CaseId,
                        ct: cancellationToken).ConfigureAwait(false);
                    // Traversal results don't carry per-node CaseId because the case scope is
                    // enforced server-side at the TraverseAsync request boundary. Tag each
                    // candidate with the request's CaseId so the strict-case hydration filter
                    // doesn't drop legitimate hits.
                    candidates = [.. traversal.Nodes.Select(node => ToCandidate(node) with { CaseId = request.CaseId })];
                    if (traversal.Degraded)
                    {
                        status = PartySearchExecutionStatus.Degraded;
                        degradedReason = BuildDegradedReason(traversal.UnavailableAxes);
                    }

                    break;

                default:
                    HybridSearchResult hybrid = await memoriesClient.HybridSearchAsync(
                        new HybridSearchRequest(request.TenantId, request.Query, request.CaseId, memoriesTopK, Explain: true),
                        cancellationToken).ConfigureAwait(false);
                    candidates = [.. hybrid.Results.Select(ToCandidate)];
                    if (hybrid.Degraded)
                    {
                        status = PartySearchExecutionStatus.Degraded;
                        degradedReason = BuildDegradedReason(hybrid.UnavailableAxes);
                    }

                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsTransientMemoriesFailure(ex))
        {
            logger.LogWarning(
                ex,
                "Hexalith.Memories search failed for {TenantId} mode={Mode}. Falling back to local display-name search.",
                request.TenantId,
                request.Mode);
            return await DegradeToLocalAsync(
                request,
                entryList,
                $"Hexalith.Memories search failed: {ex.GetType().Name}. Local fallback applied.",
                cancellationToken).ConfigureAwait(false);
        }

        return Hydrate(request, entryList, candidates, status, degradedReason, cancellationToken);
    }

    private async Task<PartySearchResponse> DegradeToLocalAsync(
        PartySearchRequest request,
        IEnumerable<PartyIndexEntry> entries,
        string degradedReason,
        CancellationToken cancellationToken)
    {
        PartySearchResponse local = await localFallback
            .SearchAsync(request, entries, cancellationToken)
            .ConfigureAwait(false);

        // Override the LocalOnly status with Degraded so callers can distinguish
        // "Memories was never configured" (LocalOnly) from "Memories tried and failed" (Degraded).
        return local with
        {
            Status = PartySearchExecutionStatus.Degraded,
            DegradedReason = degradedReason,
        };
    }

    private static bool IsTransientMemoriesFailure(Exception ex)
        => ex is HttpRequestException
            or TaskCanceledException
            or TimeoutException
            or InvalidOperationException;

    private static string MapModeToAxis(PartySearchMode mode) => mode switch
    {
        PartySearchMode.Lexical => SyntacticAxis,
        PartySearchMode.Semantic => SemanticAxis,
        PartySearchMode.Graph => GraphAxis,
        _ => HybridAxis,
    };

    private static PartySearchResponse Hydrate(
        PartySearchRequest request,
        IEnumerable<PartyIndexEntry> entries,
        IReadOnlyList<MemoryCandidate> candidates,
        PartySearchExecutionStatus status,
        string? degradedReason,
        CancellationToken cancellationToken)
    {
        // Use first-wins on duplicate ids so a corrupted index entry can't crash the request.
        Dictionary<string, PartyIndexEntry> entriesById = new(StringComparer.Ordinal);
        foreach (PartyIndexEntry entry in entries)
        {
            if (entry.IsErased)
            {
                continue;
            }

            entriesById.TryAdd(entry.Id, entry);
        }

        List<HydratedCandidate> hydrated = [];
        foreach (MemoryCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!PartyMemoryUrn.TryParse(candidate.SourceUri, out string tenantId, out string partyId))
            {
                continue;
            }

            if (!string.Equals(tenantId, request.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Strict case isolation: when the request is case-scoped, candidates without a case
            // id (or with a mismatching case id) must be dropped. The previous "either side null
            // means skip the check" policy admitted candidates from any case.
            if (!string.IsNullOrWhiteSpace(request.CaseId)
                && !string.Equals(candidate.CaseId, request.CaseId, StringComparison.Ordinal))
            {
                continue;
            }

            if (request.AuthorizedPartyIds is not null && !request.AuthorizedPartyIds.Contains(partyId))
            {
                continue;
            }

            if (!entriesById.TryGetValue(partyId, out PartyIndexEntry? entry))
            {
                continue;
            }

            if (request.TypeFilter is not null && entry.Type != request.TypeFilter.Value)
            {
                continue;
            }

            if (request.ActiveFilter is not null && entry.IsActive != request.ActiveFilter.Value)
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
            [.. items.Select(item => collapsed.First(h => h.Entry.Id == item.Party.Id)).Select(ToScoreMetadata)],
            [.. items.Select(item => collapsed.First(h => h.Entry.Id == item.Party.Id)).Select(ToSourceMetadata)]);
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
            HybridAxis,
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
            SyntacticScore: string.Equals(result.Axis, SyntacticAxis, StringComparison.OrdinalIgnoreCase) ? result.Score : null,
            SemanticScore: string.Equals(result.Axis, SemanticAxis, StringComparison.OrdinalIgnoreCase) ? result.Score : null,
            GraphScore: string.Equals(result.Axis, GraphAxis, StringComparison.OrdinalIgnoreCase) ? result.Score : null,
            CompositeScore: null);

    private static MemoryCandidate ToCandidate(TraversalNode node)
    {
        // HopDistance 0 = the start node itself. Score it 1.0 and decay 1/(hop+1) for neighbours
        // so a hop-1 neighbour scores 0.5, hop-2 scores 0.333 — distinguishable, monotonic.
        double score = 1.0 / (1 + Math.Max(0, node.HopDistance));
        return new MemoryCandidate(
            node.MemoryUnitId,
            node.SourceUri,
            CaseId: null,
            Axis: GraphAxis,
            Score: score,
            SyntacticScore: null,
            SemanticScore: null,
            GraphScore: score,
            CompositeScore: null);
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
