using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.UI.Services;

using Microsoft.AspNetCore.Http;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class PartiesAdminPortalE2eFixtureTests
{
    [Fact]
    public async Task AuthorizationService_ReturnsUnauthenticatedWithoutFixtureCookieAsync()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        var service = new PartiesAdminPortalE2eAuthorizationService(accessor);

        AdminPortalAuthorizationState state = await service.GetAuthorizationStateAsync();

        state.ShouldBe(AdminPortalAuthorizationState.Unauthenticated);
    }

    [Fact]
    public async Task AuthorizationService_ReturnsAdminOnlyWhenFixtureCookieIsPresentAsync()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        accessor.HttpContext.Request.Headers.Cookie = $"{PartiesAdminPortalE2eFixture.AdminCookieName}=enabled";
        var service = new PartiesAdminPortalE2eAuthorizationService(accessor);

        AdminPortalAuthorizationState state = await service.GetAuthorizationStateAsync();

        state.IsAuthenticated.ShouldBeTrue();
        state.HasTenantContext.ShouldBeTrue();
        state.IsAdmin.ShouldBeTrue();
        state.ContextSignature.ShouldBe("tenant:test-tenant:user:admin-e2e:admin");
    }
}
