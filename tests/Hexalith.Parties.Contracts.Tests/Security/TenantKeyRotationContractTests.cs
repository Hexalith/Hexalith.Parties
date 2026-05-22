using System.Text.Json;

using Hexalith.Parties.Contracts.Security;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Security;

public sealed class TenantKeyRotationContractTests
{
    [Fact]
    public void StatusContract_ContainsOnlyBoundedRotationMetadata()
    {
        TenantKeyRotationStatus status = new()
        {
            TenantId = "tenant-a",
            OperationId = "rotation-1",
            Phase = TenantKeyRotationPhase.Completed,
            TotalCount = 2,
            ProcessedCount = 2,
            SkippedCount = 0,
            FailedCount = 0,
            FailureCategories = new Dictionary<TenantKeyRotationFailureCategory, int>(),
            StartedAt = DateTimeOffset.Parse("2026-05-21T06:15:58.277Z"),
            CompletedAt = DateTimeOffset.Parse("2026-05-21T06:16:58.277Z"),
            CorrelationId = "correlation-1",
        };

        string json = JsonSerializer.Serialize(status);

        json.ShouldContain("tenant-a");
        json.ShouldContain("rotation-1");
        json.ShouldNotContain("keyMaterial", Case.Sensitive);
        json.ShouldNotContain("wrappedPartyKeyBytes", Case.Sensitive);
        json.ShouldNotContain("token", Case.Insensitive);
        json.ShouldNotContain("secret", Case.Insensitive);
        json.ShouldNotContain("firstName", Case.Insensitive);
        json.ShouldNotContain("lastName", Case.Insensitive);
    }

    [Fact]
    public void RotationRequest_RequiresTenantAndOperationIdentifiers()
    {
        TenantKeyRotationRequest request = new()
        {
            TenantId = "tenant-a",
            OperationId = "rotation-1",
            CorrelationId = "correlation-1",
        };

        request.TenantId.ShouldBe("tenant-a");
        request.OperationId.ShouldBe("rotation-1");
        request.CorrelationId.ShouldBe("correlation-1");
    }

    [Fact]
    public void KeyOperationType_IncludesTenantRotationWithoutRenumberingExistingValues()
    {
        ((int)KeyOperationType.Create).ShouldBe(0);
        ((int)KeyOperationType.Read).ShouldBe(1);
        ((int)KeyOperationType.Rotate).ShouldBe(2);
        ((int)KeyOperationType.Delete).ShouldBe(3);
        KeyOperationType.TenantRotate.ToString().ShouldBe("TenantRotate");
    }
}
