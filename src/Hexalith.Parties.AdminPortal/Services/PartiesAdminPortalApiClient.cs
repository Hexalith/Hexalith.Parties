using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Contracts.Models;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.AdminPortal.Services;

public sealed class PartiesAdminPortalApiClient : IPartiesAdminPortalApiClient
{
    // Backend serializes status enums via JsonStringEnumConverter() with no naming policy
    // (Enum.ToString → PascalCase). Use a matching converter on the client; do not apply
    // CamelCase to enum values, otherwise the wire status (LocalOnly/Degraded/Rich) cannot
    // be matched to the comparison literals in AdminPortalQueryMetadata.
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
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
        // are not in the controller signature, so they are intentionally not sent here. The UI
        // disables those filters in search mode (see PartiesAdminPortal.razor) so the user is
        // not surprised by silently-ignored selections.
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

    public async Task<AdminPortalRichSearchCapability> GetRichSearchCapabilityAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync("health", cancellationToken).ConfigureAwait(false);
            EnsureJsonResponse(response);

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return ParseRichSearchCapability(document.RootElement);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException
            or IOException
            or JsonException
            or AdminPortalQueryException
            or TaskCanceledException)
        {
            return AdminPortalRichSearchCapability.Degraded($"Rich search probe unavailable: {ex.GetType().Name}");
        }
    }

    private async Task<AdminPortalQueryResult<T>> SendAsync<T>(
        string url,
        Func<HttpResponseMessage, CancellationToken, Task<T?>> readPayload,
        CancellationToken cancellationToken,
        Func<AdminPortalQueryMetadata, object?, AdminPortalQueryMetadata>? enrichMetadata = null)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            ThrowIfFailure(response, cancellationToken);

            // Treat 204 No Content / 304 Not Modified as "empty result"; both legitimately have no body.
            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotModified)
            {
                return new(default!, ReadMetadata(response));
            }

            EnsureJsonResponse(response);

            T? payload = await readPayload(response, cancellationToken).ConfigureAwait(false);
            if (payload is null)
            {
                throw new AdminPortalQueryException(AdminPortalQueryFailureKind.Unknown, (int)response.StatusCode);
            }

            AdminPortalQueryMetadata metadata = ReadMetadata(response);
            if (enrichMetadata is not null)
            {
                metadata = enrichMetadata(metadata, payload);
            }

            return new(payload, metadata);
        }
        catch (HttpRequestException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, (int?)ex.StatusCode, innerException: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (IOException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (JsonException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.Unknown, innerException: ex);
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

            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotModified)
            {
                return new(EmptySearchPage(), ReadMetadata(response));
            }

            EnsureJsonResponse(response);

            AdminPortalSearchResponse? body = await ReadPayloadAsync<AdminPortalSearchResponse>(response, cancellationToken).ConfigureAwait(false);
            AdminPortalQueryMetadata wireMetadata = ReadMetadata(response);
            PagedResult<PartySearchResult> results = body?.Results ?? EmptySearchPage();
            AdminPortalQueryMetadata metadata = wireMetadata with
            {
                SearchStatus = wireMetadata.SearchStatus ?? body?.Status,
                SearchDegradedReason = wireMetadata.SearchDegradedReason ?? body?.DegradedReason,
            };

            return new(results, metadata);
        }
        catch (HttpRequestException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, (int?)ex.StatusCode, innerException: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (IOException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (JsonException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.Unknown, innerException: ex);
        }
    }

    private static PagedResult<PartySearchResult> EmptySearchPage()
        => new() { Items = Array.Empty<PartySearchResult>(), Page = 0, PageSize = 0, TotalCount = 0, TotalPages = 0 };

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

    private static async Task<T?> ReadPayloadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        => await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken).ConfigureAwait(false);

    private static void EnsureJsonResponse(HttpResponseMessage response)
    {
        // Captive portals and gateway interstitials sometimes return 200 OK with text/html.
        // Treat that as authentication-required so the user is redirected to sign in instead
        // of seeing a generic load failure.
        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is null)
        {
            return;
        }

        if (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (mediaType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.AuthenticationRequired, (int)response.StatusCode);
        }

        throw new AdminPortalQueryException(AdminPortalQueryFailureKind.Unknown, (int)response.StatusCode);
    }

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

    private static AdminPortalRichSearchCapability ParseRichSearchCapability(JsonElement root)
    {
        if (!root.TryGetProperty("results", out JsonElement results)
            || !results.TryGetProperty("memories-search", out JsonElement memoriesSearch))
        {
            return AdminPortalRichSearchCapability.LocalOnly("memories-search health check unavailable");
        }

        string? status = ReadStringProperty(memoriesSearch, "status");
        string? description = ReadStringProperty(memoriesSearch, "description");
        JsonElement data = memoriesSearch.TryGetProperty("data", out JsonElement dataElement)
            ? dataElement
            : default;
        bool enabled = ReadBoolProperty(data, "enabled") == true;
        if (!enabled)
        {
            return AdminPortalRichSearchCapability.LocalOnly(description ?? "Memories rich search is disabled");
        }

        bool searchReachable = ReadBoolProperty(data, "searchReachable") != false;
        bool degradedReportedByMemories = ReadBoolProperty(data, "degradedReportedByMemories") == true;
        if (string.Equals(status, "Healthy", StringComparison.OrdinalIgnoreCase)
            && searchReachable
            && !degradedReportedByMemories)
        {
            return AdminPortalRichSearchCapability.Available();
        }

        return AdminPortalRichSearchCapability.Degraded(description ?? $"memories-search reported {status ?? "unknown"}");
    }

    private static string? ReadStringProperty(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;

    private static bool? ReadBoolProperty(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out JsonElement property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.GetBoolean()
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
            // Distinguish missing-tenant 403 (Problem.Type contains "tenant") from admin-role
            // 403; the former routes to the TenantRequired UX, the latter to AdminRequired.
            HttpStatusCode.Forbidden => IsTenantProblem(response)
                ? AdminPortalQueryFailureKind.TenantRequired
                : AdminPortalQueryFailureKind.Forbidden,
            HttpStatusCode.NotFound => AdminPortalQueryFailureKind.NotFound,
            HttpStatusCode.Gone => AdminPortalQueryFailureKind.Gone,
            HttpStatusCode.BadRequest => AdminPortalQueryFailureKind.Validation,
            HttpStatusCode.UnprocessableEntity => AdminPortalQueryFailureKind.Validation,
            HttpStatusCode.TooManyRequests => AdminPortalQueryFailureKind.TransientFailure,
            _ when (int)response.StatusCode >= 500 => AdminPortalQueryFailureKind.TransientFailure,
            _ => AdminPortalQueryFailureKind.Unknown,
        };

        TimeSpan? retryAfter = ReadRetryAfter(response.Headers.RetryAfter);
        string? validationDetail = kind == AdminPortalQueryFailureKind.Validation
            ? TryReadProblemDetail(response)
            : null;

        throw new AdminPortalQueryException(kind, (int)response.StatusCode, validationDetail, retryAfter);
    }

    private static bool IsTenantProblem(HttpResponseMessage response)
    {
        // Heuristic: backend emits a "tenant"-flavored problem type for missing-tenant 403s.
        // A header indicator (X-Tenant-Required) is more robust if the backend later adds it;
        // for now we inspect the response header name space.
        return response.Headers.TryGetValues("X-Tenant-Required", out _);
    }

    private static TimeSpan? ReadRetryAfter(RetryConditionHeaderValue? header)
    {
        if (header is null)
        {
            return null;
        }

        if (header.Delta is TimeSpan delta)
        {
            return delta;
        }

        if (header.Date is DateTimeOffset when)
        {
            TimeSpan diff = when - DateTimeOffset.UtcNow;
            return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
        }

        return null;
    }

    private static string? TryReadProblemDetail(HttpResponseMessage response)
    {
        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is null
            || !(mediaType.Equals("application/problem+json", StringComparison.OrdinalIgnoreCase)
                || mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        try
        {
            using Stream stream = response.Content.ReadAsStream();
            using JsonDocument document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("detail", out JsonElement detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString();
            }

            if (document.RootElement.TryGetProperty("title", out JsonElement title) && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }

        return null;
    }
}
