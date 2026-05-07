namespace Hexalith.Parties.AdminPortal.Services;

public sealed record AdminPortalAuthorizationState(
    bool IsAuthenticated,
    bool HasTenantContext,
    bool IsAdmin,
    string ContextSignature)
{
    public static AdminPortalAuthorizationState Unauthenticated { get; } =
        new(false, false, false, "unauthenticated");
}
