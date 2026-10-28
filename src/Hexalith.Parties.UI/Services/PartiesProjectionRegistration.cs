using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.UI.Services;

/// <summary>Owns registration and removal so delayed transport work cannot outlive an operation.</summary>
internal sealed class PartiesProjectionRegistration(
    IProjectionStream stream,
    ILogger logger,
    string projectionType,
    string tenant,
    Action onConfirmed,
    Action<Guid> removed) : IDisposable, IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _disposal;
    private int _disposed;

    /// <summary>Gets the wrapper's registration identity.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Starts the owned registration attempt.</summary>
    public void Start() => _ = RegisterAsync();

    /// <summary>Waits for successful transport registration.</summary>
    /// <param name="cancellationToken">The operation lifetime.</param>
    /// <returns>The registration readiness.</returns>
    public Task WaitUntilReadyAsync(CancellationToken cancellationToken) => _ready.Task.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _ = DisposeAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            return new ValueTask(_disposal ??= RemoveAsync());
        }
    }

    private void Confirm()
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            onConfirmed();
        }
    }

    private async Task RegisterAsync()
    {
        try
        {
            try
            {
                await stream.EnsureStartedAsync(_lifetime.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (!_lifetime.IsCancellationRequested && !stream.IsConnected)
            {
                // Offline commands retain their polling path; the transport can register for a later join.
                logger.LogWarning(exception, "Projection connection deferred (stream unavailable).");
            }
            if (Volatile.Read(ref _disposed) == 0)
            {
                await stream.SubscribeAsync(projectionType, tenant, Confirm).ConfigureAwait(false);
                _ready.TrySetResult();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            _ready.TrySetCanceled(_lifetime.Token);
        }
        catch (Exception exception)
        {
            _ready.TrySetException(exception);
            logger.LogWarning(exception, "Projection subscription registration deferred (stream unavailable).");
        }
        finally
        {
            if (_lifetime.IsCancellationRequested)
            {
                _ready.TrySetCanceled(_lifetime.Token);
            }
            _finished.TrySetResult();
        }
    }

    private async Task RemoveAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await _finished.Task.ConfigureAwait(false);
        try
        {
            await stream.UnsubscribeAsync(projectionType, tenant, Confirm).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Projection unsubscribe deferred (stream unavailable).");
        }
        finally
        {
            removed(Id);
            _lifetime.Dispose();
        }
    }
}
