using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Security;

/// <summary>
/// Lightweight per-party circuit breaker for decryption failures.
/// Only tracks non-closed circuits — memory is proportional to active failures.
/// </summary>
public sealed partial class DecryptionCircuitBreaker(
    ILogger<DecryptionCircuitBreaker> logger)
{
    private static readonly Counter<long> s_circuitBreakerTrips = PartyKeyManagementService.s_meter
        .CreateCounter<long>("parties.encryption.circuit_breaker_trips", description: "Number of circuit breaker trips");

    private readonly ConcurrentDictionary<string, CircuitState> _circuits = new(StringComparer.Ordinal);

    /// <summary>
    /// Checks whether the circuit is open for a given party and throws if so.
    /// </summary>
    public void ThrowIfOpen(string tenantId, string partyId)
    {
        string key = GetKey(tenantId, partyId);
        if (!_circuits.TryGetValue(key, out CircuitState? state) || state is null)
        {
            return; // Closed (not tracked)
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Max open duration safeguard: force half-open after 5 minutes
        if (state.Status == CircuitStatus.Open && now - state.OpenedAt > state.MaxOpenDuration)
        {
            CircuitState halfOpen = state with { Status = CircuitStatus.HalfOpen };
            _ = _circuits.TryUpdate(key, halfOpen, state);
            LogCircuitHalfOpen(tenantId, partyId, "max open duration exceeded");
            return; // Allow this call through as half-open
        }

        if (state.Status == CircuitStatus.Open)
        {
            if (now - state.OpenedAt >= state.BreakDuration)
            {
                // Break duration expired → transition to half-open
                CircuitState halfOpen = state with { Status = CircuitStatus.HalfOpen };
                _ = _circuits.TryUpdate(key, halfOpen, state);
                LogCircuitHalfOpen(tenantId, partyId, "break duration expired");
                return; // Allow this call through as half-open
            }

            throw new DecryptionCircuitOpenException(tenantId, partyId, state.OpenedAt + state.BreakDuration);
        }

        // HalfOpen: allow through (will be recorded as success/failure)
    }

    /// <summary>
    /// Records a decryption failure for a party.
    /// </summary>
    public void RecordFailure(string tenantId, string partyId, int failureThreshold, TimeSpan breakDuration, TimeSpan maxOpenDuration)
    {
        string key = GetKey(tenantId, partyId);

        _ = _circuits.AddOrUpdate(
            key,
            _ =>
            {
                // First failure
                if (failureThreshold <= 1)
                {
                    OnCircuitOpened(tenantId, partyId, 1, "unknown");
                    return new CircuitState
                    {
                        Status = CircuitStatus.Open,
                        FailureCount = 1,
                        OpenedAt = DateTimeOffset.UtcNow,
                        BreakDuration = breakDuration,
                        MaxOpenDuration = maxOpenDuration,
                    };
                }

                return new CircuitState
                {
                    Status = CircuitStatus.Closed,
                    FailureCount = 1,
                    BreakDuration = breakDuration,
                    MaxOpenDuration = maxOpenDuration,
                };
            },
            (_, existing) =>
            {
                int newCount = existing.FailureCount + 1;

                if (existing.Status == CircuitStatus.HalfOpen)
                {
                    // Half-open failure → back to open
                    OnCircuitOpened(tenantId, partyId, newCount, "transient");
                    return existing with
                    {
                        Status = CircuitStatus.Open,
                        FailureCount = newCount,
                        OpenedAt = DateTimeOffset.UtcNow,
                    };
                }

                if (newCount >= failureThreshold)
                {
                    OnCircuitOpened(tenantId, partyId, newCount, "transient");
                    return existing with
                    {
                        Status = CircuitStatus.Open,
                        FailureCount = newCount,
                        OpenedAt = DateTimeOffset.UtcNow,
                    };
                }

                return existing with { FailureCount = newCount };
            });
    }

    /// <summary>
    /// Records a successful decryption, closing the circuit.
    /// </summary>
    public void RecordSuccess(string tenantId, string partyId)
    {
        string key = GetKey(tenantId, partyId);
        if (_circuits.TryRemove(key, out _))
        {
            LogCircuitClosed(tenantId, partyId);
        }
    }

    /// <summary>
    /// Gets the current circuit status for a party (for testing/diagnostics).
    /// </summary>
    internal CircuitStatus GetStatus(string tenantId, string partyId)
    {
        string key = GetKey(tenantId, partyId);
        return _circuits.TryGetValue(key, out CircuitState? state) && state is not null ? state.Status : CircuitStatus.Closed;
    }

    /// <summary>
    /// Gets the drain recovery state for rate limiting.
    /// Returns the number of events processed in the current half-open recovery cycle.
    /// </summary>
    internal int GetRecoveryEventsProcessed(string tenantId, string partyId)
    {
        string key = GetKey(tenantId, partyId);
        return _circuits.TryGetValue(key, out CircuitState? state) && state is not null ? state.RecoveryEventsProcessed : 0;
    }

    /// <summary>
    /// Increments the recovery events counter for drain rate limiting.
    /// </summary>
    internal void IncrementRecoveryEvents(string tenantId, string partyId)
    {
        string key = GetKey(tenantId, partyId);
        if (_circuits.TryGetValue(key, out CircuitState? state) && state is not null && state.Status == CircuitStatus.HalfOpen)
        {
            _ = _circuits.TryUpdate(key, state with { RecoveryEventsProcessed = state.RecoveryEventsProcessed + 1 }, state);
        }
    }

    private static string GetKey(string tenantId, string partyId) => $"{tenantId}:{partyId}";

    private void OnCircuitOpened(string tenantId, string partyId, int failureCount, string reason)
    {
        LogCircuitOpened(tenantId, partyId, failureCount);
        s_circuitBreakerTrips.Add(1,
            new KeyValuePair<string, object?>("tenant", tenantId),
            new KeyValuePair<string, object?>("party_id", partyId),
            new KeyValuePair<string, object?>("reason", reason));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Decryption circuit breaker OPENED for {TenantId}/{PartyId} after {FailureCount} consecutive failures")]
    private partial void LogCircuitOpened(string tenantId, string partyId, int failureCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Decryption circuit breaker transitioned to HALF-OPEN for {TenantId}/{PartyId}: {Reason}")]
    private partial void LogCircuitHalfOpen(string tenantId, string partyId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Decryption circuit breaker CLOSED for {TenantId}/{PartyId} after successful decryption")]
    private partial void LogCircuitClosed(string tenantId, string partyId);

    internal enum CircuitStatus
    {
        Closed,
        Open,
        HalfOpen,
    }

    internal sealed record CircuitState
    {
        public CircuitStatus Status { get; init; }

        public int FailureCount { get; init; }

        public DateTimeOffset OpenedAt { get; init; }

        public TimeSpan BreakDuration { get; init; }

        public TimeSpan MaxOpenDuration { get; init; }

        public int RecoveryEventsProcessed { get; init; }
    }
}
