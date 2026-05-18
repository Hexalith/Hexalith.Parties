using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Dapr.Client;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Search;

/// <summary>
/// Per-party → memory-unit-id mapping. Recorded by the indexing service after successful
/// <c>IngestAsync</c> calls; consumed by the cleanup service so erasure can iterate per-unit
/// DELETEs against the existing per-unit Memories endpoint. Implements the AC5 cleanup
/// re-architecture described in resolved decision #2 of the story spec — without this
/// mapping the erasure flow has no way to know which memory unit ids belong to a party.
/// </summary>
internal sealed record PartyMemoryUnitMappingEntry(string MemoryUnitId, string SourceUri);

internal interface IPartyMemoryUnitMappingStore
{
    Task RecordMappingAsync(
        string tenantId,
        string partyId,
        string memoryUnitId,
        string sourceUri,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken);

    Task ClearMappingsAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replace the full mapping list for a party. Used by cleanup to atomically remove
    /// successfully-deleted units (P17 / P25) — pass the remaining unfailed entries so a
    /// re-run picks up exactly where this run left off. An empty list deletes the state key
    /// entirely (equivalent to <see cref="ClearMappingsAsync"/>).
    /// </summary>
    Task ReplaceMappingsAsync(
        string tenantId,
        string partyId,
        IReadOnlyList<PartyMemoryUnitMappingEntry> entries,
        CancellationToken cancellationToken);
}

