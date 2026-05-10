namespace Hexalith.Parties.AdminPortal.Services;

public enum AdminPortalGdprOperationState
{
    NotLoaded,
    Loading,
    Ready,
    ConfirmationRequired,
    Submitting,
    Accepted,
    ErasurePending,
    VerificationPartial,
    VerificationFailed,
    Verified,
    Erased,
    MissingToken,
    MissingTenant,
    Forbidden,
    DomainRejected,
    TransientFailure,
}
