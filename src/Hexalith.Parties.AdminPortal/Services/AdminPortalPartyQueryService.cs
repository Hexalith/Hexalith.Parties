using Hexalith.Parties.Contracts.Commands;

namespace Hexalith.Parties.AdminPortal.Services;

/// <summary>
/// Per-circuit accessor for <see cref="IPartiesAdminPortalApiClient"/> that exposes a
/// scope-lifetime <see cref="ScopeCancellationToken"/>. Tenant switches call
/// <see cref="ResetForTenantSwitch"/> so any in-flight request observing the scope token is
/// cancelled before it can paint cross-tenant data.
/// </summary>
public sealed class AdminPortalPartyQueryService(IPartiesAdminPortalApiClient apiClient) : IDisposable
{
    private readonly Lock _scopeLock = new();
    private CancellationTokenSource _scopeCts = new();
    private bool _disposed;
    private AdminPortalAcceptedCommand? _acceptedCommand;

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

    /// <summary>Stages one accepted receipt only while the submitting circuit scope still owns it.</summary>
    /// <param name="partyId">The accepted party identifier.</param>
    /// <param name="contextSignature">The authorization context that submitted the command.</param>
    /// <param name="expectedUpdate">The submitted edit snapshot, or null for create identity convergence.</param>
    /// <param name="expectedScope">The circuit scope token captured before submission.</param>
    /// <returns>True when staged; false after cancellation, a scope reset or disposal.</returns>
    internal bool TryStageAcceptedCommand(string partyId, string contextSignature, UpdatePartyComposite? expectedUpdate, CancellationToken expectedScope)
    {
        lock (_scopeLock)
        {
            if (_disposed || expectedScope.IsCancellationRequested || expectedScope != _scopeCts.Token)
            {
                return false;
            }

            _acceptedCommand = new(partyId, contextSignature, expectedUpdate);
            return true;
        }
    }

    /// <summary>Consumes and clears the circuit's staged receipt, including a mismatched receipt.</summary>
    /// <param name="partyId">The party selected by the receiving route.</param>
    /// <param name="contextSignature">The receiving authorization context.</param>
    /// <returns>The matching receipt, or null when absent or owned by another route or context.</returns>
    internal AdminPortalAcceptedCommand? ConsumeAcceptedCommand(string? partyId, string contextSignature)
    {
        lock (_scopeLock)
        {
            AdminPortalAcceptedCommand? accepted = _acceptedCommand;
            _acceptedCommand = null;
            return accepted is { } command
                && string.Equals(command.PartyId, partyId, StringComparison.Ordinal)
                && string.Equals(command.ContextSignature, contextSignature, StringComparison.Ordinal)
                    ? command
                    : null;
        }
    }

    public void ResetForTenantSwitch()
    {
        CancellationTokenSource old;
        lock (_scopeLock)
        {
            if (_disposed)
            {
                return;
            }

            _acceptedCommand = null;
            old = Interlocked.Exchange(ref _scopeCts, new CancellationTokenSource());
        }

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
        lock (_scopeLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _acceptedCommand = null;
        }

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
