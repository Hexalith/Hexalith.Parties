using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// DI registration for the Story 1.5 consumer self-scope accessor.
/// </summary>
public static class SelfScopedPartiesClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISelfScopedPartiesClient"/> → <see cref="SelfScopedPartiesClient"/> as
    /// <strong>Scoped</strong> (ADR-030). Never Singleton: the accessor captures the per-circuit
    /// <see cref="AuthenticationStateProvider"/> and Scoped <see cref="PartyIdClaimResolver"/>, and the
    /// host boots with <c>ValidateScopes=true</c> — a Singleton capture would fail the boot. Call
    /// unconditionally (mirroring <c>AddPartiesUiAuthorization</c>/<c>AddPartiesUiClaimsResolution</c>)
    /// so tests and degraded boot can compose it; its gateway-client dependencies are resolved lazily,
    /// only when a consumer page actually consumes the accessor.
    /// </summary>
    /// <param name="services">The service collection to register the accessor into.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSelfScopedPartiesClient(this IServiceCollection services)
    {
        services.AddScoped<ISelfScopedPartiesClient, SelfScopedPartiesClient>();
        return services;
    }
}
