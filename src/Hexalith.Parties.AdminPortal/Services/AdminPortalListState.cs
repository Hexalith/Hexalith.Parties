namespace Hexalith.Parties.AdminPortal.Services;

public enum AdminPortalListState
{
    Loading,
    ReadyEmpty,
    ReadyHasResults,
    MissingToken,
    MissingTenant,
    Forbidden,
    NotFound,
    Gone,
    DegradedSearch,
    TransientFailure,
}
