using System.Text;

using Hexalith.Memories.Client.Rest;
using Hexalith.Parties.Contracts.Models;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.CommandApi.Search;

internal sealed record PartyMemoryIndexingResult(
    string PartyId,
    string SourceUri,
    string? WorkflowInstanceId,
    bool Indexed,
    string? FailureReason);

internal sealed class PartyMemoryIndexingService(
    MemoriesClient memoriesClient,
    IPartyMemoryUnitMappingStore mappingStore,
    ILogger<PartyMemoryIndexingService> logger)
{
    public async Task<PartyMemoryIndexingResult?> IndexAsync(
        PartyIndexEntry entry,
        PartyMemoryUnitMappingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(context);

        PartyMemoryUnit? unit = PartyMemoryUnitMapper.Map(entry, context);
        if (unit is null)
        {
            return null;
        }

        byte[] content = Encoding.UTF8.GetBytes(unit.Content);

        try
        {
            // HXL001 marks `IngestAsync` obsolete on the Memories SDK. The replacement
            // (`IndexAsync` family) is not yet available for the per-party flow Parties
            // requires here. Suppress until the SDK exposes a non-deprecated equivalent.
#pragma warning disable HXL001
            string workflowInstanceId = await memoriesClient
                .IngestAsync(
                    unit.TenantId,
                    unit.CaseId,
                    unit.SourceUri,
                    content,
                    "text/plain",
                    "Hexalith.Parties",
                    unit.Metadata,
                    cancellationToken)
                .ConfigureAwait(false);
#pragma warning restore HXL001

            // Record the per-party → memory-unit-id mapping so erasure cleanup can iterate
            // per-unit DELETEs against the existing per-unit Memories endpoint. Without this
            // mapping the AC5 cleanup flow has no way to enumerate the units it must delete.
            await mappingStore
                .RecordMappingAsync(unit.TenantId, unit.PartyId, workflowInstanceId, unit.SourceUri, cancellationToken)
                .ConfigureAwait(false);

            return new PartyMemoryIndexingResult(
                unit.PartyId,
                unit.SourceUri,
                workflowInstanceId,
                Indexed: true,
                FailureReason: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            // Indexing must be best-effort for genuine transient failures (transport
            // errors, timeouts, cancellation) so a Memories outage cannot abort the
            // projection delivery pipeline. Programming-bug indicators like
            // InvalidOperationException are NOT caught here — they should surface as 500
            // so the underlying defect is fixed rather than masked as transient outage.
            logger.LogWarning(
                ex,
                "Memories indexing failed for {TenantId}/{PartyId} (source URI {SourceUri}). Search may be stale until repair runs.",
                unit.TenantId,
                unit.PartyId,
                unit.SourceUri);
            return new PartyMemoryIndexingResult(
                unit.PartyId,
                unit.SourceUri,
                WorkflowInstanceId: null,
                Indexed: false,
                FailureReason: $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
