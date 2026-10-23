using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Search;

/// <summary>
/// SDK-backed legacy-compatible mapping inventory, keyed by tenant and party.
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
    IReadModelStore readModelStore,
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

            // A shared source URI never replaces a distinct unit's cleanup obligation.
            List<PartyMemoryUnitMappingEntry> updated = [];
            bool replaced = false;
            foreach (PartyMemoryUnitMappingEntry existing in current)
            {
                bool sameMemoryUnit = string.Equals(existing.MemoryUnitId, memoryUnitId, StringComparison.Ordinal);
                bool sameCase = string.IsNullOrWhiteSpace(existing.CaseId)
                    || string.Equals(existing.CaseId, caseId, StringComparison.Ordinal);
                if (sameCase && sameMemoryUnit)
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
                success = await readModelStore
                    .TrySaveAsync(_stateStoreName, key, updated, etag, cancellationToken)
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

        // Keep projection delivery retryable. The independent durable ingestion intent
        // retains the canonical producer until mapping persistence or verified cleanup converges.
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
                // Redis restarts a physically deleted key's ETag sequence. Keeping [] for
                // domain ledgers prevents a delayed cleanup from matching a recreated key.
                // Probe keys are unique per invocation and are never domain inventories.
                success = deleteEmptyProbeKey && updated.Count == 0
                    ? await ((IReadModelConditionalEraser)readModelStore).TryEraseAsync(
                        _stateStoreName, key, etag, cancellationToken).ConfigureAwait(false)
                    : await readModelStore.TrySaveAsync(
                        _stateStoreName, key, updated, etag, cancellationToken).ConfigureAwait(false);
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
            ReadModelEntry<List<PartyMemoryUnitMappingEntry>> state = await readModelStore
                .GetAsync<List<PartyMemoryUnitMappingEntry>>(_stateStoreName, key, cancellationToken)
                .ConfigureAwait(false);
            return (state.Value ?? (IReadOnlyList<PartyMemoryUnitMappingEntry>)[], state.ETag ?? string.Empty);
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
    internal static string BuildKey(string tenantId, string partyId)
    {
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

        return $"parties:memory-mapping:{Encoding.ASCII.GetString(trimmed)}";
    }
}
