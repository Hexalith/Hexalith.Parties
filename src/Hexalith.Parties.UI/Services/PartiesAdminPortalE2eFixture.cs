using System.Security.Claims;

using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Parties.UI.Services;

internal static class PartiesAdminPortalE2eFixture
{
    public const string EnabledConfigurationKey = "Hexalith:Parties:AdminPortalE2E:Enabled";
    public const string AdminCookieName = "parties-admin-e2e";
    public const string RequestsRoute = "/__parties/specimens/admin-portal/requests";
    public const string ResetRoute = "/__parties/specimens/admin-portal/reset";

    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        return string.Equals(configuration[EnabledConfigurationKey], "true", StringComparison.OrdinalIgnoreCase)
            && environment.IsEnvironment("Test");
    }

    public static bool IsAdminFixtureCookiePresent(IHttpContextAccessor httpContextAccessor)
        => string.Equals(
            httpContextAccessor.HttpContext?.Request.Cookies[AdminCookieName],
            "enabled",
            StringComparison.Ordinal);
}

internal sealed class PartiesAdminPortalE2eAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal AnonymousPrincipal = new(new ClaimsIdentity());
    private static readonly ClaimsPrincipal AdminPrincipal = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "admin-e2e"),
            new Claim(ClaimTypes.Name, "Admin E2E"),
            new Claim("sub", "admin-e2e"),
            new Claim("roles", "Admin"),
            new Claim("eventstore:tenant", "test-tenant"),
        ],
        authenticationType: "PartiesAdminPortalE2E",
        nameType: ClaimTypes.Name,
        roleType: "roles"));

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(
            PartiesAdminPortalE2eFixture.IsAdminFixtureCookiePresent(httpContextAccessor)
                ? AdminPrincipal
                : AnonymousPrincipal));
    }
}

internal sealed class PartiesAdminPortalE2eAuthorizationService(IHttpContextAccessor httpContextAccessor) : IAdminPortalAuthorizationService
{
    public Task<AdminPortalAuthorizationState> GetAuthorizationStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!PartiesAdminPortalE2eFixture.IsAdminFixtureCookiePresent(httpContextAccessor))
        {
            return Task.FromResult(AdminPortalAuthorizationState.Unauthenticated);
        }

        return Task.FromResult(new AdminPortalAuthorizationState(
            IsAuthenticated: true,
            HasTenantContext: true,
            IsAdmin: true,
            ContextSignature: "tenant:test-tenant:user:admin-e2e:admin"));
    }
}

internal sealed class PartiesAdminPortalE2eFixtureState
{
    private readonly object _sync = new();
    private readonly List<AdminPortalRequestCapture> _listRequests = [];
    private readonly List<AdminPortalRequestCapture> _searchRequests = [];
    private readonly List<AdminPortalRequestCapture> _detailRequests = [];

    public void CaptureList(AdminPortalListRequest request)
    {
        lock (_sync)
        {
            _listRequests.Add(AdminPortalRequestCapture.FromList(request));
        }
    }

    public void CaptureSearch(AdminPortalSearchRequest request)
    {
        lock (_sync)
        {
            _searchRequests.Add(AdminPortalRequestCapture.FromSearch(request));
        }
    }

    public void CaptureDetail(string partyId)
    {
        lock (_sync)
        {
            _detailRequests.Add(AdminPortalRequestCapture.FromDetail(partyId));
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _listRequests.Clear();
            _searchRequests.Clear();
            _detailRequests.Clear();
        }
    }

    public AdminPortalE2eSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new AdminPortalE2eSnapshot([.. _listRequests], [.. _searchRequests], [.. _detailRequests]);
        }
    }
}

internal sealed class PartiesAdminPortalE2eApiClient(PartiesAdminPortalE2eFixtureState state) : IPartiesAdminPortalApiClient
{
    private static readonly DateTimeOffset BaseDate = new(2026, 06, 10, 10, 00, 00, TimeSpan.Zero);
    private static readonly IReadOnlyList<PartyIndexEntry> Entries = CreateEntries();

