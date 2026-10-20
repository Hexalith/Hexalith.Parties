namespace Hexalith.Parties.Queries;

/// <summary>Raw wire values for strict SDK query payload validation.</summary>
/// <param name="Page">The legacy payload page, used only without envelope paging.</param>
/// <param name="PageSize">The legacy payload size, used only without envelope paging.</param>
/// <param name="Type">The optional party-kind filter.</param>
/// <param name="Active">The optional active-status filter.</param>
/// <param name="CreatedAfter">The inclusive creation lower bound.</param>
/// <param name="CreatedBefore">The inclusive creation upper bound.</param>
/// <param name="ModifiedAfter">The inclusive modification lower bound.</param>
/// <param name="ModifiedBefore">The inclusive modification upper bound.</param>
internal sealed record ListPartiesQueryPayloadWire(
    int? Page = null,
    int? PageSize = null,
    string? Type = null,
    bool? Active = null,
    string? CreatedAfter = null,
    string? CreatedBefore = null,
    string? ModifiedAfter = null,
    string? ModifiedBefore = null);
