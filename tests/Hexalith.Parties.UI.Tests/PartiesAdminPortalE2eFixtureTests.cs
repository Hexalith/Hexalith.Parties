using System.Text.Json;

using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Contracts.Models;
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

    [Fact]
    public async Task E2eApiClient_ExportPartyDataAsync_ReturnsParseablePortabilityPackageAsync()
    {
        var fixtureState = new PartiesAdminPortalE2eFixtureState();
        var client = new PartiesAdminPortalE2eApiClient(
            fixtureState,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        AdminPortalExportDownload download = await client.ExportPartyDataAsync("party-bound-001", CancellationToken.None);

        PartyDataPortabilityPackage? package = JsonSerializer.Deserialize<PartyDataPortabilityPackage>(
            download.Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        package.ShouldNotBeNull();
        package.PartyId.ShouldBe("party-bound-001");
        package.TenantId.ShouldBe("test-tenant");
        package.Status.ShouldBe("Erased");
        package.ExportedAt.ShouldBe(new DateTimeOffset(2026, 06, 10, 10, 00, 00, TimeSpan.Zero));
        package.ExportedBy.ShouldBe("consumer-e2e");
        package.CorrelationId.ShouldBe("corr-export-e2e");
        fixtureState.Snapshot().ExportRequests.Single().PartyId.ShouldBe("party-bound-001");
    }
}
