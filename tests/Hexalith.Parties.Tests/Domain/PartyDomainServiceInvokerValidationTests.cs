using System.Text.Json;

using FluentValidation;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Domain;
using Hexalith.Parties.Validation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Domain;

public sealed class PartyDomainServiceInvokerValidationTests
{
    [Fact]
    public async Task InvokeAsync_InvalidCreatePartyPayload_ReturnsRejectionResult()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainServiceInvoker invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new CreateParty
        {
            PartyId = "not-a-guid",
            Type = PartyType.Person,
            PersonDetails = null,
        });
        DomainServiceCurrentState currentState = CreateCurrentStateWithProtectedSnapshot(command);

        DomainResult result = await invoker.InvokeAsync(command, currentState, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events
            .OfType<PartyCommandValidationRejected>()
            .ShouldHaveSingleItem();
        rejection.CommandType.ShouldBe(typeof(CreateParty).FullName);
        rejection.Failures.Select(f => f.PropertyName).ShouldContain("PartyId");
        rejection.Failures.Select(f => f.PropertyName).ShouldContain("PersonDetails");

        // currentState carries a non-null snapshot AND a protected event. If validation had not
        // short-circuited before unprotect, both methods would have been invoked. The non-null
        // snapshot is what makes this assertion meaningful — passing null would render it vacuous.
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task InvokeAsync_InvalidCompositePayload_DoesNotProduceStateChangingEvents()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainServiceInvoker invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new CreatePartyComposite
        {
            PartyId = "not-a-guid",
            Type = PartyType.Organization,
            OrganizationDetails = null,
        });
        DomainServiceCurrentState currentState = CreateCurrentStateWithProtectedSnapshot(command);

        DomainResult result = await invoker.InvokeAsync(command, currentState, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        result.Events.ShouldAllBe(e => e is PartyCommandValidationRejected);
        result.Events.OfType<PartyCreated>().ShouldBeEmpty();
        result.Events.OfType<PartyDisplayNameDerived>().ShouldBeEmpty();

        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task InvokeAsync_InvalidErasePartyPayload_ReturnsRejectionResultWithoutRehydration()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainServiceInvoker invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new EraseParty
        {
            PartyId = string.Empty,
            TenantId = string.Empty,
        });
        DomainServiceCurrentState currentState = CreateCurrentStateWithProtectedSnapshot(command);

        DomainResult result = await invoker.InvokeAsync(command, currentState, CancellationToken.None);

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
    public async Task InvokeAsync_ValidCreatePartyPayload_ProducesDomainEvents()
    {
        PartyDomainServiceInvoker invoker = CreateInvoker(Substitute.For<IEventPayloadProtectionService>());
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

        DomainResult result = await invoker.InvokeAsync(command, currentState: null, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Events.ShouldContain(e => e is PartyCreated);
        result.Events.ShouldContain(e => e is PartyDisplayNameDerived);
    }

    [Fact]
    public async Task InvokeAsync_UnknownCommandType_ReturnsUnresolvedCommandTypeRejection()
    {
        PartyDomainServiceInvoker invoker = CreateInvoker(Substitute.For<IEventPayloadProtectionService>());
        CommandEnvelope command = CreateRawCommand(
            commandType: "Hexalith.Parties.Contracts.Commands.NoSuchCommand, Hexalith.Parties.Contracts",
            payload: JsonSerializer.SerializeToUtf8Bytes(new { foo = "bar" }));

        DomainResult result = await invoker.InvokeAsync(command, currentState: null, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events
            .OfType<PartyCommandValidationRejected>()
            .ShouldHaveSingleItem();
        rejection.Failures.ShouldHaveSingleItem();
        rejection.Failures[0].ErrorCode.ShouldBe("UnresolvedCommandType");
    }

    [Fact]
    public async Task InvokeAsync_AssemblyQualifiedCommandType_ResolvesAndValidates()
    {
        PartyDomainServiceInvoker invoker = CreateInvoker(Substitute.For<IEventPayloadProtectionService>());
        CommandEnvelope command = CreateRawCommand(
            commandType: typeof(CreateParty).AssemblyQualifiedName!,
            payload: JsonSerializer.SerializeToUtf8Bytes(new CreateParty
            {
                PartyId = "not-a-guid",
                Type = PartyType.Person,
                PersonDetails = null,
            }));

        DomainResult result = await invoker.InvokeAsync(command, currentState: null, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.OfType<PartyCommandValidationRejected>().ShouldHaveSingleItem();
        rejection.Failures.Select(f => f.PropertyName).ShouldContain("PartyId");
    }

    [Fact]
    public async Task InvokeAsync_MalformedJsonPayload_ReturnsInvalidJsonRejection()
    {
        PartyDomainServiceInvoker invoker = CreateInvoker(Substitute.For<IEventPayloadProtectionService>());
        CommandEnvelope command = CreateRawCommand(
            commandType: typeof(CreateParty).FullName!,
            payload: "{ this is not valid json"u8.ToArray());

        DomainResult result = await invoker.InvokeAsync(command, currentState: null, CancellationToken.None);

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
    public async Task InvokeAsync_EmptyPayload_ReturnsEmptyPayloadRejection()
    {
        PartyDomainServiceInvoker invoker = CreateInvoker(Substitute.For<IEventPayloadProtectionService>());
        CommandEnvelope command = CreateRawCommand(
            commandType: typeof(CreateParty).FullName!,
            payload: []);

        DomainResult result = await invoker.InvokeAsync(command, currentState: null, CancellationToken.None);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.OfType<PartyCommandValidationRejected>().ShouldHaveSingleItem();
        rejection.Failures[0].ErrorCode.ShouldBe("EmptyPayload");
    }

    [Fact]
    public async Task InvokeAsync_CommandTypeWithNoRegisteredValidator_ProceedsToAggregate()
    {
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainServiceInvoker invoker = new(protection, scopeFactory, NullLogger<PartyDomainServiceInvoker>.Instance);

        CommandEnvelope command = CreateCommand(new CreateParty
        {
            PartyId = Guid.NewGuid().ToString("D"),
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
        });

        DomainResult result = await invoker.InvokeAsync(command, currentState: null, CancellationToken.None);

        // Without a validator the missing-validator branch must allow the command through to the
        // aggregate. The aggregate succeeds for a valid payload, producing PartyCreated.
        result.IsSuccess.ShouldBeTrue();
        result.Events.ShouldContain(e => e is PartyCreated);
    }

    private static PartyDomainServiceInvoker CreateInvoker(IEventPayloadProtectionService protection)
    {
        // Mirror the production registration shape: assembly scan picks up every IValidator<T> in
        // the validators assembly, so adding a new validator does not silently bypass the test
        // fixture (which would otherwise leave new commands without a validator).
        ServiceProvider provider = new ServiceCollection()
            .AddValidatorsFromAssemblyContaining<CreatePartyValidator>()
            .BuildServiceProvider();

        return new PartyDomainServiceInvoker(
            protection,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PartyDomainServiceInvoker>.Instance);
    }

    private static CommandEnvelope CreateCommand<TCommand>(TCommand payload)
        where TCommand : class
    {
        string partyId = payload switch
        {
            CreateParty command => command.PartyId,
            CreatePartyComposite command => command.PartyId,
            UpdatePartyComposite command => command.PartyId,
            _ => Guid.NewGuid().ToString("D"),
        };

        if (!Guid.TryParse(partyId, out _))
        {
            partyId = Guid.NewGuid().ToString("D");
        }

        return CreateRawCommand(
            commandType: typeof(TCommand).FullName!,
            payload: JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType()),
            aggregateId: partyId);
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
            AggregateId: aggregateId ?? Guid.NewGuid().ToString("D"),
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
}
