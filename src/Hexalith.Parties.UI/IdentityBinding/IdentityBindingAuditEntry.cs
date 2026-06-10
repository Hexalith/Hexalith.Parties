namespace Hexalith.Parties.UI.IdentityBinding;

public sealed record IdentityBindingAuditEntry(
    DateTimeOffset TimestampUtc,
    string Operator,
    string Action,
    string? PartyId,
    string VerificationReference,
    string ReasonCode,
    long Version);
