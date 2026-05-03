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
        request = NormalizeRequest(request, current);

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
        // Use long arithmetic and clamp to int.MaxValue so a malicious or buggy caller passing
        // a very large Page cannot trigger OverflowException (which would escape as a 500).
        long requestedWindow = (long)request.Page * request.PageSize;
        long memoriesTopKLong = Math.Max(requestedWindow, entryList.Count);
        int memoriesTopK = memoriesTopKLong > int.MaxValue ? int.MaxValue : (int)memoriesTopKLong;

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
                    string? startNodeId = await ResolveGraphStartNodeIdAsync(request, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(startNodeId))
                    {
                        return await DegradeToLocalAsync(
                            request,
                            entryList,
                            "Graph-assisted search could not resolve the party context to a Memories memory unit.",
                            cancellationToken).ConfigureAwait(false);
                    }

                    TraversalResult traversal = await memoriesClient.TraverseAsync(
                        request.TenantId,
                        startNodeId,
                        depth: 2,
                        caseId: request.CaseId,
                        ct: cancellationToken).ConfigureAwait(false);
                    candidates = await BuildTraversalCandidatesAsync(request, traversal.Nodes, cancellationToken).ConfigureAwait(false);
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

    private async Task<string?> ResolveGraphStartNodeIdAsync(
        PartySearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.GraphContextMemoryUnitId))
        {
            return request.GraphContextMemoryUnitId;
        }

        if (string.IsNullOrWhiteSpace(request.GraphContextPartyId))
        {
            return null;
        }

        string sourceUri = PartyMemoryUrn.Build(request.TenantId, request.GraphContextPartyId);
        SearchResult result = await memoriesClient.SearchAsync(
            new SearchRequest(request.TenantId, SyntacticAxis, sourceUri, request.CaseId, MaxResults: 5, Explain: false),
            cancellationToken).ConfigureAwait(false);

        return result.Results
            .FirstOrDefault(r => string.Equals(r.SourceUri, sourceUri, StringComparison.Ordinal))?
            .MemoryUnitId;
    }

    private async Task<IReadOnlyList<MemoryCandidate>> BuildTraversalCandidatesAsync(
        PartySearchRequest request,
        IReadOnlyList<TraversalNode> nodes,
        CancellationToken cancellationToken)
    {
        List<MemoryCandidate> candidates = new(nodes.Count);
        foreach (TraversalNode node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                MemoryUnit unit = await memoriesClient
                    .GetMemoryUnitAsync(request.TenantId, request.CaseId!, node.MemoryUnitId, cancellationToken)
                    .ConfigureAwait(false);

                candidates.Add(ToCandidate(node, unit));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsTransientMemoriesFailure(ex) || ex is MemoriesRemoteException)
            {
                logger.LogWarning(
                    ex,
                    "Skipping graph traversal node {MemoryUnitId} because Memories unit hydration failed.",
                    node.MemoryUnitId);
            }
        }

        return candidates;
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
            or TimeoutException;

    private static PartySearchRequest NormalizeRequest(
        PartySearchRequest request,
        PartyMemorySearchOptions options)
    {
        if (request.AuthorizedPartyIds is null)
        {
            throw new InvalidOperationException("Party search requires an explicit AuthorizedPartyIds set.");
        }

        string? caseId = string.IsNullOrWhiteSpace(request.CaseId)
            ? options.CaseId
            : request.CaseId;
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new InvalidOperationException("Party search requires an explicit case scope.");
        }

        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        return request with { CaseId = caseId, Page = page, PageSize = pageSize };
    }

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
            if (entry.IsErased || !entry.IsActive)
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
                .ThenBy(h => h.Entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(h => h.Entry.Id, StringComparer.Ordinal),
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

        // Build a single id-keyed lookup over the page items (not the full collapsed list) so
        // ScoreMetadata / SourceMetadata don't pay an O(N²) `First` scan per page row, and so a
        // future drift between collapse and metadata can't surface as InvalidOperationException.
        Dictionary<string, HydratedCandidate> pageHydrated = new(items.Count, StringComparer.Ordinal);
        foreach (HydratedCandidate hit in collapsed.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize))
        {
            pageHydrated.TryAdd(hit.Entry.Id, hit);
        }

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
            [.. items.Select(item => ToScoreMetadata(pageHydrated[item.Party.Id]))],
            [.. items.Select(item => ToSourceMetadata(pageHydrated[item.Party.Id]))]);
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
            hydrated.Candidate.EventType);

    private static MemoryCandidate ToCandidate(FusedScoredResult result)
        => new(
            result.MemoryUnitId,
            result.SourceUri,
            result.CaseId,
            HybridAxis,
            result.CompositeScore,
            result.SyntacticScore,
            result.SemanticScore,
            result.GraphScore,
            result.CompositeScore,
            EventType: null);

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
            CompositeScore: null,
            EventType: null);

    private static MemoryCandidate ToCandidate(TraversalNode node, MemoryUnit unit)
    {
        // HopDistance 0 = the start node itself. Score it 1.0 and decay 1/(hop+1) for neighbours
        // so a hop-1 neighbour scores 0.5, hop-2 scores 0.333 — distinguishable, monotonic.
        double score = 1.0 / (1 + Math.Max(0, node.HopDistance));
        string? eventType = unit.Metadata.TryGetValue("eventType", out MetadataField? field) ? field.Value : null;
        return new MemoryCandidate(
            node.MemoryUnitId,
            unit.SourceUri,
            unit.CaseId,
            Axis: GraphAxis,
            Score: score,
            SyntacticScore: null,
            SemanticScore: null,
            GraphScore: score,
            CompositeScore: null,
            eventType);
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
        double? CompositeScore,
        string? EventType);

    private sealed record HydratedCandidate(PartyIndexEntry Entry, MemoryCandidate Candidate);
}
