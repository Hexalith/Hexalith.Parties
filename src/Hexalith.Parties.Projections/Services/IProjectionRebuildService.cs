using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Services;

public interface IProjectionRebuildService {
    Task RebuildDetailProjectionAsync(string tenantId, string? partyId, CancellationToken cancellationToken);

    Task RebuildIndexProjectionAsync(string tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProcessingActivityRecord>> GetProcessingRecordsAsync(string tenantId, string partyId, CancellationToken cancellationToken);
}
