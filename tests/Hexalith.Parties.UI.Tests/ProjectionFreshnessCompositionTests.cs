using Hexalith.Parties.UI.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.7 AC4 — DI / lifetime / inert-boot composition (mirrors <c>SelfScopedPartiesClientCompositionTests</c>).
/// Every live-freshness service is Scoped (per circuit — ADR-030); the graph builds under
/// <c>ValidateScopes=true</c>; and a no-<c>EventStore:SignalR:HubUrl</c> / no-<c>Parties:BaseUrl</c>
/// configuration composes and resolves <strong>without connecting</strong> (lazy/inert).
/// </summary>
public sealed class ProjectionFreshnessCompositionTests
{
    [Theory]
    [InlineData(typeof(IProjectionStream))]
    [InlineData(typeof(IDegradedStateAccessor))]
    [InlineData(typeof(PartiesProjectionSubscription))]
    [InlineData(typeof(ProjectionFreshnessFallback))]
    [InlineData(typeof(OptimisticReconcile))]
    public void AddPartiesProjectionFreshness_RegistersServiceAsScoped(Type serviceType)
    {
        var services = new ServiceCollection();
        services.AddPartiesProjectionFreshness(EmptyConfiguration());

        services.ShouldContain(d => d.ServiceType == serviceType && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public async Task Graph_BuildsUnderValidateScopes_AndResolvesInertWithinScope()
    {
        using ServiceProvider provider = BuildProvider();

        // Scoped under ValidateScopes=true: resolving at the root throws; inside a scope it resolves.
        _ = Should.Throw<InvalidOperationException>(
            () => provider.GetRequiredService<OptimisticReconcile>());

        // The scope holds IAsyncDisposable-only services (the subscription / stream own connections), so it
        // must be disposed asynchronously.
        AsyncServiceScope scope = provider.CreateAsyncScope();
        try
        {
            IServiceProvider scoped = scope.ServiceProvider;
            scoped.GetRequiredService<OptimisticReconcile>().ShouldNotBeNull();
            scoped.GetRequiredService<PartiesProjectionSubscription>().ShouldNotBeNull();
            scoped.GetRequiredService<ProjectionFreshnessFallback>().ShouldNotBeNull();
            scoped.GetRequiredService<IDegradedStateAccessor>().ShouldNotBeNull();

            // Inert: no hub URL configured → nothing connects.
            scoped.GetRequiredService<IProjectionStream>().IsConnected.ShouldBeFalse();
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static IConfiguration EmptyConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection().Build();

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPartiesProjectionFreshness(EmptyConfiguration());

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
