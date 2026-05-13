using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Picker.Tests.Fakes;

internal sealed record SearchCall(string Query, int Page, int PageSize);

internal class RecordingPartiesQueryClient : IPartiesQueryClient
{
    private readonly Queue<PagedResult<PartySearchResult>> _responses = [];
    private readonly Queue<Exception> _failures = [];

    public List<SearchCall> SearchCalls { get; } = [];

    public Exception? ThrowOnSearch { get; set; }

    public Func<HttpRequestMessage, CancellationToken, ValueTask>? LastRequestCustomizer { get; protected set; }

    public string? LastMode { get; protected set; }

    public string? LastCaseId { get; protected set; }

    public void Enqueue(PagedResult<PartySearchResult> response)
        => _responses.Enqueue(response);

    public void EnqueueFailure(Exception exception)
        => _failures.Enqueue(exception);

    public Task<PartyDetail> GetPartyAsync(string partyId, CancellationToken ct)
        => throw new NotSupportedException();

    public Task<PagedResult<PartyIndexEntry>> ListPartiesAsync(
        int page,
        int pageSize,
        PartyType? type,
        bool? active,
        DateTimeOffset? createdAfter,
        DateTimeOffset? createdBefore,
        DateTimeOffset? modifiedAfter,
        DateTimeOffset? modifiedBefore,
        CancellationToken ct)
        => throw new NotSupportedException();

    public virtual Task<PagedResult<PartySearchResult>> SearchPartiesAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken ct,
        string? mode = null,
        string? caseId = null,
        Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null)
    {
        SearchCalls.Add(new SearchCall(query, page, pageSize));
        LastMode = mode;
        LastCaseId = caseId;
        LastRequestCustomizer = requestCustomizer;

        if (_failures.Count > 0)
        {
            throw _failures.Dequeue();
        }

        if (ThrowOnSearch is not null)
        {
            throw ThrowOnSearch;
        }

        return Task.FromResult(_responses.Count == 0
            ? new PagedResult<PartySearchResult> { Items = [], Page = 1, PageSize = 10, TotalCount = 0, TotalPages = 0 }
            : _responses.Dequeue());
    }
}
