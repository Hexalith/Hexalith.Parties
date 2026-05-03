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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or InvalidOperationException)
        {
            // Indexing must be best-effort: a Memories outage cannot abort the projection
            // delivery pipeline. Log and surface the failure so the caller can record it for
            // repair via the Memories repair/reindex procedure.
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
