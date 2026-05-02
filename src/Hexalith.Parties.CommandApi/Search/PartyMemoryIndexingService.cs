using System.Text;

using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.CommandApi.Search;

internal sealed record PartyMemoryIndexingResult(
    string PartyId,
    string SourceUri,
    string WorkflowInstanceId);

internal sealed class PartyMemoryIndexingService(MemoriesClient memoriesClient)
{
    public async Task<PartyMemoryIndexingResult?> IndexAsync(
        PartyIndexEntry entry,
        PartyMemoryUnitMappingContext context,
        CancellationToken cancellationToken)
    {
        PartyMemoryUnit? unit = PartyMemoryUnitMapper.Map(entry, context);
        if (unit is null)
        {
            return null;
        }

        byte[] content = Encoding.UTF8.GetBytes(unit.Content);

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

        return new PartyMemoryIndexingResult(unit.PartyId, unit.SourceUri, workflowInstanceId);
    }
}
