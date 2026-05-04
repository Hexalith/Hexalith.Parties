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

    /// <summary>
    /// Reasonable upper bound for how many candidates to request from Memories per page.
    /// Local hydration filters (tenant/case/auth/type/active) can drop a fraction of returned
    /// rows so we ask for several times the requested window — but capped so a deep page
    /// number can't translate into an enormous topK that Memories would clamp anyway and that
    /// wastes RPC bandwidth. Memories itself clamps MaxResults to 100 (server-side).
    /// </summary>
    private const int MaxMemoriesTopK = 200;

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
                DegradeCause.AxisDisabled,
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
                DegradeCause.MissingContext,
                cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<MemoryCandidate> candidates;
        PartySearchExecutionStatus status = PartySearchExecutionStatus.Rich;
        string? degradedReason = null;
        // Memories must return enough candidates so that hydration can fill the requested page.
        // Request a bounded multiple of the requested window so pagination after collapse can
        // land on the right window even when many candidates are filtered out by
        // tenant/case/auth/active checks. Long arithmetic avoids OverflowException on large
        // page numbers; the cap (MaxMemoriesTopK) prevents asking Memories for an enormous
        // result set when entryList is large — Memories clamps to 100 server-side anyway, so
        // a 100k topK only wastes RPC bandwidth.
        long requestedWindow = (long)request.Page * request.PageSize;
        long memoriesTopKLong = Math.Min(MaxMemoriesTopK, Math.Max(requestedWindow, request.PageSize));
        int memoriesTopK = (int)memoriesTopKLong;

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
                            DegradeCause.MissingContext,
                            cancellationToken).ConfigureAwait(false);
                    }

                    if (string.IsNullOrWhiteSpace(request.CaseId))
                    {
                        // Defensive guard: NormalizeRequest enforces non-empty CaseId today,
                        // but a future caller path that bypasses normalization would otherwise
                        // NRE on the null-forgiving operator below.
                        throw new InvalidOperationException("Graph traversal requires a normalized case scope.");
                    }

                    TraversalResult traversal = await memoriesClient.TraverseAsync(
                        request.TenantId,
                        startNodeId,
                        depth: 2,
                        caseId: request.CaseId,
                        ct: cancellationToken).ConfigureAwait(false);
                    IReadOnlyList<TraversalNode> nodes = traversal.Nodes ?? [];
                    candidates = await BuildTraversalCandidatesAsync(request, startNodeId, nodes, cancellationToken).ConfigureAwait(false);
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
                DegradeCause.RuntimeFailure,
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
        string startNodeId,
        IReadOnlyList<TraversalNode> nodes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CaseId))
        {
            throw new InvalidOperationException("Graph traversal requires a normalized case scope.");
        }

        List<MemoryCandidate> candidates = new(nodes.Count);
        foreach (TraversalNode node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The start node is the party that originated the graph search — the caller is
            // looking for related parties, not for itself. Filter it out so a trivial
            // "search from party-A" never has party-A dominating the result page.
            if (string.Equals(node.MemoryUnitId, startNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.MemoryUnitId))
            {
                logger.LogWarning("Skipping graph traversal node with null/empty MemoryUnitId.");
                continue;
            }

            try
            {
                MemoryUnit unit = await memoriesClient
                    .GetMemoryUnitAsync(request.TenantId, request.CaseId, node.MemoryUnitId, cancellationToken)
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
        DegradeCause cause,
        CancellationToken cancellationToken)
    {
        // Coerce the request to a mode the local provider can serve before delegating. Graph
        // mode flowed through verbatim previously, but `LocalPartySearchService` ignores Mode
        // — the response would advertise "graph context required" while the local provider
        // ran the Hybrid path. Remap so the response mode and degraded reason agree.
        PartySearchRequest localRequest = request.Mode == PartySearchMode.Graph
            ? request with { Mode = PartySearchMode.Hybrid }
            : request;

        PartySearchResponse local = await localFallback
            .SearchAsync(localRequest, entries, cancellationToken)
            .ConfigureAwait(false);

        // AC4 status semantics (resolved decision #3): distinguish between "Memories
        // integration disabled by an axis-config choice" (a `LocalOnly` outcome — operator
        // intent, not an alert), "runtime outage" (a `Degraded` outcome — operations should
        // investigate), and "missing context" (also `Degraded` — caller misuse but the
        // result set is still local-only fallback). The disabled-axis path here returns
        // LocalOnly so operations alarms on `Degraded` remain unambiguous.
        PartySearchExecutionStatus status = cause switch
        {
            DegradeCause.AxisDisabled => PartySearchExecutionStatus.LocalOnly,
            _ => PartySearchExecutionStatus.Degraded,
        };

        return local with
        {
            Status = status,
            DegradedReason = degradedReason,
        };
    }

    private static bool IsTransientMemoriesFailure(Exception ex)
        => ex is HttpRequestException
            or TaskCanceledException
            or TimeoutException
            or MemoriesRemoteException;

    private static PartySearchRequest NormalizeRequest(
        PartySearchRequest request,
        PartyMemorySearchOptions options)
    {
        if (request.AuthorizedPartyIds is null)
        {
            // ArgumentException maps to a 400 ProblemDetails via the global validation
            // handler; the previous InvalidOperationException surfaced as 500.
            throw new ArgumentException(
                "Party search requires an explicit AuthorizedPartyIds set.",
                nameof(request));
        }

        string? caseId = string.IsNullOrWhiteSpace(request.CaseId)
            ? options.CaseId
            : request.CaseId;
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new ArgumentException(
                "Party search requires an explicit case scope.",
                nameof(request));
        }

        // Re-key the authorized set into an Ordinal HashSet so comparisons inside Hydrate
        // are symmetric with internal id collections (which all use Ordinal). A caller
        // passing OrdinalIgnoreCase previously slipped past the membership check, then got
        // dropped silently at the entriesById lookup with no diagnostic.
        IReadOnlySet<string> authorized = request.AuthorizedPartyIds!;
        if (authorized is not HashSet<string> { Comparer: var cmp } || !ReferenceEquals(cmp, StringComparer.Ordinal))
        {
            authorized = new HashSet<string>(authorized, StringComparer.Ordinal);
        }

        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        return request with
        {
            CaseId = caseId,
            Page = page,
            PageSize = pageSize,
            AuthorizedPartyIds = authorized,
        };
    }

    private static string MapModeToAxis(PartySearchMode mode) => mode switch
    {
        PartySearchMode.Lexical => SyntacticAxis,
        PartySearchMode.Semantic => SemanticAxis,
        PartySearchMode.Graph => GraphAxis,
        _ => HybridAxis,
    };

    private PartySearchResponse Hydrate(
        PartySearchRequest request,
        IEnumerable<PartyIndexEntry> entries,
        IReadOnlyList<MemoryCandidate> candidates,
        PartySearchExecutionStatus status,
        string? degradedReason,
        CancellationToken cancellationToken)
    {
        Dictionary<string, PartyIndexEntry> entriesById = new(StringComparer.Ordinal);
        foreach (PartyIndexEntry entry in entries)
        {
            if (entry.IsErased)
            {
                continue;
            }

            // Inactive entries stay in the lookup so callers explicitly passing
            // ActiveFilter=false can find them; the IsActive filter is reapplied below
            // against `request.ActiveFilter`.
            if (!entriesById.TryAdd(entry.Id, entry))
            {
                // Split-brain corruption from concurrent projection writers becomes invisible
                // when first-wins silently drops the duplicate. Logging makes the corruption
                // signal observable so an operator can triage rather than chase a missing row.
                logger.LogWarning(
                    "Duplicate PartyIndexEntry id detected during search hydration: {PartyId}. Keeping first occurrence; the duplicate may carry divergent data.",
                    entry.Id);
            }
        }

        List<HydratedCandidate> hydrated = [];
        foreach (MemoryCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!PartyMemoryUrn.TryParse(candidate.SourceUri, logger, out string tenantId, out string partyId))
            {
                continue;
            }

            // Tenant comparison is case-insensitive to tolerate canonical-casing drift in
            // historical data; case scope is also case-insensitive for the same reason. The
            // previous Ordinal vs OrdinalIgnoreCase asymmetry between tenant and case let
            // mismatched-casing case ids silently drop hits.
            if (!string.Equals(tenantId, request.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Strict case isolation: when the request is case-scoped, candidates without a
            // case id (or with a mismatching case id) must be dropped.
            if (!string.IsNullOrWhiteSpace(request.CaseId)
                && !string.Equals(candidate.CaseId, request.CaseId, StringComparison.OrdinalIgnoreCase))
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

        // Collapse duplicate party-id hits to one row, keeping the highest-scoring candidate.
        // Inside the group, break score ties on MemoryUnitId so the kept candidate is
        // deterministic across calls — otherwise tied scores let iterator order flip between
        // queries, breaking idempotent client caches.
        List<HydratedCandidate> collapsed =
        [
            .. hydrated
                .GroupBy(h => h.Entry.Id, StringComparer.Ordinal)
                .Select(g => g
                    .OrderByDescending(h => SafeScore(h.Candidate))
                    .ThenBy(h => h.Candidate.MemoryUnitId, StringComparer.Ordinal)
                    .First())
                .OrderByDescending(h => SafeScore(h.Candidate))
                .ThenBy(h => h.Entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(h => h.Entry.Id, StringComparer.Ordinal),
        ];

        // Use long arithmetic + clamp so a malicious or buggy `Page=int.MaxValue` cannot
        // overflow the skip count to a negative integer (Enumerable.Skip with negative
        // returns the full sequence, producing the wrong page with the advertised page index).
        long skipLong = (long)Math.Max(0, request.Page - 1) * Math.Max(0, request.PageSize);
        int skip = skipLong > int.MaxValue ? int.MaxValue : (int)skipLong;

        List<HydratedCandidate> page = [.. collapsed.Skip(skip).Take(request.PageSize)];

        List<PartySearchResult> items = [.. page.Select(ToSearchResult)];

        int totalCount = collapsed.Count;
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / request.PageSize);

        // Build score / source metadata directly from the same `page` list so there is no
        // drift between `items` and the metadata arrays — the previous two-enumeration
        // approach risked KeyNotFoundException if one enumeration ever produced a different
        // ordering than the other.
        IReadOnlyList<PartySearchScoreMetadata> scoreMetadata = [.. page.Select(ToScoreMetadata)];
        IReadOnlyList<PartySearchSourceMetadata> sourceMetadata = [.. page.Select(ToSourceMetadata)];

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
            scoreMetadata,
            sourceMetadata);
    }

    /// <summary>
    /// Returns a score that is safe to sort and serialize. NaN/Infinity values produce
    /// non-deterministic ordering with <see cref="System.Linq.Enumerable.OrderByDescending"/>
    /// and break <c>JsonSerializer</c> mid-response when
    /// <c>JsonNumberHandling.AllowNamedFloatingPointLiterals</c> is not configured.
    /// </summary>
    private static double SafeScore(MemoryCandidate candidate)
    {
        double score = candidate.CompositeScore ?? candidate.Score ?? 0;
        return double.IsNaN(score) || double.IsInfinity(score) ? 0 : score;
    }

    private static double? SanitizeScore(double? score)
    {
        if (score is null)
        {
            return null;
        }

        double v = score.Value;
        return double.IsNaN(v) || double.IsInfinity(v) ? null : v;
    }

    private static PartySearchResult ToSearchResult(HydratedCandidate hydrated)
    {
        double score = SafeScore(hydrated.Candidate);
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
            SanitizeScore(hydrated.Candidate.Score),
            SanitizeScore(hydrated.Candidate.SyntacticScore),
            SanitizeScore(hydrated.Candidate.SemanticScore),
            SanitizeScore(hydrated.Candidate.GraphScore),
            SanitizeScore(hydrated.Candidate.CompositeScore));

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
        // HopDistance >= 1 by the time we reach here (start node was filtered out in
        // BuildTraversalCandidatesAsync). 1/(hop+1) keeps the score monotonic and
        // distinguishable: hop-1 → 0.5, hop-2 → 0.333, hop-3 → 0.25, etc.
        double score = 1.0 / (1 + Math.Max(1, node.HopDistance));
        string? eventType = null;
        if (unit.Metadata is not null
            && unit.Metadata.TryGetValue("eventType", out MetadataField? field)
            && !string.IsNullOrEmpty(field?.Value))
        {
            eventType = field.Value;
        }

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

    /// <summary>
    /// Why the request fell back to the local provider. Drives the response Status so
    /// callers and operations dashboards can distinguish operator intent (axis disabled =
    /// LocalOnly) from runtime outage (Memories failed = Degraded).
    /// </summary>
    private enum DegradeCause
    {
        AxisDisabled,
        MissingContext,
        RuntimeFailure,
    }

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
