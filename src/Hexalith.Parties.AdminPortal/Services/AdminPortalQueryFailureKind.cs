namespace Hexalith.Parties.AdminPortal.Services;

public enum AdminPortalQueryFailureKind
{
    AuthenticationRequired,
    TenantRequired,
    Forbidden,
    NotFound,
    Conflict,
    Gone,
    Validation,
    TransientFailure,
    ContractUnavailable,
    Unknown,
}