/// <summary>
/// Dapr-state-store backed mapping. Storage shape: a single state key per (tenant, party)
/// holding a list of <see cref="PartyMemoryUnitMappingEntry"/>.
/// <para>
/// <b>Memory-unit-id contract (P40 / resolved decision D1).</b> The Memories server uses the
/// DAPR workflow instance id returned by <c>POST /api/ingest</c> as the canonical
/// <c>memoryUnitId</c> for the resulting memory unit (verified against
/// <c>Hexalith.Memories.Server.Workflows.IngestionWorkflow.ResolveMemoryUnitId</c>). The
/// per-unit cleanup endpoint <c>DELETE /api/tenants/{t}/cases/{c}/memory-units/{memoryUnitId}</c>
/// is keyed by that same id, so recording the workflow instance id as <c>memoryUnitId</c>
/// here is correct by design. The only divergence path is <c>SourceType.Event</c> with the
/// dedup-instance prefix, but the Memories SDK's <c>IngestAsync</c> hard-codes
/// <c>SourceType.File</c>, so the simple mapping always holds for Parties.
/// </para>
/// <para>
/// <b>Concurrency (P3).</b> Updates use ETag-based optimistic concurrency with bounded retry
/// so two concurrent indexings of the same party cannot lose a mapping under last-writer-wins.
/// </para>
/// <para>
/// <b>Failure surface (P2 / P24).</b> State-store failures during writes propagate to the
/// caller so <see cref="PartyMemoryIndexingService"/> can surface them as <c>Indexed: false</c>
/// (with compensating cleanup of the just-ingested unit). Reads degrade to "no mapping" with
/// a structured warning so cleanup never fails-closed on a transient read outage.
/// </para>
/// </summary>
internal sealed class PartyMemoryUnitMappingStore(
    DaprClient daprClient,
    IOptions<PartyMemoryUnitMappingStoreOptions> options,
    ILogger<PartyMemoryUnitMappingStore> logger) : IPartyMemoryUnitMappingStore
{
    private const int MaxConcurrencyRetries = 5;

    private readonly string _stateStoreName = options.Value.StateStoreName;

    public async Task RecordMappingAsync(
        string tenantId,
        string partyId,
        string memoryUnitId,
        string sourceUri,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryUnitId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUri);

        string key = BuildKey(tenantId, partyId);

        for (int attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (IReadOnlyList<PartyMemoryUnitMappingEntry> current, string etag) =
                await ReadWithETagAsync(key, cancellationToken).ConfigureAwait(false);

            // Dedup by both MemoryUnitId AND SourceUri. Memories returns the same memory-unit-id
            // for repeated ingests of the same source URI (per the contract documented above),
            // but if that property ever drifts (Memories rebuild, contract change), checking
            // SourceUri lets us replace the stale id rather than appending unbounded ghosts.
            List<PartyMemoryUnitMappingEntry> updated = [];
            bool replaced = false;
            foreach (PartyMemoryUnitMappingEntry existing in current)
            {
                if (string.Equals(existing.MemoryUnitId, memoryUnitId, StringComparison.Ordinal))
                {
                    // Already recorded with the same id — no write needed.
                    return;
                }

                if (string.Equals(existing.SourceUri, sourceUri, StringComparison.Ordinal))
                {
                    // Same source URI but a different memory-unit-id: replace with the latest
                    // id rather than appending; otherwise cleanup would issue a DELETE for the
                    // stale id (which 404s) and miss the live one if it ever differs.
                    updated.Add(new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri));
                    replaced = true;
                    continue;
                }

                updated.Add(existing);
            }

            if (!replaced)
            {
                updated.Add(new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri));
            }

            bool success = await daprClient
                .TrySaveStateAsync(_stateStoreName, key, updated, etag, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (success)
            {
                return;
            }

            logger.LogDebug(
                "ETag conflict recording memory-unit mapping for {TenantId}/{PartyId} (attempt {Attempt}/{Max}); retrying.",
                tenantId,
                partyId,
                attempt + 1,
                MaxConcurrencyRetries);
        }

        // All retries exhausted — propagate so the indexing service can compensate by
        // deleting the just-ingested memory unit and reporting a blocked indexing result.
        // Swallowing here would silently orphan a memory unit in Memories with no Parties-side
        // record, breaking AC5 GDPR cleanup.
        throw new InvalidOperationException(
            $"Failed to record memory-unit mapping for {tenantId}/{partyId} after {MaxConcurrencyRetries} concurrency retries.");
    }

    public async Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        (IReadOnlyList<PartyMemoryUnitMappingEntry> current, _) =
            await ReadWithETagAsync(BuildKey(tenantId, partyId), cancellationToken).ConfigureAwait(false);
        return current;
    }

    public async Task ClearMappingsAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        try
        {
            await daprClient
                .DeleteStateAsync(_stateStoreName, BuildKey(tenantId, partyId), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Propagate so the cleanup orchestrator can record the failure in the aggregate
            // BlockedReason rather than reporting "Cleaned: true" with stale mapping state.
            // The previous swallow left ghost mapping entries that misled subsequent re-runs.
            logger.LogWarning(
                ex,
                "Failed to clear memory-unit mapping for {TenantId}/{PartyId}; surfacing as cleanup failure.",
                tenantId,
                partyId);
            throw new InvalidOperationException(
                $"Failed to clear memory-unit mapping for {tenantId}/{partyId}: {ex.GetType().Name}",
                ex);
        }
    }

    public async Task ReplaceMappingsAsync(
        string tenantId,
        string partyId,
        IReadOnlyList<PartyMemoryUnitMappingEntry> entries,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            await ClearMappingsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
            return;
        }

        string key = BuildKey(tenantId, partyId);
        try
        {
            await daprClient
                .SaveStateAsync(_stateStoreName, key, entries, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to replace memory-unit mapping for {TenantId}/{PartyId}; cleanup re-run will retry the same unit set.",
                tenantId,
                partyId);
            throw new InvalidOperationException(
                $"Failed to replace memory-unit mapping for {tenantId}/{partyId}: {ex.GetType().Name}",
                ex);
        }
    }

    private async Task<(IReadOnlyList<PartyMemoryUnitMappingEntry> Mappings, string ETag)> ReadWithETagAsync(
        string key,
        CancellationToken cancellationToken)
    {
        try
        {
            (List<PartyMemoryUnitMappingEntry> state, string etag) = await daprClient
                .GetStateAndETagAsync<List<PartyMemoryUnitMappingEntry>>(
                    _stateStoreName,
                    key,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return (state ?? (IReadOnlyList<PartyMemoryUnitMappingEntry>)[], etag ?? string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Reads degrade to "no mapping" so cleanup operating with stale state-store
            // visibility never blocks erasure. The warning makes the silent-degrade path
            // observable so an operator can act on persistent read outages.
            logger.LogWarning(
                ex,
                "Failed to read memory-unit mapping for state key {Key}; treating as empty. Cleanup may not find prior mappings.",
                key);
            return ((IReadOnlyList<PartyMemoryUnitMappingEntry>)[], string.Empty);
        }
    }

    /// <summary>
    /// Build a Dapr-portable state-store key. Using a fixed-charset BASE64URL hash of the
    /// tenant + party ids avoids backend-specific key restrictions (Cosmos rejects
    /// <c>/</c> <c>\</c> <c>?</c> <c>#</c>, MongoDB has length / collation constraints,
    /// some operators sanitize percent-encoded segments). The hash is collision-resistant
    /// for SHA-256 — a 64-bit truncation would suffice but the full 256-bit prefix is cheap
    /// and makes accidental collisions essentially impossible.
    /// </summary>
    private static string BuildKey(string tenantId, string partyId)
    {
        Span<byte> input = stackalloc byte[1];
        ReadOnlySpan<byte> tenantBytes = Encoding.UTF8.GetBytes(tenantId);
        ReadOnlySpan<byte> partyBytes = Encoding.UTF8.GetBytes(partyId);
        Span<byte> separator = stackalloc byte[] { 0x1F };

        Span<byte> hash = stackalloc byte[32];
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher.AppendData(tenantBytes);
        hasher.AppendData(separator);
        hasher.AppendData(partyBytes);
        _ = hasher.TryGetHashAndReset(hash, out _);

        Span<byte> base64 = stackalloc byte[Base64.GetMaxEncodedToUtf8Length(hash.Length)];
        _ = Base64.EncodeToUtf8(hash, base64, out _, out int written);
        // BASE64URL: replace '+'/'/' with '-'/'_'; drop padding for compactness.
        Span<byte> trimmed = base64[..written];
        for (int i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] == (byte)'+')
            {
                trimmed[i] = (byte)'-';
            }
            else if (trimmed[i] == (byte)'/')
            {
                trimmed[i] = (byte)'_';
            }
        }

        while (trimmed.Length > 0 && trimmed[^1] == (byte)'=')
        {
            trimmed = trimmed[..^1];
        }

        _ = input;
        return $"parties:memory-mapping:{Encoding.ASCII.GetString(trimmed)}";
    }
}

/// <summary>
/// Configuration for the Dapr state store backing the per-party → memory-unit-id mapping.
/// The Dapr state-store component name is operator-configurable (default: <c>statestore</c>)
/// because Aspire / Dapr deployments commonly use named state stores per concern.
/// </summary>
internal sealed class PartyMemoryUnitMappingStoreOptions
{
    public const string SectionName = "Parties:MemoriesSearch:MappingStore";

    public string StateStoreName { get; init; } = "statestore";
}
