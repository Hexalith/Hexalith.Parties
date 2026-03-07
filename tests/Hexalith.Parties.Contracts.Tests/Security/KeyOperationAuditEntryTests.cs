using Hexalith.Parties.Contracts.Security;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Security;

public class KeyOperationAuditEntryTests
{
    [Fact]
    public void Constructor_WithAllProperties_SetsValuesCorrectly()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var entry = new KeyOperationAuditEntry
        {
            OperationType = KeyOperationType.Create,
            TenantId = "acme",
            PartyId = "p1",
            KeyVersion = 1,
            Timestamp = timestamp,
            CorrelationId = "corr-123",
        };

        entry.OperationType.ShouldBe(KeyOperationType.Create);
        entry.TenantId.ShouldBe("acme");
        entry.PartyId.ShouldBe("p1");
        entry.KeyVersion.ShouldBe(1);
        entry.Timestamp.ShouldBe(timestamp);
        entry.CorrelationId.ShouldBe("corr-123");
    }

    [Theory]
    [InlineData(KeyOperationType.Create)]
    [InlineData(KeyOperationType.Read)]
    [InlineData(KeyOperationType.Rotate)]
    [InlineData(KeyOperationType.Delete)]
    public void OperationType_SupportsAllKeyOperations(KeyOperationType operationType)
    {
        var entry = new KeyOperationAuditEntry
        {
            OperationType = operationType,
            TenantId = "t",
            PartyId = "p",
            KeyVersion = 1,
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "c",
        };

        entry.OperationType.ShouldBe(operationType);
    }
}