    public Task<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>> ListPartiesAsync(
        AdminPortalListRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        state.CaptureList(request);

        IReadOnlyList<PartyIndexEntry> rows = ApplyTypeAndActiveFilters(Entries, request.Type, request.Active);
        return Task.FromResult(new AdminPortalQueryResult<PagedResult<PartyIndexEntry>>(
            Page(rows, request.Page, request.PageSize),
            AdminPortalQueryMetadata.Empty));
    }

    public Task<AdminPortalQueryResult<PagedResult<PartySearchResult>>> SearchPartiesAsync(
        AdminPortalSearchRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        state.CaptureSearch(request);

        if (string.Equals(request.Query.Trim(), "stale", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new AdminPortalQueryResult<PagedResult<PartySearchResult>>(
                Page(Array.Empty<PartySearchResult>(), request.Page, request.PageSize),
                new AdminPortalQueryMetadata(ServiceDegraded: true, StaleDataAge: "00:02:00")));
        }

        IReadOnlyList<PartyIndexEntry> filtered = ApplyTypeAndActiveFilters(Entries, request.Type, request.Active)
            .Where(entry => entry.DisplayName.Contains(request.Query.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        PartySearchResult[] results = [.. filtered.Select(static entry => new PartySearchResult
        {
            Party = entry,
            RelevanceScore = 1,
            Matches =
            [
                new MatchMetadata
                {
                    MatchedField = "displayName",
                    MatchType = "contains",
                    Score = 1,
                },
            ],
        })];

        return Task.FromResult(new AdminPortalQueryResult<PagedResult<PartySearchResult>>(
            Page(results, request.Page, request.PageSize),
            new AdminPortalQueryMetadata(SearchStatus: "LocalOnly")));
    }

    public Task<AdminPortalRichSearchCapability> GetRichSearchCapabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AdminPortalRichSearchCapability.LocalOnly());
    }

