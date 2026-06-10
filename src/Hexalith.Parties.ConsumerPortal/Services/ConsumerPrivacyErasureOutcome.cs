namespace Hexalith.Parties.ConsumerPortal.Services;

public enum ConsumerPrivacyErasureOutcome
{
    Ready,
    Pending,
    CancellationAccepted,
    Permanent,
    Unavailable,
    Forbidden,
    AuthenticationRequired,
    Rejected,
    TransientFailure,
}
