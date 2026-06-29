using System.Security.Claims;

using Hexalith.Parties.Contracts.Authorization;

using Microsoft.Extensions.Primitives;

namespace Hexalith.Parties.Mcp;

internal sealed record PartiesMcpRequestContext(string TenantId, string UserId, string? Authorization);

internal interface IPartiesMcpRequestContextAccessor
{
    PartiesMcpRequestContext? Current { get; }
}

internal sealed class HttpPartiesMcpRequestContextAccessor(IHttpContextAccessor httpContextAccessor)
    : IPartiesMcpRequestContextAccessor
{
    public PartiesMcpRequestContext? Current
    {
        get
        {
            HttpContext? httpContext = httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return null;
            }

            string? tenantId = FirstNonEmpty(
                httpContext.User.FindFirst("tenant_id")?.Value,
                httpContext.User.FindFirst("tid")?.Value,
                Header(httpContext, "X-Tenant-Id"),
                Header(httpContext, "X-Tenant"));

            string? userId = FirstNonEmpty(
                httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                httpContext.User.FindFirst(PartiesClaimTypes.Subject)?.Value,
                httpContext.User.FindFirst("preferred_username")?.Value,
                Header(httpContext, "X-User-Id"),
                Header(httpContext, "X-User"));

            if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            return new PartiesMcpRequestContext(
                tenantId,
                userId,
                Header(httpContext, "Authorization"));
        }
    }

    private static string? Header(HttpContext context, string name)
        => context.Request.Headers.TryGetValue(name, out StringValues value)
            ? FirstNonEmpty(value.ToArray())
            : null;

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
