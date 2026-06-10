namespace Hexalith.Parties.UI.IdentityBinding;

public sealed record RotateIdentityBindingRequest(
    string Tenant,
    string IdpIssuer,
    string IdpSubject,
    string NewPartyId,
    string OperatorSubject,
    string VerificationReference,
    string ReasonCode,
    long ExpectedVersion);
