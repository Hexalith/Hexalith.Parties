using System.Net;

using Hexalith.Parties.Client;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Picker.Services;

public sealed class PartyPickerApiClient(IPartiesQueryClient queryClient)
{
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

        if (!await HasHostRequestContextAsync(request, cancellationToken).ConfigureAwait(false))
        {
            return AuthenticationRequired(request);
        }

        try
        {
            PagedResult<PartySearchResult> payload = await queryClient
                .SearchPartiesAsync(
                    query,
                    Math.Max(1, request.Page),
                    BoundPageSize(request.PageSize),
                    cancellationToken)
                .ConfigureAwait(false);

            return ToSearchResponse(request, payload);
        }
        catch (PartiesClientException ex) when (ex.Status > 0)
        {
            return FailureResponse(request, (HttpStatusCode)ex.Status);
        }
        catch (HttpRequestException)
        {
            return new PartyPickerSearchResponse
            {
                State = PartyPickerSearchState.TransientFailure,
                Page = request.Page,
                PageSize = BoundPageSize(request.PageSize),
                SafeReason = "The Parties client could not complete the request.",
            };
        }
    }

    public static int BoundPageSize(int pageSize)
        => Math.Clamp(pageSize <= 0 ? PartyPickerDefaults.PageSize : pageSize, 1, PartyPickerDefaults.MaxPageSize);

    private static async ValueTask<bool> HasHostRequestContextAsync(
        PartyPickerSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AccessTokenProvider is not null)
        {
            string? token = await request.AccessTokenProvider(cancellationToken).ConfigureAwait(false);
            return !string.IsNullOrWhiteSpace(token);
        }

        return request.RequestCustomizer is not null;
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
        HttpStatusCode statusCode)
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

    private static PartyPickerSearchResponse ToSearchResponse(
        PartyPickerSearchRequest request,
        PagedResult<PartySearchResult> payload)
    {
        if (payload.Items.Count == 0)
        {
            return new PartyPickerSearchResponse
            {
                State = PartyPickerSearchState.Empty,
                Page = payload.Page,
                PageSize = payload.PageSize,
                TotalCount = payload.TotalCount,
            };
        }

        return new PartyPickerSearchResponse
        {
            State = PartyPickerSearchState.Ready,
            Results = payload.Items,
            Page = payload.Page,
            PageSize = payload.PageSize,
            TotalCount = payload.TotalCount,
            Metadata = new PartyPickerSearchMetadata
            {
                SearchStatus = "Unavailable",
            },
        };
    }

    private static string NormalizeQuery(string query)
        => string.Concat(query.Where(c => !char.IsControl(c))).Trim();
}
