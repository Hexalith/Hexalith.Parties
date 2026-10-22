using System.Security.Claims;
using System.Reflection;

using Bunit;

using Hexalith.Commons.UniqueIds;
using Hexalith.Parties.AdminPortal.Components;
using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.AdminPortal.Tests.Services;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Contracts.Enums;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Parties.AdminPortal.Tests.Components;

public sealed class CreateEditPartyPageTests : BunitContext
{
    private const string AdminUserId = "admin-user";

    private readonly TestAuthenticationStateProvider _authProvider = new();
    private readonly InMemoryTenantProjectionStore _tenantStore = new();

    public CreateEditPartyPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
        Services.AddSingleton<AuthenticationStateProvider>(_authProvider);
        Services.AddSingleton<ITenantProjectionStore>(_tenantStore);
        Services.AddScoped<IAdminPortalAuthorizationService, AdminPortalAuthorizationService>();
        Services.AddOptions<PartiesAdminPortalOptions>();
        Services.AddScoped<AdminPortalPartyQueryService>();
        Services.AddScoped<AdminPortalEventStoreAdminLinks>();
        Services.AddScoped<AdminPortalGdprStateCoordinator>();
        Services.AddScoped<PartiesAdminListCoordinator>();
    }

    [Fact]
    public void CreateRoute_Unauthenticated_MakesNoDetailOrCommandCalls()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);

        IRenderedComponent<CreateEditPartyPage> cut = Render<CreateEditPartyPage>();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Sign-in is required"));
        api.DetailRequests.ShouldBeEmpty();
        api.CreateRequests.ShouldBeEmpty();
        api.UpdateRequests.ShouldBeEmpty();
    }

    [Fact]
    public void CreateRoute_MissingTenant_MakesNoDetailOrCommandCalls()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        _authProvider.SetAuthenticatedWithoutTenant(AdminUserId);

        IRenderedComponent<CreateEditPartyPage> cut = Render<CreateEditPartyPage>();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Tenant context is unavailable"));
        api.DetailRequests.ShouldBeEmpty();
        api.CreateRequests.ShouldBeEmpty();
        api.UpdateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task EditRoute_NonAdmin_MakesNoDetailOrCommandCallsAsync()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        var state = new TenantLocalState
        {
            TenantId = "scope-reader",
            Status = TenantStatus.Active,
        };
        state.Members[AdminUserId] = TenantRole.TenantReader;
        await _tenantStore.SaveAsync(state);
        _authProvider.SetAuthenticated(AdminUserId, "scope-reader");

        IRenderedComponent<CreateEditPartyPage> cut = Render<CreateEditPartyPage>(p => p
            .Add(x => x.RoutePartyId, "party-reader"));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Access denied"));
        api.DetailRequests.ShouldBeEmpty();
        api.CreateRequests.ShouldBeEmpty();
        api.UpdateRequests.ShouldBeEmpty();
    }

    [Fact]
    public void CreateRoute_SubmitsCreateCompositeWithGeneratedPartyId()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-create");

        IRenderedComponent<CreateEditPartyPage> cut = Render<CreateEditPartyPage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create party"));
        SetTextInput(cut, "First name", "Ada");
        SetTextInput(cut, "Last name", "Lovelace");
        SetSelect(cut, "Contact type", "Email");
        SetTextInput(cut, "Contact value", "ada@example.test");
        SetSelect(cut, "Identifier type", "Other");
        SetTextInput(cut, "Identifier value", "id-123");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            api.CreateRequests.Count.ShouldBe(1);
            ShouldBeSortableUniqueId(api.CreateRequests[0].PartyId);
        });
        api.CreateRequests[0].Type.ShouldBe(PartyType.Person);
        api.CreateRequests[0].PersonDetails.ShouldNotBeNull().FirstName.ShouldBe("Ada");
        api.CreateRequests[0].OrganizationDetails.ShouldBeNull();
        AddContactChannel contact = api.CreateRequests[0].ContactChannels.ShouldHaveSingleItem();
        contact.PartyId.ShouldBe(api.CreateRequests[0].PartyId);
        ShouldBeSortableUniqueId(contact.ContactChannelId);
        AddIdentifier identifier = api.CreateRequests[0].Identifiers.ShouldHaveSingleItem();
        identifier.PartyId.ShouldBe(api.CreateRequests[0].PartyId);
        ShouldBeSortableUniqueId(identifier.IdentifierId);
        cut.Markup.ShouldNotContain("name=\"PartyId\"");
    }

    [Fact]
    public void CreateRoute_RadioGroupSwitchesFieldSetsAndPreservesEnteredInput()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-radio");

        IRenderedComponent<CreateEditPartyPage> cut = Render<CreateEditPartyPage>();
        cut.WaitForAssertion(() => cut.FindComponent<FluentRadioGroup<string>>().Instance.Value.ShouldBe("Person"));
        cut.Markup.ShouldContain("Person");
        cut.Markup.ShouldContain("Organization");
        cut.Find("[role='radio'][value='Person']").GetAttribute("aria-checked").ShouldBe("true");
        cut.Find("[role='radio'][value='Organization']").GetAttribute("aria-checked").ShouldBe("false");
        SetTextInput(cut, "First name", "Ada");
        SetTextInput(cut, "Last name", "Lovelace");

        SetPartyType(cut, "Organization");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Organization details");
            cut.Find("[role='radio'][value='Person']").GetAttribute("aria-checked").ShouldBe("false");
            cut.Find("[role='radio'][value='Organization']").GetAttribute("aria-checked").ShouldBe("true");
        });
        SetTextInput(cut, "Legal name", "Analytical Engines Ltd");

        SetPartyType(cut, "Person");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Person details");
            cut.Find("[role='radio'][value='Person']").GetAttribute("aria-checked").ShouldBe("true");
            cut.Find("[role='radio'][value='Organization']").GetAttribute("aria-checked").ShouldBe("false");
        });
        FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada");
        FindTextInput(cut, "Last name").Instance.Value.ShouldBe("Lovelace");

        SetPartyType(cut, "Organization");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.ShouldContain("Organization details");
            cut.Find("[role='radio'][value='Person']").GetAttribute("aria-checked").ShouldBe("false");
            cut.Find("[role='radio'][value='Organization']").GetAttribute("aria-checked").ShouldBe("true");
        });
        FindTextInput(cut, "Legal name").Instance.Value.ShouldBe("Analytical Engines Ltd");
    }

    [Fact]
    public void EditRoute_LoadsDetailAndUsesRouteIdForUpdateIdentity()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueDetail(new PartyDetail
        {
            Id = "route-party-1",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            NameHistory = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-edit");

        IRenderedComponent<CreateEditPartyPage> cut = Render<CreateEditPartyPage>(p => p
            .Add(x => x.RoutePartyId, "route-party-1"));
        cut.WaitForAssertion(() => api.DetailRequests.Single().ShouldBe("route-party-1"));
        SetTextInput(cut, "Last name", "Byron");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => api.UpdateRequests.Count.ShouldBe(1));
        api.UpdateRequests[0].PartyId.ShouldBe("route-party-1");
        api.UpdateRequests[0].Command.PartyId.ShouldBe("route-party-1");
        api.UpdateRequests[0].Command.PersonDetails.ShouldNotBeNull().LastName.ShouldBe("Byron");
    }

    [Fact]
    public async Task EditRoute_WhenRoutePartyIdChanges_ResetsAndLoadsNewDetailAsync()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-one",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            NameHistory = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        api.EnqueueDetail(new PartyDetail
        {
            Id = "party-two",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Grace Hopper",
            SortName = "Hopper, Grace",
            PersonDetails = new PersonDetails { FirstName = "Grace", LastName = "Hopper" },
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            NameHistory = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-04T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-route-change");

        IRenderedComponent<CreateEditPartyPage> cut = Render<CreateEditPartyPage>(p => p
            .Add(x => x.RoutePartyId, "party-one"));
        cut.WaitForAssertion(() => FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada"));
        SetTextInput(cut, "First name", "Edited");

        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(CreateEditPartyPage.RoutePartyId)] = "party-two",
            })));

        cut.WaitForAssertion(() =>
        {
            api.DetailRequests.ShouldBe(["party-one", "party-two"]);
            FindTextInput(cut, "First name").Instance.Value.ShouldBe("Grace");
            FindTextInput(cut, "Last name").Instance.Value.ShouldBe("Hopper");
        });
    }

    [Fact]
    public void EditRoute_ErasedDetailShowsPrivacyCopyAndNoPersonalFields()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueDetail(new PartyDetail
        {
            Id = "erased-party",
            Type = PartyType.Person,
            IsActive = false,
            IsErased = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            NameHistory = [],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        });
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-erased");

        IRenderedComponent<CreateEditPartyPage> cut = Render<CreateEditPartyPage>(p => p
            .Add(x => x.RoutePartyId, "erased-party"));

        cut.WaitForAssertion(() => cut.Find("[role='alert']").TextContent.ShouldContain("erased or no longer inspectable"));
        cut.Markup.ShouldNotContain("First name");
        cut.Markup.ShouldNotContain("Ada Lovelace");
        api.DetailRequests.ShouldHaveSingleItem().ShouldBe("erased-party");
        api.UpdateRequests.ShouldBeEmpty();
    }

    [Fact]
    public void CreateRoute_ClientValidationShowsAlertAndPreservesInput()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-validation");

        IRenderedComponent<CreateEditPartyPage> cut = Render<CreateEditPartyPage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create party"));
        SetTextInput(cut, "First name", "Ada");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role='alert']").TextContent.ShouldContain("Fix the highlighted fields and retry.");
            api.CreateRequests.ShouldBeEmpty();
        });
        FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada");
    }

    [Fact]
    public void CreateRoute_GatewayValidationUsesSafeAlertCopy()
    {
        var api = new RecordingAdminPortalApiClient();
        api.EnqueueCreate(new AdminPortalCommandResult(
            AdminPortalCommandOutcome.ValidationRejected,
            "corr-validation",
            ValidationFailures: [new AdminPortalCommandValidationFailure("PersonDetails.LastName", "NotEmpty")]));
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-gateway-validation");

        IRenderedComponent<CreateEditPartyPage> cut = Render<CreateEditPartyPage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create party"));
        SetTextInput(cut, "First name", "Ada");
        SetTextInput(cut, "Last name", "Lovelace");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role='alert']").TextContent.ShouldContain("Fix the highlighted fields and retry.");
            cut.Markup.ShouldNotContain("corr-validation");
            cut.Markup.ShouldNotContain("PersonDetails.LastName");
        });
    }

    [Fact]
    public void CreateRoute_AcceptedWithoutPayloadShowsOptimisticStatusAndNavigatesToSafeDetail()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-optimistic");
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();

        IRenderedComponent<CreateEditPartyPage> cut = Render<CreateEditPartyPage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Create party"));
        SetTextInput(cut, "First name", "Katherine");
        SetTextInput(cut, "Last name", "Johnson");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[role='status']").TextContent.ShouldContain("Saved - updating...");
            api.CreateRequests.Count.ShouldBe(1);
        });
        Uri uri = new(navigation.Uri);
        uri.AbsolutePath.ShouldBe($"/admin/parties/{api.CreateRequests[0].PartyId}");
        cut.Dispose();

        IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>(parameters =>
            parameters.Add(x => x.RoutePartyId, api.CreateRequests[0].PartyId));
        portal.WaitForAssertion(() => portal.Find(".hx-parties-admin__accepted-command-status")
            .TextContent.ShouldBe("Saved - updating..."));
        portal.Dispose();
        IRenderedComponent<PartiesAdminPortal> subsequent = Render<PartiesAdminPortal>(parameters =>
            parameters.Add(x => x.RoutePartyId, api.CreateRequests[0].PartyId));
        subsequent.WaitForAssertion(() => subsequent.FindAll(".hx-parties-admin__accepted-command-status").ShouldBeEmpty());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AcceptedStatus_ShowsBeforePendingProjectionReadCompletes(bool delayList)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-pending");
        string partyId = SubmitAcceptedCreate(api);
        var list = new TaskCompletionSource<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var detail = new TaskCompletionSource<AdminPortalQueryResult<PartyDetail>>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (delayList)
        {
            api.EnqueueList(token => list.Task.WaitAsync(token));
        }
        else
        {
            api.EnqueueDetail(token => detail.Task.WaitAsync(token));
        }

        using IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>(parameters =>
            parameters.Add(x => x.RoutePartyId, partyId));

        portal.WaitForAssertion(() =>
        {
            var notice = portal.Find(".hx-parties-admin__accepted-command-status");
            notice.TextContent.ShouldBe("Saved - updating...");
            notice.GetAttribute("role").ShouldBe("status");
            notice.GetAttribute("aria-live").ShouldBe("polite");
            api.ListRequests.Count.ShouldBe(1);
            api.DetailRequests.Count.ShouldBe(delayList ? 0 : 1);
            list.Task.IsCompleted.ShouldBeFalse();
            detail.Task.IsCompleted.ShouldBeFalse();
        });
    }

    [Theory]
    [InlineData("healthy", false)]
    [InlineData("empty", true)]
    [InlineData("unrelated", true)]
    [InlineData("stale", true)]
    [InlineData("degraded", true)]
    [InlineData("reason-only", true)]
    [InlineData("local-only", true)]
    public void AcceptedStatus_ReconcilesOnlyMatchingHealthyCurrentRows(string response, bool remainsPending)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-convergence");
        string partyId = SubmitAcceptedCreate(api);
        using IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>(parameters =>
            parameters.Add(x => x.RoutePartyId, partyId));
        portal.WaitForAssertion(() => portal.Find(".hx-parties-admin__accepted-command-status")
            .TextContent.ShouldBe("Saved - updating..."));
        AdminPortalQueryMetadata metadata = response switch
        {
            "stale" => new(StaleDataAge: "5"),
            "degraded" => new(ServiceDegraded: true),
            "reason-only" => new(SearchDegradedReason: "backend-unavailable"),
            "local-only" => new(SearchStatus: "LocalOnly"),
            _ => AdminPortalQueryMetadata.Empty,
        };
        api.EnqueueList(new PagedResult<PartyIndexEntry>
        {
            Items = response == "empty" ? [] : [new PartyIndexEntry
            {
                Id = response == "unrelated" ? "another-party" : partyId,
                DisplayName = "Projected party",
                SortName = "Projected party",
                Type = PartyType.Person,
                IsActive = true,
            }],
            Page = 1,
            PageSize = 20,
            TotalCount = response == "empty" ? 0 : 1,
            TotalPages = response == "empty" ? 0 : 1,
        }, metadata);

        portal.FindAll("fluent-button").Single(button => button.TextContent.Trim() == "Search").Click();

        portal.WaitForAssertion(() =>
        {
            api.ListRequests.Count.ShouldBe(2);
            portal.FindAll(".hx-parties-admin__accepted-command-status").Count.ShouldBe(remainsPending ? 1 : 0);
        });
    }

    [Theory]
    [InlineData(ProjectionFreshnessStatus.Current, false)]
    [InlineData(ProjectionFreshnessStatus.Stale, false)]
    [InlineData(ProjectionFreshnessStatus.Rebuilding, false)]
    [InlineData(ProjectionFreshnessStatus.Degraded, false)]
    [InlineData(ProjectionFreshnessStatus.Unavailable, false)]
    [InlineData(ProjectionFreshnessStatus.LocalOnly, false)]
    [InlineData(ProjectionFreshnessStatus.Current, true)]
    [InlineData(ProjectionFreshnessStatus.Stale, true)]
    [InlineData(ProjectionFreshnessStatus.Rebuilding, true)]
    [InlineData(ProjectionFreshnessStatus.Degraded, true)]
    [InlineData(ProjectionFreshnessStatus.Unavailable, true)]
    [InlineData(ProjectionFreshnessStatus.LocalOnly, true)]
    public async Task AcceptedStatus_PayloadFreshnessControlsListAndSearchConvergenceAsync(ProjectionFreshnessStatus status, bool search)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-payload-freshness");
        string partyId = SubmitAcceptedCreate(api);
        using IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>(parameters => parameters
            .Add(x => x.RoutePartyId, partyId)
            .Add(x => x.SearchDebounceDelay, TimeSpan.FromHours(1)));
        portal.WaitForAssertion(() => portal.Find(".hx-parties-admin__accepted-command-status")
            .TextContent.ShouldBe("Saved - updating..."));
        var row = new PartyIndexEntry { Id = partyId, DisplayName = "Freshness row", Type = PartyType.Person };
        var freshness = new ProjectionFreshnessMetadata { Status = status };
        if (search)
        {
            api.EnqueueSearch(new PagedResult<PartySearchResult>
            {
                Items = [new PartySearchResult { Party = row, Matches = [] }],
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
                TotalPages = 1,
                Freshness = freshness,
            });
            await portal.InvokeAsync(() => portal.FindComponent<FluentTextInput>().Instance.ValueChanged.InvokeAsync("needle"));
        }
        else
        {
            api.EnqueueList(new PagedResult<PartyIndexEntry>
            {
                Items = [row],
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
                TotalPages = 1,
                Freshness = freshness,
            });
        }

        portal.FindAll("fluent-button").Single(button => button.TextContent.Trim() == "Search").Click();

        portal.WaitForAssertion(() =>
        {
            if (search)
            {
                api.SearchRequests.Count.ShouldBe(1);
            }
            else
            {
                api.ListRequests.Count.ShouldBe(2);
            }

            portal.FindAll(".hx-parties-admin__accepted-command-status").Count
                .ShouldBe(status == ProjectionFreshnessStatus.Current ? 0 : 1);
        });
    }

    [Fact]
    public void AcceptedStatus_SameAuthorizationContextConsumesNewReceipt()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-existing");
        using IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>();
        portal.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(1));
        string partyId = SubmitAcceptedCreate(api);

        portal.Render(parameters => parameters.Add(x => x.RoutePartyId, partyId));

        portal.WaitForAssertion(() => portal.Find(".hx-parties-admin__accepted-command-status")
            .TextContent.ShouldBe("Saved - updating..."));
    }

    [Fact]
    public async Task AcceptedStatus_ReadStartedBeforeReceiptCannotReconcileNoticeAsync()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-before-receipt");
        using IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>();
        portal.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(1));
        // Inline continuations force the old read to finish during receipt rendering.
        var before = new TaskCompletionSource<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>>();
        var after = new TaskCompletionSource<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.EnqueueList(_ => before.Task);
        Task beforeUpdate = portal.InvokeAsync(() => portal.Instance.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?> { [nameof(PartiesAdminPortal.ContextKey)] = "reload" })));
        portal.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(2));
        string partyId = SubmitAcceptedCreate(api);
        api.EnqueueList(_ => after.Task);
        portal.OnAfterRender += (_, _) =>
        {
            if (!before.Task.IsCompleted && portal.FindAll(".hx-parties-admin__accepted-command-status").Count > 0)
            {
                before.SetResult(new AdminPortalQueryResult<PagedResult<PartyIndexEntry>>(
                    new PagedResult<PartyIndexEntry>
                    {
                        Items = [new PartyIndexEntry { Id = partyId, DisplayName = "Before receipt", Type = PartyType.Person }],
                        Page = 1,
                        PageSize = 20,
                        TotalCount = 1,
                        TotalPages = 1,
                    }, AdminPortalQueryMetadata.Empty));
            }
        };
        Task afterUpdate = portal.InvokeAsync(() => portal.Instance.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?> { [nameof(PartiesAdminPortal.ContextKey)] = "reload", [nameof(PartiesAdminPortal.RoutePartyId)] = partyId })));
        try
        {
            portal.WaitForAssertion(() =>
            {
                api.ListRequests.Count.ShouldBe(3);
                portal.Find(".hx-parties-admin__accepted-command-status").TextContent.ShouldBe("Saved - updating...");
            });
            await beforeUpdate.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
            portal.Find(".hx-parties-admin__accepted-command-status").TextContent.ShouldBe("Saved - updating...");
            portal.Find("table").TextContent.ShouldNotContain("Before receipt");
        }
        finally
        {
            var empty = new AdminPortalQueryResult<PagedResult<PartyIndexEntry>>(
                new PagedResult<PartyIndexEntry> { Items = [], Page = 1, PageSize = 20 }, AdminPortalQueryMetadata.Empty);
            before.TrySetResult(empty);
            after.TrySetResult(empty);
            await Task.WhenAll(beforeUpdate, afterUpdate).WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData("route")]
    [InlineData("tenant")]
    [InlineData("sign-out")]
    public async Task AcceptedStatus_PendingReadCannotRestoreNoticeAfterBoundaryChangeAsync(string boundary)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-delayed-boundary");
        using IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>();
        portal.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(1));
        string partyId = SubmitAcceptedCreate(api);
        var list = new TaskCompletionSource<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.EnqueueList(_ => list.Task);
        Task parameterUpdate = portal.InvokeAsync(() => portal.Instance.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?> { [nameof(PartiesAdminPortal.RoutePartyId)] = partyId, [nameof(PartiesAdminPortal.ContextKey)] = "reload" })));
        try
        {
            portal.WaitForAssertion(() =>
            {
                api.ListRequests.Count.ShouldBe(2);
                portal.Find(".hx-parties-admin__accepted-command-status").TextContent.ShouldBe("Saved - updating...");
            });
            await portal.InvokeAsync(() =>
            {
                if (boundary == "route")
                {
                    Services.GetRequiredService<NavigationManager>().NavigateTo("/admin/parties");
                }
                else if (boundary == "tenant")
                {
                    RenderAuthorizedTenant("scope-new");
                }
                else
                {
                    _authProvider.SetUnauthenticated();
                }
            });
            portal.WaitForAssertion(() => portal.FindAll(".hx-parties-admin__accepted-command-status").ShouldBeEmpty());
        }
        finally
        {
            // Ignore cancellation deliberately to exercise a successful late response.
            list.TrySetResult(new AdminPortalQueryResult<PagedResult<PartyIndexEntry>>(
                new PagedResult<PartyIndexEntry> { Items = [], Page = 1, PageSize = 20 }, AdminPortalQueryMetadata.Empty));
            await parameterUpdate.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        }

        portal.FindAll(".hx-parties-admin__accepted-command-status").ShouldBeEmpty();
    }

    [Fact]
    public async Task AcceptedStatus_SupersededMatchingPageDoesNotReconcileNoticeAsync()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-superseded");
        using IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>();
        portal.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(1));
        string partyId = SubmitAcceptedCreate(api);
        var list = new TaskCompletionSource<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.EnqueueList(_ => list.Task);
        Task parameterUpdate = portal.InvokeAsync(() => portal.Instance.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?> { [nameof(PartiesAdminPortal.RoutePartyId)] = partyId, [nameof(PartiesAdminPortal.ContextKey)] = "reload" })));
        try
        {
            portal.WaitForAssertion(() =>
            {
                api.ListRequests.Count.ShouldBe(2);
                portal.Find(".hx-parties-admin__accepted-command-status").TextContent.ShouldBe("Saved - updating...");
            });
            portal.FindAll("fluent-button").Single(button => button.TextContent.Trim() == "Search").Click();
            portal.WaitForAssertion(() => api.ListRequests.Count.ShouldBe(3));
        }
        finally
        {
            list.TrySetResult(new AdminPortalQueryResult<PagedResult<PartyIndexEntry>>(
                new PagedResult<PartyIndexEntry>
                {
                    Items = [new PartyIndexEntry { Id = partyId, DisplayName = "Superseded row", Type = PartyType.Person }],
                    Page = 1,
                    PageSize = 20,
                    TotalCount = 1,
                    TotalPages = 1,
                }, AdminPortalQueryMetadata.Empty));
            await parameterUpdate.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        }

        portal.Find(".hx-parties-admin__accepted-command-status").TextContent.ShouldBe("Saved - updating...");
        portal.Find("table").TextContent.ShouldNotContain("Superseded row");
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("user")]
    [InlineData("missing-tenant")]
    [InlineData("sign-out")]
    [InlineData("unrelated-route")]
    public void AcceptedStatus_ClearsAcrossAuthorizationAndRouteBoundaries(string boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-accepted");
        IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>();
        form.WaitForAssertion(() => form.Markup.ShouldContain("Create party"));
        SetTextInput(form, "First name", "Katherine");
        SetTextInput(form, "Last name", "Johnson");
        form.Find("form").Submit();
        form.WaitForAssertion(() => api.CreateRequests.Count.ShouldBe(1));
        form.Dispose();
        IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>(parameters =>
            parameters.Add(x => x.RoutePartyId, api.CreateRequests[0].PartyId));
        portal.WaitForAssertion(() => portal.Find(".hx-parties-admin__accepted-command-status")
            .TextContent.ShouldBe("Saved - updating..."));

        switch (boundary)
        {
            case "tenant":
                RenderAuthorizedTenant("scope-other");
                break;
            case "user":
                _authProvider.SetAuthenticated("another-user", "scope-accepted");
                break;
            case "missing-tenant":
                _authProvider.SetAuthenticatedWithoutTenant(AdminUserId);
                break;
            case "sign-out":
                _authProvider.SetUnauthenticated();
                break;
            case "unrelated-route":
                Services.GetRequiredService<NavigationManager>().NavigateTo("/admin/parties");
                break;
        }

        portal.WaitForAssertion(() => portal.FindAll(".hx-parties-admin__accepted-command-status").ShouldBeEmpty());
    }

    [Theory]
    [InlineData("old-person")]
    [InlineData("prefix")]
    [InlineData("suffix")]
    [InlineData("dob")]
    [InlineData("contact-id")]
    [InlineData("contact-value")]
    [InlineData("contact-type")]
    [InlineData("contact-preferred")]
    [InlineData("identifier-id")]
    [InlineData("identifier-value")]
    [InlineData("identifier-type")]
    [InlineData("unrelated")]
    [InlineData("stale")]
    [InlineData("rebuilding")]
    [InlineData("unavailable")]
    [InlineData("local-only")]
    [InlineData("freshness-degraded")]
    [InlineData("no-freshness")]
    [InlineData("degraded")]
    [InlineData("search-degraded")]
    [InlineData("organization-name")]
    [InlineData("organization-trading")]
    [InlineData("organization-form")]
    [InlineData("organization-registration")]
    [InlineData("organization-natural")]
    public void AcceptedEdit_WaitsForAllSubmittedValuesThenConverges(string mismatch)
    {
        ArgumentNullException.ThrowIfNull(mismatch);
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-edit-convergence");
        bool organization = mismatch.StartsWith("organization", StringComparison.Ordinal);
        UpdatePartyComposite update = SubmitAcceptedEdit(api, organization);
        PartyDetail expected = ProjectedUpdate(update);
        PartyDetail actual = mismatch switch
        {
            "old-person" => expected with { PersonDetails = new PersonDetails { FirstName = "Prior", LastName = "Values" } },
            "prefix" => expected with { PersonDetails = expected.PersonDetails! with { Prefix = null } },
            "suffix" => expected with { PersonDetails = expected.PersonDetails! with { Suffix = null } },
            "dob" => expected with { PersonDetails = expected.PersonDetails! with { DateOfBirth = null } },
            "contact-id" => expected with { ContactChannels = [expected.ContactChannels[0] with { Id = "old-child" }] },
            "contact-value" => expected with { ContactChannels = [expected.ContactChannels[0] with { Value = "old@example.test" }] },
            "contact-type" => expected with { ContactChannels = [expected.ContactChannels[0] with { Type = ContactChannelType.Phone }] },
            "contact-preferred" => expected with { ContactChannels = [expected.ContactChannels[0] with { IsPreferred = true }] },
            "identifier-id" => expected with { Identifiers = [expected.Identifiers[0] with { Id = "old-child" }] },
            "identifier-value" => expected with { Identifiers = [expected.Identifiers[0] with { Value = "old-value" }] },
            "identifier-type" => expected with { Identifiers = [expected.Identifiers[0] with { Type = IdentifierType.VAT }] },
            "unrelated" => expected with { Id = "another-party" },
            "stale" => expected with { Freshness = new() { Status = ProjectionFreshnessStatus.Stale } },
            "rebuilding" => expected with { Freshness = new() { Status = ProjectionFreshnessStatus.Rebuilding } },
            "unavailable" => expected with { Freshness = new() { Status = ProjectionFreshnessStatus.Unavailable } },
            "local-only" => expected with { Freshness = new() { Status = ProjectionFreshnessStatus.LocalOnly } },
            "freshness-degraded" => expected with { Freshness = new() { Status = ProjectionFreshnessStatus.Degraded } },
            "no-freshness" => expected with { Freshness = null },
            "organization-name" => expected with { OrganizationDetails = expected.OrganizationDetails! with { LegalName = "Old name" } },
            "organization-trading" => expected with { OrganizationDetails = expected.OrganizationDetails! with { TradingName = null } },
            "organization-form" => expected with { OrganizationDetails = expected.OrganizationDetails! with { LegalForm = null } },
            "organization-registration" => expected with { OrganizationDetails = expected.OrganizationDetails! with { RegistrationNumber = null } },
            "organization-natural" => expected with { OrganizationDetails = expected.OrganizationDetails! with { IsNaturalPerson = true } },
            _ => expected,
        };
        AdminPortalQueryMetadata metadata = mismatch switch
        {
            "degraded" => new(ServiceDegraded: true),
            "search-degraded" => new(SearchDegradedReason: "unavailable"),
            _ => AdminPortalQueryMetadata.Empty,
        };
        // A healthy matching list identity is already visible before the edit projects.
        api.EnqueueList(new PagedResult<PartyIndexEntry>
        {
            Items = [new PartyIndexEntry { Id = update.PartyId, DisplayName = "Projected edit", Type = expected.Type }],
            Page = 1, PageSize = 20, TotalCount = 1, TotalPages = 1,
            Freshness = new() { Status = ProjectionFreshnessStatus.Current },
        });
        api.EnqueueDetail(actual, metadata);
        using IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>(p => p.Add(x => x.RoutePartyId, update.PartyId));
        portal.WaitForAssertion(() =>
        {
            api.DetailRequests.Count.ShouldBe(2);
            portal.Find(".hx-parties-admin__accepted-command-status").TextContent.ShouldBe("Saved - updating...");
        });
        api.EnqueueDetail(expected);
        portal.FindAll("fluent-button").Single(button => button.TextContent.Trim() == "Projected edit").Click();
        portal.WaitForAssertion(() =>
        {
            api.DetailRequests.Count.ShouldBe(3);
            portal.FindAll(".hx-parties-admin__accepted-command-status").ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task AcceptedEdit_SnapshotUsesCommandBuiltBeforeAwaitAsync()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-submit-snapshot");
        api.EnqueueDetail(EditSource(false));
        var completion = new TaskCompletionSource<AdminPortalCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.EnqueueUpdate(_ => completion.Task);
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, "edit-party"));
        form.WaitForAssertion(() => form.Find("form").ShouldNotBeNull());
        SetTextInput(form, "Date of birth", "1815-12-10");
        Task submit = form.Find("form").SubmitAsync();
        form.WaitForAssertion(() => api.UpdateRequests.Count.ShouldBe(1));
        SetTextInput(form, "Date of birth", "1900-01-01");
        completion.SetResult(new(AdminPortalCommandOutcome.Accepted, null));
        await submit.ConfigureAwait(true);
        await form.InvokeAsync(form.Instance.Dispose);
        api.EnqueueDetail(EditSource(false));
        using IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>(p => p.Add(x => x.RoutePartyId, "edit-party"));
        portal.WaitForAssertion(() =>
        {
            api.DetailRequests.Count.ShouldBe(2);
            portal.Find(".hx-parties-admin__accepted-command-status").TextContent.ShouldBe("Saved - updating...");
        });
        api.EnqueueDetail(ProjectedUpdate(api.UpdateRequests[0].Command));
        portal.Find("fluent-button[aria-label='Retry: Party detail']").Click();
        portal.WaitForAssertion(() =>
        {
            api.DetailRequests.Count.ShouldBe(3);
            portal.FindAll(".hx-parties-admin__accepted-command-status").ShouldBeEmpty();
        });
        api.UpdateRequests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("scope-reset")]
    [InlineData("tenant")]
    [InlineData("sign-out")]
    [InlineData("route")]
    [InlineData("dispose")]
    public async Task AcceptedEdit_LateSubmitCannotStageAfterBoundaryChangeAsync(string boundary)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-edit-submit-boundary");
        api.EnqueueDetail(EditSource(false));
        var completion = new TaskCompletionSource<AdminPortalCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.EnqueueUpdate(_ => completion.Task); // Deliberately ignore cancellation.
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, "edit-party"));
        form.WaitForAssertion(() => form.Find("form").ShouldNotBeNull());
        SetTextInput(form, "First name", "Expected secret");
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/admin/parties/edit-party/edit");
        Task submit = form.Find("form").SubmitAsync();
        form.WaitForAssertion(() => api.UpdateRequests.Count.ShouldBe(1));
        switch (boundary)
        {
            case "scope-reset":
                Services.GetRequiredService<AdminPortalPartyQueryService>().ResetForTenantSwitch();
                break;
            case "tenant":
                RenderAuthorizedTenant("scope-edit-submit-other");
                break;
            case "sign-out":
                _authProvider.SetUnauthenticated();
                break;
            case "route":
                api.EnqueueDetail(EditSource(false) with { Id = "another-edit" });
                form.Render(p => p.Add(x => x.RoutePartyId, "another-edit"));
                break;
            case "dispose":
                await form.InvokeAsync(form.Instance.Dispose).ConfigureAwait(true);
                form.Dispose();
                break;
        }
        completion.SetResult(new(AdminPortalCommandOutcome.Accepted, null));
        await submit.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        new Uri(navigation.Uri).AbsolutePath.ShouldBe("/admin/parties/edit-party/edit");
        RenderAuthorizedTenant("scope-edit-submit-boundary");
        using IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>(p => p.Add(x => x.RoutePartyId, "edit-party"));
        portal.WaitForAssertion(() =>
        {
            api.ListRequests.Count.ShouldBe(1);
            portal.FindAll(".hx-parties-admin__accepted-command-status").ShouldBeEmpty();
        });
    }

    [Theory]
    [InlineData("api")]
    [InlineData("cancellation")]
    [InlineData("authorization")]
    public async Task AcceptedEdit_OldCompletionKeepsNewRouteSubmissionBusyAsync(string completionStage)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-generation-owned");
        api.EnqueueDetail(EditSource(false));
        var oldResponse = new TaskCompletionSource<AdminPortalCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newResponse = new TaskCompletionSource<AdminPortalCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldAuthorization = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.EnqueueUpdate(_ =>
        {
            if (completionStage == "authorization")
            {
                _authProvider.NextState = oldAuthorization.Task;
            }
            return oldResponse.Task;
        });
        api.EnqueueUpdate(_ => newResponse.Task);
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, "edit-party"));
        form.WaitForAssertion(() => form.Find("form").ShouldNotBeNull());
        if (completionStage == "authorization")
        {
            oldResponse.SetResult(new(AdminPortalCommandOutcome.Accepted, null));
        }
        Task oldSubmit = form.Find("form").SubmitAsync();
        form.WaitForAssertion(() => api.UpdateRequests.Count.ShouldBe(1));
        if (completionStage == "cancellation")
        {
            Services.GetRequiredService<AdminPortalPartyQueryService>().ResetForTenantSwitch();
        }
        api.EnqueueDetail(EditSource(false) with { Id = "new-route" });
        form.Render(p => p.Add(x => x.RoutePartyId, "new-route"));
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/admin/parties/new-route/edit");
        Task newSubmit = form.Find("form").SubmitAsync();
        form.WaitForAssertion(() => api.UpdateRequests.Count.ShouldBe(2));
        try
        {
            if (completionStage == "authorization")
            {
                oldAuthorization.SetException(new InvalidOperationException("private authorization diagnostic"));
            }
            else if (completionStage == "cancellation")
            {
                oldResponse.SetCanceled();
            }
            else
            {
                oldResponse.SetResult(new(AdminPortalCommandOutcome.Accepted, null));
            }
            await oldSubmit.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
            form.WaitForAssertion(() => form.FindComponents<FluentButton>()
                .Single(x => x.Instance.Type == ButtonType.Submit).Instance.Disabled.ShouldBeTrue());
            newSubmit.IsCompleted.ShouldBeFalse();
            new Uri(navigation.Uri).AbsolutePath.ShouldBe("/admin/parties/new-route/edit");
        }
        finally
        {
            oldResponse.TrySetResult(new(AdminPortalCommandOutcome.Accepted, null));
            oldAuthorization.TrySetResult(new(new ClaimsPrincipal(new ClaimsIdentity())));
            newResponse.TrySetResult(new(AdminPortalCommandOutcome.Accepted, null));
            await Task.WhenAll(oldSubmit, newSubmit).WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        new Uri(navigation.Uri).AbsolutePath.ShouldBe("/admin/parties/new-route");
    }

    [Fact]
    public async Task AcceptedEdit_AuthorizationProviderFailureAfterAcceptanceBlocksResubmissionAsync()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-accepted-auth-failure");
        api.EnqueueDetail(EditSource(false));
        api.EnqueueUpdate(_ =>
        {
            _authProvider.NextState = Task.FromException<AuthenticationState>(new InvalidOperationException("private authorization diagnostic"));
            return Task.FromResult(new AdminPortalCommandResult(AdminPortalCommandOutcome.Accepted, null));
        });
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, "edit-party"));
        form.WaitForAssertion(() => form.Find("form").ShouldNotBeNull());
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/admin/parties/edit-party/edit");
        await form.Find("form").SubmitAsync().WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        form.WaitForAssertion(() =>
        {
            form.FindAll("form").ShouldBeEmpty();
            form.Find("[role='alert']").TextContent.ShouldBe("The selected party is unavailable");
            form.Find("[role='status']").TextContent.ShouldBe("Saved - updating...");
            form.Markup.ShouldNotContain("private authorization diagnostic");
            form.Markup.ShouldNotContain("Retry");
        });
        api.UpdateRequests.Count.ShouldBe(1);
        new Uri(navigation.Uri).AbsolutePath.ShouldBe("/admin/parties/edit-party/edit");
        using IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>(p => p.Add(x => x.RoutePartyId, "edit-party"));
        portal.WaitForAssertion(() => portal.FindAll(".hx-parties-admin__accepted-command-status").ShouldBeEmpty());
    }

    [Fact]
    public async Task AcceptedEdit_CooperativeCancellationReleasesCurrentControlsWithoutNavigationAsync()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-cooperative-submit");
        api.EnqueueDetail(EditSource(false));
        CancellationToken observedToken = default;
        var response = new TaskCompletionSource<AdminPortalCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.EnqueueUpdate(token =>
        {
            observedToken = token;
            return response.Task.WaitAsync(token);
        });
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, "edit-party"));
        form.WaitForAssertion(() => form.Find("form").ShouldNotBeNull());
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/admin/parties/edit-party/edit");
        Task submit = form.Find("form").SubmitAsync();
        form.WaitForAssertion(() =>
        {
            api.UpdateRequests.Count.ShouldBe(1);
            form.FindComponents<FluentButton>().Single(x => x.Instance.Type == ButtonType.Submit).Instance.Disabled.ShouldBeTrue();
        });
        api.EnqueueDetail(EditSource(false));
        Services.GetRequiredService<AdminPortalPartyQueryService>().ResetForTenantSwitch();
        await submit.WaitAsync(TimeSpan.FromSeconds(2), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        observedToken.IsCancellationRequested.ShouldBeTrue();
        response.Task.IsCompleted.ShouldBeFalse();
        form.WaitForAssertion(() =>
        {
            form.FindComponents<FluentButton>().Single(x => x.Instance.Type == ButtonType.Submit).Instance.Disabled.ShouldBeFalse();
            form.FindAll("[role='status']").ShouldNotContain(x => x.TextContent.Contains("Saved", StringComparison.Ordinal));
        });
        api.UpdateRequests.Count.ShouldBe(1);
        new Uri(navigation.Uri).AbsolutePath.ShouldBe("/admin/parties/edit-party/edit");
    }

    [Theory]
    [InlineData(AdminPortalQueryFailureKind.AuthenticationRequired, false, false)]
    [InlineData(AdminPortalQueryFailureKind.TenantRequired, false, false)]
    [InlineData(AdminPortalQueryFailureKind.Forbidden, false, false)]
    [InlineData(AdminPortalQueryFailureKind.Gone, false, false)]
    [InlineData(AdminPortalQueryFailureKind.NotFound, false, true)]
    [InlineData(AdminPortalQueryFailureKind.AuthenticationRequired, true, false)]
    [InlineData(AdminPortalQueryFailureKind.TenantRequired, true, false)]
    [InlineData(AdminPortalQueryFailureKind.Forbidden, true, false)]
    [InlineData(AdminPortalQueryFailureKind.Gone, true, false)]
    [InlineData(AdminPortalQueryFailureKind.NotFound, true, true)]
    public void AcceptedEdit_TerminalQueryFailuresDiscardReceiptButNotFoundRemainsPending(AdminPortalQueryFailureKind failure, bool listFailure, bool remainsPending)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-terminal-receipt");
        UpdatePartyComposite update = SubmitAcceptedEdit(api);
        if (listFailure)
        {
            api.EnqueueListFailure(failure);
        }
        else
        {
            api.EnqueueDetailFailure(failure);
        }
        using IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>(p => p.Add(x => x.RoutePartyId, update.PartyId));
        portal.WaitForAssertion(() =>
        {
            api.ListRequests.Count.ShouldBe(1);
            api.DetailRequests.Count.ShouldBe(2);
            portal.FindAll(".hx-parties-admin__accepted-command-status").Count.ShouldBe(remainsPending ? 1 : 0);
        });
        api.UpdateRequests.Count.ShouldBe(1);
    }

    [Fact]
    public void AcceptedEdit_CurrentErasedDetailDiscardsReceiptWithoutClaimingConvergence()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("scope-erased-receipt");
        UpdatePartyComposite update = SubmitAcceptedEdit(api);
        api.EnqueueDetail(ProjectedUpdate(update) with { IsErased = true });
        using IRenderedComponent<PartiesAdminPortal> portal = Render<PartiesAdminPortal>(p => p.Add(x => x.RoutePartyId, update.PartyId));
        portal.WaitForAssertion(() =>
        {
            api.DetailRequests.Count.ShouldBe(2);
            portal.FindAll(".hx-parties-admin__accepted-command-status").ShouldBeEmpty();
            portal.Markup.ShouldContain("The selected party is erased or no longer inspectable");
        });
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("user")]
    [InlineData("sign-out")]
    [InlineData("a-b-a")]
    [InlineData("scope-reset")]
    [InlineData("same-context")]
    public void LoadedCreate_ContextNotificationClearsValuesAndDoesNotSend(string change)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("loaded-context-a");
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>();
        SetTextInput(form, "First name", "Old secret");
        SetTextInput(form, "Last name", "Old name");
        switch (change)
        {
            case "tenant": RenderAuthorizedTenant("loaded-context-b"); break;
            case "user": _authProvider.SetAuthenticated("different-user", "loaded-context-a"); break;
            case "sign-out": _authProvider.SetUnauthenticated(); break;
            case "a-b-a":
                RenderAuthorizedTenant("loaded-context-b");
                RenderAuthorizedTenant("loaded-context-a");
                break;
            case "scope-reset": Services.GetRequiredService<AdminPortalPartyQueryService>().ResetForTenantSwitch(); break;
            default: _authProvider.SetAuthenticated(AdminUserId, "loaded-context-a"); break;
        }
        form.WaitForAssertion(() =>
        {
            form.Markup.ShouldNotContain("Old secret");
            if (form.FindAll("form").Count != 0)
            {
                FindTextInput(form, "First name").Instance.Value.ShouldBeNullOrEmpty();
            }
        });
        if (form.FindAll("form").Count != 0)
        {
            form.Find("form").Submit();
        }
        api.CreateRequests.ShouldBeEmpty();
        api.UpdateRequests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotificationRendersClearedLoadingStateWhileAuthorizationRemainsPending(bool edit)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("pending-notification");
        api.EnqueueDetail(EditSource(false));
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, edit ? "edit-party" : null));
        SetTextInput(form, "First name", "Old secret");
        var pending = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        _authProvider.NextState = pending.Task;
        await Task.Run(_authProvider.NotifyCurrentState, Xunit.TestContext.Current.CancellationToken);
        form.WaitForAssertion(() =>
        {
            pending.Task.IsCompleted.ShouldBeFalse();
            form.FindAll("form").ShouldBeEmpty();
            form.Markup.ShouldNotContain("Old secret");
            form.Markup.ShouldContain("Loading");
        });
        await form.InvokeAsync(form.Instance.Dispose);
        pending.SetResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        api.CreateRequests.ShouldBeEmpty();
        api.UpdateRequests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackgroundNotificationsFencePendingSubmissionAndWorkTokenOwnership(bool edit)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("background-notifications");
        api.EnqueueDetail(EditSource(false));
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, edit ? "edit-party" : null));
        SetTextInput(form, "First name", "Old secret");
        SetTextInput(form, "Last name", "Old name");
        var pending = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        _authProvider.NextState = pending.Task;
        Task submit = form.Find("form").SubmitAsync();
        for (int index = 0; index < 40; index++) { api.EnqueueDetail(EditSource(false)); }
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(_authProvider.NotifyCurrentState, Xunit.TestContext.Current.CancellationToken)));
        pending.SetResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        await submit.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        form.WaitForAssertion(() =>
        {
            form.Find("form").ShouldNotBeNull();
            form.Markup.ShouldNotContain("Old secret");
        });
        api.CreateRequests.ShouldBeEmpty();
        api.UpdateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SynchronouslyCanceledRegistrationCannotOverwriteReentrantReloadOwnership()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("registration-race");
        api.EnqueueDetail(EditSource(false));
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, "edit-party"));
        SetTextInput(form, "First name", "Old secret");
        var service = Services.GetRequiredService<AdminPortalPartyQueryService>();
        CancellationToken capturedScope = service.ScopeCancellationToken;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type component = typeof(CreateEditPartyPage);
        long version = (long)component.GetField("_formVersion", flags)!.GetValue(form.Instance)!;
        var work = (CancellationTokenSource)component.GetField("_workCts", flags)!.GetValue(form.Instance)!;
        var registration = (CancellationTokenRegistration)component.GetField("_scopeRegistration", flags)!.GetValue(form.Instance)!;
        // Model cancellation after capture but before Register: the old subscription has not been published.
        registration.Dispose();
        service.ResetForTenantSwitch();
        capturedScope.IsCancellationRequested.ShouldBeTrue();
        api.EnqueueDetail(EditSource(false) with { PersonDetails = new() { FirstName = "Current", LastName = "Owner" } });
        bool published = true;
        await form.InvokeAsync(() => published = (bool)component.GetMethod("TryRegisterScope", flags)!
            .Invoke(form.Instance, [version, capturedScope, work])!);
        published.ShouldBeFalse();
        form.WaitForAssertion(() =>
        {
            FindTextInput(form, "First name").Instance.Value.ShouldBe("Current");
            form.Markup.ShouldNotContain("Old secret");
            component.GetField("_workCts", flags)!.GetValue(form.Instance).ShouldNotBeSameAs(work);
        });
        // The newer registration remains live and clears the newer form on the next scope reset.
        var next = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        _authProvider.NextState = next.Task;
        service.ResetForTenantSwitch();
        form.WaitForAssertion(() => form.FindAll("form").ShouldBeEmpty());
        await form.InvokeAsync(form.Instance.Dispose);
        next.SetResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        api.UpdateRequests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreshLookupDifferentAuthorizedContextWithoutNotificationNeverSendsOldSnapshot(bool edit)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("signature-context-a");
        var other = new TenantLocalState { TenantId = "signature-context-b", Status = TenantStatus.Active };
        other.Members[AdminUserId] = TenantRole.TenantOwner;
        await _tenantStore.SaveAsync(other, Xunit.TestContext.Current.CancellationToken);
        api.EnqueueDetail(EditSource(false));
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, edit ? "edit-party" : null));
        SetTextInput(form, "First name", "Old secret");
        SetTextInput(form, "Last name", "Old name");
        CancellationToken scope = Services.GetRequiredService<AdminPortalPartyQueryService>().ScopeCancellationToken;
        _authProvider.NextState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(PartiesClaimTypes.Subject, AdminUserId), new Claim(PartiesClaimTypes.EventStoreTenant, "signature-context-b")], "Test"))));
        if (edit) { api.EnqueueDetail(EditSource(false)); }
        await form.Find("form").SubmitAsync();
        Services.GetRequiredService<AdminPortalPartyQueryService>().ScopeCancellationToken.ShouldBe(scope);
        api.CreateRequests.ShouldBeEmpty();
        api.UpdateRequests.ShouldBeEmpty();
        form.WaitForAssertion(() => form.Markup.ShouldNotContain("Old secret"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadedForm_RevocationWithoutNotificationPreventsSend(bool edit)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("revoked-form");
        api.EnqueueDetail(EditSource(false));
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, edit ? "edit-party" : null));
        SetTextInput(form, "First name", "Old secret");
        SetTextInput(form, "Last name", "Old name");
        var state = new TenantLocalState { TenantId = "revoked-form", Status = TenantStatus.Active };
        state.Members[AdminUserId] = TenantRole.TenantReader;
        await _tenantStore.SaveAsync(state, Xunit.TestContext.Current.CancellationToken);
        await form.Find("form").SubmitAsync();
        form.WaitForAssertion(() =>
        {
            form.Markup.ShouldContain("Access denied");
            form.FindAll("form").ShouldBeEmpty();
            form.Markup.ShouldNotContain("Old secret");
        });
        api.CreateRequests.ShouldBeEmpty();
        api.UpdateRequests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadedForm_InactiveTenantPreventsSend(bool edit)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("inactive-form");
        api.EnqueueDetail(EditSource(false));
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, edit ? "edit-party" : null));
        SetTextInput(form, "First name", "Old secret");
        SetTextInput(form, "Last name", "Old name");
        await _tenantStore.SaveAsync(new TenantLocalState { TenantId = "inactive-form", Status = TenantStatus.Disabled }, Xunit.TestContext.Current.CancellationToken);
        await form.Find("form").SubmitAsync();
        form.WaitForAssertion(() => form.Markup.ShouldContain("Tenant context is unavailable"));
        api.CreateRequests.ShouldBeEmpty();
        api.UpdateRequests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadedForm_AuthorizationFailureBeforeSendClearsValues(bool edit)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("failed-check-form");
        api.EnqueueDetail(EditSource(false));
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, edit ? "edit-party" : null));
        SetTextInput(form, "First name", "Old secret");
        SetTextInput(form, "Last name", "Old name");
        _authProvider.NextState = Task.FromException<AuthenticationState>(new InvalidOperationException("private auth failure"));
        await form.Find("form").SubmitAsync();
        form.WaitForAssertion(() =>
        {
            form.Markup.ShouldContain("The selected party is unavailable");
            form.Markup.ShouldNotContain("private auth failure");
            form.Markup.ShouldNotContain("Old secret");
        });
        api.CreateRequests.ShouldBeEmpty();
        api.UpdateRequests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("tenant", false)]
    [InlineData("tenant", true)]
    [InlineData("sign-out", false)]
    [InlineData("sign-out", true)]
    [InlineData("a-b-a", false)]
    [InlineData("a-b-a", true)]
    [InlineData("scope-reset", false)]
    [InlineData("scope-reset", true)]
    [InlineData("dispose", false)]
    [InlineData("dispose", true)]
    [InlineData("user", false)]
    [InlineData("user", true)]
    [InlineData("role", false)]
    [InlineData("role", true)]
    [InlineData("inactive", false)]
    [InlineData("inactive", true)]
    public async Task PendingPreSendAuthorization_ContextBoundaryNeverSendsOldSnapshot(string boundary, bool edit)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("pending-auth-a");
        api.EnqueueDetail(EditSource(false));
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, edit ? "edit-party" : null));
        SetTextInput(form, "First name", "Old secret");
        SetTextInput(form, "Last name", "Old name");
        var pending = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        _authProvider.NextState = pending.Task;
        Task submit = form.Find("form").SubmitAsync();
        form.WaitForAssertion(() => form.FindComponents<FluentButton>().Single(x => x.Instance.Type == ButtonType.Submit).Instance.Disabled.ShouldBeTrue());
        api.CreateRequests.ShouldBeEmpty();
        switch (boundary)
        {
            case "tenant": RenderAuthorizedTenant("pending-auth-b"); break;
            case "user": _authProvider.SetAuthenticated("another-user", "pending-auth-a"); break;
            case "role":
                var revoked = new TenantLocalState { TenantId = "pending-auth-a", Status = TenantStatus.Active };
                revoked.Members[AdminUserId] = TenantRole.TenantReader;
                await _tenantStore.SaveAsync(revoked, Xunit.TestContext.Current.CancellationToken);
                break;
            case "inactive":
                await _tenantStore.SaveAsync(new TenantLocalState { TenantId = "pending-auth-a", Status = TenantStatus.Disabled }, Xunit.TestContext.Current.CancellationToken);
                break;
            case "sign-out": _authProvider.SetUnauthenticated(); break;
            case "a-b-a": RenderAuthorizedTenant("pending-auth-b"); RenderAuthorizedTenant("pending-auth-a"); break;
            case "scope-reset": Services.GetRequiredService<AdminPortalPartyQueryService>().ResetForTenantSwitch(); break;
            default: await form.InvokeAsync(form.Instance.Dispose); break;
        }
        pending.SetResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(PartiesClaimTypes.Subject, AdminUserId), new Claim(PartiesClaimTypes.EventStoreTenant, "pending-auth-a")], "Test"))));
        await submit.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        api.CreateRequests.ShouldBeEmpty();
        api.UpdateRequests.ShouldBeEmpty();
        if (boundary != "dispose")
        {
            form.WaitForAssertion(() => form.Markup.ShouldNotContain("Old secret"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreshAuthorization_UsesSnapshotTakenBeforeAwaitAndSendsOnce(bool edit)
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("snapshot-before-authorization");
        api.EnqueueDetail(EditSource(false));
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, edit ? "edit-party" : null));
        SetTextInput(form, "First name", "Submitted first");
        SetTextInput(form, "Last name", "Submitted last");
        var pending = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        _authProvider.NextState = pending.Task;
        Task submit = form.Find("form").SubmitAsync();
        SetTextInput(form, "First name", "Later mutation");
        api.CreateRequests.ShouldBeEmpty();
        api.UpdateRequests.ShouldBeEmpty();
        pending.SetResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(PartiesClaimTypes.Subject, AdminUserId), new Claim(PartiesClaimTypes.EventStoreTenant, "snapshot-before-authorization")], "Test"))));
        await submit.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        if (edit)
        {
            api.UpdateRequests.ShouldHaveSingleItem().Command.PersonDetails!.FirstName.ShouldBe("Submitted first");
            api.CreateRequests.ShouldBeEmpty();
        }
        else
        {
            api.CreateRequests.ShouldHaveSingleItem().PersonDetails!.FirstName.ShouldBe("Submitted first");
            api.UpdateRequests.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task PreSendAuthorization_TimeoutBlocksAndLateLookupCannotSend()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("authorization-timeout");
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>();
        SetTextInput(form, "First name", "Old secret");
        SetTextInput(form, "Last name", "Old name");
        var pending = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        _authProvider.NextState = pending.Task;
        await form.Find("form").SubmitAsync().WaitAsync(TimeSpan.FromSeconds(15), Xunit.TestContext.Current.CancellationToken);
        form.WaitForAssertion(() =>
        {
            form.FindAll("form").ShouldBeEmpty();
            form.Markup.ShouldContain("The selected party is unavailable");
            form.Markup.ShouldNotContain("Old secret");
        });
        pending.SetResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(PartiesClaimTypes.Subject, AdminUserId), new Claim(PartiesClaimTypes.EventStoreTenant, "authorization-timeout")], "Test"))));
        await form.InvokeAsync(() => Task.CompletedTask);
        api.CreateRequests.ShouldBeEmpty();
        api.UpdateRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task OldEditLoad_IgnoringCancellationCannotRestorePriorValues()
    {
        var api = new RecordingAdminPortalApiClient();
        Services.AddSingleton<IPartiesAdminPortalApiClient>(api);
        RenderAuthorizedTenant("old-load-a");
        var pending = new TaskCompletionSource<AdminPortalQueryResult<PartyDetail>>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.EnqueueDetail(_ => pending.Task);
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, "edit-party"));
        form.WaitForAssertion(() => api.DetailRequests.Count.ShouldBe(1));
        api.EnqueueDetail(EditSource(false) with { PersonDetails = new() { FirstName = "Current", LastName = "Tenant" } });
        RenderAuthorizedTenant("old-load-b");
        form.WaitForAssertion(() => FindTextInput(form, "First name").Instance.Value.ShouldBe("Current"));
        pending.SetResult(new(EditSource(false) with { PersonDetails = new() { FirstName = "Old secret", LastName = "Name" } }, AdminPortalQueryMetadata.Empty));
        await form.InvokeAsync(() => Task.CompletedTask);
        form.WaitForAssertion(() =>
        {
            FindTextInput(form, "First name").Instance.Value.ShouldBe("Current");
            form.Markup.ShouldNotContain("Old secret");
        });
        api.UpdateRequests.ShouldBeEmpty();
    }

    private UpdatePartyComposite SubmitAcceptedEdit(RecordingAdminPortalApiClient api, bool organization = false)
    {
        api.EnqueueDetail(EditSource(organization));
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>(p => p.Add(x => x.RoutePartyId, "edit-party"));
        form.WaitForAssertion(() => form.Find("form").ShouldNotBeNull());
        if (organization)
        {
            SetTextInput(form, "Legal name", "New legal name");
            SetTextInput(form, "Trading name", "New trading name");
            SetTextInput(form, "Legal form", "Ltd");
            SetTextInput(form, "Registration number", "registration-123");
        }
        else
        {
            SetTextInput(form, "First name", "New first");
            SetTextInput(form, "Last name", "New last");
            SetTextInput(form, "Prefix", "Dr");
            SetTextInput(form, "Suffix", "Jr");
            SetTextInput(form, "Date of birth", "1815-12-10");
        }
        SetSelect(form, "Contact type", "Email");
        SetTextInput(form, "Contact value", "new@example.test");
        SetSelect(form, "Identifier type", "Other");
        SetTextInput(form, "Identifier value", "new-identifier");
        form.Find("form").Submit();
        form.WaitForAssertion(() => api.UpdateRequests.Count.ShouldBeGreaterThan(0));
        form.InvokeAsync(form.Instance.Dispose).GetAwaiter().GetResult();
        return api.UpdateRequests[^1].Command;
    }

    private static PartyDetail EditSource(bool organization) => new()
    {
        Id = "edit-party", Type = organization ? PartyType.Organization : PartyType.Person,
        DisplayName = "Prior values", SortName = "Prior values", IsActive = true,
        PersonDetails = organization ? null : new() { FirstName = "Prior", LastName = "Values" },
        OrganizationDetails = organization ? new() { LegalName = "Prior organization" } : null,
        CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
    };

    private static PartyDetail ProjectedUpdate(UpdatePartyComposite update) => EditSource(update.OrganizationDetails is not null) with
    {
        PersonDetails = update.PersonDetails, OrganizationDetails = update.OrganizationDetails,
        ContactChannels = update.AddContactChannels.Select(c => new ContactChannel
        {
            Id = c.ContactChannelId, Type = c.Type, Value = c.Value, IsPreferred = c.IsPreferred,
        }).ToArray(),
        Identifiers = update.AddIdentifiers.Select(i => new PartyIdentifier
        {
            Id = i.IdentifierId, Type = i.Type, Value = i.Value,
        }).ToArray(),
        Freshness = new() { Status = ProjectionFreshnessStatus.Current },
    };

    private string SubmitAcceptedCreate(RecordingAdminPortalApiClient api)
    {
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>();
        form.WaitForAssertion(() => form.Markup.ShouldContain("Create party"));
        SetTextInput(form, "First name", "Katherine");
        SetTextInput(form, "Last name", "Johnson");
        form.Find("form").Submit();
        form.WaitForAssertion(() => api.CreateRequests.Count.ShouldBe(1));
        form.InvokeAsync(form.Instance.Dispose).GetAwaiter().GetResult();
        return api.CreateRequests[0].PartyId;
    }

    private void RenderAuthorizedTenant(string tenantId)
    {
        var state = new TenantLocalState
        {
            TenantId = tenantId,
            Status = TenantStatus.Active,
        };
        state.Members[AdminUserId] = TenantRole.TenantOwner;
        _tenantStore.SaveAsync(state).GetAwaiter().GetResult();
        _authProvider.SetAuthenticated(AdminUserId, tenantId);
    }

    private static void SetTextInput(IRenderedComponent<CreateEditPartyPage> cut, string label, string value)
    {
        IRenderedComponent<FluentTextInput> input = FindTextInput(cut, label);
        cut.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync(value)).GetAwaiter().GetResult();
    }

    private static void SetPartyType(IRenderedComponent<CreateEditPartyPage> cut, string value)
    {
        IRenderedComponent<FluentRadioGroup<string>> radioGroup = cut.FindComponent<FluentRadioGroup<string>>();
        cut.InvokeAsync(() => radioGroup.Instance.ValueChanged.InvokeAsync(value)).GetAwaiter().GetResult();
    }

    private static IRenderedComponent<FluentTextInput> FindTextInput(IRenderedComponent<CreateEditPartyPage> cut, string label)
        => cut.FindComponents<FluentTextInput>()
            .Single(input => string.Equals(input.Instance.Label, label, StringComparison.Ordinal));

    private static void SetSelect(IRenderedComponent<CreateEditPartyPage> cut, string label, string value)
    {
        IRenderedComponent<FluentSelect<string, string>> select = cut.FindComponents<FluentSelect<string, string>>()
            .Single(input => string.Equals(input.Instance.Label, label, StringComparison.Ordinal));
        cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(value)).GetAwaiter().GetResult();
    }

    private static void ShouldBeSortableUniqueId(string id)
    {
        DateTimeOffset timestamp = UniqueIdHelper.ExtractTimestamp(id);
        timestamp.ShouldBeGreaterThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(-1));
        timestamp.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(1));
        Guid.TryParse(id, out _).ShouldBeFalse();
    }

    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        private AuthenticationState _state = new(new ClaimsPrincipal(new ClaimsIdentity()));

        private Task<AuthenticationState>? _nextState;
        public Task<AuthenticationState>? NextState
        {
            get => Volatile.Read(ref _nextState);
            set => Volatile.Write(ref _nextState, value);
        }

        public void NotifyCurrentState() => NotifyAuthenticationStateChanged(Task.FromResult(_state));

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            Task<AuthenticationState>? next = Interlocked.Exchange(ref _nextState, null);
            return next ?? Task.FromResult(_state);
        }

        public void SetAuthenticated(string userId, string tenantId)
        {
            Claim[] claims =
            [
                new Claim(PartiesClaimTypes.Subject, userId),
                new Claim(PartiesClaimTypes.EventStoreTenant, tenantId),
            ];
            _state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public void SetUnauthenticated()
        {
            _state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public void SetAuthenticatedWithoutTenant(string userId)
        {
            Claim[] claims = [new Claim(PartiesClaimTypes.Subject, userId)];
            _state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}
