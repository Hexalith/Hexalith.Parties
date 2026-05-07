using Bunit;

using Hexalith.Parties.AdminPortal.Components;
using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.AdminPortal.Tests.Services;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Parties.AdminPortal.Tests.Components;

public sealed class PartiesAdminPortalComponentTests : BunitContext
{
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
    public void PartiesAdminPortal_FailsClosed_WhenAuthParametersDefaultToFalse()
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
    public void PartiesAdminPortal_SearchAndFilters_UseApprovedApiAndBoundPageSize()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page<PartyIndexEntry>());
        api.EnqueueSearch(Page(new PartySearchResult
        {
            Party = IndexEntry("party-2", "Grace Hopper", PartyType.Person, true),
            Matches = [],
            RelevanceScore = 1.0,
        }), new AdminPortalQueryMetadata(SearchStatus: "local-only", SearchDegradedReason: "rich-search-disabled"));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a", pageSize: 250);

        cut.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(1));
        cut.Find("input[type=\"search\"]").Input("grace@example.test");
        cut.Find("select[aria-label=\"Party type\"]").Change("Person");
        cut.Find("select[aria-label=\"Active state\"]").Change("true");
        cut.Find("button[type=\"submit\"]").Click();

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

        cut.WaitForAssertion(() => cut.Find("tbody button").TextContent.ShouldContain("alert"));
        cut.Find("tbody button").Click();

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
    public void PartiesAdminPortal_UnauthorizedOrTenantChange_ClearsRowsAndIgnoresStaleResponses()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page(IndexEntry("party-4", "Tenant A", PartyType.Organization, true)));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = RenderAuthorized("scope-a");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenant A"));

        cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PartiesAdminPortal.ContextKey)] = "scope-b",
            [nameof(PartiesAdminPortal.IsAuthenticated)] = true,
            [nameof(PartiesAdminPortal.HasTenantContext)] = true,
            [nameof(PartiesAdminPortal.IsAdmin)] = false,
        })));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Administrator access is required");
            cut.Markup.ShouldNotContain("Tenant A");
            cut.FindAll("tbody tr").ShouldBeEmpty();
            cut.Find("button[type='submit']").HasAttribute("disabled").ShouldBeFalse();
        });
    }

    [Fact]
    public void PartiesAdminPortal_UsesSuppliedLabelsForLocalizableShellText()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueList(Page<PartyIndexEntry>());
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<PartiesAdminPortal> cut = Render<PartiesAdminPortal>(p => p
            .Add(x => x.ContextKey, "scope-a")
            .Add(x => x.IsAuthenticated, true)
            .Add(x => x.HasTenantContext, true)
            .Add(x => x.IsAdmin, true)
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
            cut.Find("input[type=\"search\"]").GetAttribute("aria-label").ShouldBe("Rechercher des tiers");
            cut.Find("input[type=\"search\"]").GetAttribute("placeholder").ShouldBe("Nom affiche");
            cut.Markup.ShouldContain("Aucun tiers");
            cut.Markup.ShouldContain("Personne");
            cut.Markup.ShouldContain("Organisation");
        });
    }

    private IRenderedComponent<PartiesAdminPortal> RenderAuthorized(string contextKey, int pageSize = AdminPortalQueryBounds.DefaultPageSize)
        => Render<PartiesAdminPortal>(p => p
            .Add(x => x.ContextKey, contextKey)
            .Add(x => x.IsAuthenticated, true)
            .Add(x => x.HasTenantContext, true)
            .Add(x => x.IsAdmin, true)
            .Add(x => x.PageSize, pageSize));

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
}
