using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Events;

/// <summary>
/// Tier 2 publisher contract tests verifying dead-letter topic derivation and
/// transient publish failure behavior with a mocked <see cref="DaprClient"/>.
/// These tests do not simulate EventStore drain recovery end to end.
/// </summary>
public class DeadLetterRoutingTests
{
    private const string _correlationId = "corr-deadletter-001";
    private const string _tenantId = "tenant-a";
    private const string _domain = "parties";
    private const string _aggregateId = "550e8400-e29b-41d4-a716-446655440000";

    private static AggregateIdentity CreateIdentity(string tenantId = _tenantId)
        => new(tenantId, _domain, _aggregateId);

    private static CommandEnvelope CreateCommandEnvelope()
    {
        return new CommandEnvelope(
            MessageId: Guid.NewGuid().ToString(),
            TenantId: _tenantId,
            Domain: _domain,
            AggregateId: _aggregateId,
            CommandType: "CreateParty",
            Payload: System.Text.Encoding.UTF8.GetBytes("{}"),
            CorrelationId: _correlationId,
            CausationId: null,
            UserId: "test-user@example.com",
            Extensions: null);
    }

    private static DeadLetterMessage CreateDeadLetterMessage()
    {
        CommandEnvelope command = CreateCommandEnvelope();
        return DeadLetterMessage.FromException(
            command,
            CommandStatus.PublishFailed,
            new InvalidOperationException("Pub/sub unavailable"));
    }

    private static (DeadLetterPublisher Publisher, DaprClient MockClient) CreateDeadLetterPublisher()
    {
        DaprClient mockClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions());
        var publisher = new DeadLetterPublisher(
            mockClient,
            options,
            NullLogger<DeadLetterPublisher>.Instance);
        return (publisher, mockClient);
    }

    private static (EventPublisher Publisher, DaprClient MockClient) CreateEventPublisher()
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
    public async Task PublishDeadLetter_FailedDelivery_RoutesToDeadLetterTopicAsync()
    {
        // Arrange
        (DeadLetterPublisher publisher, DaprClient mockClient) = CreateDeadLetterPublisher();
        AggregateIdentity identity = CreateIdentity();
        DeadLetterMessage message = CreateDeadLetterMessage();

        string? capturedTopic = null;
        await mockClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Do<string>(t => capturedTopic = t),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());

        // Act
        bool result = await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        result.ShouldBeTrue();
        capturedTopic.ShouldBe("deadletter.tenant-a.parties.events");
    }

    [Fact]
    public async Task PublishEvents_PubSubUnavailable_ReturnsFailureResultAsync()
    {
        // Arrange — simulate persist-then-publish: events were persisted but publish fails
        (EventPublisher publisher, DaprClient mockClient) = CreateEventPublisher();
        AggregateIdentity identity = CreateIdentity();

        byte[] payload = System.Text.Encoding.UTF8.GetBytes("{}");
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
                Timestamp: DateTimeOffset.UtcNow,
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

        mockClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection refused"));

        // Act
        EventPublishResult result = await publisher.PublishEventsAsync(
            identity, events, _correlationId);

        // Assert — events were persisted (by the actor pipeline), publish failure is reported
        result.Success.ShouldBeFalse();
        result.PublishedCount.ShouldBe(0);
        result.FailureReason.ShouldNotBeNull();
        result.FailureReason.ShouldContain("ReasonCode=protected-data-diagnostic-redacted");
        result.FailureReason.ShouldNotContain("Connection refused");
    }

    [Fact]
    public async Task PublishEvents_SecondAttemptAfterTransientFailure_SucceedsAsync()
    {
        // Arrange — simulate a transient broker failure followed by a successful second attempt
        (EventPublisher publisher, DaprClient mockClient) = CreateEventPublisher();
        AggregateIdentity identity = CreateIdentity();

        byte[] payload = System.Text.Encoding.UTF8.GetBytes("{}");
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
                Timestamp: DateTimeOffset.UtcNow,
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

        int callCount = 0;
        mockClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Temporary broker outage");
                }

                return Task.CompletedTask;
            });

        // Act — first attempt fails
        EventPublishResult failedResult = await publisher.PublishEventsAsync(
            identity, events, _correlationId);

        // Recovery attempt succeeds
        EventPublishResult recoveredResult = await publisher.PublishEventsAsync(
            identity, events, _correlationId);

        // Assert
        failedResult.Success.ShouldBeFalse();
        recoveredResult.Success.ShouldBeTrue();
        recoveredResult.PublishedCount.ShouldBe(1);
    }
}
