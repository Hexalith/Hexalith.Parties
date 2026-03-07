using Hexalith.Parties.Contracts.Security;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Security;

public class ErasureCertificateTests
{
    [Fact]
    public void Constructor_WithAllProperties_SetsValuesCorrectly()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var cert = new ErasureCertificate
        {
            PartyId = "p1",
            TenantId = "acme",
            Timestamp = timestamp,
            KeyVersionsDestroyed = [1, 2, 3],
            VerificationStatus = ErasureVerificationStatus.Verified,
        };

        cert.PartyId.ShouldBe("p1");
        cert.TenantId.ShouldBe("acme");
        cert.Timestamp.ShouldBe(timestamp);
        cert.KeyVersionsDestroyed.ShouldBe([1, 2, 3]);
        cert.VerificationStatus.ShouldBe(ErasureVerificationStatus.Verified);
    }
}
