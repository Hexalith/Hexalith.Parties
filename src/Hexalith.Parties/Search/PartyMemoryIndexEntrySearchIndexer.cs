using System.Collections.Concurrent;

using Hexalith.Parties.Contracts.Models;
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
    PartyMemoryIndexingService indexingService,
    PartyMemoryCleanupService cleanupService,
    IOptionsMonitor<PartyMemorySearchOptions> options,
    ILogger<PartyMemoryIndexEntrySearchIndexer> logger) : IPartyIndexSearchIndexer
{
    // One-shot warning per (tenant, party) so a misconfigured operator doesn't see a warning
    // storm — mirrors the retired PartyProjectionUpdateOrchestrator's behavior.
    private static readonly ConcurrentDictionary<string, byte> s_caseIdMissingWarned = new(StringComparer.Ordinal);

    public async Task NotifyIndexedAsync(
        string tenantId,
        PartyIndexEntry entry,
        string eventType,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            PartyMemorySearchOptions current = options.CurrentValue;
            if (!current.Enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(current.CaseId))
            {
                string warningKey = $"{tenantId}:{entry.Id}";
                if (s_caseIdMissingWarned.TryAdd(warningKey, 0))
                {
                    LogIndexingSkippedMissingCaseId(tenantId, entry.Id);
                }

                return;
            }

            PartyMemoryIndexingResult? result = await indexingService
                .IndexAsync(
                    entry,
                    PartyMemoryUnitMappingContext.ForProjection(
                        tenantId,
                        current.CaseId,
                        string.IsNullOrWhiteSpace(eventType) ? "PartyProjectionChanged" : eventType,
                        aggregateId: entry.Id,
                        timestamp: timestamp),
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is null)
            {
                LogIndexingSkippedUnmappedUnit(tenantId, entry.Id);
                return;
            }

            if (!result.Indexed)
            {
                LogIndexingFailed(tenantId, entry.Id, result.FailureReason ?? "unknown");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Seam contract: implementations must never throw into the projection path.
            LogIndexingException(ex, tenantId, entry.Id);
        }
    }

    public async Task NotifyRemovedAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        try
        {
            PartyMemorySearchOptions current = options.CurrentValue;
            if (!current.Enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(current.CaseId))
            {
                string warningKey = $"{tenantId}:{partyId}:remove";
                if (s_caseIdMissingWarned.TryAdd(warningKey, 0))
                {
                    LogIndexingSkippedMissingCaseId(tenantId, partyId);
                }

                return;
            }

            PartyMemoryCleanupResult result = await cleanupService
                .DeleteByPartyAsync(tenantId, current.CaseId, partyId, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Cleaned)
            {
                LogRemovalFailed(tenantId, partyId, result.BlockedReason ?? "unknown");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogRemovalException(ex, tenantId, partyId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories indexing skipped for {TenantId}/{PartyId}: search is enabled but no CaseId is configured.")]
    private partial void LogIndexingSkippedMissingCaseId(string tenantId, string partyId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Memories indexing skipped for {TenantId}/{PartyId}: entry could not be mapped to a memory unit.")]
    private partial void LogIndexingSkippedUnmappedUnit(string tenantId, string partyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories indexing failed for {TenantId}/{PartyId}: {Reason}")]
    private partial void LogIndexingFailed(string tenantId, string partyId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories indexing threw for {TenantId}/{PartyId}.")]
    private partial void LogIndexingException(Exception ex, string tenantId, string partyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories removal failed for {TenantId}/{PartyId}: {Reason}")]
    private partial void LogRemovalFailed(string tenantId, string partyId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories removal threw for {TenantId}/{PartyId}.")]
    private partial void LogRemovalException(Exception ex, string tenantId, string partyId);
}
