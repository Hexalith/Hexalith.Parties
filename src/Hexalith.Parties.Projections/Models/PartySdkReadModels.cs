using Hexalith.EventStore.Client.Projections;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Models;

/// <summary>The canonical aggregate-owned detail value written by the SDK projection.</summary>
public sealed record PartyDetailSdkReadModel : IReadModelFreshness
{
    public PartyDetail? Detail { get; init; }

    public long LastSequenceNumber { get; init; } = long.MinValue;

    public DateTimeOffset? ProjectedAt { get; init; }

    public string? ProjectionVersion { get; init; }
}

/// <summary>The canonical shared tenant index value written by the SDK projection.</summary>
public sealed record PartyIndexSdkReadModel : IReadModelFreshness
{
    public IReadOnlyDictionary<string, PartyIndexEntry> Entries { get; init; }
        = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, long> LastSequenceNumbers { get; init; }
        = new Dictionary<string, long>(StringComparer.Ordinal);

    public DateTimeOffset? ProjectedAt { get; init; }

    public string? ProjectionVersion { get; init; }
}

/// <summary>PII-free processing activity records projected with the aggregate detail.</summary>
public sealed record PartyProcessingSdkReadModel : IReadModelFreshness
{
    public IReadOnlyList<ProcessingActivityRecord> Records { get; init; } = [];

    public long LastSequenceNumber { get; init; } = long.MinValue;

    public DateTimeOffset? ProjectedAt { get; init; }

    public string? ProjectionVersion { get; init; }
}

/// <summary>Canonical addresses used by both Parties SDK producers and consumers.</summary>
public static class PartySdkReadModelAddresses
{
    public const string DetailSlot = "detail";
    public const string IndexSlot = "index";
    public const string ProcessingSlot = "processing-records";
    public const string SharedIndexAggregateId = "parties";

    public static string Detail(string tenantId, string partyId)
        => Build(tenantId, PartyProjectionNames.Detail, partyId, DetailSlot);

    public static string Index(string tenantId)
        => Build(tenantId, PartyProjectionNames.Index, SharedIndexAggregateId, IndexSlot);

    public static string Processing(string tenantId, string partyId)
        => Build(tenantId, PartyProjectionNames.Detail, partyId, ProcessingSlot);

    private static string Build(string tenantId, string projection, string aggregateId, string slot)
    {
        ValidateSegment(tenantId, nameof(tenantId));
        ValidateSegment(aggregateId, nameof(aggregateId));
        return $"readmodel:{tenantId}:party:{projection}:{aggregateId}:{slot}";
    }

    private static void ValidateSegment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("Read-model identity segments must not contain ':'.", parameterName);
        }
    }
}
