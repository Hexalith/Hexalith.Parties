using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Platform.IntegrationTests;

/// <summary>Exercises real Parties, Tenants and Memories; a missing handoff fails fixture initialization.</summary>
public sealed class PartyEnvironmentTests(PlatformEnvironmentFixture fixture) : IClassFixture<PlatformEnvironmentFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task RealDependencies_CreateQueryAndPersistMatchingPartyEvents()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(120));
        CancellationToken token = bound.Token;
        string tenant = fixture.Environment.SyntheticTenantId;
        // The declared Tenants API owns system-scope routing; fixtures never infer its internal query bindings.
        using HttpResponseMessage tenantResponse = await fixture.Tenants.GetAsync($"/api/v1/tenants/{Uri.EscapeDataString(tenant)}", token).ConfigureAwait(true);
        tenantResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument tenantDocument = JsonDocument.Parse(await tenantResponse.Content.ReadAsStringAsync(token).ConfigureAwait(true));
        tenantDocument.RootElement.GetProperty("tenantId").GetString().ShouldBe(tenant);
        using HttpResponseMessage memoriesResponse = await fixture.Memories.GetAsync($"/api/v1/tenants/{Uri.EscapeDataString(tenant)}", token).ConfigureAwait(true);
        memoriesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument memoriesDocument = JsonDocument.Parse(await memoriesResponse.Content.ReadAsStringAsync(token).ConfigureAwait(true));
        memoriesDocument.RootElement.GetProperty("id").GetString().ShouldBe(tenant);

        string partyId = UniqueIdHelper.GenerateSortableUniqueStringId();
        string commandId = UniqueIdHelper.GenerateSortableUniqueStringId();
        var identity = new AggregateIdentity(tenant, "party", partyId);
        var command = new CreatePartyComposite
        {
            PartyId = partyId, Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Synthetic first", LastName = "Synthetic last" },
        };
        using HttpResponseMessage submit = await fixture.Gateway.PostAsJsonAsync("/api/v1/commands", new
        {
            messageId = commandId, correlationId = commandId, tenant, domain = identity.Domain,
            aggregateId = partyId, commandType = typeof(CreatePartyComposite).FullName, payload = command,
        }, token).ConfigureAwait(true);
        submit.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using JsonDocument status = await PollStatusAsync(commandId, token).ConfigureAwait(true);
        status.RootElement.GetProperty("status").GetString().ShouldBe("Completed");
        status.RootElement.GetProperty("aggregateId").GetString().ShouldBe(partyId);
        PartyDetail detail = await PollProjectionAsync(partyId, token).ConfigureAwait(true);
        detail.Id.ShouldBe(partyId);
        detail.Type.ShouldBe(PartyType.Person);
        detail.PersonDetails.ShouldBe(command.PersonDetails);
        detail.IsActive.ShouldBeTrue();
        detail.Freshness.ShouldNotBeNull().Status.ShouldBe(ProjectionFreshnessStatus.Current);
        (AggregateMetadata metadata, EventEnvelope[] events) = await fixture.ReadStoredStreamAsync(identity, token).ConfigureAwait(true);
        metadata.CurrentSequence.ShouldBe(events.LongLength);
        events.ShouldNotBeEmpty();
        for (int index = 0; index < events.Length; index++)
        {
            EventEnvelope envelope = events[index];
            envelope.Identity.ShouldBe(identity);
            envelope.SequenceNumber.ShouldBe(index + 1);
            envelope.CorrelationId.ShouldBe(commandId);
            envelope.CausationId.ShouldBe(commandId);
            envelope.UserId.ShouldBe("g4-native-tests");
            envelope.SerializationFormat.ShouldBe("json");
        }
        EventEnvelope createdEnvelope = events.Single(envelope => envelope.EventTypeName == typeof(PartyCreated).FullName);
        PartyCreated created = JsonSerializer.Deserialize<PartyCreated>(createdEnvelope.Payload, SerializerOptions)!;
        created.Type.ShouldBe(command.Type);
        created.PersonDetails.ShouldBe(command.PersonDetails);
        created.CreatedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
        created.CreatedAt.ShouldBeLessThanOrEqualTo(createdEnvelope.Timestamp);
        detail.CreatedAt.ShouldBe(createdEnvelope.Timestamp);
        Console.WriteLine($"Persisted synthetic stream: {events.Length} exact event envelopes; Tenants and Memories returned the run tenant.");
    }

    private async Task<JsonDocument> PollStatusAsync(string commandId, CancellationToken token)
    {
        while (true)
        {
            using HttpResponseMessage response = await fixture.Gateway.GetAsync($"/api/v1/commands/status/{commandId}", token).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            JsonDocument status = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(true));
            string? outcome = status.RootElement.GetProperty("status").GetString();
            if (outcome is "Completed" or "Rejected" or "PublishFailed" or "TimedOut") { return status; }
            status.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(250), token).ConfigureAwait(true);
        }
    }

    private async Task<PartyDetail> PollProjectionAsync(string partyId, CancellationToken token)
    {
        while (true)
        {
            using HttpResponseMessage response = await fixture.Gateway.PostAsJsonAsync("/api/v1/queries", new
            {
                tenant = fixture.Environment.SyntheticTenantId, domain = "party", aggregateId = partyId,
                queryType = "PartyDetail", projectionType = "party-detail", projectionActorType = "PartyDetailProjectionQueryActor", entityId = partyId,
            }, token).ConfigureAwait(true);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(true));
                PartyDetail? detail = document.RootElement.GetProperty("payload").Deserialize<PartyDetail>(SerializerOptions);
                if (detail is { Freshness.Status: ProjectionFreshnessStatus.Current } && detail.Id == partyId) { return detail; }
            }
            else if (response.StatusCode != HttpStatusCode.NotFound && response.StatusCode != HttpStatusCode.ServiceUnavailable)
            {
                throw new InvalidOperationException($"The authenticated Party query failed with HTTP {(int)response.StatusCode}.");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), token).ConfigureAwait(true);
        }
    }
}
