using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Sample;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hexalith.Parties.Sample.Tests;

[Collection("PartyEventHandler")]
public sealed class SelectiveEventHandlingTests : IDisposable
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client;

    public SelectiveEventHandlingTests(WebApplicationFactory<Program> factory)
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
    public async Task HandlePersonDetailsUpdated_UpdatesCustomerSummaryDisplayNameAsync()
    {
        // Setup: create a party first
        await PostEventAsync("sel-1", CreateEnvelope("tenant-a:parties:p-sel-1", "PartyCreated",
            new { type = "Person", personDetails = new { firstName = "Original", lastName = "Name" } },
            "corr-sel1", 1));

        CustomerSummaryStore.Customers["p-sel-1"].DisplayName.ShouldBe("Original Name");

        // Selectively handle PersonDetailsUpdated
        await PostEventAsync("sel-2", CreateEnvelope("tenant-a:parties:p-sel-1", "PersonDetailsUpdated",
            new { personDetails = new { firstName = "Updated", lastName = "Person" } },
            "corr-sel1", 2));

        CustomerSummaryStore.Customers["p-sel-1"].DisplayName.ShouldBe("Updated Person");
    }

    [Fact]
    public async Task HandleIdentifierAdded_IncrementsCustomerSummaryIdentifierCountAsync()
    {
        // Setup: create a party
        await PostEventAsync("sel-id-1", CreateEnvelope("tenant-a:parties:p-sel-id", "PartyCreated",
            new { type = "Person", personDetails = new { firstName = "Id", lastName = "Test" } },
            "corr-selid", 1));

        CustomerSummaryStore.Customers["p-sel-id"].IdentifierCount.ShouldBe(0);

        // Add first identifier
        await PostEventAsync("sel-id-2", CreateEnvelope("tenant-a:parties:p-sel-id", "IdentifierAdded",
            new { identifierId = "id-1", type = "VAT", value = "FR123" },
            "corr-selid", 2));

        CustomerSummaryStore.Customers["p-sel-id"].IdentifierCount.ShouldBe(1);

        // Add second identifier
        await PostEventAsync("sel-id-3", CreateEnvelope("tenant-a:parties:p-sel-id", "IdentifierAdded",
            new { identifierId = "id-2", type = "SIRET", value = "456" },
            "corr-selid", 3));

        CustomerSummaryStore.Customers["p-sel-id"].IdentifierCount.ShouldBe(2);
    }

    [Fact]
    public async Task SubscriberIgnoresUnhandledEvents_ReturnsOkNoStateMutationAsync()
    {
        // Setup: create a party
        await PostEventAsync("sel-ign-1", CreateEnvelope("tenant-a:parties:p-sel-ign", "PartyCreated",
            new { type = "Person", personDetails = new { firstName = "Stable", lastName = "Party" } },
            "corr-selig", 1));

        string originalName = CustomerSummaryStore.Customers["p-sel-ign"].DisplayName;
        bool originalActive = CustomerSummaryStore.Customers["p-sel-ign"].IsActive;

        // Send an event that does not mutate the CustomerSummary key fields
        // IsNaturalPersonChanged only sets LastUpdated, does not change DisplayName/Email/IsActive
        HttpResponseMessage response = await PostEventAsync("sel-ign-2", CreateEnvelope("tenant-a:parties:p-sel-ign", "IsNaturalPersonChanged",
            new { isNaturalPerson = false },
            "corr-selig", 2));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-sel-ign"].DisplayName.ShouldBe(originalName);
        CustomerSummaryStore.Customers["p-sel-ign"].IsActive.ShouldBe(originalActive);

        // Completely unknown event should also be fine
        HttpResponseMessage response2 = await PostEventAsync("sel-ign-3", CreateEnvelope("tenant-a:parties:p-sel-ign", "SomeUnknownFutureEvent",
            new { irrelevant = true },
            "corr-selig", 3));

        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-sel-ign"].DisplayName.ShouldBe(originalName);
    }

    [Fact]
    public async Task SubscriberBuildsDomainProjection_FromPartyLifecycleSequenceAsync()
    {
        string partyId = "p-lifecycle";
        string aggregateId = $"tenant-a:parties:{partyId}";

        // 1. Create person party
        await PostEventAsync("lc-1", CreateEnvelope(aggregateId, "PartyCreated",
            new { type = "Person", personDetails = new { firstName = "Jean", lastName = "Dupont" } },
            "corr-lc", 1));

        CustomerSummaryStore.Customers[partyId].DisplayName.ShouldBe("Jean Dupont");
        CustomerSummaryStore.Customers[partyId].IsActive.ShouldBeTrue();

        // 2. Add email contact
        await PostEventAsync("lc-2", CreateEnvelope(aggregateId, "ContactChannelAdded",
            new { contactChannelId = "cc-1", type = "Email", value = "jean@example.com", isPreferred = true },
            "corr-lc", 2));

        CustomerSummaryStore.Customers[partyId].Email.ShouldBe("jean@example.com");

        // 3. Add phone contact
        await PostEventAsync("lc-3", CreateEnvelope(aggregateId, "ContactChannelAdded",
            new { contactChannelId = "cc-2", type = "Phone", value = "+33600000000", isPreferred = false },
            "corr-lc", 3));

        CustomerSummaryStore.Customers[partyId].Phone.ShouldBe("+33600000000");

        // 4. Add identifier
        await PostEventAsync("lc-4", CreateEnvelope(aggregateId, "IdentifierAdded",
            new { identifierId = "id-1", type = "VAT", value = "FR12345678901" },
            "corr-lc", 4));

        CustomerSummaryStore.Customers[partyId].IdentifierCount.ShouldBe(1);

        // 5. Update name
        await PostEventAsync("lc-5", CreateEnvelope(aggregateId, "PersonDetailsUpdated",
            new { personDetails = new { firstName = "Jean-Pierre", lastName = "Dupont-Martin" } },
            "corr-lc", 5));

        CustomerSummaryStore.Customers[partyId].DisplayName.ShouldBe("Jean-Pierre Dupont-Martin");

        // 6. Deactivate
        await PostEventAsync("lc-6", CreateEnvelope(aggregateId, "PartyDeactivated",
            new { },
            "corr-lc", 6));

        CustomerSummaryStore.Customers[partyId].IsActive.ShouldBeFalse();

        // 7. Reactivate
        await PostEventAsync("lc-7", CreateEnvelope(aggregateId, "PartyReactivated",
            new { },
            "corr-lc", 7));

        CustomerSummaryStore.Customers[partyId].IsActive.ShouldBeTrue();

        // Final projection state
        CustomerSummary final = CustomerSummaryStore.Customers[partyId];
        final.DisplayName.ShouldBe("Jean-Pierre Dupont-Martin");
        final.Email.ShouldBe("jean@example.com");
        final.Phone.ShouldBe("+33600000000");
        final.IdentifierCount.ShouldBe(1);
        final.IsActive.ShouldBeTrue();
        final.LastUpdated.ShouldNotBeNull();
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
