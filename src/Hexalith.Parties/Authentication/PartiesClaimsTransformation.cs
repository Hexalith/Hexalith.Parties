using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;

namespace Hexalith.Parties.Authentication;

public sealed class PartiesClaimsTransformation(ILogger<PartiesClaimsTransformation> logger) : IClaimsTransformation
{
    internal const string TenantClaimType = "eventstore:tenant";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.HasClaim(c => c.Type == TenantClaimType))
        {
            return Task.FromResult(principal);
        }

        var identity = new ClaimsIdentity();

        AddTenantClaims(principal, identity);

        if (identity.Claims.Any())
        {
            principal.AddIdentity(identity);
        }

        string subject = principal.FindFirst("sub")?.Value ?? "unknown";
        int tenantCount = identity.Claims.Count(c => c.Type == TenantClaimType);

        logger.LogDebug(
            "Claims transformation for Subject={Subject}: Tenants={TenantCount}",
            subject,
            tenantCount);

        return Task.FromResult(principal);
    }

    private static void AddTenantClaims(ClaimsPrincipal principal, ClaimsIdentity identity)
    {
        AddClaimsFromJwt(principal, identity, "tenants", TenantClaimType);

        string? tenantId = principal.FindFirst("tenant_id")?.Value ?? principal.FindFirst("tid")?.Value;
        if (!string.IsNullOrEmpty(tenantId) && !identity.HasClaim(TenantClaimType, tenantId))
        {
            identity.AddClaim(new Claim(TenantClaimType, tenantId));
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
                // Fall through to space-delimited parsing
            }
        }

        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            identity.AddClaim(new Claim(targetClaimType, part));
        }
    }
}
