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

    /// <summary>Starts one finite deadline measured by the owning service's clock.</summary>
    public PartyIdentityQueryDeadline(TimeSpan timeout, TimeProvider timeProvider, CancellationToken callerToken)
    {
        _callerToken = callerToken;
        _timeProvider = timeProvider;
        _startedAt = timeProvider.GetTimestamp();
        _timeout = timeout;
        _timeoutSource = new(timeout, timeProvider);
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
        CancellationToken token = _providerSource.Token;
        Task<T> pending = Task.Run(() => read(token), token);
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

    /// <inheritdoc/>
    public void Dispose()
    {
        _linkedSource.Dispose();
        _timeoutSource.Dispose();
        if (_providerCancellation is { IsCompleted: false } cancellation)
        {
            // Cleanup also stays independent of callbacks that have not returned yet.
            _ = cancellation.ContinueWith(static (_, state) => ((CancellationTokenSource)state!).Dispose(),
                _providerSource, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        else
        {
            _providerSource.Dispose();
        }
    }
}
