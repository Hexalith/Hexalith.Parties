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

    /// <summary>Removes a cached detail entry, e.g. so a degraded read cannot serve pre-erasure PII.</summary>
    public void EvictDetail(string tenantId, string partyId)
        => _details.TryRemove(PartySdkReadModelAddresses.Detail(tenantId, partyId), out _);

    public void StoreIndex(string tenantId, PartyIndexSdkReadModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _indexes[PartySdkReadModelAddresses.Index(tenantId)] = value;
    }

    public bool TryGetIndex(string tenantId, out PartyIndexSdkReadModel? value)
        => _indexes.TryGetValue(PartySdkReadModelAddresses.Index(tenantId), out value);

    /// <summary>
    /// Removes a tenant's cached shared index, e.g. so a degraded read cannot keep listing a party
    /// that was just removed from the canonical index by erasure. The next successful read
    /// repopulates the cache with the current (party-excluded) index.
    /// </summary>
    public void EvictIndex(string tenantId)
        => _indexes.TryRemove(PartySdkReadModelAddresses.Index(tenantId), out _);

    public void StoreProcessing(string tenantId, string partyId, PartyProcessingSdkReadModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _processing[PartySdkReadModelAddresses.Processing(tenantId, partyId)] = value;
    }

    public bool TryGetProcessing(string tenantId, string partyId, out PartyProcessingSdkReadModel? value)
        => _processing.TryGetValue(PartySdkReadModelAddresses.Processing(tenantId, partyId), out value);

    /// <summary>Removes a cached processing-activity checkpoint, matching the erasure reset applied to the canonical store.</summary>
    public void EvictProcessing(string tenantId, string partyId)
        => _processing.TryRemove(PartySdkReadModelAddresses.Processing(tenantId, partyId), out _);
}
