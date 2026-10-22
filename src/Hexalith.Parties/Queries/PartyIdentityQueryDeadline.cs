namespace Hexalith.Parties.Queries;

/// <summary>One monotonic operational budget for source invocation, custody and evidence release.</summary>
internal sealed class PartyIdentityQueryDeadline : IDisposable
{
    private readonly CancellationToken _callerToken;
    private readonly TimeProvider _timeProvider;
    private readonly long _startedAt;
    private readonly TimeSpan _timeout;
    private readonly CancellationTokenSource _timeoutSource;
    private readonly CancellationTokenSource _linkedSource;
    private readonly CancellationTokenSource _providerSource = new();
    private Task? _providerCancellation;
    private Task? _pending;
    private bool _hasLease;
    private bool _disposed;
    private readonly PartyIdentityQueryWorkLimiter _workLimiter;

    /// <summary>Gets completion of retained work and callback cleanup after disposal.</summary>
    internal Task WorkCompleted { get; private set; } = Task.CompletedTask;

    /// <summary>Starts the remaining timer for one finite budget measured from query entry.</summary>
    public PartyIdentityQueryDeadline(TimeSpan timeout, TimeProvider timeProvider, CancellationToken callerToken, long startedAt,
        PartyIdentityQueryWorkLimiter? workLimiter = null)
    {
        _workLimiter = workLimiter ?? PartyIdentityQueryWorkLimiter.Shared;
        _callerToken = callerToken;
        _timeProvider = timeProvider;
        _startedAt = startedAt;
        _timeout = timeout;
        TimeSpan remaining = timeout - timeProvider.GetElapsedTime(startedAt);
        _timeoutSource = new(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, timeProvider);
        _linkedSource = CancellationTokenSource.CreateLinkedTokenSource(callerToken, _timeoutSource.Token);
    }

    /// <summary>Rejects caller cancellation first, then elapsed or timer-expired deadlines.</summary>
    public void ThrowIfCancellationRequested()
    {
        if (_callerToken.IsCancellationRequested)
        {
            CancelProvider();
        }

        _callerToken.ThrowIfCancellationRequested();
        if (_timeProvider.GetElapsedTime(_startedAt) >= _timeout)
        {
            _timeoutSource.Cancel();
        }

        if (_linkedSource.IsCancellationRequested)
        {
            CancelProvider();
        }

        _linkedSource.Token.ThrowIfCancellationRequested();
    }

    /// <summary>Bounds both synchronous provider invocation and its noncooperative returned task.</summary>
    public async Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read)
    {
        ThrowIfCancellationRequested();
        if (!_hasLease)
        {
            // No admission queue: saturation fails closed without allocating more workers.
            _hasLease = _workLimiter.TryAcquire();
            if (!_hasLease)
            {
                ThrowIfCancellationRequested();
                throw new InvalidOperationException("Identity query work capacity is exhausted.");
            }
        }

        ThrowIfCancellationRequested();
        CancellationToken token = _providerSource.Token;
        Task<T> pending = Task.Run(() => read(token), token);
        _pending = pending;
        // A provider can fault after the caller has stopped waiting. Observe that fault
        // without ever resuming the identity query or releasing its result.
        _ = pending.ContinueWith(static task => { _ = task.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try
        {
            // The query's private token has no provider callbacks. A provider may block
            // indefinitely in its own cancellation callback without delaying this wait.
            T result = await pending.WaitAsync(_linkedSource.Token).ConfigureAwait(false);
            ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            if (_linkedSource.IsCancellationRequested)
            {
                CancelProvider();
            }
        }
    }

    private void CancelProvider()
    {
        if (_providerCancellation is not null)
        {
            return;
        }

        // CancelAsync signals the token immediately and runs provider callbacks away
        // from the query continuation. Callback failures must never release evidence.
        _providerCancellation = _providerSource.CancelAsync();
        _ = _providerCancellation.ContinueWith(static task => { _ = task.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>Bounds synchronous admission and replay using the same budget and work lease.</summary>
    public Task<T> RunAsync<T>(Func<CancellationToken, T> work)
        => ReadAsync(token => Task.FromResult(work(token)));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        _linkedSource.Dispose();
        _timeoutSource.Dispose();
        // Successful previous operations are complete. The last operation may still be
        // running, and callbacks from any operation may still own work on this token.
        // Retain the one query lease until both settle; this does not terminate them.
        WorkCompleted = ReleaseAfterWorkAsync(_pending ?? Task.CompletedTask, _providerCancellation ?? Task.CompletedTask);
    }

    private async Task ReleaseAfterWorkAsync(Task pending, Task cancellation)
    {
        try { await pending.ConfigureAwait(false); }
        catch (Exception) { /* Observe abandoned provider faults without releasing evidence. */ }
        try { await cancellation.ConfigureAwait(false); }
        catch (Exception) { /* Observe asynchronous provider callback faults as well. */ }
        _providerSource.Dispose();
        if (_hasLease)
        {
            _workLimiter.Release();
        }
    }
}
