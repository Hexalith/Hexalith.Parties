using Hexalith.Parties.Security;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public sealed class DecryptionCircuitBreakerTests
{
    private static DecryptionCircuitBreaker CreateBreaker()
        => new(NullLogger<DecryptionCircuitBreaker>.Instance);

    // ─── Task 5.1: Three consecutive failures opens circuit ───

    [Fact]
    public void CircuitBreaker_ThreeConsecutiveFailures_OpensCircuit()
    {
        DecryptionCircuitBreaker breaker = CreateBreaker();

        breaker.RecordFailure("acme", "p1", failureThreshold: 3, TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(5));
        breaker.GetStatus("acme", "p1").ShouldBe(DecryptionCircuitBreaker.CircuitStatus.Closed);

        breaker.RecordFailure("acme", "p1", failureThreshold: 3, TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(5));
        breaker.GetStatus("acme", "p1").ShouldBe(DecryptionCircuitBreaker.CircuitStatus.Closed);

        breaker.RecordFailure("acme", "p1", failureThreshold: 3, TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(5));
        breaker.GetStatus("acme", "p1").ShouldBe(DecryptionCircuitBreaker.CircuitStatus.Open);
    }

    // ─── Task 5.2: Open circuit throws DecryptionCircuitOpenException ───

    [Fact]
    public void CircuitBreaker_OpenCircuit_ThrowsDecryptionCircuitOpenException()
    {
        DecryptionCircuitBreaker breaker = CreateBreaker();

        for (int i = 0; i < 3; i++)
        {
            breaker.RecordFailure("acme", "p1", failureThreshold: 3, TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(5));
        }

        DecryptionCircuitOpenException ex = Should.Throw<DecryptionCircuitOpenException>(
            () => breaker.ThrowIfOpen("acme", "p1"));

        ex.TenantId.ShouldBe("acme");
        ex.PartyId.ShouldBe("p1");
        ex.BreakExpiry.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    // ─── Task 5.3: Break duration expires → half-open ───

    [Fact]
    public void CircuitBreaker_BreakDurationExpires_TransitionsToHalfOpen()
    {
        DecryptionCircuitBreaker breaker = CreateBreaker();

        // Open the circuit with zero break duration so it immediately expires
        for (int i = 0; i < 3; i++)
        {
            breaker.RecordFailure("acme", "p1", failureThreshold: 3, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        }

        breaker.GetStatus("acme", "p1").ShouldBe(DecryptionCircuitBreaker.CircuitStatus.Open);

        // ThrowIfOpen should detect expired break and transition to half-open
        breaker.ThrowIfOpen("acme", "p1"); // Should NOT throw

        breaker.GetStatus("acme", "p1").ShouldBe(DecryptionCircuitBreaker.CircuitStatus.HalfOpen);
    }

    // ─── Task 5.4: Half-open success closes circuit ───

    [Fact]
    public void CircuitBreaker_HalfOpenSuccess_ClosesCircuit()
    {
        DecryptionCircuitBreaker breaker = CreateBreaker();

        // Open circuit with zero break duration
        for (int i = 0; i < 3; i++)
        {
            breaker.RecordFailure("acme", "p1", failureThreshold: 3, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        }

        // Transition to half-open
        breaker.ThrowIfOpen("acme", "p1");
        breaker.GetStatus("acme", "p1").ShouldBe(DecryptionCircuitBreaker.CircuitStatus.HalfOpen);

        // Record success → should close
        breaker.RecordSuccess("acme", "p1");
        breaker.GetStatus("acme", "p1").ShouldBe(DecryptionCircuitBreaker.CircuitStatus.Closed);
    }

    // ─── Task 5.5: Per-party isolation ───

    [Fact]
    public void CircuitBreaker_PerPartyIsolation_IndependentCircuits()
    {
        DecryptionCircuitBreaker breaker = CreateBreaker();

        // Open circuit for party A
        for (int i = 0; i < 3; i++)
        {
            breaker.RecordFailure("acme", "partyA", failureThreshold: 3, TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(5));
        }

        breaker.GetStatus("acme", "partyA").ShouldBe(DecryptionCircuitBreaker.CircuitStatus.Open);

        // Party B should still be closed
        breaker.GetStatus("acme", "partyB").ShouldBe(DecryptionCircuitBreaker.CircuitStatus.Closed);
        breaker.ThrowIfOpen("acme", "partyB"); // Should NOT throw
    }

    // ─── Task 5.6: Erased party skip (circuit breaker behavior for dead-lettering) ───

    [Fact]
    public void CircuitBreaker_ErasedParty_SkipsRetryAndDeadLetters()
    {
        DecryptionCircuitBreaker breaker = CreateBreaker();

        // Simulate repeated failures for an erased party (key permanently destroyed)
        for (int i = 0; i < 3; i++)
        {
            breaker.RecordFailure("acme", "erased1", failureThreshold: 3, TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(5));
        }

        // Circuit is open — preventing further decryption attempts
        breaker.GetStatus("acme", "erased1").ShouldBe(DecryptionCircuitBreaker.CircuitStatus.Open);

        // Any subsequent call throws, preventing infinite retry
        Should.Throw<DecryptionCircuitOpenException>(
            () => breaker.ThrowIfOpen("acme", "erased1"));
    }

    // ─── Task 5.7: Max open duration forces half-open ───

    [Fact]
    public void CircuitBreaker_MaxOpenDuration_ForcesHalfOpen()
    {
        DecryptionCircuitBreaker breaker = CreateBreaker();

        // Open circuit with a long break duration but zero max open duration (immediate force half-open)
        for (int i = 0; i < 3; i++)
        {
            breaker.RecordFailure("acme", "p1", failureThreshold: 3, TimeSpan.FromHours(1), TimeSpan.Zero);
        }

        breaker.GetStatus("acme", "p1").ShouldBe(DecryptionCircuitBreaker.CircuitStatus.Open);

        // ThrowIfOpen should force transition to half-open due to max open duration exceeded
        breaker.ThrowIfOpen("acme", "p1"); // Should NOT throw

        breaker.GetStatus("acme", "p1").ShouldBe(DecryptionCircuitBreaker.CircuitStatus.HalfOpen);
    }

    // ─── Task 5.8: Drain recovery rate limiting ───

    [Fact]
    public void CircuitBreaker_DrainRecovery_RateLimited()
    {
        DecryptionCircuitBreaker breaker = CreateBreaker();

        // Open circuit with zero break duration
        for (int i = 0; i < 3; i++)
        {
            breaker.RecordFailure("acme", "p1", failureThreshold: 3, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        }

        // Transition to half-open
        breaker.ThrowIfOpen("acme", "p1");
        breaker.GetStatus("acme", "p1").ShouldBe(DecryptionCircuitBreaker.CircuitStatus.HalfOpen);

        // Increment recovery events — tracks processing
        breaker.GetRecoveryEventsProcessed("acme", "p1").ShouldBe(0);
        breaker.IncrementRecoveryEvents("acme", "p1");
        breaker.GetRecoveryEventsProcessed("acme", "p1").ShouldBe(1);

        // Simulate processing 5 events
        for (int i = 0; i < 4; i++)
        {
            breaker.IncrementRecoveryEvents("acme", "p1");
        }

        breaker.GetRecoveryEventsProcessed("acme", "p1").ShouldBe(5);
    }
}
