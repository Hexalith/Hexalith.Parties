using Hexalith.FrontComposer.Contracts.Rendering;
using Hexalith.Parties.Contracts.Authorization;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// Gives the explicitly enabled Test host a shell scope so route authorization can render,
/// including anonymous challenges and rejected Consumer bindings. The fixture's claims and
/// authorization services still determine access to data and pages.
/// </summary>
internal sealed class PartiesAdminPortalE2eUserContextAccessor(IHttpContextAccessor httpContextAccessor) : IUserContextAccessor
{
    /// <inheritdoc />
    public string TenantId => "test-tenant";

    /// <inheritdoc />
    public string UserId => PartiesAdminPortalE2eFixture.CreatePrincipal(httpContextAccessor)
        ?.FindFirst(PartiesClaimTypes.Subject)?.Value ?? "anonymous-e2e";
}
