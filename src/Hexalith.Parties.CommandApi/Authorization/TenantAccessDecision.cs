namespace Hexalith.Parties.CommandApi.Authorization;

public sealed record TenantAccessDecision(bool IsAllowed, TenantAccessDenialReason Reason) {
    public static TenantAccessDecision Allowed { get; } = new(true, TenantAccessDenialReason.None);

    public static TenantAccessDecision Denied(TenantAccessDenialReason reason) => new(false, reason);
}
