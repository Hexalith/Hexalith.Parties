using System.Security.Claims;

using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Contracts.Enums;

using Microsoft.AspNetCore.Components.Authorization;

using Shouldly;

namespace Hexalith.Parties.AdminPortal.Tests.Services;

/// <summary>Protects authorization results and subscription cleanup across authentication changes.</summary>
public sealed class AdminPortalAuthorizationServiceTests
{
    /// <summary>A sign-out during a tenant read cannot return the old administrator grant.</summary>
    [Fact]
    public async Task AuthenticationChangeDuringTenantReadRefusesStaleAdministratorAsync()
    {
        var provider = new ControllableAuthenticationStateProvider(Task.FromResult(Administrator()));
        var pending = new TaskCompletionSource<TenantLocalState?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new AuthorizationTenantProjectionStore((_, _) => pending.Task);
        var service = new AdminPortalAuthorizationService(provider, store);

        Task<AdminPortalAuthorizationState> authorization = service.GetAuthorizationStateAsync(TestContext.Current.CancellationToken);
        provider.SubscriberCount.ShouldBe(1);
        provider.Publish(Task.FromResult(Anonymous()));
        pending.SetResult(ActiveTenant());

        (await authorization.ConfigureAwait(true)).ShouldBe(AdminPortalAuthorizationState.Unauthenticated);
        provider.SubscriberCount.ShouldBe(0);
    }

    /// <summary>A late initial state cannot overwrite a newer notification.</summary>
    [Fact]
    public async Task LateInitialAuthenticationCannotOverwriteNewerStateAsync()
    {
        var initial = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new ControllableAuthenticationStateProvider(initial.Task);
        var store = new AuthorizationTenantProjectionStore((_, _) => throw new InvalidOperationException("Stale tenant read."));
        var service = new AdminPortalAuthorizationService(provider, store);

        Task<AdminPortalAuthorizationState> authorization = service.GetAuthorizationStateAsync(TestContext.Current.CancellationToken);
        provider.Publish(Task.FromResult(Anonymous()));
        initial.SetResult(Administrator());

        (await authorization.ConfigureAwait(true)).ShouldBe(AdminPortalAuthorizationState.Unauthenticated);
        provider.SubscriberCount.ShouldBe(0);
    }

    /// <summary>Every independent call observes the current provider state.</summary>
    [Fact]
    public async Task IndependentCallsObserveCurrentAuthenticationAsync()
    {
        var provider = new ControllableAuthenticationStateProvider(Task.FromResult(Administrator()));
        var store = new AuthorizationTenantProjectionStore((_, _) => Task.FromResult<TenantLocalState?>(ActiveTenant()));
        var service = new AdminPortalAuthorizationService(provider, store);

        (await service.GetAuthorizationStateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).IsAdmin.ShouldBeTrue();
        provider.Publish(Task.FromResult(Anonymous()));
        (await service.GetAuthorizationStateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).IsAdmin.ShouldBeFalse();
        provider.SubscriberCount.ShouldBe(0);
    }

    /// <summary>Failed reads release the real provider event subscription.</summary>
    [Fact]
    public async Task TenantReadFailureRemovesAuthenticationSubscriptionAsync()
    {
        var provider = new ControllableAuthenticationStateProvider(Task.FromResult(Administrator()));
        var store = new AuthorizationTenantProjectionStore((_, _) => throw new InvalidOperationException("Read failed."));
        var service = new AdminPortalAuthorizationService(provider, store);

        await Should.ThrowAsync<InvalidOperationException>(() => service.GetAuthorizationStateAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
        provider.SubscriberCount.ShouldBe(0);
    }

    /// <summary>Cancelled authentication reads release the provider subscription promptly.</summary>
    [Fact]
    public async Task CancellationRemovesAuthenticationSubscriptionAsync()
    {
        var pending = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new ControllableAuthenticationStateProvider(pending.Task);
        var store = new AuthorizationTenantProjectionStore((_, _) => throw new InvalidOperationException("Unexpected tenant read."));
        var service = new AdminPortalAuthorizationService(provider, store);
        using var cancellation = new CancellationTokenSource();

        Task<AdminPortalAuthorizationState> authorization = service.GetAuthorizationStateAsync(cancellation.Token);
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => authorization).ConfigureAwait(true);
        provider.SubscriberCount.ShouldBe(0);
    }

    private static AuthenticationState Anonymous() => new(new ClaimsPrincipal(new ClaimsIdentity()));

    private static AuthenticationState Administrator() => new(new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(PartiesClaimTypes.Subject, "admin-user"), new Claim(PartiesClaimTypes.EventStoreTenant, "tenant-a")], "test")));

    private static TenantLocalState ActiveTenant()
    {
        var tenant = new TenantLocalState { TenantId = "tenant-a", Status = TenantStatus.Active };
        tenant.Members["admin-user"] = TenantRole.TenantOwner;
        return tenant;
    }
}
