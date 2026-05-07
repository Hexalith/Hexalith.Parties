namespace Hexalith.Parties.AdminPortal.Services;

public enum AdminPortalQueryFailureKind
{
    AuthenticationRequired,
    TenantRequired,
    Forbidden,
    NotFound,
    Gone,
    Validation,
    TransientFailure,
    Unknown,
}
