using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Sample;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hexalith.Parties.Sample.Tests;

public sealed class PartyEventHandlerTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client;

    public PartyEventHandlerTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        CustomerSummaryStore.Customers.Clear();
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        CustomerSummaryStore.Customers.Clear();
        _client.Dispose();
    }

    [Fact]
    public async Task HandlePartyCreated_ShouldCreateCustomerSummaryAsync()
    {
        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-100",
            "PartyCreated",
            new { type = "Person", personDetails = new { firstName = "Jean", lastName = "Dupont" } },
            correlationId: "corr-1",
            sequenceNumber: 1);

        HttpResponseMessage response = await PostEventAsync("corr-1:1", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers.ContainsKey("p-100").ShouldBeTrue();
        CustomerSummaryStore.Customers["p-100"].DisplayName.ShouldBe("Jean Dupont");
        CustomerSummaryStore.Customers["p-100"].IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task HandlePartyCreated_Organization_ShouldUseLegalNameAsync()
    {
        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-200",
            "PartyCreated",
            new { type = "Organization", organizationDetails = new { legalName = "Acme Corp" } },
            correlationId: "corr-2",
            sequenceNumber: 1);

        HttpResponseMessage response = await PostEventAsync("corr-2:1", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-200"].DisplayName.ShouldBe("Acme Corp");
    }

    [Fact]
    public async Task HandleContactChannelAdded_ShouldUpdateEmailAsync()
    {
        CustomerSummaryStore.Customers["p-300"] = new CustomerSummary
        {
            Id = "p-300",
            DisplayName = "Test Party",
        };

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-300",
            "ContactChannelAdded",
            new { contactChannelId = "cc-1", type = "Email", value = "test@example.com", isPreferred = true },
            correlationId: "corr-3",
            sequenceNumber: 2);

        HttpResponseMessage response = await PostEventAsync("corr-3:2", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-300"].Email.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task HandlePartyDeactivated_ShouldMarkInactiveAsync()
    {
        CustomerSummaryStore.Customers["p-400"] = new CustomerSummary
        {
            Id = "p-400",
            DisplayName = "Active Party",
        };

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-400",
            "PartyDeactivated",
            new { },
            correlationId: "corr-4",
            sequenceNumber: 3);

        HttpResponseMessage response = await PostEventAsync("corr-4:3", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-400"].IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task HandlePartyDeactivated_WhenAlreadyInactive_ShouldBeIdempotentAsync()
    {
        CustomerSummaryStore.Customers["p-500"] = new CustomerSummary
        {
            Id = "p-500",
            DisplayName = "Inactive Party",
            IsActive = false,
        };

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-500",
            "PartyDeactivated",
            new { },
            correlationId: "corr-5",
            sequenceNumber: 4);

        HttpResponseMessage response = await PostEventAsync("corr-5:4", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-500"].IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task HandlePartyDeactivated_WhenPartyNotFound_ShouldReturnOkAsync()
    {
        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:unknown-id",
            "PartyDeactivated",
            new { },
            correlationId: "corr-6",
            sequenceNumber: 5);

        HttpResponseMessage response = await PostEventAsync("corr-6:5", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HandleUnknownEventType_ShouldReturnOkAsync()
    {
        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-600",
            "SomeUnknownEvent",
            new { data = "test" },
            correlationId: "corr-7",
            sequenceNumber: 6);

        HttpResponseMessage response = await PostEventAsync("corr-7:6", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HandleDuplicateEvent_ShouldBeIdempotentAsync()
    {
        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-700",
            "PartyCreated",
            new { type = "Person", personDetails = new { firstName = "Dup", lastName = "Test" } },
            correlationId: "corr-dup",
            sequenceNumber: 1);

        // Send first time
        HttpResponseMessage response1 = await PostEventAsync("dup-event", envelope);
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-700"].DisplayName.ShouldBe("Dup Test");

        // Modify the store to detect if second processing runs
        CustomerSummaryStore.Customers["p-700"].DisplayName = "Modified";

        // Send same event again (same correlationId:sequenceNumber)
        HttpResponseMessage response2 = await PostEventAsync("dup-event", envelope);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Name should still be "Modified" because the duplicate was skipped
        CustomerSummaryStore.Customers["p-700"].DisplayName.ShouldBe("Modified");
    }

    [Fact]
    public async Task HandlePartyCreated_WithDifferentCloudEventId_ShouldProcessAgainAsync()
    {
        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-800",
            "PartyCreated",
            new { type = "Person", personDetails = new { firstName = "Jean", lastName = "Replay" } },
            correlationId: "corr-replay",
            sequenceNumber: 1);

        HttpResponseMessage firstResponse = await PostEventAsync("event-1", envelope);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        CustomerSummaryStore.Customers["p-800"].DisplayName = "Changed Between Deliveries";

        HttpResponseMessage secondResponse = await PostEventAsync("event-2", envelope);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        CustomerSummaryStore.Customers["p-800"].DisplayName.ShouldBe("Jean Replay");
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
