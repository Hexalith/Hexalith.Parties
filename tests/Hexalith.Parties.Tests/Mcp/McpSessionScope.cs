using Hexalith.Parties.Mcp;

namespace Hexalith.Parties.Tests.Mcp;

/// <summary>
/// Temporarily sets MCP session tenant/user AsyncLocal values and restores the previous values on dispose.
/// </summary>
internal sealed class McpSessionScope : IDisposable
{
    private readonly string? _previousTenant;
    private readonly string? _previousUserId;

    private McpSessionScope(string tenantId, string userId)
    {
        _previousTenant = McpSessionContext.Tenant.Value;
        _previousUserId = McpSessionContext.UserId.Value;
        McpSessionContext.Tenant.Value = tenantId;
        McpSessionContext.UserId.Value = userId;
    }

    public static McpSessionScope For(string tenantId, string userId) => new(tenantId, userId);

    public void Dispose()
    {
        McpSessionContext.Tenant.Value = _previousTenant;
        McpSessionContext.UserId.Value = _previousUserId;
    }
}
