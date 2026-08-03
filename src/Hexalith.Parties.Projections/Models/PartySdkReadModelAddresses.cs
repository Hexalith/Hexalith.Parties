using System.Buffers;

using Hexalith.Parties.Contracts;

namespace Hexalith.Parties.Projections.Models;

/// <summary>Canonical addresses used by both Parties SDK producers and consumers.</summary>
public static class PartySdkReadModelAddresses
{
    /// <summary>The aggregate-owned detail slot name.</summary>
    public const string DetailSlot = "detail";

    /// <summary>The shared tenant index slot name.</summary>
    public const string IndexSlot = "index";

    /// <summary>The aggregate-owned processing activity slot name.</summary>
    public const string ProcessingSlot = "processing-records";

    /// <summary>The synthetic aggregate identifier used by the shared tenant index.</summary>
    public const string SharedIndexAggregateId = "parties";

    private static readonly SearchValues<char> s_reservedChars = SearchValues.Create(":\0|\r\n");

    /// <summary>Builds the canonical detail address.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="partyId">The party aggregate identifier.</param>
    /// <returns>The canonical detail state key.</returns>
    public static string Detail(string tenantId, string partyId)
        => Build(tenantId, PartyProjectionNames.Detail, partyId, DetailSlot);

    /// <summary>Builds the canonical shared tenant index address.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>The canonical shared index state key.</returns>
    public static string Index(string tenantId)
        => Build(tenantId, PartyProjectionNames.Index, SharedIndexAggregateId, IndexSlot);

    /// <summary>Builds the canonical processing activity address.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="partyId">The party aggregate identifier.</param>
    /// <returns>The canonical processing activity state key.</returns>
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

        if (value.AsSpan().IndexOfAny(s_reservedChars) >= 0)
        {
            throw new ArgumentException(
                $"{parameterName} must not contain ':', '\\0', '|', '\\r', or '\\n' — reserved by the projection key scheme.",
                parameterName);
        }
    }
}
