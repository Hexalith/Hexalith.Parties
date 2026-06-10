namespace Hexalith.Parties.UI.IdentityBinding;

public sealed record ChangeIdentityBindingStatusRequest(
    string Tenant,
    string IdpIssuer,
    string IdpSubject,
    string OperatorSubject,
    string VerificationReference,
    string ReasonCode,
    long ExpectedVersion);
