namespace Hexalith.Parties.Mcp;

internal sealed class McpContextForwardingHandler(IPartiesMcpRequestContextAccessor contextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        PartiesMcpRequestContext? context = contextAccessor.Current;
        if (context is not null)
        {
            if (!string.IsNullOrWhiteSpace(context.Authorization) && !request.Headers.Contains("Authorization"))
            {
                _ = request.Headers.TryAddWithoutValidation("Authorization", context.Authorization);
            }

            _ = request.Headers.TryAddWithoutValidation("X-Tenant-Id", context.TenantId);
            _ = request.Headers.TryAddWithoutValidation("X-User-Id", context.UserId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
