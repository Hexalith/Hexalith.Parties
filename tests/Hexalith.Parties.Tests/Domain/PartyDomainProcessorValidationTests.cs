using System.Text;
using System.Text.Json;

using FluentValidation;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Domain;
using Hexalith.Parties.Security;
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

        DomainResult result = await invoker.ProcessAsync(command, currentState, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        PartyEncryptionKeyDeleted deleted = result.Events.OfType<PartyEncryptionKeyDeleted>().ShouldHaveSingleItem();
        deleted.PartyId.ShouldBe(partyId);
        await protection.Received(1).UnprotectEventPayloadAsync(
            Arg.Any<AggregateIdentity>(),
            typeof(PartyCreated).FullName!,
            Arg.Any<byte[]>(),
            "json+pdenc-v1",
            Arg.Any<CancellationToken>());
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

    [Fact]
    public async Task ProcessAsync_CommandTypeWithNoRegisteredValidator_ProceedsToAggregate()
    {
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainProcessor invoker = new(protection, scopeFactory, NullLogger<PartyDomainProcessor>.Instance);

        CommandEnvelope command = CreateCommand(new CreateParty
        {
            PartyId = Guid.NewGuid().ToString("D"),
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
        });

        DomainResult result = await invoker.ProcessAsync(command, currentState: null, CancellationToken.None);

        // Without a validator the missing-validator branch must allow the command through to the
        // aggregate. The aggregate succeeds for a valid payload, producing PartyCreated.
        result.IsSuccess.ShouldBeTrue();
        result.Events.ShouldContain(e => e is PartyCreated);
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
            VerificationStatus = ErasureVerificationStatus.Failed,
        };
        recordStore.GetCertificateAsync("tenant-a", partyId, Arg.Any<CancellationToken>())
            .Returns(certificate);
        IErasureVerificationService verificationService = Substitute.For<IErasureVerificationService>();
        verificationService.VerifyErasureAsync("tenant-a", partyId, certificate, Arg.Any<CancellationToken>())
            .Returns(new ErasureVerificationReport
            {
                PartyId = partyId,
                TenantId = "tenant-a",
                Timestamp = DateTimeOffset.Parse("2026-05-21T20:50:00Z"),
                OverallStatus = ErasureVerificationOverallStatus.Complete,
                StoreResults =
                [
                    new ErasureVerificationStoreResult
                    {
                        StoreName = "detail-projection",
                        Status = ErasureStoreCleanupStatus.Cleaned,
                        Timestamp = DateTimeOffset.Parse("2026-05-21T20:50:00Z"),
                    },
                ],
            });
        PartyDomainProcessor invoker = CreateInvoker(protection, recordStore, CreateOrchestrator(verificationService));
        PartyState state = PartyTestState(partyId, ErasureStatus.KeyDestroyed);
        CommandEnvelope command = CreateCommand(new RetryErasureVerification { PartyId = partyId, TenantId = "tenant-a" });

        DomainResult result = await invoker.ProcessAsync(command, state, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Events.OfType<ErasureVerified>().ShouldHaveSingleItem();
        result.Events.OfType<PartyErased>().ShouldHaveSingleItem();
        await verificationService.Received(1).VerifyErasureAsync("tenant-a", partyId, certificate, Arg.Any<CancellationToken>());
        await recordStore.Received(1).SaveVerificationReportAsync(
            Arg.Is<ErasureVerificationReport>(report => report != null && report.OverallStatus == ErasureVerificationOverallStatus.Complete),
            Arg.Any<CancellationToken>());
        await recordStore.Received(1).SaveCertificateAsync(
            Arg.Is<ErasureCertificate>(saved => saved != null && saved.VerificationStatus == ErasureVerificationStatus.Verified),
            Arg.Any<CancellationToken>());
        await recordStore.Received(1).SaveStatusAsync(
            Arg.Is<PartyErasureStatusRecord>(saved => saved != null && saved.Status == ErasureStatus.Verified.ToString() && saved.ErrorMessage == null),
            Arg.Any<CancellationToken>());
        await recordStore.Received(1).SaveStatusAsync(
            Arg.Is<PartyErasureStatusRecord>(saved => saved != null && saved.Status == ErasureStatus.Erased.ToString() && saved.ErasedAt == DateTimeOffset.Parse("2026-05-21T20:50:00Z")),
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
        string serializationFormat)
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
                MetadataVersion: 1,
                SerializationFormat: serializationFormat),
            payload,
            Extensions: null);
}
