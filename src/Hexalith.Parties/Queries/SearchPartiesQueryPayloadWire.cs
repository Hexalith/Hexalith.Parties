namespace Hexalith.Parties.Queries;

/// <summary>Raw wire values for strict SDK query payload validation.</summary>
/// <param name="Query">The display-name query.</param>
/// <param name="Page">The legacy payload page, used only without envelope paging.</param>
/// <param name="PageSize">The legacy payload size, used only without envelope paging.</param>
/// <param name="Type">The optional party-kind filter.</param>
/// <param name="Active">The optional active-status filter.</param>
/// <param name="Mode">The legacy search-mode compatibility value.</param>
/// <param name="CaseId">An unsupported legacy case filter, rejected if supplied.</param>
internal sealed record SearchPartiesQueryPayloadWire(
    string? Query = null,
    int? Page = null,
    int? PageSize = null,
    string? Type = null,
    bool? Active = null,
    string? Mode = null,
    string? CaseId = null);
