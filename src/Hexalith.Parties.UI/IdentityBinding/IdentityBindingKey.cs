namespace Hexalith.Parties.UI.IdentityBinding;

public sealed record IdentityBindingKey(
    string Tenant,
    string IdpIssuer,
    string IdpSubject);
