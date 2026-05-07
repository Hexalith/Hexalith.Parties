using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.AdminPortal.Tests.Services;

internal sealed class RecordingAdminPortalApiClient : IPartiesAdminPortalApiClient
{
    private readonly Queue<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>> _listResponses = [];
    private readonly Queue<AdminPortalQueryResult<PagedResult<PartySearchResult>>> _searchResponses = [];
    private readonly Queue<AdminPortalQueryResult<PartyDetail>> _detailResponses = [];

    public List<AdminPortalListRequest> ListRequests { get; } = [];

    public List<AdminPortalSearchRequest> SearchRequests { get; } = [];

    public List<string> DetailRequests { get; } = [];

    public void EnqueueList(PagedResult<PartyIndexEntry> page, AdminPortalQueryMetadata? metadata = null)
        => _listResponses.Enqueue(new AdminPortalQueryResult<PagedResult<PartyIndexEntry>>(page, metadata ?? AdminPortalQueryMetadata.Empty));

    public void EnqueueSearch(PagedResult<PartySearchResult> page, AdminPortalQueryMetadata? metadata = null)
        => _searchResponses.Enqueue(new AdminPortalQueryResult<PagedResult<PartySearchResult>>(page, metadata ?? AdminPortalQueryMetadata.Empty));

    public void EnqueueDetail(PartyDetail detail, AdminPortalQueryMetadata? metadata = null)
        => _detailResponses.Enqueue(new AdminPortalQueryResult<PartyDetail>(detail, metadata ?? AdminPortalQueryMetadata.Empty));

    public Task<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>> ListPartiesAsync(AdminPortalListRequest request, CancellationToken cancellationToken)
    {
        ListRequests.Add(request with { PageSize = AdminPortalQueryBounds.BoundPageSize(request.PageSize) });
        return Task.FromResult(_listResponses.Count == 0
            ? new AdminPortalQueryResult<PagedResult<PartyIndexEntry>>(Empty<PartyIndexEntry>(), AdminPortalQueryMetadata.Empty)
            : _listResponses.Dequeue());
    }

    public Task<AdminPortalQueryResult<PagedResult<PartySearchResult>>> SearchPartiesAsync(AdminPortalSearchRequest request, CancellationToken cancellationToken)
    {
        SearchRequests.Add(request with { PageSize = AdminPortalQueryBounds.BoundPageSize(request.PageSize) });
        return Task.FromResult(_searchResponses.Count == 0
            ? new AdminPortalQueryResult<PagedResult<PartySearchResult>>(Empty<PartySearchResult>(), AdminPortalQueryMetadata.Empty)
            : _searchResponses.Dequeue());
    }

    public Task<AdminPortalQueryResult<PartyDetail>> GetPartyAsync(string partyId, CancellationToken cancellationToken)
    {
        DetailRequests.Add(partyId);
        return Task.FromResult(_detailResponses.Count == 0
            ? new AdminPortalQueryResult<PartyDetail>(EmptyDetail(partyId), AdminPortalQueryMetadata.Empty)
            : _detailResponses.Dequeue());
    }

    private static PagedResult<T> Empty<T>() => new()
    {
        Items = [],
        Page = 1,
        PageSize = 20,
        TotalCount = 0,
        TotalPages = 0,
    };

    private static PartyDetail EmptyDetail(string id) => new()
    {
        Id = id,
        Type = default,
        IsActive = false,
        DisplayName = string.Empty,
        SortName = string.Empty,
        ContactChannels = [],
        Identifiers = [],
        ConsentRecords = [],
        CreatedAt = default,
        LastModifiedAt = default,
    };
}
