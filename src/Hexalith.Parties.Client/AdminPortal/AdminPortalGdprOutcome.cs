namespace Hexalith.Parties.Client.AdminPortal;

public enum AdminPortalGdprOutcome
{
    Accepted,
    Completed,
    ValidationRejected,
    Forbidden,
    MissingTenant,
    ErasureInProgress,
    Erased,
    NotFound,
    AuthenticationRequired,
    TransientFailure,
    ContractUnavailable,
    Unknown,
}
