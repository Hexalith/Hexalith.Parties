namespace Hexalith.Parties.AdminPortal.Services;

public sealed class AdminPortalPartyQueryService(IPartiesAdminPortalApiClient apiClient) : IDisposable
{
    private CancellationTokenSource _scopeCts = new();

    public IPartiesAdminPortalApiClient ApiClient { get; } = apiClient;

    public CancellationToken ScopeCancellationToken => _scopeCts.Token;

    public void ResetForTenantSwitch()
    {
        CancellationTokenSource old = Interlocked.Exchange(ref _scopeCts, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }

    public void Dispose()
    {
        _scopeCts.Cancel();
        _scopeCts.Dispose();
    }
}
