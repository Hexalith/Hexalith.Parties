namespace Hexalith.Parties.AdminPortal.Services;

public interface IAdminPortalAuthorizationService
{
    Task<AdminPortalAuthorizationState> GetAuthorizationStateAsync(CancellationToken cancellationToken = default);
}
