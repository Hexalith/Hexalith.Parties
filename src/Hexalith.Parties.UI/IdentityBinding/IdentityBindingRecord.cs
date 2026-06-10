namespace Hexalith.Parties.UI.IdentityBinding;

public sealed record IdentityBindingRecord(
    IdentityBindingKey Key,
    string? PartyId,
    IdentityBindingStatus Status,
    string BoundByOperator,
    string UpdatedByOperator,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string VerificationReference,
    string ReasonCode,
    long Version,
    IReadOnlyList<IdentityBindingAuditEntry> AuditTrail);
