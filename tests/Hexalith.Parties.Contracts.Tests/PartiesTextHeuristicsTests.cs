using Hexalith.Parties.Contracts;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests;

public sealed class PartiesTextHeuristicsTests
{
    [Theory]
    [InlineData("Tenant context is unavailable.")]
    [InlineData("missing TENANT membership")]
    [InlineData("eventstore:tenant claim is required")]
    public void ContainsTenant_WhenTenantTextIsPresent_ReturnsTrue(string value)
    {
        PartiesTextHeuristics.ContainsTenant(value).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("permission denied")]
    [InlineData("user is not authorized for this operation")]
    public void ContainsTenant_WhenTenantTextIsAbsent_ReturnsFalse(string? value)
    {
        PartiesTextHeuristics.ContainsTenant(value).ShouldBeFalse();
    }
}
