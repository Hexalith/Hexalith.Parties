using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Results;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Domain;
using Hexalith.Parties.Tests.FitnessTests;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Tests.Domain;

public sealed class PartyDomainEventPublicationContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = PartiesJsonOptions.Default;

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
    public void ForwardCompatibleLifecycleEventContractsRemainAdditiveAndDocumented()
    {
        (Type EventType, string[] RequiredProperties)[] futureContracts =
        [
            (typeof(PartyMerged), ["SurvivorPartyId", "MergedPartyId"]),
            (typeof(PartyErased), ["PartyId", "TenantId", "ErasedAt", "ErasureStatus", "VerificationStatus"]),
            (typeof(ErasePartyRequested), ["PartyId", "TenantId", "RequestedAt", "RequestedBy"]),
            (typeof(ConsentRecorded), ["PartyId", "TenantId", "ConsentId", "ChannelId", "Purpose", "LawfulBasis", "GrantedAt", "GrantedBy", "Source"]),
            (typeof(ConsentRevoked), ["PartyId", "TenantId", "ConsentId", "RevokedAt", "RevokedBy", "Reason", "Source"]),
            (typeof(ProcessingRestricted), ["PartyId", "TenantId", "RestrictedAt", "RestrictedBy", "CorrelationId"]),
            (typeof(RestrictionLifted), ["PartyId", "TenantId", "LiftedAt", "LiftedBy", "CorrelationId"]),
        ];

        foreach ((Type eventType, string[] requiredProperties) in futureContracts)
        {
            eventType.Assembly.ShouldBe(typeof(PartyCreated).Assembly);
            typeof(IEventPayload).IsAssignableFrom(eventType).ShouldBeTrue($"{eventType.Name} must remain a public EventStore event contract.");
            foreach (string propertyName in requiredProperties)
            {
                eventType.GetProperty(propertyName).ShouldNotBeNull($"{eventType.Name}.{propertyName} must not be removed or renamed.");
            }
        }

        string json = """
            {
              "survivorPartyId": "party-survivor",
              "mergedPartyId": "party-merged",
              "futureAdditiveField": "ignored"
            }
            """;
        JsonSerializer.Deserialize<PartyMerged>(json, PartiesJsonOptions.Default)!.SurvivorPartyId.ShouldBe("party-survivor");

        string docsRoot = RepositoryRoot.Locate();
        string handlerPatterns = File.ReadAllText(Path.Combine(docsRoot, "docs", "event-handler-patterns.md"));
        string subscribing = File.ReadAllText(Path.Combine(docsRoot, "docs", "event-subscribing.md"));
        (handlerPatterns + subscribing).ShouldContain("PartyMerged");
        (handlerPatterns + subscribing).ShouldContain("v2");
        (handlerPatterns + subscribing).ShouldContain("PartyErased");
        (handlerPatterns + subscribing).ShouldContain("v1.1");
        (handlerPatterns + subscribing).ShouldContain("Consent");
        (handlerPatterns + subscribing).ShouldContain("Restriction");
    }

    [Fact]
    public async Task PartyErasedPublicationCarriesPrivacySafeStatusAndEnvelopeMetadataAsync()
    {
        PartyState state = PartyTestData.CreateErasurePendingState();
        state.Apply(new PartyEncryptionKeyDeleted
        {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            DeletedAt = DateTimeOffset.UtcNow,
        });
        state.Apply(new ErasureVerified
        {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            VerifiedAt = DateTimeOffset.UtcNow,
            VerificationReportId = "report-6-4",
        });

        CompletePartyErasure command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            ErasedAt = DateTimeOffset.UtcNow,
        };
        DomainResult domainResult = PartyAggregate.Handle(command, state);

        domainResult.IsSuccess.ShouldBeTrue();
        PartyErased erased = domainResult.Events.OfType<PartyErased>().ShouldHaveSingleItem();
        erased.PartyId.ShouldBe(PartyTestData.DefaultPartyId);
        erased.TenantId.ShouldBe(PartyTestData.DefaultTenantId);
        erased.ErasureStatus.ShouldBe(ErasureStatus.Erased.ToString());
        erased.VerificationStatus.ShouldBe(ErasureVerificationOverallStatus.Complete.ToString());
        string payloadJson = JsonSerializer.Serialize(erased, JsonOptions);
        payloadJson.ShouldNotContain("John");
        payloadJson.ShouldNotContain("Doe");
        payloadJson.ShouldNotContain("john.doe@example.com");

        var identity = new AggregateIdentity(PartyTestData.DefaultTenantId, "party", PartyTestData.DefaultPartyId);
        var persister = new FakeEventPersister();
        EventPersistResult persisted = await persister.PersistEventsAsync(
            identity,
            aggregateType: "Party",
            CreateEnvelope(
                tenantId: identity.TenantId,
                aggregateId: identity.AggregateId,
                payload: JsonSerializer.SerializeToUtf8Bytes(command)),
            domainResult,
            domainServiceVersion: "v1",
            CancellationToken.None).ConfigureAwait(true);

        persisted.PersistedEnvelopes.ShouldHaveSingleItem();
        Hexalith.EventStore.Server.Events.EventEnvelope envelope = persisted.PersistedEnvelopes[0];
        envelope.TenantId.ShouldBe(PartyTestData.DefaultTenantId);
        envelope.AggregateId.ShouldBe(PartyTestData.DefaultPartyId);
        envelope.CorrelationId.ShouldBe("corr-5-2");
        envelope.CausationId.ShouldBe("cause-5-2");
        envelope.UserId.ShouldBe("user-a");
        envelope.EventTypeName.ShouldBe(typeof(PartyErased).FullName);
        envelope.Payload.Length.ShouldBeGreaterThan(0);
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
            CancellationToken.None).ConfigureAwait(true);

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

    [Fact]
    public async Task PersistedPartyEventsPublishToConfiguredPubSubTopicAsync()
    {
        (AggregateIdentity identity, EventPersistResult persisted) = await PersistCreatedPartyEventsAsync();
        var publisher = new FakeEventPublisher();

        EventPublishResult result = await publisher.PublishEventsAsync(
            identity,
            persisted.PersistedEnvelopes,
            "corr-5-3",
            CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.PublishedCount.ShouldBe(persisted.PersistedEnvelopes.Count);
        publisher.GetPublishedTopics().ShouldBe([identity.PubSubTopic]);
        identity.PubSubTopic.ShouldBe("tenant-authenticated.party.events");

        FakeEventPublisher.PublishCall publishCall = publisher.PublishCalls.ShouldHaveSingleItem();
        publishCall.Identity.ShouldBe(identity);
        publishCall.Topic.ShouldBe(identity.PubSubTopic);
        publishCall.CorrelationId.ShouldBe("corr-5-3");

        IReadOnlyList<Hexalith.EventStore.Server.Events.EventEnvelope> published =
            publisher.GetEventsForTopic(identity.PubSubTopic);
        published.Count.ShouldBe(persisted.PersistedEnvelopes.Count);
        published.Select(e => e.MessageId).Order().ShouldBe(
            persisted.PersistedEnvelopes.Select(e => e.MessageId).Order());
    }

    [Fact]
    public async Task PublicationFailureAfterPersistenceLeavesEventsAvailableForRetryAsync()
    {
        (AggregateIdentity identity, EventPersistResult persisted) = await PersistCreatedPartyEventsAsync();
        var publisher = new FakeEventPublisher();
        publisher.SetupFailure("Pub/sub unavailable");

        EventPublishResult failed = await publisher.PublishEventsAsync(
            identity,
            persisted.PersistedEnvelopes,
            "corr-5-3",
            CancellationToken.None);

        failed.Success.ShouldBeFalse();
        failed.PublishedCount.ShouldBe(0);
        publisher.TotalEventsPublished.ShouldBe(0);
        persisted.PersistedEnvelopes.ShouldNotBeEmpty();

        publisher.ClearFailure();
        EventPublishResult retried = await publisher.PublishEventsAsync(
            identity,
            persisted.PersistedEnvelopes,
            "corr-5-3-retry",
            CancellationToken.None);

        retried.Success.ShouldBeTrue();
        retried.PublishedCount.ShouldBe(persisted.PersistedEnvelopes.Count);
        publisher.GetEventsForTopic(identity.PubSubTopic).Count.ShouldBe(persisted.PersistedEnvelopes.Count);
        publisher.PublishCalls.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SubscriberFailureBeforeAckMayDeliverDuplicateEventOnRetryAsync()
    {
        (AggregateIdentity identity, EventPersistResult persisted) = await PersistCreatedPartyEventsAsync();
        persisted.PersistedEnvelopes.Count.ShouldBeGreaterThan(1);
        var publisher = new FakeEventPublisher();
        publisher.SetupPartialFailure(eventIndex: 1, failureMessage: "Subscriber failed before ack");

        EventPublishResult partial = await publisher.PublishEventsAsync(
            identity,
            persisted.PersistedEnvelopes,
            "corr-5-3",
            CancellationToken.None);

        partial.Success.ShouldBeFalse();
        partial.PublishedCount.ShouldBe(1);

        publisher.ClearFailure();
        EventPublishResult retried = await publisher.PublishEventsAsync(
            identity,
            persisted.PersistedEnvelopes,
            "corr-5-3-retry",
            CancellationToken.None);

        retried.Success.ShouldBeTrue();
        IReadOnlyList<Hexalith.EventStore.Server.Events.EventEnvelope> delivered =
            publisher.GetEventsForTopic(identity.PubSubTopic);
        delivered.Count.ShouldBe(persisted.PersistedEnvelopes.Count + 1);
        delivered
            .GroupBy(e => e.MessageId)
            .ShouldContain(g => g.Key == persisted.PersistedEnvelopes[0].MessageId && g.Count() == 2);
    }

    private static PartyCreated RoundTrip(PartyCreated @event)
    {
        string json = JsonSerializer.Serialize(@event, JsonOptions);
        return JsonSerializer.Deserialize<PartyCreated>(json, JsonOptions)!;
    }

    private static async Task<(AggregateIdentity Identity, EventPersistResult Persisted)> PersistCreatedPartyEventsAsync()
    {
        string partyId = Guid.NewGuid().ToString("D");
        CompositeCommandResult domainResult = PartyAggregate.Handle(new CreatePartyComposite
        {
            PartyId = partyId,
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Grace", LastName = "Hopper" },
        }, state: null);
        domainResult.IsSuccess.ShouldBeTrue();

        var identity = new AggregateIdentity("tenant-authenticated", "party", partyId);
        var persister = new FakeEventPersister();
        EventPersistResult persisted = await persister.PersistEventsAsync(
            identity,
            aggregateType: "Party",
            CreateEnvelope(
                tenantId: identity.TenantId,
                aggregateId: partyId,
                payload: JsonSerializer.SerializeToUtf8Bytes(new { partyId })),
            domainResult,
            domainServiceVersion: "v1",
            CancellationToken.None).ConfigureAwait(true);

        return (identity, persisted);
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
