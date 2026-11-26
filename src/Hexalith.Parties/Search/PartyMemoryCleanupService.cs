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
            HashSet<PartyMemoryIngestionIntent> expected = [.. snapshot.Where(item => item.CaseId == caseId)];
            string? producerAuthority = BuildProducerAuthority(httpClient.BaseAddress);
            PartyMemoryIngestionIntent? intent = snapshot.FirstOrDefault(item => item.CaseId == caseId
                && (item.WorkflowInstanceId == memoryUnitId || item.MemoryUnitId == memoryUnitId)
                && producerAuthority is not null
                && (item.CleanupSettled ? item.AbsenceAuthority == producerAuthority : item.ProducerAuthority == producerAuthority));
            if (intent is { CleanupSettled: false } && PartyMemoryIngestionEvidence.IsTypedRetryReservation(intent))
            {
                return Result(tenantId, partyId, memoryUnitId, false, "Memories cleanup retains an unacknowledged typed ingestion obligation.");
            }

            string? canonical = intent is { CleanupSettled: true }
                ? intent.MemoryUnitId ?? memoryUnitId
                : intent?.MemoryUnitId;
            if (canonical is null || intent is { CleanupSettled: false } && (intent.RequiresTerminalEvidence || intent.TerminalObservationId is not null))
            {
                if (intent is { CleanupSettled: false })
                {
                    (intent, canonical) = await ObserveDirectTerminalStatusAsync(intent).ConfigureAwait(false);
                }
                else
                {
                    canonical = await ResolveAsync(tenantId, caseId, memoryUnitId, cancellationToken).ConfigureAwait(false);
                }

                if (intent?.MemoryUnitId is not null && canonical is not null && intent.MemoryUnitId != canonical)
                {
                    throw new InvalidOperationException("memory-source-inventory-output-mismatch");
                }
            }
            if (canonical is null || producerAuthority is null)
            {
                return Result(tenantId, partyId, memoryUnitId, false, "Memories cleanup requires validated terminal ingestion evidence.");
            }

            bool selectedWasActive = intent is { CleanupSettled: false };
            if (intent is { CleanupSettled: false } && (intent.MemoryUnitId is null || intent.RequiresTerminalEvidence || intent.TerminalObservationId is not null))
            {
                intent = await RetainDirectTerminalStatusAsync(intent, canonical).ConfigureAwait(false);
            }

            PartyMemoryIngestionIntent[] siblings = snapshot.Where(item => item.AttemptId != intent?.AttemptId
                && item.CaseId == caseId && !item.CleanupSettled && !item.SubmissionRejected
                && (HasUnknownSharedOutput(item)
                    || (item.MemoryUnitId == canonical
                        && (item.ProducerAuthority is null || item.ProducerAuthority == producerAuthority))))
                .ToArray();
            if (intent is null)
            {
                // Retain the alias even when a sibling still prevents us from settling it.
                intent = await intentStore.RetainLegacyAsync(tenantId, partyId,
                    new(memoryUnitId, PartyMemoryUrn.Build(tenantId, partyId), caseId), caseId, canonical, cancellationToken,
                    producerAuthority, requirePending: siblings.Length > 0, expectedCase: expected).ConfigureAwait(false);
                expected.Add(intent);
            }
            List<PartyMemoryIngestionIntent> sharedSiblings = [];
            foreach (PartyMemoryIngestionIntent sibling in siblings)
            {
                if (!PartyMemoryIngestionEvidence.IsTypedRetryReservation(sibling)
                    && sibling is { RequiresTerminalEvidence: false, TerminalObservationId: null } && sibling.MemoryUnitId == canonical
                    && sibling.ProducerAuthority == producerAuthority)
                {
                    sharedSiblings.Add(sibling);
                    continue;
                }

                if ((sibling.ProducerAuthority is not null && sibling.ProducerAuthority != producerAuthority)
                    || string.IsNullOrWhiteSpace(sibling.WorkflowInstanceId))
                {
                    return Result(tenantId, partyId, canonical, false, "Memories cleanup retains an unresolved sibling ingestion obligation.");
                }

                var (observedSibling, observed) = await ObserveDirectTerminalStatusAsync(sibling).ConfigureAwait(false);
                if (observed is null)
                {
                    return Result(tenantId, partyId, canonical, false, "Memories cleanup retains an unresolved sibling ingestion obligation.");
                }

                if (sibling.MemoryUnitId is not null && sibling.MemoryUnitId != observed)
                {
                    throw new InvalidOperationException("memory-source-inventory-output-mismatch");
                }

                PartyMemoryIngestionIntent retainedSibling = await RetainDirectTerminalStatusAsync(observedSibling, observed).ConfigureAwait(false);
                if (observed == canonical)
                {
                    sharedSiblings.Add(retainedSibling);
                }
            }

            // Newly terminal producers of this unit can invalidate historical absence. Keep
            // their terminal evidence pending until deletion and scoped absence both succeed.
            bool alreadyAbsent = !selectedWasActive && sharedSiblings.Count == 0
                && (intent.CleanupSettled || HasAbsenceReceipt(canonical));
            if (!alreadyAbsent)
            {
                await intentStore.ConfirmDirectCleanupAsync(tenantId, partyId, caseId, expected, [], producerAuthority,
                    cancellationToken).ConfigureAwait(false);
            }

            PartyMemoryCleanupResult result = alreadyAbsent
                ? Result(tenantId, partyId, canonical, true, null)
                : await DeleteTerminalUnitAsync(tenantId, caseId, partyId, canonical, cancellationToken).ConfigureAwait(false);
            if (result.Cleaned)
            {
                List<PartyMemoryIngestionIntent> cleaned = sharedSiblings;
                if (!intent.CleanupSettled)
                {
                    cleaned.Add(intent);
                }

                await intentStore.ConfirmDirectCleanupAsync(tenantId, partyId, caseId, expected, cleaned, producerAuthority,
                    cancellationToken).ConfigureAwait(false);
            }

            return result;

            async Task<(PartyMemoryIngestionIntent Intent, string? Unit)> ObserveDirectTerminalStatusAsync(PartyMemoryIngestionIntent previous)
            {
                PartyMemoryIngestionIntent reserved = await ReserveTypedTerminalObservationAsync(tenantId, partyId, previous, cancellationToken).ConfigureAwait(false);
                ReplaceExpected(previous, reserved);
                var (observed, unit) = await ObserveTerminalOutputAsync(tenantId, partyId, previous, reserved, cancellationToken).ConfigureAwait(false);
                ReplaceExpected(reserved, observed);
                return (observed, unit);
            }

            async Task<PartyMemoryIngestionIntent> RetainDirectTerminalStatusAsync(PartyMemoryIngestionIntent previous, string unit)
            {
                PartyMemoryIngestionIntent retained = await intentStore.RetainTerminalStatusAsync(tenantId, partyId, previous, unit,
                    producerAuthority, cancellationToken).ConfigureAwait(false);
                ReplaceExpected(previous, retained);
                return retained;
            }

            void ReplaceExpected(PartyMemoryIngestionIntent previous, PartyMemoryIngestionIntent current)
            {
                _ = expected.Remove(previous);
                expected.Add(current);
            }

            bool HasAbsenceReceipt(string unit) => snapshot.Any(receipt => receipt.CleanupSettled
                && !receipt.SubmissionRejected && receipt.AbsenceAuthority == producerAuthority
                && receipt.CaseId == caseId && receipt.MemoryUnitId == unit);
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
        bool remoteComplete;
        string? sourceClosureVersion;
        string? verifiedRemoteVersion = null;
        try
        {
            // Close admission first: a racing registration either appears in this snapshot
            // or conflicts and observes the enduring fence before any remote submission.
            intents = await intentStore.BeginCleanupAsync(tenantId, partyId, cancellationToken, permanent).ConfigureAwait(false);
            sourceClosureVersion = await intentStore.GetSourceClosureVersionAsync(tenantId, partyId, cancellationToken, BuildProducerAuthority(httpClient.BaseAddress)).ConfigureAwait(false);
            remoteComplete = sourceClosureVersion is not null;
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

        string? remoteBlock = null;
        HashSet<string> casesWithUnknownOutputs = new(StringComparer.Ordinal);
        if (!remoteComplete)
        {
            try
            {
                (remoteComplete, _, _) = await ReconcileSourceInventoryAsync(tenantId, partyId, permanent, fence: true, casesWithUnknownOutputs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                LogObservationFailure(logger, ex.GetType().Name);
                remoteBlock = "Memories cleanup retained incomplete remote source inventory.";
            }
            // Partial discoveries remain durable and can make verified local progress.
            intents = await intentStore.BeginCleanupAsync(tenantId, partyId, cancellationToken, permanent).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();
        string? producerAuthority = BuildProducerAuthority(httpClient.BaseAddress);
        List<PartyMemoryUnitMappingEntry> cleanedMappings = [];
        List<PartyMemoryIngestionIntent> cleanedIntents = [];
        List<PartyMemoryIngestionIntent> terminalIntents = [];
        Dictionary<string, PartyMemoryIngestionIntent> resolvedIntents = new(StringComparer.Ordinal);
        HashSet<(string CaseId, string UnitId)> unresolvedUnits = [];
        Dictionary<string, long> unresolvedTypedRevisions = new(StringComparer.Ordinal);
        string sourceUri = PartyMemoryUrn.Build(tenantId, partyId);
        HashSet<(string CaseId, string MappingId)> protectedMappings = [];
        Dictionary<(string CaseId, string UnitId), Task<PartyMemoryCleanupResult>> deletionAttempts = [];
        string? blocked = remoteComplete ? null : remoteBlock ?? "Memories source inventory requires owner reconciliation.";
        string blockedUnit = string.Empty;
        int remainingMappings = mappings.Count;
        int remainingIntents = intents.Count;
        try
        {
            // Reconcile every active workflow before deleting any known unit. One terminal
            // workflow cannot discharge another workflow that may still publish to that unit.
            foreach (PartyMemoryIngestionIntent snapshot in intents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (snapshot.CleanupSettled)
                {
                    // Historical receipts can settle a late mapping even when credentials or the case disappear.
                    if (producerAuthority is not null && snapshot.AbsenceAuthority == producerAuthority
                        && !snapshot.SubmissionRejected && snapshot.MemoryUnitId is not null)
                    {
                        resolvedIntents[snapshot.AttemptId] = snapshot;
                    }

                    continue;
                }

                if (snapshot.ProducerAuthority is not null && snapshot.ProducerAuthority != producerAuthority)
                {
                    blocked ??= "Memories cleanup retains an obligation from a different producer authority.";
                    ProtectMappings(snapshot);
                    ProtectCaseWithUnknownOutput(snapshot);
                    continue;
                }

                if (snapshot.SubmissionRejected)
                {
                    cleanedIntents.Add(snapshot);
                    continue;
                }

                PartyMemoryIngestionIntent intent = snapshot;
                string? observedUnit = null;
                bool ordinaryTypedObservation = false;
                try
                {
                    if ((snapshot.MemoryUnitId is null || snapshot.RequiresTerminalEvidence || snapshot.ProducerAuthority is null || snapshot.TerminalObservationId is not null)
                        && !string.IsNullOrWhiteSpace(snapshot.WorkflowInstanceId) && producerAuthority is not null)
                    {
                        intent = await ReserveTypedTerminalObservationAsync(tenantId, partyId, snapshot, cancellationToken).ConfigureAwait(false);
                        (intent, observedUnit) = await ObserveTerminalOutputAsync(tenantId, partyId, snapshot, intent, cancellationToken).ConfigureAwait(false);
                        ordinaryTypedObservation = intent.TerminalObservationId is null && !intent.RequiresTerminalEvidence
                            && intent.SourceUri == sourceUri && intent.ProducerAuthority == producerAuthority
                            && PartyMemoryIngestionEvidence.HasVerifiedRefreshAdmission(intent);
                        if (observedUnit is not null)
                        {
                            intent = await intentStore.RetainTerminalStatusAsync(tenantId, partyId, intent, observedUnit,
                                producerAuthority, cancellationToken).ConfigureAwait(false);
                        }
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

                if (intent.MemoryUnitId is null || intent.RequiresTerminalEvidence || intent.ProducerAuthority is null || intent.TerminalObservationId is not null
                    || PartyMemoryIngestionEvidence.IsTypedRetryReservation(intent))
                {
                    blocked ??= "Memories cleanup retained an unresolved ingestion obligation.";
                    ProtectMappings(intent);
                    if (intent.MemoryUnitId is { } knownUnit)
                    {
                        unresolvedUnits.Add((intent.CaseId, knownUnit));
                    }

                    if (observedUnit is not null)
                    {
                        unresolvedUnits.Add((intent.CaseId, observedUnit));
                    }
                    else
                    {
                        if (ordinaryTypedObservation && intent.MemoryUnitId is null)
                        {
                            long revision = intent.SourceRevisionNumber!.Value;
                            unresolvedTypedRevisions[intent.CaseId] = Math.Max(revision, unresolvedTypedRevisions.GetValueOrDefault(intent.CaseId));
                        }
                        else
                        {
                            ProtectCaseWithUnknownOutput(intent);
                        }
                    }

                    continue;
                }

                resolvedIntents[intent.AttemptId] = intent;
                terminalIntents.Add(intent);
            }

            foreach (PartyMemoryIngestionIntent intent in terminalIntents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string unit = intent.MemoryUnitId!;
                if (casesWithUnknownOutputs.Contains(intent.CaseId) || IsHeldByTypedObservation(intent) || unresolvedUnits.Contains((intent.CaseId, unit)))
                {
                    continue;
                }

                try
                {
                    PartyMemoryCleanupResult result = await ObserveDeletionAsync((intent.CaseId, unit)).ConfigureAwait(false);
                    if (result.Cleaned)
                    {
                        cleanedIntents.Add(intent);
                    }
                    else
                    {
                        blocked ??= result.BlockedReason;
                        blockedUnit = unit;
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

                if (casesWithUnknownOutputs.Contains(resolvedCase) || protectedMappings.Contains((resolvedCase, mapping.MemoryUnitId)))
                {
                    continue;
                }

                try
                {
                    PartyMemoryIngestionIntent? known = resolvedIntents.Values.FirstOrDefault(intent => intent.CaseId == resolvedCase
                        && (intent.MemoryUnitId == mapping.MemoryUnitId || intent.WorkflowInstanceId == mapping.MemoryUnitId));
                    if (known is null)
                    {
                        if (unresolvedTypedRevisions.ContainsKey(resolvedCase))
                        {
                            continue;
                        }

                        string? canonical = await ResolveAsync(tenantId, resolvedCase, mapping.MemoryUnitId, cancellationToken).ConfigureAwait(false);
                        if (canonical is null)
                        {
                            blocked ??= "Memories cleanup requires validated terminal ingestion evidence.";
                            blockedUnit = mapping.MemoryUnitId;
                            continue;
                        }

                        // Keep the validated alias even when a sibling prevents deletion in this pass.
                        var canonicalScope = (resolvedCase, canonical);
                        known = await intentStore.RetainLegacyAsync(tenantId, partyId, mapping,
                            resolvedCase, canonical, cancellationToken, producerAuthority,
                            requirePending: unresolvedUnits.Contains(canonicalScope) || deletionAttempts.ContainsKey(canonicalScope)).ConfigureAwait(false);
                        resolvedIntents[known.AttemptId] = known;
                    }

                    var scope = (resolvedCase, known.MemoryUnitId!);
                    if (IsHeldByTypedObservation(known) || unresolvedUnits.Contains(scope))
                    {
                        continue;
                    }

                    PartyMemoryCleanupResult result = known.CleanupSettled && !deletionAttempts.ContainsKey(scope)
                        ? Result(tenantId, partyId, known.MemoryUnitId!, true, null)
                        : await ObserveDeletionAsync(scope).ConfigureAwait(false);
                    if (result.Cleaned)
                    {
                        cleanedMappings.Add(mapping);
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
                if (cleanedIntents.Count > 0)
                {
                    // Exact settled receipts preserve provenance for every surviving or late
                    // mapping even when cancellation prevents mapping subtraction altogether.
                    remainingIntents = await intentStore.RemoveAsync(tenantId, partyId, cleanedIntents, recovery.Token, producerAuthority).ConfigureAwait(false);
                }

                if (cleanedMappings.Count > 0)
                {
                    remainingMappings = await mappingStore.RemoveMappingsAsync(tenantId, partyId, cleanedMappings, recovery.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                LogCancelledProgressPersistenceFailure(logger, ex.GetType().Name);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        // Re-read the fenced remote source after deletion. Pending completion, partial
        // pages and unavailable inventory cannot be discharged by empty local lists.
        try
        {
            if (sourceClosureVersion is null)
            {
                (bool complete, bool terminal, string version) = await ReconcileSourceInventoryAsync(tenantId, partyId, permanent, fence: false, casesWithUnknownOutputs, cancellationToken).ConfigureAwait(false);
                if (!complete || !terminal) { blocked ??= "Memories source inventory has not established complete terminal absence."; }
                else { verifiedRemoteVersion = version; }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            LogObservationFailure(logger, ex.GetType().Name);
            blocked ??= "Memories cleanup could not verify its final source inventory.";
        }
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

        if (permanent && sourceClosureVersion is null && verifiedRemoteVersion is not null)
        {
            try { await intentStore.RetainSourceClosureVersionAsync(tenantId, partyId, verifiedRemoteVersion, cancellationToken, BuildProducerAuthority(httpClient.BaseAddress)).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                LogObservationFailure(logger, ex.GetType().Name);
                return Result(tenantId, partyId, string.Empty, false, "Memories cleanup could not retain complete source-closure evidence.");
            }
        }
        return Result(tenantId, partyId, mappings.Count == 1 ? mappings[0].MemoryUnitId : string.Empty, true,
            cleanedMappings.Count > 1 ? $"Memories cleanup deleted {cleanedMappings.Count} of {mappings.Count} units." : null);

        Task<PartyMemoryCleanupResult> ObserveDeletionAsync((string CaseId, string UnitId) scope)
        {
            if (!deletionAttempts.TryGetValue(scope, out Task<PartyMemoryCleanupResult>? attempt))
            {
                attempt = DeleteTerminalUnitAsync(tenantId, scope.CaseId, partyId, scope.UnitId, cancellationToken);
                deletionAttempts.Add(scope, attempt);
            }

            return attempt;
        }

        bool IsHeldByTypedObservation(PartyMemoryIngestionIntent candidate)
            => unresolvedTypedRevisions.TryGetValue(candidate.CaseId, out long revision)
                && !(candidate.SourceUri == sourceUri && candidate.ProducerAuthority == producerAuthority
                    && PartyMemoryIngestionEvidence.HasVerifiedRefreshAdmission(candidate) && candidate.SourceRevisionNumber > revision);

        void ProtectCaseWithUnknownOutput(PartyMemoryIngestionIntent intent)
        {
            if (HasUnknownSharedOutput(intent))
            {
                casesWithUnknownOutputs.Add(intent.CaseId);
            }
        }

        void ProtectMappings(PartyMemoryIngestionIntent intent)
        {
            if (intent.MemoryUnitId is { } unit)
            {
                protectedMappings.Add((intent.CaseId, unit));
            }

            if (intent.WorkflowInstanceId is { } workflow)
            {
                protectedMappings.Add((intent.CaseId, workflow));
            }
        }
    }

    // An admission identifies a producer, but only scoped output evidence can narrow its cleanup hold.
    private static bool HasUnknownSharedOutput(PartyMemoryIngestionIntent intent)
        => intent is { CleanupSettled: false, SubmissionRejected: false }
            && (intent.MemoryUnitId is null || PartyMemoryIngestionEvidence.IsTypedRetryReservation(intent));

    private Task<PartyMemoryIngestionIntent> ReserveTypedTerminalObservationAsync(string tenantId, string partyId,
        PartyMemoryIngestionIntent snapshot, CancellationToken cancellationToken)
        => snapshot is { WorkflowInstanceId: not null, MemoryUnitId: null, RequiresTerminalEvidence: false }
            && snapshot.SourceUri == PartyMemoryUrn.Build(tenantId, partyId)
            && PartyMemoryIngestionEvidence.HasVerifiedRefreshAdmission(snapshot)
            ? intentStore.SetTerminalObservationAsync(tenantId, partyId, snapshot, Guid.NewGuid().ToString("N"), cancellationToken)
            : Task.FromResult(snapshot);

    private async Task<(PartyMemoryIngestionIntent Intent, string? Unit)> ObserveTerminalOutputAsync(string tenantId, string partyId,
        PartyMemoryIngestionIntent original, PartyMemoryIngestionIntent reserved, CancellationToken cancellationToken)
    {
        string? unit = null;
        if (!string.IsNullOrWhiteSpace(reserved.WorkflowInstanceId))
        {
            try
            {
                unit = await ResolveAsync(tenantId, reserved.CaseId, reserved.WorkflowInstanceId, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                LogObservationFailure(logger, ex.GetType().Name);
            }
        }

        // Only the first observer can release its exact marker after a nonpositive observation.
        // A prior marker may represent terminal output that another pass could not persist.
        if (unit is null && original.TerminalObservationId is null && reserved.TerminalObservationId is not null)
        {
            reserved = await intentStore.SetTerminalObservationAsync(tenantId, partyId, reserved, observationId: null, cancellationToken).ConfigureAwait(false);
        }

        return (reserved, unit);
    }

    private async Task<string?> ResolveAsync(string tenantId, string caseId, string workflowId, CancellationToken cancellationToken)
        => PartyMemoryIngestionEvidence.Resolve(await ReadWorkflowStatusAsync(workflowId, cancellationToken).ConfigureAwait(false),
            workflowId, tenantId, caseId);

    private async Task<IngestionWorkflowStatus?> ReadWorkflowStatusAsync(string workflowId, CancellationToken cancellationToken)
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

        return await response.Content.ReadFromJsonAsync<IngestionWorkflowStatus>(cancellationToken).ConfigureAwait(false);
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
