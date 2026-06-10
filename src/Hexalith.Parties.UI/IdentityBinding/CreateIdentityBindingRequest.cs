namespace Hexalith.Parties.UI.IdentityBinding;

public sealed record CreateIdentityBindingRequest(
    string Tenant,
    string IdpIssuer,
    string IdpSubject,
    string PartyId,
    string OperatorSubject,
    string VerificationReference,
    string ReasonCode);
