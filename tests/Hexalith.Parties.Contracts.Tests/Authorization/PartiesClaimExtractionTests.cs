using System.Security.Claims;

using Hexalith.Parties.Contracts.Authorization;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Authorization;

public sealed class PartiesClaimExtractionTests
{
    [Fact]
    public void TryGetTenantId_WithSingleNormalizedTenant_ReturnsSuccess()
    {
        ClaimsPrincipal principal = Principal(new Claim(PartiesClaimTypes.EventStoreTenant, "tenant-a"));

        PartiesClaimExtractionResult result = principal.TryGetTenantId();

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldBe("tenant-a");
        result.Failure.ShouldBe(PartiesClaimExtractionFailure.None);
    }

    [Fact]
    public void TryGetTenantId_WithAbsentTenant_FailsClosedAsMissing()
    {
        ClaimsPrincipal principal = Principal();

        PartiesClaimExtractionResult result = principal.TryGetTenantId();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(PartiesClaimExtractionFailure.Missing);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetTenantId_WithEmptyTenant_FailsClosedAsEmpty(string tenantId)
    {
        ClaimsPrincipal principal = Principal(new Claim(PartiesClaimTypes.EventStoreTenant, tenantId));

        PartiesClaimExtractionResult result = principal.TryGetTenantId();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(PartiesClaimExtractionFailure.Empty);
    }

    [Fact]
    public void TryGetUserId_WithSubjectAndObjectId_UsesSubjectFirst()
    {
        ClaimsPrincipal principal = Principal(
            new Claim(PartiesClaimTypes.Subject, "subject-user"),
            new Claim(PartiesClaimTypes.ObjectId, "object-user"));

        PartiesClaimExtractionResult result = principal.TryGetUserId();

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldBe("subject-user");
    }

    [Fact]
    public void TryGetUserId_WithEmptySubject_FallsBackToObjectId()
    {
        ClaimsPrincipal principal = Principal(
            new Claim(PartiesClaimTypes.Subject, " "),
            new Claim(PartiesClaimTypes.ObjectId, "object-user"));

        PartiesClaimExtractionResult result = principal.TryGetUserId();

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldBe("object-user");
    }

    [Fact]
    public void TryGetUserId_WithAmbiguousSubject_DoesNotFallbackToObjectId()
    {
        ClaimsPrincipal principal = Principal(
            new Claim(PartiesClaimTypes.Subject, "subject-1"),
            new Claim(PartiesClaimTypes.Subject, "subject-2"),
            new Claim(PartiesClaimTypes.ObjectId, "object-user"));

        PartiesClaimExtractionResult result = principal.TryGetUserId();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(PartiesClaimExtractionFailure.Ambiguous);
    }

    [Fact]
    public void TryGetUserId_WithMissingSubjectAndAmbiguousObjectId_FailsClosedAsAmbiguous()
    {
        ClaimsPrincipal principal = Principal(
            new Claim(PartiesClaimTypes.ObjectId, "object-1"),
            new Claim(PartiesClaimTypes.ObjectId, "object-2"));

        PartiesClaimExtractionResult result = principal.TryGetUserId();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(PartiesClaimExtractionFailure.Ambiguous);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetPartyId_WithEmptyPartyId_FailsClosed(string partyId)
    {
        ClaimsPrincipal principal = Principal(new Claim(PartiesClaimTypes.PartyId, partyId));

        PartiesClaimExtractionResult result = principal.TryGetPartyId();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(PartiesClaimExtractionFailure.Empty);
    }

    [Fact]
    public void TryGetPartyId_WithMultiplePartyIds_FailsClosedAsAmbiguous()
    {
        ClaimsPrincipal principal = Principal(
            new Claim(PartiesClaimTypes.PartyId, "party-1"),
            new Claim(PartiesClaimTypes.PartyId, "party-2"));

        PartiesClaimExtractionResult result = principal.TryGetPartyId();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(PartiesClaimExtractionFailure.Ambiguous);
    }

    [Fact]
    public void TryGetPartyId_WithDuplicateSameValuePartyIds_FailsClosedAsAmbiguous()
    {
        ClaimsPrincipal principal = Principal(
            new Claim(PartiesClaimTypes.PartyId, "party-1"),
            new Claim(PartiesClaimTypes.PartyId, "party-1"));

        PartiesClaimExtractionResult result = principal.TryGetPartyId();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(PartiesClaimExtractionFailure.Ambiguous);
    }

    [Fact]
    public void TryGetTenantId_WithMultipleTenants_FailsClosedAsAmbiguous()
    {
        ClaimsPrincipal principal = Principal(
            new Claim(PartiesClaimTypes.EventStoreTenant, "tenant-a"),
            new Claim(PartiesClaimTypes.EventStoreTenant, "tenant-b"));

        PartiesClaimExtractionResult result = principal.TryGetTenantId();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(PartiesClaimExtractionFailure.Ambiguous);
    }

    [Fact]
    public void TryGetUserId_WithAbsentSubjectAndObjectId_FailsClosedAsMissing()
    {
        ClaimsPrincipal principal = Principal();

        PartiesClaimExtractionResult result = principal.TryGetUserId();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe(PartiesClaimExtractionFailure.Missing);
    }

    [Fact]
    public void TryGetTenantId_WithClaimsIdentity_UsesSameExtractionRules()
    {
        var identity = new ClaimsIdentity(
            [new Claim(PartiesClaimTypes.EventStoreTenant, "tenant-a")],
            authenticationType: "Test");

        PartiesClaimExtractionResult result = identity.TryGetTenantId();

        result.Succeeded.ShouldBeTrue();
        result.Value.ShouldBe("tenant-a");
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "Test"));
}
