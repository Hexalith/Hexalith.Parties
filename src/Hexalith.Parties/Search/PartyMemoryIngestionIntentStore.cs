using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Models;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Search;

/// <summary>SDK persistence for bounded ingestion metadata, cleanup receipts and admission generations.</summary>
internal sealed class PartyMemoryIngestionIntentStore(
    IReadModelStore store,
    IOptions<PartyMemoryUnitMappingStoreOptions> options,
    IOptions<PartySdkReadModelOptions>? projectionOptions = null) : IPartyMemoryIngestionIntentStore
{
    /// <summary>Maximum retained attempts and receipts per party; exhaustion blocks submission.</summary>
    internal const int Capacity = 1024;

    private readonly string _storeName = options.Value.StateStoreName;

    /// <inheritdoc/>
    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        if (store is not IReadModelConditionalEraser eraser || store is not IReadModelExpiringStore expiring)
        {
            throw new InvalidOperationException("memory-ingestion-probe-capabilities-unavailable");
        }

        const string tenant = "_health-probe";
        string party = $"_health-probe-{Guid.NewGuid():N}";
        string key = Key(tenant, party);
        var intent = new PartyMemoryIngestionIntent("_probe-attempt", "_probe-case", "urn:_probe");
        bool primaryFailed = true;
        try
        {
            var probeStore = new PartyMemoryProbeReadModelStore(store, expiring, _storeName, key);
            var probe = new PartyMemoryIngestionIntentStore(probeStore, options, projectionOptions);
            _ = await probe.RegisterAsync(tenant, party, intent, cancellationToken).ConfigureAwait(false);
            ReadModelEntry<PartyMemoryIngestionInventory> read = await store.GetAsync<PartyMemoryIngestionInventory>(
                _storeName, key, cancellationToken).ConfigureAwait(false);
            if (!IsProbeInventory(read.Value, intent) || string.IsNullOrWhiteSpace(read.ETag))
            {
                throw new InvalidOperationException("memory-ingestion-probe-roundtrip-failed");
            }

            primaryFailed = false;
        }
        finally
        {
            try
            {
                // A failed/cancelled write can have committed. Always read back and dispose only
                // this invocation's exact reserved sentinel under an independent bounded budget.
                using var cleanupBudget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                ReadModelEntry<PartyMemoryIngestionInventory> current = await store.GetAsync<PartyMemoryIngestionInventory>(
                    _storeName, key, cleanupBudget.Token).ConfigureAwait(false);
                if (current.Value is not null)
                {
                    if (!IsProbeInventory(current.Value, intent) || string.IsNullOrWhiteSpace(current.ETag)
                        || !await eraser.TryEraseAsync(_storeName, key, current.ETag, cleanupBudget.Token).ConfigureAwait(false))
                    {
                        throw new InvalidOperationException("memory-ingestion-probe-cleanup-failed");
                    }
                }

                ReadModelEntry<PartyMemoryIngestionInventory> absent = await store.GetAsync<PartyMemoryIngestionInventory>(
                    _storeName, key, cleanupBudget.Token).ConfigureAwait(false);
                if (absent.Value is not null || !string.IsNullOrEmpty(absent.ETag))
                {
                    throw new InvalidOperationException("memory-ingestion-probe-cleanup-unverified");
                }
            }
            catch (Exception) when (primaryFailed)
            {
                // Preserve the primary read/write/cancellation failure, including its stack.
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static bool IsProbeInventory(PartyMemoryIngestionInventory? inventory, PartyMemoryIngestionIntent intent)
        => inventory is { CleanupRequested: false, PermanentCleanup: false, Generation: 0 }
            && inventory.Intents.SequenceEqual([intent]) && (inventory.CleanupReceipts?.Count ?? 0) == 0;

    /// <inheritdoc/>
    public async Task<PartyMemoryIngestionRegistration> RegisterAsync(string tenantId, string partyId, PartyMemoryIngestionIntent intent, CancellationToken cancellationToken)
    {
        bool created = false;
        string attemptId = intent.AttemptId;
        PartyMemoryIngestionInventory saved = await ChangeAsync(tenantId, partyId, current =>
        {
            created = false;
            if (current.CleanupRequested)
            {
                throw new InvalidOperationException("memory-ingestion-cleanup-requested");
            }

            attemptId = current.Generation == 0 ? intent.AttemptId : Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes(intent.AttemptId + ":" + current.Generation.ToString(CultureInfo.InvariantCulture))));
            PartyMemoryIngestionIntent? existing = current.Intents.Find(item => item.AttemptId == attemptId);
            if (existing is not null)
            {
                for (int hop = 0; existing.SuccessorAttemptId is not null && hop < Capacity; hop++)
                {
                    existing = current.Intents.Single(item => item.AttemptId == existing.SuccessorAttemptId);
                    attemptId = existing.AttemptId;
                }
                if (existing.SuccessorAttemptId is not null) { throw new InvalidOperationException("memory-ingestion-successor-cycle"); }
                if (existing.CaseId != intent.CaseId || existing.SourceUri != intent.SourceUri
                    || (intent.ProducerAuthority is not null && existing.ProducerAuthority != intent.ProducerAuthority)
                    || (intent.RefreshProtocolVersion is not null && (existing.RefreshProtocolVersion != intent.RefreshProtocolVersion
                        || existing.SourceRevisionNumber != intent.SourceRevisionNumber || string.IsNullOrWhiteSpace(existing.RefreshIdentityDigest))))
                {
                    throw new InvalidOperationException("memory-ingestion-intent-identity-mismatch");
                }

                if (existing.SubmissionRejected)
                {
                    if (existing.WorkflowInstanceId is not null || existing.MemoryUnitId is not null)
                    { throw new InvalidOperationException("memory-ingestion-rejected-attempt-has-producer"); }
                    created = true;
                    // Refresh preparation keeps non-admission proof until the final claim.
                    // Already submitted or ambiguous attempts never enter this branch.
                    return current with { Intents = [.. current.Intents.Select(item => item.AttemptId == attemptId
                        ? item with { SubmissionRejected = intent.SubmissionRejected, RefreshIdentityDigest = intent.RefreshIdentityDigest } : item)] };
                }

                return current;
            }

            if (current.Intents.Count + (current.CleanupReceipts?.Count ?? 0) >= Capacity)
            {
                throw new InvalidOperationException("memory-ingestion-intent-capacity");
            }

            created = true;
            return current with { Intents = [.. current.Intents, intent with { AttemptId = attemptId, Generation = current.Generation }] };
        }, cancellationToken).ConfigureAwait(false);
        return new(saved.Intents.Single(item => item.AttemptId == attemptId), created);
    }

    /// <inheritdoc/>
    public async Task<bool> TryClaimSubmissionAsync(string tenantId, string partyId, PartyMemoryIngestionIntent prepared, CancellationToken cancellationToken)
    {
        bool claimed = false;
        _ = await ChangeAsync(tenantId, partyId, current =>
        {
            claimed = !current.CleanupRequested && current.Generation == prepared.Generation
                && prepared is { SubmissionRejected: true, WorkflowInstanceId: null, MemoryUnitId: null, SuccessorAttemptId: null }
                && current.Intents.Contains(prepared);
            return claimed ? current with { Intents = [.. current.Intents.Select(item => item == prepared
                ? item with { SubmissionRejected = false } : item)] } : current;
        }, cancellationToken).ConfigureAwait(false);
        return claimed;
    }

    /// <inheritdoc/>
    public async Task RegisterSupersededSuccessorAsync(string tenantId, string partyId, PartyMemoryIngestionIntent prior,
        string supersededOutputId, string? currentPredecessor, CancellationToken cancellationToken)
    {
        _ = await ChangeAsync(tenantId, partyId, current =>
        {
            if (current.CleanupRequested || current.Generation != prior.Generation) { throw new InvalidOperationException("memory-ingestion-cleanup-requested"); }
            PartyMemoryIngestionIntent existing = current.Intents.Single(item => item.AttemptId == prior.AttemptId);
            if (existing.SuccessorAttemptId is not null) { return current; }
            if (existing.WorkflowInstanceId != prior.WorkflowInstanceId || existing.CaseId != prior.CaseId || existing.SourceUri != prior.SourceUri
                || existing.MemoryUnitId is not null || string.IsNullOrWhiteSpace(supersededOutputId))
            {
                throw new InvalidOperationException("memory-ingestion-superseded-identity-mismatch");
            }
            if (current.Intents.Count + (current.CleanupReceipts?.Count ?? 0) >= Capacity) { throw new InvalidOperationException("memory-ingestion-intent-capacity"); }
            string successorId = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
                { Prior = prior.AttemptId, CurrentPredecessor = currentPredecessor })));
            var successor = new PartyMemoryIngestionIntent(successorId, prior.CaseId, prior.SourceUri, Generation: current.Generation,
                SubmissionRejected: true, ProducerAuthority: prior.ProducerAuthority, RefreshProtocolVersion: prior.RefreshProtocolVersion,
                SourceRevisionNumber: prior.SourceRevisionNumber, RefreshIdentityDigest: prior.RefreshIdentityDigest);
            return current with { Intents = [.. current.Intents.Select(item => item == existing ? item with
            {
                MemoryUnitId = supersededOutputId, RequiresTerminalEvidence = false, SuccessorAttemptId = successorId,
            } : item), successor] };
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(string tenantId, string partyId, PartyMemoryIngestionIntent intent, CancellationToken cancellationToken)
    {
        _ = await ChangeAsync(tenantId, partyId, current =>
        {
            PartyMemoryIngestionIntent existing = current.Intents.Concat(current.CleanupReceipts ?? [])
                .FirstOrDefault(item => item.AttemptId == intent.AttemptId)
                ?? throw new InvalidOperationException("memory-ingestion-intent-missing");
            if (existing.CaseId != intent.CaseId || existing.SourceUri != intent.SourceUri
                || existing.Generation != intent.Generation
                || existing.ProducerAuthority != intent.ProducerAuthority
                || existing.AbsenceAuthority != intent.AbsenceAuthority
                || existing.RefreshProtocolVersion != intent.RefreshProtocolVersion
                || existing.SourceRevisionNumber != intent.SourceRevisionNumber
                || existing.RefreshIdentityDigest != intent.RefreshIdentityDigest
                || (existing.WorkflowInstanceId is not null && existing.WorkflowInstanceId != intent.WorkflowInstanceId)
                || (existing.MemoryUnitId is not null && existing.MemoryUnitId != intent.MemoryUnitId)
                || (intent.SubmissionRejected && (intent.WorkflowInstanceId is not null || intent.MemoryUnitId is not null)))
            {
                throw new InvalidOperationException("memory-ingestion-intent-identity-mismatch");
            }

            if (existing.SuccessorAttemptId is not null) { return current; }
            // A delayed recovery write cannot unset an already verified absence receipt.
            if ((current.CleanupReceipts ?? []).Any(item => item.AttemptId == intent.AttemptId))
            {
                return current;
            }

            if (intent.CleanupSettled)
            {
                throw new InvalidOperationException("memory-ingestion-intent-identity-mismatch");
            }

            return current with
            {
                Intents = [.. current.Intents.Select(item => item.AttemptId == intent.AttemptId ? intent with { TerminalObservationId = existing.TerminalObservationId } : item)],
            };
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PartyMemoryIngestionIntent>> BeginCleanupAsync(string tenantId, string partyId, CancellationToken cancellationToken, bool permanent = true)
    {
        PartyMemoryIngestionInventory inventory = await ChangeAsync(tenantId, partyId, current => current with
        {
            CleanupRequested = true,
            PermanentCleanup = current.PermanentCleanup || permanent,
        }, cancellationToken).ConfigureAwait(false);
        // Retained receipts reconcile mappings published by a writer after an earlier cleanup.
        return CleanupSnapshot(inventory);
    }

    /// <inheritdoc/>
    public async Task ConfirmDirectCleanupAsync(string tenantId, string partyId, string caseId,
        IReadOnlyCollection<PartyMemoryIngestionIntent> expected, IReadOnlyList<PartyMemoryIngestionIntent> confirmed,
        string producerAuthority, CancellationToken cancellationToken)
    {
        HashSet<PartyMemoryIngestionIntent> expectedItems = [.. expected];
        HashSet<PartyMemoryIngestionIntent> targets = [.. confirmed];
        if (!targets.IsSubsetOf(expectedItems)
            || targets.Any(item => item.CleanupSettled || item.CaseId != caseId || item.ProducerAuthority != producerAuthority))
        {
            throw new InvalidOperationException("memory-ingestion-direct-cleanup-scope-mismatch");
        }

        _ = await ChangeAsync(tenantId, partyId, current =>
        {
            RequireCleanupSnapshot(current, caseId, expectedItems);

            // The conditional write arbitrates with inventory imports on the same durable key.
            return targets.Count == 0 ? current : Settle(current, targets, producerAuthority);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<PartyMemoryIngestionIntent> SetTerminalObservationAsync(string tenantId, string partyId,
        PartyMemoryIngestionIntent snapshot, string? observationId, CancellationToken cancellationToken)
        => ReplaceCleanupIntentAsync(tenantId, partyId, snapshot, snapshot with { TerminalObservationId = observationId }, cancellationToken);

    /// <inheritdoc/>
    public Task<PartyMemoryIngestionIntent> RetainTerminalStatusAsync(string tenantId, string partyId,
        PartyMemoryIngestionIntent snapshot, string canonicalId, string producerAuthority, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(producerAuthority);
        if (snapshot.CleanupSettled || snapshot.SubmissionRejected || string.IsNullOrWhiteSpace(snapshot.WorkflowInstanceId)
            || (snapshot.ProducerAuthority is not null && snapshot.ProducerAuthority != producerAuthority)
            || (snapshot.MemoryUnitId is not null && snapshot.MemoryUnitId != canonicalId))
        {
            throw new InvalidOperationException("memory-ingestion-terminal-status-identity-mismatch");
        }

        PartyMemoryIngestionIntent retained = snapshot with
        {
            MemoryUnitId = canonicalId,
            RequiresTerminalEvidence = false,
            ProducerAuthority = producerAuthority,
            TerminalObservationId = null,
        };
        return ReplaceCleanupIntentAsync(tenantId, partyId, snapshot, retained, cancellationToken);
    }

    private async Task<PartyMemoryIngestionIntent> ReplaceCleanupIntentAsync(string tenantId, string partyId,
        PartyMemoryIngestionIntent snapshot, PartyMemoryIngestionIntent retained, CancellationToken cancellationToken)
    {
        _ = await ChangeAsync(tenantId, partyId, current => current.CleanupRequested && current.Intents.Contains(snapshot)
            ? current with { Intents = [.. current.Intents.Select(item => item == snapshot ? retained : item)] }
            : throw new InvalidOperationException("memory-ingestion-cleanup-snapshot-changed"), cancellationToken).ConfigureAwait(false);
        return retained;
    }

    /// <inheritdoc/>
    public async Task<PartyMemoryIngestionIntent> RetainLegacyAsync(string tenantId, string partyId,
        PartyMemoryUnitMappingEntry mapping, string caseId, string canonicalId, CancellationToken cancellationToken,
        string? producerAuthority = null, bool requirePending = false,
        IReadOnlyCollection<PartyMemoryIngestionIntent>? expectedCase = null)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalId);
        string attemptId = "legacy-" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { mapping.MemoryUnitId, mapping.SourceUri, CaseId = caseId, ProducerAuthority = producerAuthority })));
        PartyMemoryIngestionIntent? retained = null;
        _ = await ChangeAsync(tenantId, partyId, current =>
        {
            if (!current.CleanupRequested)
            {
                throw new InvalidOperationException("memory-ingestion-cleanup-not-requested");
            }

            if (requirePending && (current.CleanupReceipts ?? []).Any(item => item.AttemptId == attemptId))
            {
                throw new InvalidOperationException("memory-ingestion-legacy-snapshot-changed");
            }

            PartyMemoryIngestionIntent? receipt = requirePending ? null : (current.CleanupReceipts ?? []).FirstOrDefault(item =>
                item.CaseId == caseId && item.MemoryUnitId == canonicalId
                && !string.IsNullOrWhiteSpace(producerAuthority) && item.AbsenceAuthority == producerAuthority);
            if (receipt is not null && expectedCase is not null)
            {
                RequireCleanupSnapshot(current, caseId, expectedCase);
            }

            if (receipt is not null && (receipt.WorkflowInstanceId == mapping.MemoryUnitId || receipt.MemoryUnitId == mapping.MemoryUnitId))
            {
                retained = receipt with { CleanupSettled = true };
                return current;
            }

            PartyMemoryIngestionIntent? existing = current.Intents.Find(item => item.AttemptId == attemptId);
            if (existing is not null)
            {
                if (existing.CaseId != caseId || existing.SourceUri != mapping.SourceUri
                    || existing.WorkflowInstanceId != mapping.MemoryUnitId || existing.MemoryUnitId != canonicalId)
                {
                    throw new InvalidOperationException("memory-ingestion-intent-identity-mismatch");
                }

                retained = existing;
                return current;
            }

            if (current.Intents.Count + (current.CleanupReceipts?.Count ?? 0) >= Capacity)
            {
                throw new InvalidOperationException("memory-ingestion-intent-capacity");
            }

            retained = new(attemptId, caseId, mapping.SourceUri, mapping.MemoryUnitId, canonicalId, current.Generation,
                CleanupSettled: receipt is not null, ProducerAuthority: producerAuthority, AbsenceAuthority: receipt?.AbsenceAuthority);
            // A different legacy workflow still needs its own durable association, even
            // when an existing canonical receipt supplies the already verified absence.
            return receipt is null
                ? current with { Intents = [.. current.Intents, retained] }
                : current with { CleanupReceipts = [.. current.CleanupReceipts ?? [], retained] };
        }, cancellationToken).ConfigureAwait(false);
        return retained!;
    }

    /// <inheritdoc/>
    public async Task<bool> RetainDiscoveredAsync(string tenantId, string partyId, string caseId, string sourceUri, string? workflowId,
        string? canonicalId, string? retryIdentityHash, bool terminalVerified, CancellationToken cancellationToken, string? producerAuthority = null,
        bool requireTerminalEvidence = false, (string? ProtocolVersion, long? SourceRevision, string? IdentityDigest) admission = default,
        bool unitPresent = false)
    {
        bool retained = false;
        _ = await ChangeAsync(tenantId, partyId, current =>
        {
            if (!current.CleanupRequested || sourceUri != PartyMemoryUrn.Build(tenantId, partyId)
                || string.IsNullOrWhiteSpace(caseId) || (string.IsNullOrWhiteSpace(workflowId) && string.IsNullOrWhiteSpace(canonicalId))
                || (unitPresent && (workflowId is not null || string.IsNullOrWhiteSpace(producerAuthority))))
            {
                throw new InvalidOperationException("memory-ingestion-remote-scope-mismatch");
            }
            PartyMemoryIngestionIntent? typedRetry = retryIdentityHash is null ? null : current.Intents.Find(item =>
                PartyMemoryIngestionEvidence.HasTypedRefreshAdmission(item)
                && (item.WorkflowInstanceId is null || admission.ProtocolVersion is not null)
                && PartyMemoryIngestionEvidence.RetryIdentityHash(item.AttemptId) == retryIdentityHash);
            if (typedRetry is not null && (typedRetry.CaseId != caseId || typedRetry.SourceUri != sourceUri || workflowId is null
                || (typedRetry.WorkflowInstanceId is not null && typedRetry.WorkflowInstanceId != workflowId)
                || requireTerminalEvidence || string.IsNullOrWhiteSpace(producerAuthority) || typedRetry.ProducerAuthority != producerAuthority
                || !PartyMemoryIngestionEvidence.MatchesRefreshAdmission(typedRetry,
                    admission.ProtocolVersion, admission.SourceRevision, admission.IdentityDigest)))
            {
                retained = false;
                return current;
            }

            retained = true;
            List<PartyMemoryIngestionIntent> receipts = current.CleanupReceipts ?? [];
            PartyMemoryIngestionIntent? existing = typedRetry ?? current.Intents.Concat(unitPresent ? [] : receipts).FirstOrDefault(item =>
                item.CaseId == caseId && item.SourceUri == sourceUri
                && (current.Intents.Contains(item) ? item.ProducerAuthority is null || item.ProducerAuthority == producerAuthority
                    : !string.IsNullOrWhiteSpace(producerAuthority) && item.AbsenceAuthority == producerAuthority)
                && ((workflowId is not null && item.WorkflowInstanceId == workflowId)
                    || (workflowId is null && item.MemoryUnitId == canonicalId)
                    || (item.WorkflowInstanceId is null && retryIdentityHash is not null
                        && PartyMemoryIngestionEvidence.RetryIdentityHash(item.AttemptId) == retryIdentityHash)));
            // A unit-only inventory row can precede the workflow row on another page.
            // Attach that observation to its producer without merging independent workflows.
            PartyMemoryIngestionIntent? unitObservation = workflowId is null || canonicalId is null ? null
                : current.Intents.Find(item => item.WorkflowInstanceId is null && item.MemoryUnitId == canonicalId
                    && item.CaseId == caseId && item.SourceUri == sourceUri && item.ProducerAuthority == producerAuthority
                    && item.AttemptId.StartsWith("inventory-", StringComparison.Ordinal));
            existing ??= unitObservation;
            if (existing is not null)
            {
                if (existing.MemoryUnitId is not null && canonicalId is not null && existing.MemoryUnitId != canonicalId)
                {
                    throw new InvalidOperationException("memory-ingestion-remote-identity-mismatch");
                }
                if (receipts.Contains(existing)) { return current; }
                bool unverifiedLegacyWorkflow = existing.ProducerAuthority is null && existing.WorkflowInstanceId is not null
                    && (workflowId is null || canonicalId is null || !terminalVerified);
                PartyMemoryIngestionIntent captured = existing with
                {
                    SubmissionRejected = false,
                    ProducerAuthority = existing.ProducerAuthority
                        ?? (workflowId is null && existing.WorkflowInstanceId is not null ? null : producerAuthority),
                    WorkflowInstanceId = existing.WorkflowInstanceId ?? workflowId,
                    MemoryUnitId = existing.MemoryUnitId ?? canonicalId,
                    CleanupRevision = unitPresent && existing.ProducerAuthority == producerAuthority
                        ? checked(existing.CleanupRevision + 1) : existing.CleanupRevision,
                    TerminalObservationId = terminalVerified && workflowId is not null && canonicalId is not null
                        ? null : existing.TerminalObservationId,
                    RequiresTerminalEvidence = requireTerminalEvidence || unverifiedLegacyWorkflow || (workflowId is null ? existing.RequiresTerminalEvidence
                        : !terminalVerified && (existing.RequiresTerminalEvidence || existing.WorkflowInstanceId is null || existing.MemoryUnitId is null)),
                };
                return current with { Intents = [.. current.Intents.Where(item => item == existing || item != unitObservation)
                    .Select(item => item == existing ? captured : item)] };
            }
            string attemptId = "inventory-" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
                new { CaseId = caseId, WorkflowId = workflowId, CanonicalId = workflowId is null ? canonicalId : null, SourceUri = sourceUri, ProducerAuthority = producerAuthority })));
            // Reopen the exact inventory slot, including any workflow alias adopted later.
            // Separate workflow receipts normally stay historical, but an exact scoped receipt
            // can carry the current obligation when no new slot is available.
            bool atCapacity = current.Intents.Count + receipts.Count >= Capacity;
            if (unitPresent)
            {
                PartyMemoryIngestionIntent? reopened = receipts.Find(item => item.AttemptId == attemptId && MatchesUnitReceipt(item));
                if (reopened is null && atCapacity)
                {
                    reopened = receipts.Find(MatchesUnitReceipt);
                }

                if (reopened is not null)
                {
                    return current with
                    {
                        Intents = [.. current.Intents, reopened with
                        {
                            CleanupSettled = false,
                            AbsenceAuthority = null,
                            CleanupRevision = checked(reopened.CleanupRevision + 1),
                        }],
                        CleanupReceipts = [.. receipts.Where(item => item != reopened)],
                    };
                }
            }

            if (atCapacity)
            {
                throw new InvalidOperationException("memory-ingestion-intent-capacity");
            }

            var discovered = new PartyMemoryIngestionIntent(attemptId, caseId, sourceUri, workflowId, canonicalId, current.Generation,
                RequiresTerminalEvidence: workflowId is not null && !terminalVerified, ProducerAuthority: producerAuthority);
            return current with { Intents = [.. current.Intents, discovered] };
        }, cancellationToken).ConfigureAwait(false);
        return retained;

        bool MatchesUnitReceipt(PartyMemoryIngestionIntent receipt) => !receipt.SubmissionRejected
            && receipt.CaseId == caseId && receipt.SourceUri == sourceUri && receipt.MemoryUnitId == canonicalId
            && receipt.ProducerAuthority == producerAuthority && receipt.AbsenceAuthority == producerAuthority;
    }

    /// <inheritdoc/>
    public async Task<string?> GetSourceClosureVersionAsync(string tenantId, string partyId, CancellationToken cancellationToken, string? producerAuthority = null)
    {
        PartyMemoryIngestionInventory? current = (await store.GetAsync<PartyMemoryIngestionInventory>(
            _storeName, Key(tenantId, partyId), cancellationToken).ConfigureAwait(false)).Value;
        return current is { CleanupRequested: true, PermanentCleanup: true, Intents.Count: 0 }
            && !string.IsNullOrWhiteSpace(producerAuthority) && current.SourceClosureAuthority == producerAuthority
            ? current.SourceClosureVersion : null;
    }

    /// <inheritdoc/>
    public async Task RetainSourceClosureVersionAsync(string tenantId, string partyId, string version, CancellationToken cancellationToken, string? producerAuthority = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(producerAuthority);
        _ = await ChangeAsync(tenantId, partyId, current => current is { CleanupRequested: true, PermanentCleanup: true, Intents.Count: 0 }
            ? current with { SourceClosureVersion = version, SourceClosureAuthority = producerAuthority }
            : throw new InvalidOperationException("memory-source-closure-not-settled"), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> CanPublishAsync(string tenantId, string partyId, PartyMemoryIngestionIntent intent, CancellationToken cancellationToken)
    {
        PartyMemoryIngestionInventory? current = (await store.GetAsync<PartyMemoryIngestionInventory>(
            _storeName, Key(tenantId, partyId), cancellationToken).ConfigureAwait(false)).Value;
        return current is { CleanupRequested: false, PermanentCleanup: false }
            && current.Generation == intent.Generation && current.Intents.Contains(intent)
            && !(current.CleanupReceipts ?? []).Any(receipt => receipt.CaseId == intent.CaseId
                && receipt.MemoryUnitId is not null && receipt.MemoryUnitId == intent.MemoryUnitId);
    }

    /// <inheritdoc/>
    public async Task<bool> TryReopenAsync(string tenantId, string partyId, CancellationToken cancellationToken)
    {
        PartyMemoryIngestionInventory? observed = (await store.GetAsync<PartyMemoryIngestionInventory>(
            _storeName, Key(tenantId, partyId), cancellationToken).ConfigureAwait(false)).Value;
        if (observed is null || !observed.CleanupRequested)
        {
            return true;
        }

        if (observed.PermanentCleanup || observed.Intents.Count != 0)
        {
            return false;
        }

        string projectionStore = projectionOptions?.Value.ReadModelStateStoreName ?? "statestore";
        PartyIndexSdkReadModel? canonical = (await store.GetAsync<PartyIndexSdkReadModel>(projectionStore,
            PartySdkReadModelAddresses.Index(tenantId), cancellationToken).ConfigureAwait(false)).Value;
        if (canonical is not { Entries: not null, ErasureSequenceNumbers: not null, LastIndexedEvents: not null, LastSequenceNumbers: not null }
            || canonical.ErasureSequenceNumbers.ContainsKey(partyId)
            || !canonical.Entries.TryGetValue(partyId, out var entry)
            || entry is null || entry.IsErased || entry.Id != partyId)
        {
            return false;
        }

        PartyMemoryIngestionInventory saved = await ChangeAsync(tenantId, partyId, current =>
            current.CleanupRequested && !current.PermanentCleanup && current.Intents.Count == 0
                && current.Generation == observed.Generation
                ? current with { CleanupRequested = false, Generation = checked(current.Generation + 1) }
                : current, cancellationToken).ConfigureAwait(false);
        return !saved.CleanupRequested && !saved.PermanentCleanup;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PartyMemoryIngestionIntent>> GetAsync(string tenantId, string partyId, CancellationToken cancellationToken)
        => (await store.GetAsync<PartyMemoryIngestionInventory>(_storeName, Key(tenantId, partyId), cancellationToken).ConfigureAwait(false)).Value?.Intents ?? [];

    /// <inheritdoc/>
    public async Task<int> RemoveAsync(string tenantId, string partyId, IReadOnlyList<PartyMemoryIngestionIntent> intents, CancellationToken cancellationToken, string? absenceAuthority = null)
    {
        HashSet<PartyMemoryIngestionIntent> targets = [.. intents];
        return (await ChangeAsync(tenantId, partyId, current => Settle(current, targets, absenceAuthority),
            cancellationToken).ConfigureAwait(false)).Intents.Count;
    }

    private static void RequireCleanupSnapshot(PartyMemoryIngestionInventory inventory, string caseId,
        IReadOnlyCollection<PartyMemoryIngestionIntent> expected)
    {
        if (!inventory.CleanupRequested || !CleanupSnapshot(inventory).Where(item => item.CaseId == caseId).ToHashSet().SetEquals(expected))
        {
            throw new InvalidOperationException("memory-ingestion-cleanup-snapshot-changed");
        }
    }

    private static IReadOnlyList<PartyMemoryIngestionIntent> CleanupSnapshot(PartyMemoryIngestionInventory inventory)
        => [.. inventory.Intents, .. (inventory.CleanupReceipts ?? []).Select(item => item with { CleanupSettled = true })];

    private static PartyMemoryIngestionInventory Settle(PartyMemoryIngestionInventory current,
        HashSet<PartyMemoryIngestionIntent> targets, string? absenceAuthority)
        => current with
        {
            Intents = [.. current.Intents.Where(item => !targets.Contains(item))],
            // Preserve provenance rather than waiting for process-local writers or guessing that
            // a crashed writer cannot resume. A late mapping remains resolvable after restart.
            CleanupReceipts = [.. (current.CleanupReceipts ?? []).Concat(current.Intents.Where(targets.Contains)
                .Select(item => item with { CleanupSettled = true, AbsenceAuthority = absenceAuthority })).Distinct()],
        };

    private Task<PartyMemoryIngestionInventory> ChangeAsync(string tenantId, string partyId, Func<PartyMemoryIngestionInventory, PartyMemoryIngestionInventory> change, CancellationToken cancellationToken)
        => ReadModelWritePolicy.UpdateAsync<PartyMemoryIngestionInventory>(store, _storeName, Key(tenantId, partyId),
            current => change(current ?? new(false, [])), maxAttempts: 5, cancellationToken: cancellationToken);

    private static string Key(string tenantId, string partyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return PartyMemoryUnitMappingStore.BuildKey(tenantId, partyId).Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal);
    }
}
