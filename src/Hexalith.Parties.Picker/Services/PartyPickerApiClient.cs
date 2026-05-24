using System.Net;
using System.Net.Http.Headers;

using Hexalith.Parties.Client;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Picker.Services;

public sealed class PartyPickerApiClient(IPartiesQueryClient queryClient)
{
    public async Task<PartyPickerSelection> ResolveSelectedPartyAsync(
        PartyPickerSelectedPartyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string partyId = NormalizeQuery(request.PartyId);
        if (partyId.Length == 0)
        {
            return new PartyPickerSelection
            {
                PartyId = request.PartyId,
                State = PartyPickerSelectionState.NotFound,
                SafeReason = "The selected party could not be found.",
            };
        }

        bool hasContext;
        try
        {
            hasContext = await HasHostRequestContextAsync(
                request.AccessTokenProvider,
                request.RequestCustomizer,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SelectionFailure(partyId, PartyPickerSelectionState.AuthenticationRequired, "Authentication is required.");
        }

        if (!hasContext)
        {
            return SelectionFailure(partyId, PartyPickerSelectionState.AuthenticationRequired, "Authentication is required.");
        }

        try
        {
            PartyDetail detail = await queryClient
                .GetPartyAsync(
                    partyId,
                    cancellationToken,
                    CreateRequestCustomizer(request.AccessTokenProvider, request.RequestCustomizer))
                .ConfigureAwait(false);

            return new PartyPickerSelection
            {
                PartyId = partyId,
                State = PartyPickerSelectionState.Available,
                DisplayName = detail.DisplayName,
                PartyType = detail.Type,
                IsActive = detail.IsActive,
                IsErased = detail.IsErased,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PartiesClientException ex) when (ex.Status > 0)
        {
            return SelectionFailure(partyId, (HttpStatusCode)ex.Status);
        }
        catch (PartiesClientException)
        {
            return SelectionFailure(
                partyId,
                PartyPickerSelectionState.Unavailable,
                "The Parties client returned an invalid response.");
        }
        catch (HttpRequestException)
        {
            return SelectionFailure(
                partyId,
                PartyPickerSelectionState.TransientFailure,
                "The Parties client could not complete the request.");
        }
        catch (Exception)
        {
            return SelectionFailure(
                partyId,
                PartyPickerSelectionState.Unavailable,
                "The Parties client could not complete the request.");
        }
    }

    public async Task<PartyPickerSearchResponse> SearchAsync(
        PartyPickerSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        int boundedPage = Math.Max(1, request.Page);
        int boundedPageSize = BoundPageSize(request.PageSize);
        string query = NormalizeQuery(request.Query);
        if (query.Length == 0)
        {
            return new PartyPickerSearchResponse
            {
                State = PartyPickerSearchState.Idle,
                Page = boundedPage,
                PageSize = boundedPageSize,
            };
        }

        bool hasContext;
        try
        {
            hasContext = await HasHostRequestContextAsync(
                request.AccessTokenProvider,
                request.RequestCustomizer,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return AuthenticationRequired(request, boundedPage, boundedPageSize);
        }

        if (!hasContext)
        {
            return AuthenticationRequired(request, boundedPage, boundedPageSize);
        }

        try
        {
            PagedResult<PartySearchResult> payload = await queryClient
                .SearchPartiesAsync(
                    query,
                    boundedPage,
                    boundedPageSize,
                    cancellationToken,
                    request.Mode?.ToString().ToLowerInvariant(),
                    request.CaseId,
                    CreateRequestCustomizer(request.AccessTokenProvider, request.RequestCustomizer))
                .ConfigureAwait(false);

            return ToSearchResponse(payload, boundedPage, boundedPageSize);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PartiesClientException ex) when (ex.Status > 0)
        {
            return FailureResponse(boundedPage, boundedPageSize, (HttpStatusCode)ex.Status);
        }
        catch (PartiesClientException)
        {
            return new PartyPickerSearchResponse
            {
                State = PartyPickerSearchState.Error,
                Page = boundedPage,
                PageSize = boundedPageSize,
                SafeReason = "The Parties client returned an invalid response.",
            };
        }
        catch (HttpRequestException)
        {
            return new PartyPickerSearchResponse
            {
                State = PartyPickerSearchState.TransientFailure,
                Page = boundedPage,
                PageSize = boundedPageSize,
                SafeReason = "The Parties client could not complete the request.",
            };
        }
        catch (Exception)
        {
            return new PartyPickerSearchResponse
            {
                State = PartyPickerSearchState.Error,
                Page = boundedPage,
                PageSize = boundedPageSize,
                SafeReason = "The Parties client could not complete the request.",
            };
        }
    }

    public static int BoundPageSize(int pageSize)
        => Math.Clamp(pageSize <= 0 ? PartyPickerDefaults.PageSize : pageSize, 1, PartyPickerDefaults.MaxPageSize);

    private static async ValueTask<bool> HasHostRequestContextAsync(
        Func<CancellationToken, ValueTask<string?>>? accessTokenProvider,
        Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer,
        CancellationToken cancellationToken)
    {
        if (accessTokenProvider is not null)
        {
            string? token = await accessTokenProvider(cancellationToken).ConfigureAwait(false);
            return !string.IsNullOrWhiteSpace(token);
        }

        return requestCustomizer is not null;
    }

    private static Func<HttpRequestMessage, CancellationToken, ValueTask>? CreateRequestCustomizer(
        Func<CancellationToken, ValueTask<string?>>? accessTokenProvider,
        Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer)
    {
        if (accessTokenProvider is null)
        {
            return requestCustomizer;
        }

        return async (message, cancellationToken) =>
        {
            string? token = await accessTokenProvider(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            if (requestCustomizer is not null)
            {
                await requestCustomizer(message, cancellationToken).ConfigureAwait(false);
            }
        };
    }

    private static PartyPickerSearchResponse AuthenticationRequired(
        PartyPickerSearchRequest request,
        int boundedPage,
        int boundedPageSize)
        => new()
        {
            State = PartyPickerSearchState.AuthenticationRequired,
            Page = boundedPage,
            PageSize = boundedPageSize,
            SafeReason = "Authentication is required.",
        };

    private static PartyPickerSearchResponse FailureResponse(
        int boundedPage,
        int boundedPageSize,
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
            Page = boundedPage,
            PageSize = boundedPageSize,
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

    private static PartyPickerSelection SelectionFailure(
        string partyId,
        HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.Unauthorized => SelectionFailure(
                partyId,
                PartyPickerSelectionState.Unauthorized,
                "Authentication is required."),
            HttpStatusCode.Forbidden => SelectionFailure(
                partyId,
                PartyPickerSelectionState.Forbidden,
                "Access to this tenant or party is forbidden."),
            HttpStatusCode.NotFound => SelectionFailure(
                partyId,
                PartyPickerSelectionState.NotFound,
                "The selected party could not be found."),
            HttpStatusCode.Gone => SelectionFailure(
                partyId,
                PartyPickerSelectionState.Gone,
                "The selected party is no longer available."),
            HttpStatusCode.RequestTimeout or
                HttpStatusCode.TooManyRequests or
                HttpStatusCode.BadGateway or
                HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout => SelectionFailure(
                    partyId,
                    PartyPickerSelectionState.TransientFailure,
                    "The Parties API could not complete the request."),
            _ => SelectionFailure(
                partyId,
                PartyPickerSelectionState.Unavailable,
                "The Parties API could not complete the request."),
        };

    private static PartyPickerSelection SelectionFailure(
        string partyId,
        PartyPickerSelectionState state,
        string safeReason)
        => new()
        {
            PartyId = partyId,
            State = state,
            SafeReason = safeReason,
        };

    private static PartyPickerSearchResponse ToSearchResponse(
        PagedResult<PartySearchResult> payload,
        int boundedPage,
        int boundedPageSize)
    {
        IReadOnlyList<PartySearchResult> items = payload.Items is null
            ? []
            : [.. payload.Items
                .Where(static r => r?.Party is not null)
                .Take(boundedPageSize)];

        int page = payload.Page <= 0 ? boundedPage : payload.Page;
        int pageSize = payload.PageSize <= 0
            ? boundedPageSize
            : Math.Min(BoundPageSize(payload.PageSize), boundedPageSize);

        if (items.Count == 0)
        {
            return new PartyPickerSearchResponse
            {
                State = PartyPickerSearchState.Empty,
                Page = page,
                PageSize = pageSize,
                TotalCount = payload.TotalCount,
                Metadata = new PartyPickerSearchMetadata
                {
                    SearchStatus = "Unavailable",
                },
            };
        }

        return new PartyPickerSearchResponse
        {
            State = PartyPickerSearchState.Ready,
            Results = items,
            Page = page,
            PageSize = pageSize,
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
