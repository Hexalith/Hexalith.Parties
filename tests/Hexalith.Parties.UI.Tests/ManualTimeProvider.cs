namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// A minimal, deterministic <see cref="TimeProvider"/> test double (the repo's CPM does not carry
/// <c>Microsoft.Extensions.TimeProvider.Testing</c>, and Story 1.7 adds no new package version). Supports
/// <see cref="GetUtcNow"/>, one-shot/periodic <see cref="CreateTimer"/>, and <see cref="Advance"/> — enough
/// to drive the <c>ProjectionFreshnessFallback</c> poll loop (which schedules a fresh one-shot timer per
/// interval). Timer callbacks fire <strong>outside</strong> the internal lock and use synchronous
/// continuations, so an <c>Advance(interval)</c> deterministically completes one poll before returning.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new ManualTimer(this, callback, state);
        lock (_gate)
        {
            timer.Schedule(_now, dueTime, period);
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>Moves the clock forward, firing every timer that becomes due (in chronological order).</summary>
    public void Advance(TimeSpan amount)
    {
        DateTimeOffset target;
        lock (_gate)
        {
            target = _now + amount;
        }

        while (true)
        {
            ManualTimer? next = null;
            lock (_gate)
            {
                foreach (ManualTimer timer in _timers)
                {
                    if (timer.NextFire is { } fire && fire <= target && (next is null || fire < next.NextFire))
                    {
                        next = timer;
                    }
                }

                if (next is null)
                {
                    _now = target;
                    return;
                }

                _now = next.NextFire!.Value;
                next.PrepareNextFire();
            }

            next.Fire(); // outside the lock: an inline continuation may schedule a new timer
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_gate)
        {
            _ = _timers.Remove(timer);
        }
    }

    private bool ChangeTimer(ManualTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            timer.Schedule(_now, dueTime, period);
        }

        return true;
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public DateTimeOffset? NextFire { get; private set; }

        public void Schedule(DateTimeOffset now, TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            NextFire = dueTime == Timeout.InfiniteTimeSpan ? null : now + dueTime;
        }

        public void PrepareNextFire()
            => NextFire = _period <= TimeSpan.Zero || _period == Timeout.InfiniteTimeSpan
                ? null
                : NextFire + _period;

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period) => owner.ChangeTimer(this, dueTime, period);

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            owner.Remove(this);
            return ValueTask.CompletedTask;
        }
    }
}
