using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Picker.Tests.Fakes;

internal sealed record SearchCall(string Query, int Page, int PageSize);

internal class RecordingPartiesQueryClient : IPartiesQueryClient
{
    private readonly Queue<PagedResult<PartySearchResult>> _responses = [];
    private readonly Queue<Exception> _failures = [];
    private readonly Queue<PartyDetail> _detailResponses = [];
    private readonly Queue<Exception> _detailFailures = [];

    public List<SearchCall> SearchCalls { get; } = [];

    public List<string> GetCalls { get; } = [];

    public Exception? ThrowOnSearch { get; set; }

    public Exception? ThrowOnGet { get; set; }

    public Func<HttpRequestMessage, CancellationToken, ValueTask>? LastRequestCustomizer { get; protected set; }

    public string? LastMode { get; protected set; }

    public string? LastCaseId { get; protected set; }

    public void Enqueue(PagedResult<PartySearchResult> response)
        => _responses.Enqueue(response);

    public void EnqueueFailure(Exception exception)
        => _failures.Enqueue(exception);

    public void EnqueueDetail(PartyDetail detail)
        => _detailResponses.Enqueue(detail);

    public void EnqueueGetFailure(Exception exception)
        => _detailFailures.Enqueue(exception);

    public virtual Task<PartyDetail> GetPartyAsync(
        string partyId,
        CancellationToken ct,
        Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null)
    {
        GetCalls.Add(partyId);
        LastRequestCustomizer = requestCustomizer;

        if (_detailFailures.Count > 0)
        {
            throw _detailFailures.Dequeue();
        }

        if (ThrowOnGet is not null)
        {
            throw ThrowOnGet;
        }

        return Task.FromResult(_detailResponses.Count == 0
            ? new PartyDetail
            {
                Id = partyId,
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = partyId,
                SortName = partyId,
                CreatedAt = DateTimeOffset.Parse("2026-05-05T00:00:00Z"),
                LastModifiedAt = DateTimeOffset.Parse("2026-05-05T00:00:00Z"),
            }
            : _detailResponses.Dequeue());
    }

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
        Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null,
        PartyType? type = null,
        bool? active = null)
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
