using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.AdminPortal.Services;

public interface IPartiesAdminPortalApiClient
{
    Task<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>> ListPartiesAsync(
        AdminPortalListRequest request,
        CancellationToken cancellationToken);

    Task<AdminPortalQueryResult<PagedResult<PartySearchResult>>> SearchPartiesAsync(
        AdminPortalSearchRequest request,
        CancellationToken cancellationToken);

    Task<AdminPortalQueryResult<PartyDetail>> GetPartyAsync(
        string partyId,
        CancellationToken cancellationToken);
}
