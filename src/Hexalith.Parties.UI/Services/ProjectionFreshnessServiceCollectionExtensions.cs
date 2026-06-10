using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// DI registration for the Story 1.7 live-freshness mechanism (AR-D6): the SignalR projection subscription,
/// the optimistic-reconcile primitive, and the degraded fallback / header seam.
/// </summary>
public static class ProjectionFreshnessServiceCollectionExtensions
{
    /// <summary>
    /// Registers the live-freshness building blocks <strong>Scoped</strong> (per circuit — ADR-030;
    /// <c>ValidateScopes=true</c> must still boot green): <see cref="IProjectionStream"/> →
    /// <see cref="EventStoreSignalRProjectionStream"/>, <see cref="PartiesProjectionSubscription"/>,
    /// <see cref="IDegradedStateAccessor"/>, <see cref="ProjectionFreshnessFallback"/>, and
    /// <see cref="OptimisticReconcile"/>.
    /// </summary>
    /// <remarks>
    /// Call <strong>unconditionally</strong> (mirroring <c>AddSelfScopedPartiesClient</c> /
    /// <c>AddPartiesUiClaimsResolution</c>): composition is <strong>lazy/inert</strong> — the stream stays
    /// inert when the hub URL is empty/whitespace (no connect), and nothing resolves until a screen consumes
    /// the primitive, so a test / degraded boot (no <c>EventStore:SignalR:HubUrl</c>, no
    /// <c>Parties:BaseUrl</c>) composes cleanly.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration carrying <c>EventStore:SignalR:HubUrl</c> and <c>Parties:Freshness:PollingIntervalSeconds</c>.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddPartiesProjectionFreshness(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        _ = services
            .AddOptions<ProjectionFreshnessOptions>()
            .Configure(options =>
            {
                options.HubUrl = configuration["EventStore:SignalR:HubUrl"];

                if (int.TryParse(configuration["Parties:Freshness:PollingIntervalSeconds"], out int seconds) && seconds > 0)
                {
                    options.PollingIntervalSeconds = seconds;
                }
            });

        // TimeProvider is already a Singleton in the FrontComposer graph; TryAdd keeps this extension
        // self-contained (e.g. the composition test) without double-registering.
        services.TryAddSingleton(TimeProvider.System);

        // The access-token seam defaults to inert (null token); the live per-circuit OIDC capture lands
        // with the first authenticated data screen (Epic 2/4) — see IProjectionAccessTokenProvider.
        services.TryAddScoped<IProjectionAccessTokenProvider, NullProjectionAccessTokenProvider>();

        services.TryAddScoped<IProjectionStream, EventStoreSignalRProjectionStream>();
        services.TryAddScoped<IDegradedStateAccessor, DegradedStateAccessor>();
        services.TryAddScoped<PartiesProjectionSubscription>();
        services.TryAddScoped<ProjectionFreshnessFallback>();
        services.TryAddScoped<OptimisticReconcile>();

        return services;
    }
}
