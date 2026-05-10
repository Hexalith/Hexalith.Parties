using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Client;

public sealed class HttpPartiesQueryClient : IPartiesQueryClient
{
    private const string PartyDomain = "party";
    private const string QueryGatewayPath = "api/v1/queries";
    private const string ListAggregateId = "parties";
    private const string DetailProjectionActorType = "PartyDetailProjectionActor";

    private readonly HttpClient _httpClient;
    private readonly PartiesClientOptions _options;

    public HttpPartiesQueryClient(HttpClient httpClient)
        : this(httpClient, Options.Create(new PartiesClientOptions()))
    {
    }

    [ActivatorUtilitiesConstructor]
    public HttpPartiesQueryClient(HttpClient httpClient, IOptions<PartiesClientOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<PartyDetail> GetPartyAsync(string partyId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        var request = new SubmitQueryRequest(
            Tenant: _options.Tenant,
            Domain: PartyDomain,
            AggregateId: partyId,
            QueryType: "GetParty",
            ProjectionType: "PartyDetail",
            Payload: null,
            EntityId: partyId,
            ProjectionActorType: DetailProjectionActorType);

        return PostQueryAsync<PartyDetail>(request, ct);
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
    {
        var payload = new ListPartiesQueryPayload(
            page,
            pageSize,
            type?.ToString(),
            active,
            FormatDate(createdAfter),
            FormatDate(createdBefore),
            FormatDate(modifiedAfter),
            FormatDate(modifiedBefore));

        var request = new SubmitQueryRequest(
            Tenant: _options.Tenant,
            Domain: PartyDomain,
            AggregateId: ListAggregateId,
            QueryType: "ListParties",
            ProjectionType: "PartyIndex",
            Payload: JsonSerializer.SerializeToElement(payload, HttpPartiesCommandClient.JsonOptions),
            EntityId: ListAggregateId);

        return PostQueryAsync<PagedResult<PartyIndexEntry>>(request, ct);
    }

    public Task<PagedResult<PartySearchResult>> SearchPartiesAsync(string query, int page, int pageSize, CancellationToken ct)
    {
        var payload = new SearchPartiesQueryPayload(query, page, pageSize);
        var request = new SubmitQueryRequest(
            Tenant: _options.Tenant,
            Domain: PartyDomain,
            AggregateId: ListAggregateId,
            QueryType: "SearchParties",
            ProjectionType: "PartySearch",
            Payload: JsonSerializer.SerializeToElement(payload, HttpPartiesCommandClient.JsonOptions),
            EntityId: ListAggregateId);

        return PostQueryAsync<PagedResult<PartySearchResult>>(request, ct);
    }

    private async Task<T> PostQueryAsync<T>(SubmitQueryRequest request, CancellationToken ct)
    {
        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(QueryGatewayPath, request, HttpPartiesCommandClient.JsonOptions, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await HttpPartiesCommandClient.ThrowOnErrorAsync(response, ct).ConfigureAwait(false);
        }

        string? correlationId = null;
        try
        {
            using JsonDocument doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                cancellationToken: ct).ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("correlationId", out JsonElement correlationIdElement))
            {
                correlationId = correlationIdElement.GetString();
            }

            if (doc.RootElement.TryGetProperty("payload", out JsonElement payloadElement)
                && payloadElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                return payloadElement.Deserialize<T>(HttpPartiesCommandClient.JsonOptions)
                    ?? throw new PartiesClientException(200, "OK", null, "Response body was null.", correlationId);
            }
        }
        catch (JsonException)
        {
            throw new PartiesClientException(
                (int)response.StatusCode,
                response.ReasonPhrase ?? "OK",
                null,
                "Response did not contain a valid query payload.",
                correlationId);
        }

        throw new PartiesClientException(
            (int)response.StatusCode,
            response.ReasonPhrase ?? "OK",
            null,
            "Response did not contain a valid query payload.",
            correlationId);
    }

    private static string? FormatDate(DateTimeOffset? value)
        => value?.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private sealed record ListPartiesQueryPayload(
        int Page,
        int PageSize,
        string? Type,
        bool? Active,
        string? CreatedAfter,
        string? CreatedBefore,
        string? ModifiedAfter,
        string? ModifiedBefore);

    private sealed record SearchPartiesQueryPayload(string Query, int Page, int PageSize);
}
