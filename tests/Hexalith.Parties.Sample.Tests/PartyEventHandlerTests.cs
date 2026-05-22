using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Sample;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hexalith.Parties.Sample.Tests;

[Collection("PartyEventHandler")]
public sealed class PartyEventHandlerTests : IDisposable
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
        PartyEventHandler.ClearProcessedEventIds();
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
    public async Task HandlePartyCreated_WithDifferentCloudEventIdAndSameSequence_ShouldSkipReplayAsync()
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

        CustomerSummaryStore.Customers["p-800"].DisplayName.ShouldBe("Changed Between Deliveries");
    }

    [Fact]
    public async Task HandlePersonDetailsUpdated_ShouldUpdateDisplayNameAsync()
    {
        CustomerSummaryStore.Customers["p-900"] = new CustomerSummary
        {
            Id = "p-900",
            DisplayName = "Old Name",
        };

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-900",
            "PersonDetailsUpdated",
            new { personDetails = new { firstName = "Marie", lastName = "Martin" } },
            correlationId: "corr-pdu",
            sequenceNumber: 2);

        HttpResponseMessage response = await PostEventAsync("corr-pdu:2", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-900"].DisplayName.ShouldBe("Marie Martin");
        CustomerSummaryStore.Customers["p-900"].LastUpdated.ShouldNotBeNull();
    }

    [Fact]
    public async Task HandleOlderSequenceAfterNewerSequence_ShouldNotOverwriteLocalStateAsync()
    {
        CustomerSummaryStore.Customers["p-out-of-order"] = new CustomerSummary
        {
            Id = "p-out-of-order",
            DisplayName = "Initial",
        };

        EventEnvelope newer = CreateEnvelope(
            "tenant-a:parties:p-out-of-order",
            "PartyDisplayNameDerived",
            new { displayName = "Newest Name", sortName = "Newest Name" },
            correlationId: "corr-order",
            sequenceNumber: 3);

        HttpResponseMessage newerResponse = await PostEventAsync("corr-order:3", newer);
        newerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-out-of-order"].DisplayName.ShouldBe("Newest Name");

        EventEnvelope older = CreateEnvelope(
            "tenant-a:parties:p-out-of-order",
            "PersonDetailsUpdated",
            new { personDetails = new { firstName = "Older", lastName = "Name" } },
            correlationId: "corr-order",
            sequenceNumber: 2);

        HttpResponseMessage olderResponse = await PostEventAsync("corr-order:2", older);
        olderResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-out-of-order"].DisplayName.ShouldBe("Newest Name");
    }

    [Fact]
    public async Task HandleOrganizationDetailsUpdated_ShouldUpdateDisplayNameAsync()
    {
        CustomerSummaryStore.Customers["p-901"] = new CustomerSummary
        {
            Id = "p-901",
            DisplayName = "Old Org",
        };

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-901",
            "OrganizationDetailsUpdated",
            new { organizationDetails = new { legalName = "New Corp Ltd" } },
            correlationId: "corr-odu",
            sequenceNumber: 2);

        HttpResponseMessage response = await PostEventAsync("corr-odu:2", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-901"].DisplayName.ShouldBe("New Corp Ltd");
        CustomerSummaryStore.Customers["p-901"].LastUpdated.ShouldNotBeNull();
    }

    [Fact]
    public async Task HandleContactChannelUpdated_Email_ShouldUpdateEmailAsync()
    {
        CustomerSummaryStore.Customers["p-902"] = new CustomerSummary
        {
            Id = "p-902",
            DisplayName = "Test Party",
            Email = "old@example.com",
        };

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-902",
            "ContactChannelUpdated",
            new { contactChannelId = "cc-1", type = "Email", value = "new@example.com" },
            correlationId: "corr-ccu",
            sequenceNumber: 3);

        HttpResponseMessage response = await PostEventAsync("corr-ccu:3", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-902"].Email.ShouldBe("new@example.com");
    }

    [Fact]
    public async Task HandleContactChannelUpdated_Phone_ShouldUpdatePhoneAsync()
    {
        CustomerSummaryStore.Customers["p-903"] = new CustomerSummary
        {
            Id = "p-903",
            DisplayName = "Test Party",
        };

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-903",
            "ContactChannelUpdated",
            new { contactChannelId = "cc-2", type = "Phone", value = "+33612345678" },
            correlationId: "corr-ccu-ph",
            sequenceNumber: 3);

        HttpResponseMessage response = await PostEventAsync("corr-ccu-ph:3", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-903"].Phone.ShouldBe("+33612345678");
    }

    [Fact]
    public async Task HandleContactChannelRemoved_ShouldReturnOkAsync()
    {
        CustomerSummaryStore.Customers["p-904"] = new CustomerSummary
        {
            Id = "p-904",
            DisplayName = "Test Party",
            Email = "removed@example.com",
        };
        CustomerSummaryStore.Customers["p-904"].ContactChannels["cc-1"] = new CustomerContactChannel("Email", "removed@example.com", true);

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-904",
            "ContactChannelRemoved",
            new { contactChannelId = "cc-1" },
            correlationId: "corr-ccr",
            sequenceNumber: 4);

        HttpResponseMessage response = await PostEventAsync("corr-ccr:4", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-904"].Email.ShouldBeNull();
        CustomerSummaryStore.Customers["p-904"].LastUpdated.ShouldNotBeNull();
    }

    [Fact]
    public async Task HandlePreferredContactChannelChanged_ShouldPromoteRequestedChannelAsync()
    {
        CustomerSummaryStore.Customers["p-905"] = new CustomerSummary
        {
            Id = "p-905",
            DisplayName = "Test Party",
            Email = "old@example.com",
        };
        CustomerSummaryStore.Customers["p-905"].ContactChannels["cc-1"] = new CustomerContactChannel("Email", "old@example.com", true);
        CustomerSummaryStore.Customers["p-905"].ContactChannels["cc-2"] = new CustomerContactChannel("Email", "new@example.com", false);

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-905",
            "PreferredContactChannelChanged",
            new { contactChannelId = "cc-2" },
            correlationId: "corr-pcc",
            sequenceNumber: 5);

        HttpResponseMessage response = await PostEventAsync("corr-pcc:5", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-905"].Email.ShouldBe("new@example.com");
        CustomerSummaryStore.Customers["p-905"].LastUpdated.ShouldNotBeNull();
    }

    [Fact]
    public async Task HandleIdentifierAdded_ShouldIncrementIdentifierCountAsync()
    {
        CustomerSummaryStore.Customers["p-906"] = new CustomerSummary
        {
            Id = "p-906",
            DisplayName = "Test Party",
        };

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-906",
            "IdentifierAdded",
            new { identifierId = "id-1", type = "VAT", value = "FR12345678901" },
            correlationId: "corr-ia",
            sequenceNumber: 3);

        HttpResponseMessage response = await PostEventAsync("corr-ia:3", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-906"].IdentifierCount.ShouldBe(1);
        CustomerSummaryStore.Customers["p-906"].LastUpdated.ShouldNotBeNull();
    }

    [Fact]
    public async Task HandleIdentifierRemoved_ShouldDecrementIdentifierCountAsync()
    {
        CustomerSummaryStore.Customers["p-907"] = new CustomerSummary
        {
            Id = "p-907",
            DisplayName = "Test Party",
        };
        CustomerSummaryStore.Customers["p-907"].Identifiers["id-1"] = new CustomerIdentifier("VAT", "FR12345678901");
        CustomerSummaryStore.Customers["p-907"].Identifiers["id-2"] = new CustomerIdentifier("SIRET", "12345678901234");
        CustomerSummaryStore.Customers["p-907"].IdentifierCount = CustomerSummaryStore.Customers["p-907"].Identifiers.Count;

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-907",
            "IdentifierRemoved",
            new { identifierId = "id-1" },
            correlationId: "corr-ir",
            sequenceNumber: 4);

        HttpResponseMessage response = await PostEventAsync("corr-ir:4", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-907"].IdentifierCount.ShouldBe(1);
    }

    [Fact]
    public async Task HandlePartyCreated_WithFullyQualifiedEventTypeName_ShouldProcessAsync()
    {
        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-fully-qualified",
            "Hexalith.Parties.Contracts.Events.PartyCreated",
            new { type = "Person", personDetails = new { firstName = "Qualified", lastName = "Name" } },
            correlationId: "corr-fq",
            sequenceNumber: 1);

        HttpResponseMessage response = await PostEventAsync("corr-fq:1", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-fully-qualified"].DisplayName.ShouldBe("Qualified Name");
    }

    [Fact]
    public async Task HandlePartyReactivated_ShouldMarkActiveAsync()
    {
        CustomerSummaryStore.Customers["p-908"] = new CustomerSummary
        {
            Id = "p-908",
            DisplayName = "Inactive Party",
            IsActive = false,
        };

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-908",
            "PartyReactivated",
            new { },
            correlationId: "corr-pr",
            sequenceNumber: 5);

        HttpResponseMessage response = await PostEventAsync("corr-pr:5", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-908"].IsActive.ShouldBeTrue();
        CustomerSummaryStore.Customers["p-908"].LastUpdated.ShouldNotBeNull();
    }

    [Fact]
    public async Task HandlePartyDisplayNameDerived_ShouldUpdateDisplayNameAsync()
    {
        CustomerSummaryStore.Customers["p-909"] = new CustomerSummary
        {
            Id = "p-909",
            DisplayName = "Old Name",
        };

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-909",
            "PartyDisplayNameDerived",
            new { displayName = "Derived Display Name", sortName = "Display Name, Derived" },
            correlationId: "corr-pdnd",
            sequenceNumber: 3);

        HttpResponseMessage response = await PostEventAsync("corr-pdnd:3", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-909"].DisplayName.ShouldBe("Derived Display Name");
    }

    [Fact]
    public async Task HandleIsNaturalPersonChanged_ShouldReturnOkAsync()
    {
        CustomerSummaryStore.Customers["p-910"] = new CustomerSummary
        {
            Id = "p-910",
            DisplayName = "Test Party",
        };

        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-910",
            "IsNaturalPersonChanged",
            new { isNaturalPerson = true },
            correlationId: "corr-inp",
            sequenceNumber: 2);

        HttpResponseMessage response = await PostEventAsync("corr-inp:2", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CustomerSummaryStore.Customers["p-910"].LastUpdated.ShouldNotBeNull();
    }

    [Fact]
    public async Task HandlePartyMerged_ShouldReturnOkWithoutErrorAsync()
    {
        EventEnvelope envelope = CreateEnvelope(
            "tenant-a:parties:p-911",
            "PartyMerged",
            new { survivorPartyId = "p-survivor", mergedPartyId = "p-911" },
            correlationId: "corr-pm",
            sequenceNumber: 10);

        HttpResponseMessage response = await PostEventAsync("corr-pm:10", envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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
