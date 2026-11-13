using Hexalith.Tenants.Client.Projections;

namespace Hexalith.Parties.AdminPortal.Tests.Services;

/// <summary>Controls the tenant-read boundary while authentication changes.</summary>
/// <param name="read">The controlled projection read.</param>
internal sealed class AuthorizationTenantProjectionStore(
    Func<string, CancellationToken, Task<TenantLocalState?>> read) : ITenantProjectionStore
{
    /// <inheritdoc/>
    public Task<TenantLocalState?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
        => read(tenantId, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync(TenantLocalState state, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("These authorization controls never write tenant state.");
}
