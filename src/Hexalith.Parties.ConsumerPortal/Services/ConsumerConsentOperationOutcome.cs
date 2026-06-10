namespace Hexalith.Parties.ConsumerPortal.Services;

public enum ConsumerConsentOperationOutcome
{
    Accepted,
    ValidationRejected,
    Forbidden,
    TransientFailure,
    Erased,
    Failed,
}
