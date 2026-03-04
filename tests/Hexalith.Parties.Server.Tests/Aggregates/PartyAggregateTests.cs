using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Server.Aggregates;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public class PartyAggregateTests
{
    [Fact]
    public void Handle_CreatePartyPerson_ReturnsCreatedAndDisplayNameEvents()
    {
        CreateParty command = new()
        {
            PartyId = Guid.NewGuid().ToString(),
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Jane",
                LastName = "Doe",
            },
        };

        var result = PartyAggregate.Handle(command, null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);

        PartyCreated created = result.Events[0].ShouldBeOfType<PartyCreated>();
        created.Type.ShouldBe(PartyType.Person);
        created.PersonDetails.ShouldNotBeNull();
        created.PersonDetails.FirstName.ShouldBe("Jane");
        created.PersonDetails.LastName.ShouldBe("Doe");

        PartyDisplayNameDerived nameDerived = result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        nameDerived.DisplayName.ShouldBe("Jane Doe");
        nameDerived.SortName.ShouldBe("Doe, Jane");
    }

    [Fact]
    public void Handle_CreatePartyOrganization_UsesLegalNameForDisplayAndSort()
    {
        CreateParty command = new()
        {
            PartyId = Guid.NewGuid().ToString(),
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails
            {
                LegalName = "Acme Legal",
                TradingName = "Acme Trading",
            },
        };

        var result = PartyAggregate.Handle(command, null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);

        PartyCreated created = result.Events[0].ShouldBeOfType<PartyCreated>();
        created.Type.ShouldBe(PartyType.Organization);
        created.OrganizationDetails.ShouldNotBeNull();
        created.OrganizationDetails.LegalName.ShouldBe("Acme Legal");

        PartyDisplayNameDerived nameDerived = result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        nameDerived.DisplayName.ShouldBe("Acme Legal");
        nameDerived.SortName.ShouldBe("Acme Legal");
    }

    [Fact]
    public void Handle_CreatePartyOrganization_NullTradingName_UsesLegalName()
    {
        CreateParty command = new()
        {
            PartyId = Guid.NewGuid().ToString(),
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails
            {
                LegalName = "Solo Legal Name",
            },
        };

        var result = PartyAggregate.Handle(command, null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);

        PartyDisplayNameDerived nameDerived = result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        nameDerived.DisplayName.ShouldBe("Solo Legal Name");
        nameDerived.SortName.ShouldBe("Solo Legal Name");
    }

    [Fact]
    public void Handle_ExistingState_ReturnsNoOp()
    {
        CreateParty command = new()
        {
            PartyId = Guid.NewGuid().ToString(),
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "A", LastName = "B" },
        };

        var result = PartyAggregate.Handle(command, new PartyState());

        result.IsNoOp.ShouldBeTrue();
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Handle_DefaultType_ReturnsTypeRejection()
    {
        CreateParty command = new()
        {
            PartyId = Guid.NewGuid().ToString(),
            Type = PartyType.Unknown,
        };

        var result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyCannotBeCreatedWithoutType>();
    }

    [Fact]
    public void Handle_PersonWithoutDetails_ReturnsPersonDetailsRejection()
    {
        CreateParty command = new()
        {
            PartyId = Guid.NewGuid().ToString(),
            Type = PartyType.Person,
        };

        var result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyCannotBeCreatedWithoutPersonDetails>();
    }

    [Fact]
    public void Handle_OrganizationWithoutDetails_ReturnsOrganizationDetailsRejection()
    {
        CreateParty command = new()
        {
            PartyId = Guid.NewGuid().ToString(),
            Type = PartyType.Organization,
        };

        var result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyCannotBeCreatedWithoutOrganizationDetails>();
    }

    [Fact]
    public void Handle_NullCommand_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => PartyAggregate.Handle(null!, null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    public void Handle_InvalidPartyId_ReturnsInvalidIdRejection(string partyId)
    {
        CreateParty command = new()
        {
            PartyId = partyId,
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "A", LastName = "B" },
        };

        var result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyCannotBeCreatedWithInvalidId>();
    }
}
