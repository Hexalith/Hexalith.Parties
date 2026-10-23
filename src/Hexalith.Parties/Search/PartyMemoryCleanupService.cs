using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.Memories.Contracts.V1;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Search;

/// <summary>Reconciles durable ingestion obligations and observes scoped unit absence before cleanup.</summary>
internal sealed partial class PartyMemoryCleanupService(
    HttpClient httpClient,
    IPartyMemoryUnitMappingStore mappingStore,
    ILogger<PartyMemoryCleanupService> logger,
    IPartyMemoryIngestionIntentStore intentStore)
{
    /// <summary>Reconciles a legacy workflow identity before deleting its canonical terminal output.</summary>
    public async Task<PartyMemoryCleanupResult> DeleteMemoryUnitAsync(
        string tenantId, string caseId, string partyId, string memoryUnitId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryUnitId);
        try
        {
            IReadOnlyList<PartyMemoryIngestionIntent> snapshot = await intentStore.BeginCleanupAsync(
                tenantId, partyId, cancellationToken).ConfigureAwait(false);
            PartyMemoryIngestionIntent? intent = snapshot.FirstOrDefault(item => item.CaseId == caseId
                && (item.WorkflowInstanceId == memoryUnitId || item.MemoryUnitId == memoryUnitId));
            if (intent?.CleanupSettled == true)
            {
                return Result(tenantId, partyId, intent.MemoryUnitId ?? memoryUnitId, true, null);
            }

            string? canonical = intent?.MemoryUnitId
                ?? await ResolveAsync(tenantId, caseId, memoryUnitId, cancellationToken).ConfigureAwait(false);
            if (canonical is null)
            {
                return Result(tenantId, partyId, memoryUnitId, false, "Memories cleanup requires validated terminal ingestion evidence.");
            }

            if (intent is null)
            {
                intent = await intentStore.RetainLegacyAsync(tenantId, partyId,
                    new(memoryUnitId, PartyMemoryUrn.Build(tenantId, partyId), caseId), caseId, canonical, cancellationToken).ConfigureAwait(false);
            }
            else if (intent.MemoryUnitId is null)
            {
                intent = intent with { MemoryUnitId = canonical };
                await intentStore.UpdateAsync(tenantId, partyId, intent, cancellationToken).ConfigureAwait(false);
            }
            PartyMemoryCleanupResult result = intent.CleanupSettled
                ? Result(tenantId, partyId, canonical, true, null)
                : await DeleteTerminalUnitAsync(tenantId, caseId, partyId, canonical, cancellationToken).ConfigureAwait(false);
            if (result.Cleaned && !intent.CleanupSettled)
            {
                _ = await intentStore.RemoveAsync(tenantId, partyId, [intent], cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            LogObservationFailure(logger, ex.GetType().Name);
            return Result(tenantId, partyId, memoryUnitId, false, "Memories cleanup could not verify terminal ingestion.");
        }
    }

    /// <summary>Fences new submissions, reconciles every known intent and subtracts exact verified obligations.</summary>
    public async Task<PartyMemoryCleanupResult> DeleteByPartyAsync(
        string tenantId, string caseId, string partyId, CancellationToken cancellationToken, bool permanent = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        IReadOnlyList<PartyMemoryUnitMappingEntry> mappings;
        IReadOnlyList<PartyMemoryIngestionIntent> intents;
        try
        {
            // Close admission first: a racing registration either appears in this snapshot
            // or conflicts and observes the enduring fence before any remote submission.
            intents = await intentStore.BeginCleanupAsync(tenantId, partyId, cancellationToken, permanent).ConfigureAwait(false);
            mappings = await mappingStore.GetMappingsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            LogObservationFailure(logger, ex.GetType().Name);
            return Result(tenantId, partyId, string.Empty, false, "Memories cleanup could not verify its mapping inventory.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        List<PartyMemoryUnitMappingEntry> cleanedMappings = [];
        List<PartyMemoryIngestionIntent> cleanedIntents = [];
        Dictionary<string, PartyMemoryIngestionIntent> resolvedIntents = new(StringComparer.Ordinal);
        HashSet<(string CaseId, string UnitId)> absentUnits = [];
        string? blocked = null;
        string blockedUnit = string.Empty;
        int remainingMappings = mappings.Count;
        int remainingIntents = intents.Count;
        try
        {
            foreach (PartyMemoryIngestionIntent snapshot in intents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PartyMemoryIngestionIntent intent = snapshot;
                try
                {
                    if (intent.CleanupSettled)
                    {
                        // Historical receipts carry already verified absence, not remote work.
                        // They can settle a late mapping even if credentials or the case disappear.
                        resolvedIntents[intent.AttemptId] = intent;
                        if (intent.MemoryUnitId is not null)
                        {
                            absentUnits.Add((intent.CaseId, intent.MemoryUnitId));
                        }

                        continue;
                    }

                    if (intent.SubmissionRejected)
                    {
                        cleanedIntents.Add(intent);
                        continue;
                    }

                    if (intent.MemoryUnitId is null && !string.IsNullOrWhiteSpace(intent.WorkflowInstanceId))
                    {
                        string? canonical = await ResolveAsync(tenantId, intent.CaseId, intent.WorkflowInstanceId, cancellationToken).ConfigureAwait(false);
                        if (canonical is not null)
                        {
                            intent = intent with { MemoryUnitId = canonical };
                            await intentStore.UpdateAsync(tenantId, partyId, intent, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    resolvedIntents[intent.AttemptId] = intent;
                    if (intent.MemoryUnitId is null)
                    {
                        blocked ??= "Memories cleanup retained an unresolved ingestion obligation.";
                        continue;
                    }

                    PartyMemoryCleanupResult result = await DeleteTerminalUnitAsync(tenantId, intent.CaseId, partyId, intent.MemoryUnitId, cancellationToken).ConfigureAwait(false);
                    if (result.Cleaned)
                    {
                        absentUnits.Add((intent.CaseId, intent.MemoryUnitId));
                        cleanedIntents.Add(intent);
                    }
                    else
                    {
                        blocked ??= result.BlockedReason;
                        blockedUnit = intent.MemoryUnitId;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
                {
                    LogObservationFailure(logger, ex.GetType().Name);
                    blocked ??= "Memories cleanup could not reconcile an ingestion obligation.";
                }
            }

            foreach (PartyMemoryUnitMappingEntry mapping in mappings)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string resolvedCase = string.IsNullOrWhiteSpace(mapping.CaseId) ? caseId : mapping.CaseId;
                if (string.IsNullOrWhiteSpace(resolvedCase))
                {
                    blocked ??= "Memories cleanup blocked: the indexed unit has no persisted CaseId and no fallback CaseId is configured.";
                    blockedUnit = mapping.MemoryUnitId;
                    continue;
                }

                try
                {
                    PartyMemoryIngestionIntent? known = resolvedIntents.Values.FirstOrDefault(intent => intent.CaseId == resolvedCase
                        && (intent.MemoryUnitId == mapping.MemoryUnitId || intent.WorkflowInstanceId == mapping.MemoryUnitId));
                    if (known?.MemoryUnitId is null)
                    {
                        string? canonical = await ResolveAsync(tenantId, resolvedCase, mapping.MemoryUnitId, cancellationToken).ConfigureAwait(false);
                        if (canonical is null)
                        {
                            blocked ??= "Memories cleanup requires validated terminal ingestion evidence.";
                            blockedUnit = mapping.MemoryUnitId;
                            continue;
                        }

                        // Persist legacy workflow/canonical provenance before the first DELETE.
                        // A failed subtraction or expired workflow must not lose this association.
                        if (known is null)
                        {
                            known = await intentStore.RetainLegacyAsync(tenantId, partyId, mapping,
                                resolvedCase, canonical, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            known = known with { MemoryUnitId = canonical };
                            await intentStore.UpdateAsync(tenantId, partyId, known, cancellationToken).ConfigureAwait(false);
                        }

                        resolvedIntents[known.AttemptId] = known;
                    }

                    PartyMemoryCleanupResult result = known.CleanupSettled || absentUnits.Contains((resolvedCase, known.MemoryUnitId!))
                        ? Result(tenantId, partyId, known.MemoryUnitId!, true, null)
                        : await DeleteTerminalUnitAsync(tenantId, resolvedCase, partyId, known.MemoryUnitId!, cancellationToken).ConfigureAwait(false);
                    if (result.Cleaned)
                    {
                        cleanedMappings.Add(mapping);
                        absentUnits.Add((resolvedCase, known.MemoryUnitId!));
                        if (!known.CleanupSettled && !cleanedIntents.Contains(known))
                        {
                            cleanedIntents.Add(known);
                        }
                    }
                    else
                    {
                        blocked ??= result.BlockedReason;
                        blockedUnit = result.MemoryUnitId;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
                {
                    LogObservationFailure(logger, ex.GetType().Name);
                    blocked ??= "Memories cleanup could not durably retain legacy ingestion evidence.";
                    blockedUnit = mapping.MemoryUnitId;
                }
            }
        }
        finally
        {
            // Confirmed progress survives caller cancellation without expanding either snapshot.
            using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                if (cleanedMappings.Count > 0)
                {
                    remainingMappings = await mappingStore.RemoveMappingsAsync(tenantId, partyId, cleanedMappings, recovery.Token).ConfigureAwait(false);
                }

                if (cleanedIntents.Count > 0)
                {
                    // A changed or concurrent mapping may still need this canonical
                    // evidence. Re-read after conditional subtraction before removing it.
                    IReadOnlyList<PartyMemoryUnitMappingEntry> retained = await mappingStore
                        .GetMappingsAsync(tenantId, partyId, recovery.Token).ConfigureAwait(false);
                    List<PartyMemoryIngestionIntent> removable = [.. cleanedIntents.Where(intent => !retained.Any(mapping =>
                        (mapping.MemoryUnitId == intent.MemoryUnitId || mapping.MemoryUnitId == intent.WorkflowInstanceId)
                        && (string.IsNullOrWhiteSpace(mapping.CaseId) ? caseId : mapping.CaseId) == intent.CaseId))];
                    remainingIntents = await intentStore.RemoveAsync(tenantId, partyId, removable, recovery.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                LogCancelledProgressPersistenceFailure(logger, ex.GetType().Name);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        // Include concurrent legacy mapping arrivals; the intent fence prevents new submissions.
        remainingMappings = (await mappingStore.GetMappingsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false)).Count;
        remainingIntents = (await intentStore.GetAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false)).Count;
        if (blocked is not null)
        {
            return Result(tenantId, partyId, blockedUnit, false,
                $"Memories cleanup partially failed ({cleanedMappings.Count} of {mappings.Count} units deleted): {blocked}");
        }

        if (remainingMappings > 0 || remainingIntents > 0)
        {
            return Result(tenantId, partyId, string.Empty, false, "Memories cleanup retained additional obligations; another cleanup pass is required.");
        }

        return Result(tenantId, partyId, mappings.Count == 1 ? mappings[0].MemoryUnitId : string.Empty, true,
            cleanedMappings.Count > 1 ? $"Memories cleanup deleted {cleanedMappings.Count} of {mappings.Count} units." : null);
    }

    private async Task<string?> ResolveAsync(string tenantId, string caseId, string workflowId, CancellationToken cancellationToken)
    {
        if (httpClient.BaseAddress is null)
        {
            return null;
        }

        using HttpResponseMessage response = await httpClient.GetAsync(MemoriesRoutes.IngestStatusPath(workflowId), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        IngestionWorkflowStatus? status = await response.Content.ReadFromJsonAsync<IngestionWorkflowStatus>(cancellationToken).ConfigureAwait(false);
        return PartyMemoryIngestionEvidence.Resolve(status, workflowId, tenantId, caseId);
    }

    private async Task<PartyMemoryCleanupResult> DeleteTerminalUnitAsync(string tenantId, string caseId, string partyId, string memoryUnitId, CancellationToken cancellationToken)
    {
        if (httpClient.BaseAddress is null)
        {
            return Result(tenantId, partyId, memoryUnitId, false, "Memories cleanup blocked: HttpClient has no BaseAddress (Parties:MemoriesSearch:Endpoint missing).");
        }

        string path = MemoriesRoutes.CaseMemoryUnitPath(tenantId, caseId, memoryUnitId);
        try
        {
            using HttpResponseMessage deleted = await httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
            if (!await PartyMemoryCleanupResponse.IsAcceptedAsync(deleted, cancellationToken).ConfigureAwait(false))
            {
                return Result(tenantId, partyId, memoryUnitId, false, $"Memories cleanup failed with HTTP {(int)deleted.StatusCode} {deleted.ReasonPhrase}.");
            }

            // DELETE is asynchronous. Observe the exact scoped resource after known terminal
            // ingestion; a successful DELETE or arbitrary 404 cannot discharge an obligation.
            using HttpResponseMessage observed = await httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
            bool absent = await PartyMemoryCleanupResponse.IsAbsentAsync(observed, cancellationToken).ConfigureAwait(false);
            return Result(tenantId, partyId, memoryUnitId, absent,
                absent ? null : "Memories cleanup awaits qualified unit absence after terminal ingestion.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or JsonException)
        {
            LogObservationFailure(logger, ex.GetType().Name);
            return Result(tenantId, partyId, memoryUnitId, false, $"Memories cleanup transport error: {ex.GetType().Name}");
        }
    }

    private static PartyMemoryCleanupResult Result(string tenantId, string partyId, string memoryUnitId, bool cleaned, string? reason)
        => new(partyId, memoryUnitId, PartyMemoryUrn.Build(tenantId, partyId), cleaned, reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories cleanup evidence was unavailable with {ExceptionType}; obligations must be retried.")]
    private static partial void LogObservationFailure(ILogger logger, string exceptionType);

    /// <summary>
    /// Lightweight health probe of the per-unit cleanup route. Issues a <c>DELETE</c> against a
    /// synthetic memory-unit id. Only a structured MEMORY_UNIT_NOT_FOUND response proves
    /// that the route handled a miss in the requested case. Other 404s surface a deployment gap:
    /// <list type="bullet">
    ///   <item><description>200/204 — route reachable and the synthetic id improbably matched (still healthy).</description></item>
    ///   <item><description>404 with MEMORY_UNIT_NOT_FOUND — route reachable, unit absent in the requested case.</description></item>
    ///   <item><description>401/403 — auth misconfigured (the token in <see cref="PartyMemorySearchOptions.ApiToken"/> is rejected).</description></item>
    ///   <item><description>405 — DELETE not allowed at this shape (the AC5 gap that resolved decision #2 fixed; signals a Memories-server regression).</description></item>
    ///   <item><description>5xx / network error — endpoint unreachable.</description></item>
    /// </list>
    /// </summary>
    public async Task<PartyMemoryCleanupProbeResult> ProbeCleanupRouteAsync(
        string tenantId,
        string caseId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);

        if (httpClient.BaseAddress is null)
        {
            return new PartyMemoryCleanupProbeResult(false, null, "HttpClient has no BaseAddress.");
        }

        string syntheticUnitId = $"health-probe-{Guid.NewGuid():N}";
        string path = MemoriesRoutes.CaseMemoryUnitPath(tenantId, caseId, syntheticUnitId);

        try
        {
            using HttpResponseMessage response = await httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
            int code = (int)response.StatusCode;
            // An unqualified 404 can be a routing or provisioning failure. Reuse the same
            // response contract as erasure cleanup and indexing compensation.
            bool reachable = await PartyMemoryCleanupResponse.IsAcceptedAsync(response, cancellationToken).ConfigureAwait(false);
            string? reason = reachable
                ? null
                : code switch
                {
                    401 or 403 => $"Memories cleanup auth misconfigured (HTTP {code}).",
                    404 => "Memories cleanup probe returned an unqualified HTTP 404; route or tenant/case provisioning could not be verified.",
                    405 => "Memories cleanup route does not accept DELETE (AC5 regression — server may have removed the per-unit DELETE endpoint).",
                    _ => $"Memories cleanup probe returned unexpected HTTP {code}.",
                };
            return new PartyMemoryCleanupProbeResult(reachable, code, reason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            logger.LogWarning(ex, "Memories cleanup probe failed for tenant {TenantId}/case {CaseId}.", tenantId, caseId);
            return new PartyMemoryCleanupProbeResult(false, null, $"Memories cleanup endpoint unreachable: {ex.GetType().Name}.");
        }
    }

    /// <summary>Probes status-read routing and explicit authorization rejection without claiming terminal evidence.</summary>
    /// <remarks>A synthetic status miss exercises routing but cannot establish access to an existing tenant workflow.</remarks>
    internal async Task<PartyMemoryCleanupProbeResult> ProbeIngestionStatusRouteAsync(CancellationToken cancellationToken)
    {
        if (httpClient.BaseAddress is null)
        {
            return new(false, null, "HttpClient has no BaseAddress.");
        }

        try
        {
            string instanceId = $"health-probe-{Guid.NewGuid():N}";
            using HttpResponseMessage response = await httpClient.GetAsync(
                MemoriesRoutes.IngestStatusPath(instanceId), cancellationToken).ConfigureAwait(false);
            bool routed = false;
            if (response.IsSuccessStatusCode)
            {
                IngestionWorkflowStatus? status = await response.Content.ReadFromJsonAsync<IngestionWorkflowStatus>(cancellationToken).ConfigureAwait(false);
                routed = status is not null
                    && string.Equals(status.InstanceId, instanceId, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(status.TenantId)
                    && !string.IsNullOrWhiteSpace(status.CaseId)
                    && status.RuntimeStatus is "Pending" or "Running" or "Completed" or "Failed" or "Canceled" or "Terminated" or "Suspended" or "ContinuedAsNew"
                    && status.CreatedAt != default
                    && status.LastUpdatedAt != default;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                ErrorResponse? error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken).ConfigureAwait(false);
                routed = string.Equals(error?.Code, "INGESTION_STATUS_NOT_FOUND", StringComparison.Ordinal);
            }

            int code = (int)response.StatusCode;
            return new(routed, code, routed ? null : code is 401 or 403
                ? $"Memories ingestion status-read authorization rejected (HTTP {code})."
                : $"Memories ingestion status-read route returned unqualified HTTP {code}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or JsonException or NotSupportedException or InvalidOperationException)
        {
            return new(false, null, $"Memories ingestion status-read probe failed: {ex.GetType().Name}.");
        }
    }

    /// <summary>
    /// Configures the API token bearer header on the supplied HttpClient if a token is provided.
    /// Called by DI when registering the typed HttpClient. Throws on a null client (programming
    /// error) and falls through cleanly when the token is malformed (whitespace / control chars
    /// would otherwise blow up <see cref="AuthenticationHeaderValue"/> with a
    /// <see cref="FormatException"/>).
    /// </summary>
    public static void ConfigureAuthorization(HttpClient httpClient, string? apiToken, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return;
        }

        try
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        }
        catch (FormatException ex)
        {
            logger?.LogError(
                ex,
                "Memories cleanup API token is not a valid HTTP header value (likely contains whitespace or control characters). The token has been ignored; cleanup calls will be unauthenticated.");
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Memories cleanup progress persistence failed after caller cancellation with {ExceptionType}; cleanup must be retried.")]
    private static partial void LogCancelledProgressPersistenceFailure(ILogger logger, string exceptionType);
}
