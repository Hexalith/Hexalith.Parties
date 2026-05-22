using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

internal sealed record TenantKeyRotationProgress
{
    public required TenantKeyRotationStatus Status { get; init; }

    public required TenantKeyMetadata? TargetTenantKey { get; init; }

    public required IReadOnlyList<string> CompletedPartyKeyRecords { get; init; }
}
