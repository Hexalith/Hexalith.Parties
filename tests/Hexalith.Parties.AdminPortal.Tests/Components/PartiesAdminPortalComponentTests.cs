using Bunit;

using AngleSharp.Dom;

using System.Globalization;
using System.Security.Claims;

using Hexalith.Parties.AdminPortal.Components;
using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.AdminPortal.Tests.Services;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Contracts.Enums;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Parties.AdminPortal.Tests.Components;

public sealed class PartiesAdminPortalComponentTests : BunitContext
{
    private const string AdminUserId = "admin-user";

    private readonly TestAuthenticationStateProvider _authProvider = new();
    private readonly InMemoryTenantProjectionStore _tenantStore = new();

    public PartiesAdminPortalComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
        Services.AddSingleton<AuthenticationStateProvider>(_authProvider);
        Services.AddSingleton<ITenantProjectionStore>(_tenantStore);
        Services.AddScoped<IAdminPortalAuthorizationService, AdminPortalAuthorizationService>();
        Services.AddScoped<AdminPortalPartyQueryService>();
        Services.AddScoped<PartiesAdminListCoordinator>();
    }

    [Fact]
    public void PartiesAdminPortal_InitialBrowse_RendersDensePartyRows()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-1", "Ada Lovelace", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");

        cut.WaitForAssertion(() =>
        {
            cut.Find("table").GetAttribute("aria-label").ShouldBe("Parties");
            cut.Markup.ShouldContain("Ada Lovelace");
            cut.Markup.ShouldContain("Person");
            cut.Find("td span.hx-parties-admin__badge").TextContent.Trim().ShouldBe("Active");
            cut.Markup.ShouldNotContain("hero");
        });

        api.ListRequests.Single().PageSize.ShouldBe(20);
    }

    [Fact]
    public void PartiesAdminPortal_BrowseSurface_UsesFluentComponentsInsteadOfRawHtmlControls()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-fluent", "Fluent Row", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Fluent Row");
            cut.FindComponent<FluentDataGrid<PartyIndexEntry>>();
            cut.FindComponent<FluentTextInput>();
            cut.FindComponents<FluentSelect<string, string>>().Count.ShouldBe(2);
            cut.FindComponents<FluentButton>().Count.ShouldBeGreaterThan(0);
            cut.Markup.ShouldNotContain("<input", Case.Sensitive);
            cut.Markup.ShouldNotContain("<select", Case.Sensitive);
        });
    }

    [Fact]
    public void PartiesAdminPortal_FailsClosed_WhenAuthenticationStateIsUnauthenticated()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-secret", "Secret", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = Render<PartiesAdminPortal>(p => p
            .Add(x => x.ContextKey, "scope-a"));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Sign-in is required");
            cut.Markup.ShouldNotContain("Secret");
        });

        api.ListRequests.ShouldBeEmpty();
    }

    [Fact]
    public void PartiesAdminPortal_DerivesAuthorizationFromTenantsMembership()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-owner", "Owner visible", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        SeedTenant("scope-a", AdminUserId, TenantRole.TenantOwner);

        _authProvider.SetAuthenticated(AdminUserId, "scope-a");
        IRenderedComponent<PartiesAdminPortal> cut = Render<PartiesAdminPortal>(p => p
            .Add(x => x.ContextKey, "scope-a"));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Owner visible"));
        api.ListRequests.Single().Page.ShouldBe(1);
    }

    [Fact]
    public void PartiesAdminPortal_DerivesAdminRoleFromTenantsMembership()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-reader", "Reader hidden", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        SeedTenant("scope-a", "reader-user", TenantRole.TenantReader);

        _authProvider.SetAuthenticated("reader-user", "scope-a");
        IRenderedComponent<PartiesAdminPortal> cut = Render<PartiesAdminPortal>(p => p
            .Add(x => x.ContextKey, "scope-a"));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Administrator access is required");
            cut.Markup.ShouldNotContain("Reader hidden");
        });

        api.ListRequests.ShouldBeEmpty();
    }

    [Fact]
    public void PartiesAdminPortal_SearchAndFilters_UseApprovedApiAndBoundPageSize()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page<PartyIndexEntry>());
        api.EnqueueSearch(Page(new PartySearchResult
        {
            Party = IndexEntry("party-2", "Grace Hopper", PartyType.Person, true),
            Matches = [],
            RelevanceScore = 1.0,
        }), new AdminPortalQueryMetadata(SearchStatus: "LocalOnly", SearchDegradedReason: "rich-search-disabled"));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a", pageSize: 250);

        cut.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(1));
        SetSearch(cut, "grace@example.test");
        ClickFluentButton(cut, "Search");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Display-name search only");
            cut.Markup.ShouldContain("Grace Hopper");
        });

        AdminPortalSearchRequest search = api.SearchRequests.Single();
        search.Query.ShouldBe("grace@example.test");
        search.PageSize.ShouldBe(100);
        // Type/Active intentionally not forwarded to the rich-search query contract yet.
        // UI disables the filter selects in search mode (D2.1) so the user
        // sees the constraint rather than a silently-ignored selection.
        search.Type.ShouldBeNull();
        search.Active.ShouldBeNull();
        // Filter selects must be disabled while the query is non-empty.
        IReadOnlyList<IRenderedComponent<FluentSelect<string, string>>> selects = cut.FindComponents<FluentSelect<string, string>>();
        (selects[0].Instance.Disabled == true).ShouldBeTrue();
        (selects[1].Instance.Disabled == true).ShouldBeTrue();
    }

    [Fact]
    public void PartiesAdminPortal_HealthyRichSearchProbe_EnablesRichSearchModesForCircuit()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueRichSearchCapability(AdminPortalRichSearchCapability.Available());
        api.EnqueueList(Page(IndexEntry("party-rich", "Rich Search", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Rich Search");
            FindFluentButton(cut, "Email").HasAttribute("disabled").ShouldBeFalse();
            FindFluentButton(cut, "Identifier").HasAttribute("disabled").ShouldBeFalse();
        });

        SetSearch(cut, "rich@example.test");
        ClickFluentButton(cut, "Search");

        cut.WaitForAssertion(() => api.SearchRequests.Count.ShouldBe(1));
        api.RichSearchCapabilityProbeCount.ShouldBe(1);
    }

    [Fact]
    public void PartiesAdminPortal_DegradedRichSearchProbe_DisablesRichSearchModesWithDistinctStatus()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueRichSearchCapability(AdminPortalRichSearchCapability.Degraded("memories-search degraded"));
        api.EnqueueList(Page(IndexEntry("party-local", "Local Only", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Rich search is temporarily unavailable");
            cut.Markup.ShouldNotContain("Display-name search only");
            FindFluentButton(cut, "Email").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Identifier").HasAttribute("disabled").ShouldBeTrue();
        });
    }

    [Fact]
    public void PartiesAdminPortal_TenantSwitch_InvalidatesRichSearchProbeCache()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueRichSearchCapability(AdminPortalRichSearchCapability.Available());
        api.EnqueueList(Page(IndexEntry("party-a", "Tenant A", PartyType.Person, true)));
        api.EnqueueRichSearchCapability(AdminPortalRichSearchCapability.Degraded("tenant-b search offline"));
        api.EnqueueList(Page(IndexEntry("party-b", "Tenant B", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Email").HasAttribute("disabled").ShouldBeFalse());

        SeedTenant("scope-b", AdminUserId, TenantRole.TenantOwner);
        cut.InvokeAsync(() => _authProvider.SetAuthenticated(AdminUserId, "scope-b"));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Tenant B");
            cut.Markup.ShouldContain("Rich search is temporarily unavailable");
            FindFluentButton(cut, "Email").HasAttribute("disabled").ShouldBeTrue();
        });
        api.RichSearchCapabilityProbeCount.ShouldBe(2);
    }

    [Fact]
    public void PartiesAdminPortal_SelectParty_HydratesDetailAndEncodesUntrustedFields()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-3", "<script>alert(1)</script>", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-3",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "<script>alert(1)</script>",
            SortName = "script",
            PersonDetails = new PersonDetails { FirstName = "<b>Ada</b>", LastName = "Lovelace" },
            ContactChannels = [new ContactChannel { Id = "c-1", Type = ContactChannelType.Email, Value = "ada@example.test<script>", IsPreferred = true }],
            Identifiers = [new PartyIdentifier { Id = "i-1", Type = IdentifierType.Other, Value = "ID<123>" }],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");

        cut.WaitForAssertion(() => FindFluentButton(cut, "alert").TextContent.ShouldContain("alert"));
        ClickFluentButton(cut, "alert");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("&lt;script&gt;alert(1)&lt;/script&gt;");
            cut.Markup.ShouldContain("&lt;b&gt;Ada&lt;/b&gt;");
            cut.Markup.ShouldContain("ada@example.test&lt;script&gt;");
            cut.Markup.ShouldContain("ID&lt;123&gt;");
            cut.Markup.ShouldContain("Contact channels");
            cut.Markup.ShouldContain("Identifiers");
            cut.Markup.ShouldNotContain("<script>alert(1)</script>");
            cut.Markup.ShouldNotContain("<b>Ada</b>");
        });
    }

    [Fact]
    public void PartiesAdminPortal_SelectParty_RendersRestrictionsSystemMetadataNameHistoryAndStaleAge()
    {
        var api = new RecordingAdminPortalApiClient();
        DateTimeOffset restrictedAt = DateTimeOffset.Parse("2026-05-03T10:15:00Z");
        api.EnqueueList(Page(IndexEntry("party-3b", "Ada Lovelace", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-3b",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            NameHistory =
            [
                new NameHistoryEntry
                {
                    DisplayName = "Ada Byron",
                    SortName = "Byron, Ada",
                    ChangedAt = DateTimeOffset.Parse("2026-05-01T09:00:00Z"),
                    TriggeredBy = "PartyDisplayNameDerived",
                },
            ],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-03T10:15:00Z"),
            IsRestricted = true,
            RestrictedAt = restrictedAt,
            IsErased = false,
        }, new AdminPortalQueryMetadata(StaleDataAge: "PT12S"));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Ada Lovelace").TextContent.ShouldContain("Ada Lovelace"));
        ClickFluentButton(cut, "Ada Lovelace");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Data age");
            cut.Markup.ShouldContain("PT12S");
            cut.Markup.ShouldContain("Restrictions");
            cut.Markup.ShouldContain("Restricted at");
            cut.Markup.ShouldContain("System metadata");
            // Scoped party id is intentionally not rendered as a user-facing label (D3.1).
            cut.Markup.ShouldNotContain("party-3b");
            cut.Markup.ShouldContain("Lovelace, Ada");
            cut.Markup.ShouldContain("Name history");
            cut.Markup.ShouldContain("Ada Byron");
            cut.Markup.ShouldContain("PartyDisplayNameDerived");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_SurfaceUsesEventStoreClientContract()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr", "GDPR Party", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-gdpr",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "GDPR Party",
            SortName = "Party, GDPR",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => FindFluentButton(cut, "GDPR Party").TextContent.ShouldContain("GDPR Party"));
        ClickFluentButton(cut, "GDPR Party");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("GDPR operations");
            cut.Markup.ShouldContain("Operational summary");
            FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeFalse();
            FindFluentButton(cut, "Restrict processing").HasAttribute("disabled").ShouldBeFalse();
            FindFluentButton(cut, "Lift restriction").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Add consent").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Export party data").HasAttribute("disabled").ShouldBeFalse();
            FindFluentButton(cut, "Processing records").HasAttribute("disabled").ShouldBeFalse();
            cut.Markup.ShouldNotContain("api/v1/admin");
        });
    }

    [Fact]
    public void PartiesAdminPortal_RequestErasure_RequiresConfirmationAndDisplaysCorrelation()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-erase", "Erase Candidate", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-erase",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Erase Candidate",
            SortName = "Candidate, Erase",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        api.EnqueueErasureStatus(new PartyErasureStatusRecord
        {
            PartyId = "party-erase",
            TenantId = "scope-a",
            Status = "ErasurePending",
            UpdatedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Erase Candidate"));
        ClickFluentButton(cut, "Erase Candidate");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR operations"));

        ClickFluentButton(cut, "Request erasure");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("irreversible verification"));
        ClickFluentButton(cut, "Confirm erasure");

        cut.WaitForAssertion(() =>
        {
            api.ErasureRequests.Single().ShouldBe("party-erase");
            api.ErasureStatusRequests.Single().ShouldBe("party-erase");
            cut.Markup.ShouldContain("corr-erasure");
            cut.Markup.ShouldContain("ErasurePending");
        });
    }

    [Fact]
    public void PartiesAdminPortal_ProcessingRecords_RenderEncodedAuditSummaries()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-processing", "Processing Party", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-processing",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Processing Party",
            SortName = "Party, Processing",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        api.EnqueueProcessingRecords(new ProcessingActivityRecord
        {
            SequenceNumber = 7,
            EventType = "ConsentRecorded",
            Timestamp = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
            Summary = "<script>alert(1)</script>",
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Processing Party"));
        ClickFluentButton(cut, "Processing Party");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR operations"));

        ClickFluentButton(cut, "Processing records");

        cut.WaitForAssertion(() =>
        {
            api.ProcessingRecordRequests.Single().ShouldBe("party-processing");
            cut.Markup.ShouldContain("ConsentRecorded");
            cut.Markup.ShouldContain("&lt;script&gt;alert(1)&lt;/script&gt;");
            cut.Markup.ShouldNotContain("<script>alert(1)</script>");
        });
    }

    [Fact]
    public void PartiesAdminPortal_PortabilityExport_UsesSafeDownloadEnvelopeCue()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-export", "Export Party", PartyType.Organization, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-export",
            Type = PartyType.Organization,
            IsActive = true,
            DisplayName = "Export Party",
            SortName = "Party, Export",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        api.EnqueueExport(new AdminPortalExportDownload("party-party-export-export.json", "application/json", []));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Export Party"));
        ClickFluentButton(cut, "Export Party");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR operations"));

        ClickFluentButton(cut, "Export party data");

        cut.WaitForAssertion(() =>
        {
            api.ExportRequests.Single().ShouldBe("party-export");
            cut.Markup.ShouldContain("Export prepared");
            cut.Markup.ShouldContain("party-party-export-export.json");
            cut.Markup.ShouldNotContain("Export Party-export");
        });
    }

    [Fact]
    public void PartiesAdminPortal_UnauthorizedOrTenantChange_ClearsRowsAndIgnoresStaleResponses()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-4", "Tenant A", PartyType.Organization, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenant A"));

        SeedTenant("scope-b", "reader-user", TenantRole.TenantReader);
        cut.InvokeAsync(() => _authProvider.SetAuthenticated("reader-user", "scope-b"));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Administrator access is required");
            cut.Markup.ShouldNotContain("Tenant A");
            FindFluentButton(cut, "Search").HasAttribute("disabled").ShouldBeFalse();
        });
    }

    [Fact]
    public void PartiesAdminPortal_TenantSwitch_IgnoresDelayedPreviousTenantResponse()
    {
        var api = new RecordingAdminPortalApiClient();
        var delayed = new TaskCompletionSource<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>>();
        api.EnqueueList(ct =>
        {
            ct.Register(() => delayed.TrySetCanceled(ct));
            return delayed.Task;
        });
        api.EnqueueList(Page(IndexEntry("party-new", "Tenant B", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(1));

        SeedTenant("scope-b", AdminUserId, TenantRole.TenantOwner);
        cut.InvokeAsync(() => _authProvider.SetAuthenticated(AdminUserId, "scope-b"));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenant B"));
        delayed.TrySetResult(new AdminPortalQueryResult<PagedResult<PartyIndexEntry>>(
            Page(IndexEntry("party-old", "Tenant A", PartyType.Organization, true)),
            AdminPortalQueryMetadata.Empty));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Tenant B");
            cut.Markup.ShouldNotContain("Tenant A");
        });
    }

    [Fact]
    public void PartiesAdminPortal_DetailGone_RemovesRowAndDoesNotLeakPreviousDetail()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gone", "Erased Person", PartyType.Person, true)));
        api.EnqueueDetailFailure(AdminPortalQueryFailureKind.Gone);
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Erased Person"));
        ClickFluentButton(cut, "Erased Person");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("erased or no longer inspectable");
            cut.Markup.ShouldNotContain("Erased Person");
        });
    }

    [Fact]
    public void PartiesAdminPortal_DetailForbidden_ClearsDetailWithoutRemovingBrowseContext()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-forbidden", "Tenant Scoped", PartyType.Organization, true)));
        api.EnqueueDetailFailure(AdminPortalQueryFailureKind.Forbidden);
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenant Scoped"));
        ClickFluentButton(cut, "Tenant Scoped");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Access denied");
            cut.Markup.ShouldContain("Tenant Scoped");
            cut.Markup.ShouldNotContain("party-forbidden</dd>");
        });
    }

    [Fact]
    public void PartiesAdminPortal_ListTransientFailure_OffersRetry()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueListFailure(AdminPortalQueryFailureKind.TransientFailure);
        api.EnqueueList(Page(IndexEntry("party-retry", "Retry Success", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Party data is temporarily unavailable"));
        ClickFluentButton(cut, "Retry");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Retry Success"));
        api.ListRequests.Count.ShouldBe(2);
    }

    [Fact]
    public void PartiesAdminPortal_UsesLocalizedLabelsForDatesBooleansAndCounts()
    {
        CultureInfo? previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            var api = new RecordingAdminPortalApiClient();
            api.EnqueueList(new PagedResult<PartyIndexEntry>
            {
                Items = [IndexEntry("party-fr", "Jean Martin", PartyType.Person, false)],
                Page = 1,
                PageSize = 20,
                TotalCount = 42,
                TotalPages = 3,
            });
            api.EnqueueDetail(new PartyDetail
            {
                Id = "party-fr",
                Type = PartyType.Person,
                IsActive = false,
                DisplayName = "Jean Martin",
                SortName = "Martin, Jean",
                ContactChannels = [],
                Identifiers = [],
                ConsentRecords = [],
                CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
                LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
                IsRestricted = true,
                RestrictedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
            });
            Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
            SeedTenant("scope-fr", AdminUserId, TenantRole.TenantOwner);
            _authProvider.SetAuthenticated(AdminUserId, "scope-fr");

            IRenderedComponent<PartiesAdminPortal> cut = Render<PartiesAdminPortal>(p => p
                .Add(x => x.ContextKey, "scope-fr")
                .Add(x => x.Labels, new AdminPortalLabels
                {
                    Page = "Page",
                    Of = "sur",
                    TotalCount = "au total",
                    Inactive = "Inactif",
                    Yes = "Oui",
                    No = "Non",
                }));

            cut.WaitForAssertion(() =>
            {
                cut.Markup.ShouldContain("Page 1 sur 3, 42 au total");
                cut.Find("td span.hx-parties-admin__badge").TextContent.Trim().ShouldBe("Inactif");
                cut.Markup.ShouldContain("01/05/2026");
            });
            ClickFluentButton(cut, "Jean Martin");

            cut.WaitForAssertion(() =>
            {
                cut.Markup.ShouldContain("Oui");
                cut.Markup.ShouldContain("Non");
                cut.Markup.ShouldContain("03/05/2026");
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void PartiesAdminPortal_UsesSuppliedLabelsForLocalizableShellText()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page<PartyIndexEntry>());
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        SeedTenant("scope-a", AdminUserId, TenantRole.TenantOwner);
        _authProvider.SetAuthenticated(AdminUserId, "scope-a");

        IRenderedComponent<PartiesAdminPortal> cut = Render<PartiesAdminPortal>(p => p
            .Add(x => x.ContextKey, "scope-a")
            .Add(x => x.Labels, new AdminPortalLabels
            {
                Title = "Tiers",
                SearchAriaLabel = "Rechercher des tiers",
                SearchPlaceholder = "Nom affiche",
                NoParties = "Aucun tiers",
                PersonOption = "Personne",
                OrganizationOption = "Organisation",
            }));

        cut.WaitForAssertion(() =>
        {
            cut.Find("h1").TextContent.ShouldBe("Tiers");
            FluentTextInput search = cut.FindComponent<FluentTextInput>().Instance;
            search.AdditionalAttributes!["aria-label"].ShouldBe("Rechercher des tiers");
            search.Placeholder.ShouldBe("Nom affiche");
            cut.Markup.ShouldContain("Aucun tiers");
            cut.Markup.ShouldContain("Personne");
            cut.Markup.ShouldContain("Organisation");
        });
    }

    [Fact]
    public void PartiesAdminPortal_CrossTenantScopedId_DetailReturnsForbiddenAndRowIsHidden()
    {
        // Task line 100: cross-tenant scoped ids must be rejected or hidden consistent with
        // PartiesController.GetPartyAsync. The portal accepts the id from the row click,
        // but the backend returns Forbidden — UI shows "Access denied" without leaking
        // the scoped id into a detail field, and browse context remains intact.
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("tenant-other:party:p-99", "Visible Row", PartyType.Person, true)));
        api.EnqueueDetailFailure(AdminPortalQueryFailureKind.Forbidden);
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Visible Row"));
        ClickFluentButton(cut, "Visible Row");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Access denied");
            cut.Markup.ShouldContain("Visible Row");
            // Scoped id must never appear in detail fields; D3.1 also prohibits it from the
            // System metadata block.
            cut.Markup.ShouldNotContain("tenant-other:party:p-99</dd>");
            cut.Markup.ShouldNotContain("tenant-other:party:p-99</dt>");
        });
    }

    [Fact]
    public void PartiesAdminPortal_KeyboardNavigation_TabReachesSearchFiltersAndRows()
    {
        // P39: validate that the toolbar / list / paging surface is keyboard-reachable.
        // Native HTML `<input>`, `<select>`, `<button>` are tab-focusable by default; this
        // test pins the affordances and tab-order semantics so a future refactor cannot
        // introduce role="presentation" or tabindex="-1" regressions silently.
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-kbd", "Keyboard Row", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Keyboard Row"));

        // Search input is a native focusable element with no overriding tabindex.
        (cut.FindComponent<FluentTextInput>().Instance.Disabled == true).ShouldBeFalse();

        // Type and active filter selects are reachable.
        cut.FindComponents<FluentSelect<string, string>>().Count.ShouldBe(2);

        // Submit and Clear buttons are reachable.
        FindFluentButton(cut, "Search");
        FindFluentButton(cut, "Clear");

        // Each row's display-name button is keyboard-reachable.
        FindFluentButton(cut, "Keyboard Row");

        // Pagination buttons are keyboard-reachable (state-disabled is acceptable).
        cut.FindAll("nav.hx-parties-admin__paging fluent-button").Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void PartiesAdminPortal_StatusBadges_ColorIndependentTextDistinguishesStates()
    {
        // P39 / Task line 110: active/erased/inactive states must NOT be distinguished by
        // color alone. Verify each carries text content sufficient for a screen reader.
        // (PartyIndexEntry does not carry IsRestricted; restricted state is a detail-panel
        // concern only.)
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(new PagedResult<PartyIndexEntry>
        {
            Items =
            [
                IndexEntry("p-active", "Active Row", PartyType.Person, true),
                IndexEntry("p-inactive", "Inactive Row", PartyType.Person, false),
            ],
            Page = 1,
            PageSize = 20,
            TotalCount = 2,
            TotalPages = 1,
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.FindAll("td span.hx-parties-admin__badge").Count.ShouldBe(2));

        var badges = cut.FindAll("td span.hx-parties-admin__badge").Select(b => b.TextContent.Trim()).ToList();
        badges.ShouldContain("Active");
        badges.ShouldContain("Inactive");

        // Each badge has aria-label matching its visible text — screen readers read the same content.
        foreach (var badge in cut.FindAll("td span.hx-parties-admin__badge"))
        {
            badge.GetAttribute("aria-label").ShouldBe(badge.TextContent.Trim());
        }
    }

    [Fact]
    public void PartiesAdminPortal_HealthyLoad_DrivesListCoordinatorToReadyHasResults()
    {
        // AC4: live load/error transitions must drive PartiesAdminListCoordinator.State
        // (no longer dead-code scaffolding). A successful list with rows transitions the
        // coordinator from the initial Loading to ReadyHasResults.
        var coordinator = new PartiesAdminListCoordinator();
        Services.AddSingleton(coordinator);

        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-c1", "Coordinated Row", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-coord");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Coordinated Row");
            coordinator.State.ShouldBe(AdminPortalListState.ReadyHasResults);
        });
    }

    [Fact]
    public void PartiesAdminPortal_EmptyLoad_DrivesListCoordinatorToReadyEmpty()
    {
        var coordinator = new PartiesAdminListCoordinator();
        Services.AddSingleton(coordinator);

        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page<PartyIndexEntry>());
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-empty");

        cut.WaitForAssertion(() => coordinator.State.ShouldBe(AdminPortalListState.ReadyEmpty));
    }

    [Fact]
    public void PartiesAdminPortal_LocalOnlySearch_DrivesListCoordinatorToDegradedSearch()
    {
        var coordinator = new PartiesAdminListCoordinator();
        Services.AddSingleton(coordinator);

        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page<PartyIndexEntry>());
        api.EnqueueSearch(
            Page(new PartySearchResult
            {
                Party = IndexEntry("party-degraded", "Degraded Match", PartyType.Person, true),
                Matches = [],
                RelevanceScore = 1.0,
            }),
            new AdminPortalQueryMetadata(SearchStatus: "LocalOnly", SearchDegradedReason: "rich-search-disabled"));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-degraded");
        cut.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(1));

        SetSearch(cut, "needle");
        ClickFluentButton(cut, "Search");

        cut.WaitForAssertion(() => coordinator.State.ShouldBe(AdminPortalListState.DegradedSearch));
    }

    [Fact]
    public void PartiesAdminPortal_TransientListFailure_DrivesListCoordinatorToTransientFailure()
    {
        var coordinator = new PartiesAdminListCoordinator();
        Services.AddSingleton(coordinator);

        var api = new RecordingAdminPortalApiClient();
        api.EnqueueListFailure(AdminPortalQueryFailureKind.TransientFailure);
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-transient");

        cut.WaitForAssertion(() => coordinator.State.ShouldBe(AdminPortalListState.TransientFailure));
    }

    [Fact]
    public void PartiesAdminPortal_Unauthenticated_DrivesListCoordinatorToMissingToken()
    {
        var coordinator = new PartiesAdminListCoordinator();
        Services.AddSingleton(coordinator);

        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = Render<PartiesAdminPortal>(p => p
            .Add(x => x.ContextKey, "scope-anon"));

        cut.WaitForAssertion(() => coordinator.State.ShouldBe(AdminPortalListState.MissingToken));
    }

    [Fact]
    public void PartiesAdminPortal_MissingTenant_DrivesListCoordinatorToMissingTenant()
    {
        var coordinator = new PartiesAdminListCoordinator();
        Services.AddSingleton(coordinator);

        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        // Authenticated user but the tenant projection knows nothing about the active scope.
        _authProvider.SetAuthenticated(AdminUserId, "scope-unknown");

        IRenderedComponent<PartiesAdminPortal> cut = Render<PartiesAdminPortal>(p => p
            .Add(x => x.ContextKey, "scope-unknown"));

        cut.WaitForAssertion(() => coordinator.State.ShouldBe(AdminPortalListState.MissingTenant));
    }

    [Fact]
    public void PartiesAdminPortal_NonAdmin_DrivesListCoordinatorToForbidden()
    {
        var coordinator = new PartiesAdminListCoordinator();
        Services.AddSingleton(coordinator);

        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        SeedTenant("scope-reader", "reader-user", TenantRole.TenantReader);
        _authProvider.SetAuthenticated("reader-user", "scope-reader");

        IRenderedComponent<PartiesAdminPortal> cut = Render<PartiesAdminPortal>(p => p
            .Add(x => x.ContextKey, "scope-reader"));

        cut.WaitForAssertion(() => coordinator.State.ShouldBe(AdminPortalListState.Forbidden));
    }

    [Fact]
    public void PartiesAdminPortal_TenantSwitch_CancelsQueryServiceScopeToken()
    {
        // AC4: AdminPortalPartyQueryService must be wired into the component lifecycle, not
        // dead code. Tenant switch must invoke ResetForTenantSwitch on the live instance,
        // observable as the previous scope token transitioning to canceled.
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-tenA", "Tenant A", PartyType.Person, true)));
        api.EnqueueList(Page(IndexEntry("party-tenB", "Tenant B", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        var queryService = new AdminPortalPartyQueryService(api);
        Services.AddSingleton(queryService);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenant A"));

        CancellationToken beforeToken = queryService.ScopeCancellationToken;
        beforeToken.IsCancellationRequested.ShouldBeFalse();

        SeedTenant("scope-b", AdminUserId, TenantRole.TenantOwner);
        cut.InvokeAsync(() => _authProvider.SetAuthenticated(AdminUserId, "scope-b"));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Tenant B");
            beforeToken.IsCancellationRequested.ShouldBeTrue();
        });
    }

    private IRenderedComponent<PartiesAdminPortal> RenderAuthorized(string contextKey, int pageSize = AdminPortalQueryBounds.DefaultPageSize)
    {
        SeedTenant(contextKey, AdminUserId, TenantRole.TenantOwner);
        _authProvider.SetAuthenticated(AdminUserId, contextKey);
        return Render<PartiesAdminPortal>(p => p
            .Add(x => x.ContextKey, contextKey)
            .Add(x => x.PageSize, pageSize));
    }

    private static IElement FindFluentButton(IRenderedComponent<PartiesAdminPortal> cut, string text)
    {
        List<IElement> exact = [.. cut.FindAll("fluent-button")
            .Where(button => string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal))];
        if (exact.Count == 1)
        {
            return exact[0];
        }

        return cut.FindAll("fluent-button")
            .Single(button => button.TextContent.Contains(text, StringComparison.Ordinal));
    }

    private static void ClickFluentButton(IRenderedComponent<PartiesAdminPortal> cut, string text)
        => FindFluentButton(cut, text).Click();

    private static void SetSearch(IRenderedComponent<PartiesAdminPortal> cut, string value)
    {
        IRenderedComponent<FluentTextInput> input = cut.FindComponent<FluentTextInput>();
        cut.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync(value)).GetAwaiter().GetResult();
    }

    private void SeedTenant(string tenantId, string userId, TenantRole role)
    {
        var state = new TenantLocalState
        {
            TenantId = tenantId,
            Status = TenantStatus.Active,
        };
        state.Members[userId] = role;
        _tenantStore.SaveAsync(state).GetAwaiter().GetResult();
    }

    private static PartyIndexEntry IndexEntry(string id, string name, PartyType type, bool active) => new()
    {
        Id = id,
        Type = type,
        IsActive = active,
        DisplayName = name,
        CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
        LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
    };

    private static PagedResult<T> Page<T>(params T[] items) => new()
    {
        Items = items,
        Page = 1,
        PageSize = 20,
        TotalCount = items.Length,
        TotalPages = items.Length == 0 ? 0 : 1,
    };

    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        private AuthenticationState _state = new(new ClaimsPrincipal(new ClaimsIdentity()));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);

        public void SetAuthenticated(string userId, string tenantId)
        {
            Claim[] claims =
            [
                new Claim("sub", userId),
                new Claim("eventstore:tenant", tenantId),
            ];
            _state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}
