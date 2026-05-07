using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Contracts.Models;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.AdminPortal.Services;

public sealed class PartiesAdminPortalApiClient : IPartiesAdminPortalApiClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly HttpClient _httpClient;

    public PartiesAdminPortalApiClient(HttpClient httpClient, IOptions<PartiesAdminPortalOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;

        if (options.Value.ApiBaseAddress is not null)
        {
            _httpClient.BaseAddress = options.Value.ApiBaseAddress;
        }

        if (_httpClient.BaseAddress is null)
        {
            throw new InvalidOperationException(
                "PartiesAdminPortalApiClient requires HttpClient.BaseAddress or PartiesAdminPortalOptions.ApiBaseAddress to be configured.");
        }
    }

    public async Task<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>> ListPartiesAsync(
        AdminPortalListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await SendAsync(
            BuildListUrl(request),
            ReadPayloadAsync<PagedResult<PartyIndexEntry>>,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdminPortalQueryResult<PagedResult<PartySearchResult>>> SearchPartiesAsync(
        AdminPortalSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Backend /api/v1/parties/search accepts q, mode, caseId, page, pageSize only — type/active
        // are not in the controller signature, so they are intentionally not sent here.
        var parts = new List<string>
        {
            $"q={Uri.EscapeDataString(request.Query ?? string.Empty)}",
            $"page={AdminPortalQueryBounds.BoundPage(request.Page)}",
            $"pageSize={AdminPortalQueryBounds.BoundPageSize(request.PageSize)}",
        };

        return await SendSearchAsync(
            $"api/v1/parties/search?{string.Join('&', parts)}",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdminPortalQueryResult<PartyDetail>> GetPartyAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        return await SendAsync(
            $"api/v1/parties/{Uri.EscapeDataString(partyId)}",
            ReadPayloadAsync<PartyDetail>,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<AdminPortalQueryResult<T>> SendAsync<T>(
        string url,
        Func<HttpResponseMessage, CancellationToken, Task<T>> readPayload,
        CancellationToken cancellationToken,
        Func<AdminPortalQueryMetadata, object?, AdminPortalQueryMetadata>? enrichMetadata = null)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            ThrowIfFailure(response, cancellationToken);
            T payload = await readPayload(response, cancellationToken).ConfigureAwait(false);
            AdminPortalQueryMetadata metadata = ReadMetadata(response);
            if (enrichMetadata is not null)
            {
                metadata = enrichMetadata(metadata, payload);
            }

            return new(payload, metadata);
        }
        catch (HttpRequestException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, (int?)ex.StatusCode);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure);
        }
        catch (JsonException)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.Unknown);
        }
    }

    private async Task<AdminPortalQueryResult<PagedResult<PartySearchResult>>> SendSearchAsync(
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            ThrowIfFailure(response, cancellationToken);
            AdminPortalSearchResponse body = await ReadPayloadAsync<AdminPortalSearchResponse>(response, cancellationToken).ConfigureAwait(false);
            AdminPortalQueryMetadata wireMetadata = ReadMetadata(response);
            AdminPortalQueryMetadata metadata = wireMetadata with
            {
                SearchStatus = wireMetadata.SearchStatus ?? body.Status,
                SearchDegradedReason = wireMetadata.SearchDegradedReason ?? body.DegradedReason,
            };

            return new(body.Results, metadata);
        }
        catch (HttpRequestException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, (int?)ex.StatusCode);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure);
        }
        catch (JsonException)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.Unknown);
        }
    }

    private static string BuildListUrl(AdminPortalListRequest request)
    {
        var parts = new List<string>
        {
            $"page={AdminPortalQueryBounds.BoundPage(request.Page)}",
            $"pageSize={AdminPortalQueryBounds.BoundPageSize(request.PageSize)}",
        };

        if (request.Type is not null)
        {
            parts.Add($"type={Uri.EscapeDataString(request.Type.Value.ToString())}");
        }

        if (request.Active is not null)
        {
            parts.Add($"active={Uri.EscapeDataString(request.Active.Value.ToString().ToLowerInvariant())}");
        }

        AddDate(parts, "createdAfter", request.CreatedAfter);
        AddDate(parts, "createdBefore", request.CreatedBefore);
        AddDate(parts, "modifiedAfter", request.ModifiedAfter);
        AddDate(parts, "modifiedBefore", request.ModifiedBefore);

        return $"api/v1/parties?{string.Join('&', parts)}";
    }

    private static void AddDate(List<string> parts, string name, DateTimeOffset? value)
    {
        if (value is not null)
        {
            parts.Add($"{name}={Uri.EscapeDataString(value.Value.ToString("O"))}");
        }
    }

    private static async Task<T> ReadPayloadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        => await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminPortalQueryException(AdminPortalQueryFailureKind.Unknown, (int)response.StatusCode);

    private static AdminPortalQueryMetadata ReadMetadata(HttpResponseMessage response)
        => new(
            ServiceDegraded: ReadHeader(response, "X-Service-Degraded") is string degraded
                && bool.TryParse(degraded, out bool isDegraded)
                && isDegraded,
            StaleDataAge: ReadHeader(response, "X-Stale-Data-Age"),
            SearchStatus: ReadHeader(response, "X-Parties-Search-Status"),
            SearchDegradedReason: ReadHeader(response, "X-Parties-Search-Degraded-Reason"));

    private static string? ReadHeader(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;

    private static void ThrowIfFailure(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        AdminPortalQueryFailureKind kind = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => AdminPortalQueryFailureKind.AuthenticationRequired,
            HttpStatusCode.Forbidden => AdminPortalQueryFailureKind.Forbidden,
            HttpStatusCode.NotFound => AdminPortalQueryFailureKind.NotFound,
            HttpStatusCode.Gone => AdminPortalQueryFailureKind.Gone,
            HttpStatusCode.BadRequest => AdminPortalQueryFailureKind.Validation,
            HttpStatusCode.UnprocessableEntity => AdminPortalQueryFailureKind.Validation,
            HttpStatusCode.TooManyRequests => AdminPortalQueryFailureKind.TransientFailure,
            _ when (int)response.StatusCode >= 500 => AdminPortalQueryFailureKind.TransientFailure,
            _ => AdminPortalQueryFailureKind.Unknown,
        };

        throw new AdminPortalQueryException(kind, (int)response.StatusCode);
    }
}
