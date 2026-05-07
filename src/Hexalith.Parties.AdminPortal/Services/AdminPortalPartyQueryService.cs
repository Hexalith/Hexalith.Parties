namespace Hexalith.Parties.AdminPortal.Services;

// TODO(Story 10-1.1): wire into PartiesAdminPortal.razor — currently scaffolding for
// FrontComposer integration. The component does not yet consume this service; tenant-switch
// reset is performed component-locally via OnParametersSetAsync signature change.
public sealed class AdminPortalPartyQueryService(IPartiesAdminPortalApiClient apiClient) : IDisposable
{
    private CancellationTokenSource _scopeCts = new();
    private bool _disposed;

    public IPartiesAdminPortalApiClient ApiClient { get; } = apiClient;

    public CancellationToken ScopeCancellationToken
    {
        get
        {
            CancellationTokenSource current = _scopeCts;
            try
            {
                return current.Token;
            }
            catch (ObjectDisposedException)
            {
                return new CancellationToken(canceled: true);
            }
        }
    }

    public void ResetForTenantSwitch()
    {
        if (_disposed)
        {
            return;
        }

        CancellationTokenSource old = Interlocked.Exchange(ref _scopeCts, new CancellationTokenSource());
        try
        {
            old.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        old.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _scopeCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _scopeCts.Dispose();
    }
}
