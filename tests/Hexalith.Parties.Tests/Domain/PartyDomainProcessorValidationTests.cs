using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Dapr.Client;

using FluentValidation;

using Hexalith.Commons.UniqueIds;
using Hexalith.McpCli.Abstractions;
using Hexalith.Parties.Contracts;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Domain;
using Hexalith.Parties.Security;
using Hexalith.Parties.Testing;
using Hexalith.Parties.Validation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.Domain;

public sealed class PartyDomainProcessorValidationTests
{
    [Fact]
    public async Task ProcessAsync_InvalidCreatePartyPayload_ReturnsRejectionResult()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainProcessor invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new CreateParty
        {
            PartyId = "party/unsafe",
            Type = PartyType.Person,
            PersonDetails = null,
        });
        DomainServiceCurrentState currentState = CreateCurrentStateWithProtectedSnapshot(command);

        DomainResult result = await invoker.ProcessAsync(command, currentState, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events
            .OfType<PartyCommandValidationRejected>()
            .ShouldHaveSingleItem();
        rejection.CommandType.ShouldBe(typeof(CreateParty).FullName);
        rejection.Failures.Select(f => f.PropertyName).ShouldContain("PartyId");
        rejection.Failures.Select(f => f.PropertyName).ShouldContain("PersonDetails");

        // currentState carries a non-null snapshot AND a protected event. If validation had not
        // short-circuited before unprotect, both methods would have been invoked. The non-null
        // snapshot is what makes this assertion meaningful - passing null would render it vacuous.
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ProcessAsync_InvalidCompositePayload_DoesNotProduceStateChangingEvents()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainProcessor invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new CreatePartyComposite
        {
            PartyId = "party/unsafe",
            Type = PartyType.Organization,
            OrganizationDetails = null,
        });
        DomainServiceCurrentState currentState = CreateCurrentStateWithProtectedSnapshot(command);

        DomainResult result = await invoker.ProcessAsync(command, currentState, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        result.Events.ShouldAllBe(e => e is PartyCommandValidationRejected);
        result.Events.OfType<PartyCreated>().ShouldBeEmpty();
        result.Events.OfType<PartyDisplayNameDerived>().ShouldBeEmpty();

        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ProcessAsync_InvalidErasePartyPayload_ReturnsRejectionResultWithoutRehydration()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainProcessor invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new EraseParty
        {
            PartyId = string.Empty,
            TenantId = string.Empty,
        });
        DomainServiceCurrentState currentState = CreateCurrentStateWithProtectedSnapshot(command);

        DomainResult result = await invoker.ProcessAsync(command, currentState, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events
            .OfType<PartyCommandValidationRejected>()
            .ShouldHaveSingleItem();
        rejection.CommandType.ShouldBe(typeof(EraseParty).FullName);
        rejection.Failures.Select(f => f.PropertyName).ShouldContain("PartyId");
        rejection.Failures.Select(f => f.PropertyName).ShouldContain("TenantId");
        rejection.Failures.ShouldAllBe(f => !f.ErrorCode.Contains("Ada", StringComparison.OrdinalIgnoreCase));
        rejection.Failures.ShouldAllBe(f => !f.ErrorCode.Contains("Lovelace", StringComparison.OrdinalIgnoreCase));

        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ProcessAsync_InvalidRestrictProcessingPayload_ReturnsBoundedRejectionWithoutRehydration()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainProcessor invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new RestrictProcessing
        {
            PartyId = string.Empty,
            TenantId = string.Empty,
            Reason = new string('x', RestrictProcessingValidator.MaximumReasonLength + 1),
            ActorUserId = "Ada Lovelace",
            CorrelationId = "corr-sensitive",
        });
        DomainServiceCurrentState currentState = CreateCurrentStateWithProtectedSnapshot(command);

        DomainResult result = await invoker.ProcessAsync(command, currentState, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events
            .OfType<PartyCommandValidationRejected>()
            .ShouldHaveSingleItem();
        rejection.CommandType.ShouldBe(typeof(RestrictProcessing).FullName);
        rejection.Failures.Select(f => f.PropertyName).ShouldContain("PartyId");
        rejection.Failures.Select(f => f.PropertyName).ShouldContain("TenantId");
        rejection.Failures.Select(f => f.PropertyName).ShouldContain("Reason");
        rejection.Failures.ShouldAllBe(f => !f.ErrorCode.Contains("Ada", StringComparison.OrdinalIgnoreCase));
        rejection.Failures.ShouldAllBe(f => !f.ErrorCode.Contains("corr-sensitive", StringComparison.OrdinalIgnoreCase));

        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ProcessAsync_InvalidRecordConsentIdentifier_ReturnsBoundedRejectionWithoutRehydration()
    {
        const string unsafeChannelId = "channel/unsafe-sensitive";
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainProcessor invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new RecordConsent
        {
            PartyId = "party-1",
            TenantId = "tenant-a",
            ChannelId = unsafeChannelId,
            Purpose = "marketing",
            LawfulBasis = LawfulBasis.Consent,
        });
        DomainServiceCurrentState currentState = CreateCurrentStateWithProtectedSnapshot(command);

        DomainResult result = await invoker.ProcessAsync(command, currentState, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events
            .ShouldHaveSingleItem()
            .ShouldBeOfType<PartyCommandValidationRejected>();
        rejection.CommandType.ShouldBe(typeof(RecordConsent).FullName);
        rejection.Failures.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(RecordConsent.ChannelId));
        JsonSerializer.Serialize(rejection).ShouldNotContain(unsafeChannelId, Case.Sensitive);
        rejection.Failures.ShouldAllBe(failure => failure.ErrorCode.Length <= 128);
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ProcessAsync_RecordConsentWith101CharacterPurpose_ReturnsBoundedRejectionWithoutRehydration()
    {
        string purpose = new('p', 101);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainProcessor invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new RecordConsent
        {
            PartyId = "party-1",
            TenantId = "tenant-a",
            ChannelId = "channel-1",
            Purpose = purpose,
            LawfulBasis = LawfulBasis.Consent,
        });
        DomainServiceCurrentState currentState = CreateCurrentStateWithProtectedSnapshot(command);

        DomainResult result = await invoker.ProcessAsync(command, currentState, CancellationToken.None);

        PartyCommandValidationRejected rejection = result.Events
            .ShouldHaveSingleItem()
            .ShouldBeOfType<PartyCommandValidationRejected>();
        rejection.Failures.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(RecordConsent.Purpose));
        JsonSerializer.Serialize(rejection).ShouldNotContain(purpose, Case.Sensitive);
        rejection.Failures.ShouldAllBe(failure => failure.ErrorCode.Length <= 128);
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ProcessAsync_InvalidRevokeConsentIdentifier_ReturnsBoundedRejectionWithoutRehydration()
    {
        const string unsafeConsentId = "channel:purpose:unsafe-sensitive";
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainProcessor invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new RevokeConsent
        {
            PartyId = "party-1",
            TenantId = "tenant-a",
            ConsentId = unsafeConsentId,
        });
        DomainServiceCurrentState currentState = CreateCurrentStateWithProtectedSnapshot(command);

        DomainResult result = await invoker.ProcessAsync(command, currentState, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events
            .ShouldHaveSingleItem()
            .ShouldBeOfType<PartyCommandValidationRejected>();
        rejection.CommandType.ShouldBe(typeof(RevokeConsent).FullName);
        rejection.Failures.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(RevokeConsent.ConsentId));
        JsonSerializer.Serialize(rejection).ShouldNotContain(unsafeConsentId, Case.Sensitive);
        rejection.Failures.ShouldAllBe(failure => failure.ErrorCode.Length <= 128);
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    /// <summary>
    /// Ensures undefined lawful bases are rejected before protected state is read.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public async Task ProcessAsync_UndefinedConsentLawfulBasis_ReturnsRejectionWithoutRehydration(int lawfulBasis)
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainProcessor invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new RecordConsent
        {
            PartyId = "party-1",
            TenantId = "tenant-a",
            ChannelId = "ch-email-1",
            Purpose = "marketing",
            LawfulBasis = (LawfulBasis)lawfulBasis,
        });
        DomainServiceCurrentState currentState = CreateCurrentStateWithProtectedSnapshot(command);

        DomainResult result = await invoker.ProcessAsync(command, currentState, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events
            .ShouldHaveSingleItem()
            .ShouldBeOfType<PartyCommandValidationRejected>();
        rejection.CommandType.ShouldBe(typeof(RecordConsent).FullName);
        rejection.Failures.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(RecordConsent.LawfulBasis));
        rejection.Failures.ShouldHaveSingleItem().ErrorCode.ShouldBe("EnumValidator");
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    /// <summary>
    /// Ensures oversized reasons are rejected without reading protected state or echoing the reason.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_OversizedConsentRevocationReason_ReturnsBoundedRejectionWithoutRehydration()
    {
        string reason = new('r', 257);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainProcessor invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new RevokeConsent
        {
            PartyId = "party-1",
            TenantId = "tenant-a",
            ConsentId = "ch-email-1:marketing",
            Reason = reason,
        });
        DomainServiceCurrentState currentState = CreateCurrentStateWithProtectedSnapshot(command);

        DomainResult result = await invoker.ProcessAsync(command, currentState, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events
            .ShouldHaveSingleItem()
            .ShouldBeOfType<PartyCommandValidationRejected>();
        rejection.CommandType.ShouldBe(typeof(RevokeConsent).FullName);
        rejection.Failures.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(RevokeConsent.Reason));
        JsonSerializer.Serialize(rejection).ShouldNotContain(reason, Case.Sensitive);
        rejection.Failures.ShouldHaveSingleItem().ErrorCode.ShouldBe("MaximumLengthValidator");
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ProcessAsync_ValidCreatePartyPayload_ProducesDomainEvents()
    {
        PartyDomainProcessor invoker = CreateInvoker(Substitute.For<IEventPayloadProtectionService>());
        CommandEnvelope command = CreateCommand(new CreateParty
        {
            PartyId = Guid.NewGuid().ToString("D"),
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Ada",
                LastName = "Lovelace",
            },
        });

        DomainResult result = await invoker.ProcessAsync(command, currentState: null, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Events.ShouldContain(e => e is PartyCreated);
        result.Events.ShouldContain(e => e is PartyDisplayNameDerived);
    }

    [Fact]
    public async Task ProcessAsync_OrganizationOnlyCompositeProducesReplacementEvents()
    {
        PartyState state = PartyTestData.CreateOrganizationState();
        OrganizationDetails replacement = new() { LegalName = "Replacement Organization" };
        CommandEnvelope command = CreateCommand(new UpdatePartyComposite
        {
            PartyId = "organization-story49",
            OrganizationDetails = replacement,
        });

        DomainResult result = await CreateInvoker(Substitute.For<IEventPayloadProtectionService>())
            .ProcessAsync(command, state, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        OrganizationDetailsUpdated updated = result.Events.OfType<OrganizationDetailsUpdated>().ShouldHaveSingleItem();
        updated.OrganizationDetails.ShouldBe(replacement);
        result.Events.ShouldContain(e => e is PartyDisplayNameDerived);
        result.Events.OfType<PartyCommandValidationRejected>().ShouldBeEmpty();
        state.Apply(updated);
        state.Organization.ShouldBe(replacement);
    }

    [Fact]
    public async Task ProcessAsync_ProtectedHistoricalPayloadWithDestroyedKey_RedactsAndContinuesRehydration()
    {
        string partyId = Guid.NewGuid().ToString("D");
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        protection
            .UnprotectEventPayloadAsync(
                Arg.Any<AggregateIdentity>(),
                typeof(PartyCreated).FullName!,
                Arg.Any<byte[]>(),
                "json+pdenc-v1",
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new PartyEncryptionKeyDestroyedException("tenant-a", partyId));
        protection
            .UnprotectEventPayloadAsync(
                Arg.Any<AggregateIdentity>(),
                typeof(ErasePartyRequested).FullName!,
                Arg.Any<byte[]>(),
                "json",
                Arg.Any<CancellationToken>())
            .Returns(callInfo => new PayloadProtectionResult((byte[])callInfo[2]!, "json"));

        PartyDomainProcessor invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new MarkPartyEncryptionKeyDeleted
        {
            PartyId = partyId,
            TenantId = "tenant-a",
            DeletedAt = DateTimeOffset.Parse("2026-05-21T20:45:00Z"),
        });
        DomainServiceCurrentState currentState = CreateErasurePendingStateWithProtectedCreatedEvent(command, partyId);
        byte[] originalProtectedPayload = currentState.Events[0].Payload.ToArray();
        EventMetadata originalMetadata = currentState.Events[0].Metadata with { };

        DomainResult result = await invoker.ProcessAsync(command, currentState, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        PartyEncryptionKeyDeleted deleted = result.Events.OfType<PartyEncryptionKeyDeleted>().ShouldHaveSingleItem();
        deleted.PartyId.ShouldBe(partyId);
        // SDK replay receives a redacted JSON copy; the stored stream keeps its protection marker.
        currentState.Events[0].Payload.ShouldBe(originalProtectedPayload);
        currentState.Events[0].Metadata.ShouldBe(originalMetadata);
        currentState.Events[0].Metadata.SerializationFormat.ShouldBe("json+pdenc-v1");
        Encoding.UTF8.GetString(currentState.Events[0].Payload).ShouldContain("\"$enc\":true");
        await protection.Received(1).UnprotectEventPayloadAsync(
            Arg.Any<AggregateIdentity>(),
            typeof(PartyCreated).FullName!,
            Arg.Any<byte[]>(),
            "json+pdenc-v1",
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("stored-redacted")]
    [InlineData("provider-redacted")]
    [InlineData("provider-protected")]
    [InlineData("provider-opaque")]
    [InlineData("provider-version")]
    [InlineData("metadata-version")]
    [InlineData("event-contract")]
    [InlineData("payload-version")]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    [InlineData("sequence-gap")]
    [InlineData("checkpoint")]
    [InlineData("missing-snapshot")]
    public async Task ProcessAsync_UntrustedReplayHistory_RejectsBeforeLifecycleEffect(string corruption)
    {
        string partyId = "party-1";
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        protection.UnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new PayloadProtectionResult((byte[])call[2],
                corruption == "provider-redacted" ? "json-redacted" : "json",
                corruption switch
                {
                    "provider-protected" => EventStorePayloadProtectionMetadata.Unprotected(PayloadProtectionState.Protected),
                    "provider-opaque" => EventStorePayloadProtectionMetadata.ProviderOpaque(),
                    "provider-version" => EventStorePayloadProtectionMetadata.Unprotected() with { MetadataVersion = 2 },
                    _ => EventStorePayloadProtectionMetadata.Unprotected(),
                }));
        CommandEnvelope command = CreateCommand(new MarkPartyEncryptionKeyDeleted
        {
            PartyId = partyId,
            TenantId = "tenant-a",
            DeletedAt = DateTimeOffset.Parse("2026-05-21T20:45:00Z"),
        });
        DomainServiceCurrentState valid = CreateErasurePendingStateWithProtectedCreatedEvent(command, partyId);
        EventEnvelope created = CreateEventEnvelope(command, 1, typeof(PartyCreated).FullName!,
            JsonSerializer.SerializeToUtf8Bytes(new PartyCreated { Type = PartyType.Person }),
            corruption == "stored-redacted" ? "json-redacted" : "json", corruption == "metadata-version" ? 2 : 1);
        EventMetadata metadata = corruption switch
        {
            "event-contract" => created.Metadata with { EventContractType = "future-event" },
            "payload-version" => created.Metadata with { PayloadVersion = 2 },
            "tenant" => created.Metadata with { TenantId = "tenant-b" },
            "domain" => created.Metadata with { Domain = "other" },
            "aggregate" => created.Metadata with { AggregateId = "foreign-party" },
            _ => created.Metadata,
        };
        EventEnvelope requested = corruption == "sequence-gap"
            ? CreateEventEnvelope(command, 3, typeof(ErasePartyRequested).FullName!, valid.Events[1].Payload, "json")
            : valid.Events[1];
        DomainServiceCurrentState untrusted = valid with
        {
            Events = corruption == "missing-snapshot" ? [requested] : [new(metadata, created.Payload, created.Extensions), requested],
            CurrentSequence = corruption == "checkpoint" ? 3 : 2,
            LastSnapshotSequence = corruption == "missing-snapshot" ? 1 : 0,
        };
        byte[] originalPayload = untrusted.Events[0].Payload.ToArray();

        await Should.ThrowAsync<InvalidOperationException>(() => CreateInvoker(protection)
            .ProcessAsync(command, untrusted, TestContext.Current.CancellationToken));

        untrusted.Events[0].Payload.ShouldBe(originalPayload);
        untrusted.Events[0].Metadata.ShouldBe(corruption == "missing-snapshot" ? requested.Metadata : metadata);
        if (!corruption.StartsWith("provider-", StringComparison.Ordinal))
        {
            await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessAsync_SerializedLifecycleTail_UsesValidatedProtectionBoundary(bool invalidMetadata)
    {
        CommandEnvelope command = CreateCommand(new MarkPartyEncryptionKeyDeleted
        {
            PartyId = "party-1", TenantId = "tenant-a", DeletedAt = DateTimeOffset.Parse("2026-05-21T20:45:00Z"),
        });
        DomainServiceCurrentState state = CreateErasurePendingStateWithProtectedCreatedEvent(command, command.AggregateId);
        if (invalidMetadata)
        {
            state = state with { Events = [state.Events[0], CreateEventEnvelope(command, 2,
                typeof(ErasePartyRequested).FullName!, state.Events[1].Payload, "json", metadataVersion: 2)] };
        }

        byte[] wireBytes = JsonSerializer.SerializeToUtf8Bytes(new DomainServiceRequest(command, state), EventStorePayloadSerialization.Options);
        DomainServiceRequest wireRequest = JsonSerializer.Deserialize<DomainServiceRequest>(wireBytes, EventStorePayloadSerialization.Options).ShouldNotBeNull();
        wireRequest.CurrentState.ShouldBeOfType<JsonElement>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        protection.UnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.ArgAt<string>(3) == "json+pdenc-v1"
                ? throw new PartyEncryptionKeyDestroyedException(command.TenantId, command.AggregateId)
                : new PayloadProtectionResult(call.Arg<byte[]>(), "json"));

        if (invalidMetadata)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => CreateInvoker(protection)
                .ProcessAsync(wireRequest.Command, wireRequest.CurrentState, TestContext.Current.CancellationToken));
            await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
        }
        else
        {
            DomainResult result = await CreateInvoker(protection).ProcessAsync(wireRequest.Command, wireRequest.CurrentState, TestContext.Current.CancellationToken);
            result.IsSuccess.ShouldBeTrue();
            result.Events.OfType<PartyEncryptionKeyDeleted>().ShouldHaveSingleItem().PartyId.ShouldBe(command.AggregateId);
            await protection.Received(2).UnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(),
                Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("json-null")]
    [InlineData("json-undefined")]
    [InlineData("destroyed-profile")]
    [InlineData("destroyed-history")]
    public async Task ProcessAsync_RequiredSnapshotUnavailable_RejectsIndependentValidTail(string failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        CommandEnvelope command = CreateCommand(new MarkPartyEncryptionKeyDeleted
        {
            PartyId = "party-1", TenantId = "tenant-a", DeletedAt = DateTimeOffset.Parse("2026-05-21T20:45:00Z"),
        });
        DomainServiceCurrentState fullState = CreateErasurePendingStateWithProtectedCreatedEvent(command, command.AggregateId);
        DomainServiceCurrentState state = fullState with
        {
            SnapshotState = new { Format = failure == "destroyed-history" ? "identity-history-snapshot-v1" : "json+pdenc-v1" },
            Events = [fullState.Events[1]],
            LastSnapshotSequence = 1,
        };
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        protection.UnprotectSnapshotStateAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<object>(failure switch
            {
                "destroyed-profile" or "destroyed-history" => throw new PartyEncryptionKeyDestroyedException(command.TenantId, command.AggregateId),
                "json-null" => JsonSerializer.SerializeToElement<object?>(null),
                "json-undefined" => default(JsonElement),
                _ => null!,
            }));
        protection.UnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => new PayloadProtectionResult(call.Arg<byte[]>(), "json"));

        if (failure.StartsWith("destroyed-", StringComparison.Ordinal))
        {
            await Should.ThrowAsync<PartyEncryptionKeyDestroyedException>(() => CreateInvoker(protection)
                .ProcessAsync(command, state, TestContext.Current.CancellationToken));
        }
        else
        {
            await Should.ThrowAsync<InvalidOperationException>(() => CreateInvoker(protection)
                .ProcessAsync(command, state, TestContext.Current.CancellationToken));
        }

        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
        state.Events.ShouldHaveSingleItem().Metadata.SequenceNumber.ShouldBe(2);
        state.CurrentSequence.ShouldBe(2);
    }

    [Fact]
    public async Task ProcessAsync_ZeroCheckpointWithSnapshot_RejectsOtherwiseValidTail()
    {
        CommandEnvelope command = CreateCommand(new MarkPartyEncryptionKeyDeleted
        {
            PartyId = "party-1", TenantId = "tenant-a", DeletedAt = DateTimeOffset.Parse("2026-05-21T20:45:00Z"),
        });
        DomainServiceCurrentState state = CreateErasurePendingStateWithProtectedCreatedEvent(command, command.AggregateId)
            with { SnapshotState = PartyTestState(command.AggregateId, ErasureStatus.Active) };
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        protection.UnprotectSnapshotStateAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<object>());
        protection.UnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => new PayloadProtectionResult(
                call.ArgAt<string>(3) == "json+pdenc-v1"
                    ? JsonSerializer.SerializeToUtf8Bytes(new PartyCreated { Type = PartyType.Person })
                    : call.Arg<byte[]>(), "json"));

        await Should.ThrowAsync<InvalidOperationException>(() => CreateInvoker(protection)
            .ProcessAsync(command, state, TestContext.Current.CancellationToken));

        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ProcessAsync_ValidSnapshotAndTail_PreservesCreationBeforeDispatch()
    {
        CommandEnvelope command = CreateCommand(new CreateParty
        {
            PartyId = "party-1", Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
        });
        PartyState snapshot = PartyTestState(command.AggregateId, ErasureStatus.Active);
        DomainServiceCurrentState state = new(snapshot,
            [CreateEventEnvelope(command, 2, typeof(PartyDisplayNameDerived).FullName!,
                JsonSerializer.SerializeToUtf8Bytes(new PartyDisplayNameDerived { DisplayName = "Ada Lovelace", SortName = "Lovelace, Ada" }), "json")],
            LastSnapshotSequence: 1, CurrentSequence: 2);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        protection.UnprotectSnapshotStateAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<object>());
        protection.UnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => new PayloadProtectionResult(call.Arg<byte[]>(), "json"));

        DomainResult result = await CreateInvoker(protection).ProcessAsync(command, state, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldBeEmpty(); // Dropping the snapshot would create the party again.
        await protection.Received(1).UnprotectSnapshotStateAsync(command.AggregateIdentity, snapshot, Arg.Any<CancellationToken>());
        await protection.Received(1).UnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(),
            Arg.Any<byte[]>(), "json", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_CanceledDuringTailCapture_StopsBeforeReadingRemainderOrCallingProvider()
    {
        using var cancellation = new CancellationTokenSource();
        CommandEnvelope command = CreateCommand(new MarkPartyEncryptionKeyDeleted
        {
            PartyId = "party-1", TenantId = "tenant-a", DeletedAt = DateTimeOffset.Parse("2026-05-21T20:45:00Z"),
        });
        DomainServiceCurrentState fullState = CreateErasurePendingStateWithProtectedCreatedEvent(command, command.AggregateId);
        EventEnvelope[] events = [.. fullState.Events, CreateEventEnvelope(command, 3, typeof(PartyEncryptionKeyDeleted).FullName!,
            command.Payload, "json")];
        int visited = 0;
        IEnumerable<EventEnvelope> Enumerate()
        {
            foreach (EventEnvelope envelope in events)
            {
                visited++;
                if (visited == 2)
                {
                    cancellation.Cancel();
                }

                yield return envelope;
            }
        }

        IReadOnlyList<EventEnvelope> tail = Substitute.For<IReadOnlyList<EventEnvelope>>();
        tail.Count.Returns(events.Length);
        tail.GetEnumerator().Returns(_ => Enumerate().GetEnumerator());
        DomainServiceCurrentState state = fullState with { Events = tail, CurrentSequence = events.Length };
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();

        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() => CreateInvoker(protection)
            .ProcessAsync(command, state, cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        visited.ShouldBe(2);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessAsync_MutatingProvider_PreservesStoredBytesAndLifecycleReplay(bool destroyed)
    {
        CommandEnvelope command = CreateCommand(new MarkPartyEncryptionKeyDeleted
        {
            PartyId = "party-1", TenantId = "tenant-a", DeletedAt = DateTimeOffset.Parse("2026-05-21T20:45:00Z"),
        });
        DomainServiceCurrentState state = CreateErasurePendingStateWithProtectedCreatedEvent(command, command.AggregateId);
        byte[][] originalPayloads = state.Events.Select(envelope => envelope.Payload.ToArray()).ToArray();
        EventMetadata[] originalMetadata = state.Events.Select(envelope => envelope.Metadata with { }).ToArray();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        protection.UnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                byte[] output = call.ArgAt<string>(1) == typeof(PartyCreated).FullName
                    ? JsonSerializer.SerializeToUtf8Bytes(new PartyCreated { Type = PartyType.Person })
                    : call.Arg<byte[]>().ToArray();
                Array.Fill(call.Arg<byte[]>(), (byte)0);
                return destroyed && call.ArgAt<string>(3) == "json+pdenc-v1"
                    ? throw new PartyEncryptionKeyDestroyedException(command.TenantId, command.AggregateId)
                    : new PayloadProtectionResult(output, "json");
            });

        DomainResult result = await CreateInvoker(protection).ProcessAsync(command, state, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Events.OfType<PartyEncryptionKeyDeleted>().ShouldHaveSingleItem().PartyId.ShouldBe(command.AggregateId);
        for (int index = 0; index < state.Events.Count; index++)
        {
            state.Events[index].Payload.ShouldBe(originalPayloads[index]);
            state.Events[index].Metadata.ShouldBe(originalMetadata[index]);
        }
    }

    [Fact]
    public async Task ProcessAsync_DestroyedHistoryKey_CannotUseProfileRedactionFallback()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        protection.UnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(),
            "json+identity-history-v1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new PartyEncryptionKeyDestroyedException("tenant-a", "party-1"));
        CommandEnvelope command = CreateCommand(new MarkPartyEncryptionKeyDeleted
        {
            PartyId = "party-1", TenantId = "tenant-a", DeletedAt = DateTimeOffset.Parse("2026-05-21T20:45:00Z"),
        });
        DomainServiceCurrentState state = CreateErasurePendingStateWithProtectedCreatedEvent(command, "party-1");
        state = state with { Events = [CreateEventEnvelope(command, 1, typeof(HumanActorBindingEstablished).FullName!,
            "{}"u8.ToArray(), "json+identity-history-v1"), state.Events[1]] };

        await Should.ThrowAsync<PartyEncryptionKeyDestroyedException>(() => CreateInvoker(protection)
            .ProcessAsync(command, state, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessAsync_UnknownCommandType_ReturnsUnresolvedCommandTypeRejection()
    {
        PartyDomainProcessor invoker = CreateInvoker(Substitute.For<IEventPayloadProtectionService>());
        CommandEnvelope command = CreateRawCommand(
            commandType: "Hexalith.Parties.Contracts.Commands.NoSuchCommand, Hexalith.Parties.Contracts",
            payload: JsonSerializer.SerializeToUtf8Bytes(new { foo = "bar" }));

        DomainResult result = await invoker.ProcessAsync(command, currentState: null, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events
            .OfType<PartyCommandValidationRejected>()
            .ShouldHaveSingleItem();
        rejection.Failures.ShouldHaveSingleItem();
        rejection.Failures[0].ErrorCode.ShouldBe("UnresolvedCommandType");
    }

    [Fact]
    public async Task ProcessAsync_AssemblyQualifiedCommandType_ResolvesAndValidates()
    {
        PartyDomainProcessor invoker = CreateInvoker(Substitute.For<IEventPayloadProtectionService>());
        CommandEnvelope command = CreateRawCommand(
            commandType: typeof(CreateParty).AssemblyQualifiedName!,
            payload: JsonSerializer.SerializeToUtf8Bytes(new CreateParty
            {
                PartyId = "party/unsafe",
                Type = PartyType.Person,
                PersonDetails = null,
            }));

        DomainResult result = await invoker.ProcessAsync(command, currentState: null, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.OfType<PartyCommandValidationRejected>().ShouldHaveSingleItem();
        rejection.Failures.Select(f => f.PropertyName).ShouldContain("PartyId");
    }

    [Fact]
    public async Task ProcessAsync_MalformedJsonPayload_ReturnsInvalidJsonRejection()
    {
        PartyDomainProcessor invoker = CreateInvoker(Substitute.For<IEventPayloadProtectionService>());
        CommandEnvelope command = CreateRawCommand(
            commandType: typeof(CreateParty).FullName!,
            payload: "{ this is not valid json"u8.ToArray());

        DomainResult result = await invoker.ProcessAsync(command, currentState: null, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.OfType<PartyCommandValidationRejected>().ShouldHaveSingleItem();
        rejection.Failures.ShouldHaveSingleItem();
        rejection.Failures[0].ErrorCode.ShouldBe("InvalidJson");

        // Critical: the rejection event must NOT carry the raw JsonException.Message which can
        // include payload fragments and parser offset context. Only stable error codes leak out.
        foreach (PartyValidationFailure failure in rejection.Failures)
        {
            failure.ErrorCode.ShouldNotContain("Position");
            failure.ErrorCode.ShouldNotContain("LineNumber");
        }
    }

    [Fact]
    public async Task ProcessAsync_EmptyPayload_ReturnsEmptyPayloadRejection()
    {
        PartyDomainProcessor invoker = CreateInvoker(Substitute.For<IEventPayloadProtectionService>());
        CommandEnvelope command = CreateRawCommand(
            commandType: typeof(CreateParty).FullName!,
            payload: []);

        DomainResult result = await invoker.ProcessAsync(command, currentState: null, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.OfType<PartyCommandValidationRejected>().ShouldHaveSingleItem();
        rejection.Failures[0].ErrorCode.ShouldBe("EmptyPayload");
    }

    /// <summary>
    /// Rejects an unvalidated command before reading otherwise admissible protected replay state.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_CommandTypeWithNoRegisteredValidator_RejectsBeforeRehydration()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainProcessor invoker = new(protection, scopeFactory, NullLogger<PartyDomainProcessor>.Instance);

        CommandEnvelope command = CreateCommand(new CreateParty
        {
            PartyId = Guid.NewGuid().ToString("D"),
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
        });

        DomainServiceCurrentState currentState = CreateCurrentStateWithProtectedSnapshot(command) with
        {
            LastSnapshotSequence = 1,
            CurrentSequence = 2,
            Events = [CreateEventEnvelope(command, 2, typeof(PartyCreated).FullName!, "{}"u8.ToArray(), "json+pdenc-v1")],
        };
        DomainResult result = await invoker.ProcessAsync(command, currentState, TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.ShouldHaveSingleItem()
            .ShouldBeOfType<PartyCommandValidationRejected>();
        rejection.CommandType.ShouldBe(typeof(CreateParty).FullName);
        PartyValidationFailure failure = rejection.Failures.ShouldHaveSingleItem();
        failure.PropertyName.ShouldBe("CommandType");
        failure.ErrorCode.ShouldBe("ValidatorNotRegistered");
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ProcessAsync_RetryErasureVerification_KeyDestroyedRunsExistingOrchestratorAndPersistsBoundedRecords()
    {
        string partyId = Guid.NewGuid().ToString("D");
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        IPartyErasureRecordStore recordStore = Substitute.For<IPartyErasureRecordStore>();
        ErasureCertificate certificate = new()
        {
            PartyId = partyId,
            TenantId = "tenant-a",
            Timestamp = DateTimeOffset.Parse("2026-05-21T20:45:00Z"),
            KeyVersionsDestroyed = [1],
            VerificationStatus = ErasureVerificationStatus.Verified,
        };
        ErasureCertificate persistedCertificate = certificate;
        recordStore.GetCertificateAsync("tenant-a", partyId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<ErasureCertificate?>(persistedCertificate));
        recordStore.SaveCertificateAsync(Arg.Any<ErasureCertificate>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                persistedCertificate = call.ArgAt<ErasureCertificate>(0);
                return Task.CompletedTask;
            });
        DateTimeOffset verificationAt = certificate.Timestamp.AddMinutes(5);
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(verificationAt);
        IErasureVerificationService verificationService = new ErasureVerificationService(
            [
                (_, _, _) => Task.FromResult(new ErasureVerificationStoreResult
                    {
                        StoreName = "detail-projection",
                        Status = ErasureStoreCleanupStatus.Cleaned,
                        Timestamp = certificate.Timestamp,
                    }),
            ],
            NullLogger<ErasureVerificationService>.Instance,
            clock);
        PartyDomainProcessor invoker = CreateInvoker(protection, recordStore, CreateOrchestrator(verificationService));
        PartyState state = PartyTestState(partyId, ErasureStatus.KeyDestroyed);
        CommandEnvelope command = CreateCommand(new RetryErasureVerification { PartyId = partyId, TenantId = "tenant-a" });

        DomainResult result = await invoker.ProcessAsync(command, state, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Events.OfType<ErasureVerified>().ShouldHaveSingleItem();
        result.Events.OfType<PartyErased>().ShouldHaveSingleItem();
        await recordStore.Received(1).SaveVerificationReportAsync(
            Arg.Is<ErasureVerificationReport>(report => report != null
                && report.OverallStatus == ErasureVerificationOverallStatus.Complete
                && report.Timestamp == verificationAt),
            Arg.Any<CancellationToken>());
        await recordStore.DidNotReceiveWithAnyArgs().SaveCertificateAsync(default!, default);
        ErasureCertificate savedCertificate = (await recordStore.GetCertificateAsync(
            "tenant-a", partyId, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        savedCertificate.Timestamp.ShouldBe(certificate.Timestamp);
        savedCertificate.VerificationStatus.ShouldBe(ErasureVerificationStatus.Verified);
        await recordStore.Received(1).SaveStatusAsync(
            Arg.Is<PartyErasureStatusRecord>(saved => saved != null && saved.Status == ErasureStatus.Verified.ToString() && saved.ErrorMessage == null),
            Arg.Any<CancellationToken>());
        await recordStore.Received(1).SaveStatusAsync(
            Arg.Is<PartyErasureStatusRecord>(saved => saved != null && saved.Status == ErasureStatus.Erased.ToString() && saved.ErasedAt == DateTimeOffset.Parse("2026-05-21T20:50:00Z")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_RetryErasureVerification_FailedSdkCleanupCannotVerifyOrEmitTerminalErasure()
    {
        string partyId = Guid.NewGuid().ToString("D");
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        IPartyErasureRecordStore recordStore = Substitute.For<IPartyErasureRecordStore>();
        ErasureCertificate certificate = new()
        {
            PartyId = partyId,
            TenantId = "tenant-a",
            Timestamp = DateTimeOffset.Parse("2026-05-21T20:45:00Z"),
            KeyVersionsDestroyed = [1],
            VerificationStatus = ErasureVerificationStatus.Verified,
        };
        recordStore.GetCertificateAsync("tenant-a", partyId, Arg.Any<CancellationToken>())
            .Returns(certificate);
        ErasureStoreCleanupDelegate failedSdkCleanup = (_, _, _) => Task.FromResult(
            new ErasureVerificationStoreResult
            {
                StoreName = "sdk-read-models",
                Status = ErasureStoreCleanupStatus.Failed,
                Timestamp = DateTimeOffset.Parse("2026-05-21T20:50:00Z"),
                ErrorMessage = "SDK read-model cleanup did not complete.",
            });
        var verificationService = new ErasureVerificationService(
            [failedSdkCleanup],
            NullLogger<ErasureVerificationService>.Instance);
        PartyDomainProcessor invoker = CreateInvoker(
            protection,
            recordStore,
            CreateOrchestrator(verificationService));
        PartyState state = PartyTestState(partyId, ErasureStatus.KeyDestroyed);
        CommandEnvelope command = CreateCommand(new RetryErasureVerification { PartyId = partyId, TenantId = "tenant-a" });

        DomainResult result = await invoker.ProcessAsync(command, state, CancellationToken.None);

        result.Events.OfType<ErasureVerified>().ShouldBeEmpty();
        result.Events.OfType<PartyErased>().ShouldBeEmpty();
        await recordStore.Received(1).SaveVerificationReportAsync(
            Arg.Is<ErasureVerificationReport>(report => report != null
                && report.OverallStatus != ErasureVerificationOverallStatus.Complete
                && report.StoreResults.Single().StoreName == "sdk-read-models"
                && report.StoreResults.Single().Status == ErasureStoreCleanupStatus.Failed),
            Arg.Any<CancellationToken>());
        await recordStore.DidNotReceiveWithAnyArgs().SaveCertificateAsync(default!, default);
    }

    [Theory]
    [InlineData(ErasureStoreCleanupStatus.Failed, false)]
    [InlineData(ErasureStoreCleanupStatus.Pending, false)]
    [InlineData(ErasureStoreCleanupStatus.Failed, true)]
    public async Task ProcessAsync_RetryErasureVerification_PreservesPersistedKeyEvidenceUntilCleanupSucceeds(
        ErasureStoreCleanupStatus incompleteStatus, bool mixedStores)
    {
        string partyId = UniqueIdHelper.GenerateSortableUniqueStringId();
        using DaprClient daprClient = Substitute.For<DaprClient>();
        Dictionary<string, string> persistedState = new(StringComparer.Ordinal);
        ConfigureErasureRecordState<ErasureCertificate>(daprClient, persistedState);
        ConfigureErasureRecordState<ErasureVerificationReport>(daprClient, persistedState);
        ConfigureErasureRecordState<PartyErasureStatusRecord>(daprClient, persistedState);
        var recordStore = new PartyErasureRecordStore(daprClient);
        CancellationToken token = TestContext.Current.CancellationToken;
        ErasureCertificate certificate = new()
        {
            PartyId = partyId,
            TenantId = "tenant-a",
            Timestamp = DateTimeOffset.UnixEpoch,
            KeyVersionsDestroyed = [1, 2],
            VerificationStatus = ErasureVerificationStatus.Verified,
        };
        await recordStore.SaveCertificateAsync(certificate, token);
        string originalCertificate = persistedState[$"tenant-a:erasure:{partyId}"];
        int attempts = 0;
        DateTimeOffset verificationAt = certificate.Timestamp.AddMinutes(5);
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_ => verificationAt);
        List<ErasureStoreCleanupDelegate> cleanups =
            [
                (tenant, party, cancellation) =>
                {
                    tenant.ShouldBe("tenant-a");
                    party.ShouldBe(partyId);
                    cancellation.ShouldBe(token);
                    attempts++;
                    return Task.FromResult(new ErasureVerificationStoreResult
                    {
                        StoreName = "sdk-read-models",
                        Status = attempts == 1 ? incompleteStatus : ErasureStoreCleanupStatus.Cleaned,
                        Timestamp = verificationAt,
                    });
                },
            ];
        if (mixedStores)
        {
            cleanups.Add((_, _, _) => Task.FromResult(new ErasureVerificationStoreResult
            {
                StoreName = "cache",
                Status = ErasureStoreCleanupStatus.Cleaned,
                Timestamp = verificationAt,
            }));
        }

        var verification = new ErasureVerificationService(cleanups, NullLogger<ErasureVerificationService>.Instance, clock);
        PartyDomainProcessor processor = CreateInvoker(
            Substitute.For<IEventPayloadProtectionService>(), recordStore, CreateOrchestrator(verification));
        PartyState state = PartyTestState(partyId, ErasureStatus.KeyDestroyed);
        CommandEnvelope command = CreateCommand(new RetryErasureVerification { PartyId = partyId, TenantId = "tenant-a" });

        DomainResult first = await processor.ProcessAsync(command, state, token);

        first.Events.ShouldBeEmpty();
        persistedState[$"tenant-a:erasure:{partyId}"].ShouldBe(originalCertificate);
        ErasureVerificationReport incomplete = (await recordStore.GetVerificationReportAsync("tenant-a", partyId, token)).ShouldNotBeNull();
        incomplete.OverallStatus.ShouldBe(mixedStores ? ErasureVerificationOverallStatus.Partial
            : incompleteStatus == ErasureStoreCleanupStatus.Pending
                ? ErasureVerificationOverallStatus.Pending : ErasureVerificationOverallStatus.Failed);
        incomplete.Timestamp.ShouldBe(verificationAt);
        PartyErasureStatusRecord pending = (await recordStore.GetStatusAsync("tenant-a", partyId, token)).ShouldNotBeNull();
        pending.Status.ShouldBe(incomplete.OverallStatus.ToString());
        pending.ErasedAt.ShouldBeNull();
        pending.UpdatedAt.ShouldBe(verificationAt);
        verificationAt = verificationAt.AddMinutes(5);

        DomainResult retry = await processor.ProcessAsync(command, state, token);

        attempts.ShouldBe(2);
        retry.IsSuccess.ShouldBeTrue();
        retry.Events.OfType<ErasureVerified>().ShouldHaveSingleItem().VerifiedAt.ShouldBe(verificationAt);
        retry.Events.OfType<PartyErased>().ShouldHaveSingleItem().ErasedAt.ShouldBe(verificationAt);
        persistedState[$"tenant-a:erasure:{partyId}"].ShouldBe(originalCertificate);
        ErasureVerificationReport complete = (await recordStore.GetVerificationReportAsync("tenant-a", partyId, token)).ShouldNotBeNull();
        complete.OverallStatus.ShouldBe(ErasureVerificationOverallStatus.Complete);
        complete.Timestamp.ShouldBe(verificationAt);
        complete.StoreResults.Count.ShouldBe(mixedStores ? 2 : 1);
        complete.StoreResults.ShouldAllBe(store => store.Status == ErasureStoreCleanupStatus.Cleaned);
        PartyErasureStatusRecord erased = (await recordStore.GetStatusAsync("tenant-a", partyId, token)).ShouldNotBeNull();
        erased.Status.ShouldBe(nameof(ErasureStatus.Erased));
        erased.UpdatedAt.ShouldBe(verificationAt);
        erased.ErasedAt.ShouldBe(verificationAt);
        erased.ErrorMessage.ShouldBeNull();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("party")]
    [InlineData("pending")]
    [InlineData("failed")]
    public async Task ProcessAsync_RetryErasureVerification_InvalidCertificateCannotCleanOrEmitErasure(string invalidField)
    {
        string partyId = UniqueIdHelper.GenerateSortableUniqueStringId();
        ErasureCertificate certificate = new()
        {
            PartyId = invalidField == "party" ? "other-party" : partyId,
            TenantId = invalidField == "tenant" ? "other-tenant" : "tenant-a",
            Timestamp = DateTimeOffset.UnixEpoch,
            KeyVersionsDestroyed = [1],
            VerificationStatus = invalidField == "pending" ? ErasureVerificationStatus.Pending
                : invalidField == "failed" ? ErasureVerificationStatus.Failed : ErasureVerificationStatus.Verified,
        };
        IPartyErasureRecordStore recordStore = Substitute.For<IPartyErasureRecordStore>();
        recordStore.GetCertificateAsync("tenant-a", partyId, Arg.Any<CancellationToken>()).Returns(certificate);
        bool cleanupCalled = false;
        ErasureStoreCleanupDelegate cleanup = (_, _, _) =>
        {
            cleanupCalled = true;
            throw new InvalidOperationException("Invalid certificate must not reach cleanup.");
        };
        var verification = new ErasureVerificationService([cleanup], NullLogger<ErasureVerificationService>.Instance);
        PartyDomainProcessor processor = CreateInvoker(
            Substitute.For<IEventPayloadProtectionService>(), recordStore, CreateOrchestrator(verification));
        PartyState state = PartyTestState(partyId, ErasureStatus.KeyDestroyed);
        CommandEnvelope command = CreateCommand(new RetryErasureVerification { PartyId = partyId, TenantId = "tenant-a" });

        DomainResult result = await processor.ProcessAsync(command, state, TestContext.Current.CancellationToken);

        cleanupCalled.ShouldBeFalse();
        result.Events.ShouldBeEmpty();
        await recordStore.DidNotReceiveWithAnyArgs().SaveCertificateAsync(default!, default);
        await recordStore.Received(1).SaveVerificationReportAsync(
            Arg.Is<ErasureVerificationReport>(report => report.TenantId == "tenant-a" && report.PartyId == partyId
                && report.OverallStatus == ErasureVerificationOverallStatus.Failed), Arg.Any<CancellationToken>());
        await recordStore.Received(1).SaveStatusAsync(
            Arg.Is<PartyErasureStatusRecord>(status => status.TenantId == "tenant-a" && status.PartyId == partyId
                && status.Status == nameof(ErasureVerificationOverallStatus.Failed) && status.ErasedAt == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_RetryErasureVerification_ActivePartyIsBoundedRejectionWithoutStoreMutation()
    {
        string partyId = Guid.NewGuid().ToString("D");
        IPartyErasureRecordStore recordStore = Substitute.For<IPartyErasureRecordStore>();
        IErasureVerificationService verificationService = Substitute.For<IErasureVerificationService>();
        PartyDomainProcessor invoker = CreateInvoker(
            Substitute.For<IEventPayloadProtectionService>(),
            recordStore,
            CreateOrchestrator(verificationService));
        CommandEnvelope command = CreateCommand(new RetryErasureVerification { PartyId = partyId, TenantId = "tenant-a" });

        DomainResult result = await invoker.ProcessAsync(command, PartyTestState(partyId, ErasureStatus.Active), CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyErasureInProgress rejection = result.Events.OfType<PartyErasureInProgress>().ShouldHaveSingleItem();
        rejection.Status.ShouldBe(ErasureStatus.Active.ToString());
        string rejectionMessage = rejection.Message.ShouldNotBeNull();
        rejectionMessage.ShouldNotContain("key", Case.Insensitive);
        rejectionMessage.ShouldNotContain("decrypt", Case.Insensitive);
        await recordStore.DidNotReceiveWithAnyArgs().SaveCertificateAsync(default!, default);
        await recordStore.DidNotReceiveWithAnyArgs().SaveVerificationReportAsync(default!, default);
        await verificationService.DidNotReceiveWithAnyArgs().VerifyErasureAsync(default!, default!, default!, default);
    }

    private static void ConfigureErasureRecordState<T>(DaprClient client, Dictionary<string, string> state)
        where T : class
    {
        client.SaveStateAsync("statestore", Arg.Any<string>(), Arg.Any<T>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                state[call.ArgAt<string>(1)] = JsonSerializer.Serialize(call.ArgAt<T>(2));
                return Task.CompletedTask;
            });
        client.GetStateAsync<T>("statestore", Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => state.TryGetValue(call.ArgAt<string>(1), out string? json) ? JsonSerializer.Deserialize<T>(json)! : null!);
    }

    [Theory]
    [MemberData(nameof(InvalidGatewayComposites))]
    public async Task ProcessAsync_InvalidGatewayCompositeRejectsBeforeProtectedStateAccess(object payload, string code)
    {
        ArgumentNullException.ThrowIfNull(payload);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainProcessor invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateRawCommand(payload.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType()), "p");
        DomainResult result = await invoker.ProcessAsync(command, CreateCurrentStateWithProtectedSnapshot(command), TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyCommandValidationRejected>();
        rejection.CommandType.ShouldBe(payload.GetType().FullName);
        rejection.Failures.ShouldContain(f => f.ErrorCode == code);
        JsonSerializer.Serialize(rejection).ShouldNotContain("private-value");
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ProcessAsync_UndefinedContactUpdateTypeRejectsBeforeProtectedStateAccess()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        UpdatePartyComposite payload = new()
        {
            PartyId = "p",
            UpdateContactChannels = [new() { PartyId = "p", ContactChannelId = "c", Type = (ContactChannelType)999 }],
        };
        CommandEnvelope command = CreateCommand(payload);
        DomainResult result = await CreateInvoker(protection).ProcessAsync(command,
            CreateCurrentStateWithProtectedSnapshot(command), TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyCommandValidationRejected>();
        rejection.CommandType.ShouldBe(typeof(UpdatePartyComposite).FullName);
        rejection.Failures.ShouldBe([new PartyValidationFailure
        {
            PropertyName = "UpdateContactChannels[0].Type",
            ErrorCode = "EnumValidator",
        }]);
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Theory]
    [InlineData(typeof(CreatePartyComposite))]
    [InlineData(typeof(UpdatePartyComposite))]
    [InlineData(typeof(ReactivateParty))]
    [InlineData(typeof(DeactivateParty))]
    public void DeclaredCommandExamplesPassProductionDeserializationAndRegisteredValidation(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        HexalithCommandAttribute declaration = commandType.GetCustomAttribute<HexalithCommandAttribute>().ShouldNotBeNull();
        object payload = JsonSerializer.Deserialize(declaration.Example!, commandType, PartiesJsonOptions.Default).ShouldNotBeNull();
        using ServiceProvider provider = new ServiceCollection()
            .AddValidatorsFromAssemblyContaining<CreatePartyValidator>()
            .BuildServiceProvider();
        IValidator validator = (IValidator)provider.GetRequiredService(typeof(IValidator<>).MakeGenericType(commandType));
        validator.Validate(new ValidationContext<object>(payload)).Errors.ShouldBeEmpty();

        if (payload is CreatePartyComposite create)
        {
            create.ContactChannels.ShouldBeEmpty();
            create.Identifiers.ShouldBeEmpty();
        }
        else if (payload is UpdatePartyComposite update)
        {
            update.AddContactChannels.ShouldBeEmpty();
            update.UpdateContactChannels.ShouldBeEmpty();
            update.RemoveContactChannelIds.ShouldBeEmpty();
            update.AddIdentifiers.ShouldBeEmpty();
            update.RemoveIdentifierIds.ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData("add-contact")]
    [InlineData("remove-contact")]
    [InlineData("add-identifier")]
    public void CompositeUpdatesAcceptEachSupportedListAsTheOnlyChange(string member)
    {
        UpdatePartyComposite payload = member switch
        {
            "add-contact" => new()
            {
                PartyId = "p",
                AddContactChannels = [new() { PartyId = "p", ContactChannelId = "c", Type = ContactChannelType.Email, Value = "a@b.test" }],
            },
            "remove-contact" => new() { PartyId = "p", RemoveContactChannelIds = ["c"] },
            _ => new()
            {
                PartyId = "p",
                AddIdentifiers = [new() { PartyId = "p", IdentifierId = "i", Type = IdentifierType.Other, Value = "v" }],
            },
        };

        new UpdatePartyCompositeValidator().Validate(payload).Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_EmptyContactUpdateHasOnlyTheEntryFailureBeforeProtectedStateAccess()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        UpdatePartyComposite payload = new()
        {
            PartyId = "p",
            UpdateContactChannels = [new() { PartyId = "p", ContactChannelId = "c" }],
        };
        CommandEnvelope command = CreateCommand(payload);
        DomainResult result = await CreateInvoker(protection).ProcessAsync(command,
            CreateCurrentStateWithProtectedSnapshot(command), TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyCommandValidationRejected>();
        rejection.CommandType.ShouldBe(typeof(UpdatePartyComposite).FullName);
        rejection.Failures.ShouldBe([new PartyValidationFailure
        {
            PropertyName = "UpdateContactChannels[0]",
            ErrorCode = "CompositeEmptyContactUpdate",
        }]);
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Theory]
    [MemberData(nameof(NullGatewayComposites))]
    public async Task ProcessAsync_NullCompositeCollectionsAndItemsHaveExactFailuresBeforeProtectedStateAccess(object payload, string propertyName, bool removalItem)
    {
        ArgumentNullException.ThrowIfNull(payload);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        CommandEnvelope command = CreateRawCommand(payload.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType()), "p");
        DomainResult result = await CreateInvoker(protection).ProcessAsync(command,
            CreateCurrentStateWithProtectedSnapshot(command), TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyCommandValidationRejected>();
        rejection.CommandType.ShouldBe(payload.GetType().FullName);
        List<PartyValidationFailure> expected = [new() { PropertyName = propertyName, ErrorCode = "NotNullValidator" }];
        if (removalItem)
        {
            expected.Add(new() { PropertyName = propertyName, ErrorCode = "NotEmptyValidator" });
        }

        rejection.Failures.ShouldBe(expected);
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    public static IEnumerable<object[]> NullGatewayComposites()
    {
        CreatePartyComposite create = new() { PartyId = "p", Type = PartyType.Person, PersonDetails = new() { FirstName = "A", LastName = "B" } };
        UpdatePartyComposite update = new() { PartyId = "p", PersonDetails = create.PersonDetails };
        yield return [create with { ContactChannels = null! }, "ContactChannels", false];
        yield return [create with { Identifiers = null! }, "Identifiers", false];
        yield return [create with { ContactChannels = [null!] }, "ContactChannels[0]", false];
        yield return [create with { Identifiers = [null!] }, "Identifiers[0]", false];
        yield return [update with { AddContactChannels = null! }, "AddContactChannels", false];
        yield return [update with { UpdateContactChannels = null! }, "UpdateContactChannels", false];
        yield return [update with { RemoveContactChannelIds = null! }, "RemoveContactChannelIds", false];
        yield return [update with { AddIdentifiers = null! }, "AddIdentifiers", false];
        yield return [update with { RemoveIdentifierIds = null! }, "RemoveIdentifierIds", false];
        yield return [update with { AddContactChannels = [null!] }, "AddContactChannels[0]", false];
        yield return [update with { UpdateContactChannels = [null!] }, "UpdateContactChannels[0]", false];
        yield return [update with { AddIdentifiers = [null!] }, "AddIdentifiers[0]", false];
        yield return [update with { RemoveContactChannelIds = [null!] }, "RemoveContactChannelIds[0]", true];
        yield return [update with { RemoveIdentifierIds = [null!] }, "RemoveIdentifierIds[0]", true];
    }

    [Theory]
    [InlineData("type")]
    [InlineData("value")]
    [InlineData("empty")]
    [InlineData("false")]
    public void CompositeContactUpdatesAcceptOneNonNullMutableValue(string member)
    {
        UpdateContactChannel change = member switch
        {
            "type" => new() { PartyId = "p", ContactChannelId = "ch-email-1", Type = ContactChannelType.Phone },
            "value" => new() { PartyId = "p", ContactChannelId = "ch-email-1", Value = "replacement@example.test" },
            "empty" => new() { PartyId = "p", ContactChannelId = "ch-email-1", Value = string.Empty },
            _ => new() { PartyId = "p", ContactChannelId = "ch-email-1", IsPreferred = false },
        };
        UpdatePartyComposite payload = new() { PartyId = "p", UpdateContactChannels = [change] };
        new UpdatePartyCompositeValidator().Validate(payload).IsValid.ShouldBeTrue();

        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        ContactChannel before = state.ContactChannels.Single(c => c.Id == change.ContactChannelId);
        var result = PartyAggregate.Handle(payload, state);
        result.IsSuccess.ShouldBeTrue();
        ContactChannelUpdated update = result.Events.OfType<ContactChannelUpdated>().ShouldHaveSingleItem();
        update.Type.ShouldBe(change.Type);
        update.Value.ShouldBe(change.Value);
        update.IsPreferred.ShouldBe(change.IsPreferred);
        state.Apply(update);
        ContactChannel after = state.ContactChannels.Single(c => c.Id == change.ContactChannelId);
        after.Type.ShouldBe(change.Type ?? before.Type);
        after.Value.ShouldBe(change.Value ?? before.Value);
        after.IsPreferred.ShouldBe(change.IsPreferred ?? before.IsPreferred);
    }

    public static IEnumerable<object[]> InvalidGatewayComposites()
    {
        CreatePartyComposite create = new() { PartyId = "p", Type = PartyType.Person, PersonDetails = new() { FirstName = "A", LastName = "B" } };
        UpdatePartyComposite update = new() { PartyId = "p", PersonDetails = create.PersonDetails };
        yield return [create with { PersonDetails = create.PersonDetails! with { FirstName = new string(' ', 513) } }, "CompositeStringLength"];
        yield return [update with { OrganizationDetails = new() { LegalName = "private-value" + new string('x', 513) } }, "CompositeStringLength"];
        yield return [new UpdatePartyComposite { PartyId = "p" }, "CompositeEmptyUpdate"];
        yield return [create with { ContactChannels = [new() { PartyId = "other", ContactChannelId = "c", Type = ContactChannelType.Email, Value = "a@b.test" }] }, "PredicateValidator"];
        yield return [update with { AddIdentifiers = [new() { PartyId = "other", IdentifierId = "i", Type = IdentifierType.Other, Value = "v" }] }, "PredicateValidator"];
        yield return [create with { PartyId = new string('p', 129) }, "PredicateValidator"];
        yield return [update with { RemoveIdentifierIds = [new string('i', 129)] }, "PredicateValidator"];
        yield return [create with { Identifiers = Enumerable.Range(0, 100).Select(_ => new AddIdentifier { PartyId = "p", IdentifierId = "i", Type = IdentifierType.Other, Value = "v" }).ToArray() }, "PredicateValidator"];
        yield return [update with { RemoveContactChannelIds = Enumerable.Repeat("c", 100).ToArray() }, "PredicateValidator"];
        yield return [create with { Identifiers = Enumerable.Range(0, 8).Select(_ => new AddIdentifier { PartyId = "p", IdentifierId = "i", Type = IdentifierType.Other, Value = new string('x', 512) }).ToArray() }, "CompositeTotalLength"];
        yield return [update with { RemoveIdentifierIds = Enumerable.Repeat(new string('i', 128), 32).ToArray() }, "CompositeTotalLength"];
    }

    [Theory]
    [MemberData(nameof(OversizedCompositeStrings))]
    public async Task ProcessAsync_EveryExposedStringIsBoundedBeforeStateAccess(object payload)
        => await ProcessAsync_InvalidGatewayCompositeRejectsBeforeProtectedStateAccess(payload, "CompositeStringLength");

    public static IEnumerable<object[]> OversizedCompositeStrings()
    {
        PersonDetails person = new() { FirstName = "A", LastName = "B", Prefix = "Dr", Suffix = "Jr" };
        OrganizationDetails organization = new() { LegalName = "Company", TradingName = "Trade", LegalForm = "Ltd", RegistrationNumber = "reg" };
        AddContactChannel channel = new() { PartyId = "p", ContactChannelId = "c", Type = ContactChannelType.Email, Value = "mail@example.test" };
        AddIdentifier identifier = new() { PartyId = "p", IdentifierId = "i", Type = IdentifierType.Other, Value = "v" };
        object[] commands = [
            new CreatePartyComposite { PartyId = "p", Type = PartyType.Person, PersonDetails = person, OrganizationDetails = organization, ContactChannels = [channel], Identifiers = [identifier] },
            new UpdatePartyComposite { PartyId = "p", PersonDetails = person, OrganizationDetails = organization, AddContactChannels = [channel], UpdateContactChannels = [new() { PartyId = "p", ContactChannelId = "u", Value = "v" }], AddIdentifiers = [identifier], RemoveContactChannelIds = ["r"], RemoveIdentifierIds = ["r"] }];
        foreach (object command in commands)
        {
            JsonNode root = JsonSerializer.SerializeToNode(command)!;
            foreach (string path in StringPaths(root, ""))
            {
                JsonNode copy = root.DeepClone();
                string[] segments = path.Split('/');
                JsonNode parent = copy;
                foreach (string segment in segments[..^1])
                {
                    parent = parent is JsonArray array ? array[int.Parse(segment)]! : parent[segment]!;
                }

                if (parent is JsonArray list)
                {
                    list[int.Parse(segments[^1])] = new string(' ', 513);
                }
                else
                {
                    parent[segments[^1]] = new string(' ', 513);
                }

                yield return [copy.Deserialize(command.GetType())!];
            }
        }
    }

    private static IEnumerable<string> StringPaths(JsonNode node, string prefix)
    {
        if (node is JsonObject obj)
        {
            foreach ((string key, JsonNode? value) in obj)
            {
                if (value is not null && key != "Type")
                {
                    foreach (string path in StringPaths(value, prefix + key + "/"))
                    {
                        yield return path;
                    }
                }
            }
        }
        else if (node is JsonArray array)
        {
            for (int i = 0; i < array.Count; i++)
            {
                foreach (string path in StringPaths(array[i]!, prefix + i + "/"))
                {
                    yield return path;
                }
            }
        }
        else if (node is JsonValue value && value.TryGetValue(out string? _))
        {
            yield return prefix.TrimEnd('/');
        }
    }

    [Fact]
    public void RepeatedSoftDeactivationDoesNotEraseOrEmitAnotherEvent()
    {
        PartyState state = PartyTestState("p", ErasureStatus.Active);
        state.Apply(new PartyDeactivated());
        string before = JsonSerializer.Serialize(state);
        PartyAggregate.Handle(new DeactivateParty { PartyId = "p" }, state).Events.ShouldBeEmpty();
        PartyAggregate.Handle(new DeactivateParty { PartyId = "p" }, state).Events.ShouldBeEmpty();
        JsonSerializer.Serialize(state).ShouldBe(before);
        state.ErasureStatus.ShouldBe(ErasureStatus.Active);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompositeValidatorsAcceptExactRawStringAndTotalBoundaries(bool updating)
    {
        // Root ID (1), person strings (512 + 512 + 512 + 512), organization strings
        // (512 + 512 + 512 + 511) = 4096 raw UTF-16 units, including all whitespace.
        PersonDetails person = new() { FirstName = new string(' ', 512), LastName = new string('x', 512), Prefix = new string('x', 512), Suffix = new string('x', 512) };
        OrganizationDetails organization = new() { LegalName = new string('x', 512), TradingName = new string('x', 512), LegalForm = new string('x', 512), RegistrationNumber = new string('x', 511) };
        if (updating)
        {
            UpdatePartyComposite command = new() { PartyId = "p", PersonDetails = person, OrganizationDetails = organization };
            new UpdatePartyCompositeValidator().Validate(command).IsValid.ShouldBeTrue();
            new UpdatePartyCompositeValidator().Validate(command with { OrganizationDetails = organization with { RegistrationNumber = new string('x', 512) } }).Errors.ShouldContain(f => f.ErrorCode == "CompositeTotalLength");
        }
        else
        {
            CreatePartyComposite command = new() { PartyId = "p", Type = PartyType.Person, PersonDetails = person, OrganizationDetails = organization };
            new CreatePartyCompositeValidator().Validate(command).IsValid.ShouldBeTrue();
            new CreatePartyCompositeValidator().Validate(command with { OrganizationDetails = organization with { RegistrationNumber = new string('x', 512) } }).Errors.ShouldContain(f => f.ErrorCode == "CompositeTotalLength");
        }
    }

    [Fact]
    public void CompositeValidatorsCountEveryRepeatedIdAndKeepTheOperationCap()
    {
        CreatePartyComposite create = new() { PartyId = "p", Type = PartyType.Person, PersonDetails = new() { FirstName = "A", LastName = "B" },
            Identifiers = Enumerable.Range(0, 99).Select(_ => new AddIdentifier { PartyId = "p", IdentifierId = "i", Type = IdentifierType.Other, Value = "v" }).ToArray() };
        new CreatePartyCompositeValidator().Validate(create).IsValid.ShouldBeTrue();
        UpdatePartyComposite update = new() { PartyId = "p", RemoveIdentifierIds = Enumerable.Repeat("i", 100).ToArray() };
        new UpdatePartyCompositeValidator().Validate(update).IsValid.ShouldBeTrue();
        new UpdatePartyCompositeValidator().Validate(update with { RemoveIdentifierIds = Enumerable.Repeat("i", 101).ToArray() }).IsValid.ShouldBeFalse();
        string repeated = new('p', 128);
        CreatePartyComposite repeatedIds = create with { PartyId = repeated, Identifiers = Enumerable.Range(0, 16).Select(_ => new AddIdentifier { PartyId = repeated, IdentifierId = repeated, Type = IdentifierType.Other, Value = "v" }).ToArray() };
        new CreatePartyCompositeValidator().Validate(repeatedIds).Errors.ShouldContain(f => f.ErrorCode == "CompositeTotalLength");
    }

    private static PartyDomainProcessor CreateInvoker(
        IEventPayloadProtectionService protection,
        IPartyErasureRecordStore? erasureRecordStore = null,
        PartyErasureOrchestrator? erasureOrchestrator = null)
    {
        // Mirror the production registration shape: assembly scan picks up every IValidator<T> in
        // the validators assembly, so adding a new validator does not silently bypass the test
        // fixture (which would otherwise leave new commands without a validator).
        ServiceProvider provider = new ServiceCollection()
            .AddValidatorsFromAssemblyContaining<CreatePartyValidator>()
            .BuildServiceProvider();

        return new PartyDomainProcessor(
            protection,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PartyDomainProcessor>.Instance,
            erasureRecordStore,
            erasureOrchestrator);
    }

    private static PartyErasureOrchestrator CreateOrchestrator(IErasureVerificationService verificationService)
        => new(
            Substitute.For<IPartyKeyManagementService>(),
            verificationService,
            NullLogger<PartyErasureOrchestrator>.Instance);

    private static PartyState PartyTestState(string partyId, ErasureStatus status)
    {
        PartyState state = new();
        state.Apply(new PartyCreated { Type = PartyType.Person });
        if (status is ErasureStatus.Active)
        {
            return state;
        }

        state.Apply(new ErasePartyRequested
        {
            PartyId = partyId,
            TenantId = "tenant-a",
            RequestedAt = DateTimeOffset.Parse("2026-05-21T20:40:00Z"),
            RequestedBy = "admin",
        });
        if (status is ErasureStatus.ErasurePending)
        {
            return state;
        }

        state.Apply(new PartyEncryptionKeyDeleted
        {
            PartyId = partyId,
            TenantId = "tenant-a",
            DeletedAt = DateTimeOffset.Parse("2026-05-21T20:45:00Z"),
        });
        if (status is ErasureStatus.KeyDestroyed or ErasureStatus.VerificationInProgress)
        {
            return state;
        }

        state.Apply(new ErasureVerified
        {
            PartyId = partyId,
            TenantId = "tenant-a",
            VerifiedAt = DateTimeOffset.Parse("2026-05-21T20:50:00Z"),
            VerificationReportId = "report-1",
        });
        if (status is ErasureStatus.Verified)
        {
            return state;
        }

        state.Apply(new PartyErased
        {
            PartyId = partyId,
            TenantId = "tenant-a",
            ErasedAt = DateTimeOffset.Parse("2026-05-21T20:55:00Z"),
        });
        return state;
    }

    private static CommandEnvelope CreateCommand<TCommand>(TCommand payload)
        where TCommand : class
    {
        string partyId = payload switch
        {
            CreateParty command => command.PartyId,
            CreatePartyComposite command => command.PartyId,
            UpdatePartyComposite command => command.PartyId,
            RetryErasureVerification command => command.PartyId,
            MarkPartyEncryptionKeyDeleted command => command.PartyId,
            _ => UniqueIdHelper.GenerateSortableUniqueStringId(),
        };

        return CreateRawCommand(
            commandType: typeof(TCommand).FullName!,
            payload: JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType()),
            aggregateId: PartyIdentifier.IsValid(partyId)
                ? partyId
                : UniqueIdHelper.GenerateSortableUniqueStringId());
    }

    private static CommandEnvelope CreateRawCommand(
        string commandType,
        byte[] payload,
        string? aggregateId = null)
    {
        return new CommandEnvelope(
            MessageId: "01HX00000000000000000000M1",
            TenantId: "tenant-a",
            Domain: "party",
            AggregateId: aggregateId ?? UniqueIdHelper.GenerateSortableUniqueStringId(),
            CommandType: commandType,
            Payload: payload,
            CorrelationId: "01HX00000000000000000000C1",
            CausationId: null,
            UserId: "user-a",
            Extensions: null);
    }

    private static DomainServiceCurrentState CreateCurrentStateWithProtectedSnapshot(CommandEnvelope command)
        => new(
            SnapshotState: new { Kind = "snapshot" },
            Events:
            [
                new EventEnvelope(
                    new EventMetadata(
                        MessageId: "01HX00000000000000000000M1",
                        AggregateId: command.AggregateId,
                        AggregateType: "Party",
                        TenantId: command.TenantId,
                        Domain: command.Domain,
                        SequenceNumber: 1,
                        GlobalPosition: 1,
                        Timestamp: DateTimeOffset.UtcNow,
                        CorrelationId: command.CorrelationId,
                        CausationId: command.CausationId ?? string.Empty,
                        UserId: command.UserId,
                        DomainServiceVersion: "v1",
                        EventTypeName: typeof(PartyCreated).FullName!,
                        MetadataVersion: 1,
                        SerializationFormat: "application/json"),
                    JsonSerializer.SerializeToUtf8Bytes(new PartyCreated { Type = PartyType.Person }),
                    Extensions: null),
            ],
            LastSnapshotSequence: 0,
            CurrentSequence: 1);

    private static DomainServiceCurrentState CreateErasurePendingStateWithProtectedCreatedEvent(CommandEnvelope command, string partyId)
        => new(
            SnapshotState: null,
            Events:
            [
                CreateEventEnvelope(
                    command,
                    sequenceNumber: 1,
                    eventTypeName: typeof(PartyCreated).FullName!,
                    payload: Encoding.UTF8.GetBytes(
                        """
                        {"type":1,"personDetails":{"$enc":true,"kid":"deleted-key","alg":"A256GCM","iv":"redacted","ct":"redacted"}}
                        """),
                    serializationFormat: "json+pdenc-v1"),
                CreateEventEnvelope(
                    command,
                    sequenceNumber: 2,
                    eventTypeName: typeof(ErasePartyRequested).FullName!,
                    payload: JsonSerializer.SerializeToUtf8Bytes(new ErasePartyRequested
                    {
                        PartyId = partyId,
                        TenantId = command.TenantId,
                        RequestedAt = DateTimeOffset.Parse("2026-05-21T20:40:00Z"),
                        RequestedBy = "admin",
                    }),
                    serializationFormat: "json"),
            ],
            LastSnapshotSequence: 0,
            CurrentSequence: 2);

    private static EventEnvelope CreateEventEnvelope(
        CommandEnvelope command,
        long sequenceNumber,
        string eventTypeName,
        byte[] payload,
        string serializationFormat,
        int metadataVersion = 1)
        => new(
            new EventMetadata(
                MessageId: $"01HX0000000000000000000{sequenceNumber:00}",
                AggregateId: command.AggregateId,
                AggregateType: "Party",
                TenantId: command.TenantId,
                Domain: command.Domain,
                SequenceNumber: sequenceNumber,
                GlobalPosition: sequenceNumber,
                Timestamp: DateTimeOffset.UtcNow,
                CorrelationId: command.CorrelationId,
                CausationId: command.CausationId ?? string.Empty,
                UserId: command.UserId,
                DomainServiceVersion: "v1",
                EventTypeName: eventTypeName,
                MetadataVersion: metadataVersion,
                SerializationFormat: serializationFormat),
            payload,
            Extensions: null);
}
