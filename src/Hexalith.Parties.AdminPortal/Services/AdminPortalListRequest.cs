using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.AdminPortal.Services;

public sealed record AdminPortalListRequest(
    int Page,
    int PageSize,
    PartyType? Type,
    bool? Active,
    DateTimeOffset? CreatedAfter = null,
    DateTimeOffset? CreatedBefore = null,
    DateTimeOffset? ModifiedAfter = null,
    DateTimeOffset? ModifiedBefore = null);
