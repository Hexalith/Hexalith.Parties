using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Models;

/// <summary>A fixed-capacity sequence partition of retained PII-free Article 30 facts.</summary>
public sealed record PartyProcessingBucket
{
    /// <summary>The maximum number of records in one sequence partition or inline model.</summary>
    public const int Capacity = 32;

    /// <summary>Gets the ordered, unique facts in this sequence partition.</summary>
    public IReadOnlyList<ProcessingActivityRecord> Records { get; init; } = [];

    /// <summary>Calculates the partition identity for a nonnegative aggregate sequence.</summary>
    public static long Number(long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        return sequence / Capacity;
    }
}
