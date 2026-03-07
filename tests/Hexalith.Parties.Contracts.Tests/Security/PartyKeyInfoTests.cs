using Hexalith.Parties.Contracts.Security;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Security;

public class PartyKeyInfoTests
{
    [Fact]
    public void Constructor_WithAllProperties_SetsValuesCorrectly()
    {
        var created = DateTimeOffset.UtcNow;
        var info = new PartyKeyInfo
        {
            KeyId = "acme/parties/p1/v1",
            Version = 1,
            TenantId = "acme",
            PartyId = "p1",
            Algorithm = EncryptionAlgorithm.AES256GCM,
            CreatedAt = created,
        };

        info.KeyId.ShouldBe("acme/parties/p1/v1");
        info.Version.ShouldBe(1);
        info.TenantId.ShouldBe("acme");
        info.PartyId.ShouldBe("p1");
        info.Algorithm.ShouldBe(EncryptionAlgorithm.AES256GCM);
        info.CreatedAt.ShouldBe(created);
    }

    [Fact]
    public void TwoInstances_WithSameValues_AreEqual()
    {
        var created = DateTimeOffset.UtcNow;
        var a = new PartyKeyInfo
        {
            KeyId = "acme/parties/p1/v1",
            Version = 1,
            TenantId = "acme",
            PartyId = "p1",
            Algorithm = EncryptionAlgorithm.AES256GCM,
            CreatedAt = created,
        };
        var b = new PartyKeyInfo
        {
            KeyId = "acme/parties/p1/v1",
            Version = 1,
            TenantId = "acme",
            PartyId = "p1",
            Algorithm = EncryptionAlgorithm.AES256GCM,
            CreatedAt = created,
        };

        a.ShouldBe(b);
    }
}