    public Task<AdminPortalGdprCapability> GetGdprCapabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AdminPortalGdprCapability.Unavailable());
    }

    public Task<AdminPortalQueryResult<PartyDetail>> GetPartyAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        state.CaptureDetail(partyId);

        PartyIndexEntry? entry = Entries.FirstOrDefault(row => string.Equals(row.Id, partyId, StringComparison.Ordinal));
        if (entry is null)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.NotFound);
        }

        return Task.FromResult(new AdminPortalQueryResult<PartyDetail>(
            DetailFromEntry(entry),
            AdminPortalQueryMetadata.Empty));
    }

    public Task<AdminPortalGdprCommandResult> RequestErasureAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.ContractUnavailable, null));
    }

    public Task<PartyErasureStatusRecord?> GetErasureStatusAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<PartyErasureStatusRecord?>(null);
    }

    public Task<ErasureCertificate?> GetErasureCertificateAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ErasureCertificate?>(null);
    }

    public Task<AdminPortalGdprCommandResult> RetryErasureVerificationAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.ContractUnavailable, null));
    }

    public Task<AdminPortalGdprCommandResult> RestrictProcessingAsync(string partyId, string? reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.ContractUnavailable, null));
    }

    public Task<AdminPortalGdprCommandResult> LiftRestrictionAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.ContractUnavailable, null));
    }

    public Task<AdminPortalGdprCommandResult> AddConsentAsync(
        string partyId,
        string channelId,
        string purpose,
        LawfulBasis lawfulBasis,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.ContractUnavailable, null));
    }

    public Task<AdminPortalGdprCommandResult> RevokeConsentAsync(string partyId, string consentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.ContractUnavailable, null));
    }

    public Task<IReadOnlyList<ConsentRecord>> GetConsentAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ConsentRecord>>([]);
    }

    public Task<AdminPortalExportDownload> ExportPartyDataAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AdminPortalExportDownload("party-export.json", "application/json", []));
    }

    public Task<IReadOnlyList<ProcessingActivityRecord>> GetProcessingRecordsAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProcessingActivityRecord>>([]);
    }

    private static IReadOnlyList<PartyIndexEntry> ApplyTypeAndActiveFilters(
        IReadOnlyList<PartyIndexEntry> rows,
        PartyType? type,
        bool? active)
    {
        IEnumerable<PartyIndexEntry> query = rows;
        if (type is not null)
        {
            query = query.Where(row => row.Type == type);
        }

        if (active is not null)
        {
            query = query.Where(row => row.IsActive == active);
        }

        return query.ToArray();
    }

    private static PagedResult<T> Page<T>(IReadOnlyList<T> rows, int page, int pageSize)
    {
        int boundedPage = Math.Max(page, 1);
        int boundedPageSize = Math.Clamp(pageSize, 1, 100);
        int totalPages = rows.Count == 0 ? 0 : (int)Math.Ceiling(rows.Count / (double)boundedPageSize);
        return new PagedResult<T>
        {
            Items = [.. rows.Skip((boundedPage - 1) * boundedPageSize).Take(boundedPageSize)],
            Page = rows.Count == 0 ? 1 : Math.Min(boundedPage, totalPages),
            PageSize = boundedPageSize,
            TotalCount = rows.Count,
            TotalPages = totalPages,
        };
    }

    private static PartyDetail DetailFromEntry(PartyIndexEntry entry)
        => new()
        {
            Id = entry.Id,
            Type = entry.Type,
            IsActive = entry.IsActive,
            DisplayName = entry.DisplayName,
            SortName = entry.SortName,
            PersonDetails = entry.Type == PartyType.Person
                ? new PersonDetails { FirstName = entry.DisplayName.Split(' ')[0], LastName = entry.SortName }
                : null,
            OrganizationDetails = entry.Type == PartyType.Organization
                ? new OrganizationDetails { LegalName = entry.DisplayName, TradingName = entry.DisplayName, LegalForm = "SAS" }
                : null,
            CreatedAt = entry.CreatedAt,
            LastModifiedAt = entry.LastModifiedAt,
        };

    private static IReadOnlyList<PartyIndexEntry> CreateEntries()
    {
        List<PartyIndexEntry> entries =
        [
            Entry("ada-lovelace", PartyType.Person, true, "Ada Lovelace", "Lovelace", 0),
            Entry("grace-hopper", PartyType.Person, true, "Grace Hopper", "Hopper", 1),
            Entry("marie-curie", PartyType.Person, false, "Marie Curie", "Curie", 2),
            Entry("inactive-labs", PartyType.Organization, false, "Inactive Labs", "Inactive Labs", 3),
        ];

        for (int i = 1; i <= 25; i++)
        {
            entries.Add(Entry(
                $"paging-party-{i:00}",
                PartyType.Organization,
                true,
                $"Paging Party {i:00}",
                $"Paging Party {i:00}",
                10 + i));
        }

        return entries;
    }

    private static PartyIndexEntry Entry(
        string id,
        PartyType type,
        bool active,
        string displayName,
        string sortName,
        int dayOffset)
        => new()
        {
            Id = id,
            Type = type,
            IsActive = active,
            DisplayName = displayName,
            SortName = sortName,
            CreatedAt = BaseDate.AddDays(-dayOffset),
            LastModifiedAt = BaseDate.AddDays(-dayOffset).AddHours(1),
        };
}

internal sealed record AdminPortalE2eSnapshot(
    IReadOnlyList<AdminPortalRequestCapture> ListRequests,
    IReadOnlyList<AdminPortalRequestCapture> SearchRequests,
    IReadOnlyList<AdminPortalRequestCapture> DetailRequests);

internal sealed record AdminPortalRequestCapture(
    string Kind,
    string? Query,
    int Page,
    int PageSize,
    string? Type,
    bool? Active,
    string? PartyId)
{
    public static AdminPortalRequestCapture FromList(AdminPortalListRequest request)
        => new("list", null, request.Page, request.PageSize, request.Type?.ToString(), request.Active, null);

    public static AdminPortalRequestCapture FromSearch(AdminPortalSearchRequest request)
        => new("search", request.Query, request.Page, request.PageSize, request.Type?.ToString(), request.Active, null);

    public static AdminPortalRequestCapture FromDetail(string partyId)
        => new("detail", null, 0, 0, null, null, partyId);
}
