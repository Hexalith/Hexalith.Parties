using System.Security.Claims;

using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.UI.Authentication;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.4 AC1/AC4 — the UI-local <see cref="PartiesClaimsTransformation"/> tenant-claim normalization.
/// Verifies the idempotent short-circuit (returns the same principal untouched when
/// <see cref="PartiesUiAuthorization.TenantClaimType"/> is already present — the normal case), derivation
/// of the normalized claim from a <c>tid</c>/<c>tenants</c> token when absent, and idempotency across the
/// repeated <c>TransformAsync</c> calls the runtime makes per request.
/// </summary>
public sealed class PartiesClaimsTransformationTests
{
    [Fact]
    public async Task ExistingTenantClaim_ShortCircuits_ReturnsSamePrincipalUntouched()
    {
        ClaimsPrincipal principal = Principal(new Claim(PartiesUiAuthorization.TenantClaimType, "tenant-a"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.ShouldBeSameAs(principal);
        result.FindAll(PartiesUiAuthorization.TenantClaimType).Count().ShouldBe(1);
    }

    [Fact]
    public async Task AbsentTenantClaim_DerivesNormalizedClaimFromTid()
    {
        ClaimsPrincipal principal = Principal(new Claim("tid", "tenant-b"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindFirst(PartiesUiAuthorization.TenantClaimType)?.Value.ShouldBe("tenant-b");
    }

    [Fact]
    public async Task AbsentTenantClaim_DerivesNormalizedClaimFromTenantsJsonArray()
    {
        ClaimsPrincipal principal = Principal(new Claim("tenants", "[\"tenant-c\"]"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindFirst(PartiesUiAuthorization.TenantClaimType)?.Value.ShouldBe("tenant-c");
    }

    [Fact]
    public async Task RepeatedTransform_IsIdempotent_NoDuplicateTenantClaim()
    {
        ClaimsPrincipal principal = Principal(new Claim("tid", "tenant-d"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal first = await sut.TransformAsync(principal);
        ClaimsPrincipal second = await sut.TransformAsync(first);

        second.FindAll(PartiesUiAuthorization.TenantClaimType).Count().ShouldBe(1);
    }

    [Fact]
    public async Task NoTenantSource_AddsNoClaim_DoesNotThrow()
    {
        ClaimsPrincipal principal = Principal(new Claim(PartiesClaimTypes.Subject, "user-1"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(PartiesUiAuthorization.TenantClaimType).ShouldBeEmpty();
    }

    [Fact]
    public async Task AbsentTenantClaim_DerivesNormalizedClaimFromTenantId()
    {
        // The implementation reads tenant_id as well as tid (tenant_id ?? tid) — the tid path is covered
        // above; this pins the tenant_id source so neither provider-name variant regresses untested.
        ClaimsPrincipal principal = Principal(new Claim("tenant_id", "tenant-e"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindFirst(PartiesUiAuthorization.TenantClaimType)?.Value.ShouldBe("tenant-e");
    }

    [Fact]
    public async Task AbsentTenantClaim_DerivesNormalizedClaimsFromSpaceDelimitedTenants()
    {
        // A non-JSON `tenants` value is parsed space-delimited (the branch the JSON-array test never hits).
        ClaimsPrincipal principal = Principal(new Claim("tenants", "tenant-x tenant-y"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        string[] tenants = result.FindAll(PartiesUiAuthorization.TenantClaimType).Select(c => c.Value).ToArray();
        tenants.ShouldBe(["tenant-x", "tenant-y"], ignoreOrder: true);
    }

    [Fact]
    public async Task AbsentTenantClaim_DerivesNormalizedClaimsFromMultiValueTenantsJsonArray()
    {
        // A multi-element JSON array adds one normalized claim per tenant (the single-element JSON test
        // never proves the loop runs more than once).
        ClaimsPrincipal principal = Principal(new Claim("tenants", "[\"tenant-x\",\"tenant-y\"]"));
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        ClaimsPrincipal result = await sut.TransformAsync(principal);

        result.FindAll(PartiesUiAuthorization.TenantClaimType).Count().ShouldBe(2);
    }

    [Fact]
    public async Task NullPrincipal_ThrowsArgumentNullException()
    {
        var sut = new PartiesClaimsTransformation(NullLogger<PartiesClaimsTransformation>.Instance);

        await Should.ThrowAsync<ArgumentNullException>(() => sut.TransformAsync(null!));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "Test"));
}
