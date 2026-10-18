namespace Hexalith.Parties.Search;

/// <summary>Reports whether the versioned cleanup route handled a probe.</summary>
/// <param name="Reachable">Whether the response established cleanup-route reachability.</param>
/// <param name="StatusCode">The observed HTTP status, if any.</param>
/// <param name="Reason">A bounded explanation when reachability could not be established.</param>
internal sealed record PartyMemoryCleanupProbeResult(
    bool Reachable,
    int? StatusCode,
    string? Reason);
