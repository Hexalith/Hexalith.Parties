using System.Text.Json;
using System.Text.Json.Nodes;

using FluentValidation;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Domain;
using Hexalith.Parties.Extensions;
using Hexalith.Parties.Validation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Validation;

/// <summary>
/// Verifies the production validator composition and its command-processing boundary.
/// </summary>
public sealed class PartiesValidatorRegistrationTests
{
    /// <summary>
    /// Pins the complete command inventory and its four legacy aggregate-only exceptions.
    /// </summary>
    [Fact]
    public void AddParties_RegistersValidatorsForAllOtherCommands()
    {
        using ServiceProvider provider = CreateServices().BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        Type[] missingValidators = [.. typeof(CreateParty).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(CreateParty).Namespace)
            .Where(type => scope.ServiceProvider.GetService(typeof(IValidator<>).MakeGenericType(type)) is null)];

        missingValidators.ShouldBe(
            [typeof(MarkPartyEncryptionKeyDeleted), typeof(MarkErasureVerified), typeof(CompletePartyErasure), typeof(RotatePartyKey)],
            ignoreOrder: true);
        scope.ServiceProvider.GetRequiredService<IValidator<RecordConsent>>().ShouldBeOfType<RecordConsentValidator>();
        scope.ServiceProvider.GetRequiredService<IValidator<RevokeConsent>>().ShouldBeOfType<RevokeConsentValidator>();
    }

    /// <summary>
    /// Exercises consent validation through production registration before protected-state access.
    /// </summary>
    /// <param name="revoke">Whether to test revocation rather than recording.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessAsync_ProductionConsentValidators_RejectInvalidPayloads(bool revoke)
    {
        using ServiceProvider provider = CreateServices().BuildServiceProvider();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        object payload = revoke
            ? new RevokeConsent { PartyId = "party-1", TenantId = "tenant-a", ConsentId = "email:marketing", Reason = new string('x', 257) }
            : new RecordConsent { PartyId = "party-1", TenantId = "tenant-a", ChannelId = "email", Purpose = "marketing", LawfulBasis = (LawfulBasis)int.MaxValue };
        CommandEnvelope command = Envelope(payload);

        DomainResult result = await Processor(provider, protection).ProcessAsync(command, ProtectedState(command), TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyCommandValidationRejected>();
        rejection.CommandType.ShouldBe(payload.GetType().FullName);
        PartyValidationFailure failure = rejection.Failures.ShouldHaveSingleItem();
        failure.PropertyName.ShouldBe(revoke ? nameof(RevokeConsent.Reason) : nameof(RecordConsent.LawfulBasis));
        failure.ErrorCode.ShouldBe(revoke ? "MaximumLengthValidator" : "EnumValidator");
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    /// <summary>
    /// Rejects absent production consent validators for each supported command-name representation.
    /// </summary>
    /// <param name="revoke">Whether to test revocation rather than recording.</param>
    /// <param name="representation">The command-name representation.</param>
    [Theory]
    [InlineData(false, "full")]
    [InlineData(false, "short")]
    [InlineData(false, "qualified")]
    [InlineData(true, "full")]
    [InlineData(true, "short")]
    [InlineData(true, "qualified")]
    public async Task ProcessAsync_MissingConsentValidator_RejectsBeforeRehydration(bool revoke, string representation)
    {
        IServiceCollection services = CreateServices();
        services.RemoveAll(revoke ? typeof(IValidator<RevokeConsent>) : typeof(IValidator<RecordConsent>));
        using ServiceProvider provider = services.BuildServiceProvider();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        object payload = ConsentCommand(revoke);
        Type commandType = payload.GetType();
        CommandEnvelope command = Envelope(payload) with
        {
            CommandType = representation switch
            {
                "short" => commandType.Name,
                "qualified" => commandType.AssemblyQualifiedName!,
                _ => commandType.FullName!,
            },
        };

        DomainResult result = await Processor(provider, protection).ProcessAsync(command, ProtectedState(command), TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyCommandValidationRejected>();
        rejection.CommandType.ShouldBe(commandType.FullName);
        PartyValidationFailure failure = rejection.Failures.ShouldHaveSingleItem();
        failure.PropertyName.ShouldBe("CommandType");
        failure.ErrorCode.ShouldBe("ValidatorNotRegistered");
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    /// <summary>
    /// Keeps valid consent commands usable with the production validators.
    /// </summary>
    /// <param name="revoke">Whether to test revocation rather than recording.</param>
    /// <param name="numeric">Whether to send a legacy numeric lawful basis.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ProcessAsync_ProductionConsentValidators_AllowValidPayloads(bool revoke, bool numeric)
    {
        using ServiceProvider provider = CreateServices().BuildServiceProvider();
        PartyState state = ActiveState();
        if (revoke)
        {
            state.Apply(new ConsentRecorded { ConsentId = "email:marketing", PartyId = "party-1", TenantId = "tenant-a", ChannelId = "email", Purpose = "marketing", LawfulBasis = LawfulBasis.Consent, GrantedAt = DateTimeOffset.UnixEpoch, GrantedBy = "test" });
        }

        CommandEnvelope command = Envelope(ConsentCommand(revoke));
        if (!revoke)
        {
            JsonObject payload = JsonNode.Parse(command.Payload)!.AsObject();
            payload["lawfulBasis"]!.GetValue<string>().ShouldBe(nameof(LawfulBasis.LegitimateInterest));
            if (numeric)
            {
                payload["lawfulBasis"] = (int)LawfulBasis.LegitimateInterest;
                command = command with { Payload = JsonSerializer.SerializeToUtf8Bytes(payload) };
            }
        }

        DomainResult result = await Processor(provider, Substitute.For<IEventPayloadProtectionService>())
            .ProcessAsync(command, state, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        if (revoke)
        {
            ConsentRevoked revoked = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConsentRevoked>();
            revoked.PartyId.ShouldBe("party-1");
            revoked.TenantId.ShouldBe("tenant-a");
            revoked.ConsentId.ShouldBe("email:marketing");
            revoked.Reason.ShouldBe("requested");
        }
        else
        {
            ConsentRecorded recorded = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConsentRecorded>();
            recorded.PartyId.ShouldBe("party-1");
            recorded.TenantId.ShouldBe("tenant-a");
            recorded.ConsentId.ShouldBe("email:marketing");
            recorded.ChannelId.ShouldBe("email");
            recorded.Purpose.ShouldBe("marketing");
            recorded.LawfulBasis.ShouldBe(LawfulBasis.LegitimateInterest);
        }
    }

    /// <summary>Rejects unknown lawful-basis names before accessing protected state.</summary>
    [Fact]
    public async Task ProcessAsync_UnknownLawfulBasisName_RejectsBeforeRehydration()
    {
        using ServiceProvider provider = CreateServices().BuildServiceProvider();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        CommandEnvelope command = Envelope(ConsentCommand(false));
        JsonObject payload = JsonNode.Parse(command.Payload)!.AsObject();
        payload["lawfulBasis"] = "unsupported-lawful-basis";
        command = command with { Payload = JsonSerializer.SerializeToUtf8Bytes(payload) };

        DomainResult result = await Processor(provider, protection).ProcessAsync(command, ProtectedState(command), TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyCommandValidationRejected>();
        PartyValidationFailure failure = rejection.Failures.ShouldHaveSingleItem();
        failure.PropertyName.ShouldBe("Payload");
        failure.ErrorCode.ShouldBe("InvalidJson");
        JsonSerializer.Serialize(rejection).ShouldNotContain("unsupported-lawful-basis");
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    /// <summary>Historical numeric party and composite commands still emit the submitted values.</summary>
    /// <param name="composite">Whether to include nested contact-channel and identifier commands.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessAsync_HistoricalNumericCreation_EmitsExpectedEnums(bool composite)
    {
        using ServiceProvider provider = CreateServices().BuildServiceProvider();
        object payload = composite ? CreationComposite() : new CreateParty
        {
            PartyId = "party-1", Type = PartyType.Organization, OrganizationDetails = new OrganizationDetails { LegalName = "Test Company" },
        };
        CommandEnvelope command = Envelope(payload);
        JsonObject body = JsonNode.Parse(command.Payload)!.AsObject();
        body["type"] = 2;
        if (composite)
        {
            body["contactChannels"]![0]!["type"] = 1;
            body["identifiers"]![0]!["type"] = 4;
        }

        command = command with { Payload = JsonSerializer.SerializeToUtf8Bytes(body) };

        DomainResult result = await Processor(provider, Substitute.For<IEventPayloadProtectionService>())
            .ProcessAsync(command, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Events.OfType<PartyCreated>().ShouldHaveSingleItem().Type.ShouldBe(PartyType.Organization);
        if (composite)
        {
            result.Events.OfType<ContactChannelAdded>().ShouldHaveSingleItem().Type.ShouldBe(ContactChannelType.Phone);
            result.Events.OfType<IdentifierAdded>().ShouldHaveSingleItem().Type.ShouldBe(IdentifierType.TaxId);
        }
    }

    /// <summary>Malformed composite child enum names reject before protected-state access.</summary>
    /// <param name="childCollection">The nested command collection containing the malformed enum.</param>
    [Theory]
    [InlineData("contactChannels")]
    [InlineData("identifiers")]
    public async Task ProcessAsync_UnknownNestedEnumName_RejectsBeforeRehydration(string childCollection)
    {
        using ServiceProvider provider = CreateServices().BuildServiceProvider();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        CommandEnvelope command = Envelope(CreationComposite());
        JsonObject payload = JsonNode.Parse(command.Payload)!.AsObject();
        payload[childCollection]![0]!["type"] = "unsupported-nested-enum";
        command = command with { Payload = JsonSerializer.SerializeToUtf8Bytes(payload) };

        DomainResult result = await Processor(provider, protection).ProcessAsync(command, ProtectedState(command), TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        PartyCommandValidationRejected rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyCommandValidationRejected>();
        PartyValidationFailure failure = rejection.Failures.ShouldHaveSingleItem();
        failure.PropertyName.ShouldBe("Payload");
        failure.ErrorCode.ShouldBe("InvalidJson");
        JsonSerializer.Serialize(rejection).ShouldNotContain("unsupported-nested-enum");
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    /// <summary>Undefined numeric inputs retain the existing command validation rules.</summary>
    /// <param name="commandName">The command whose enum membership behavior is characterized.</param>
    [Theory]
    [InlineData(nameof(CreateParty))]
    [InlineData(nameof(CreatePartyComposite))]
    [InlineData(nameof(AddContactChannel))]
    [InlineData(nameof(UpdateContactChannel))]
    [InlineData(nameof(AddIdentifier))]
    [InlineData(nameof(RecordConsent))]
    public async Task ProcessAsync_UndefinedNumericEnum_PreservesExistingValidation(string commandName)
    {
        using ServiceProvider provider = CreateServices().BuildServiceProvider();
        object payload = commandName switch
        {
            nameof(CreateParty) => new CreateParty { PartyId = "party-1", Type = (PartyType)int.MaxValue },
            nameof(CreatePartyComposite) => CreationComposite() with { Type = (PartyType)int.MaxValue },
            nameof(AddContactChannel) => new AddContactChannel { PartyId = "party-1", ContactChannelId = "phone", Type = (ContactChannelType)int.MaxValue, Value = "555" },
            nameof(UpdateContactChannel) => new UpdateContactChannel { PartyId = "party-1", ContactChannelId = "email", Type = (ContactChannelType)int.MaxValue },
            nameof(AddIdentifier) => new AddIdentifier { PartyId = "party-1", IdentifierId = "tax", Type = (IdentifierType)int.MaxValue, Value = "123" },
            nameof(RecordConsent) => new RecordConsent { PartyId = "party-1", TenantId = "tenant-a", ChannelId = "email", Purpose = "billing", LawfulBasis = (LawfulBasis)int.MaxValue },
            _ => throw new ArgumentOutOfRangeException(nameof(commandName)),
        };
        CommandEnvelope command = Envelope(payload);
        using JsonDocument body = JsonDocument.Parse(command.Payload);
        body.RootElement.GetProperty(commandName == nameof(RecordConsent) ? "lawfulBasis" : "type").GetInt32().ShouldBe(int.MaxValue);

        DomainResult result = await Processor(provider, Substitute.For<IEventPayloadProtectionService>())
            .ProcessAsync(command, ActiveState(), TestContext.Current.CancellationToken);

        if (commandName == nameof(UpdateContactChannel))
        {
            // This existing validator has no enum-membership rule; DW-195 preserves that behavior.
            result.IsSuccess.ShouldBeTrue();
            result.Events.OfType<ContactChannelUpdated>().ShouldHaveSingleItem().Type.ShouldBe((ContactChannelType)int.MaxValue);
        }
        else
        {
            result.IsRejection.ShouldBeTrue();
            result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyCommandValidationRejected>()
                .Failures.ShouldContain(failure => failure.ErrorCode == "EnumValidator");
        }
    }

    /// <summary>
    /// Preserves successful processing for the four existing validator-free commands.
    /// </summary>
    /// <param name="commandName">The legacy command to process.</param>
    [Theory]
    [InlineData(nameof(MarkPartyEncryptionKeyDeleted))]
    [InlineData(nameof(MarkErasureVerified))]
    [InlineData(nameof(CompletePartyErasure))]
    [InlineData(nameof(RotatePartyKey))]
    public async Task ProcessAsync_LegacyAggregateOnlyCommands_KeepExistingBehavior(string commandName)
    {
        using ServiceProvider provider = CreateServices().BuildServiceProvider();
        PartyState state = ActiveState();
        if (commandName != nameof(RotatePartyKey))
        {
            state.Apply(new ErasePartyRequested { PartyId = "party-1", TenantId = "tenant-a", RequestedAt = DateTimeOffset.UnixEpoch, RequestedBy = "test" });
        }

        if (commandName is nameof(MarkErasureVerified) or nameof(CompletePartyErasure))
        {
            state.Apply(new PartyEncryptionKeyDeleted { PartyId = "party-1", TenantId = "tenant-a", DeletedAt = DateTimeOffset.UnixEpoch });
        }

        if (commandName == nameof(CompletePartyErasure))
        {
            state.Apply(new ErasureVerified { PartyId = "party-1", TenantId = "tenant-a", VerifiedAt = DateTimeOffset.UnixEpoch, VerificationReportId = "report-1" });
        }

        (object Payload, Type EventType) scenario = LegacyScenario(commandName);

        DomainResult result = await Processor(provider, Substitute.For<IEventPayloadProtectionService>())
            .ProcessAsync(Envelope(scenario.Payload), state, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().GetType().ShouldBe(scenario.EventType);
    }

    /// <summary>
    /// Honors a host-provided validator even for a legacy aggregate-only command.
    /// </summary>
    /// <param name="commandName">The legacy command with a supplied validator.</param>
    [Theory]
    [InlineData(nameof(MarkPartyEncryptionKeyDeleted))]
    [InlineData(nameof(MarkErasureVerified))]
    [InlineData(nameof(CompletePartyErasure))]
    [InlineData(nameof(RotatePartyKey))]
    public async Task ProcessAsync_LegacyCommandWithRegisteredValidator_UsesValidator(string commandName)
    {
        IServiceCollection services = CreateServices();
        AddRejectingValidator<MarkPartyEncryptionKeyDeleted>(services);
        AddRejectingValidator<MarkErasureVerified>(services);
        AddRejectingValidator<CompletePartyErasure>(services);
        AddRejectingValidator<RotatePartyKey>(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        CommandEnvelope command = Envelope(LegacyScenario(commandName).Payload);

        DomainResult result = await Processor(provider, protection).ProcessAsync(command, ProtectedState(command), TestContext.Current.CancellationToken);

        result.IsRejection.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyCommandValidationRejected>()
            .Failures.ShouldHaveSingleItem().ErrorCode.ShouldBe("SuppliedValidator");
        await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
        await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
    }

    /// <summary>
    /// Rejects every other known command when its production validator registration is removed.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_AnyOtherCommandWithoutValidator_RejectsBeforeRehydration()
    {
        foreach (Type commandType in typeof(CreateParty).Assembly.GetTypes().Where(type => type.Namespace == typeof(CreateParty).Namespace
            && type != typeof(MarkPartyEncryptionKeyDeleted) && type != typeof(MarkErasureVerified)
            && type != typeof(CompletePartyErasure) && type != typeof(RotatePartyKey)))
        {
            IServiceCollection services = CreateServices();
            services.RemoveAll(typeof(IValidator<>).MakeGenericType(commandType));
            using ServiceProvider provider = services.BuildServiceProvider();
            IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
            CommandEnvelope command = Envelope(new { }) with { CommandType = commandType.FullName! };

            DomainResult result = await Processor(provider, protection).ProcessAsync(command, ProtectedState(command), TestContext.Current.CancellationToken);

            result.IsRejection.ShouldBeTrue();
            PartyCommandValidationRejected rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<PartyCommandValidationRejected>();
            rejection.CommandType.ShouldBe(commandType.FullName);
            PartyValidationFailure failure = rejection.Failures.ShouldHaveSingleItem();
            failure.PropertyName.ShouldBe("CommandType");
            failure.ErrorCode.ShouldBe("ValidatorNotRegistered");
            await protection.DidNotReceiveWithAnyArgs().UnprotectSnapshotStateAsync(default!, default!, default);
            await protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
        }
    }

    private static void AddRejectingValidator<TCommand>(IServiceCollection services)
    {
        InlineValidator<TCommand> validator = new();
        validator.RuleFor(command => command).Must(_ => false).WithErrorCode("SuppliedValidator");
        services.AddScoped<IValidator<TCommand>>(_ => validator);
    }

    private static (object Payload, Type EventType) LegacyScenario(string commandName)
        => commandName switch
        {
            nameof(MarkPartyEncryptionKeyDeleted) => (new MarkPartyEncryptionKeyDeleted { PartyId = "party-1", TenantId = "tenant-a", DeletedAt = DateTimeOffset.UnixEpoch }, typeof(PartyEncryptionKeyDeleted)),
            nameof(MarkErasureVerified) => (new MarkErasureVerified { PartyId = "party-1", TenantId = "tenant-a", VerifiedAt = DateTimeOffset.UnixEpoch, VerificationReportId = "report-1" }, typeof(ErasureVerified)),
            nameof(CompletePartyErasure) => (new CompletePartyErasure { PartyId = "party-1", TenantId = "tenant-a", ErasedAt = DateTimeOffset.UnixEpoch }, typeof(PartyErased)),
            nameof(RotatePartyKey) => (new RotatePartyKey { PartyId = "party-1", NewKeyVersion = 2, PreviousKeyVersion = 1 }, typeof(PartyEncryptionKeyRotated)),
            _ => throw new ArgumentException("Unknown legacy command.", nameof(commandName)),
        };

    private static IServiceCollection CreateServices()
        => new ServiceCollection().AddLogging().AddParties(new ConfigurationBuilder().Build());

    private static PartyDomainProcessor Processor(ServiceProvider provider, IEventPayloadProtectionService protection)
        => new(protection, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<PartyDomainProcessor>.Instance);

    private static object ConsentCommand(bool revoke)
        => revoke
            ? new RevokeConsent { PartyId = "party-1", TenantId = "tenant-a", ConsentId = "email:marketing", Reason = "requested" }
            : new RecordConsent { PartyId = "party-1", TenantId = "tenant-a", ChannelId = "email", Purpose = "marketing", LawfulBasis = LawfulBasis.LegitimateInterest };

    private static PartyState ActiveState()
    {
        PartyState state = new();
        state.Apply(new PartyCreated { Type = PartyType.Person });
        state.Apply(new ContactChannelAdded { ContactChannelId = "email", Type = ContactChannelType.Email, Value = "test@example.invalid" });
        return state;
    }

    private static CreatePartyComposite CreationComposite()
        => new()
        {
            PartyId = "party-1", Type = PartyType.Organization, OrganizationDetails = new OrganizationDetails { LegalName = "Test Company" },
            ContactChannels = [new AddContactChannel { PartyId = "party-1", ContactChannelId = "phone", Type = ContactChannelType.Phone, Value = "555" }],
            Identifiers = [new AddIdentifier { PartyId = "party-1", IdentifierId = "tax", Type = IdentifierType.TaxId, Value = "123" }],
        };

    // Match the canonical enum names sent by the production Parties clients.
    private static CommandEnvelope Envelope(object payload)
        => new("01HX00000000000000000000M1", "tenant-a", "party", "party-1", payload.GetType().FullName!,
            JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), PartiesJsonOptions.Default),
            "01HX00000000000000000000C1", null, "user-a", null);

    private static DomainServiceCurrentState ProtectedState(CommandEnvelope command)
        => new(new { Kind = "protected-snapshot" },
            [new EventEnvelope(new EventMetadata(command.MessageId, command.AggregateId, "Party", command.TenantId, command.Domain,
                2, 2, DateTimeOffset.UnixEpoch, command.CorrelationId, string.Empty, command.UserId, "v1", typeof(PartyCreated).FullName!, 1, "json+pdenc-v1"),
                "{}"u8.ToArray(), null)], 1, 2);
}
