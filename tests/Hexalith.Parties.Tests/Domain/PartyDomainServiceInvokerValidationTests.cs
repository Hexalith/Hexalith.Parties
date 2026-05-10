using System.Text.Json;

using FluentValidation;
using FluentValidation.Results;

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
    public async Task InvokeAsync_InvalidCreatePartyPayload_ThrowsValidationExceptionBeforeDomainProcessing()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainServiceInvoker invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new CreateParty
        {
            PartyId = "not-a-guid",
            Type = PartyType.Person,
            PersonDetails = null,
        });

        ValidationException exception = await Should.ThrowAsync<ValidationException>(
            () => invoker.InvokeAsync(command, currentState: null, CancellationToken.None));

        exception.Errors.Select(e => e.PropertyName).ShouldContain("PartyId");
        exception.Errors.Select(e => e.PropertyName).ShouldContain("PersonDetails");
        await protection
            .DidNotReceiveWithAnyArgs()
            .UnprotectSnapshotStateAsync(default!, default!, default);
        await protection
            .DidNotReceiveWithAnyArgs()
            .UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task InvokeAsync_InvalidPayloadWithProtectedCurrentState_DoesNotUnprotectStateOrEvents()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        PartyDomainServiceInvoker invoker = CreateInvoker(protection);
        CommandEnvelope command = CreateCommand(new CreatePartyComposite
        {
            PartyId = "not-a-guid",
            Type = PartyType.Organization,
            OrganizationDetails = null,
        });
        DomainServiceCurrentState currentState = new(
            SnapshotState: new { Kind = "snapshot" },
            Events:
            [
                new EventEnvelope(
                    new EventMetadata(
                        MessageId: "01HX0000000000000000000000",
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

        await Should.ThrowAsync<ValidationException>(
            () => invoker.InvokeAsync(command, currentState, CancellationToken.None));

        await protection
            .DidNotReceiveWithAnyArgs()
            .UnprotectSnapshotStateAsync(default!, default!, default);
        await protection
            .DidNotReceiveWithAnyArgs()
            .UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
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

    private static PartyDomainServiceInvoker CreateInvoker(IEventPayloadProtectionService protection)
    {
        ServiceProvider validators = new ServiceCollection()
            .AddSingleton<IValidator<CreateParty>, CreatePartyValidator>()
            .AddSingleton<IValidator<CreatePartyComposite>, CreatePartyCompositeValidator>()
            .AddSingleton<IValidator<UpdatePartyComposite>, UpdatePartyCompositeValidator>()
            .BuildServiceProvider();

        return new PartyDomainServiceInvoker(
            protection,
            validators,
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

        return new CommandEnvelope(
            MessageId: "01HX0000000000000000000001",
            TenantId: "tenant-a",
            Domain: "party",
            AggregateId: partyId,
            CommandType: typeof(TCommand).FullName!,
            Payload: JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType()),
            CorrelationId: "01HX0000000000000000000002",
            CausationId: null,
            UserId: "user-a",
            Extensions: null);
    }
}
