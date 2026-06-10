namespace Hexalith.Parties.ConsumerPortal.Services;

public enum ConsumerPrivacyProcessingOutcome
{
    Ready,
    Empty,
    Forbidden,
    AuthenticationRequired,
    Unavailable,
    Stale,
    TransientFailure,
    Erased,
}
