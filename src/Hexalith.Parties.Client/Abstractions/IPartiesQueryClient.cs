using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Client.Abstractions;

public interface IPartiesQueryClient
{
    Task<PartyDetail> GetPartyAsync(
        string partyId,
        CancellationToken ct,
        Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null);

    Task<PagedResult<PartyIndexEntry>> ListPartiesAsync(
        int page,
        int pageSize,
        PartyType? type,
        bool? active,
        DateTimeOffset? createdAfter,
        DateTimeOffset? createdBefore,
        DateTimeOffset? modifiedAfter,
        DateTimeOffset? modifiedBefore,
        CancellationToken ct);

    Task<PagedResult<PartySearchResult>> SearchPartiesAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken ct,
        string? mode = null,
        string? caseId = null,
        Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null);
}
