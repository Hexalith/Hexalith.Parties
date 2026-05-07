using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.AdminPortal.Services;

internal sealed record AdminPortalSearchResponse(
    PagedResult<PartySearchResult> Results,
    string? Status,
    string? DegradedReason);
