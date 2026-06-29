using Hexalith.Parties.Contracts.Authorization;

using Microsoft.AspNetCore.Authorization;

namespace Hexalith.Parties.Authorization;

// Story 1.5 (AR-D3) — single source of truth + reusable registration helper for the server-side
// Consumer authorization policy. Registered alongside the existing Admin policy (same posture:
// registered + policy-resolvable, role-claim based). Kept testable in isolation (Add can be exercised
// through a minimal AddAuthorizationCore) rather than only via the monolithic AddParties.
public static class ConsumerPolicy
{
    public const string Name = PartiesRoles.ConsumerPolicy;

    public static readonly string[] RoleNames = PartiesRoles.ConsumerRoleNames;

    public static void Add(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddPolicy(Name, policy => policy.RequireRole(RoleNames));
    }
}
