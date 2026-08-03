using System.Collections.Concurrent;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Search;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Search;

/// <summary>
/// Bridges <see cref="Hexalith.Parties.Projections.Handlers.PartyIndexSdkProjectionHandler"/>'s
/// post-write notification to <see cref="PartyMemoryIndexingService"/>. The Projections project
/// cannot reference this project directly, so this adapter implements the Projections-owned
/// <see cref="IPartyIndexSearchIndexer"/> seam and is registered by <c>AddParties</c>.
/// </summary>
internal sealed partial class PartyMemoryIndexEntrySearchIndexer(
    PartyMemoryIndexingService indexingService,
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

        _ = await indexingService
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
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memories indexing skipped for {TenantId}/{PartyId}: search is enabled but no CaseId is configured.")]
    private partial void LogIndexingSkippedMissingCaseId(string tenantId, string partyId);
}
