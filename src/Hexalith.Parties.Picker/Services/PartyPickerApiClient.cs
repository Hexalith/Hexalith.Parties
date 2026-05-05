using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Picker.Services;

public sealed class PartyPickerApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PartyPickerSearchResponse> SearchAsync(
        PartyPickerSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string query = NormalizeQuery(request.Query);
        if (query.Length == 0)
        {
            return new PartyPickerSearchResponse
            {
                State = PartyPickerSearchState.Idle,
                Page = request.Page,
                PageSize = BoundPageSize(request.PageSize),
            };
        }

        if (request.AccessTokenProvider is null && request.RequestCustomizer is null)
        {
            return AuthenticationRequired(request);
        }

        using HttpRequestMessage message = new(HttpMethod.Get, BuildSearchUri(request, query));

        if (request.AccessTokenProvider is not null)
        {
            string? token = await request.AccessTokenProvider(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                return AuthenticationRequired(request);
            }

            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (request.RequestCustomizer is not null)
        {
            await request.RequestCustomizer(message, cancellationToken).ConfigureAwait(false);
        }

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        PartyPickerSearchMetadata metadata = ReadMetadata(response);

        if (!response.IsSuccessStatusCode)
        {
            return FailureResponse(request, response.StatusCode, metadata);
        }

        PagedResult<PartySearchResult>? payload = await response.Content
            .ReadFromJsonAsync<PagedResult<PartySearchResult>>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || payload.Items.Count == 0)
        {
            return new PartyPickerSearchResponse
            {
                State = PartyPickerSearchState.Empty,
                Metadata = metadata,
                Page = payload?.Page ?? request.Page,
                PageSize = payload?.PageSize ?? BoundPageSize(request.PageSize),
                TotalCount = payload?.TotalCount ?? 0,
            };
        }

        return new PartyPickerSearchResponse
        {
            State = metadata.IsLocalOnly
                ? PartyPickerSearchState.LocalOnly
                : metadata.IsDegraded
                    ? PartyPickerSearchState.Degraded
                    : PartyPickerSearchState.Ready,
            Results = payload.Items,
            Metadata = metadata,
            Page = payload.Page,
            PageSize = payload.PageSize,
            TotalCount = payload.TotalCount,
        };
    }

    public static int BoundPageSize(int pageSize)
        => Math.Clamp(pageSize <= 0 ? PartyPickerDefaults.PageSize : pageSize, 1, PartyPickerDefaults.MaxPageSize);

    internal static Uri BuildSearchUri(PartyPickerSearchRequest request, string normalizedQuery)
    {
        int page = Math.Max(1, request.Page);
        int pageSize = BoundPageSize(request.PageSize);
        var parts = new List<string>
        {
            $"q={Uri.EscapeDataString(normalizedQuery)}",
            $"page={page.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}",
        };

        if (request.Mode is not null)
        {
            parts.Add($"mode={Uri.EscapeDataString(ToApiMode(request.Mode.Value))}");
        }

        if (!string.IsNullOrWhiteSpace(request.CaseId))
        {
            parts.Add($"caseId={Uri.EscapeDataString(request.CaseId)}");
        }

        string relative = $"api/v1/parties/search?{string.Join('&', parts)}";
        return request.ApiBaseAddress is null
            ? new Uri(relative, UriKind.Relative)
            : new Uri(request.ApiBaseAddress, relative);
    }

    private static PartyPickerSearchResponse AuthenticationRequired(PartyPickerSearchRequest request)
        => new()
        {
            State = PartyPickerSearchState.AuthenticationRequired,
            Page = request.Page,
            PageSize = BoundPageSize(request.PageSize),
            SafeReason = "Authentication is required.",
        };

    private static PartyPickerSearchResponse FailureResponse(
        PartyPickerSearchRequest request,
        HttpStatusCode statusCode,
        PartyPickerSearchMetadata metadata)
        => new()
        {
            State = statusCode switch
            {
                HttpStatusCode.Unauthorized => PartyPickerSearchState.Unauthorized,
                HttpStatusCode.Forbidden => PartyPickerSearchState.Forbidden,
                HttpStatusCode.NotFound => PartyPickerSearchState.NotFound,
                HttpStatusCode.Gone => PartyPickerSearchState.Gone,
                HttpStatusCode.RequestTimeout or
                    HttpStatusCode.TooManyRequests or
                    HttpStatusCode.BadGateway or
                    HttpStatusCode.ServiceUnavailable or
                    HttpStatusCode.GatewayTimeout => PartyPickerSearchState.TransientFailure,
                _ => PartyPickerSearchState.Error,
            },
            Metadata = metadata,
            Page = request.Page,
            PageSize = BoundPageSize(request.PageSize),
            SafeReason = statusCode switch
            {
                HttpStatusCode.Unauthorized => "Authentication is required.",
                HttpStatusCode.Forbidden => "Access to this tenant or party is forbidden.",
                HttpStatusCode.NotFound => "The selected party could not be found.",
                HttpStatusCode.Gone => "The selected party is no longer available.",
                _ => "The Parties API could not complete the request.",
            },
            StatusCode = statusCode,
        };

    private static PartyPickerSearchMetadata ReadMetadata(HttpResponseMessage response)
        => new()
        {
            SearchStatus = ReadHeader(response, "X-Parties-Search-Status"),
            DegradedReason = ReadHeader(response, "X-Parties-Search-Degraded-Reason"),
            ServiceDegraded = ReadHeader(response, "X-Service-Degraded"),
            StaleDataAge = ReadHeader(response, "X-Stale-Data-Age"),
        };

    private static string? ReadHeader(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;

    private static string NormalizeQuery(string query)
        => string.Concat(query.Where(c => !char.IsControl(c))).Trim();

    private static string ToApiMode(PartyPickerSearchMode mode)
        => mode switch
        {
            PartyPickerSearchMode.Lexical => "lexical",
            PartyPickerSearchMode.Semantic => "semantic",
            _ => "hybrid",
        };
}
