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
    private (string PartyId, string ContextSignature)? _acceptedCommand;

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

    internal void StageAcceptedCommand(string partyId, string contextSignature)
        => _acceptedCommand = (partyId, contextSignature);

    internal bool ConsumeAcceptedCommand(string? partyId, string contextSignature)
    {
        (string PartyId, string ContextSignature)? accepted = _acceptedCommand;
        _acceptedCommand = null;
        return accepted is { } command
            && string.Equals(command.PartyId, partyId, StringComparison.Ordinal)
            && string.Equals(command.ContextSignature, contextSignature, StringComparison.Ordinal);
    }

    public void ResetForTenantSwitch()
    {
        if (_disposed)
        {
            return;
        }

        _acceptedCommand = null;
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
        _acceptedCommand = null;
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
