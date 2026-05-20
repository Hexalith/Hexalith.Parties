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
    private const string PartyDetailProjectionActorType = "PartyDetailProjectionQueryActor";
    private const string PartyDetailProjectionType = "party-detail";
    private const string PartyDetailQueryType = "PartyDetail";
    private const string PartyIndexProjectionActorType = "PartyIndexProjectionQueryActor";
    private const string PartyIndexProjectionType = "party-index";
    private const string PartyIndexQueryType = "PartyIndex";
    private const string PartySearchQueryType = "PartySearch";

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
            Tenant: HttpPartiesCommandClient.GetValidatedTenant(_options),
            Domain: PartyDomain,
            AggregateId: partyId,
            QueryType: PartyDetailQueryType,
            ProjectionType: PartyDetailProjectionType,
            Payload: null,
            EntityId: partyId,
            ProjectionActorType: PartyDetailProjectionActorType);

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
            Tenant: HttpPartiesCommandClient.GetValidatedTenant(_options),
            Domain: PartyDomain,
            AggregateId: ListAggregateId,
            QueryType: PartyIndexQueryType,
            ProjectionType: PartyIndexProjectionType,
            Payload: JsonSerializer.SerializeToElement(payload, HttpPartiesCommandClient.JsonOptions),
            EntityId: ListAggregateId,
            ProjectionActorType: PartyIndexProjectionActorType);

        return PostQueryAsync<PagedResult<PartyIndexEntry>>(request, ct);
    }

    public Task<PagedResult<PartySearchResult>> SearchPartiesAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken ct,
        string? mode = null,
        string? caseId = null,
        Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null)
    {
        var payload = new SearchPartiesQueryPayload(query, page, pageSize, mode, caseId);
        var request = new SubmitQueryRequest(
            Tenant: HttpPartiesCommandClient.GetValidatedTenant(_options),
            Domain: PartyDomain,
            AggregateId: ListAggregateId,
            QueryType: PartySearchQueryType,
            ProjectionType: PartyIndexProjectionType,
            Payload: JsonSerializer.SerializeToElement(payload, HttpPartiesCommandClient.JsonOptions),
            EntityId: ListAggregateId,
            ProjectionActorType: PartyIndexProjectionActorType);

        return PostQueryAsync<PagedResult<PartySearchResult>>(request, ct, requestCustomizer);
    }

    private async Task<T> PostQueryAsync<T>(
        SubmitQueryRequest request,
        CancellationToken ct,
        Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, QueryGatewayPath)
        {
            Content = JsonContent.Create(request, options: HttpPartiesCommandClient.JsonOptions),
        };

        if (requestCustomizer is not null)
        {
            await requestCustomizer(httpRequest, ct).ConfigureAwait(false);
        }

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, ct)
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

    private sealed record SearchPartiesQueryPayload(string Query, int Page, int PageSize, string? Mode, string? CaseId);
}
