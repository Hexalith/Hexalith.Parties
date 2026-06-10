namespace Hexalith.Parties.ConsumerPortal.Services;

public enum ConsumerPrivacyExportOutcome
{
    Ready,
    TransientFailure,
    Forbidden,
    Erased,
    Unavailable,
    Restricted,
    Failed,
}
