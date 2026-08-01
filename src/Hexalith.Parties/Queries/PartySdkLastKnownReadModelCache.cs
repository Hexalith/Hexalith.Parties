using System.Collections.Concurrent;

using Hexalith.Parties.Projections.Models;

namespace Hexalith.Parties.Queries;

/// <summary>Keeps tenant-scoped last-known SDK read models for bounded degraded reads.</summary>
public sealed class PartySdkLastKnownReadModelCache
{
    private readonly ConcurrentDictionary<string, PartyDetailSdkReadModel> _details = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PartyIndexSdkReadModel> _indexes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PartyProcessingSdkReadModel> _processing = new(StringComparer.Ordinal);

    public void StoreDetail(string tenantId, string partyId, PartyDetailSdkReadModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _details[PartySdkReadModelAddresses.Detail(tenantId, partyId)] = value;
    }

    public bool TryGetDetail(string tenantId, string partyId, out PartyDetailSdkReadModel? value)
        => _details.TryGetValue(PartySdkReadModelAddresses.Detail(tenantId, partyId), out value);

    public void StoreIndex(string tenantId, PartyIndexSdkReadModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _indexes[PartySdkReadModelAddresses.Index(tenantId)] = value;
    }

    public bool TryGetIndex(string tenantId, out PartyIndexSdkReadModel? value)
        => _indexes.TryGetValue(PartySdkReadModelAddresses.Index(tenantId), out value);

    public void StoreProcessing(string tenantId, string partyId, PartyProcessingSdkReadModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _processing[PartySdkReadModelAddresses.Processing(tenantId, partyId)] = value;
    }

    public bool TryGetProcessing(string tenantId, string partyId, out PartyProcessingSdkReadModel? value)
        => _processing.TryGetValue(PartySdkReadModelAddresses.Processing(tenantId, partyId), out value);
}
