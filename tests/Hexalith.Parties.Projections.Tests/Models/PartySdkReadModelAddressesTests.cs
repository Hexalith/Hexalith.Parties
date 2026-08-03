using Hexalith.Parties.Contracts;
using Hexalith.Parties.Projections.Models;

using Shouldly;

namespace Hexalith.Parties.Projections.Tests.Models;

public sealed class PartySdkReadModelAddressesTests
{
    [Fact]
    public void Addresses_BuildCanonicalSlotKeys()
    {
        PartySdkReadModelAddresses.Detail("tenant-a", "party-1")
            .ShouldBe($"readmodel:tenant-a:party:{PartyProjectionNames.Detail}:party-1:detail");
        PartySdkReadModelAddresses.Index("tenant-a")
            .ShouldBe($"readmodel:tenant-a:party:{PartyProjectionNames.Index}:parties:index");
        PartySdkReadModelAddresses.Processing("tenant-a", "party-1")
            .ShouldBe($"readmodel:tenant-a:party:{PartyProjectionNames.Detail}:party-1:processing-records");
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
        _ = Should.Throw<ArgumentException>(() => PartySdkReadModelAddresses.Detail(tenantId!, partyId!));
    }

    [Theory]
    [InlineData("ten:ant", "party-1")]
    [InlineData("tenant-a", "par|ty")]
    [InlineData("tenant-a", "par\rty")]
    [InlineData("tenant-a", "par\nty")]
    [InlineData("tenant-a", "par\0ty")]
    public void Detail_WithReservedCharacters_Throws(string tenantId, string partyId)
    {
        _ = Should.Throw<ArgumentException>(() => PartySdkReadModelAddresses.Detail(tenantId, partyId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ten:ant")]
    [InlineData("ten|ant")]
    public void Index_WithInvalidTenant_Throws(string? tenantId)
    {
        _ = Should.Throw<ArgumentException>(() => PartySdkReadModelAddresses.Index(tenantId!));
    }
}
