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
        Should.Throw<ArgumentNullException>(() => PartyAggregate.Handle((CreateParty)null!, null));
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

    [Fact]
    public void Handle_UpdatePersonDetails_ValidState_ReturnsUpdatedAndDisplayNameEvents()
    {
        UpdatePersonDetails command = new()
        {
            PartyId = Guid.NewGuid().ToString(),
            PersonDetails = new PersonDetails
            {
                FirstName = "John",
                LastName = "Smith",
            },
        };

        PartyState state = new();
        state.Apply(new PartyCreated
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Old", LastName = "Name" },
        });

        var result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
        result.Events[0].ShouldBeOfType<PersonDetailsUpdated>();
        PartyDisplayNameDerived derived = result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        derived.DisplayName.ShouldBe("John Smith");
        derived.SortName.ShouldBe("Smith, John");
    }

    [Fact]
    public void Handle_UpdatePersonDetails_NullPayload_ReturnsRejection()
    {
        UpdatePersonDetails command = new()
        {
            PartyId = Guid.NewGuid().ToString(),
            PersonDetails = null!,
        };

        PartyState state = new();
        state.Apply(new PartyCreated
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "A", LastName = "B" },
        });

        var result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        PartyTypeMismatch rejection = result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
        rejection.Message.ShouldBe("Person details are required.");
    }

    [Fact]
    public void Handle_UpdateOrganizationDetails_NullPayload_ReturnsRejection()
    {
        UpdateOrganizationDetails command = new()
        {
            PartyId = Guid.NewGuid().ToString(),
            OrganizationDetails = null!,
        };

        PartyState state = new();
        state.Apply(new PartyCreated
        {
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails { LegalName = "Legal" },
        });

        var result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        PartyTypeMismatch rejection = result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
        rejection.Message.ShouldBe("Organization details are required.");
    }

    [Fact]
    public void Handle_UpdateOrganizationDetails_OnPerson_ReturnsTypeMismatch()
    {
        UpdateOrganizationDetails command = new()
        {
            PartyId = Guid.NewGuid().ToString(),
            OrganizationDetails = new OrganizationDetails { LegalName = "New Legal" },
        };

        PartyState state = new();
        state.Apply(new PartyCreated
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "A", LastName = "B" },
        });

        var result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
    }

    [Fact]
    public void Handle_DeactivateParty_AlreadyInactive_ReturnsNoOp()
    {
        DeactivateParty command = new()
        {
            PartyId = Guid.NewGuid().ToString(),
        };

        PartyState state = new();
        state.Apply(new PartyCreated
        {
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails { LegalName = "Legal" },
        });
        state.Apply(new PartyDeactivated());

        var result = PartyAggregate.Handle(command, state);

        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Handle_SetIsNaturalPerson_SameValue_ReturnsNoOp()
    {
        SetIsNaturalPerson command = new()
        {
            PartyId = Guid.NewGuid().ToString(),
            IsNaturalPerson = true,
        };

        PartyState state = new();
        state.Apply(new PartyCreated
        {
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails { LegalName = "Legal", IsNaturalPerson = true },
        });

        var result = PartyAggregate.Handle(command, state);

        result.IsNoOp.ShouldBeTrue();
    }
}
