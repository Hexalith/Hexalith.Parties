using Hexalith.Parties.Contracts;

namespace Hexalith.Parties.Projections.Services;

public sealed record PartyProjectionRebuildScope(
    string TenantId,
    string ProjectionName,
    string? PartyId = null,
    string? OperationId = null)
{
    public static PartyProjectionRebuildScope Detail(string tenantId, string? partyId = null, string? operationId = null)
        => new(tenantId, PartyProjectionNames.Detail, partyId, operationId);

    public static PartyProjectionRebuildScope Index(string tenantId, string? operationId = null)
        => new(tenantId, PartyProjectionNames.Index, null, operationId);
}
