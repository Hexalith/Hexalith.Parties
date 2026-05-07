namespace Hexalith.Parties.AdminPortal.Services;

// TODO(Story 10-1.1): wire into PartiesAdminPortal.razor — currently scaffolding for
// FrontComposer integration. Only Loading is reachable from the live component path.
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
