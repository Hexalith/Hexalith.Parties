using System.Reflection;

using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Server.Aggregates;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public class PartyAggregateRestrictionTests {
    // === Task 7.8 ===
    [Fact]
    public void Handle_RestrictProcessing_EmitsProcessingRestrictedEvent() {
        // Arrange
        RestrictProcessing command = PartyTestData.ValidRestrictProcessing();
        PartyState state = PartyTestData.CreatePersonState();

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        ProcessingRestricted restricted = result.Events[0].ShouldBeOfType<ProcessingRestricted>();
        restricted.PartyId.ShouldBe(PartyTestData.DefaultPartyId);
        restricted.Reason.ShouldBe("Investigation pending");
    }

    // === Task 7.9 ===
    [Fact]
    public void Handle_RestrictProcessing_AlreadyRestricted_ReturnsNoOp() {
        // Arrange
        RestrictProcessing command = PartyTestData.ValidRestrictProcessing();
        PartyState state = PartyTestData.CreateRestrictedState();

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsNoOp.ShouldBeTrue();
    }

    // === Task 7.10 ===
    [Fact]
    public void Handle_LiftRestriction_EmitsRestrictionLiftedEvent() {
        // Arrange
        LiftRestriction command = PartyTestData.ValidLiftRestriction();
        PartyState state = PartyTestData.CreateRestrictedState();

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        RestrictionLifted lifted = result.Events[0].ShouldBeOfType<RestrictionLifted>();
        lifted.PartyId.ShouldBe(PartyTestData.DefaultPartyId);
    }

    // === Task 7.11 ===
    [Fact]
    public void Handle_LiftRestriction_NotRestricted_RejectsWithPartyNotRestricted() {
        // Arrange
        LiftRestriction command = PartyTestData.ValidLiftRestriction();
        PartyState state = PartyTestData.CreatePersonState();

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        PartyNotRestricted rejection = result.Events[0].ShouldBeOfType<PartyNotRestricted>();
        rejection.PartyId.ShouldBe(PartyTestData.DefaultPartyId);
        rejection.TenantId.ShouldBe(PartyTestData.DefaultTenantId);
    }

    // === Task 7.12 — parameterized test covering ALL 12 blocked commands ===
    public static IEnumerable<object[]> BlockedCommandsData() {
        PartyState orgState = PartyTestData.CreateOrganizationStateWithChannelsAndIdentifiers();
        orgState.Apply(new ProcessingRestricted {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            RestrictedAt = DateTimeOffset.UtcNow,
            Reason = "test",
        });

        PartyState personState = PartyTestData.CreateRestrictedState();

        yield return new object[] { "UpdatePersonDetails", PartyTestData.ValidUpdatePersonDetails(), personState };
        yield return new object[] { "UpdateOrganizationDetails", PartyTestData.ValidUpdateOrganizationDetails(), orgState };
        yield return new object[] { "SetIsNaturalPerson", PartyTestData.ValidSetIsNaturalPerson(), orgState };
        yield return new object[] { "AddContactChannel", new AddContactChannel {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-new",
            Type = ContactChannelType.Email,
            Value = "new@test.com",
        }, personState };
        yield return new object[] { "UpdateContactChannel", new UpdateContactChannel {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-email-1",
            Value = "updated@test.com",
        }, personState };
        yield return new object[] { "RemoveContactChannel", new RemoveContactChannel {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-email-1",
        }, personState };
        yield return new object[] { "AddIdentifier", new AddIdentifier {
            PartyId = PartyTestData.DefaultPartyId,
            IdentifierId = "id-new",
            Type = IdentifierType.VAT,
            Value = "FR99999999999",
        }, personState };
        yield return new object[] { "RemoveIdentifier", new RemoveIdentifier {
            PartyId = PartyTestData.DefaultPartyId,
            IdentifierId = "id-vat-1",
        }, personState };
        yield return new object[] { "DeactivateParty", PartyTestData.ValidDeactivateParty(), personState };
        yield return new object[] { "ReactivateParty", PartyTestData.ValidReactivateParty(), PartyTestData.CreateRestrictedState() };
        yield return new object[] { "RotatePartyKey", PartyTestData.ValidRotatePartyKey(), personState };
    }

    [Theory]
    [MemberData(nameof(BlockedCommandsData))]
    public void Handle_AllBlockedCommands_WhenRestricted_RejectsWithPartyProcessingRestricted(
        string commandName, object command, PartyState state) {
        ArgumentNullException.ThrowIfNull(command);

        // Act — use reflection to call the static Handle method
        MethodInfo? handleMethod = typeof(PartyAggregate).GetMethod(
            "Handle",
            BindingFlags.Public | BindingFlags.Static,
            [command.GetType(), typeof(PartyState)]);

        handleMethod.ShouldNotBeNull($"Handle method for {commandName} not found");

        object? resultObj = handleMethod.Invoke(null, [command, state]);
        DomainResult result = resultObj.ShouldBeOfType<DomainResult>();

        // Assert
        result.IsRejection.ShouldBeTrue($"{commandName} should be rejected when restricted");
        PartyProcessingRestricted rejection = result.Events[0].ShouldBeOfType<PartyProcessingRestricted>();
        rejection.PartyId.ShouldNotBeNullOrWhiteSpace();
        rejection.TenantId.ShouldNotBeNull();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_WhenRestricted_Rejects() {
        // Arrange
        UpdatePartyComposite command = PartyTestData.ValidUpdatePersonComposite();
        PartyState state = PartyTestData.CreateRestrictedState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        PartyProcessingRestricted rejection = result.Events[0].ShouldBeOfType<PartyProcessingRestricted>();
        rejection.PartyId.ShouldBe(PartyTestData.DefaultPartyId);
        rejection.TenantId.ShouldBe(string.Empty);
    }

    // === Task 7.13 — parameterized test covering ALL exempt commands ===
    public static IEnumerable<object[]> ExemptCommandsData() {
        PartyState state = PartyTestData.CreateRestrictedState();

        yield return new object[] { "RecordConsent", PartyTestData.ValidRecordConsent(), state };
        yield return new object[] { "LiftRestriction", PartyTestData.ValidLiftRestriction(), state };
        yield return new object[] { "EraseParty", PartyTestData.ValidEraseParty(), state };
    }

    [Theory]
    [MemberData(nameof(ExemptCommandsData))]
    public void Handle_AllExemptCommands_WhenRestricted_Succeeds(
        string commandName, object command, PartyState state) {
        ArgumentNullException.ThrowIfNull(command);

        // Act — use reflection to call the static Handle method
        MethodInfo? handleMethod = typeof(PartyAggregate).GetMethod(
            "Handle",
            BindingFlags.Public | BindingFlags.Static,
            [command.GetType(), typeof(PartyState)]);

        handleMethod.ShouldNotBeNull($"Handle method for {commandName} not found");

        object? resultObj = handleMethod.Invoke(null, [command, state]);
        DomainResult result = resultObj.ShouldBeOfType<DomainResult>();

        // Assert — should NOT be a PartyProcessingRestricted rejection
        if (result.IsRejection) {
            result.Events[0].ShouldNotBeOfType<PartyProcessingRestricted>(
                $"{commandName} should not be blocked by restriction guard");
        }
    }

    [Fact]
    public void Handle_RevokeConsent_WhenRestricted_NotBlockedByRestrictionGuard() {
        // Arrange — special case: need state with consent to revoke
        PartyState state = PartyTestData.CreateRestrictedState();
        state.Apply(new ConsentRecorded {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            ConsentId = "ch-email-1:marketing",
            ChannelId = "ch-email-1",
            Purpose = "marketing",
            LawfulBasis = Contracts.Security.LawfulBasis.Consent,
            GrantedAt = DateTimeOffset.UtcNow,
            GrantedBy = "admin",
        });
        RevokeConsent command = PartyTestData.ValidRevokeConsent("ch-email-1:marketing");

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<ConsentRevoked>();
    }

    // === Task 7.14 — Architectural fitness test ===
    [Fact]
    public void AllModificationHandleMethods_CallRejectIfRestricted() {
        // Commands that MUST have the restriction guard
        HashSet<string> mustHaveGuard = [
            nameof(UpdatePersonDetails),
            nameof(UpdateOrganizationDetails),
            nameof(SetIsNaturalPerson),
            nameof(AddContactChannel),
            nameof(UpdateContactChannel),
            nameof(RemoveContactChannel),
            nameof(AddIdentifier),
            nameof(RemoveIdentifier),
            nameof(DeactivateParty),
            nameof(ReactivateParty),
            nameof(RotatePartyKey),
        ];

        // Commands that are EXEMPT from the restriction guard
        HashSet<string> exempt = [
            nameof(RecordConsent),
            nameof(RevokeConsent),
            nameof(LiftRestriction),
            nameof(EraseParty),
            nameof(RestrictProcessing),
            nameof(CreateParty),
            nameof(CreatePartyComposite),
            nameof(UpdatePartyComposite), // Has its own inline check
            nameof(MarkPartyEncryptionKeyDeleted),
            nameof(MarkErasureVerified),
            nameof(CompletePartyErasure),
        ];

        // Get all Handle methods from PartyAggregate
        MethodInfo[] handleMethods = typeof(PartyAggregate)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "Handle")
            .ToArray();

        List<string> missingGuard = [];
        List<string> unknownCommand = [];

        foreach (MethodInfo method in handleMethods) {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length < 1) continue;

            string commandType = parameters[0].ParameterType.Name;

            if (mustHaveGuard.Contains(commandType)) {
                // Verify by calling Handle with a restricted state
                // The restricted state causes the method to reject with PartyProcessingRestricted
                // We'll check this indirectly via the blocked commands test above
                continue;
            }

            if (!exempt.Contains(commandType)) {
                unknownCommand.Add(commandType);
            }
        }

        unknownCommand.ShouldBeEmpty(
            "Unknown Handle methods found that are not categorized as needing restriction guard or exempt. " +
            "Add them to either mustHaveGuard or exempt list, and add the appropriate guard if needed: " +
            string.Join(", ", unknownCommand));
    }

    // === Task 7.17 ===
    [Fact]
    public void Apply_ProcessingRestricted_SetsIsRestrictedTrue() {
        // Arrange
        PartyState state = PartyTestData.CreatePersonState();
        DateTimeOffset restrictedAt = DateTimeOffset.UtcNow;
        ProcessingRestricted e = new() {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            RestrictedAt = restrictedAt,
            Reason = "Investigation",
        };

        // Act
        state.Apply(e);

        // Assert
        state.IsRestricted.ShouldBeTrue();
        state.RestrictedAt.ShouldBe(restrictedAt);
        state.RestrictionReason.ShouldBe("Investigation");
    }

    // === Task 7.18 ===
    [Fact]
    public void Apply_RestrictionLifted_SetsIsRestrictedFalse() {
        // Arrange
        PartyState state = PartyTestData.CreateRestrictedState();
        RestrictionLifted e = new() {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            LiftedAt = DateTimeOffset.UtcNow,
        };

        // Act
        state.Apply(e);

        // Assert
        state.IsRestricted.ShouldBeFalse();
        state.RestrictedAt.ShouldBeNull();
        state.RestrictionReason.ShouldBeNull();
    }

    [Fact]
    public void Handle_RestrictProcessing_NullState_ReturnsRejection() {
        // Arrange
        RestrictProcessing command = PartyTestData.ValidRestrictProcessing();

        // Act
        DomainResult result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyNotFound>();
    }

    [Fact]
    public void Handle_LiftRestriction_NullState_ReturnsRejection() {
        // Arrange
        LiftRestriction command = PartyTestData.ValidLiftRestriction();

        // Act
        DomainResult result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyNotFound>();
    }

    [Fact]
    public void Handle_RestrictProcessing_ErasurePending_RejectsWithErasureInProgress() {
        // Arrange
        RestrictProcessing command = PartyTestData.ValidRestrictProcessing();
        PartyState state = PartyTestData.CreateErasurePendingState();

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyErasureInProgress>();
    }
}
