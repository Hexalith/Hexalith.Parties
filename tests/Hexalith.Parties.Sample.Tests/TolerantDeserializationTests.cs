using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Sample;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hexalith.Parties.Sample.Tests;

[Collection("PartyEventHandler")]
public sealed class TolerantDeserializationTests : IDisposable
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client;

    public TolerantDeserializationTests(WebApplicationFactory<Program> factory)
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
    public async Task PartyMerged_DeserializedAndHandledGracefully_ReturnsOkAsync()
    {
        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-merge-1",
            "PartyMerged",
            new { survivorPartyId = "p-survivor", mergedPartyId = "p-merge-1" },
            "corr-merge",
            5);

        HttpResponseMessage response = await PostEventAsync("merge-event-1", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownEventType_FutureEventType_ReturnsOkAndContinuesAsync()
    {
        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-future",
            "FutureEventType",
            new { someField = "someValue", anotherField = 42 },
            "corr-future",
            1);

        HttpResponseMessage response = await PostEventAsync("future-event-1", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EventWithUnknownAdditionalJsonFields_DeserializesWithoutErrorAsync()
    {
        // Create a PartyCreated event with extra unknown fields in the payload
        object payloadObj = new
        {
            type = "Person",
            personDetails = new { firstName = "Jean", lastName = "Dupont" },
            unknownField1 = "should be ignored",
            unknownField2 = 999,
            nestedUnknown = new { deep = true },
        };

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-unknown-fields",
            "PartyCreated",
            payloadObj,
            "corr-uf",
            1);

        HttpResponseMessage response = await PostEventAsync("unknown-fields-1", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers.ContainsKey("p-unknown-fields").ShouldBeTrue();
        CustomerSummaryStore.Customers["p-unknown-fields"].DisplayName.ShouldBe("Jean Dupont");
    }

    [Fact]
    public async Task EventWithMissingOptionalFields_DeserializesWithDefaultsAsync()
    {
        // PartyCreated with Person type but minimal person details (no optional fields)
        object payloadObj = new
        {
            type = "Person",
            personDetails = new { firstName = "Minimal", lastName = "Person" },
            // organizationDetails is missing (optional)
        };

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-minimal",
            "PartyCreated",
            payloadObj,
            "corr-min",
            1);

        HttpResponseMessage response = await PostEventAsync("minimal-event-1", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-minimal"].DisplayName.ShouldBe("Minimal Person");

        // Also test ContactChannelUpdated with only partial fields
        EventEnvelope updateEnvelope = CreateEnvelope(
            "tenant-a:parties:p-minimal",
            "ContactChannelUpdated",
            new { contactChannelId = "cc-1" },  // type and value are optional/nullable
            "corr-min",
            2);

        HttpResponseMessage response2 = await PostEventAsync("minimal-event-2", updateEnvelope);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static EventEnvelope CreateEnvelope(
        string aggregateId,
        string eventTypeName,
        object payloadObj,
        string correlationId,
        long sequenceNumber)
    {
        string payloadJson = JsonSerializer.Serialize(payloadObj, _jsonOptions);
        string payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));

        return new EventEnvelope
        {
            AggregateId = aggregateId,
            TenantId = "tenant-a",
            Domain = "parties",
            SequenceNumber = sequenceNumber,
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            EventTypeName = eventTypeName,
            Payload = payloadBase64,
        };
    }

    private async Task<HttpResponseMessage> PostEventAsync(string cloudEventId, EventEnvelope envelope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cloudEventId);

        string json = JsonSerializer.Serialize(
            new
            {
                specversion = "1.0",
                id = cloudEventId,
                source = "hexalith-eventstore/tenant-a/parties",
                type = envelope.EventTypeName,
                datacontenttype = "application/json",
                data = envelope,
            },
            _jsonOptions);

        using StringContent content = new(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/events/parties", content).ConfigureAwait(true);
    }
}
