using System.Security.Claims;

using Hexalith.Parties.Authentication;
using Hexalith.Parties.Contracts.Authorization;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Hexalith.Parties.Authentication.Tests;

public sealed class PartiesClaimsTransformationTests
{
    [Fact]
    public async Task ExistingTenantClaim_ShortCircuits_ReturnsSamePrincipalUntouched()
    {
        ClaimsPrincipal principal = Principal(new Claim(PartiesClaimTypes.EventStoreTenant, "tenant-a"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.ShouldBeSameAs(principal);
        result.FindAll(PartiesClaimTypes.EventStoreTenant).Count().ShouldBe(1);
    }

    [Fact]
    public async Task AbsentTenantClaim_DerivesNormalizedClaimFromTid()
    {
        ClaimsPrincipal principal = Principal(new Claim("tid", "tenant-b"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindFirst(PartiesClaimTypes.EventStoreTenant)?.Value.ShouldBe("tenant-b");
    }

    [Fact]
    public async Task AbsentTenantClaim_DerivesNormalizedClaimFromTenantId()
    {
        ClaimsPrincipal principal = Principal(new Claim("tenant_id", "tenant-e"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindFirst(PartiesClaimTypes.EventStoreTenant)?.Value.ShouldBe("tenant-e");
    }

    [Fact]
    public async Task AbsentTenantClaim_DerivesNormalizedClaimFromTenantsJsonArray()
    {
        ClaimsPrincipal principal = Principal(new Claim("tenants", "[\"tenant-c\"]"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindFirst(PartiesClaimTypes.EventStoreTenant)?.Value.ShouldBe("tenant-c");
    }

    [Fact]
    public async Task AbsentTenantClaim_DerivesNormalizedClaimsFromMultiValueTenantsJsonArray()
    {
        ClaimsPrincipal principal = Principal(new Claim("tenants", "[\"tenant-x\",\"tenant-y\"]"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(PartiesClaimTypes.EventStoreTenant).Select(claim => claim.Value).ToArray()
            .ShouldBe(["tenant-x", "tenant-y"], ignoreOrder: true);
    }

    [Fact]
    public async Task AbsentTenantClaim_DerivesNormalizedClaimsFromSpaceDelimitedTenants()
    {
        ClaimsPrincipal principal = Principal(new Claim("tenants", "tenant-x tenant-y"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(PartiesClaimTypes.EventStoreTenant).Select(claim => claim.Value).ToArray()
            .ShouldBe(["tenant-x", "tenant-y"], ignoreOrder: true);
    }

    [Fact]
    public async Task MalformedTenantsJson_FallsBackToSpaceDelimitedParsing()
    {
        ClaimsPrincipal principal = Principal(new Claim("tenants", "[tenant-x tenant-y"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(PartiesClaimTypes.EventStoreTenant).Select(claim => claim.Value).ToArray()
            .ShouldBe(["[tenant-x", "tenant-y"], ignoreOrder: true);
    }

    [Fact]
    public async Task RepeatedTransform_IsIdempotent_NoDuplicateTenantClaim()
    {
        ClaimsPrincipal principal = Principal(new Claim("tid", "tenant-d"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal first = await sut.TransformAsync(principal);
        ClaimsPrincipal second = await sut.TransformAsync(first);

        second.FindAll(PartiesClaimTypes.EventStoreTenant).Count().ShouldBe(1);
    }

    [Fact]
    public async Task NullPrincipal_ThrowsArgumentNullException()
    {
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        await Should.ThrowAsync<ArgumentNullException>(() => sut.TransformAsync(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyTenantSource_AddsNoClaim_DoesNotThrow(string tenantSource)
    {
        ClaimsPrincipal principal = Principal(new Claim("tid", tenantSource));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(PartiesClaimTypes.EventStoreTenant).ShouldBeEmpty();
    }

    [Fact]
    public async Task NoTenantSource_AddsNoClaim_DoesNotThrow()
    {
        ClaimsPrincipal principal = Principal(new Claim(PartiesClaimTypes.Subject, "user-1"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(PartiesClaimTypes.EventStoreTenant).ShouldBeEmpty();
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "Test"));
}
