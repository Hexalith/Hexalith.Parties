using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Events;

/// <summary>
/// Tier 2 publisher contract tests verifying multi-tenant topic selection.
/// These tests focus on tenant-scoped publish behavior at the publisher boundary,
/// not on full subscriber topology wiring.
/// </summary>
public class TenantIsolationTests
{
    private const string _correlationId = "corr-tenant-001";
    private const string _domain = "parties";
    private const string _aggregateId = "550e8400-e29b-41d4-a716-446655440000";

    private static EventEnvelope CreateEventEnvelope(string tenantId, string eventTypeName, long sequenceNumber)
    {
        byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { test = true }));
        return new EventEnvelope(
            AggregateId: _aggregateId,
            TenantId: tenantId,
            Domain: _domain,
            SequenceNumber: sequenceNumber,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: _correlationId,
            CausationId: _correlationId,
            UserId: "test-user@example.com",
            DomainServiceVersion: "1.0.0",
            EventTypeName: eventTypeName,
            SerializationFormat: "json",
            Payload: payload,
            Extensions: null);
    }

    private static (EventPublisher Publisher, DaprClient MockClient) CreatePublisher()
    {
        DaprClient mockClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions());
        var publisher = new EventPublisher(
            mockClient,
            options,
            NullLogger<EventPublisher>.Instance);
        return (publisher, mockClient);
    }

    [Fact]
    public async Task PublishEvents_TenantScopedTopic_PublishesToCorrectTopicAsync()
    {
        // Arrange
        (EventPublisher publisher, DaprClient mockClient) = CreatePublisher();
        var identity = new AggregateIdentity("acme", _domain, _aggregateId);
        var events = new List<EventEnvelope>
        {
            CreateEventEnvelope("acme", "PartyCreated", 1),
        };

        string? capturedTopic = null;
        await mockClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Do<string>(t => capturedTopic = t),
            Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());

        // Act
        await publisher.PublishEventsAsync(identity, events, _correlationId);

        // Assert
        capturedTopic.ShouldBe("acme.parties.events");
    }

    [Fact]
    public async Task PublishEvents_TwoTenants_PublishedConcurrently_RemainTenantScopedAsync()
    {
        // Arrange
        (EventPublisher publisher, DaprClient mockClient) = CreatePublisher();

        var identityTenantA = new AggregateIdentity("tenant-a", _domain, _aggregateId);
        var identityTenantB = new AggregateIdentity("tenant-b", _domain, _aggregateId);

        var eventsTenantA = new List<EventEnvelope>
        {
            CreateEventEnvelope("tenant-a", "PartyCreated", 1),
        };
        var eventsTenantB = new List<EventEnvelope>
        {
            CreateEventEnvelope("tenant-b", "PartyCreated", 1),
        };

        var publishedTopics = new ConcurrentBag<string>();
        await mockClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Do<string>(t => publishedTopics.Add(t)),
            Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());

        // Act — publish for both tenants concurrently
        await Task.WhenAll(
            publisher.PublishEventsAsync(identityTenantA, eventsTenantA, _correlationId),
            publisher.PublishEventsAsync(identityTenantB, eventsTenantB, "corr-tenant-002"));

        // Assert — each tenant's events go to their own topic
        string[] topics = publishedTopics.ToArray();
        topics.Length.ShouldBe(2);
        topics.ShouldContain("tenant-a.parties.events");
        topics.ShouldContain("tenant-b.parties.events");
    }

    [Fact]
    public async Task PublishEvents_TenantContext_PresentInEventEnvelopeAsync()
    {
        // Arrange
        (EventPublisher publisher, DaprClient mockClient) = CreatePublisher();
        var identity = new AggregateIdentity("tenant-a", _domain, _aggregateId);
        var events = new List<EventEnvelope>
        {
            CreateEventEnvelope("tenant-a", "PartyCreated", 1),
        };

        EventEnvelope? captured = null;
        Dictionary<string, string>? capturedMetadata = null;
        await mockClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<EventEnvelope>(e => captured = e),
            Arg.Do<Dictionary<string, string>>(m => capturedMetadata = m),
            Arg.Any<CancellationToken>());

        // Act
        await publisher.PublishEventsAsync(identity, events, _correlationId);

        // Assert — tenant context in both envelope and CloudEvents source
        captured.ShouldNotBeNull();
        captured.TenantId.ShouldBe("tenant-a");
        capturedMetadata.ShouldNotBeNull();
        capturedMetadata["cloudevent.source"].ShouldContain("tenant-a");
    }
}
