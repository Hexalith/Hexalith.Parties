using Hexalith.Parties.AdminPortal.Services;

using Shouldly;

namespace Hexalith.Parties.AdminPortal.Tests.Services;

public sealed class AdminPortalRouteIdsTests
{
    [Theory]
    [InlineData("party-route-1")]
    [InlineData("01HYX7QS3NP8M4KQJR5A7CVWKM")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    public void IsSafe_SupportSafePartyId_ReturnsTrue(string partyId)
        => AdminPortalRouteIds.IsSafe(partyId).ShouldBeTrue();

    [Theory]
    [InlineData("party~route")]
    [InlineData("tenant-a:parties:party-route")]
    [InlineData("party/unsafe")]
    [InlineData("---")]
    public void IsSafe_UnsafePartyId_ReturnsFalse(string partyId)
        => AdminPortalRouteIds.IsSafe(partyId).ShouldBeFalse();
}
