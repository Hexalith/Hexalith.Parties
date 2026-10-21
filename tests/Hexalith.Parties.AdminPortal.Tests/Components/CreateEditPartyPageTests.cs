using System.Security.Claims;

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

    private string SubmitAcceptedCreate(RecordingAdminPortalApiClient api)
    {
        using IRenderedComponent<CreateEditPartyPage> form = Render<CreateEditPartyPage>();
        form.WaitForAssertion(() => form.Markup.ShouldContain("Create party"));
        SetTextInput(form, "First name", "Katherine");
        SetTextInput(form, "Last name", "Johnson");
        form.Find("form").Submit();
        form.WaitForAssertion(() => api.CreateRequests.Count.ShouldBe(1));
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

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);

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
