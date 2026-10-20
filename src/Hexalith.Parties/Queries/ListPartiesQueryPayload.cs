using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Queries;

/// <summary>Validated query values for SDK read execution.</summary>
/// <param name="Page">The legacy payload page, used only without envelope paging.</param>
/// <param name="PageSize">The legacy payload size, used only without envelope paging.</param>
/// <param name="Type">The optional party-kind filter.</param>
/// <param name="Active">The optional active-status filter.</param>
/// <param name="CreatedAfter">The inclusive creation lower bound.</param>
/// <param name="CreatedBefore">The inclusive creation upper bound.</param>
/// <param name="ModifiedAfter">The inclusive modification lower bound.</param>
/// <param name="ModifiedBefore">The inclusive modification upper bound.</param>
internal sealed record ListPartiesQueryPayload(
    int Page,
    int PageSize,
    PartyType? Type,
    bool? Active,
    DateTimeOffset? CreatedAfter,
    DateTimeOffset? CreatedBefore,
    DateTimeOffset? ModifiedAfter,
    DateTimeOffset? ModifiedBefore);
