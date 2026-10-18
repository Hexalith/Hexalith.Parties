using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Dapr.Client;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Search;

/// <summary>
/// Dapr-state-store backed mapping. Storage shape: a single state key per (tenant, party)
/// holding a list of <see cref="PartyMemoryUnitMappingEntry"/>.
/// <para>
/// <b>Memory-unit-id contract (P40 / resolved decision D1).</b> The Memories server uses the
/// DAPR workflow instance id returned by <c>POST /api/ingest</c> as the canonical
/// <c>memoryUnitId</c> for the resulting memory unit (verified against
/// <c>Hexalith.Memories.Server.Workflows.IngestionWorkflow.ResolveMemoryUnitId</c>). The
/// per-unit cleanup endpoint <c>DELETE /api/v1/tenants/{t}/cases/{c}/memory-units/{memoryUnitId}</c>
/// is keyed by that same id, so recording the workflow instance id as <c>memoryUnitId</c>
/// here is correct by design. The only divergence path is <c>SourceType.Event</c> with the
/// dedup-instance prefix, but the Memories SDK's <c>IngestAsync</c> hard-codes
/// <c>SourceType.File</c>, so the simple mapping always holds for Parties.
/// </para>
/// <para>
/// <b>Concurrency (P3).</b> Updates use ETag-based optimistic concurrency with bounded retry
/// so indexing and cleanup preserve concurrent mappings. Cleanup subtracts exact snapshot
/// entries from fresh state on every attempt. Empty domain inventories retain their ETag
/// generation; physical deletion is restricted to unique, reserved health-probe keys.
/// </para>
/// <para>
/// <b>Failure surface (P2 / P24).</b> State-store failures propagate to the caller. A failed
/// mapping read must never be interpreted as an authoritative empty inventory because that could
/// certify cleanup while Memories still contains indexed data.
/// </para>
/// </summary>
internal sealed partial class PartyMemoryUnitMappingStore(
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
        string caseId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryUnitId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);

        string key = BuildKey(tenantId, partyId);

        for (int attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (IReadOnlyList<PartyMemoryUnitMappingEntry> current, string etag) =
                await ReadWithETagAsync(key, cancellationToken).ConfigureAwait(false);

            // Prefer MemoryUnitId matches over SourceUri-only matches inside the same case.
            // If different rows match each identity, the source-only row still represents a
            // distinct unit that cleanup must retain. Use historical source replacement only
            // when no eligible unit match exists. A source may be
            // reindexed into a new case after configuration changes; retaining the prior-case
            // entry is essential because cleanup must still discover and erase that old unit.
            // Legacy entries without CaseId are eligible for replacement and case adoption;
            // a retained source-only legacy row keeps its original fallback-case behavior.
            bool hasMatchingUnit = current.Any(existing =>
                string.Equals(existing.MemoryUnitId, memoryUnitId, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(existing.CaseId)
                    || string.Equals(existing.CaseId, caseId, StringComparison.Ordinal)));
            List<PartyMemoryUnitMappingEntry> updated = [];
            bool replaced = false;
            foreach (PartyMemoryUnitMappingEntry existing in current)
            {
                bool sameMemoryUnit = string.Equals(existing.MemoryUnitId, memoryUnitId, StringComparison.Ordinal);
                bool sameSource = string.Equals(existing.SourceUri, sourceUri, StringComparison.Ordinal);
                bool sameCase = string.IsNullOrWhiteSpace(existing.CaseId)
                    || string.Equals(existing.CaseId, caseId, StringComparison.Ordinal);
                if (sameCase && (sameMemoryUnit || (!hasMatchingUnit && sameSource)))
                {
                    if (!replaced)
                    {
                        updated.Add(new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri, caseId));
                        replaced = true;
                    }

                    continue;
                }

                updated.Add(existing);
            }

            if (!replaced)
            {
                updated.Add(new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri, caseId));
            }

            if (current.SequenceEqual(updated))
            {
                return;
            }

            bool success;
            try
            {
                success = await daprClient
                    .TrySaveStateAsync(_stateStoreName, key, updated, etag, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Failed to record a memory-unit mapping with {ExceptionType}; mapping was not persisted.",
                    ex.GetType().Name);
                throw new InvalidOperationException("memory-unit-mapping-record-failed");
            }

            if (success)
            {
                return;
            }

            if (attempt + 1 < MaxConcurrencyRetries)
            {
                logger.LogDebug(
                    "ETag conflict recording a memory-unit mapping (attempt {Attempt}/{Max}); retrying.",
                    attempt + 1,
                    MaxConcurrencyRetries);
                continue;
            }

            logger.LogWarning(
                "ETag conflicts exhausted while recording a memory-unit mapping after {AttemptCount} attempts; mapping was not persisted.",
                MaxConcurrencyRetries);
        }

        // All retries exhausted — propagate so the indexing service can compensate by
        // deleting the just-ingested memory unit and reporting a blocked indexing result.
        // Swallowing here would silently orphan a memory unit in Memories with no Parties-side
        // record, breaking AC5 GDPR cleanup.
        throw new InvalidOperationException("memory-unit-mapping-record-conflict");
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
        const string probePrefix = "_health-probe-";
        if (!string.Equals(tenantId, "_health-probe", StringComparison.Ordinal)
            || !partyId.StartsWith(probePrefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(partyId.AsSpan(probePrefix.Length), "N", out _))
        {
            throw new ArgumentException("Physical mapping deletion is restricted to unique reserved health probes.", nameof(partyId));
        }

        IReadOnlyList<PartyMemoryUnitMappingEntry> snapshot =
            await GetMappingsAsync(tenantId, partyId, cancellationToken).ConfigureAwait(false);
        _ = await RemoveMappingsCoreAsync(tenantId, partyId, snapshot, "clear", cancellationToken, deleteEmptyProbeKey: true).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<int> RemoveMappingsAsync(
        string tenantId,
        string partyId,
        IReadOnlyList<PartyMemoryUnitMappingEntry> cleanedEntries,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentNullException.ThrowIfNull(cleanedEntries);
        return RemoveMappingsCoreAsync(tenantId, partyId, cleanedEntries, "remove", cancellationToken);
    }

    private async Task<int> RemoveMappingsCoreAsync(
        string tenantId,
        string partyId,
        IReadOnlyList<PartyMemoryUnitMappingEntry> cleanedEntries,
        string operation,
        CancellationToken cancellationToken,
        bool deleteEmptyProbeKey = false)
    {
        // Capture the targets once. A conflict must never expand removal to new arrivals.
        HashSet<PartyMemoryUnitMappingEntry> targets = [.. cleanedEntries];
        string key = BuildKey(tenantId, partyId);
        for (int attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (IReadOnlyList<PartyMemoryUnitMappingEntry> current, string etag) =
                await ReadWithETagAsync(key, cancellationToken).ConfigureAwait(false);
            List<PartyMemoryUnitMappingEntry> updated = [.. current.Where(entry => !targets.Contains(entry))];
            if (updated.Count == current.Count)
            {
                return current.Count;
            }

            if (string.IsNullOrWhiteSpace(etag))
            {
                LogRemovalMissingETag(logger, operation);
                throw new InvalidOperationException($"memory-unit-mapping-{operation}-missing-etag");
            }

            bool success;
            try
            {
                var stateOptions = new StateOptions { Concurrency = ConcurrencyMode.FirstWrite };
                // Redis restarts a physically deleted key's ETag sequence. Keeping [] for
                // domain ledgers prevents a delayed cleanup from matching a recreated key.
                // Probe keys are unique per invocation and are never domain inventories.
                success = deleteEmptyProbeKey && updated.Count == 0
                    ? await daprClient.TryDeleteStateAsync(
                        _stateStoreName, key, etag, stateOptions, cancellationToken: cancellationToken).ConfigureAwait(false)
                    : await daprClient.TrySaveStateAsync(
                        _stateStoreName, key, updated, etag, stateOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogRemovalFailure(logger, operation, ex.GetType().Name);
                throw new InvalidOperationException($"memory-unit-mapping-{operation}-failed");
            }

            if (success)
            {
                return updated.Count;
            }

            if (attempt + 1 < MaxConcurrencyRetries)
            {
                LogRemovalRetry(logger, operation, attempt + 1, MaxConcurrencyRetries);
            }
        }

        LogRemovalExhausted(logger, operation, MaxConcurrencyRetries);
        throw new InvalidOperationException($"memory-unit-mapping-{operation}-conflict");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memory-unit mapping {Operation} requires an ETag; inventory was retained.")]
    private static partial void LogRemovalMissingETag(ILogger logger, string operation);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memory-unit mapping {Operation} failed with {ExceptionType}; inventory must be retried.")]
    private static partial void LogRemovalFailure(ILogger logger, string operation, string exceptionType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ETag conflict during memory-unit mapping {Operation} (attempt {Attempt}/{Max}); retrying.")]
    private static partial void LogRemovalRetry(ILogger logger, string operation, int attempt, int max);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ETag conflicts exhausted during memory-unit mapping {Operation} after {AttemptCount} attempts; inventory must be retried.")]
    private static partial void LogRemovalExhausted(ILogger logger, string operation, int attemptCount);

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Failed to read a memory-unit mapping with {ExceptionType}; cleanup cannot certify an empty inventory.",
                ex.GetType().Name);
            throw new InvalidOperationException("memory-unit-mapping-read-failed");
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
