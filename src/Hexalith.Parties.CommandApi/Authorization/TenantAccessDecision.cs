namespace Hexalith.Parties.CommandApi.Authorization;

public sealed record TenantAccessDecision(bool IsAllowed, TenantAccessDenialReason Reason, string? DiagnosticText = null) {
    public static TenantAccessDecision Allowed { get; } = new(true, TenantAccessDenialReason.None);

    public static TenantAccessDecision Denied(TenantAccessDenialReason reason, string? diagnosticText = null)
        => new(false, reason, diagnosticText);
}
