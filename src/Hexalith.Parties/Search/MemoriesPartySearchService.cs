using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Contracts.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Search;

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
    /// Memories itself clamps MaxResults to 100 server-side, so a higher value here only
    /// wastes RPC bandwidth. We multiply the requested window by an amplification factor
    /// (so hydration filters dropping a fraction of rows still leave the page filled) and
    /// then cap at this constant.
    /// </summary>
    private const int MaxMemoriesTopK = 200;

    /// <summary>
    /// Multiplier applied to the requested page window when asking Memories for candidates.
    /// Tenant / case / authorization / active-state hydration filters drop a fraction of
    /// rows locally; without amplification, page 1 of a 50%-filter-drop tenant returns half
    /// the requested page size.
    /// </summary>
    private const int FilterAmplificationFactor = 4;

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
        // Compute a bounded multiple of the requested window so pagination after collapse can
        // land on the right window even when many candidates are filtered out by
        // tenant/case/auth/active checks. Long arithmetic avoids OverflowException on large
        // page numbers; the cap (MaxMemoriesTopK) prevents an enormous topK that Memories
        // would clamp anyway.
        long requestedWindow = (long)request.Page * request.PageSize;
        long amplified = checked(requestedWindow * FilterAmplificationFactor);
        long memoriesTopKLong = Math.Min(MaxMemoriesTopK, Math.Max(amplified, request.PageSize));
        int memoriesTopK = memoriesTopKLong > int.MaxValue ? int.MaxValue : (int)memoriesTopKLong;

        try
        {
            switch (request.Mode)
            {
                case PartySearchMode.Lexical:
                    SearchResult lexical = await memoriesClient.SearchAsync(
                        new SearchRequest(request.TenantId, SyntacticAxis, request.Query, request.CaseId, memoriesTopK, Explain: true),
                        cancellationToken).ConfigureAwait(false);
                    // P8: defend against `Results == null` from a misbehaving server; the
                    // traversal path already does this and the same defense applies here.
                    candidates = [.. (lexical.Results ?? []).Select(ToCandidate)];
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
                    candidates = [.. (semantic.Results ?? []).Select(ToCandidate)];
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
                    candidates = [.. (hybrid.Results ?? []).Select(ToCandidate)];
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

        return (result.Results ?? [])
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
            catch (Exception ex) when (IsTransientMemoriesFailure(ex))
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
        // P9: Coerce the request to a mode the local provider can serve before delegating
        // on every degrade path, not just Graph. `LocalPartySearchService` ignores Mode, so
        // a Lexical/Semantic/Hybrid request flowing through verbatim makes the response
        // advertise (e.g.) "Mode=Semantic, Status=Degraded" while the local provider ran
        // the same fuzzy lexical pipeline regardless of mode. Remap so the response mode and
        // the actual local behavior agree.
        PartySearchRequest localRequest = request with { Mode = PartySearchMode.Hybrid };

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

    /// <summary>
    /// P6: Narrow the transient-failure surface to genuinely-transient exception shapes.
    /// <see cref="MemoriesRemoteException"/> wraps both 4xx (caller misuse — config drift,
    /// bad tenant id) and 5xx (server outage), so blanket-degrading on it would mask config
    /// bugs as outages. Inspect the response status code and only treat connection-level
    /// transports + 5xx as transient.
    /// </summary>
    private static bool IsTransientMemoriesFailure(Exception ex)
    {
        if (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            return true;
        }

        if (ex is MemoriesRemoteException remote)
        {
            int status = (int)remote.StatusCode;
            return status is 0 or 408 or 429 or >= 500;
        }

        return false;
    }

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

        // P26 + P28: Re-key the authorized set into an Ordinal HashSet so comparisons inside
        // Hydrate are symmetric with internal id collections (which all use Ordinal). Filter
        // null/whitespace ids so the silent-drop the dedup-log warns about cannot happen
        // here. Use Equals (not ReferenceEquals) on the comparer because future BCL changes
        // may wrap StringComparer.Ordinal in a singleton wrapper, and ReferenceEquals would
        // start re-keying every call.
        IReadOnlySet<string> authorized = NormalizeAuthorizedPartyIds(request.AuthorizedPartyIds!);

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

    private static IReadOnlySet<string> NormalizeAuthorizedPartyIds(IReadOnlySet<string> source)
    {
        if (source is HashSet<string> existing && Equals(existing.Comparer, StringComparer.Ordinal))
        {
            // Already an Ordinal HashSet; only re-allocate if any null/whitespace element
            // would otherwise leak through.
            if (!ContainsBlank(existing))
            {
                return existing;
            }
        }

        HashSet<string> rekeyed = new(StringComparer.Ordinal);
        foreach (string id in source)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                _ = rekeyed.Add(id);
            }
        }

        return rekeyed;
    }

    private static bool ContainsBlank(IEnumerable<string> ids)
    {
        foreach (string id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return true;
            }
        }

        return false;
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
        int duplicateCount = 0;
        string? firstDuplicateId = null;
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
                // P11: Aggregate duplicate-id observations into a single per-request warning
                // instead of warning per row. A real corruption surface across thousands of
                // duplicate entries used to overwhelm the log pipeline; one summary warning
                // preserves the diagnostic without flooding.
                duplicateCount++;
                firstDuplicateId ??= entry.Id;
            }
        }

        if (duplicateCount > 0)
        {
            logger.LogWarning(
                "Duplicate PartyIndexEntry ids detected during search hydration for tenant {TenantId} case {CaseId}: {DuplicateCount} duplicate(s), first occurrence party id {FirstPartyId}. Keeping first occurrence per id; duplicates may carry divergent data.",
                request.TenantId,
                request.CaseId,
                duplicateCount,
                firstDuplicateId);
        }

        List<HydratedCandidate> hydrated = [];
        foreach (MemoryCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!PartyMemoryUrn.TryParse(candidate.SourceUri, logger, out string tenantId, out string partyId))
            {
                continue;
            }

            // P5: Tenant + case comparison reverted to Ordinal per spec line 400 ("Ordinal
            // preferred, with canonical casing enforced at write time"). The previous
            // OrdinalIgnoreCase relaxation risked multi-tenant data leak when tenant ids
            // differed only by casing. Canonical-casing drift in historical data is
            // diagnosed via the URN logger — not silently merged here.
            if (!string.Equals(tenantId, request.TenantId, StringComparison.Ordinal))
            {
                continue;
            }

            // Strict case isolation: when the request is case-scoped, candidates without a
            // case id (or with a mismatching case id) must be dropped.
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
        // P31: defensive max(1, …) so a future caller path that bypasses NormalizeRequest
        // cannot divide by zero and produce NaN/Infinity — which then casts to int.MinValue
        // for advertised TotalPages.
        int divisor = Math.Max(1, request.PageSize);
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / divisor);

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
    private double SafeScore(MemoryCandidate candidate)
    {
        double score = candidate.CompositeScore ?? candidate.Score ?? 0;
        if (double.IsNaN(score) || double.IsInfinity(score))
        {
            // P12: Surface NaN/Infinity occurrences so a Memories upgrade emitting bad
            // floats produces a diagnostic instead of silently corrupted ranking.
            logger.LogWarning(
                "MemoryCandidate score was NaN or Infinity (raw composite={Composite}, score={Score}); clamping to 0.",
                candidate.CompositeScore,
                candidate.Score);
            return 0;
        }

        return score;
    }

    private double? SanitizeScore(double? score)
    {
        if (score is null)
        {
            return null;
        }

        double v = score.Value;
        if (double.IsNaN(v) || double.IsInfinity(v))
        {
            logger.LogWarning("Sanitized NaN/Infinity score from Memories candidate; returning null in response metadata.");
            return null;
        }

        return v;
    }

    private PartySearchResult ToSearchResult(HydratedCandidate hydrated)
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

    private PartySearchScoreMetadata ToScoreMetadata(HydratedCandidate hydrated)
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
        // BuildTraversalCandidatesAsync). Use Math.Max(0, hop) to keep the score monotonic
        // and let the upstream filter — not a score floor — guarantee start-node exclusion.
        // This way a missed-filter bug surfaces as a 1.0 score that obviously dominates the
        // page rather than as a hop-1 tie that hides the failure.
        double score = 1.0 / (1 + Math.Max(0, node.HopDistance));
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
