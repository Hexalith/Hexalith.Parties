namespace Hexalith.Parties.Queries;

/// <summary>Finite process-wide admission for identity work, with no waiting-worker queue.</summary>
internal sealed class PartyIdentityQueryWorkLimiter
{
    private readonly SemaphoreSlim _capacity;

    /// <summary>Gets the shared bound for current and historical queries across service instances.</summary>
    internal static PartyIdentityQueryWorkLimiter Shared { get; } = new(16);

    /// <summary>Creates an admission boundary with a fixed positive capacity.</summary>
    internal PartyIdentityQueryWorkLimiter(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = new(capacity, capacity);
    }

    /// <summary>Acquires immediately or rejects saturation without queuing work.</summary>
    internal bool TryAcquire() => _capacity.Wait(0);

    /// <summary>Restores one lease after its work and cancellation callbacks have completed.</summary>
    internal void Release() => _capacity.Release();
}
