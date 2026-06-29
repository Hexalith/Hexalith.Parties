using Hexalith.Parties.Contracts;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests;

public sealed class PartyProjectionAnchorTests
{
    [Fact]
    public void PartyProjectionNames_ExposeCanonicalProjectionNames()
    {
        PartyProjectionNames.Detail.ShouldBe("party-detail");
        PartyProjectionNames.Index.ShouldBe("party-index");
    }

    [Fact]
    public void PartyActorIds_BuildRuntimeCompatibleProjectionActorIds()
    {
        PartyActorIds.Detail("tenant-a", "party-1").ShouldBe("tenant-a:party-detail:party-1");
        PartyActorIds.Index("tenant-a").ShouldBe("tenant-a:party-index");
    }

    [Theory]
    [InlineData(null, "party-1")]
    [InlineData("", "party-1")]
    [InlineData("   ", "party-1")]
    [InlineData("tenant-a", null)]
    [InlineData("tenant-a", "")]
    [InlineData("tenant-a", "   ")]
    public void Detail_WithMissingSegments_Throws(string? tenantId, string? partyId)
    {
        _ = Should.Throw<ArgumentException>(() => PartyActorIds.Detail(tenantId!, partyId!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Index_WithMissingTenant_Throws(string? tenantId)
    {
        _ = Should.Throw<ArgumentException>(() => PartyActorIds.Index(tenantId!));
    }
}
