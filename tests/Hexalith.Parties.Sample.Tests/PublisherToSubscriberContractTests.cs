using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.Parties.Sample;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using SampleEventEnvelope = Hexalith.Parties.Sample.EventEnvelope;
using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.Parties.Sample.Tests;

[Collection("PartyEventHandler")]
public sealed class PublisherToSubscriberContractTests : IDisposable
{
    private readonly HttpClient _client;

    public PublisherToSubscriberContractTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        CustomerSummaryStore.Customers.Clear();
        PartyEventHandler.ClearProcessedEventIds();
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        CustomerSummaryStore.Customers.Clear();
        _client.Dispose();
    }

    [Fact]
    public async Task PublisherGeneratedCloudEvents_FullyQualifiedEventNames_AreProcessedEndToEndAsync()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var publisher = new EventPublisher(
            daprClient,
            Options.Create(new EventPublisherOptions()),
            NullLogger<EventPublisher>.Instance);

        AggregateIdentity identity = new("tenant-a", "parties", "p-publisher-contract");
        List<(ServerEventEnvelope Envelope, Dictionary<string, string> Metadata)> publishedEvents = [];

        await daprClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<ServerEventEnvelope>(envelope => publishedEvents.Add((envelope, new Dictionary<string, string>()))),
            Arg.Do<Dictionary<string, string>>(metadata =>
            {
                int lastIndex = publishedEvents.Count - 1;
                publishedEvents[lastIndex] = (publishedEvents[lastIndex].Envelope, new Dictionary<string, string>(metadata));
            }),
            Arg.Any<CancellationToken>());

        List<ServerEventEnvelope> envelopes =
        [
            CreateServerEnvelope(identity, 1, "Hexalith.Parties.Contracts.Events.PartyCreated", new
            {
                type = "Person",
                personDetails = new { firstName = "Publisher", lastName = "Contract" },
            }),
            CreateServerEnvelope(identity, 2, "Hexalith.Parties.Contracts.Events.ContactChannelAdded", new
            {
                contactChannelId = "cc-email",
                type = "Email",
                value = "publisher@example.com",
                isPreferred = true,
            }),
            CreateServerEnvelope(identity, 3, "Hexalith.Parties.Contracts.Events.IdentifierAdded", new
            {
                identifierId = "id-1",
                type = "VAT",
                value = "FR12345678901",
            }),
            CreateServerEnvelope(identity, 4, "Hexalith.Parties.Contracts.Events.PartyDeactivated", new { }),
        ];

        EventPublishResult publishResult = await publisher.PublishEventsAsync(identity, envelopes, "corr-publisher-contract");

        publishResult.Success.ShouldBeTrue();
        publishedEvents.Count.ShouldBe(4);

        foreach ((ServerEventEnvelope envelope, Dictionary<string, string> metadata) in publishedEvents)
        {
            HttpResponseMessage response = await PostPublishedEventAsync(envelope, metadata);
            response.EnsureSuccessStatusCode();
        }

        CustomerSummary summary = CustomerSummaryStore.Customers[identity.AggregateId];
        summary.DisplayName.ShouldBe("Publisher Contract");
        summary.Email.ShouldBe("publisher@example.com");
        summary.IdentifierCount.ShouldBe(1);
        summary.IsActive.ShouldBeFalse();
    }

    private static ServerEventEnvelope CreateServerEnvelope(
        AggregateIdentity identity,
        long sequenceNumber,
        string eventTypeName,
        object payload)
    {
        byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        return new ServerEventEnvelope(
            AggregateId: identity.AggregateId,
            TenantId: identity.TenantId,
            Domain: identity.Domain,
            SequenceNumber: sequenceNumber,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-publisher-contract",
            CausationId: "corr-publisher-contract",
            UserId: "reviewer@example.com",
            DomainServiceVersion: "1.0.0",
            EventTypeName: eventTypeName,
            SerializationFormat: "json",
            Payload: payloadBytes,
            Extensions: null);
    }

    private async Task<HttpResponseMessage> PostPublishedEventAsync(
        ServerEventEnvelope envelope,
        Dictionary<string, string> metadata)
    {
        var sampleEnvelope = new SampleEventEnvelope
        {
            AggregateId = envelope.AggregateId,
            TenantId = envelope.TenantId,
            Domain = envelope.Domain,
            SequenceNumber = envelope.SequenceNumber,
            Timestamp = envelope.Timestamp,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            UserId = envelope.UserId,
            DomainServiceVersion = envelope.DomainServiceVersion,
            EventTypeName = envelope.EventTypeName,
            SerializationFormat = envelope.SerializationFormat,
            Payload = Convert.ToBase64String(envelope.Payload),
            Extensions = metadata.ToDictionary(static kvp => kvp.Key, static kvp => (object)kvp.Value),
        };

        string json = JsonSerializer.Serialize(new
        {
            specversion = "1.0",
            id = metadata["cloudevent.id"],
            source = metadata["cloudevent.source"],
            type = metadata["cloudevent.type"],
            datacontenttype = "application/json",
            data = sampleEnvelope,
        });

        using StringContent content = new(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/events/parties", content).ConfigureAwait(true);
    }
}
