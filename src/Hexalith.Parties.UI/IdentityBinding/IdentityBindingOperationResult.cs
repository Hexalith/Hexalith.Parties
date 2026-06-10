namespace Hexalith.Parties.UI.IdentityBinding;

public sealed record IdentityBindingOperationResult(
    bool Succeeded,
    string Code,
    IdentityBindingRecord? Binding = null,
    IdentityBindingDriftReport? Drift = null)
{
    public static IdentityBindingOperationResult Success(string code, IdentityBindingRecord binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(binding);

        return new(true, code, binding);
    }

    public static IdentityBindingOperationResult Reconciled(IdentityBindingDriftReport drift, IdentityBindingRecord? binding)
    {
        ArgumentNullException.ThrowIfNull(drift);

        return new(true, drift.HasDrift ? "DriftDetected" : "InSync", binding, drift);
    }

    public static IdentityBindingOperationResult Failure(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return new(false, code);
    }
}
