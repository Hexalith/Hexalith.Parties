namespace Hexalith.Parties.Contracts.Security;

public enum TenantKeyRotationFailureCategory
{
    None,
    MissingKeyProvider,
    BackendUnavailable,
    ErasedParty,
    ConcurrencyConflict,
}
