using System.Security.Claims;

using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Client;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Contracts.Commands;
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
    private static readonly DateTimeOffset BaseDate = new(2026, 06, 10, 10, 00, 00, TimeSpan.Zero);

    private readonly object _sync = new();
    private readonly List<AdminPortalRequestCapture> _listRequests = [];
    private readonly List<AdminPortalRequestCapture> _searchRequests = [];
    private readonly List<AdminPortalRequestCapture> _detailRequests = [];
    private readonly List<AdminPortalRequestCapture> _createRequests = [];
    private readonly List<AdminPortalRequestCapture> _updateRequests = [];
    private readonly List<AdminPortalRequestCapture> _pickerSearchRequests = [];
    private readonly List<AdminPortalRequestCapture> _pickerDetailRequests = [];
    private readonly Dictionary<string, PartyDetail> _details = CreateInitialDetails();

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

    public void CapturePickerSearch(string query, int page, int pageSize)
    {
        lock (_sync)
        {
            _pickerSearchRequests.Add(AdminPortalRequestCapture.FromPickerSearch(query, page, pageSize));
        }
    }

    public void CaptureDetail(string partyId)
    {
        lock (_sync)
        {
            _detailRequests.Add(AdminPortalRequestCapture.FromDetail(partyId));
        }
    }

    public void CapturePickerDetail(string partyId)
    {
        lock (_sync)
        {
            _pickerDetailRequests.Add(AdminPortalRequestCapture.FromPickerDetail(partyId));
        }
    }

    public PartyDetail CaptureCreate(CreatePartyComposite command)
    {
        lock (_sync)
        {
            PartyDetail detail = DetailFromCreate(command);
            _details[detail.Id] = detail;
            _createRequests.Add(AdminPortalRequestCapture.FromCreate(command));
            return detail;
        }
    }

    public PartyDetail CaptureUpdate(string partyId, UpdatePartyComposite command)
    {
        lock (_sync)
        {
            PartyDetail current = _details.TryGetValue(partyId, out PartyDetail? existing)
                ? existing
                : new PartyDetail
                {
                    Id = partyId,
                    Type = command.OrganizationDetails is null ? PartyType.Person : PartyType.Organization,
                    IsActive = true,
                    DisplayName = partyId,
                    SortName = partyId,
                    ContactChannels = [],
                    Identifiers = [],
                    ConsentRecords = [],
                    NameHistory = [],
                };
            PartyDetail updated = DetailFromUpdate(current, command);
            _details[partyId] = updated;
            _updateRequests.Add(AdminPortalRequestCapture.FromUpdate(partyId, command));
            return updated;
        }
    }

    public PartyDetail? Detail(string partyId)
    {
        lock (_sync)
        {
            return _details.TryGetValue(partyId, out PartyDetail? detail) ? detail : null;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _listRequests.Clear();
            _searchRequests.Clear();
            _detailRequests.Clear();
            _createRequests.Clear();
            _updateRequests.Clear();
            _pickerSearchRequests.Clear();
            _pickerDetailRequests.Clear();
            _details.Clear();
            foreach ((string key, PartyDetail detail) in CreateInitialDetails())
            {
                _details[key] = detail;
            }
        }
    }

    public AdminPortalE2eSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new AdminPortalE2eSnapshot(
                [.. _listRequests],
                [.. _searchRequests],
                [.. _detailRequests],
                [.. _createRequests],
                [.. _updateRequests],
                [.. _pickerSearchRequests],
                [.. _pickerDetailRequests]);
        }
    }

    private static Dictionary<string, PartyDetail> CreateInitialDetails()
        => CreateEntries().ToDictionary(static entry => entry.Id, DetailFromEntry, StringComparer.Ordinal);

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
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            NameHistory = [],
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

    private static PartyDetail DetailFromCreate(CreatePartyComposite command)
        => new()
        {
            Id = command.PartyId,
            Type = command.Type,
            IsActive = true,
            DisplayName = DisplayName(command.Type, command.PersonDetails, command.OrganizationDetails),
            SortName = SortName(command.Type, command.PersonDetails, command.OrganizationDetails),
            PersonDetails = command.Type == PartyType.Person ? command.PersonDetails : null,
            OrganizationDetails = command.Type == PartyType.Organization ? command.OrganizationDetails : null,
            ContactChannels = [.. command.ContactChannels.Select(static channel => new ContactChannel
            {
                Id = channel.ContactChannelId,
                Type = channel.Type,
                Value = channel.Value,
                IsPreferred = channel.IsPreferred,
            })],
            Identifiers = [.. command.Identifiers.Select(static identifier => new PartyIdentifier
            {
                Id = identifier.IdentifierId,
                Type = identifier.Type,
                Value = identifier.Value,
            })],
            ConsentRecords = [],
            NameHistory = [],
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };

    private static PartyDetail DetailFromUpdate(PartyDetail current, UpdatePartyComposite command)
    {
        PartyType type = command.OrganizationDetails is not null ? PartyType.Organization : PartyType.Person;
        return current with
        {
            Type = type,
            DisplayName = DisplayName(type, command.PersonDetails, command.OrganizationDetails),
            SortName = SortName(type, command.PersonDetails, command.OrganizationDetails),
            PersonDetails = type == PartyType.Person ? command.PersonDetails : null,
            OrganizationDetails = type == PartyType.Organization ? command.OrganizationDetails : null,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static string DisplayName(PartyType type, PersonDetails? person, OrganizationDetails? organization)
        => type == PartyType.Person
            ? $"{person?.FirstName} {person?.LastName}".Trim()
            : organization?.LegalName ?? string.Empty;

    private static string SortName(PartyType type, PersonDetails? person, OrganizationDetails? organization)
        => type == PartyType.Person
            ? $"{person?.LastName}, {person?.FirstName}".Trim(' ', ',')
            : organization?.LegalName ?? string.Empty;
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
        return Task.FromResult(AdminPortalGdprCapability.ProvisionalBridge());
    }

    public Task<AdminPortalQueryResult<PartyDetail>> GetPartyAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        state.CaptureDetail(partyId);

        PartyDetail? detail = state.Detail(partyId);
        if (detail is null)
        {
            throw new AdminPortalQueryException(AdminPortalQueryFailureKind.NotFound);
        }

        return Task.FromResult(new AdminPortalQueryResult<PartyDetail>(
            detail,
            AdminPortalQueryMetadata.Empty));
    }

    public Task<AdminPortalCommandResult> CreatePartyCompositeAsync(
        CreatePartyComposite command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(command.PersonDetails?.LastName, "Reject", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new AdminPortalCommandResult(
                AdminPortalCommandOutcome.ValidationRejected,
                "corr-validation",
                ValidationFailures: [new AdminPortalCommandValidationFailure("PersonDetails.LastName", "Rejected")]));
        }

        PartyDetail detail = state.CaptureCreate(command);
        return Task.FromResult(new AdminPortalCommandResult(AdminPortalCommandOutcome.Accepted, "corr-create", detail));
    }

    public Task<AdminPortalCommandResult> UpdatePartyCompositeAsync(
        string partyId,
        UpdatePartyComposite command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PartyDetail detail = state.CaptureUpdate(partyId, command);
        return Task.FromResult(new AdminPortalCommandResult(AdminPortalCommandOutcome.Accepted, "corr-update", detail));
    }

    public Task<AdminPortalGdprCommandResult> RequestErasureAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-erasure"));
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
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-restriction"));
    }

    public Task<AdminPortalGdprCommandResult> LiftRestrictionAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-lift-restriction"));
    }

    public Task<AdminPortalGdprCommandResult> AddConsentAsync(
        string partyId,
        string channelId,
        string purpose,
        LawfulBasis lawfulBasis,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-add-consent"));
    }

    public Task<AdminPortalGdprCommandResult> RevokeConsentAsync(string partyId, string consentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-revoke-consent"));
    }

    public Task<IReadOnlyList<ConsentRecord>> GetConsentAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ConsentRecord>>([]);
    }

    public Task<AdminPortalExportDownload> ExportPartyDataAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AdminPortalExportDownload("party-export.json", "application/json", [(byte)'{', (byte)'}']));
    }

    public Task<IReadOnlyList<ProcessingActivityRecord>> GetProcessingRecordsAsync(string partyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProcessingActivityRecord>>(
        [
            new ProcessingActivityRecord
            {
                SequenceNumber = 1,
                EventType = "GdprOperationRecorded",
                Timestamp = BaseDate,
                Summary = "Bounded GDPR operation record",
            },
        ]);
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

internal sealed class PartiesAdminPortalE2ePartiesQueryClient(PartiesAdminPortalE2eFixtureState state) : IPartiesQueryClient
{
    private static readonly DateTimeOffset BaseDate = new(2026, 06, 10, 10, 00, 00, TimeSpan.Zero);
    private static readonly IReadOnlyList<PartyIndexEntry> Entries =
    [
        Entry("ada-lovelace", PartyType.Person, true, "Ada Lovelace", "Lovelace", 0),
        Entry("grace-hopper", PartyType.Person, true, "Grace Hopper", "Hopper", 1),
        Entry("local-only-partners", PartyType.Organization, true, "Local Only Partners", "Local Only Partners", 2),
        Entry("degraded-partners", PartyType.Organization, false, "Degraded Partners", "Degraded Partners", 3),
    ];

    public Task<PartyDetail> GetPartyAsync(
        string partyId,
        CancellationToken ct,
        Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null)
    {
        ct.ThrowIfCancellationRequested();
        state.CapturePickerDetail(partyId);

        PartyIndexEntry? entry = Entries.FirstOrDefault(item => string.Equals(item.Id, partyId, StringComparison.Ordinal));
        if (entry is null)
        {
            throw new PartiesClientException(404, "Not Found", null, "The selected party was not found.", null);
        }

        return Task.FromResult(DetailFromEntry(entry));
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
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Page(Entries, page, pageSize));
    }

    public Task<PagedResult<PartySearchResult>> SearchPartiesAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken ct,
        string? mode = null,
        string? caseId = null,
        Func<HttpRequestMessage, CancellationToken, ValueTask>? requestCustomizer = null,
        PartyType? type = null,
        bool? active = null)
    {
        ct.ThrowIfCancellationRequested();
        state.CapturePickerSearch(query, page, pageSize);

        IReadOnlyList<PartyIndexEntry> matches = QueryEntries(query, type, active);
        ProjectionFreshnessMetadata? freshness = query.Trim().ToLowerInvariant() switch
        {
            "local" => ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.LocalOnly),
            "degraded" => ProjectionFreshnessMetadata.Create(
                ProjectionFreshnessStatus.Degraded,
                ProjectionFreshnessMetadata.WarningProjectionRebuilding),
            _ => null,
        };

        return Task.FromResult(Page([.. matches.Select(static entry => new PartySearchResult
        {
            Party = entry,
            Matches = [],
            RelevanceScore = 1,
        })], page, pageSize) with { Freshness = freshness });
    }

    private static IReadOnlyList<PartyIndexEntry> QueryEntries(string query, PartyType? type, bool? active)
    {
        string normalized = query.Trim();
        IEnumerable<PartyIndexEntry> results = Entries;
        if (type is not null)
        {
            results = results.Where(entry => entry.Type == type);
        }

        if (active is not null)
        {
            results = results.Where(entry => entry.IsActive == active);
        }

        return normalized.ToLowerInvariant() switch
        {
            "local" => results.Where(static entry => entry.Id == "local-only-partners").ToArray(),
            "degraded" => results.Where(static entry => entry.Id == "degraded-partners").ToArray(),
            _ => results.Where(entry => entry.DisplayName.Contains(normalized, StringComparison.OrdinalIgnoreCase)).ToArray(),
        };
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
    IReadOnlyList<AdminPortalRequestCapture> DetailRequests,
    IReadOnlyList<AdminPortalRequestCapture> CreateRequests,
    IReadOnlyList<AdminPortalRequestCapture> UpdateRequests,
    IReadOnlyList<AdminPortalRequestCapture> PickerSearchRequests,
    IReadOnlyList<AdminPortalRequestCapture> PickerDetailRequests);

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

    public static AdminPortalRequestCapture FromPickerSearch(string query, int page, int pageSize)
        => new("picker-search", query, page, pageSize, null, null, null);

    public static AdminPortalRequestCapture FromDetail(string partyId)
        => new("detail", null, 0, 0, null, null, partyId);

    public static AdminPortalRequestCapture FromPickerDetail(string partyId)
        => new("picker-detail", null, 0, 0, null, null, partyId);

    public static AdminPortalRequestCapture FromCreate(CreatePartyComposite command)
        => new("create", null, 0, 0, command.Type.ToString(), null, command.PartyId);

    public static AdminPortalRequestCapture FromUpdate(string partyId, UpdatePartyComposite command)
        => new("update", null, 0, 0, command.OrganizationDetails is null ? "Person" : "Organization", null, partyId);
}
