namespace Hexalith.Parties.AdminPortal.Services;

/// <summary>
/// Per-circuit accessor for <see cref="IPartiesAdminPortalApiClient"/> that exposes a
/// scope-lifetime <see cref="ScopeCancellationToken"/>. Tenant switches call
/// <see cref="ResetForTenantSwitch"/> so any in-flight request observing the scope token is
/// cancelled before it can paint cross-tenant data.
/// </summary>
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
