using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Results;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Server.Aggregates;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Tests.Domain;

public sealed class PartyDomainEventPublicationContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void CreateCompositeProducesStableConsumerEventsAndUpdatedPayload()
    {
        CompositeCommandResult result = PartyAggregate.Handle(PartyTestData.ValidCreatePersonComposite(), state: null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Select(e => e.GetType()).ShouldBe(
            [
                typeof(PartyCreated),
                typeof(PartyDisplayNameDerived),
                typeof(ContactChannelAdded),
                typeof(ContactChannelAdded),
                typeof(IdentifierAdded),
            ]);
        result.UpdatedPartyDetail.ShouldNotBeNull();
        result.ResultPayload.ShouldNotBeNullOrWhiteSpace();

        PartyCreated created = result.Events.OfType<PartyCreated>().ShouldHaveSingleItem();
        created.PersonDetails!.FirstName.ShouldBe("John");
        RoundTrip(created).PersonDetails!.LastName.ShouldBe("Doe");
    }

    [Fact]
    public void UpdateCompositeProducesStableDetailContactAndIdentifierEvents()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();

        CompositeCommandResult result = PartyAggregate.Handle(PartyTestData.ValidUpdatePersonComposite(), state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.ShouldContain(e => e is PersonDetailsUpdated);
        result.Events.ShouldContain(e => e is PartyDisplayNameDerived);
        result.Events.ShouldContain(e => e is ContactChannelAdded);
        result.Events.ShouldContain(e => e is ContactChannelUpdated);
        result.Events.ShouldContain(e => e is ContactChannelRemoved);
        result.Events.ShouldContain(e => e is IdentifierAdded);
        result.Events.OfType<IdentifierAdded>().ShouldHaveSingleItem().Value.ShouldBe("synthetic-siret-value");
        result.UpdatedPartyDetail.ShouldNotBeNull();
        result.ResultPayload.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void LifecycleCommandsProduceStableDeactivateAndReactivateEvents()
    {
        DomainResult deactivate = PartyAggregate.Handle(PartyTestData.ValidDeactivateParty(), PartyTestData.CreatePersonState());
        DomainResult reactivate = PartyAggregate.Handle(PartyTestData.ValidReactivateParty(), PartyTestData.CreateDeactivatedPersonState());

        deactivate.IsSuccess.ShouldBeTrue();
        reactivate.IsSuccess.ShouldBeTrue();
        deactivate.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyDeactivated>();
        reactivate.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyReactivated>();
        deactivate.ShouldBeOfType<PartyCommandResult>().ResultPayload.ShouldNotBeNullOrWhiteSpace();
        reactivate.ShouldBeOfType<PartyCommandResult>().ResultPayload.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RejectionEventsRemainDistinguishableFromSuccessfulStateChanges()
    {
        DomainResult rejection = PartyAggregate.Handle(
            new RemoveIdentifier { PartyId = PartyTestData.DefaultPartyId, IdentifierId = "missing-identifier" },
            PartyTestData.CreatePersonStateWithIdentifier());

        rejection.IsRejection.ShouldBeTrue();
        rejection.Events.ShouldHaveSingleItem().ShouldBeAssignableTo<IRejectionEvent>();
        rejection.Events.OfType<IdentifierRemoved>().ShouldBeEmpty();
    }

    [Fact]
    public void PublishedPartyEventContractsRemainInContractsAssemblyAndSerializable()
    {
        Type[] supportedEvents =
        [
            typeof(PartyCreated),
            typeof(PersonDetailsUpdated),
            typeof(OrganizationDetailsUpdated),
            typeof(ContactChannelAdded),
            typeof(ContactChannelUpdated),
            typeof(ContactChannelRemoved),
            typeof(PreferredContactChannelChanged),
            typeof(IdentifierAdded),
            typeof(IdentifierRemoved),
            typeof(PartyDeactivated),
            typeof(PartyReactivated),
            typeof(PartyCommandValidationRejected),
        ];

        foreach (Type eventType in supportedEvents)
        {
            eventType.Assembly.ShouldBe(typeof(PartyCreated).Assembly);
            typeof(IEventPayload).IsAssignableFrom(eventType).ShouldBeTrue($"{eventType.Name} must be an EventStore payload contract.");
        }

        ContactChannelAdded contact = new()
        {
            ContactChannelId = "contact-1",
            Type = ContactChannelType.Email,
            Value = "ada@example.test",
            IsPreferred = true,
        };
        string json = JsonSerializer.Serialize(contact, JsonOptions);
        json.ShouldContain("ada@example.test");
        JsonSerializer.Deserialize<ContactChannelAdded>(json, JsonOptions)!.Value.ShouldBe("ada@example.test");
    }

    [Fact]
    public async Task PersistedEventEnvelopesUseAuthenticatedTenantAndStableMetadataAsync()
    {
        string partyId = Guid.NewGuid().ToString("D");
        CompositeCommandResult domainResult = PartyAggregate.Handle(new CreatePartyComposite
        {
            PartyId = partyId,
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
        }, state: null);
        var persister = new FakeEventPersister();
        var identity = new AggregateIdentity("tenant-authenticated", "party", partyId);
        CommandEnvelope command = CreateEnvelope(
            tenantId: "tenant-authenticated",
            aggregateId: partyId,
            payload: JsonSerializer.SerializeToUtf8Bytes(new
            {
                partyId,
                tenantId = "tenant-payload-should-not-route",
                firstName = "Ada",
            }));

        var persisted = await persister.PersistEventsAsync(
            identity,
            aggregateType: "Party",
            command,
            domainResult,
            domainServiceVersion: "v1",
            CancellationToken.None);

        persisted.PersistedEnvelopes.ShouldNotBeEmpty();
        foreach (var envelope in persisted.PersistedEnvelopes)
        {
            envelope.TenantId.ShouldBe("tenant-authenticated");
            envelope.AggregateId.ShouldBe(partyId);
            envelope.AggregateType.ShouldBe("Party");
            envelope.Domain.ShouldBe("party");
            envelope.CorrelationId.ShouldBe("corr-5-2");
            envelope.CausationId.ShouldBe("cause-5-2");
            envelope.UserId.ShouldBe("user-a");
            envelope.DomainServiceVersion.ShouldBe("v1");
            envelope.EventTypeName.ShouldStartWith("Hexalith.Parties.Contracts.Events.");
            envelope.Timestamp.ShouldNotBe(default);
            envelope.MetadataVersion.ShouldBe(1);
            envelope.SerializationFormat.ShouldBe("json");
            envelope.Payload.Length.ShouldBeGreaterThan(0);
            envelope.ToString().ShouldNotContain("Lovelace", Case.Insensitive);
            envelope.ToString().ShouldContain("Payload = [REDACTED]");
        }

        persisted.PersistedEnvelopes.Select(e => e.SequenceNumber).ShouldBe([1, 2]);
    }

    [Fact]
    public void MissingTenantContextFailsBeforeEnvelopePublication()
    {
        Should.Throw<ArgumentException>(() => new AggregateIdentity(string.Empty, "party", PartyTestData.DefaultPartyId));
        Should.Throw<ArgumentException>(() => CreateEnvelope(
            tenantId: string.Empty,
            aggregateId: PartyTestData.DefaultPartyId,
            payload: JsonSerializer.SerializeToUtf8Bytes(new { partyId = PartyTestData.DefaultPartyId })));
    }

    private static PartyCreated RoundTrip(PartyCreated @event)
    {
        string json = JsonSerializer.Serialize(@event, JsonOptions);
        return JsonSerializer.Deserialize<PartyCreated>(json, JsonOptions)!;
    }

    private static CommandEnvelope CreateEnvelope(string tenantId, string aggregateId, byte[] payload)
        => new(
            MessageId: "msg-5-2",
            TenantId: tenantId,
            Domain: "party",
            AggregateId: aggregateId,
            CommandType: typeof(CreatePartyComposite).FullName!,
            Payload: payload,
            CorrelationId: "corr-5-2",
            CausationId: "cause-5-2",
            UserId: "user-a",
            Extensions: null);
}
