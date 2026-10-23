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
                if (existing.CaseId != intent.CaseId || existing.SourceUri != intent.SourceUri)
                {
                    throw new InvalidOperationException("memory-ingestion-intent-identity-mismatch");
                }

                if (existing.SubmissionRejected)
                {
                    created = true;
                    return current with { Intents = [.. current.Intents.Select(item => item.AttemptId == attemptId
                        ? item with { SubmissionRejected = false } : item)] };
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
    public async Task UpdateAsync(string tenantId, string partyId, PartyMemoryIngestionIntent intent, CancellationToken cancellationToken)
    {
        _ = await ChangeAsync(tenantId, partyId, current =>
        {
            PartyMemoryIngestionIntent existing = current.Intents.Concat(current.CleanupReceipts ?? [])
                .FirstOrDefault(item => item.AttemptId == intent.AttemptId)
                ?? throw new InvalidOperationException("memory-ingestion-intent-missing");
            if (existing.CaseId != intent.CaseId || existing.SourceUri != intent.SourceUri
                || existing.Generation != intent.Generation
                || (existing.WorkflowInstanceId is not null && existing.WorkflowInstanceId != intent.WorkflowInstanceId)
                || (existing.MemoryUnitId is not null && existing.MemoryUnitId != intent.MemoryUnitId)
                || (intent.SubmissionRejected && (intent.WorkflowInstanceId is not null || intent.MemoryUnitId is not null)))
            {
                throw new InvalidOperationException("memory-ingestion-intent-identity-mismatch");
            }

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
                Intents = [.. current.Intents.Select(item => item.AttemptId == intent.AttemptId ? intent : item)],
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
        return [.. inventory.Intents, .. (inventory.CleanupReceipts ?? []).Select(item => item with { CleanupSettled = true })];
    }

    /// <inheritdoc/>
    public async Task<PartyMemoryIngestionIntent> RetainLegacyAsync(string tenantId, string partyId,
        PartyMemoryUnitMappingEntry mapping, string caseId, string canonicalId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalId);
        string attemptId = "legacy-" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { mapping.MemoryUnitId, mapping.SourceUri, CaseId = caseId })));
        PartyMemoryIngestionIntent? retained = null;
        _ = await ChangeAsync(tenantId, partyId, current =>
        {
            if (!current.CleanupRequested)
            {
                throw new InvalidOperationException("memory-ingestion-cleanup-not-requested");
            }

            PartyMemoryIngestionIntent? receipt = (current.CleanupReceipts ?? []).FirstOrDefault(item =>
                item.CaseId == caseId && item.MemoryUnitId == canonicalId);
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
                CleanupSettled: receipt is not null);
            // A different legacy workflow still needs its own durable association, even
            // when an existing canonical receipt supplies the already verified absence.
            return receipt is null
                ? current with { Intents = [.. current.Intents, retained] }
                : current with { CleanupReceipts = [.. current.CleanupReceipts ?? [], retained] };
        }, cancellationToken).ConfigureAwait(false);
        return retained!;
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
    public async Task<int> RemoveAsync(string tenantId, string partyId, IReadOnlyList<PartyMemoryIngestionIntent> intents, CancellationToken cancellationToken)
    {
        HashSet<PartyMemoryIngestionIntent> targets = [.. intents];
        return (await ChangeAsync(tenantId, partyId, current => current with
        {
            Intents = [.. current.Intents.Where(item => !targets.Contains(item))],
            // Preserve provenance rather than waiting for process-local writers or guessing that
            // a crashed writer cannot resume. A late mapping remains resolvable after restart.
            CleanupReceipts = [.. (current.CleanupReceipts ?? []).Concat(current.Intents.Where(targets.Contains)
                .Select(item => item with { CleanupSettled = true })).Distinct()],
        }, cancellationToken).ConfigureAwait(false)).Intents.Count;
    }

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
