using System.Text.Json;

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
    private const string RichSearchProbeHttpClientName = "parties-admin-portal-richsearch";
    private const string ContractUnavailableUserMessage = "The Parties query contract is not configured for this admin portal.";
    private const string RichSearchUnavailableUserMessage = "Rich search is not currently configured for this admin portal.";

    private readonly IPartiesQueryClient? _partiesQueryClient;
    private readonly IAdminPortalGdprClient? _gdprClient;
    private readonly IQueryService? _queryService;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly PartiesAdminPortalOptions _options;

    [ActivatorUtilitiesConstructor]
    public PartiesAdminPortalApiClient(IServiceProvider serviceProvider, IOptions<PartiesAdminPortalOptions> options)
        : this(
            (serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider))).GetService<IPartiesQueryClient>(),
            serviceProvider.GetService<IAdminPortalGdprClient>(),
            serviceProvider.GetService<IQueryService>(),
            serviceProvider.GetService<IHttpClientFactory>(),
            options)
    {
    }

    public PartiesAdminPortalApiClient(IQueryService queryService, IOptions<PartiesAdminPortalOptions> options)
        : this(null, null, queryService, null, options)
    {
    }

    private PartiesAdminPortalApiClient(
        IPartiesQueryClient? partiesQueryClient,
        IAdminPortalGdprClient? gdprClient,
        IQueryService? queryService,
        IHttpClientFactory? httpClientFactory,
        IOptions<PartiesAdminPortalOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _partiesQueryClient = partiesQueryClient;
        _gdprClient = gdprClient;
        _queryService = queryService;
        _httpClientFactory = httpClientFactory;
        _options = options.Value
            ?? throw new InvalidOperationException("PartiesAdminPortalOptions.Value resolved to null.");
    }

    public async Task<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>> ListPartiesAsync(
        AdminPortalListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        int page = AdminPortalQueryBounds.BoundPage(request.Page);
        int pageSize = AdminPortalQueryBounds.BoundPageSize(request.PageSize);
        int skip = ComputeBoundedSkip(page, pageSize);
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
            return new(typedResult ?? new PagedResult<PartyIndexEntry> { Items = [] }, AdminPortalQueryMetadata.Empty);
        }

        QueryRequest query = new(
            ProjectionType: RequireContract(_options.ListProjectionType, nameof(_options.ListProjectionType)),
            TenantId: null,
            Skip: skip,
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
        int skip = ComputeBoundedSkip(page, pageSize);
        if (_partiesQueryClient is not null)
        {
            PagedResult<PartySearchResult> typedResult = await ExecutePartiesQueryAsync(
                (client, token) => client.SearchPartiesAsync(request.Query ?? string.Empty, page, pageSize, token),
                cancellationToken).ConfigureAwait(false);
            return new(typedResult ?? new PagedResult<PartySearchResult> { Items = [] }, AdminPortalQueryMetadata.Empty);
        }

        QueryRequest query = new(
            ProjectionType: RequireContract(_options.SearchProjectionType, nameof(_options.SearchProjectionType)),
            TenantId: null,
            Skip: skip,
            Take: pageSize,
            SearchQuery: request.Query ?? string.Empty,
            Domain: Domain,
            QueryType: RequireContract(_options.SearchQueryType, nameof(_options.SearchQueryType)),
            CacheDiscriminator: SearchCacheDiscriminator);

        QueryResult<PartySearchResult> result = await ExecuteAsync<PartySearchResult>(query, cancellationToken).ConfigureAwait(false);
        return new(ToPage(result, page, pageSize), MetadataFrom(result));
    }

    public async Task<AdminPortalRichSearchCapability> GetRichSearchCapabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_httpClientFactory is null || _options.RichSearchProbeBaseAddress is null)
        {
            return AdminPortalRichSearchCapability.LocalOnly(RichSearchUnavailableUserMessage);
        }

        try
        {
            HttpClient httpClient = _httpClientFactory.CreateClient(RichSearchProbeHttpClientName);
            httpClient.BaseAddress ??= _options.RichSearchProbeBaseAddress;

            using HttpResponseMessage response = await httpClient
                .GetAsync("health", cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return AdminPortalRichSearchCapability.Degraded(
                    $"Rich search probe returned HTTP {(int)response.StatusCode}.");
            }

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument
                .ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return ParseRichSearchCapability(document.RootElement);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException
            or IOException
            or JsonException
            or TaskCanceledException
            or TimeoutException)
        {
            return AdminPortalRichSearchCapability.Degraded(
                $"Rich search probe unavailable: {ex.GetType().Name}");
        }
    }

    public Task<AdminPortalGdprCapability> GetGdprCapabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // No GDPR client wired → Story 7.6 fail-closed gate: everything stays disabled.
        // When the provisional bridge IS wired, report its honest surface rather than
        // Available(): the registered HttpAdminPortalGdprClient reports erasure-certificate
        // and verification-retry as contract-unavailable, so those two stay disabled with the
        // exact bounded blocker while the seven genuinely-working operations stay enabled.
        return Task.FromResult(_gdprClient is null
            ? AdminPortalGdprCapability.Unavailable()
            : AdminPortalGdprCapability.ProvisionalBridge());
    }

    public async Task<AdminPortalQueryResult<PartyDetail>> GetPartyAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        if (_partiesQueryClient is not null)
        {
            PartyDetail typedDetail = await ExecutePartiesQueryAsync(
                (client, token) => client.GetPartyAsync(partyId, token),
                cancellationToken).ConfigureAwait(false);
            if (typedDetail is null)
            {
                throw new AdminPortalQueryException(AdminPortalQueryFailureKind.NotFound);
            }

            return new(NormalizeDetail(typedDetail), AdminPortalQueryMetadata.Empty);
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
        if (result.Items.Count > 1)
        {
            throw new AdminPortalQueryException(
                AdminPortalQueryFailureKind.Unknown,
                validationDetail: "Detail projection returned more than one item for an aggregate-by-id query.");
        }

        PartyDetail? detail = result.Items.FirstOrDefault();
        if (detail is null)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.NotFound);
        }

        return new(NormalizeDetail(detail), MetadataFrom(result));
    }

    public Task<AdminPortalGdprCommandResult> RequestErasureAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return ExecuteGdprCommandAsync(client => client.RequestErasureAsync(partyId, cancellationToken), cancellationToken);
    }

    public Task<PartyErasureStatusRecord?> GetErasureStatusAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return ExecuteGdprQueryAsync(client => client.GetErasureStatusAsync(partyId, cancellationToken), cancellationToken);
    }

    public Task<ErasureCertificate?> GetErasureCertificateAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return ExecuteGdprQueryAsync(client => client.GetErasureCertificateAsync(partyId, cancellationToken), cancellationToken);
    }

    public Task<AdminPortalGdprCommandResult> RetryErasureVerificationAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return ExecuteGdprCommandAsync(client => client.RetryErasureVerificationAsync(partyId, cancellationToken), cancellationToken);
    }

    public Task<AdminPortalGdprCommandResult> RestrictProcessingAsync(
        string partyId,
        string? reason,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return ExecuteGdprCommandAsync(client => client.RestrictProcessingAsync(partyId, reason, cancellationToken), cancellationToken);
    }

    public Task<AdminPortalGdprCommandResult> LiftRestrictionAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return ExecuteGdprCommandAsync(client => client.LiftRestrictionAsync(partyId, cancellationToken), cancellationToken);
    }

    public Task<AdminPortalGdprCommandResult> AddConsentAsync(
        string partyId,
        string channelId,
        string purpose,
        LawfulBasis lawfulBasis,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        return ExecuteGdprCommandAsync(client => client.AddConsentAsync(partyId, channelId, purpose, lawfulBasis, cancellationToken), cancellationToken);
    }

    public Task<AdminPortalGdprCommandResult> RevokeConsentAsync(
        string partyId,
        string consentId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(consentId);
        return ExecuteGdprCommandAsync(client => client.RevokeConsentAsync(partyId, consentId, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<ConsentRecord>> GetConsentAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return ExecuteGdprQueryAsync(client => client.GetConsentAsync(partyId, cancellationToken), cancellationToken);
    }

    public Task<AdminPortalExportDownload> ExportPartyDataAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return ExecuteGdprQueryAsync(client => client.ExportPartyDataAsync(partyId, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<ProcessingActivityRecord>> GetProcessingRecordsAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return ExecuteGdprQueryAsync(client => client.GetProcessingRecordsAsync(partyId, cancellationToken), cancellationToken);
    }

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
        catch (HttpRequestException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (TimeoutException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (JsonException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (NotSupportedException ex)
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
        catch (HttpRequestException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (TimeoutException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (JsonException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (NotSupportedException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
    }

    private async Task<AdminPortalGdprCommandResult> ExecuteGdprCommandAsync(
        Func<IAdminPortalGdprClient, Task<AdminPortalGdprCommandResult>> operation,
        CancellationToken cancellationToken)
    {
        IAdminPortalGdprClient client = _gdprClient
            ?? throw new AdminPortalQueryException(AdminPortalQueryFailureKind.ContractUnavailable);

        try
        {
            return await operation(client).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PartiesClientException ex)
        {
            return new AdminPortalGdprCommandResult(MapGdprOutcome(ex), ex.CorrelationId, Detail: null);
        }
        catch (Exception ex) when (ex is HttpRequestException
            or TimeoutException
            or JsonException
            or NotSupportedException
            or TaskCanceledException
            or OperationCanceledException)
        {
            return new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.TransientFailure, CorrelationId: null, Detail: null);
        }
    }

    private async Task<T> ExecuteGdprQueryAsync<T>(
        Func<IAdminPortalGdprClient, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        IAdminPortalGdprClient client = _gdprClient
            ?? throw new AdminPortalQueryException(AdminPortalQueryFailureKind.ContractUnavailable);

        try
        {
            return await operation(client).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PartiesClientException ex)
        {
            throw new AdminPortalQueryException(MapFailureKind(ex), ex.Status, innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (TimeoutException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (JsonException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (NotSupportedException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.TransientFailure, innerException: ex);
        }
    }

    private static int ComputeBoundedSkip(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new AdminPortalQueryException(
                AdminPortalQueryFailureKind.Validation,
                validationDetail: "Page number must be >= 1.");
        }

        long skip = ((long)page - 1) * pageSize;
        if (skip > int.MaxValue)
        {
            throw new AdminPortalQueryException(
                AdminPortalQueryFailureKind.Validation,
                validationDetail: "Requested page exceeds the addressable range.");
        }

        return (int)skip;
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
            _ => MapByStatus(ex.Problem.Status),
        };

    private static AdminPortalQueryFailureKind MapByStatus(int? status)
        => status switch
        {
            409 => AdminPortalQueryFailureKind.Conflict,
            410 => AdminPortalQueryFailureKind.Gone,
            400 or 422 => AdminPortalQueryFailureKind.Validation,
            408 or 429 => AdminPortalQueryFailureKind.TransientFailure,
            >= 500 => AdminPortalQueryFailureKind.TransientFailure,
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
            409 => AdminPortalQueryFailureKind.Conflict,
            410 => AdminPortalQueryFailureKind.Gone,
            501 => AdminPortalQueryFailureKind.ContractUnavailable,
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
            501 => AdminPortalGdprOutcome.ContractUnavailable,
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

        // Internal dependency note: this surface is the IQueryService fallback path.
        // The typed IPartiesQueryClient path bypasses RequireContract entirely; once that
        // typed boundary is the only path, this fallback (and its options) can be retired.
        throw new AdminPortalQueryException(
            AdminPortalQueryFailureKind.ContractUnavailable,
            validationDetail: $"{optionName} is not configured. {ContractUnavailableUserMessage}");
    }

    private static PagedResult<T> ToPage<T>(QueryResult<T> result, int page, int pageSize)
        => new()
        {
            Items = result.Items,
            Page = page,
            PageSize = pageSize,
            TotalCount = result.TotalCount,
            TotalPages = pageSize <= 0 ? 0 : Math.Max(0, (int)Math.Ceiling(result.TotalCount / (double)pageSize)),
        };

    private static AdminPortalQueryMetadata MetadataFrom<T>(QueryResult<T> result)
        => result.IsNotModified
            ? new AdminPortalQueryMetadata(StaleDataAge: "not-modified")
            : AdminPortalQueryMetadata.Empty;

    private static PartyDetail NormalizeDetail(PartyDetail detail)
        => detail with
        {
            // System.Text.Json overwrites the contract's `[]` defaults with `null` if the
            // backend omits/nulls the property. Normalize at the API client boundary so
            // every consumer sees non-null collections regardless of transport.
            ContactChannels = detail.ContactChannels ?? [],
            Identifiers = detail.Identifiers ?? [],
            ConsentRecords = detail.ConsentRecords ?? [],
            NameHistory = detail.NameHistory ?? [],
        };

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

        // Fail closed: a missing or non-boolean searchReachable value degrades safely to
        // local-only rather than implicitly trusting the backend reports rich search.
        bool searchReachable = ReadBoolProperty(data, "searchReachable") == true;
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
}
