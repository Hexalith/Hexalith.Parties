#if HEXALITH_DEVELOPMENT_CATALOG_SOURCE
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Isolates endpoint protocol tests from catalog ownership while retaining HTTP authentication.</summary>
internal sealed class IsolatedDomainRouteCatalogGuard : IDomainRouteCatalogGuard
{
    /// <inheritdoc/>
    public Task<RouteCatalogDispatchLease?> AcquireAsync(EndpointFilterInvocationContext context)
        => Task.FromResult<RouteCatalogDispatchLease?>(null);
}
#endif
