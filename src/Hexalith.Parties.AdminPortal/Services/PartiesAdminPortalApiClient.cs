using Hexalith.FrontComposer.Contracts.Communication;
using Hexalith.Parties.Client;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.AdminPortal.Services;

public sealed class PartiesAdminPortalApiClient : IPartiesAdminPortalApiClient
{
    private const string ListCacheDiscriminator = "parties-admin-list-v1";
    private const string SearchCacheDiscriminator = "parties-admin-search-v1";
    private const string DetailCacheDiscriminator = "parties-admin-detail-v1";

    private readonly IPartiesQueryClient? _partiesQueryClient;
    private readonly IAdminPortalGdprClient? _gdprClient;
    private readonly IQueryService? _queryService;
    private readonly PartiesAdminPortalOptions _options;

    [ActivatorUtilitiesConstructor]
    public PartiesAdminPortalApiClient(IServiceProvider serviceProvider, IOptions<PartiesAdminPortalOptions> options)
        : this(
            serviceProvider?.GetService<IPartiesQueryClient>(),
            serviceProvider?.GetService<IAdminPortalGdprClient>(),
            serviceProvider?.GetService<IQueryService>(),
            options)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
    }

    public PartiesAdminPortalApiClient(IQueryService queryService, IOptions<PartiesAdminPortalOptions> options)
        : this(null, null, queryService, options)
    {
    }

    private PartiesAdminPortalApiClient(
        IPartiesQueryClient? partiesQueryClient,
        IAdminPortalGdprClient? gdprClient,
        IQueryService? queryService,
        IOptions<PartiesAdminPortalOptions> options)
    {
        _partiesQueryClient = partiesQueryClient;
        _gdprClient = gdprClient;
        _queryService = queryService;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>> ListPartiesAsync(
        AdminPortalListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        int page = AdminPortalQueryBounds.BoundPage(request.Page);
        int pageSize = AdminPortalQueryBounds.BoundPageSize(request.PageSize);
        if (_partiesQueryClient is not null)
        {
            PagedResult<PartyIndexEntry> typedResult = await ExecutePartiesQueryAsync(
                (client, token) => client.ListPartiesAsync(
                    page,
                    pageSize,
                    request.Type,
                    request.Active,
                    request.CreatedAfter,
                    request.CreatedBefore,
                    request.ModifiedAfter,
                    request.ModifiedBefore,
                    token),
                cancellationToken).ConfigureAwait(false);
            return new(typedResult, AdminPortalQueryMetadata.Empty);
        }

        QueryRequest query = new(
            ProjectionType: RequireContract(_options.ListProjectionType, nameof(_options.ListProjectionType)),
            TenantId: null,
            Skip: (page - 1) * pageSize,
            Take: pageSize,
            ColumnFilters: BuildListFilters(request),
            Domain: Domain,
            QueryType: RequireContract(_options.ListQueryType, nameof(_options.ListQueryType)),
            CacheDiscriminator: ListCacheDiscriminator);

        QueryResult<PartyIndexEntry> result = await ExecuteAsync<PartyIndexEntry>(query, cancellationToken).ConfigureAwait(false);
        return new(ToPage(result, page, pageSize), MetadataFrom(result));
    }

    public async Task<AdminPortalQueryResult<PagedResult<PartySearchResult>>> SearchPartiesAsync(
        AdminPortalSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        int page = AdminPortalQueryBounds.BoundPage(request.Page);
        int pageSize = AdminPortalQueryBounds.BoundPageSize(request.PageSize);
        if (_partiesQueryClient is not null)
        {
            PagedResult<PartySearchResult> typedResult = await ExecutePartiesQueryAsync(
                (client, token) => client.SearchPartiesAsync(request.Query ?? string.Empty, page, pageSize, token),
                cancellationToken).ConfigureAwait(false);
            return new(typedResult, AdminPortalQueryMetadata.Empty);
        }

        QueryRequest query = new(
            ProjectionType: RequireContract(_options.SearchProjectionType, nameof(_options.SearchProjectionType)),
            TenantId: null,
            Skip: (page - 1) * pageSize,
            Take: pageSize,
            SearchQuery: request.Query ?? string.Empty,
            Domain: Domain,
            QueryType: RequireContract(_options.SearchQueryType, nameof(_options.SearchQueryType)),
            CacheDiscriminator: SearchCacheDiscriminator);

        QueryResult<PartySearchResult> result = await ExecuteAsync<PartySearchResult>(query, cancellationToken).ConfigureAwait(false);
        return new(ToPage(result, page, pageSize), MetadataFrom(result));
    }

    public Task<AdminPortalRichSearchCapability> GetRichSearchCapabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AdminPortalRichSearchCapability.LocalOnly(
            "Rich-search capability query is blocked until Story 12.4/12.5 freezes the EventStore query contract."));
    }

    public async Task<AdminPortalQueryResult<PartyDetail>> GetPartyAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        if (_partiesQueryClient is not null)
        {
            PartyDetail typedDetail = await ExecutePartiesQueryAsync(
                (client, token) => client.GetPartyAsync(partyId, token),
                cancellationToken).ConfigureAwait(false);
            return new(typedDetail, AdminPortalQueryMetadata.Empty);
        }

        QueryRequest query = new(
            ProjectionType: RequireContract(_options.DetailProjectionType, nameof(_options.DetailProjectionType)),
            TenantId: null,
            Take: 1,
            Domain: Domain,
            AggregateId: partyId,
            QueryType: RequireContract(_options.DetailQueryType, nameof(_options.DetailQueryType)),
            EntityId: partyId,
            ProjectionActorType: _options.DetailProjectionActorType,
            CacheDiscriminator: DetailCacheDiscriminator);

        QueryResult<PartyDetail> result = await ExecuteAsync<PartyDetail>(query, cancellationToken).ConfigureAwait(false);
        PartyDetail? detail = result.Items.FirstOrDefault();
        if (detail is null)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.NotFound);
        }

        return new(detail, MetadataFrom(result));
    }

    public Task<AdminPortalGdprCommandResult> RequestErasureAsync(string partyId, CancellationToken cancellationToken)
        => ExecuteGdprCommandAsync(client => client.RequestErasureAsync(partyId, cancellationToken));

    public Task<PartyErasureStatusRecord?> GetErasureStatusAsync(string partyId, CancellationToken cancellationToken)
        => ExecuteGdprQueryAsync(client => client.GetErasureStatusAsync(partyId, cancellationToken));

    public Task<ErasureCertificate?> GetErasureCertificateAsync(string partyId, CancellationToken cancellationToken)
        => ExecuteGdprQueryAsync(client => client.GetErasureCertificateAsync(partyId, cancellationToken));

    public Task<AdminPortalGdprCommandResult> RetryErasureVerificationAsync(string partyId, CancellationToken cancellationToken)
        => ExecuteGdprCommandAsync(client => client.RetryErasureVerificationAsync(partyId, cancellationToken));

    public Task<AdminPortalGdprCommandResult> RestrictProcessingAsync(
        string partyId,
        string? reason,
        CancellationToken cancellationToken)
        => ExecuteGdprCommandAsync(client => client.RestrictProcessingAsync(partyId, reason, cancellationToken));

    public Task<AdminPortalGdprCommandResult> LiftRestrictionAsync(string partyId, CancellationToken cancellationToken)
        => ExecuteGdprCommandAsync(client => client.LiftRestrictionAsync(partyId, cancellationToken));

    public Task<AdminPortalGdprCommandResult> AddConsentAsync(
        string partyId,
        string channelId,
        string purpose,
        LawfulBasis lawfulBasis,
        CancellationToken cancellationToken)
        => ExecuteGdprCommandAsync(client => client.AddConsentAsync(partyId, channelId, purpose, lawfulBasis, cancellationToken));

    public Task<AdminPortalGdprCommandResult> RevokeConsentAsync(
        string partyId,
        string consentId,
        CancellationToken cancellationToken)
        => ExecuteGdprCommandAsync(client => client.RevokeConsentAsync(partyId, consentId, cancellationToken));

    public Task<IReadOnlyList<ConsentRecord>> GetConsentAsync(string partyId, CancellationToken cancellationToken)
        => ExecuteGdprQueryAsync(client => client.GetConsentAsync(partyId, cancellationToken));

    public Task<AdminPortalExportDownload> ExportPartyDataAsync(string partyId, CancellationToken cancellationToken)
        => ExecuteGdprQueryAsync(client => client.ExportPartyDataAsync(partyId, cancellationToken));

    public Task<IReadOnlyList<ProcessingActivityRecord>> GetProcessingRecordsAsync(string partyId, CancellationToken cancellationToken)
        => ExecuteGdprQueryAsync(client => client.GetProcessingRecordsAsync(partyId, cancellationToken));

    private string Domain => string.IsNullOrWhiteSpace(_options.Domain) ? "party" : _options.Domain.Trim();

    private async Task<QueryResult<T>> ExecuteAsync<T>(QueryRequest query, CancellationToken cancellationToken)
    {
        IQueryService queryService = _queryService ?? throw new AdminPortalQueryException(
            AdminPortalQueryFailureKind.ContractUnavailable,
            validationDetail: "Neither IPartiesQueryClient nor FrontComposer IQueryService is registered for the Parties admin portal.");

        try
        {
            return await queryService.QueryAsync<T>(query, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AuthRedirectRequiredException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.AuthenticationRequired, innerException: ex);
        }
        catch (QueryFailureException ex)
        {
            throw new AdminPortalQueryException(MapFailureKind(ex), ex.Problem.Status, retryAfter: ex.RetryAfter, innerException: ex);
        }
        catch (TimeoutException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
    }

    private async Task<T> ExecutePartiesQueryAsync<T>(
        Func<IPartiesQueryClient, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await operation(_partiesQueryClient!, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PartiesClientException ex)
        {
            throw new AdminPortalQueryException(MapFailureKind(ex), ex.Status, innerException: ex);
        }
        catch (TimeoutException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
    }

    private async Task<AdminPortalGdprCommandResult> ExecuteGdprCommandAsync(
        Func<IAdminPortalGdprClient, Task<AdminPortalGdprCommandResult>> operation)
    {
        IAdminPortalGdprClient client = _gdprClient
            ?? throw new AdminPortalQueryException(AdminPortalQueryFailureKind.ContractUnavailable);

        try
        {
            return await operation(client).ConfigureAwait(false);
        }
        catch (PartiesClientException ex)
        {
            return new AdminPortalGdprCommandResult(MapGdprOutcome(ex), ex.CorrelationId, ex.Detail);
        }
    }

    private async Task<T> ExecuteGdprQueryAsync<T>(Func<IAdminPortalGdprClient, Task<T>> operation)
    {
        IAdminPortalGdprClient client = _gdprClient
            ?? throw new AdminPortalQueryException(AdminPortalQueryFailureKind.ContractUnavailable);

        try
        {
            return await operation(client).ConfigureAwait(false);
        }
        catch (PartiesClientException ex)
        {
            throw new AdminPortalQueryException(MapFailureKind(ex), ex.Status, innerException: ex);
        }
    }

    private static AdminPortalQueryFailureKind MapFailureKind(QueryFailureException ex)
        => ex.Kind switch
        {
            QueryFailureKind.Unauthorized => AdminPortalQueryFailureKind.AuthenticationRequired,
            QueryFailureKind.Forbidden => IsTenantProblem(ex.Problem)
                ? AdminPortalQueryFailureKind.TenantRequired
                : AdminPortalQueryFailureKind.Forbidden,
            QueryFailureKind.NotFound => AdminPortalQueryFailureKind.NotFound,
            QueryFailureKind.RateLimited => AdminPortalQueryFailureKind.TransientFailure,
            _ => AdminPortalQueryFailureKind.Unknown,
        };

    private static bool IsTenantProblem(ProblemDetailsPayload problem)
        => ContainsTenant(problem.Title)
            || ContainsTenant(problem.Detail)
            || problem.GlobalErrors.Any(ContainsTenant);

    private static AdminPortalQueryFailureKind MapFailureKind(PartiesClientException ex)
        => ex.Status switch
        {
            401 => AdminPortalQueryFailureKind.AuthenticationRequired,
            403 => ContainsTenant(ex.Title) || ContainsTenant(ex.Detail)
                ? AdminPortalQueryFailureKind.TenantRequired
                : AdminPortalQueryFailureKind.Forbidden,
            404 => AdminPortalQueryFailureKind.NotFound,
            410 => AdminPortalQueryFailureKind.Gone,
            400 or 422 => AdminPortalQueryFailureKind.Validation,
            408 or 429 => AdminPortalQueryFailureKind.TransientFailure,
            >= 500 => AdminPortalQueryFailureKind.TransientFailure,
            _ => AdminPortalQueryFailureKind.Unknown,
        };

    private static AdminPortalGdprOutcome MapGdprOutcome(PartiesClientException ex)
        => ex.Status switch
        {
            401 => AdminPortalGdprOutcome.AuthenticationRequired,
            403 => ContainsTenant(ex.Title) || ContainsTenant(ex.Detail)
                ? AdminPortalGdprOutcome.MissingTenant
                : AdminPortalGdprOutcome.Forbidden,
            404 => AdminPortalGdprOutcome.NotFound,
            409 => AdminPortalGdprOutcome.ErasureInProgress,
            410 => AdminPortalGdprOutcome.Erased,
            400 or 422 => AdminPortalGdprOutcome.ValidationRejected,
            408 or 429 => AdminPortalGdprOutcome.TransientFailure,
            >= 500 => AdminPortalGdprOutcome.TransientFailure,
            _ => AdminPortalGdprOutcome.Unknown,
        };

    private static bool ContainsTenant(string? value)
        => value?.Contains("tenant", StringComparison.OrdinalIgnoreCase) == true;

    private static IReadOnlyDictionary<string, string>? BuildListFilters(AdminPortalListRequest request)
    {
        var filters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["page"] = AdminPortalQueryBounds.BoundPage(request.Page).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["pageSize"] = AdminPortalQueryBounds.BoundPageSize(request.PageSize).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        if (request.Type is not null)
        {
            filters["type"] = request.Type.Value.ToString();
        }

        if (request.Active is not null)
        {
            filters["active"] = request.Active.Value ? "true" : "false";
        }

        AddDate(filters, "createdAfter", request.CreatedAfter);
        AddDate(filters, "createdBefore", request.CreatedBefore);
        AddDate(filters, "modifiedAfter", request.ModifiedAfter);
        AddDate(filters, "modifiedBefore", request.ModifiedBefore);

        return filters;
    }

    private static void AddDate(Dictionary<string, string> filters, string name, DateTimeOffset? value)
    {
        if (value is not null)
        {
            filters[name] = value.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static string RequireContract(string? value, string optionName)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        throw new AdminPortalQueryException(
            AdminPortalQueryFailureKind.ContractUnavailable,
            validationDetail: $"{optionName} is not configured because Story 12.4/12.5 has not frozen the Parties EventStore query contract.");
    }

    private static PagedResult<T> ToPage<T>(QueryResult<T> result, int page, int pageSize)
        => new()
        {
            Items = result.Items,
            Page = page,
            PageSize = pageSize,
            TotalCount = result.TotalCount,
            TotalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(result.TotalCount / (double)pageSize),
        };

    private static AdminPortalQueryMetadata MetadataFrom<T>(QueryResult<T> result)
        => result.IsNotModified
            ? new AdminPortalQueryMetadata(StaleDataAge: "not-modified")
            : AdminPortalQueryMetadata.Empty;
}
