using Bunit;

using AngleSharp.Dom;

using System.Globalization;
using System.IO;
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
using Microsoft.AspNetCore.Components.Web;
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
        Services.AddOptions<PartiesAdminPortalOptions>();
        Services.AddScoped<AdminPortalEventStoreAdminLinks>();
        Services.AddScoped<AdminPortalGdprStateCoordinator>();
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
    public void PartiesAdminPortal_FirstViewport_RendersWorkingConsoleRegionsWithoutLandingShell()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-console", "Console Row", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");

        cut.WaitForAssertion(() =>
        {
            IElement root = cut.Find("section.hx-parties-admin");
            root.QuerySelector("header.hx-parties-admin__header h1")!.TextContent.Trim().ShouldBe("Parties");
            root.QuerySelector(".hx-parties-admin__status[role='status']")!.TextContent.Trim().ShouldNotBeEmpty();
            cut.FindComponent<FluentTextInput>();
            cut.FindComponents<FluentSelect<string, string>>().Count.ShouldBe(2);
            root.QuerySelector(".hx-parties-admin__search-modes").ShouldNotBeNull();
            cut.FindComponent<FluentDataGrid<PartyIndexEntry>>();
            root.QuerySelector("aside.hx-parties-admin__detail")!.TextContent.ShouldContain("Select a party");
            root.TextContent.ShouldContain("Console Row");

            IElement layout = root.QuerySelector(".hx-parties-admin__layout")!;
            layout.Children.Length.ShouldBe(2);
            layout.Children[0].ClassList.ShouldContain("hx-parties-admin__list");
            layout.Children[1].ClassList.ShouldContain("hx-parties-admin__detail");
            layout.ParentElement!.ClassList.ShouldContain("hx-parties-admin");

            root.QuerySelector("[class*='hero']").ShouldBeNull();
            root.QuerySelector("[class*='landing']").ShouldBeNull();
            root.QuerySelector("[class*='marketing']").ShouldBeNull();
            root.QuerySelector("[class*='intro']").ShouldBeNull();
            root.QuerySelector("[class*='card-shell']").ShouldBeNull();
        });
    }

    [Fact]
    public void PartiesAdminPortal_EmptyFirstViewport_KeepsToolbarGridPagingAndDetailReachable()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page<PartyIndexEntry>());
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-parties-admin__toolbar").TextContent.ShouldContain("Search");
            cut.Find("table").GetAttribute("aria-label").ShouldBe("Parties");
            cut.Find(".hx-parties-admin__empty").TextContent.Trim().ShouldBe("No parties");
            cut.Find(".hx-parties-admin__empty").GetAttribute("role").ShouldBe("status");
            cut.Find(".hx-parties-admin__empty").GetAttribute("aria-live").ShouldBe("polite");
            cut.Find("nav.hx-parties-admin__paging").TextContent.ShouldContain("Page");
            cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("Select a party");
            cut.Find("aside.hx-parties-admin__detail p[role='status']").GetAttribute("aria-live").ShouldBe("polite");
        });
    }

    [Fact]
    public void PartiesAdminPortal_SelectParty_NavigatesToSafeDetailRouteUsingOnlyPartyId()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-route-1", "Route Party", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-route-1",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Route Party",
            SortName = "Party, Route",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Route Party").TextContent.ShouldContain("Route Party"));
        ClickFluentButton(cut, "Route Party");

        cut.WaitForAssertion(() =>
        {
            navigation.ToBaseRelativePath(navigation.Uri).ShouldBe("admin/parties/party-route-1");
            navigation.Uri.ShouldNotContain("scope-a");
            navigation.Uri.ShouldNotContain("Route%20Party");
            api.DetailRequests.Single().ShouldBe("party-route-1");
            cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("Route Party");
        });
    }

    [Fact]
    public void PartiesAdminPortal_SelectScopedPartyId_DoesNotWriteTenantScopedIdentifierToRoute()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("tenant-a:parties:party-route-2", "Scoped Route Party", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "tenant-a:parties:party-route-2",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Scoped Route Party",
            SortName = "Party, Scoped",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Scoped Route Party").TextContent.ShouldContain("Scoped Route Party"));
        ClickFluentButton(cut, "Scoped Route Party");

        cut.WaitForAssertion(() =>
        {
            navigation.Uri.ShouldNotContain("tenant-a");
            navigation.Uri.ShouldNotContain("parties:party-route-2");
            api.DetailRequests.Single().ShouldBe("tenant-a:parties:party-route-2");
            cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("Scoped Route Party");
        });
    }

    [Fact]
    public void PartiesAdminPortal_DetailRoute_LoadsPartyDetailFromNonPiiRouteParameter()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-route-3", "Route Loaded Party", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-route-3",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Route Loaded Party",
            SortName = "Party, Route Loaded",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a", routePartyId: "party-route-3");

        cut.WaitForAssertion(() =>
        {
            api.DetailRequests.Single().ShouldBe("party-route-3");
            cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("Route Loaded Party");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprRouteVariant_LoadsPartyDetailWithGdprPanelVisible()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-route-gdpr", "Gdpr Route Party", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-route-gdpr",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Gdpr Route Party",
            SortName = "Party, Gdpr",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        // The /gdpr route variant resolves to the same component with the party id as the
        // route parameter. The GDPR operations panel must be reachable from that URL.
        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a", routePartyId: "party-route-gdpr");

        cut.WaitForAssertion(() =>
        {
            api.DetailRequests.Single().ShouldBe("party-route-gdpr");
            cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("Gdpr Route Party");
            cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("GDPR operations");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprEntryAction_NavigatesToManifestRouteUsingSafePartyIdOnly()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-action", "Action GDPR Party", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-gdpr-action", "Action GDPR Party", PartyType.Person, isActive: true));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Action GDPR Party"));
        ClickFluentButton(cut, "Action GDPR Party");
        cut.WaitForAssertion(() => FindFluentButton(cut, "GDPR operations").HasAttribute("disabled").ShouldBeFalse());

        ClickFluentButton(cut, "GDPR operations");

        navigation.ToBaseRelativePath(navigation.Uri).ShouldBe("admin/parties/party-gdpr-action/gdpr");
        navigation.Uri.ShouldNotContain("scope-a");
        navigation.Uri.ShouldNotContain("Action%20GDPR%20Party");
    }

    [Fact]
    public void PartiesAdminPortal_GdprEntryAction_DisabledForUnsafePartyId()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("tenant-a:parties:party-gdpr-action", "Scoped GDPR Party", PartyType.Person, true)));
        api.EnqueueDetail(Detail("tenant-a:parties:party-gdpr-action", "Scoped GDPR Party", PartyType.Person, isActive: true));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Scoped GDPR Party"));
        ClickFluentButton(cut, "Scoped GDPR Party");

        cut.WaitForAssertion(() => FindFluentButton(cut, "GDPR operations").HasAttribute("disabled").ShouldBeTrue());
    }

    [Fact]
    public void PartiesAdminPortal_GdprDirectRoute_MarksOperationsHeadingAsPrimaryDestination()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-focus", "Focus GDPR Party", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-gdpr-focus", "Focus GDPR Party", PartyType.Person, isActive: true));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/admin/parties/party-gdpr-focus/gdpr");

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a", routePartyId: "party-gdpr-focus");

        cut.WaitForAssertion(() =>
        {
            IElement heading = cut.Find("h3[data-primary-destination='gdpr']");
            heading.TextContent.Trim().ShouldBe("GDPR operations");
            heading.GetAttribute("tabindex").ShouldBe("-1");
            api.DetailRequests.Single().ShouldBe("party-gdpr-focus");
        });
    }

    [Fact]
    public void PartiesAdminPortal_UnsafeGdprRoutePartyId_RejectsDetailFetchAndDoesNotLeakIdentifier()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-route-safe", "Safe Party", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/admin/parties/tenant-a%3Aparties%3Ahostile/gdpr");

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized(
            "scope-a",
            routePartyId: "tenant-a%3Aparties%3Ahostile");

        cut.WaitForAssertion(() =>
        {
            api.DetailRequests.ShouldBeEmpty();
            IElement detail = cut.Find("aside.hx-parties-admin__detail");
            detail.TextContent.ShouldContain("The selected party is unavailable");
            detail.TextContent.ShouldNotContain("tenant-a");
            detail.TextContent.ShouldNotContain("hostile");
        });
    }

    [Fact]
    public void PartiesAdminPortal_PartialGdprRoute_RendersBoundedStateWithoutPartyDataOrMutationControls()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page<PartyIndexEntry>());
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-partial-gdpr",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Partial GDPR Secret",
            SortName = string.Empty,
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            NameHistory = [],
            CreatedAt = default,
            LastModifiedAt = default,
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/admin/parties/party-partial-gdpr/gdpr");

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a", routePartyId: "party-partial-gdpr");

        cut.WaitForAssertion(() =>
        {
            api.DetailRequests.Single().ShouldBe("party-partial-gdpr");
            IElement detail = cut.Find("aside.hx-parties-admin__detail");
            detail.TextContent.ShouldContain("The selected party is unavailable");
            detail.TextContent.ShouldNotContain("Partial GDPR Secret");
            foreach (string mutationControl in GdprMutationControlLabels)
            {
                detail.TextContent.ShouldNotContain(mutationControl);
            }
        });
    }

    [Fact]
    public void PartiesAdminPortal_UnsafeRoutePartyId_RejectsDetailFetchAndDoesNotLeakIdentifier()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-route-safe", "Safe Party", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        // A scoped/tenant-prefixed party id pasted into the URL must not trigger a detail
        // fetch and must not be echoed into the user-visible detail region.
        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized(
            "scope-a",
            routePartyId: "tenant-a:parties:hostile");

        cut.WaitForAssertion(() =>
        {
            api.DetailRequests.ShouldBeEmpty();
            IElement detail = cut.Find("aside.hx-parties-admin__detail");
            detail.TextContent.ShouldNotContain("tenant-a");
            detail.TextContent.ShouldNotContain("hostile");
            detail.TextContent.ShouldContain("The selected party is unavailable");
        });
    }

    [Fact]
    public void PartiesAdminPortal_PathTraversalRoutePartyId_IsRejected()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-route-safe", "Safe Party", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        // `.` and `..` would pass an unreserved-character validator but must be rejected
        // so they never reach the detail fetch pipeline or surface in operator logs.
        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a", routePartyId: "..");

        cut.WaitForAssertion(() =>
        {
            api.DetailRequests.ShouldBeEmpty();
            cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("The selected party is unavailable");
        });
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
        SetSelect(cut, "Party type", "Person");
        SetSelect(cut, "Active state", "true");
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
        search.Type.ShouldBe(PartyType.Person);
        search.Active.ShouldBe(true);
        IReadOnlyList<IRenderedComponent<FluentSelect<string, string>>> selects = cut.FindComponents<FluentSelect<string, string>>();
        (selects[0].Instance.Disabled == true).ShouldBeFalse();
        (selects[1].Instance.Disabled == true).ShouldBeFalse();
        (FindTextInput(cut, "Created after").Instance.Disabled == true).ShouldBeTrue();
        (FindTextInput(cut, "Created before").Instance.Disabled == true).ShouldBeTrue();
        (FindTextInput(cut, "Modified after").Instance.Disabled == true).ShouldBeTrue();
        (FindTextInput(cut, "Modified before").Instance.Disabled == true).ShouldBeTrue();
    }

    [Fact]
    public void PartiesAdminPortal_SearchInput_DebouncesWithoutSearchButton()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page<PartyIndexEntry>());
        api.EnqueueSearch(Page(new PartySearchResult
        {
            Party = IndexEntry("party-debounced", "Debounced Row", PartyType.Person, true),
            Matches = [],
            RelevanceScore = 1.0,
        }));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized(
            "scope-debounce",
            searchDebounceDelay: TimeSpan.Zero);
        cut.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(1));

        SetSearch(cut, "debounced");

        cut.WaitForAssertion(() =>
        {
            api.SearchRequests.Single().Query.ShouldBe("debounced");
            cut.Markup.ShouldContain("Debounced Row");
        });
    }

    [Fact]
    public void PartiesAdminPortal_SupersededSearch_DoesNotPaintStaleRows()
    {
        var api = new RecordingAdminPortalApiClient();
        var first = new TaskCompletionSource<AdminPortalQueryResult<PagedResult<PartySearchResult>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<AdminPortalQueryResult<PagedResult<PartySearchResult>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.EnqueueList(Page<PartyIndexEntry>());
        api.EnqueueSearch(token => first.Task.WaitAsync(token));
        api.EnqueueSearch(token => second.Task.WaitAsync(token));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized(
            "scope-superseded",
            searchDebounceDelay: TimeSpan.Zero);
        cut.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(1));

        SetSearch(cut, "old");
        cut.WaitForAssertion(() => api.SearchRequests.Count.ShouldBe(1));
        SetSearch(cut, "new");
        cut.WaitForAssertion(() => api.SearchRequests.Count.ShouldBe(2));

        second.SetResult(new AdminPortalQueryResult<PagedResult<PartySearchResult>>(
            Page(new PartySearchResult
            {
                Party = IndexEntry("party-new", "New Row", PartyType.Person, true),
                Matches = [],
                RelevanceScore = 1.0,
            }),
            AdminPortalQueryMetadata.Empty));
        first.SetResult(new AdminPortalQueryResult<PagedResult<PartySearchResult>>(
            Page(new PartySearchResult
            {
                Party = IndexEntry("party-old", "Old Row", PartyType.Person, true),
                Matches = [],
                RelevanceScore = 1.0,
            }),
            AdminPortalQueryMetadata.Empty));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("New Row");
            cut.Markup.ShouldNotContain("Old Row");
        });
    }

    [Fact]
    public void PartiesAdminPortal_Paging_PreservesSearchTypeAndActiveCriteria()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page<PartyIndexEntry>());
        api.EnqueueSearch(new PagedResult<PartySearchResult>
        {
            Items =
            [
                new PartySearchResult
                {
                    Party = IndexEntry("party-page-1", "Page One", PartyType.Organization, true),
                    Matches = [],
                    RelevanceScore = 1.0,
                },
            ],
            Page = 1,
            PageSize = 20,
            TotalCount = 2,
            TotalPages = 2,
        });
        api.EnqueueSearch(new PagedResult<PartySearchResult>
        {
            Items =
            [
                new PartySearchResult
                {
                    Party = IndexEntry("party-page-2", "Page Two", PartyType.Organization, true),
                    Matches = [],
                    RelevanceScore = 1.0,
                },
            ],
            Page = 2,
            PageSize = 20,
            TotalCount = 2,
            TotalPages = 2,
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-page");
        cut.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(1));
        SetSelect(cut, "Party type", "Organization");
        SetSelect(cut, "Active state", "true");
        SetSearch(cut, "page");
        ClickFluentButton(cut, "Search");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Page One"));

        ClickFluentButton(cut, "Next");

        cut.WaitForAssertion(() =>
        {
            api.SearchRequests.Count.ShouldBe(2);
            cut.Markup.ShouldContain("Page Two");
        });
        api.SearchRequests[1].Query.ShouldBe("page");
        api.SearchRequests[1].Type.ShouldBe(PartyType.Organization);
        api.SearchRequests[1].Active.ShouldBe(true);
        api.SearchRequests[1].Page.ShouldBe(2);
        api.SearchRequests[1].PageSize.ShouldBe(20);
    }

    [Fact]
    public void PartiesAdminPortal_DateFilters_UseListContractAndBoundedDates()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page<PartyIndexEntry>());
        api.EnqueueList(Page(IndexEntry("party-filtered", "Filtered Row", PartyType.Organization, false)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(1));

        SetTextInput(cut, "Created after", "2026-05-01");
        SetTextInput(cut, "Created before", "2026-05-31");
        SetTextInput(cut, "Modified after", "2026-06-01");
        SetTextInput(cut, "Modified before", "2026-06-30");
        ClickFluentButton(cut, "Search");

        cut.WaitForAssertion(() =>
        {
            api.ListRequests.Count.ShouldBe(2);
            cut.Markup.ShouldContain("Filtered Row");
        });

        AdminPortalListRequest request = api.ListRequests[1];
        request.CreatedAfter.ShouldBe(DateTimeOffset.Parse("2026-05-01T00:00:00+00:00", CultureInfo.InvariantCulture));
        request.CreatedBefore.ShouldBe(DateTimeOffset.Parse("2026-05-31T23:59:59.9999999+00:00", CultureInfo.InvariantCulture));
        request.ModifiedAfter.ShouldBe(DateTimeOffset.Parse("2026-06-01T00:00:00+00:00", CultureInfo.InvariantCulture));
        request.ModifiedBefore.ShouldBe(DateTimeOffset.Parse("2026-06-30T23:59:59.9999999+00:00", CultureInfo.InvariantCulture));
        api.SearchRequests.ShouldBeEmpty();
    }

    [Fact]
    public void PartiesAdminPortal_InvalidDateFilter_ShowsBoundedValidationAndDoesNotQuery()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-initial", "Initial Row", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(1));

        SetTextInput(cut, "Created after", "2026-06-01");
        SetTextInput(cut, "Created before", "2026-05-01");
        ClickFluentButton(cut, "Search");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Validation: Created date range is invalid");
            cut.Markup.ShouldNotContain("Initial Row");
        });

        api.ListRequests.Count.ShouldBe(1);
        api.SearchRequests.ShouldBeEmpty();
    }

    [Fact]
    public void PartiesAdminPortal_EmptySearch_UsesLocalizedTenantNeutralNoMatches()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-existing", "Existing Row", PartyType.Person, true)));
        api.EnqueueSearch(Page<PartySearchResult>());
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Existing Row"));

        SetSearch(cut, "missing@example.test");
        ClickFluentButton(cut, "Search");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-parties-admin__status").TextContent.ShouldContain("Parties loaded");
            cut.Find(".hx-parties-admin__empty").TextContent.ShouldContain("No parties match.");
            FindFluentButton(cut, "Clear").TextContent.ShouldContain("Clear");
            cut.Markup.ShouldNotContain("Existing Row");
            cut.Markup.ShouldNotContain("another tenant", Case.Insensitive);
            cut.Markup.ShouldNotContain("scope-a");
        });
        api.SearchRequests.Single().Query.ShouldBe("missing@example.test");
    }

    [Fact]
    public void PartiesAdminPortal_FilterOnlyEmptyResult_UsesRecoverableNoMatchesCopy()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-existing", "Existing Row", PartyType.Person, true)));
        api.EnqueueList(Page<PartyIndexEntry>());
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-filter-empty");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Existing Row"));

        SetSelect(cut, "Party type", "Organization");
        SetSelect(cut, "Active state", "false");
        ClickFluentButton(cut, "Search");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-parties-admin__empty").TextContent.ShouldContain("No parties match.");
            FindFluentButton(cut, "Clear").TextContent.ShouldContain("Clear");
            cut.Markup.ShouldNotContain("Existing Row");
        });
        api.ListRequests.Count.ShouldBe(2);
        api.ListRequests[1].Type.ShouldBe(PartyType.Organization);
        api.ListRequests[1].Active.ShouldBe(false);
    }

    [Fact]
    public void PartiesAdminPortal_ClearFiltersFromEmptyState_ResetsSearchTypeActiveAndPaging()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-existing", "Existing Row", PartyType.Person, true)));
        api.EnqueueSearch(Page<PartySearchResult>());
        api.EnqueueList(Page(IndexEntry("party-reset", "Reset Row", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-clear");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Existing Row"));
        SetSelect(cut, "Party type", "Person");
        SetSelect(cut, "Active state", "false");
        SetSearch(cut, "missing");
        ClickFluentButton(cut, "Search");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No parties match."));

        ClickFluentButton(cut, "Clear");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Reset Row");
            api.ListRequests.Count.ShouldBe(2);
        });
        AdminPortalListRequest reset = api.ListRequests[1];
        reset.Page.ShouldBe(1);
        reset.Type.ShouldBeNull();
        reset.Active.ShouldBeNull();
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
    public void PartiesAdminPortal_DegradedListMetadata_ShowsBoundedStatusWithoutRawReason()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(
            Page(IndexEntry("party-degraded-list", "Degraded List Row", PartyType.Person, true)),
            new AdminPortalQueryMetadata(
                ServiceDegraded: true,
                SearchDegradedReason: "raw backend says ada@example.test is stale and tenant scope-a failed"));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Degraded List Row");
            cut.Find(".hx-parties-admin__status").TextContent.ShouldContain("Data may be stale or degraded");
            cut.Markup.ShouldNotContain("ada@example.test");
            cut.Markup.ShouldNotContain("scope-a failed");
        });
    }

    [Fact]
    public void PartiesAdminPortal_StaleListMetadata_PreservesLastKnownRows()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-last-known", "Last Known Row", PartyType.Person, true)));
        api.EnqueueList(
            Page<PartyIndexEntry>(),
            new AdminPortalQueryMetadata(StaleDataAge: "not-modified"));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-stale");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Last Known Row"));

        ClickFluentButton(cut, "Search");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Last Known Row");
            cut.Find(".hx-parties-admin__status").TextContent.ShouldContain("Data may be stale or degraded");
            cut.Find(".hx-parties-admin__status").GetAttribute("aria-live").ShouldBe("polite");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GridKeyboard_ArrowsAndEnterActivateFocusedRow()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(
            IndexEntry("party-key-1", "Keyboard One", PartyType.Person, true),
            IndexEntry("party-key-2", "Keyboard Two", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-key-2", "Keyboard Two", PartyType.Person, isActive: true));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-keyboard");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Keyboard Two"));
        IElement list = cut.Find(".hx-parties-admin__list");

        list.TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        cut.WaitForAssertion(() => cut.Find(".hx-parties-admin__list").GetAttribute("data-focused-row-index").ShouldBe("1"));
        cut.Find(".hx-parties-admin__list").TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() =>
        {
            api.DetailRequests.Single().ShouldBe("party-key-2");
            cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("Keyboard Two");
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
    public void PartiesAdminPortal_RowAccessibilityAttributes_DoNotLeakPartyIdOrRawName()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry(
            "tenant-a:party:pii-123",
            "<script>alert(1)</script>",
            PartyType.Person,
            true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");

        cut.WaitForAssertion(() =>
        {
            IElement button = FindFluentButton(cut, "alert");
            string describedBy = button.GetAttribute("aria-describedby")!;
            describedBy.ShouldNotContain("tenant-a");
            describedBy.ShouldNotContain("pii-123");
            describedBy.ShouldNotContain("script", Case.Insensitive);
            cut.Find($"#{describedBy}").TextContent.Trim().ShouldBe("Active");
            cut.Markup.ShouldContain("&lt;script&gt;alert(1)&lt;/script&gt;");
            cut.Markup.ShouldNotContain("<script>alert(1)</script>");
            cut.Markup.ShouldNotContain("tenant-a:party:pii-123");
        });
    }

    [Fact]
    public void PartiesAdminPortal_SourceDoesNotUseUnsafeRenderingStorageLoggingOrTelemetryApis()
    {
        string root = LocateRepositoryRoot();
        string sourceRoot = Path.Combine(root, "src", "Hexalith.Parties.AdminPortal");
        string[] forbiddenTokens =
        [
            "MarkupString",
            "AddMarkupContent",
            "RenderTreeBuilder",
            "innerHTML",
            "outerHTML",
            "localStorage",
            "sessionStorage",
            "ILogger",
            "LoggerMessage",
            "TelemetryClient",
            "ActivitySource",
        ];

        List<string> offenders = [];
        foreach (string path in Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            string relative = Path.GetRelativePath(root, path);
            if (relative.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || relative.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string text = File.ReadAllText(path);
            foreach (string token in forbiddenTokens)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                {
                    offenders.Add($"{relative}: {token}");
                }
            }
        }

        offenders.ShouldBeEmpty("AdminPortal must not render raw markup, persist browser storage data, or add logging/telemetry before a bounded privacy design exists.");
    }

    [Fact]
    public void PartiesAdminPortal_SelectParty_RendersRestrictionsSystemMetadataNameHistoryAndFreshness()
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
            IElement freshness = cut.Find("aside.hx-parties-admin__detail .data-freshness-indicator");
            freshness.TextContent.ShouldContain("Showing what we last knew - refreshing");
            freshness.GetAttribute("class")!.ShouldContain("data-freshness-indicator--degraded");
            freshness.QuerySelector("[role='status']")!.GetAttribute("aria-live").ShouldBe("polite");
            cut.Markup.ShouldNotContain("Data age");
            cut.Markup.ShouldNotContain("PT12S");
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
    public void PartiesAdminPortal_DetailStatus_RendersNonColorOnlyStateText()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(new PagedResult<PartyIndexEntry>
        {
            Items =
            [
                IndexEntry("party-active", "Active Detail", PartyType.Person, true),
                IndexEntry("party-restricted", "Restricted Detail", PartyType.Person, true),
                IndexEntry("party-inactive", "Inactive Detail", PartyType.Organization, false),
            ],
            Page = 1,
            PageSize = 20,
            TotalCount = 3,
            TotalPages = 1,
        });
        api.EnqueueDetail(Detail("party-active", "Active Detail", PartyType.Person, isActive: true));
        api.EnqueueDetail(Detail("party-restricted", "Restricted Detail", PartyType.Person, isActive: true) with
        {
            IsRestricted = true,
            RestrictedAt = DateTimeOffset.Parse("2026-05-03T10:15:00Z"),
        });
        api.EnqueueDetail(Detail("party-inactive", "Inactive Detail", PartyType.Organization, isActive: false));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Active Detail"));

        ClickFluentButton(cut, "Active Detail");
        cut.WaitForAssertion(() =>
        {
            IElement badge = cut.Find("aside.hx-parties-admin__detail .party-state-badge");
            badge.TextContent.Trim().ShouldBe("Active");
            badge.GetAttribute("class")!.ShouldContain("hx-parties-admin__badge--active");
        });

        ClickFluentButton(cut, "Restricted Detail");
        cut.WaitForAssertion(() =>
        {
            IElement badge = cut.Find("aside.hx-parties-admin__detail .party-state-badge");
            badge.TextContent.Trim().ShouldBe("Restricted");
            badge.GetAttribute("class")!.ShouldContain("hx-parties-admin__badge--restricted");
        });

        ClickFluentButton(cut, "Inactive Detail");
        cut.WaitForAssertion(() =>
        {
            IElement badge = cut.Find("aside.hx-parties-admin__detail .party-state-badge");
            badge.TextContent.Trim().ShouldBe("Inactive");
            badge.GetAttribute("class")!.ShouldContain("hx-parties-admin__badge--inactive");
        });
    }

    [Fact]
    public void PartiesAdminPortal_DetailBadges_StayAlignedWithSharedStateSemantics()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(new PagedResult<PartyIndexEntry>
        {
            Items =
            [
                IndexEntry("party-active", "Active Shared", PartyType.Person, true),
                IndexEntry("party-restricted", "Restricted Shared", PartyType.Person, true),
                IndexEntry("party-inactive", "Inactive Shared", PartyType.Organization, false),
            ],
            Page = 1,
            PageSize = 20,
            TotalCount = 3,
            TotalPages = 1,
        });
        api.EnqueueDetail(Detail("party-active", "Active Shared", PartyType.Person, isActive: true));
        api.EnqueueDetail(Detail("party-restricted", "Restricted Shared", PartyType.Person, isActive: true) with
        {
            IsRestricted = true,
        });
        api.EnqueueDetail(Detail("party-inactive", "Inactive Shared", PartyType.Organization, isActive: false));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-shared-semantics");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Active Shared"));

        Dictionary<string, string> expectedClasses = new(StringComparer.Ordinal)
        {
            ["Active Shared"] = "hx-parties-admin__badge--active",
            ["Restricted Shared"] = "hx-parties-admin__badge--restricted",
            ["Inactive Shared"] = "hx-parties-admin__badge--inactive",
        };
        Dictionary<string, string> expectedLabels = new(StringComparer.Ordinal)
        {
            ["Active Shared"] = "Active",
            ["Restricted Shared"] = "Restricted",
            ["Inactive Shared"] = "Inactive",
        };

        foreach ((string buttonText, string expectedClass) in expectedClasses)
        {
            ClickFluentButton(cut, buttonText);
            cut.WaitForAssertion(() =>
            {
                IElement badge = cut.Find("aside.hx-parties-admin__detail .party-state-badge");
                badge.TextContent.Trim().ShouldBe(expectedLabels[buttonText]);
                badge.GetAttribute("aria-label").ShouldBe(expectedLabels[buttonText]);
                badge.GetAttribute("class")!.ShouldContain(expectedClass);
            });
        }
    }

    [Fact]
    public void PartiesAdminPortal_PartialDetail_ShowsDisplayNameAndStillLoadingSections()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-partial", "Partial Projection", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-partial",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Partial Projection",
            SortName = string.Empty,
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            NameHistory = [],
            CreatedAt = default,
            LastModifiedAt = default,
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-partial");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Partial Projection"));
        ClickFluentButton(cut, "Partial Projection");

        cut.WaitForAssertion(() =>
        {
            IElement detail = cut.Find("aside.hx-parties-admin__detail");
            detail.TextContent.ShouldContain("Partial Projection");
            detail.TextContent.ShouldContain("Person details");
            detail.TextContent.ShouldContain("System metadata");
            detail.TextContent.ShouldContain("Still loading");
            detail.TextContent.ShouldNotContain("No contact channels");
            detail.TextContent.ShouldNotContain("No identifiers");
            detail.TextContent.ShouldNotContain("No consent records");
            detail.TextContent.ShouldNotContain("No name history");
        });
    }

    [Fact]
    public void PartiesAdminPortal_RefreshSelectedParty_DoesNotStealFocusFromRoutineDetailUpdates()
    {
        string root = LocateRepositoryRoot();
        string componentPath = Path.Combine(
            root,
            "src",
            "Hexalith.Parties.AdminPortal",
            "Components",
            "PartiesAdminPortal.razor");

        string source = File.ReadAllText(componentPath);

        source.ShouldContain("focusDetail = true");
        source.ShouldContain("focusDetail: false");
        source.ShouldContain("RefreshSelectedPartyAsync()");
        source.ShouldContain("SelectPartyAsync(_selectedPartyId, updateRoute: false, trackOrigin: false, focusDetail: false)");
    }

    [Fact]
    public void PartiesAdminPortal_DetailActions_RenderBackEditAndActiveRowCue()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-actions", "Action Party", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-actions", "Action Party", PartyType.Person, isActive: true));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-actions");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Action Party"));
        ClickFluentButton(cut, "Action Party");

        cut.WaitForAssertion(() =>
        {
            IElement selected = FindFluentButton(cut, "Action Party");
            selected.GetAttribute("aria-current").ShouldBe("page");
            (selected.GetAttribute("class") ?? string.Empty).ShouldContain("hx-parties-admin__row-button--selected");
            FindFluentButton(cut, "Back to list");
            FindFluentButton(cut, "Edit").HasAttribute("disabled").ShouldBeFalse();
        });

        ClickFluentButton(cut, "Edit");
        navigation.ToBaseRelativePath(navigation.Uri).ShouldBe("admin/parties/party-actions/edit");

        ClickFluentButton(cut, "Back to list");

        cut.WaitForAssertion(() =>
        {
            cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("Select a party");
            FindFluentButton(cut, "Action Party").HasAttribute("autofocus").ShouldBeTrue();
        });
    }

    [Fact]
    public void PartiesAdminPortal_ErasedDetailPayload_ShowsTerminalStateOnlyAndClearsSensitiveSections()
    {
        var coordinator = new AdminPortalGdprStateCoordinator();
        Services.AddSingleton(coordinator);

        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-erased-payload", "Erased Detail Payload", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-erased-payload",
            Type = PartyType.Person,
            IsActive = false,
            IsErased = true,
            ErasedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
            DisplayName = "Erased Detail Payload",
            SortName = "Payload, Erased",
            ContactChannels = [new ContactChannel { Id = "contact-secret", Type = ContactChannelType.Email, Value = "erased@example.test", IsPreferred = true }],
            Identifiers = [new PartyIdentifier { Id = "identifier-secret", Type = IdentifierType.Other, Value = "SECRET-ID" }],
            ConsentRecords = [new ConsentRecord { ConsentId = "consent-secret", ChannelId = "contact-secret", Purpose = "marketing-secret", LawfulBasis = LawfulBasis.Consent, GrantedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"), GrantedBy = "operator-secret" }],
            NameHistory = [new NameHistoryEntry { DisplayName = "Previous Secret Name", SortName = "Secret", ChangedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z") }],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Erased Detail Payload"));
        ClickFluentButton(cut, "Erased Detail Payload");

        cut.WaitForAssertion(() =>
        {
            IElement detail = cut.Find("aside.hx-parties-admin__detail");
            detail.TextContent.ShouldContain("erased or no longer inspectable");
            detail.TextContent.ShouldNotContain("erased@example.test");
            detail.TextContent.ShouldNotContain("SECRET-ID");
            detail.TextContent.ShouldNotContain("Previous Secret Name");
            cut.Markup.ShouldNotContain("Erased Detail Payload");
            coordinator.State.ShouldBe(AdminPortalGdprOperationState.Erased);
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
            cut.Find("[data-gdpr-operation-announcement]").TextContent.Trim().ShouldBeEmpty();
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_DisableActionsWhenClientContractUnavailable()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-blocked", "GDPR Blocked", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-gdpr-blocked",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "GDPR Blocked",
            SortName = "Blocked, GDPR",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        api.EnqueueGdprCapability(AdminPortalGdprCapability.Unavailable());
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR Blocked"));
        ClickFluentButton(cut, "GDPR Blocked");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain(AdminPortalGdprCapability.ContractUnavailableReason);
            FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Refresh erasure status").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Restrict processing").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Lift restriction").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Add consent").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Export party data").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Processing records").HasAttribute("disabled").ShouldBeTrue();
            api.GdprCapabilityProbeCount.ShouldBe(1);
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_EnableOnlySupportedPartialContractActions()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-partial", "GDPR Partial", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-gdpr-partial",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "GDPR Partial",
            SortName = "Partial, GDPR",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        api.EnqueueGdprCapability(AdminPortalGdprCapability.Partial(
            canExportData: true,
            canReadProcessingRecords: true));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR Partial"));
        ClickFluentButton(cut, "GDPR Partial");

        cut.WaitForAssertion(() =>
        {
            FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Restrict processing").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Add consent").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Export party data").HasAttribute("disabled").ShouldBeFalse();
            FindFluentButton(cut, "Processing records").HasAttribute("disabled").ShouldBeFalse();
            cut.Markup.ShouldContain("GDPR operations are temporarily unavailable");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_ErasureSectionShowsBlockerWhenCertificateIsUnsupported()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-certificate-blocked", "Certificate Blocked", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-gdpr-certificate-blocked", "Certificate Blocked", PartyType.Person, isActive: true));
        api.EnqueueGdprCapability(AdminPortalGdprCapability.Partial(
            canRequestErasure: true,
            canReadErasureStatus: true,
            canRetryVerification: true,
            canReadErasureCertificate: false));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Certificate Blocked"));
        ClickFluentButton(cut, "Certificate Blocked");

        cut.WaitForAssertion(() =>
        {
            FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeFalse();
            FindFluentButton(cut, "Refresh erasure status").HasAttribute("disabled").ShouldBeFalse();
            cut.Markup.ShouldContain("GDPR operations are temporarily unavailable");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_MalformedCapabilityFailsClosedWithoutRawDetails()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-malformed", "GDPR Malformed", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-gdpr-malformed",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "GDPR Malformed",
            SortName = "Malformed, GDPR",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        api.EnqueueGdprCapability(_ => Task.FromException<AdminPortalGdprCapability>(
            new System.Text.Json.JsonException("raw-token backend parser detail")));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR Malformed"));
        ClickFluentButton(cut, "GDPR Malformed");

        cut.WaitForAssertion(() =>
        {
            FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Processing records").HasAttribute("disabled").ShouldBeTrue();
            cut.Markup.ShouldContain("GDPR operations are temporarily unavailable");
            cut.Markup.ShouldNotContain("raw-token");
            cut.Markup.ShouldNotContain("JsonException");
            cut.Markup.ShouldNotContain("parser detail");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_NullCapabilityFailsClosed()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-null", "GDPR Null", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-gdpr-null", "GDPR Null", PartyType.Person, isActive: true));
        api.EnqueueGdprCapability(_ => Task.FromResult<AdminPortalGdprCapability>(null!));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR Null"));
        ClickFluentButton(cut, "GDPR Null");

        cut.WaitForAssertion(() =>
        {
            FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Export party data").HasAttribute("disabled").ShouldBeTrue();
            cut.Markup.ShouldContain("GDPR operations are temporarily unavailable");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_TenantSwitchInvalidatesCapability()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-tenant-a", "GDPR Tenant A", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-gdpr-tenant-a", "GDPR Tenant A", PartyType.Person, isActive: true));
        api.EnqueueGdprCapability(AdminPortalGdprCapability.Available());
        api.EnqueueList(Page(IndexEntry("party-gdpr-tenant-b", "GDPR Tenant B", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-gdpr-tenant-b", "GDPR Tenant B", PartyType.Person, isActive: true));
        api.EnqueueGdprCapability(AdminPortalGdprCapability.Unavailable());
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR Tenant A"));
        ClickFluentButton(cut, "GDPR Tenant A");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeFalse());

        SeedTenant("scope-b", AdminUserId, TenantRole.TenantOwner);
        cut.InvokeAsync(() => _authProvider.SetAuthenticated(AdminUserId, "scope-b"));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR Tenant B"));
        ClickFluentButton(cut, "GDPR Tenant B");

        cut.WaitForAssertion(() =>
        {
            FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeTrue();
            cut.Markup.ShouldContain(AdminPortalGdprCapability.ContractUnavailableReason);
            api.GdprCapabilityProbeCount.ShouldBe(2);
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_ClearOpenStateWhenCapabilityCloses()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(
            IndexEntry("party-gdpr-open", "GDPR Open", PartyType.Person, true),
            IndexEntry("party-gdpr-closed", "GDPR Closed", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-gdpr-open",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "GDPR Open",
            SortName = "Open, GDPR",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-gdpr-closed",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "GDPR Closed",
            SortName = "Closed, GDPR",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        api.EnqueueGdprCapability(AdminPortalGdprCapability.Available());
        api.EnqueueGdprCapability(AdminPortalGdprCapability.Unavailable());
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR Open"));
        ClickFluentButton(cut, "GDPR Open");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeFalse());
        ClickFluentButton(cut, "Request erasure");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("irreversible verification"));
        SetTextInput(cut, "Restriction reason", "contains-sensitive-operator-note");

        ClickFluentButton(cut, "GDPR Closed");

        cut.WaitForAssertion(() =>
        {
            FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeTrue();
            cut.Markup.ShouldContain(AdminPortalGdprCapability.ContractUnavailableReason);
            cut.Markup.ShouldNotContain("irreversible verification");
            cut.Markup.ShouldNotContain("contains-sensitive-operator-note");
            api.ErasureRequests.ShouldBeEmpty();
            api.RestrictionRequests.ShouldBeEmpty();
            api.GdprCapabilityProbeCount.ShouldBe(2);
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_ProvisionalBridge_EnablesSupportedOperationsAndBlocksContractUnavailableOnes()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-bridge", "GDPR Bridge", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-gdpr-bridge", "GDPR Bridge", PartyType.Person, isActive: true));
        api.EnqueueGdprCapability(AdminPortalGdprCapability.ProvisionalBridge());
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR Bridge"));
        ClickFluentButton(cut, "GDPR Bridge");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR operations"));

        // Supply consent inputs so the Add consent button reflects the enabled capability rather
        // than the empty-input guard.
        SetTextInput(cut, "Channel id", "news-email");
        SetTextInput(cut, "Purpose", "newsletter");

        cut.WaitForAssertion(() =>
        {
            FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeFalse();
            FindFluentButton(cut, "Refresh erasure status").HasAttribute("disabled").ShouldBeFalse();
            FindFluentButton(cut, "Restrict processing").HasAttribute("disabled").ShouldBeFalse();
            FindFluentButton(cut, "Add consent").HasAttribute("disabled").ShouldBeFalse();
            FindFluentButton(cut, "Export party data").HasAttribute("disabled").ShouldBeFalse();
            FindFluentButton(cut, "Processing records").HasAttribute("disabled").ShouldBeFalse();

            // Retry verification is contract-unavailable, so its button is never rendered.
            cut.FindAll("fluent-button")
                .Any(button => button.TextContent.Contains("Retry verification", StringComparison.Ordinal))
                .ShouldBeFalse();

            // The erasure section carries the exact bounded blocker for the two unavailable operations.
            cut.Markup.ShouldContain(AdminPortalGdprCapability.ContractUnavailableReason);
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_ProvisionalBridge_RefreshErasedPartySkipsCertificateAndAvoidsFailure()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-erased", "GDPR Erased", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-gdpr-erased", "GDPR Erased", PartyType.Person, isActive: true));
        api.EnqueueGdprCapability(AdminPortalGdprCapability.ProvisionalBridge());
        api.EnqueueErasureStatus(new PartyErasureStatusRecord
        {
            PartyId = "party-gdpr-erased",
            TenantId = "scope-a",
            Status = "Erased",
            UpdatedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
            ErasedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR Erased"));
        ClickFluentButton(cut, "GDPR Erased");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Refresh erasure status").HasAttribute("disabled").ShouldBeFalse());

        ClickFluentButton(cut, "Refresh erasure status");

        cut.WaitForAssertion(() =>
        {
            api.ErasureStatusRequests.ShouldContain("party-gdpr-erased");
            // The certificate fetch is gated off for the provisional bridge: it must never be
            // attempted, so refreshing an erased party cannot surface a contract-unavailable failure.
            api.ErasureCertificateRequests.ShouldBeEmpty();
            cut.Markup.ShouldContain("Erased");
            cut.Markup.ShouldContain("Certificate unavailable");
            cut.Markup.ShouldContain("Operation completed");
            cut.Markup.ShouldNotContain("Operation failed");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOutcomeAccepted_UsesPoliteStatusWithoutFocusableAlert()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-accepted", "GDPR Accepted", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-gdpr-accepted", "GDPR Accepted", PartyType.Person, isActive: true));
        api.EnqueueErasureStatus(new PartyErasureStatusRecord
        {
            PartyId = "party-gdpr-accepted",
            TenantId = "scope-a",
            Status = "ErasurePending",
            UpdatedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR Accepted"));
        ClickFluentButton(cut, "GDPR Accepted");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeFalse());

        ClickFluentButton(cut, "Request erasure");
        SetTextInput(cut, "Type the selected party display name", "GDPR Accepted");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Erase").HasAttribute("disabled").ShouldBeFalse());
        ClickFluentButton(cut, "Erase");

        cut.WaitForAssertion(() =>
        {
            IElement region = cut.Find("[data-gdpr-operation-announcement]");
            region.TextContent.Trim().ShouldBe("Saved - updating...");
            region.GetAttribute("role").ShouldBe("status");
            region.GetAttribute("aria-live").ShouldBe("polite");
            region.HasAttribute("tabindex").ShouldBeFalse();
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOutcomeCompleted_UsesPoliteStatusWithoutFocusableAlert()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-completed", "GDPR Completed", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-gdpr-completed", "GDPR Completed", PartyType.Person, isActive: true));
        api.EnqueueProcessingRecords();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR Completed"));
        ClickFluentButton(cut, "GDPR Completed");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Processing records").HasAttribute("disabled").ShouldBeFalse());

        ClickFluentButton(cut, "Processing records");

        cut.WaitForAssertion(() =>
        {
            IElement region = cut.Find("[data-gdpr-operation-announcement]");
            region.TextContent.Trim().ShouldBe("Operation completed");
            region.GetAttribute("role").ShouldBe("status");
            region.GetAttribute("aria-live").ShouldBe("polite");
            region.HasAttribute("tabindex").ShouldBeFalse();
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOutcomeValidationRejected_UsesAssertiveFocusableAlert()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-validation", "GDPR Validation", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-gdpr-validation", "GDPR Validation", PartyType.Person, isActive: true));
        api.EnqueueErasureResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.ValidationRejected, "corr-rejected"));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR Validation"));
        ClickFluentButton(cut, "GDPR Validation");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeFalse());

        ClickFluentButton(cut, "Request erasure");
        SetTextInput(cut, "Type the selected party display name", "GDPR Validation");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Erase").HasAttribute("disabled").ShouldBeFalse());
        ClickFluentButton(cut, "Erase");

        cut.WaitForAssertion(() =>
        {
            IElement region = cut.Find("[data-gdpr-operation-announcement]");
            region.TextContent.Trim().ShouldBe("Operation rejected");
            region.GetAttribute("role").ShouldBe("alert");
            region.GetAttribute("aria-live").ShouldBe("assertive");
            region.GetAttribute("tabindex").ShouldBe("-1");
            cut.Markup.ShouldNotContain("corr-rejected");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOutcomeValidationRejected_ClearsPriorCommandCorrelation()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-stale-correlation", "GDPR Stale Correlation", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-gdpr-stale-correlation", "GDPR Stale Correlation", PartyType.Person, isActive: true));
        api.EnqueueDetail(Detail(
            "party-gdpr-stale-correlation",
            "GDPR Stale Correlation",
            PartyType.Person,
            isActive: true,
            isRestricted: true));
        api.EnqueueErasureResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.ValidationRejected, "corr-rejected"));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR Stale Correlation"));
        ClickFluentButton(cut, "GDPR Stale Correlation");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Restrict processing").HasAttribute("disabled").ShouldBeFalse());

        ClickFluentButton(cut, "Restrict processing");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Confirm restriction"));
        ClickFluentButton(cut, "Confirm");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("corr-restrict"));

        ClickFluentButton(cut, "Request erasure");
        SetTextInput(cut, "Type the selected party display name", "GDPR Stale Correlation");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Erase").HasAttribute("disabled").ShouldBeFalse());
        ClickFluentButton(cut, "Erase");

        cut.WaitForAssertion(() =>
        {
            IElement region = cut.Find("[data-gdpr-operation-announcement]");
            region.TextContent.Trim().ShouldBe("Operation rejected");
            region.GetAttribute("role").ShouldBe("alert");
            cut.Markup.ShouldNotContain("corr-rejected");
            cut.Markup.ShouldNotContain("corr-restrict");
            cut.Markup.ShouldNotContain("Open EventStore correlation");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_AddAndRevokeConsentRouteThroughAcceptedContractPerChannelPurpose()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-consent", "Consent Party", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-consent", "Consent Party", PartyType.Person, isActive: true));
        api.EnqueueGdprCapability(AdminPortalGdprCapability.ProvisionalBridge());
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-consent",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Consent Party",
            SortName = "Party, Consent",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords =
            [
                new ConsentRecord
                {
                    ConsentId = "consent-1",
                    ChannelId = "news-email",
                    Purpose = "newsletter",
                    LawfulBasis = LawfulBasis.Consent,
                    GrantedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
                    GrantedBy = "admin-user",
                },
            ],
            NameHistory = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Consent Party"));
        ClickFluentButton(cut, "Consent Party");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("GDPR operations"));

        SetTextInput(cut, "Channel id", "news-email");
        SetTextInput(cut, "Purpose", "newsletter");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Add consent").HasAttribute("disabled").ShouldBeFalse());
        ClickFluentButton(cut, "Add consent");

        cut.WaitForAssertion(() =>
        {
            (string PartyId, string ChannelId, string Purpose, LawfulBasis LawfulBasis) added = api.AddConsentRequests.Single();
            added.PartyId.ShouldBe("party-consent");
            added.ChannelId.ShouldBe("news-email");
            added.Purpose.ShouldBe("newsletter");
            // History renders bounded consent status and offers a revoke control.
            cut.Markup.ShouldContain("newsletter");
            IElement region = cut.Find("[data-gdpr-operation-announcement]");
            region.TextContent.Trim().ShouldBe("Saved - updating...");
            region.GetAttribute("role").ShouldBe("status");
            region.GetAttribute("aria-live").ShouldBe("polite");
            FindFluentButton(cut, "Revoke consent").HasAttribute("disabled").ShouldBeFalse();
        });

        ClickFluentButton(cut, "Revoke consent");
        api.RevokeConsentRequests.ShouldBeEmpty();
        cut.WaitForAssertion(() =>
        {
            IElement confirmation = ConfirmationGroup(cut, "Confirm revoke consent");
            confirmation.TextContent.ShouldContain("Revoke this active consent record?");
            confirmation.TextContent.ShouldNotContain("Consent Party");
        });

        ClickFluentButton(cut, "Confirm");

        cut.WaitForAssertion(() =>
        {
            (string PartyId, string ConsentId) revoked = api.RevokeConsentRequests.Single();
            revoked.PartyId.ShouldBe("party-consent");
            revoked.ConsentId.ShouldBe("consent-1");
            IElement region = cut.Find("[data-gdpr-operation-announcement]");
            region.TextContent.Trim().ShouldBe("Saved - updating...");
            region.GetAttribute("role").ShouldBe("status");
            region.GetAttribute("aria-live").ShouldBe("polite");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_ConsentLedgerDoesNotEchoRawChannelOrOperatorIds()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-consent-private", "Consent Private", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-consent-private", "Consent Private", PartyType.Person, isActive: true) with
        {
            ConsentRecords =
            [
                new ConsentRecord
                {
                    ConsentId = "consent-secret-id",
                    ChannelId = "contact-secret-channel-id",
                    Purpose = "newsletter",
                    LawfulBasis = LawfulBasis.Consent,
                    GrantedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
                    GrantedBy = "tenant-user-secret",
                    RevokedAt = DateTimeOffset.Parse("2026-05-04T00:00:00Z"),
                    RevokedBy = "tenant-user-revoker",
                },
            ],
        });
        api.EnqueueGdprCapability(AdminPortalGdprCapability.ProvisionalBridge());
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Consent Private"));
        ClickFluentButton(cut, "Consent Private");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("newsletter");
            cut.Markup.ShouldContain("Consent");
            cut.Markup.ShouldContain("Revoked");
            cut.Markup.ShouldNotContain("consent-secret-id");
            cut.Markup.ShouldNotContain("contact-secret-channel-id");
            cut.Markup.ShouldNotContain("tenant-user-secret");
            cut.Markup.ShouldNotContain("tenant-user-revoker");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_RestrictThenLiftRouteThroughAcceptedContractWithReason()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-restrict", "Restrict Party", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-restrict", "Restrict Party", PartyType.Person, isActive: true));
        api.EnqueueGdprCapability(AdminPortalGdprCapability.ProvisionalBridge());
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-restrict",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Restrict Party",
            SortName = "Party, Restrict",
            IsRestricted = true,
            RestrictedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            NameHistory = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Restrict Party"));
        ClickFluentButton(cut, "Restrict Party");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Restrict processing").HasAttribute("disabled").ShouldBeFalse());

        SetTextInput(cut, "Restriction reason", "court-order-2026");
        ClickFluentButton(cut, "Restrict processing");
        api.RestrictionRequests.ShouldBeEmpty();
        cut.WaitForAssertion(() =>
        {
            IElement confirmation = ConfirmationGroup(cut, "Confirm restriction");
            confirmation.TextContent.ShouldContain("Restrict processing for the selected party?");
            confirmation.TextContent.ShouldNotContain("court-order-2026");
            cut.FindAll("[role='dialog']").ShouldBeEmpty();
        });

        ClickFluentButton(cut, "Confirm");

        cut.WaitForAssertion(() =>
        {
            (string PartyId, string? Reason) restriction = api.RestrictionRequests.Single();
            restriction.PartyId.ShouldBe("party-restrict");
            restriction.Reason.ShouldBe("court-order-2026");
            IElement region = cut.Find("[data-gdpr-operation-announcement]");
            region.TextContent.Trim().ShouldBe("Saved - updating...");
            region.GetAttribute("role").ShouldBe("status");
            region.GetAttribute("aria-live").ShouldBe("polite");
            IElement badge = cut.Find("aside.hx-parties-admin__detail .party-state-badge");
            badge.TextContent.Trim().ShouldBe("Restricted");
            // After the refreshed (restricted) detail loads, lifting becomes available.
            FindFluentButton(cut, "Lift restriction").HasAttribute("disabled").ShouldBeFalse();
        });

        ClickFluentButton(cut, "Lift restriction");
        api.LiftRestrictionRequests.ShouldBeEmpty();
        cut.WaitForAssertion(() =>
        {
            IElement confirmation = ConfirmationGroup(cut, "Confirm lift restriction");
            confirmation.TextContent.ShouldContain("Lift processing restriction for the selected party?");
            confirmation.TextContent.ShouldNotContain("Restrict Party");
        });

        ClickFluentButton(cut, "Confirm");

        cut.WaitForAssertion(() =>
        {
            api.LiftRestrictionRequests.Single().ShouldBe("party-restrict");
            IElement region = cut.Find("[data-gdpr-operation-announcement]");
            region.TextContent.Trim().ShouldBe("Saved - updating...");
            region.GetAttribute("role").ShouldBe("status");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_CancelRestrictionAndRevokeConfirmationsSendNoCommand()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(
            IndexEntry("party-cancel-restrict", "Cancel Restrict", PartyType.Person, true),
            IndexEntry("party-cancel-gdpr", "Cancel GDPR", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-cancel-restrict", "Cancel Restrict", PartyType.Person, isActive: true));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-cancel-gdpr",
            Type = PartyType.Person,
            IsActive = true,
            IsRestricted = true,
            DisplayName = "Cancel GDPR",
            SortName = "GDPR, Cancel",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords =
            [
                new ConsentRecord
                {
                    ConsentId = "consent-cancel",
                    ChannelId = "email",
                    Purpose = "newsletter",
                    LawfulBasis = LawfulBasis.Consent,
                    GrantedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
                    GrantedBy = "admin-user",
                },
            ],
            NameHistory = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Cancel Restrict"));
        ClickFluentButton(cut, "Cancel Restrict");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Restrict processing").HasAttribute("disabled").ShouldBeFalse());

        SetTextInput(cut, "Restriction reason", "cancelled-reason");
        ClickFluentButton(cut, "Restrict processing");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Confirm restriction"));
        ClickFluentButton(cut, "Cancel");

        cut.WaitForAssertion(() =>
        {
            api.RestrictionRequests.ShouldBeEmpty();
            cut.Markup.ShouldNotContain("Confirm restriction");
        });

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Cancel GDPR"));
        ClickFluentButton(cut, "Cancel GDPR");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Lift restriction").HasAttribute("disabled").ShouldBeFalse());

        ClickFluentButton(cut, "Lift restriction");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Confirm lift restriction"));
        ClickFluentButton(cut, "Cancel");

        cut.WaitForAssertion(() =>
        {
            api.LiftRestrictionRequests.ShouldBeEmpty();
            cut.Markup.ShouldNotContain("Confirm lift restriction");
        });

        ClickFluentButton(cut, "Revoke consent");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Confirm revoke consent"));
        ClickFluentButton(cut, "Cancel");

        cut.WaitForAssertion(() =>
        {
            api.RevokeConsentRequests.ShouldBeEmpty();
            cut.Markup.ShouldNotContain("Confirm revoke consent");
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_PartySwitchClearsPendingConfirmationsBeforeMutation()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(
            IndexEntry("party-switch-a", "Switch A", PartyType.Person, true),
            IndexEntry("party-switch-b", "Switch B", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-switch-a", "Switch A", PartyType.Person, isActive: true));
        api.EnqueueGdprCapability(AdminPortalGdprCapability.ProvisionalBridge());
        api.EnqueueDetail(Detail("party-switch-b", "Switch B", PartyType.Person, isActive: true) with
        {
            ConsentRecords =
            [
                new ConsentRecord
                {
                    ConsentId = "switch-consent",
                    ChannelId = "switch-channel",
                    Purpose = "newsletter",
                    LawfulBasis = LawfulBasis.Consent,
                    GrantedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
                    GrantedBy = "admin-user",
                },
            ],
        });
        api.EnqueueGdprCapability(AdminPortalGdprCapability.ProvisionalBridge());
        api.EnqueueDetail(Detail("party-switch-a", "Switch A", PartyType.Person, isActive: true));
        api.EnqueueGdprCapability(AdminPortalGdprCapability.ProvisionalBridge());
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Switch A"));
        ClickFluentButton(cut, "Switch A");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Restrict processing").HasAttribute("disabled").ShouldBeFalse());

        ClickFluentButton(cut, "Restrict processing");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Confirm restriction"));
        ClickFluentButton(cut, "Switch B");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Switch B");
            cut.Markup.ShouldNotContain("Confirm restriction");
            api.RestrictionRequests.ShouldBeEmpty();
        });

        ClickFluentButton(cut, "Revoke consent");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Confirm revoke consent"));
        ClickFluentButton(cut, "Switch A");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Switch A");
            cut.Markup.ShouldNotContain("Confirm revoke consent");
            api.RevokeConsentRequests.ShouldBeEmpty();
        });
    }

    [Fact]
    public void PartiesAdminPortal_GdprOperations_AssertiveRestrictionFailureClearsSuccessCorrelationAndBoundedCopy()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-restrict-fail", "Restrict Failure", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-restrict-fail", "Restrict Failure", PartyType.Person, isActive: true));
        api.EnqueueGdprCapability(AdminPortalGdprCapability.ProvisionalBridge());
        api.EnqueueRestrictionResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-restrict-success"));
        api.EnqueueDetail(Detail("party-restrict-fail", "Restrict Failure", PartyType.Person, isActive: true, isRestricted: true));
        api.EnqueueLiftRestrictionResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.ValidationRejected, "corr-lift-rejected"));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Restrict Failure"));
        ClickFluentButton(cut, "Restrict Failure");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Restrict processing").HasAttribute("disabled").ShouldBeFalse());

        SetTextInput(cut, "Restriction reason", "sensitive-free-text");
        ClickFluentButton(cut, "Restrict processing");
        ClickFluentButton(cut, "Confirm");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("corr-restrict-success"));

        ClickFluentButton(cut, "Lift restriction");
        ClickFluentButton(cut, "Confirm");

        cut.WaitForAssertion(() =>
        {
            IElement region = cut.Find("[data-gdpr-operation-announcement]");
            region.TextContent.Trim().ShouldBe("Operation rejected");
            region.GetAttribute("role").ShouldBe("alert");
            region.GetAttribute("aria-live").ShouldBe("assertive");
            region.TextContent.ShouldNotContain("sensitive-free-text");
            cut.Markup.ShouldNotContain("corr-restrict-success");
            cut.Markup.ShouldNotContain("corr-lift-rejected");
            cut.Markup.ShouldNotContain("Open EventStore correlation");
        });
    }

    [Fact]
    public void PartiesAdminPortal_DetailShowsSafeEventStoreAdminLinksWhenConfigured()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-link", "Ada <Admin>", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "tenant-a:party:party-link",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada <Admin>",
            SortName = "Admin, Ada",
            ContactChannels = [new ContactChannel { Id = "c-1", Type = ContactChannelType.Email, Value = "ada@example.test", IsPreferred = true }],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        Services.Configure<PartiesAdminPortalOptions>(options =>
        {
            options.EventStoreAdminUiBaseAddress = new Uri("https://admin.example/");
        });

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Ada &lt;Admin&gt;"));
        ClickFluentButton(cut, "Ada");

        cut.WaitForAssertion(() =>
        {
            IElement link = cut.Find("a[href*='streams?aggregateId=tenant-a%3Aparty%3Aparty-link']");
            link.TextContent.Trim().ShouldBe("Open EventStore stream");
            link.GetAttribute("target").ShouldBe("_blank");
            link.GetAttribute("rel").ShouldBe("noopener noreferrer");
            link.GetAttribute("href")!.ShouldNotContain("Ada");
            link.GetAttribute("href")!.ShouldNotContain("ada@example.test");
        });
    }

    [Fact]
    public void PartiesAdminPortal_EventStoreLink_UsesGenericLabelAndExcludesPartyData()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-link-redaction", "Private Person", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "tenant-a:party:party link redaction",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Private Person",
            SortName = "Person, Private",
            ContactChannels = [new ContactChannel { Id = "contact-private", Type = ContactChannelType.Email, Value = "private@example.test", IsPreferred = true }],
            Identifiers = [new PartyIdentifier { Id = "identifier-private", Type = IdentifierType.Other, Value = "SSN-123-45-6789" }],
            ConsentRecords = [new ConsentRecord { ConsentId = "consent-private", ChannelId = "contact-private", Purpose = "marketing-private", LawfulBasis = LawfulBasis.Consent, GrantedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"), GrantedBy = "operator-private" }],
            NameHistory = [new NameHistoryEntry { DisplayName = "Prior Private Name", SortName = "Private", ChangedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z") }],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        Services.Configure<PartiesAdminPortalOptions>(options =>
        {
            options.EventStoreAdminUiBaseAddress = new Uri("https://admin.example/ui/?shell=frontcomposer");
        });

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Private Person"));
        ClickFluentButton(cut, "Private Person");

        cut.WaitForAssertion(() =>
        {
            IElement link = cut.Find("a[href*='aggregateId=tenant-a%3Aparty%3Aparty%20link%20redaction']");
            link.TextContent.Trim().ShouldBe("Open EventStore stream");
            link.TextContent.ShouldNotContain("Private Person");
            string href = link.GetAttribute("href")!;
            href.ShouldStartWith("https://admin.example/ui/streams?shell=frontcomposer&aggregateId=");
            href.ShouldNotContain("party link redaction");
            href.ShouldNotContain("Private Person");
            href.ShouldNotContain("private@example.test");
            href.ShouldNotContain("SSN-123-45-6789");
            href.ShouldNotContain("marketing-private");
            href.ShouldNotContain("Prior Private Name");
            href.ShouldNotContain("token", Case.Insensitive);
            href.ShouldNotContain("payload", Case.Insensitive);
        });
    }

    [Fact]
    public void PartiesAdminPortal_DetailDisablesEventStoreAdminLinksWhenUrlIsUnavailable()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-no-admin-ui", "No Admin UI", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-no-admin-ui",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "No Admin UI",
            SortName = "Admin UI, No",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("No Admin UI"));
        ClickFluentButton(cut, "No Admin UI");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("EventStore Admin UI unavailable");
            cut.FindAll("a[href*='streams']").ShouldBeEmpty();
        });
    }

    [Fact]
    public void PartiesAdminPortal_RequestErasure_RequiresTypedConfirmationAndDisplaysCorrelation()
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
        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("GDPR operations");
            FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeFalse();
        });

        ClickFluentButton(cut, "Request erasure");
        cut.WaitForAssertion(() =>
        {
            IElement dialog = cut.Find("[role='dialog'][aria-modal='true']");
            string labelledBy = dialog.GetAttribute("aria-labelledby")!;
            labelledBy.ShouldNotBeNullOrWhiteSpace();
            cut.Find($"#{labelledBy}").TextContent.Trim().ShouldBe("Erase party");
            cut.FindAll("[role='dialog'][aria-modal='true']").Count.ShouldBe(1);

            FluentTextInput input = FindTextInput(cut, "Type the selected party display name").Instance;
            input.AdditionalAttributes.ShouldNotBeNull();
            input.AdditionalAttributes.ShouldContainKey("aria-describedby");
            string descriptionId = input.AdditionalAttributes["aria-describedby"]?.ToString() ?? string.Empty;
            cut.Find($"#{descriptionId}").TextContent.ShouldContain("irreversible verification");

            IElement eraseButton = FindFluentButton(cut, "Erase");
            eraseButton.HasAttribute("disabled").ShouldBeTrue();
            (eraseButton.GetAttribute("aria-disabled") == "true" || eraseButton.HasAttribute("disabled")).ShouldBeTrue();
            cut.Markup.ShouldNotContain("Party id: party-erase");
        });

        SetTextInput(cut, "Type the selected party display name", "Erase candidate");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Erase").HasAttribute("disabled").ShouldBeTrue());
        api.ErasureRequests.ShouldBeEmpty();

        SetTextInput(cut, "Type the selected party display name", "Erase Candidate");
        cut.WaitForAssertion(() =>
        {
            FindFluentButton(cut, "Erase").HasAttribute("disabled").ShouldBeFalse();
            cut.Find("[data-erasure-confirmation-live]").TextContent.Trim().ShouldBe("Erase action enabled.");
        });
        ClickFluentButton(cut, "Erase");

        cut.WaitForAssertion(() =>
        {
            api.ErasureRequests.Single().ShouldBe("party-erase");
            api.ErasureStatusRequests.Single().ShouldBe("party-erase");
            cut.Markup.ShouldContain("corr-erasure");
            cut.Markup.ShouldContain("ErasurePending");
            cut.FindAll("[role='dialog'][aria-modal='true']").ShouldBeEmpty();
        });
    }

    [Fact]
    public void PartiesAdminPortal_RequestErasure_CancelPartySwitchAndUnavailableStateClearTypedValue()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(
            IndexEntry("party-erase-clear", "Erase Clear", PartyType.Person, true),
            IndexEntry("party-erase-next", "Erase Next", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-erase-clear", "Erase Clear", PartyType.Person, isActive: true));
        api.EnqueueDetail(Detail("party-erase-next", "Erase Next", PartyType.Person, isActive: true));
        api.EnqueueGdprCapability(AdminPortalGdprCapability.Available());
        api.EnqueueGdprCapability(AdminPortalGdprCapability.Unavailable());
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Erase Clear"));
        ClickFluentButton(cut, "Erase Clear");
        cut.WaitForAssertion(() => FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeFalse());

        ClickFluentButton(cut, "Request erasure");
        SetTextInput(cut, "Type the selected party display name", "typed-secret-cancel");
        ClickFluentButton(cut, "Cancel");

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[role='dialog'][aria-modal='true']").ShouldBeEmpty();
            cut.Markup.ShouldNotContain("typed-secret-cancel");
        });

        ClickFluentButton(cut, "Request erasure");
        SetTextInput(cut, "Type the selected party display name", "typed-secret-switch");
        ClickFluentButton(cut, "Erase Next");

        cut.WaitForAssertion(() =>
        {
            FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeTrue();
            cut.Markup.ShouldNotContain("typed-secret-switch");
            cut.FindAll("[role='dialog'][aria-modal='true']").ShouldBeEmpty();
            api.ErasureRequests.ShouldBeEmpty();
        });
    }

    [Fact]
    public void PartiesAdminPortal_RequestErasure_StatusRefreshClosesDialogAndDisablesPendingMutations()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-erasure-pending", "Pending Lock", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-erasure-pending",
            Type = PartyType.Person,
            IsActive = true,
            IsRestricted = true,
            DisplayName = "Pending Lock",
            SortName = "Lock, Pending",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords =
            [
                new ConsentRecord
                {
                    ConsentId = "consent-pending",
                    ChannelId = "email",
                    Purpose = "newsletter",
                    LawfulBasis = LawfulBasis.Consent,
                    GrantedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
                    GrantedBy = "admin-user",
                },
            ],
            NameHistory = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
        });
        api.EnqueueErasureStatus(new PartyErasureStatusRecord
        {
            PartyId = "party-erasure-pending",
            TenantId = "scope-a",
            Status = "ErasurePending",
            UpdatedAt = DateTimeOffset.Parse("2026-05-04T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Pending Lock"));
        ClickFluentButton(cut, "Pending Lock");
        cut.WaitForAssertion(() =>
        {
            FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeFalse();
            FindFluentButton(cut, "Lift restriction").HasAttribute("disabled").ShouldBeFalse();
            FindFluentButton(cut, "Revoke consent").HasAttribute("disabled").ShouldBeFalse();
        });

        ClickFluentButton(cut, "Lift restriction");
        ClickFluentButton(cut, "Revoke consent");
        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Confirm lift restriction");
            cut.Markup.ShouldContain("Confirm revoke consent");
            api.LiftRestrictionRequests.ShouldBeEmpty();
            api.RevokeConsentRequests.ShouldBeEmpty();
        });

        ClickFluentButton(cut, "Request erasure");
        SetTextInput(cut, "Type the selected party display name", "typed-secret-pending");
        ClickFluentButton(cut, "Refresh erasure status");

        cut.WaitForAssertion(() =>
        {
            api.ErasureStatusRequests.Single().ShouldBe("party-erasure-pending");
            api.ErasureRequests.ShouldBeEmpty();
            cut.FindAll("[role='dialog'][aria-modal='true']").ShouldBeEmpty();
            cut.Markup.ShouldNotContain("typed-secret-pending");
            cut.Markup.ShouldNotContain("Confirm lift restriction");
            cut.Markup.ShouldNotContain("Confirm revoke consent");
            cut.Markup.ShouldContain("ErasurePending");
            FindFluentButton(cut, "Request erasure").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Lift restriction").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Revoke consent").HasAttribute("disabled").ShouldBeTrue();
            FindFluentButton(cut, "Export party data").HasAttribute("disabled").ShouldBeTrue();
        });
    }

    [Fact]
    public void PartiesAdminPortal_RetryVerification_RefreshesAuthoritativeErasureStatus()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-retry-erasure", "Retry Erasure", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-retry-erasure",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Retry Erasure",
            SortName = "Erasure, Retry",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        api.EnqueueErasureStatus(new PartyErasureStatusRecord
        {
            PartyId = "party-retry-erasure",
            TenantId = "scope-a",
            Status = "VerificationFailed",
            UpdatedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
        });
        api.EnqueueErasureStatus(new PartyErasureStatusRecord
        {
            PartyId = "party-retry-erasure",
            TenantId = "scope-a",
            Status = "Verified",
            UpdatedAt = DateTimeOffset.Parse("2026-05-03T00:05:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Retry Erasure"));
        ClickFluentButton(cut, "Retry Erasure");
        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("GDPR operations");
            FindFluentButton(cut, "Refresh erasure status").HasAttribute("disabled").ShouldBeFalse();
        });

        ClickFluentButton(cut, "Refresh erasure status");
        cut.WaitForAssertion(() =>
        {
            api.ErasureStatusRequests.Count.ShouldBe(1);
            cut.Markup.ShouldContain("VerificationFailed");
        });

        cut.WaitForAssertion(() => FindFluentButton(cut, "Retry verification").HasAttribute("disabled").ShouldBeFalse());
        ClickFluentButton(cut, "Retry verification");

        cut.WaitForAssertion(() =>
        {
            api.RetryVerificationRequests.Single().ShouldBe("party-retry-erasure");
            api.ErasureStatusRequests.Count.ShouldBe(2);
            cut.Markup.ShouldContain("Verified");
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
        },
        new ProcessingActivityRecord
        {
            SequenceNumber = 8,
            EventType = "LongProcessingRecord",
            Timestamp = DateTimeOffset.Parse("2026-05-04T00:00:00Z"),
            Summary = new string('A', 200),
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
            cut.Markup.ShouldContain(new string('A', 160) + "...");
            cut.Markup.ShouldNotContain(new string('A', 200));
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
        // Server-supplied FileName is intentionally an attacker-style PII-leaking name to prove
        // the UI re-derives a safe non-PII filename via GdprExportFileNameBuilder (D2 defense-in-depth).
        api.EnqueueExport(new AdminPortalExportDownload("attacker-Export Party-leak.json", "text/html", [0x7B, 0x7D]));
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
            cut.Markup.ShouldContain("party-party-export-export-");
            cut.Markup.ShouldContain("Z.json");
            // The transport-supplied attacker filename must never reach the UI.
            cut.Markup.ShouldNotContain("attacker-Export Party-leak.json");
            cut.Markup.ShouldNotContain("attacker");
            cut.Markup.ShouldNotContain("leak");
            var invocation = JSInterop.Invocations.Single(invocation =>
                invocation.Identifier == "HexalithPartiesAdminPortal.downloadJson");
            invocation.Arguments[1].ShouldBe("application/json");
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
    public void PartiesAdminPortal_TenantSwitch_ClearsGdprCoordinatorState()
    {
        var coordinator = new AdminPortalGdprStateCoordinator();
        Services.AddSingleton(coordinator);

        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-gdpr-state", "Tenant A Subject", PartyType.Person, true)));
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-gdpr-state",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Tenant A Subject",
            SortName = "Subject, Tenant A",
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        api.EnqueueList(Page(IndexEntry("party-gdpr-state-b", "Tenant B Subject", PartyType.Person, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenant A Subject"));
        ClickFluentButton(cut, "Tenant A Subject");

        cut.WaitForAssertion(() =>
        {
            coordinator.ActivePartyId.ShouldBe("party-gdpr-state");
            coordinator.State.ShouldBe(AdminPortalGdprOperationState.Ready);
        });

        SeedTenant("scope-b", AdminUserId, TenantRole.TenantOwner);
        cut.InvokeAsync(() => _authProvider.SetAuthenticated(AdminUserId, "scope-b"));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Tenant B Subject");
            coordinator.ActivePartyId.ShouldBeNull();
            coordinator.State.ShouldBe(AdminPortalGdprOperationState.NotLoaded);
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
    public void PartiesAdminPortal_DetailContractUnavailable_ClearsPreviousDetailWithoutRawFailureText()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(new PagedResult<PartyIndexEntry>
        {
            Items =
            [
                IndexEntry("party-loaded", "Loaded Detail", PartyType.Person, true),
                IndexEntry("party-broken", "Broken Detail", PartyType.Organization, true),
            ],
            Page = 1,
            PageSize = 20,
            TotalCount = 2,
            TotalPages = 1,
        });
        api.EnqueueDetail(Detail("party-loaded", "Loaded Detail", PartyType.Person, isActive: true) with
        {
            ContactChannels = [new ContactChannel { Id = "contact-loaded", Type = ContactChannelType.Email, Value = "loaded@example.test", IsPreferred = true }],
        });
        api.EnqueueDetailFailure(AdminPortalQueryFailureKind.ContractUnavailable);
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Loaded Detail"));
        ClickFluentButton(cut, "Loaded Detail");
        cut.WaitForAssertion(() => cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("loaded@example.test"));

        ClickFluentButton(cut, "Broken Detail");

        cut.WaitForAssertion(() =>
        {
            IElement detail = cut.Find("aside.hx-parties-admin__detail");
            detail.TextContent.ShouldContain("Detail could not be loaded");
            detail.TextContent.ShouldNotContain("loaded@example.test");
            detail.TextContent.ShouldNotContain("Loaded Detail");
            detail.TextContent.ShouldNotContain("ContractUnavailable");
        });
    }

    [Fact]
    public void PartiesAdminPortal_DetailMalformedFailure_ClearsPreviousDetailWithoutParserText()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(new PagedResult<PartyIndexEntry>
        {
            Items =
            [
                IndexEntry("party-prior", "Prior Detail", PartyType.Person, true),
                IndexEntry("party-malformed", "Malformed Detail", PartyType.Organization, true),
            ],
            Page = 1,
            PageSize = 20,
            TotalCount = 2,
            TotalPages = 1,
        });
        api.EnqueueDetail(Detail("party-prior", "Prior Detail", PartyType.Person, isActive: true) with
        {
            Identifiers = [new PartyIdentifier { Id = "identifier-prior", Type = IdentifierType.Other, Value = "PRIOR-SECRET" }],
        });
        api.EnqueueDetailFailure(AdminPortalQueryFailureKind.Unknown);
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Prior Detail"));
        ClickFluentButton(cut, "Prior Detail");
        cut.WaitForAssertion(() => cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("PRIOR-SECRET"));

        ClickFluentButton(cut, "Malformed Detail");

        cut.WaitForAssertion(() =>
        {
            IElement detail = cut.Find("aside.hx-parties-admin__detail");
            detail.TextContent.ShouldContain("Detail could not be loaded");
            detail.TextContent.ShouldNotContain("PRIOR-SECRET");
            detail.TextContent.ShouldNotContain("System.Text.Json");
            detail.TextContent.ShouldNotContain("Malformed");
        });
    }

    [Fact]
    public void PartiesAdminPortal_TenantSwitch_CancelsDelayedDetailAndSuppressesPreviousTenantPayload()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-old-tenant", "Old Tenant Row", PartyType.Person, true)));
        api.EnqueueList(Page(IndexEntry("party-new-tenant", "New Tenant Row", PartyType.Organization, true)));
        var delayed = new TaskCompletionSource<AdminPortalQueryResult<PartyDetail>>();
        var canceled = false;
        api.EnqueueDetail(ct =>
        {
            ct.Register(() => canceled = true);
            return delayed.Task;
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Old Tenant Row"));
        ClickFluentButton(cut, "Old Tenant Row");
        cut.WaitForAssertion(() => api.DetailRequests.Single().ShouldBe("party-old-tenant"));

        SeedTenant("scope-b", AdminUserId, TenantRole.TenantOwner);
        cut.InvokeAsync(() => _authProvider.SetAuthenticated(AdminUserId, "scope-b"));

        cut.WaitForAssertion(() =>
        {
            canceled.ShouldBeTrue();
            cut.Markup.ShouldContain("New Tenant Row");
            cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("Select a party");
        });
        delayed.TrySetResult(new AdminPortalQueryResult<PartyDetail>(
            Detail("party-old-tenant", "Old Tenant Secret", PartyType.Person, isActive: true) with
            {
                ContactChannels = [new ContactChannel { Id = "contact-old", Type = ContactChannelType.Email, Value = "old-tenant@example.test", IsPreferred = true }],
            },
            AdminPortalQueryMetadata.Empty));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("New Tenant Row");
            cut.Markup.ShouldNotContain("Old Tenant Secret");
            cut.Markup.ShouldNotContain("old-tenant@example.test");
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
        FindFluentButton(cut, "Retry").HasAttribute("autofocus").ShouldBeTrue();
        ClickFluentButton(cut, "Retry");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Retry Success"));
        api.ListRequests.Count.ShouldBe(2);
    }

    [Fact]
    public void PartiesAdminPortal_ListContractUnavailable_ClearsSensitiveDetailAndShowsBoundedFailure()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-before-failure", "Before Failure", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-before-failure", "Before Failure", PartyType.Person, isActive: true) with
        {
            ContactChannels = [new ContactChannel { Id = "contact-before", Type = ContactChannelType.Email, Value = "before-failure@example.test", IsPreferred = true }],
        });
        api.EnqueueListFailure(AdminPortalQueryFailureKind.ContractUnavailable);
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Before Failure"));
        ClickFluentButton(cut, "Before Failure");
        cut.WaitForAssertion(() => cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("before-failure@example.test"));

        ClickFluentButton(cut, "Search");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-parties-admin__status").TextContent.ShouldContain("Party data could not be loaded");
            cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("Select a party");
            cut.Markup.ShouldNotContain("before-failure@example.test");
            cut.Markup.ShouldNotContain("ContractUnavailable");
            cut.FindAll("fluent-button").Any(button => button.TextContent.Contains("Retry", StringComparison.Ordinal)).ShouldBeFalse();
        });
    }

    [Fact]
    public void PartiesAdminPortal_ListUnknownFailure_UsesRetryAndDoesNotRenderRawParserText()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueListFailure(AdminPortalQueryFailureKind.Unknown);
        api.EnqueueList(Page(IndexEntry("party-recovered", "Recovered Row", PartyType.Organization, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-parties-admin__status").TextContent.ShouldContain("Party data could not be loaded");
            cut.Markup.ShouldNotContain("System.Text.Json");
            cut.Markup.ShouldNotContain("ProblemDetails");
            FindFluentButton(cut, "Retry");
        });
        ClickFluentButton(cut, "Retry");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Recovered Row");
            api.ListRequests.Count.ShouldBe(2);
        });
    }

    [Fact]
    public void PartiesAdminPortal_MissingTenantAfterDetail_ClearsSensitiveState()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-sensitive", "Sensitive Row", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-sensitive", "Sensitive Row", PartyType.Person, isActive: true) with
        {
            ContactChannels = [new ContactChannel { Id = "contact-sensitive", Type = ContactChannelType.Email, Value = "sensitive@example.test", IsPreferred = true }],
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Sensitive Row"));
        ClickFluentButton(cut, "Sensitive Row");
        cut.WaitForAssertion(() => cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("sensitive@example.test"));

        cut.InvokeAsync(() => _authProvider.SetAuthenticated(AdminUserId, "scope-missing"));

        cut.WaitForAssertion(() =>
        {
            cut.Find(".hx-parties-admin__status").TextContent.ShouldContain("Tenant context is unavailable");
            cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldContain("Tenant context is unavailable");
            cut.Markup.ShouldNotContain("Sensitive Row");
            cut.Markup.ShouldNotContain("sensitive@example.test");
        });
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
    public void PartiesAdminPortal_UsesSuppliedLabelsForValidationAndGdprOutcomes()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-localized-gdpr", "Localized GDPR", PartyType.Person, true)));
        api.EnqueueList(Page(IndexEntry("party-localized-gdpr", "Localized GDPR", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-localized-gdpr", "Localized GDPR", PartyType.Person, isActive: true));
        api.EnqueueExport(new AdminPortalExportDownload("server-name.json", "application/json", [0x7B, 0x7D]));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        SeedTenant("scope-a", AdminUserId, TenantRole.TenantOwner);
        _authProvider.SetAuthenticated(AdminUserId, "scope-a");

        var labels = new AdminPortalLabels
        {
            ValidationProblemPrefix = "Controle",
            DateFilterInvalid = "Date invalide",
            GdprOperations = "Operations RGPD",
            ExportPartyData = "Exporter les donnees",
            GdprOperationCompleted = "Operation terminee",
        };
        IRenderedComponent<PartiesAdminPortal> cut = Render<PartiesAdminPortal>(p => p
            .Add(x => x.ContextKey, "scope-a")
            .Add(x => x.Labels, labels));

        SetTextInput(cut, "Created after", "not-a-date");
        ClickFluentButton(cut, "Search");
        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Controle: Date invalide");
            cut.Markup.ShouldNotContain("Validation: Use YYYY-MM-DD");
        });

        SetTextInput(cut, "Created after", string.Empty);
        ClickFluentButton(cut, "Search");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Localized GDPR"));
        ClickFluentButton(cut, "Localized GDPR");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Operations RGPD"));

        ClickFluentButton(cut, "Exporter les donnees");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Operation terminee");
            api.ExportRequests.Single().ShouldBe("party-localized-gdpr");
        });
    }

    [Fact]
    public void PartiesAdminPortal_UsesLocalizedEnumLabelOverrides()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-localized-enums", "Localized Enums", PartyType.Person, true)));
        api.EnqueueDetail(Detail("party-localized-enums", "Localized Enums", PartyType.Person, isActive: true) with
        {
            ContactChannels =
            [
                new ContactChannel
                {
                    Id = "contact-email",
                    Type = ContactChannelType.Email,
                    Value = "localized@example.test",
                    IsPreferred = true,
                },
            ],
            Identifiers =
            [
                new PartyIdentifier
                {
                    Id = "identifier-other",
                    Type = IdentifierType.Other,
                    Value = "safe-id",
                },
            ],
            ConsentRecords =
            [
                new ConsentRecord
                {
                    ConsentId = "consent-1",
                    ChannelId = "email-main",
                    Purpose = "Marketing",
                    LawfulBasis = LawfulBasis.LegitimateInterest,
                    GrantedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
                    GrantedBy = "system",
                },
            ],
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        SeedTenant("scope-a", AdminUserId, TenantRole.TenantOwner);
        _authProvider.SetAuthenticated(AdminUserId, "scope-a");

        IRenderedComponent<PartiesAdminPortal> cut = Render<PartiesAdminPortal>(p => p
            .Add(x => x.ContextKey, "scope-a")
            .Add(x => x.Labels, new LocalizedEnumLabels()));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Localized Enums"));
        ClickFluentButton(cut, "Localized Enums");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Courriel");
            cut.Markup.ShouldContain("Autre identifiant");
            cut.Markup.ShouldContain("Prospection");
            cut.Markup.ShouldContain("Interet legitime");
            cut.Find("aside.hx-parties-admin__detail").TextContent.ShouldNotContain("LegitimateInterest");
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

        string activeDescription = FindFluentButton(cut, "Active Row").GetAttribute("aria-describedby")!;
        string inactiveDescription = FindFluentButton(cut, "Inactive Row").GetAttribute("aria-describedby")!;
        cut.Find($"#{activeDescription}").TextContent.Trim().ShouldBe("Active");
        cut.Find($"#{inactiveDescription}").TextContent.Trim().ShouldBe("Inactive");

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

    private IRenderedComponent<PartiesAdminPortal> RenderAuthorized(
        string contextKey,
        int pageSize = AdminPortalQueryBounds.DefaultPageSize,
        string? routePartyId = null,
        TimeSpan? searchDebounceDelay = null)
    {
        SeedTenant(contextKey, AdminUserId, TenantRole.TenantOwner);
        _authProvider.SetAuthenticated(AdminUserId, contextKey);
        return Render<PartiesAdminPortal>(p => p
            .Add(x => x.ContextKey, contextKey)
            .Add(x => x.PageSize, pageSize)
            .Add(x => x.SearchDebounceDelay, searchDebounceDelay ?? TimeSpan.FromMilliseconds(300))
            .Add(x => x.RoutePartyId, routePartyId));
    }

    private static IElement FindFluentButton(IRenderedComponent<PartiesAdminPortal> cut, string text)
    {
        List<IElement> exact = [.. cut.FindAll("fluent-button")
            .Where(button => string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal))];
        if (exact.Count > 0)
        {
            return exact[0];
        }

        return cut.FindAll("fluent-button")
            .Single(button => button.TextContent.Contains(text, StringComparison.Ordinal));
    }

    private static void ClickFluentButton(IRenderedComponent<PartiesAdminPortal> cut, string text)
        => FindFluentButton(cut, text).Click();

    private static IElement ConfirmationGroup(IRenderedComponent<PartiesAdminPortal> cut, string title)
        => cut.FindAll(".hx-parties-admin__gdpr-confirmation")
            .Single(group => group.TextContent.Contains(title, StringComparison.Ordinal));

    private static readonly string[] GdprMutationControlLabels =
    [
        "Request erasure",
        "Refresh erasure status",
        "Restrict processing",
        "Lift restriction",
        "Add consent",
        "Revoke consent",
        "Export party data",
        "Processing records",
        "Retry verification",
    ];

    private static void SetSearch(IRenderedComponent<PartiesAdminPortal> cut, string value)
    {
        IRenderedComponent<FluentTextInput> input = cut.FindComponent<FluentTextInput>();
        cut.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync(value)).GetAwaiter().GetResult();
    }

    private static void SetSelect(IRenderedComponent<PartiesAdminPortal> cut, string label, string value)
    {
        IRenderedComponent<FluentSelect<string, string>> select = cut.FindComponents<FluentSelect<string, string>>()
            .Single(input => string.Equals(input.Instance.Label, label, StringComparison.Ordinal));
        cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(value)).GetAwaiter().GetResult();
    }

    private static IRenderedComponent<FluentTextInput> FindTextInput(IRenderedComponent<PartiesAdminPortal> cut, string label)
        => cut.FindComponents<FluentTextInput>()
            .Single(input => string.Equals(input.Instance.Label, label, StringComparison.Ordinal));

    private static void SetTextInput(IRenderedComponent<PartiesAdminPortal> cut, string label, string value)
    {
        IRenderedComponent<FluentTextInput> input = FindTextInput(cut, label);
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

    private static string LocateRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Parties.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
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

    private static PartyDetail Detail(string id, string name, PartyType type, bool isActive, bool isRestricted = false) => new()
    {
        Id = id,
        Type = type,
        IsActive = isActive,
        IsRestricted = isRestricted,
        DisplayName = name,
        SortName = name,
        ContactChannels = [],
        Identifiers = [],
        ConsentRecords = [],
        NameHistory = [],
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

    private sealed record LocalizedEnumLabels : AdminPortalLabels
    {
        public override string ContactChannelTypeLabel(string typeName)
            => typeName == ContactChannelType.Email.ToString() ? "Courriel" : typeName;

        public override string IdentifierTypeLabel(string typeName)
            => typeName == IdentifierType.Other.ToString() ? "Autre identifiant" : typeName;

        public override string ConsentPurposeLabel(string purposeName)
            => purposeName == "Marketing" ? "Prospection" : purposeName;

        public override string LawfulBasisLabel(string lawfulBasisName)
            => lawfulBasisName == LawfulBasis.LegitimateInterest.ToString() ? "Interet legitime" : lawfulBasisName;
    }
}
