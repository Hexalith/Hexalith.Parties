namespace Hexalith.Parties.Contracts.Security;

public enum ErasureStatus
{
    Active,
    ErasurePending,
    KeyDestroyed,
    VerificationInProgress,
    Verified,
    Erased,
}
