namespace Hexalith.Parties.UI.IdentityBinding;

public sealed record IdentityBindingDriftReport(
    IdentityBindingKey Key,
    bool HasDrift,
    string StoreStatus,
    string IdpAttributeShape);
