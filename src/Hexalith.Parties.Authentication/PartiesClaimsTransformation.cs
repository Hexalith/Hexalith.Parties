using System.Security.Claims;
using System.Text.Json;

using Hexalith.Parties.Contracts.Authorization;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Authentication;

public sealed class PartiesClaimsTransformation(ILogger<PartiesClaimsTransformation> logger) : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.HasClaim(claim => claim.Type == PartiesClaimTypes.EventStoreTenant))
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
            "Parties claims transformation normalized {TenantClaimCount} tenant claim(s).",
            identity.Claims.Count(claim => claim.Type == PartiesClaimTypes.EventStoreTenant));

        return Task.FromResult(principal);
    }

    private static void AddTenantClaims(ClaimsPrincipal principal, ClaimsIdentity identity)
    {
        AddClaimsFromJwt(principal, identity, "tenants");

        string? tenantId = principal.FindFirst("tenant_id")?.Value ?? principal.FindFirst("tid")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId)
            && !identity.HasClaim(PartiesClaimTypes.EventStoreTenant, tenantId))
        {
            identity.AddClaim(new Claim(PartiesClaimTypes.EventStoreTenant, tenantId));
        }
    }

    private static void AddClaimsFromJwt(ClaimsPrincipal principal, ClaimsIdentity identity, string sourceClaimType)
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
                        if (!string.IsNullOrWhiteSpace(item))
                        {
                            identity.AddClaim(new Claim(PartiesClaimTypes.EventStoreTenant, item));
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
            identity.AddClaim(new Claim(PartiesClaimTypes.EventStoreTenant, part));
        }
    }
}
