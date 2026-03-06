using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Sample;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hexalith.Parties.Sample.Tests;

[Collection("PartyEventHandler")]
public sealed class SubscriberDeliveryVerificationTests : IDisposable
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client;

    public SubscriberDeliveryVerificationTests(WebApplicationFactory<Program> factory)
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
    public async Task Publish10SequentialEvents_SameAggregate_SubscriberReceivesAll10Async()
    {
        string partyId = "p-delivery-10";
        string aggregateId = $"tenant-a:parties:{partyId}";

        // Event 1: PartyCreated
        await PostEventAsync("d10-1", CreateEnvelope(aggregateId, "PartyCreated",
            new { type = "Person", personDetails = new { firstName = "Jean", lastName = "Dupont" } },
            "corr-d10", 1));

        // Event 2: ContactChannelAdded (email)
        await PostEventAsync("d10-2", CreateEnvelope(aggregateId, "ContactChannelAdded",
            new { contactChannelId = "cc-1", type = "Email", value = "jean@example.com", isPreferred = true },
            "corr-d10", 2));

        // Event 3: ContactChannelAdded (phone)
        await PostEventAsync("d10-3", CreateEnvelope(aggregateId, "ContactChannelAdded",
            new { contactChannelId = "cc-2", type = "Phone", value = "+33612345678", isPreferred = false },
            "corr-d10", 3));

        // Event 4: IdentifierAdded
        await PostEventAsync("d10-4", CreateEnvelope(aggregateId, "IdentifierAdded",
            new { identifierId = "id-1", type = "VAT", value = "FR12345678901" },
            "corr-d10", 4));

        // Event 5: IdentifierAdded
        await PostEventAsync("d10-5", CreateEnvelope(aggregateId, "IdentifierAdded",
            new { identifierId = "id-2", type = "SIRET", value = "12345678901234" },
            "corr-d10", 5));

        // Event 6: PersonDetailsUpdated
        await PostEventAsync("d10-6", CreateEnvelope(aggregateId, "PersonDetailsUpdated",
            new { personDetails = new { firstName = "Jean-Pierre", lastName = "Dupont" } },
            "corr-d10", 6));

        // Event 7: ContactChannelUpdated
        await PostEventAsync("d10-7", CreateEnvelope(aggregateId, "ContactChannelUpdated",
            new { contactChannelId = "cc-1", type = "Email", value = "jp.dupont@example.com" },
            "corr-d10", 7));

        // Event 8: PreferredContactChannelChanged
        await PostEventAsync("d10-8", CreateEnvelope(aggregateId, "PreferredContactChannelChanged",
            new { contactChannelId = "cc-2" },
            "corr-d10", 8));

        // Event 9: PartyDisplayNameDerived
        await PostEventAsync("d10-9", CreateEnvelope(aggregateId, "PartyDisplayNameDerived",
            new { displayName = "Dupont, Jean-Pierre", sortName = "Dupont Jean-Pierre" },
            "corr-d10", 9));

        // Event 10: PartyDeactivated
        HttpResponseMessage finalResponse = await PostEventAsync("d10-10", CreateEnvelope(aggregateId, "PartyDeactivated",
            new { },
            "corr-d10", 10));

        finalResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify all 10 events were processed
        CustomerSummaryStore.Customers.ContainsKey(partyId).ShouldBeTrue();
        CustomerSummary summary = CustomerSummaryStore.Customers[partyId];
        summary.DisplayName.ShouldBe("Dupont, Jean-Pierre"); // Last name update from event 9
        summary.Email.ShouldBe("jp.dupont@example.com");      // Updated in event 7
        summary.Phone.ShouldBe("+33612345678");                // Added in event 3
        summary.IdentifierCount.ShouldBe(2);                   // 2 identifiers added
        summary.IsActive.ShouldBeFalse();                       // Deactivated in event 10
        summary.LastUpdated.ShouldNotBeNull();
        summary.ContactChannels.Count.ShouldBe(2);
        summary.Identifiers.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DuplicateEventDelivery_SameCloudEventsId_HandledIdempotentlyAsync()
    {
        string partyId = "p-dup-verify";
        string aggregateId = $"tenant-a:parties:{partyId}";

        EventEnvelope envelope = CreateEnvelope(aggregateId, "PartyCreated",
            new { type = "Person", personDetails = new { firstName = "Dup", lastName = "Test" } },
            "corr-dup-v", 1);

        // First delivery
        HttpResponseMessage response1 = await PostEventAsync("dup-verify-event", envelope);
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers[partyId].DisplayName.ShouldBe("Dup Test");

        // Modify store to detect if second processing runs
        CustomerSummaryStore.Customers[partyId].DisplayName = "Modified After First";

        // Second delivery with same CloudEvents id
        HttpResponseMessage response2 = await PostEventAsync("dup-verify-event", envelope);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);

        // State should be unchanged (duplicate was skipped)
        CustomerSummaryStore.Customers[partyId].DisplayName.ShouldBe("Modified After First");
    }

    [Fact]
    public async Task MultipleAggregates_NoContaminationBetweenPartiesAsync()
    {
        // Create two parties
        await PostEventAsync("multi-1", CreateEnvelope("tenant-a:parties:party-A", "PartyCreated",
            new { type = "Person", personDetails = new { firstName = "Alice", lastName = "A" } },
            "corr-ma", 1));

        await PostEventAsync("multi-2", CreateEnvelope("tenant-a:parties:party-B", "PartyCreated",
            new { type = "Organization", organizationDetails = new { legalName = "B Corp" } },
            "corr-mb", 1));

        // Deactivate party A only
        await PostEventAsync("multi-3", CreateEnvelope("tenant-a:parties:party-A", "PartyDeactivated",
            new { },
            "corr-ma", 2));

        // Add email to party B only
        await PostEventAsync("multi-4", CreateEnvelope("tenant-a:parties:party-B", "ContactChannelAdded",
            new { contactChannelId = "cc-b1", type = "Email", value = "b@example.com", isPreferred = true },
            "corr-mb", 2));

        // Verify no cross-contamination
        CustomerSummaryStore.Customers["party-A"].IsActive.ShouldBeFalse();
        CustomerSummaryStore.Customers["party-A"].Email.ShouldBeNull();

        CustomerSummaryStore.Customers["party-B"].IsActive.ShouldBeTrue();
        CustomerSummaryStore.Customers["party-B"].Email.ShouldBe("b@example.com");
        CustomerSummaryStore.Customers["party-B"].DisplayName.ShouldBe("B Corp");
    }

    [Fact]
    public async Task BurstOfEvents_ConcurrentDelivery_HandledSafelyAsync()
    {
        // Pre-create parties
        for (int i = 0; i < 10; i++)
        {
            await PostEventAsync($"burst-create-{i}", CreateEnvelope($"tenant-a:parties:burst-{i}", "PartyCreated",
                new { type = "Person", personDetails = new { firstName = $"Person{i}", lastName = "Burst" } },
                $"corr-burst-{i}", 1));
        }

        // Send a burst of concurrent events
        Task<HttpResponseMessage>[] tasks = new Task<HttpResponseMessage>[10];
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            tasks[i] = PostEventAsync($"burst-deactivate-{index}", CreateEnvelope($"tenant-a:parties:burst-{index}", "PartyDeactivated",
                new { },
                $"corr-burst-{index}", 2));
        }

        HttpResponseMessage[] responses = await Task.WhenAll(tasks);

        // All responses should be OK
        foreach (HttpResponseMessage response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // All parties should be deactivated
        for (int i = 0; i < 10; i++)
        {
            CustomerSummaryStore.Customers[$"burst-{i}"].IsActive.ShouldBeFalse();
        }
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
