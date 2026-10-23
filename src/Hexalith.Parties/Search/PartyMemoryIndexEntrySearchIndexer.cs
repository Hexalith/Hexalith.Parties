using Hexalith.EventStore.Client.Projections;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Search;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Search;

/// <summary>
/// Bridges <see cref="Hexalith.Parties.Projections.Handlers.PartyIndexSdkProjectionHandler"/>'s
/// post-write notification to <see cref="PartyMemoryIndexingService"/> /
/// <see cref="PartyMemoryCleanupService"/>. The Projections project cannot reference this project
/// directly, so this adapter implements the Projections-owned
/// <see cref="IPartyIndexSearchIndexer"/> seam and is registered by <c>AddParties</c>.
/// </summary>
internal sealed partial class PartyMemoryIndexEntrySearchIndexer(
    PartyMemoryIndexingService? indexingService,
    PartyMemoryCleanupService cleanupService,
    IOptionsMonitor<PartyMemorySearchOptions> options,
    ILogger<PartyMemoryIndexEntrySearchIndexer> logger,
    IReadModelStore? readModelStore = null,
    IOptions<PartySdkReadModelOptions>? projectionOptions = null) : IPartyIndexSearchIndexer
{
    // Suppress retained pairs for this singleton's lifetime. Evicted pairs may warn again.
    // Both collections are bounded, and duplicates never add eviction bookkeeping.
    private const int MissingCaseWarningCapacity = 1024;

    private readonly Lock _caseIdMissingWarningLock = new();
    private readonly HashSet<(string TenantId, string PartyId)> _caseIdMissingWarned = [];
    private readonly Queue<(string TenantId, string PartyId)> _caseIdMissingWarningOrder = new();

    public Task<bool> NotifyIndexedAsync(
        string tenantId,
        PartyIndexEntry entry,
        string eventType,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
        => NotifyIndexedAsync(tenantId, entry, eventType, timestamp, null, cancellationToken);

    /// <summary>Retains the canonical application sequence in the durable ingestion identity.</summary>
    public async Task<bool> NotifyIndexedAsync(
        string tenantId,
        PartyIndexEntry entry,
        string eventType,
        DateTimeOffset timestamp,
        long? sequenceNumber,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            PartyMemorySearchOptions current = options.CurrentValue;
            if (!current.Enabled)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(current.CaseId))
            {
                if (TryRecordMissingCaseWarning(tenantId, entry.Id))
                {
                    LogIndexingSkippedMissingCaseId();
                }

                // Keep the canonical projection delivery retryable so repaired configuration
                // reconciles this target on redelivery. Removal still uses persisted mappings.
                return false;
            }

            if (indexingService is null)
            {
                LogIndexingFailed();
                return false;
            }

            if (!await indexingService.TryResumeLiveProjectionAsync(tenantId, entry.Id, cancellationToken).ConfigureAwait(false))
            {
                // A supported live re-add must finish transient removal before opening a new
                // ingestion generation. Permanent erasure and unreadable canonical state stay fenced.
                _ = await cleanupService.DeleteByPartyAsync(tenantId, current.CaseId, entry.Id,
                    cancellationToken, permanent: false).ConfigureAwait(false);
                if (!await indexingService.TryResumeLiveProjectionAsync(tenantId, entry.Id, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
            }

            PartyMemoryIndexingResult? result = await indexingService
                .IndexAsync(
                    entry,
                    PartyMemoryUnitMappingContext.ForProjection(
                        tenantId,
                        current.CaseId,
                        string.IsNullOrWhiteSpace(eventType) ? "PartyProjectionChanged" : eventType,
                        aggregateId: entry.Id,
                        timestamp: timestamp,
                        sequenceNumber: sequenceNumber),
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is null)
            {
                LogIndexingSkippedUnmappedUnit();
                return true;
            }

            if (!result.Indexed)
            {
                LogIndexingFailed();
            }

            return result.Indexed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Convert external failures into an explicit non-converged result for retry.
            LogIndexingException(ex.GetType().Name);
            return false;
        }
    }

    public async Task<bool> NotifyRemovedAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        try
        {
            PartyMemorySearchOptions current = options.CurrentValue;
            bool permanent = true;
            if (readModelStore is not null)
            {
                string storeName = projectionOptions?.Value.ReadModelStateStoreName ?? "statestore";
                PartyIndexSdkReadModel? canonical = (await readModelStore.GetAsync<PartyIndexSdkReadModel>(storeName,
                    PartySdkReadModelAddresses.Index(tenantId), cancellationToken).ConfigureAwait(false)).Value;
                if (canonical is not { Entries: not null, ErasureSequenceNumbers: not null, LastSequenceNumbers: not null, LastIndexedEvents: not null })
                {
                    return false;
                }

                permanent = canonical.ErasureSequenceNumbers.ContainsKey(partyId);
                if (canonical.Entries.TryGetValue(partyId, out PartyIndexEntry? target))
                {
                    if (target is null || !string.Equals(target.Id, partyId, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    // A delayed transient removal cannot fence or delete an already live target.
                    // A retained erasure checkpoint still takes precedence over a live entry.
                    if (!permanent && !target.IsErased)
                    {
                        return false;
                    }
                }
            }

            PartyMemoryCleanupResult result = await cleanupService
                .DeleteByPartyAsync(tenantId, current.CaseId ?? string.Empty, partyId, cancellationToken, permanent)
                .ConfigureAwait(false);

            if (!result.Cleaned)
            {
                LogRemovalFailed();
            }

            return result.Cleaned;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogRemovalException(ex.GetType().Name);
            return false;
        }
    }

    /// <summary>
    /// Atomically admits an exact tenant/party pair, evicting the oldest pair at capacity.
    /// Duplicate notifications leave both the retained set and eviction order unchanged.
    /// </summary>
    private bool TryRecordMissingCaseWarning(string tenantId, string partyId)
    {
        (string TenantId, string PartyId) key = (tenantId, partyId);
        lock (_caseIdMissingWarningLock)
        {
            if (_caseIdMissingWarned.Contains(key))
            {
                return false;
            }

            if (_caseIdMissingWarned.Count == MissingCaseWarningCapacity)
            {
                _ = _caseIdMissingWarned.Remove(_caseIdMissingWarningOrder.Dequeue());
            }

            _ = _caseIdMissingWarned.Add(key);
            _caseIdMissingWarningOrder.Enqueue(key);
            return true;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories party indexing skipped: search is enabled but no CaseId is configured.")]
    private partial void LogIndexingSkippedMissingCaseId();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Memories party indexing skipped: entry could not be mapped to a memory unit.")]
    private partial void LogIndexingSkippedUnmappedUnit();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories party indexing failed.")]
    private partial void LogIndexingFailed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories party indexing threw: {ExceptionType}")]
    private partial void LogIndexingException(string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories party removal failed.")]
    private partial void LogRemovalFailed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories party removal threw: {ExceptionType}")]
    private partial void LogRemovalException(string exceptionType);
}
