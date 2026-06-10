namespace Hexalith.Parties.Authorization;

public sealed record DataSubjectAccessDecision(bool IsAllowed, DataSubjectAccessDenialReason Reason, string? DiagnosticText = null) {
    public static DataSubjectAccessDecision Allowed { get; } = new(true, DataSubjectAccessDenialReason.None);

    public static DataSubjectAccessDecision Denied(DataSubjectAccessDenialReason reason, string? diagnosticText = null)
        => new(false, reason, diagnosticText);
}
