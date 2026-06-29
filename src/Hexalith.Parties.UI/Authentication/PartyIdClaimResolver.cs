using System.Security.Claims;

using Hexalith.Parties.Contracts.Authorization;

namespace Hexalith.Parties.UI.Authentication;

/// <summary>
/// Resolves a Consumer principal's verified <see cref="PartiesUiAuthorization.PartyIdClaimType"/> claim
/// to a single bound party, <strong>fail-closed</strong> (Story 1.4, AR-D2).
/// </summary>
/// <remarks>
/// Pure and dependency-free — it reads only the passed-in principal — so it is trivially Scoped and
/// testable. Registered via <see cref="PartiesUiAuthorization.AddPartiesUiClaimsResolution"/>;
/// <c>RoleLandingRedirect</c> uses it to divert an unbound Consumer away from the <c>/me</c> data area to
/// the <c>NoPartyBinding</c> state.
/// </remarks>
public sealed class PartyIdClaimResolver
{
    /// <summary>
    /// Resolves the party binding for <paramref name="user"/>, fail-closed.
    /// </summary>
    /// <param name="user">The authenticated principal to resolve.</param>
    /// <returns>
    /// <see cref="PartyBindingResult.Bound"/> only when the principal carries <strong>exactly one</strong>
    /// non-null/non-whitespace <see cref="PartiesUiAuthorization.PartyIdClaimType"/> claim. Zero claims, an
    /// empty/whitespace value, or two-or-more <c>party_id</c> claims (ambiguous binding) all resolve to
    /// <see cref="PartyBindingResult.Unbound"/>.
    /// </returns>
    public PartyBindingResult Resolve(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        PartiesClaimExtractionResult partyId = user.TryGetPartyId();
        PartiesClaimExtractionResult tenant = user.TryGetTenantId();

        // Fail closed: a single bound party_id and a single normalized tenant are required before a
        // Consumer reaches a data screen.
        if (!partyId.Succeeded || !tenant.Succeeded)
        {
            return PartyBindingResult.Unbound;
        }

        return PartyBindingResult.Bound(tenant.Value!, partyId.Value!);
    }
}

/// <summary>
/// The outcome of <see cref="PartyIdClaimResolver.Resolve"/>: a Consumer's effective scope
/// <c>{tenant, party_id}</c> when bound, or the fail-closed unbound state.
/// </summary>
/// <param name="IsBound">Whether the principal resolved to exactly one bound party.</param>
/// <param name="Tenant">The normalized tenant value when bound; otherwise <see langword="null"/>.</param>
/// <param name="PartyId">The single bound <c>party_id</c> when bound; otherwise <see langword="null"/>.</param>
public sealed record PartyBindingResult(bool IsBound, string? Tenant, string? PartyId)
{
    /// <summary>The fail-closed unbound result (no binding, or an ambiguous/invalid one).</summary>
    public static PartyBindingResult Unbound { get; } = new(false, null, null);

    /// <summary>Creates a bound result for the resolved <paramref name="tenant"/> and <paramref name="partyId"/>.</summary>
    public static PartyBindingResult Bound(string tenant, string partyId) => new(true, tenant, partyId);
}
