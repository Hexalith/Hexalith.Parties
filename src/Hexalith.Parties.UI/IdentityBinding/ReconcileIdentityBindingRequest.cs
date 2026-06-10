namespace Hexalith.Parties.UI.IdentityBinding;

public sealed record ReconcileIdentityBindingRequest(
    string Tenant,
    string IdpIssuer,
    string IdpSubject);
