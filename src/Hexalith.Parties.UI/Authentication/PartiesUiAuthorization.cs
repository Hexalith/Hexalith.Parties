using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Parties.UI.Authentication;

/// <summary>
/// Single source of truth for the Parties UI host's area-level authorization (Story 1.3, AR-D2).
/// Holds the role-claim-based <c>Admin</c> and <c>Consumer</c> policy names, the role names each
/// policy accepts, and the DI registration that wires both policies into the container.
/// </summary>
/// <remarks>
/// <para>
/// The <c>Admin</c> policy mirrors the actor host's Admin role set
/// (<c>Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs</c> →
/// <c>RequireRole("admin","Admin","administrator","Administrator")</c>) PLUS <c>TenantOwner</c>, per
/// the epic's "Admin or TenantOwner" landing rule. The new <c>Consumer</c> policy accepts the
/// <c>Consumer</c> role. Gating is role-claim based (not tenant-membership based — that is a deeper,
/// out-of-scope concern Epic 2 reconciles).
/// </para>
/// <para>
/// <c>RoleLandingRedirect</c>, the nav-entry registration, and the per-area
/// <c>[Authorize(Policy = …)]</c> attributes all reference these constants/arrays so role and policy
/// names are never re-hardcoded.
/// </para>
/// </remarks>
public static class PartiesUiAuthorization
{
    /// <summary>The authorization policy name gating the Admin area and Admin navigation entry.</summary>
    public const string AdminPolicy = "Admin";

    /// <summary>The authorization policy name gating the Consumer area and Consumer navigation entry.</summary>
    public const string ConsumerPolicy = "Consumer";

    /// <summary>
    /// Role names that satisfy the <see cref="AdminPolicy"/>. Mirrors the actor host's Admin role set
    /// plus <c>TenantOwner</c> (case variants included to tolerate provider casing).
    /// </summary>
    public static readonly string[] AdminRoleNames =
        ["Admin", "admin", "Administrator", "administrator", "TenantOwner", "tenantowner"];

    /// <summary>Role names that satisfy the <see cref="ConsumerPolicy"/>.</summary>
    public static readonly string[] ConsumerRoleNames = ["Consumer", "consumer"];

    /// <summary>
    /// The claim type carrying a Consumer's verified party binding (Story 1.4, AR-D2). Resolved
    /// fail-closed by <see cref="PartyIdClaimResolver"/>: a bound Consumer carries exactly one
    /// non-empty <c>party_id</c> claim. The claim itself is issued by the AR-Gap-Binding mechanism
    /// (Stories 4.1/4.2); this host only consumes it. Single source of truth — never re-hardcode the
    /// literal anywhere (resolver, transformation, page, tests all reference this const).
    /// </summary>
    public const string PartyIdClaimType = "party_id";

    /// <summary>
    /// The normalized tenant claim type (Story 1.4, AR-D2). The <c>hexalith-parties-ui</c> Keycloak
    /// client emits <c>eventstore:tenant</c> directly; <see cref="PartiesClaimsTransformation"/>
    /// defensively derives it from <c>tenants</c>/<c>tenant_id</c>/<c>tid</c> when a provider does not.
    /// Captured into a bound Consumer's effective scope <c>{tenant, party_id}</c>. Single source of
    /// truth — never re-hardcode the literal anywhere.
    /// </summary>
    public const string TenantClaimType = "eventstore:tenant";

    /// <summary>
    /// Registers the <see cref="AdminPolicy"/> and <see cref="ConsumerPolicy"/> via
    /// <see cref="PolicyServiceCollectionExtensions.AddAuthorizationCore(IServiceCollection, System.Action{AuthorizationOptions})"/>.
    /// Call unconditionally (not gated on whether interactive OIDC sign-in is wired) so
    /// <c>&lt;AuthorizeView Policy=…&gt;</c> and <c>[Authorize(Policy = …)]</c> resolve the policies in
    /// every boot mode (tests, degraded boot, full sign-in). <c>AddAuthorizationCore</c> is additive,
    /// so calling it again only accumulates the two new policies.
    /// </summary>
    /// <param name="services">The service collection to register the policies into.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddPartiesUiAuthorization(this IServiceCollection services)
        => services.AddAuthorizationCore(options =>
        {
            options.AddPolicy(AdminPolicy, policy => policy.RequireRole(AdminRoleNames));
            options.AddPolicy(ConsumerPolicy, policy => policy.RequireRole(ConsumerRoleNames));
        });

    /// <summary>
    /// Registers the Story 1.4 fail-closed claim-resolution services (AR-D2): the Scoped
    /// <see cref="PartyIdClaimResolver"/> and the <see cref="PartiesClaimsTransformation"/> as an
    /// <see cref="IClaimsTransformation"/>. Call unconditionally (not gated on whether interactive OIDC
    /// sign-in is wired) so the resolver resolves in every boot mode (tests, degraded boot, full
    /// sign-in); <see cref="IClaimsTransformation"/> is only <em>invoked</em> when
    /// <c>UseAuthentication</c> is wired, so unconditional registration is harmless.
    /// </summary>
    /// <remarks>
    /// Both are <strong>Scoped</strong> (ADR-030): the host boots with <c>ValidateScopes=true</c>, which
    /// fails closed if a Singleton captures these per-request, claims-derived services. Do not register
    /// them as Singletons.
    /// </remarks>
    /// <param name="services">The service collection to register the resolution services into.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddPartiesUiClaimsResolution(this IServiceCollection services)
    {
        services.AddScoped<PartyIdClaimResolver>();
        services.AddScoped<IClaimsTransformation, PartiesClaimsTransformation>();
        return services;
    }
}
