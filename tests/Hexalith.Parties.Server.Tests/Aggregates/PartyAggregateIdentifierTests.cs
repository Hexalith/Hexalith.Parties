using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Server.Aggregates;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public class PartyAggregateIdentifierTests
{
    [Fact]
    public void Handle_AddIdentifier_NullState_ReturnsPartyNotFound()
    {
        AddIdentifier command = PartyTestData.ValidAddVatIdentifier();

        var result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyNotFound>();
    }

    [Fact]
    public void Handle_AddIdentifier_ValidVat_EmitsIdentifierAdded()
    {
        PartyState state = PartyTestData.CreatePersonState();
        AddIdentifier command = PartyTestData.ValidAddVatIdentifier();

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        IdentifierAdded added = result.Events[0].ShouldBeOfType<IdentifierAdded>();
        added.IdentifierId.ShouldBe("id-vat-1");
        added.Type.ShouldBe(IdentifierType.VAT);
        added.Value.ShouldBe("FR12345678901");
    }

    [Fact]
    public void Handle_AddIdentifier_ValidSiret_EmitsIdentifierAdded()
    {
        PartyState state = PartyTestData.CreatePersonState();
        AddIdentifier command = PartyTestData.ValidAddSiretIdentifier();

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        IdentifierAdded added = result.Events[0].ShouldBeOfType<IdentifierAdded>();
        added.IdentifierId.ShouldBe("id-siret-1");
        added.Type.ShouldBe(IdentifierType.SIRET);
        added.Value.ShouldBe("12345678901234");
    }

    [Fact]
    public void Handle_AddIdentifier_ValidNationalId_EmitsIdentifierAdded()
    {
        PartyState state = PartyTestData.CreatePersonState();
        AddIdentifier command = PartyTestData.ValidAddNationalIdIdentifier();

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        IdentifierAdded added = result.Events[0].ShouldBeOfType<IdentifierAdded>();
        added.IdentifierId.ShouldBe("id-natid-1");
        added.Type.ShouldBe(IdentifierType.NationalId);
        added.Value.ShouldBe("850101123456789");
    }

    [Fact]
    public void Handle_AddIdentifier_DuplicateId_ReturnsNoOp()
    {
        PartyState state = PartyTestData.CreatePersonStateWithIdentifier();
        AddIdentifier command = PartyTestData.ValidAddVatIdentifier();

        var result = PartyAggregate.Handle(command, state);

        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Handle_RemoveIdentifier_NullState_ReturnsPartyNotFound()
    {
        RemoveIdentifier command = PartyTestData.ValidRemoveIdentifier();

        var result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyNotFound>();
    }

    [Fact]
    public void Handle_RemoveIdentifier_NotFound_ReturnsIdentifierNotFound()
    {
        PartyState state = PartyTestData.CreatePersonState();
        RemoveIdentifier command = PartyTestData.ValidRemoveIdentifier();

        var result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<IdentifierNotFound>();
    }

    [Fact]
    public void Handle_RemoveIdentifier_Existing_EmitsIdentifierRemoved()
    {
        PartyState state = PartyTestData.CreatePersonStateWithIdentifier();
        RemoveIdentifier command = PartyTestData.ValidRemoveIdentifier();

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        IdentifierRemoved removed = result.Events[0].ShouldBeOfType<IdentifierRemoved>();
        removed.IdentifierId.ShouldBe("id-vat-1");
    }
}
