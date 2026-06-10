namespace Hexalith.Parties.ConsumerPortal.Services;

public enum ConsumerPrivacyErasureState
{
    Active,
    ErasurePending,
    KeyDestroyed,
    VerificationInProgress,
    Verified,
    Erased,
    Unknown,
}
