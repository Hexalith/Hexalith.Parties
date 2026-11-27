namespace Hexalith.Parties.UI.Services;

/// <summary>Holds one profile operation in the end-to-end fixture until the browser releases it.</summary>
/// <param name="sync">The shared fixture lock used to reset gates and specimen data together.</param>
internal sealed class PartiesAdminPortalE2eProfileGate(object sync)
{
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(30);

    private TaskCompletionSource? _armed;
    private TaskCompletionSource? _active;

    /// <summary>Arms the next operation and releases any previous wait.</summary>
    public void Arm()
    {
        lock (sync)
        {
            ReleasePending();
            _armed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Releases and clears any armed or active wait.</summary>
    public void Release()
    {
        lock (sync)
        {
            ReleasePending();
        }
    }

    /// <summary>Consumes the armed gate and waits for its release, if present.</summary>
    /// <param name="cancellationToken">The cancellation token for the fixture operation.</param>
    /// <returns>A task that completes when the fixture operation may proceed.</returns>
    public Task WaitAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        lock (sync)
        {
            if (_armed is not { } gate)
            {
                return Task.CompletedTask;
            }

            _armed = null;
            _active = gate;
            return AwaitReleaseAsync(gate, cancellationToken);
        }
    }

    private void ReleasePending()
    {
        _armed?.TrySetResult();
        _active?.TrySetResult();
        _armed = null;
        _active = null;
    }

    private async Task AwaitReleaseAsync(TaskCompletionSource gate, CancellationToken cancellationToken)
    {
        try
        {
            await gate.Task.WaitAsync(GateTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            gate.TrySetResult();
        }
        finally
        {
            lock (sync)
            {
                if (ReferenceEquals(_active, gate))
                {
                    _active = null;
                }
            }
        }
    }
}
