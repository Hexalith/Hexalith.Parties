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
        SetTextInput(cut, "First name", "Ada");
        SetTextInput(cut, "Last name", "Lovelace");

        SetPartyType(cut, "Organization");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Organization details"));
        SetTextInput(cut, "Legal name", "Analytical Engines Ltd");

        SetPartyType(cut, "Person");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Person details"));
        FindTextInput(cut, "First name").Instance.Value.ShouldBe("Ada");
        FindTextInput(cut, "Last name").Instance.Value.ShouldBe("Lovelace");

        SetPartyType(cut, "Organization");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Organization details"));
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

        public void SetAuthenticatedWithoutTenant(string userId)
        {
            Claim[] claims = [new Claim(PartiesClaimTypes.Subject, userId)];
            _state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}
