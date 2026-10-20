using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Queries;

/// <summary>Validated query values for SDK read execution.</summary>
/// <param name="Query">The display-name query.</param>
/// <param name="Page">The legacy payload page, used only without envelope paging.</param>
/// <param name="PageSize">The legacy payload size, used only without envelope paging.</param>
/// <param name="Type">The optional party-kind filter.</param>
/// <param name="Active">The optional active-status filter.</param>
/// <param name="Mode">The legacy search-mode compatibility value.</param>
/// <param name="CaseId">An unsupported legacy case filter, rejected if supplied.</param>
internal sealed record SearchPartiesQueryPayload(
    string Query,
    int Page,
    int PageSize,
    PartyType? Type,
    bool? Active,
    string? Mode,
    string? CaseId);
