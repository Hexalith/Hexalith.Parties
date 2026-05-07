using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.AdminPortal.Services;

public sealed record AdminPortalSearchRequest(
    string Query,
    int Page,
    int PageSize,
    PartyType? Type,
    bool? Active);
