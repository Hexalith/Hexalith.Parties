namespace Hexalith.Parties.Contracts.Models;

/// <summary>A bounded retained processing-history page and its protected continuation.</summary>
/// <param name="Items">The ordered authorized records.</param>
/// <param name="NextCursor">The opaque continuation, or null when complete.</param>
/// <param name="TotalCount">The authoritative retained record count.</param>
public sealed record ProcessingActivityPage(IReadOnlyList<ProcessingActivityRecord> Items, string? NextCursor, long? TotalCount);
