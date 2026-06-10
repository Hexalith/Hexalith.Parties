using System.Security.Claims;

using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.UI.Authentication;
using Hexalith.Parties.UI.Services;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.5 AC5 (DI / ValidateScopes) — proves the self-scope accessor is <strong>Scoped</strong>
/// (never Singleton). The registered <see cref="ServiceDescriptor"/> lifetime is pinned directly, and a
/// container built with <c>ValidateScopes=true</c> (ADR-030 parity with the host) refuses to resolve the
/// accessor at the root (a Singleton-capture would be caught at boot) yet resolves it inside a scope.
/// </summary>
public sealed class SelfScopedPartiesClientCompositionTests
{
    [Fact]
    public void AddSelfScopedPartiesClient_RegistersAccessorAsScoped()
    {
        // ADR-030: the accessor MUST be Scoped, never Singleton. Pin the registered lifetime directly so
        // a Singleton regression fails the build even where ValidateScopes alone could not catch it.
        var services = new ServiceCollection();
        services.AddSelfScopedPartiesClient();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ISelfScopedPartiesClient)
            && d.ImplementationType == typeof(SelfScopedPartiesClient)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Accessor_DoesNotResolveAtRoot_ButResolvesInsideScope()
    {
        using ServiceProvider provider = BuildProvider();

        // Scoped under ValidateScopes=true: resolving at the root container throws.
        Should.Throw<InvalidOperationException>(
            () => provider.GetRequiredService<ISelfScopedPartiesClient>());

        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ISelfScopedPartiesClient>().ShouldNotBeNull();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IPartiesQueryClient>());
        services.AddSingleton(Substitute.For<IPartiesCommandClient>());
        services.AddSingleton(Substitute.For<IAdminPortalGdprClient>());
        services.AddSingleton<AuthenticationStateProvider>(
            new FakeAuthStateProvider(new ClaimsPrincipal(new ClaimsIdentity())));
        services.AddPartiesUiClaimsResolution();
        services.AddSelfScopedPartiesClient();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
