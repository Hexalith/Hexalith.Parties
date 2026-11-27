using System.Text.Json;

using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.UI.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class PartiesAdminPortalE2eFixtureTests
{
    [Theory]
    [InlineData("true", "Test", true)]
    [InlineData("false", "Test", false)]
    [InlineData("true", "Development", false)]
    [InlineData("true", "Production", false)]
    public void FixtureRequiresExplicitConfigurationAndTestEnvironment(string enabled, string environmentName, bool expected)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PartiesAdminPortalE2eFixture.EnabledConfigurationKey] = enabled,
            })
            .Build();
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        PartiesAdminPortalE2eFixture.IsEnabled(configuration, environment).ShouldBe(expected);
    }

    [Fact]
    public async Task FixtureShellScopeWithoutCookiePreservesAnonymousAuthorizationAsync()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var shellContext = new PartiesAdminPortalE2eUserContextAccessor(accessor);
        var authentication = new PartiesAdminPortalE2eAuthenticationStateProvider(accessor);

        shellContext.TenantId.ShouldBe("test-tenant");
        shellContext.UserId.ShouldBe("anonymous-e2e");
        (await authentication.GetAuthenticationStateAsync()).User.Identity.ShouldNotBeNull().IsAuthenticated.ShouldBeFalse();
        (await new PartiesAdminPortalE2eAuthorizationService(accessor).GetAuthorizationStateAsync())
            .ShouldBe(AdminPortalAuthorizationState.Unauthenticated);
    }

    [Fact]
    public async Task FixtureShellScopePreservesConsumerMissingTenantClaimsAndRoleRestrictionsAsync()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Request.Headers.Cookie = $"{PartiesAdminPortalE2eFixture.ConsumerCookieName}=no-tenant";
        var shellContext = new PartiesAdminPortalE2eUserContextAccessor(accessor);
        var authentication = new PartiesAdminPortalE2eAuthenticationStateProvider(accessor);

        shellContext.UserId.ShouldBe("consumer-e2e");
        var principal = (await authentication.GetAuthenticationStateAsync()).User;
        principal.FindFirst(PartiesClaimTypes.EventStoreTenant).ShouldBeNull();
        principal.IsInRole(PartiesRoles.Consumer).ShouldBeTrue();
        principal.IsInRole(PartiesRoles.Admin).ShouldBeFalse();
        (await new PartiesAdminPortalE2eAuthorizationService(accessor).GetAuthorizationStateAsync())
            .ShouldBe(AdminPortalAuthorizationState.Unauthenticated);
    }

    [Fact]
    public async Task AuthenticationHandler_WithAdminFixtureCookie_AuthenticatesRequestAsync()
    {
        var accessor = new HttpContextAccessor();
        using ServiceProvider provider = BuildAuthenticationProvider(accessor);
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Headers.Cookie = $"{PartiesAdminPortalE2eFixture.AdminCookieName}=enabled";
        accessor.HttpContext = context;

        AuthenticateResult result = await context
            .AuthenticateAsync(PartiesAdminPortalE2eAuthenticationHandler.AuthenticationScheme)
            .ConfigureAwait(true);

        result.Succeeded.ShouldBeTrue();
        result.Principal.ShouldNotBeNull().IsInRole(PartiesRoles.Admin).ShouldBeTrue();
    }

    [Fact]
    public async Task AuthenticationHandler_WithoutFixtureCookie_PreservesChallengeReturnUrlAsync()
    {
        var accessor = new HttpContextAccessor();
        using ServiceProvider provider = BuildAuthenticationProvider(accessor);
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = "/admin/parties";
        accessor.HttpContext = context;

        await context
            .ChallengeAsync(PartiesAdminPortalE2eAuthenticationHandler.AuthenticationScheme)
            .ConfigureAwait(true);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        context.Response.Headers.Location.ToString()
            .ShouldBe("/authentication/challenge?returnUrl=%2Fadmin%2Fparties");
    }

    [Fact]
    public async Task AuthorizationService_ReturnsUnauthenticatedWithoutFixtureCookieAsync()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        var service = new PartiesAdminPortalE2eAuthorizationService(accessor);

        AdminPortalAuthorizationState state = await service.GetAuthorizationStateAsync();

        state.ShouldBe(AdminPortalAuthorizationState.Unauthenticated);
    }

    [Fact]
    public async Task AuthorizationService_ReturnsAdminOnlyWhenFixtureCookieIsPresentAsync()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        accessor.HttpContext.Request.Headers.Cookie = $"{PartiesAdminPortalE2eFixture.AdminCookieName}=enabled";
        var service = new PartiesAdminPortalE2eAuthorizationService(accessor);

        AdminPortalAuthorizationState state = await service.GetAuthorizationStateAsync();

        state.IsAuthenticated.ShouldBeTrue();
        state.HasTenantContext.ShouldBeTrue();
        state.IsAdmin.ShouldBeTrue();
        state.ContextSignature.ShouldBe("tenant:test-tenant:user:admin-e2e:admin");
    }

    [Fact]
    public async Task E2eApiClient_ExportPartyDataAsync_ReturnsParseablePortabilityPackageAsync()
    {
        var fixtureState = new PartiesAdminPortalE2eFixtureState();
        var client = new PartiesAdminPortalE2eApiClient(
            fixtureState,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        AdminPortalExportDownload download = await client.ExportPartyDataAsync("party-bound-001", CancellationToken.None);

        PartyDataPortabilityPackage? package = JsonSerializer.Deserialize<PartyDataPortabilityPackage>(
            download.Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        package.ShouldNotBeNull();
        package.PartyId.ShouldBe("party-bound-001");
        package.TenantId.ShouldBe("test-tenant");
        package.Status.ShouldBe("Erased");
        package.ExportedAt.ShouldBe(new DateTimeOffset(2026, 06, 10, 10, 00, 00, TimeSpan.Zero));
        package.ExportedBy.ShouldBe("consumer-e2e");
        package.CorrelationId.ShouldBe("corr-export-e2e");
        fixtureState.Snapshot().ExportRequests.Single().PartyId.ShouldBe("party-bound-001");
    }

    [Fact]
    public async Task ConsumerProfileLoadGate_BlocksInteractiveReadOnceWithoutBlockingSsrPrerenderAsync()
    {
        var fixtureState = new PartiesAdminPortalE2eFixtureState();
        fixtureState.ConsumerProfileLoadGate.Arm();

        var ssrContext = new DefaultHttpContext();
        ssrContext.Request.Path = "/me/edit";
        var ssrClient = new PartiesAdminPortalE2ePartiesQueryClient(
            fixtureState,
            new HttpContextAccessor { HttpContext = ssrContext });

        Task<PartyDetail> ssrRead = ssrClient.GetPartyAsync("party-bound-001", TestContext.Current.CancellationToken);
        ssrRead.IsCompletedSuccessfully.ShouldBeTrue();
        (await ssrRead.ConfigureAwait(true)).Id.ShouldBe("party-bound-001");

        var circuitContext = new DefaultHttpContext();
        circuitContext.Request.Path = "/_blazor";
        var interactiveClient = new PartiesAdminPortalE2ePartiesQueryClient(
            fixtureState,
            new HttpContextAccessor { HttpContext = circuitContext });

        Task<PartyDetail> gatedRead = interactiveClient.GetPartyAsync("party-bound-001", TestContext.Current.CancellationToken);
        gatedRead.IsCompleted.ShouldBeFalse();

        fixtureState.ConsumerProfileLoadGate.Release();
        PartyDetail released = await gatedRead.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
        released.Id.ShouldBe("party-bound-001");

        Task<PartyDetail> secondRead = interactiveClient.GetPartyAsync("party-bound-001", TestContext.Current.CancellationToken);
        secondRead.IsCompletedSuccessfully.ShouldBeTrue();
        (await secondRead.ConfigureAwait(true)).Id.ShouldBe("party-bound-001");
    }

    [Fact]
    public async Task ConsumerProfileLoadGate_ResetReleasesInFlightInteractiveReadAsync()
    {
        var fixtureState = new PartiesAdminPortalE2eFixtureState();
        fixtureState.ConsumerProfileLoadGate.Arm();

        var circuitContext = new DefaultHttpContext();
        circuitContext.Request.Path = "/_blazor";
        var interactiveClient = new PartiesAdminPortalE2ePartiesQueryClient(
            fixtureState,
            new HttpContextAccessor { HttpContext = circuitContext });

        Task<PartyDetail> gatedRead = interactiveClient.GetPartyAsync("party-bound-001", TestContext.Current.CancellationToken);
        gatedRead.IsCompleted.ShouldBeFalse();

        fixtureState.Reset();
        PartyDetail released = await gatedRead.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
        released.Id.ShouldBe("party-bound-001");

        Task<PartyDetail> followUpRead = interactiveClient.GetPartyAsync("party-bound-001", TestContext.Current.CancellationToken);
        followUpRead.IsCompletedSuccessfully.ShouldBeTrue();
        (await followUpRead.ConfigureAwait(true)).Id.ShouldBe("party-bound-001");
    }

    [Fact]
    public async Task ConsumerProfileLoadGate_AlreadyCanceledTokenPreservesArmedGateAsync()
    {
        var fixtureState = new PartiesAdminPortalE2eFixtureState();
        fixtureState.ConsumerProfileLoadGate.Arm();

        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(
            () => fixtureState.ConsumerProfileLoadGate.WaitAsync(canceled.Token)).ConfigureAwait(true);

        Task stillArmed = fixtureState.ConsumerProfileLoadGate.WaitAsync(TestContext.Current.CancellationToken);
        stillArmed.IsCompleted.ShouldBeFalse();

        fixtureState.ConsumerProfileLoadGate.Release();
        await stillArmed.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Theory]
    [InlineData(null, null, "party-bound-001", ProjectionFreshnessStatus.Current)]
    [InlineData("bound", null, "party-bound-001", ProjectionFreshnessStatus.Current)]
    [InlineData("bound", "unknown", "party-bound-001", ProjectionFreshnessStatus.Current)]
    [InlineData("bound", "stale", "party-bound-001", ProjectionFreshnessStatus.Stale)]
    [InlineData("bound", "stale", "ada-lovelace", ProjectionFreshnessStatus.Current)]
    [InlineData(null, "stale", "party-bound-001", ProjectionFreshnessStatus.Current)]
    public async Task ConsumerProfileFreshnessCookie_ChangesOnlyTheBoundConsumerProfileAsync(
        string? consumerState,
        string? freshnessMode,
        string partyId,
        ProjectionFreshnessStatus expected)
    {
        var context = new DefaultHttpContext();
        List<string> cookies = [];
        if (consumerState is not null)
        {
            cookies.Add($"{PartiesAdminPortalE2eFixture.ConsumerCookieName}={consumerState}");
        }

        if (freshnessMode is not null)
        {
            cookies.Add($"{PartiesAdminPortalE2eFixture.ConsumerFreshnessModeCookieName}={freshnessMode}");
        }

        context.Request.Headers.Cookie = string.Join("; ", cookies);
        var client = new PartiesAdminPortalE2ePartiesQueryClient(
            new PartiesAdminPortalE2eFixtureState(),
            new HttpContextAccessor { HttpContext = context });

        PartyDetail profile = await client.GetPartyAsync(partyId, TestContext.Current.CancellationToken).ConfigureAwait(true);

        profile.Freshness.ShouldNotBeNull().Status.ShouldBe(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsumerProfileSaveGate_HoldsOneBoundResponseAndReleasesOnResetAsync(bool reset)
    {
        const string boundPartyId = "party-bound-001";
        var fixtureState = new PartiesAdminPortalE2eFixtureState();
        var client = new PartiesAdminPortalE2ePartiesCommandClient(fixtureState);
        var update = new UpdatePartyComposite
        {
            PartyId = boundPartyId,
            PersonDetails = new PersonDetails { FirstName = "Updated", LastName = "Consumer" },
        };
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        fixtureState.ConsumerProfileSaveGate.Arm();

        const string otherPartyId = "ada-lovelace";
        var otherSave = client.UpdatePartyCompositeWithResultAsync(otherPartyId, update with { PartyId = otherPartyId }, cancellationToken);
        otherSave.IsCompletedSuccessfully.ShouldBeTrue();
        (await otherSave.ConfigureAwait(true)).Payload.ShouldNotBeNull().Id.ShouldBe(otherPartyId);

        var gatedSave = client.UpdatePartyCompositeWithResultAsync(boundPartyId, update, cancellationToken);
        gatedSave.IsCompleted.ShouldBeFalse();
        fixtureState.Snapshot().UpdateRequests.Select(request => request.PartyId).ShouldBe([otherPartyId, boundPartyId]);

        var concurrentSave = client.UpdatePartyCompositeWithResultAsync(boundPartyId, update, cancellationToken);
        concurrentSave.IsCompletedSuccessfully.ShouldBeTrue();
        (await concurrentSave.ConfigureAwait(true)).Payload.ShouldNotBeNull().Id.ShouldBe(boundPartyId);
        gatedSave.IsCompleted.ShouldBeFalse();

        if (reset)
        {
            fixtureState.Reset();
        }
        else
        {
            fixtureState.ConsumerProfileSaveGate.Release();
        }

        var released = await gatedSave.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(true);
        released.Payload.ShouldNotBeNull().DisplayName.ShouldBe("Updated Consumer");
        fixtureState.Snapshot().UpdateRequests.Count.ShouldBe(reset ? 0 : 3);

        var nextSave = client.UpdatePartyCompositeWithResultAsync(boundPartyId, update, cancellationToken);
        nextSave.IsCompletedSuccessfully.ShouldBeTrue();
        (await nextSave.ConfigureAwait(true)).Payload.ShouldNotBeNull().Id.ShouldBe(boundPartyId);
    }

    private static ServiceProvider BuildAuthenticationProvider(IHttpContextAccessor accessor)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(accessor);
        services
            .AddAuthentication(PartiesAdminPortalE2eAuthenticationHandler.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, PartiesAdminPortalE2eAuthenticationHandler>(
                PartiesAdminPortalE2eAuthenticationHandler.AuthenticationScheme,
                _ => { });
        return services.BuildServiceProvider();
    }
}
