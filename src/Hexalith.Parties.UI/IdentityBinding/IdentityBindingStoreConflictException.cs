namespace Hexalith.Parties.UI.IdentityBinding;

public sealed class IdentityBindingStoreConflictException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
}
