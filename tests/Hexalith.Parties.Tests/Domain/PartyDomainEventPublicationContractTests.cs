using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
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

    private static PartyCreated RoundTrip(PartyCreated @event)
    {
        string json = JsonSerializer.Serialize(@event, JsonOptions);
        return JsonSerializer.Deserialize<PartyCreated>(json, JsonOptions)!;
    }
}
