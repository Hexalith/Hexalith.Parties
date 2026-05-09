namespace Hexalith.Parties.AdminPortal.Services;

/// <summary>
/// Discrete browse-surface states observed by FrontComposer shells. The live admin portal
/// component drives every value through <see cref="PartiesAdminListCoordinator.Transition"/>.
/// </summary>
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
