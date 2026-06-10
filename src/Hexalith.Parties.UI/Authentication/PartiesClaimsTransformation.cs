using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;

namespace Hexalith.Parties.UI.Authentication;

/// <summary>
/// UI-host-local claims transformation (Story 1.4, AR-D2) that normalizes the tenant claim to
/// <see cref="PartiesUiAuthorization.TenantClaimType"/> so a Consumer's effective scope is
/// <c>{tenant, party_id}</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberate UI-local copy of the internal actor host's
/// <c>Hexalith.Parties.Authentication.PartiesClaimsTransformation</c>. The internal class lives in the
/// host-private <c>Hexalith.Parties</c> project, which the adopter-facing BFF (<c>Hexalith.Parties.UI</c>)
/// does <strong>not</strong> reference; reusing it would cross the adopter-facing↔internal boundary the
/// project-context pins (and trip a fitness test). The logic is reproduced, not referenced.
/// </para>
/// <para>
/// <strong>Idempotent / fail-closed.</strong> <see cref="IClaimsTransformation"/> can run multiple times
/// per request, so this short-circuits and returns the principal unchanged when
/// <see cref="PartiesUiAuthorization.TenantClaimType"/> is already present (the normal case — the
/// <c>hexalith-parties-ui</c> Keycloak client emits <c>eventstore:tenant</c> directly). Otherwise it
/// derives the tenant from a <c>tenants</c> JSON-array / space-delimited claim or <c>tenant_id</c>/
/// <c>tid</c>, adding only the missing claims. It never throws on missing input.
/// </para>
/// <para>
/// <strong>PII hygiene.</strong> Logs carry coarse counts only — never the tenant value, the
/// <c>party_id</c>, or any other claim value.
/// </para>
/// </remarks>
public sealed class PartiesClaimsTransformation(ILogger<PartiesClaimsTransformation> logger) : IClaimsTransformation
{
    /// <inheritdoc />
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // Idempotent short-circuit: the normalized tenant claim already exists, so do nothing. This is
        // the single biggest IClaimsTransformation footgun — the handler runs more than once per request.
        if (principal.HasClaim(c => c.Type == PartiesUiAuthorization.TenantClaimType))
        {
            return Task.FromResult(principal);
        }

        var identity = new ClaimsIdentity();

        AddTenantClaims(principal, identity);

        if (identity.Claims.Any())
        {
            principal.AddIdentity(identity);
        }

        logger.LogDebug(
            "Parties UI claims transformation normalized {TenantClaimCount} tenant claim(s).",
            identity.Claims.Count(c => c.Type == PartiesUiAuthorization.TenantClaimType));

        return Task.FromResult(principal);
    }

    private static void AddTenantClaims(ClaimsPrincipal principal, ClaimsIdentity identity)
    {
        AddClaimsFromJwt(principal, identity, "tenants", PartiesUiAuthorization.TenantClaimType);

        string? tenantId = principal.FindFirst("tenant_id")?.Value ?? principal.FindFirst("tid")?.Value;
        if (!string.IsNullOrEmpty(tenantId) && !identity.HasClaim(PartiesUiAuthorization.TenantClaimType, tenantId))
        {
            identity.AddClaim(new Claim(PartiesUiAuthorization.TenantClaimType, tenantId));
        }
    }

    private static void AddClaimsFromJwt(ClaimsPrincipal principal, ClaimsIdentity identity, string sourceClaimType, string targetClaimType)
    {
        Claim? sourceClaim = principal.FindFirst(sourceClaimType);
        if (sourceClaim is null)
        {
            return;
        }

        string value = sourceClaim.Value;

        if (value.StartsWith('['))
        {
            try
            {
                string[]? items = JsonSerializer.Deserialize<string[]>(value);
                if (items is not null)
                {
                    foreach (string item in items)
                    {
                        if (!string.IsNullOrEmpty(item))
                        {
                            identity.AddClaim(new Claim(targetClaimType, item));
                        }
                    }

                    return;
                }
            }
            catch (JsonException)
            {
                // Fall through to space-delimited parsing.
            }
        }

        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            identity.AddClaim(new Claim(targetClaimType, part));
        }
    }
}
