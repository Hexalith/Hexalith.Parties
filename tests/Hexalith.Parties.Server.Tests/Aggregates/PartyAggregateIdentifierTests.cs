using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Server.Aggregates;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public class PartyAggregateIdentifierTests {

    [Fact]
    public void Handle_AddIdentifier_DuplicateId_ReturnsNoOp() {
        PartyState state = PartyTestData.CreatePersonStateWithIdentifier();
        AddIdentifier command = PartyTestData.ValidAddVatIdentifier();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsNoOp.ShouldBeTrue();
        result.Events.OfType<IdentifierAdded>().ShouldBeEmpty();
        state.Identifiers.Count.ShouldBe(1);
    }

    [Fact]
    public void Handle_AddIdentifier_ErasurePending_ReturnsRejectionWithoutIdentifierValue() {
        PartyState state = PartyTestData.CreateErasurePendingState();
        AddIdentifier command = PartyTestData.ValidAddVatIdentifier();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        PartyErasureInProgress rejection = result.Events[0].ShouldBeOfType<PartyErasureInProgress>();
        result.Events.OfType<IdentifierAdded>().ShouldBeEmpty();
        string rejectionMessage = rejection.Message.ShouldNotBeNull();
        rejectionMessage.ShouldNotContain(command.Value);
    }

    [Fact]
    public void Handle_AddIdentifier_NullState_ReturnsPartyNotFound() {
        AddIdentifier command = PartyTestData.ValidAddVatIdentifier();

        DomainResult result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        _ = result.Events[0].ShouldBeOfType<PartyNotFound>();
    }

    [Fact]
    public void Handle_AddIdentifier_RepeatedDuplicateId_RemainsStable() {
        PartyState state = PartyTestData.CreatePersonStateWithIdentifier();
        AddIdentifier command = PartyTestData.ValidAddVatIdentifier();

        DomainResult firstResult = PartyAggregate.Handle(command, state);
        DomainResult secondResult = PartyAggregate.Handle(command, state);

        firstResult.IsNoOp.ShouldBeTrue();
        secondResult.IsNoOp.ShouldBeTrue();
        firstResult.Events.OfType<IdentifierAdded>().ShouldBeEmpty();
        secondResult.Events.OfType<IdentifierAdded>().ShouldBeEmpty();
        state.Identifiers.Select(x => x.Id).ShouldBe(["id-vat-1"]);
    }

    [Fact]
    public void Handle_AddIdentifier_UnsafeIdentifierId_ReturnsSupportSafeRejection() {
        PartyState state = PartyTestData.CreatePersonState();
        AddIdentifier command = PartyTestData.ValidAddVatIdentifier() with
        {
            IdentifierId = "identifier/unsafe",
        };

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        CompositeOperationConflict rejection = result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        rejection.Message.ShouldBe("Identifier ID is invalid.");
        rejection.Message.ShouldNotBeNull().ShouldNotContain(command.IdentifierId);
        result.Events.OfType<IdentifierAdded>().ShouldBeEmpty();
    }

    [Fact]
    public void Handle_AddIdentifier_RestrictedParty_ReturnsRejectionWithoutIdentifierValue() {
        PartyState state = PartyTestData.CreateRestrictedState();
        AddIdentifier command = PartyTestData.ValidAddVatIdentifier();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        PartyProcessingRestricted rejection = result.Events[0].ShouldBeOfType<PartyProcessingRestricted>();
        result.Events.OfType<IdentifierAdded>().ShouldBeEmpty();
        string rejectionMessage = rejection.Message.ShouldNotBeNull();
        rejectionMessage.ShouldNotContain(command.Value);
    }

    [Fact]
    public void Handle_AddIdentifier_ValidNationalId_EmitsIdentifierAdded() {
        PartyState state = PartyTestData.CreatePersonState();
        AddIdentifier command = PartyTestData.ValidAddNationalIdIdentifier();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        IdentifierAdded added = result.Events[0].ShouldBeOfType<IdentifierAdded>();
        added.IdentifierId.ShouldBe("id-natid-1");
        added.Type.ShouldBe(IdentifierType.NationalId);
        added.Value.ShouldBe("synthetic-national-id-value");
    }

    [Fact]
    public void Handle_AddIdentifier_ValidSiret_EmitsIdentifierAdded() {
        PartyState state = PartyTestData.CreatePersonState();
        AddIdentifier command = PartyTestData.ValidAddSiretIdentifier();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        IdentifierAdded added = result.Events[0].ShouldBeOfType<IdentifierAdded>();
        added.IdentifierId.ShouldBe("id-siret-1");
        added.Type.ShouldBe(IdentifierType.SIRET);
        added.Value.ShouldBe("synthetic-siret-value");
    }

    [Fact]
    public void Handle_AddIdentifier_ValidVat_EmitsIdentifierAdded() {
        PartyState state = PartyTestData.CreatePersonState();
        AddIdentifier command = PartyTestData.ValidAddVatIdentifier();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        IdentifierAdded added = result.Events[0].ShouldBeOfType<IdentifierAdded>();
        added.IdentifierId.ShouldBe("id-vat-1");
        added.Type.ShouldBe(IdentifierType.VAT);
        added.Value.ShouldBe("synthetic-vat-value");
    }

    [Fact]
    public void Handle_RemoveIdentifier_Existing_EmitsIdentifierRemoved() {
        PartyState state = PartyTestData.CreatePersonStateWithIdentifier();
        RemoveIdentifier command = PartyTestData.ValidRemoveIdentifier();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        IdentifierRemoved removed = result.Events[0].ShouldBeOfType<IdentifierRemoved>();
        removed.IdentifierId.ShouldBe("id-vat-1");
    }

    [Fact]
    public void Handle_RemoveIdentifier_ErasurePending_ReturnsRejectionOnly() {
        PartyState state = PartyTestData.CreateErasurePendingState();
        RemoveIdentifier command = PartyTestData.ValidRemoveIdentifier();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        PartyErasureInProgress rejection = result.Events[0].ShouldBeOfType<PartyErasureInProgress>();
        result.Events.OfType<IdentifierRemoved>().ShouldBeEmpty();
        string rejectionMessage = rejection.Message.ShouldNotBeNull();
        rejectionMessage.ShouldNotContain(command.IdentifierId);
    }

    [Fact]
    public void Handle_RemoveIdentifier_NotFound_ReturnsIdentifierNotFound() {
        PartyState state = PartyTestData.CreatePersonState();
        RemoveIdentifier command = PartyTestData.ValidRemoveIdentifier();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        _ = result.Events[0].ShouldBeOfType<IdentifierNotFound>();
        result.Events.OfType<IdentifierRemoved>().ShouldBeEmpty();
    }

    [Fact]
    public void Handle_RemoveIdentifier_UnsafeIdentifierId_ReturnsSupportSafeRejectionBeforeNotFound() {
        PartyState state = PartyTestData.CreatePersonState();
        RemoveIdentifier command = PartyTestData.ValidRemoveIdentifier() with
        {
            IdentifierId = "identifier/unsafe",
        };

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        CompositeOperationConflict rejection = result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        rejection.Message.ShouldBe("Identifier ID is invalid.");
        rejection.Message.ShouldNotBeNull().ShouldNotContain(command.IdentifierId);
        result.Events.OfType<IdentifierNotFound>().ShouldBeEmpty();
        result.Events.OfType<IdentifierRemoved>().ShouldBeEmpty();
    }

    [Fact]
    public void Handle_RemoveIdentifier_NullState_ReturnsPartyNotFound() {
        RemoveIdentifier command = PartyTestData.ValidRemoveIdentifier();

        DomainResult result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        _ = result.Events[0].ShouldBeOfType<PartyNotFound>();
    }

    [Fact]
    public void Handle_RemoveIdentifier_RestrictedParty_ReturnsRejectionOnly() {
        PartyState state = PartyTestData.CreateRestrictedState();
        RemoveIdentifier command = PartyTestData.ValidRemoveIdentifier();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        PartyProcessingRestricted rejection = result.Events[0].ShouldBeOfType<PartyProcessingRestricted>();
        result.Events.OfType<IdentifierRemoved>().ShouldBeEmpty();
        rejection.PartyId.ShouldBe(command.PartyId);
        string rejectionMessage = rejection.Message.ShouldNotBeNull();
        rejectionMessage.ShouldNotContain(command.IdentifierId);
    }
}
