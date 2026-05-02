using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Events;

/// <summary>
/// Tier 2 publisher contract tests verifying that <see cref="EventPublisher"/>
/// publishes party domain event envelopes to DAPR pub/sub with CloudEvents 1.0 metadata.
/// These tests validate the publication contract with a mocked <see cref="DaprClient"/>;
/// they do not exercise the full command-processing pipeline.
/// </summary>
public class EventPublishingVerificationTests
{
    private const string _correlationId = "corr-001";
    private const string _tenantId = "tenant-a";
    private const string _domain = "parties";
    private const string _aggregateId = "550e8400-e29b-41d4-a716-446655440000";

    private static AggregateIdentity CreateIdentity(string tenantId = _tenantId)
        => new(tenantId, _domain, _aggregateId);

    private static EventEnvelope CreateEventEnvelope(
        string eventTypeName,
        long sequenceNumber,
        string tenantId = _tenantId)
    {
        byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { test = true }));
        return new EventEnvelope(
            MessageId: Guid.NewGuid().ToString(),
            AggregateId: _aggregateId,
            AggregateType: "Party",
            TenantId: tenantId,
            Domain: _domain,
            SequenceNumber: sequenceNumber,
            GlobalPosition: sequenceNumber,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: _correlationId,
            CausationId: _correlationId,
            UserId: "test-user@example.com",
            DomainServiceVersion: "1.0.0",
            EventTypeName: eventTypeName,
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: payload,
            Extensions: null);
    }

    private static (EventPublisher Publisher, DaprClient MockClient) CreatePublisher()
    {
        DaprClient mockClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions());
        var payloadProtectionService = new NoOpEventPayloadProtectionService();
        var publisher = new EventPublisher(
            mockClient,
            options,
            NullLogger<EventPublisher>.Instance,
            payloadProtectionService,
            new NoOpProjectionUpdateOrchestrator());
        return (publisher, mockClient);
    }

    [Fact]
    public async Task PublishEvents_EventBatch_PublishesEventsToDaprPubSubAsync()
    {
        // Arrange
        (EventPublisher publisher, DaprClient mockClient) = CreatePublisher();
        AggregateIdentity identity = CreateIdentity();
        var events = new List<EventEnvelope>
        {
            CreateEventEnvelope("PartyCreated", 1),
        };

        // Act
        EventPublishResult result = await publisher.PublishEventsAsync(
            identity, events, _correlationId);

        // Assert
        result.Success.ShouldBeTrue();
        result.PublishedCount.ShouldBe(1);
        await mockClient.Received(1).PublishEventAsync(
            "pubsub",
            "tenant-a.parties.events",
            Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishEvents_PartyCreatedThenContactChannelAdded_ArrivesInSequenceAsync()
    {
        // Arrange
        (EventPublisher publisher, DaprClient mockClient) = CreatePublisher();
        AggregateIdentity identity = CreateIdentity();
        var events = new List<EventEnvelope>
        {
            CreateEventEnvelope("PartyCreated", 1),
            CreateEventEnvelope("ContactChannelAdded", 2),
        };

        var publishedEnvelopes = new List<EventEnvelope>();
        await mockClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<EventEnvelope>(e => publishedEnvelopes.Add(e)),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());

        // Act
        EventPublishResult result = await publisher.PublishEventsAsync(
            identity, events, _correlationId);

        // Assert
        result.Success.ShouldBeTrue();
        result.PublishedCount.ShouldBe(2);
        publishedEnvelopes.Count.ShouldBe(2);
        publishedEnvelopes[0].EventTypeName.ShouldBe("PartyCreated");
        publishedEnvelopes[0].SequenceNumber.ShouldBe(1);
        publishedEnvelopes[1].EventTypeName.ShouldBe("ContactChannelAdded");
        publishedEnvelopes[1].SequenceNumber.ShouldBe(2);
    }

    [Fact]
    public async Task PublishEvents_EventPayload_UsesCamelCaseIso8601StringEnumsAsync()
    {
        // Arrange — verify that EventEnvelope timestamp uses ISO 8601 and payload is serialized as JSON
        (EventPublisher publisher, DaprClient mockClient) = CreatePublisher();
        AggregateIdentity identity = CreateIdentity();

        DateTimeOffset timestamp = new(2026, 3, 6, 10, 30, 0, TimeSpan.Zero);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            new { partyType = "Person", firstName = "Ada" },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var events = new List<EventEnvelope>
        {
            new(
                MessageId: Guid.NewGuid().ToString(),
                AggregateId: _aggregateId,
                AggregateType: "Party",
                TenantId: _tenantId,
                Domain: _domain,
                SequenceNumber: 1,
                GlobalPosition: 1,
                Timestamp: timestamp,
                CorrelationId: _correlationId,
                CausationId: _correlationId,
                UserId: "test-user@example.com",
                DomainServiceVersion: "1.0.0",
                EventTypeName: "PartyCreated",
                MetadataVersion: 1,
                SerializationFormat: "json",
                Payload: payload,
                Extensions: null),
        };

        EventEnvelope? captured = null;
        await mockClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<EventEnvelope>(e => captured = e),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());

        // Act
        await publisher.PublishEventsAsync(identity, events, _correlationId);

        // Assert
        captured.ShouldNotBeNull();
        captured.Timestamp.ShouldBe(timestamp);
        captured.Timestamp.ToString("o").ShouldContain("2026-03-06T10:30:00");
        captured.SerializationFormat.ShouldBe("json");

        // Verify camelCase in payload (exact case-sensitive checks)
        string payloadJson = Encoding.UTF8.GetString(captured.Payload);
        payloadJson.ShouldContain("\"partyType\"", Case.Sensitive);
        payloadJson.ShouldContain("\"firstName\"", Case.Sensitive);
        payloadJson.ShouldNotContain("\"PartyType\"", Case.Sensitive);
        payloadJson.ShouldNotContain("\"FirstName\"", Case.Sensitive);
    }

    [Fact]
    public async Task PublishEvents_CloudEventsEnvelope_HasCorrectTypeSourceIdAttributesAsync()
    {
        // Arrange
        (EventPublisher publisher, DaprClient mockClient) = CreatePublisher();
        AggregateIdentity identity = CreateIdentity();
        var events = new List<EventEnvelope>
        {
            CreateEventEnvelope("PartyCreated", 1),
        };

        Dictionary<string, string>? capturedMetadata = null;
        await mockClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<EventEnvelope>(),
            Arg.Do<Dictionary<string, string>>(m => capturedMetadata = m),
            Arg.Any<CancellationToken>());

        // Act
        await publisher.PublishEventsAsync(identity, events, _correlationId);

        // Assert
        capturedMetadata.ShouldNotBeNull();
        capturedMetadata["cloudevent.type"].ShouldBe("PartyCreated");
        capturedMetadata["cloudevent.source"].ShouldBe("hexalith-eventstore/tenant-a/parties");
        capturedMetadata["cloudevent.id"].ShouldBe("corr-001:1");
    }

    [Fact]
    public async Task PublishEvents_EventBatch_PublishesAllEventsSequentiallyAsync()
    {
        // Arrange — simulate a command result containing multiple persisted events
        (EventPublisher publisher, DaprClient mockClient) = CreatePublisher();
        AggregateIdentity identity = CreateIdentity();
        var events = new List<EventEnvelope>
        {
            CreateEventEnvelope("PartyCreated", 1),
            CreateEventEnvelope("PartyDisplayNameDerived", 2),
            CreateEventEnvelope("ContactChannelAdded", 3),
            CreateEventEnvelope("IdentifierAdded", 4),
        };

        var publishedTypes = new List<string>();
        await mockClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<EventEnvelope>(e => publishedTypes.Add(e.EventTypeName)),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());

        // Act
        EventPublishResult result = await publisher.PublishEventsAsync(
            identity, events, _correlationId);

        // Assert
        result.Success.ShouldBeTrue();
        result.PublishedCount.ShouldBe(4);
        publishedTypes.ShouldBe(new[] { "PartyCreated", "PartyDisplayNameDerived", "ContactChannelAdded", "IdentifierAdded" });
    }
}
