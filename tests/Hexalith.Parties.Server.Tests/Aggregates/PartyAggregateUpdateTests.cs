using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Server.Aggregates;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public class PartyAggregateUpdateTests
{
    // --- UpdatePersonDetails ---

    [Fact]
    public void Handle_UpdatePersonDetails_ValidState_EmitsUpdatedAndDisplayNameDerived()
    {
        // Arrange
        UpdatePersonDetails command = PartyTestData.ValidUpdatePersonDetails();
        PartyState state = PartyTestData.CreatePersonState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
        PersonDetailsUpdated updated = result.Events[0].ShouldBeOfType<PersonDetailsUpdated>();
        updated.PersonDetails.ShouldNotBeNull();
        updated.PersonDetails.FirstName.ShouldBe(command.PersonDetails.FirstName);
        updated.PersonDetails.LastName.ShouldBe(command.PersonDetails.LastName);
        result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
    }

    [Fact]
    public void Handle_UpdatePersonDetails_NullState_ReturnsRejection()
    {
        // Arrange
        UpdatePersonDetails command = PartyTestData.ValidUpdatePersonDetails();

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
    }

    [Fact]
    public void Handle_UpdatePersonDetails_OnOrganization_ReturnsTypeMismatch()
    {
        // Arrange
        UpdatePersonDetails command = PartyTestData.ValidUpdatePersonDetails();
        PartyState state = PartyTestData.CreateOrganizationState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
    }

    [Fact]
    public void Handle_UpdatePersonDetails_NullPayload_ReturnsRejection()
    {
        // Arrange
        UpdatePersonDetails command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            PersonDetails = null!,
        };
        PartyState state = PartyTestData.CreatePersonState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        PartyTypeMismatch rejection = result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
        rejection.Message.ShouldBe("Person details are required.");
    }

    [Fact]
    public void Handle_UpdatePersonDetails_NullCommand_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => PartyAggregate.Handle((UpdatePersonDetails)null!, null));
    }

    [Fact]
    public void Handle_UpdatePersonDetails_DisplayNameReDerived_CorrectFormat()
    {
        // Arrange
        UpdatePersonDetails command = PartyTestData.ValidUpdatePersonDetails();
        PartyState state = PartyTestData.CreatePersonState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        PartyDisplayNameDerived nameDerived = result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        nameDerived.DisplayName.ShouldBe("Jane Smith");
        nameDerived.SortName.ShouldBe("Smith, Jane");
    }

    [Fact]
    public void Handle_UpdatePersonDetails_ApplyEvents_StateReflectsNewDetails()
    {
        // Arrange
        UpdatePersonDetails command = PartyTestData.ValidUpdatePersonDetails();
        PartyState state = PartyTestData.CreatePersonState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Apply events
        foreach (object evt in result.Events)
        {
            switch (evt)
            {
                case PersonDetailsUpdated updated:
                    state.Apply(updated);
                    break;
                case PartyDisplayNameDerived nameDerived:
                    state.Apply(nameDerived);
                    break;
            }
        }

        // Assert
        state.Person.ShouldNotBeNull();
        state.Person.FirstName.ShouldBe("Jane");
        state.Person.LastName.ShouldBe("Smith");
        state.DisplayName.ShouldBe("Jane Smith");
        state.SortName.ShouldBe("Smith, Jane");
    }

    // --- UpdateOrganizationDetails ---

    [Fact]
    public void Handle_UpdateOrganizationDetails_ValidState_EmitsUpdatedAndDisplayNameDerived()
    {
        // Arrange
        UpdateOrganizationDetails command = PartyTestData.ValidUpdateOrganizationDetails();
        PartyState state = PartyTestData.CreateOrganizationState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
        OrganizationDetailsUpdated updated = result.Events[0].ShouldBeOfType<OrganizationDetailsUpdated>();
        updated.OrganizationDetails.ShouldNotBeNull();
        updated.OrganizationDetails.LegalName.ShouldBe(command.OrganizationDetails.LegalName);
        updated.OrganizationDetails.TradingName.ShouldBe(command.OrganizationDetails.TradingName);
        result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
    }

    [Fact]
    public void Handle_UpdateOrganizationDetails_NullState_ReturnsRejection()
    {
        // Arrange
        UpdateOrganizationDetails command = PartyTestData.ValidUpdateOrganizationDetails();

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
    }

    [Fact]
    public void Handle_UpdateOrganizationDetails_OnPerson_ReturnsTypeMismatch()
    {
        // Arrange
        UpdateOrganizationDetails command = PartyTestData.ValidUpdateOrganizationDetails();
        PartyState state = PartyTestData.CreatePersonState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
    }

    [Fact]
    public void Handle_UpdateOrganizationDetails_NullPayload_ReturnsRejection()
    {
        // Arrange
        UpdateOrganizationDetails command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            OrganizationDetails = null!,
        };
        PartyState state = PartyTestData.CreateOrganizationState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        PartyTypeMismatch rejection = result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
        rejection.Message.ShouldBe("Organization details are required.");
    }

    [Fact]
    public void Handle_UpdateOrganizationDetails_NullCommand_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => PartyAggregate.Handle((UpdateOrganizationDetails)null!, null));
    }

    [Fact]
    public void Handle_UpdateOrganizationDetails_DisplayNameReDerived_UsesNewLegalName()
    {
        // Arrange
        UpdateOrganizationDetails command = PartyTestData.ValidUpdateOrganizationDetails();
        PartyState state = PartyTestData.CreateOrganizationState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        PartyDisplayNameDerived nameDerived = result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        nameDerived.DisplayName.ShouldBe("New Legal Name");
        nameDerived.SortName.ShouldBe("New Legal Name");
    }

    [Fact]
    public void Handle_UpdateOrganizationDetails_ApplyEvents_StateReflectsNewDetails()
    {
        // Arrange
        UpdateOrganizationDetails command = PartyTestData.ValidUpdateOrganizationDetails();
        PartyState state = PartyTestData.CreateOrganizationState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Apply events
        foreach (object evt in result.Events)
        {
            switch (evt)
            {
                case OrganizationDetailsUpdated updated:
                    state.Apply(updated);
                    break;
                case PartyDisplayNameDerived nameDerived:
                    state.Apply(nameDerived);
                    break;
            }
        }

        // Assert
        state.Organization.ShouldNotBeNull();
        state.Organization.LegalName.ShouldBe("New Legal Name");
        state.DisplayName.ShouldBe("New Legal Name");
        state.SortName.ShouldBe("New Legal Name");
    }

    // --- SetIsNaturalPerson ---

    [Fact]
    public void Handle_SetIsNaturalPerson_ValidOrg_EmitsChangedEvent()
    {
        // Arrange
        SetIsNaturalPerson command = PartyTestData.ValidSetIsNaturalPerson(true);
        PartyState state = PartyTestData.CreateOrganizationState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        IsNaturalPersonChanged changed = result.Events[0].ShouldBeOfType<IsNaturalPersonChanged>();
        changed.IsNaturalPerson.ShouldBeTrue();
    }

    [Fact]
    public void Handle_SetIsNaturalPerson_NullState_ReturnsRejection()
    {
        // Arrange
        SetIsNaturalPerson command = PartyTestData.ValidSetIsNaturalPerson();

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
    }

    [Fact]
    public void Handle_SetIsNaturalPerson_OnPerson_ReturnsTypeMismatch()
    {
        // Arrange
        SetIsNaturalPerson command = PartyTestData.ValidSetIsNaturalPerson();
        PartyState state = PartyTestData.CreatePersonState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
    }

    [Fact]
    public void Handle_SetIsNaturalPerson_SameValue_ReturnsNoOp()
    {
        // Arrange — org with IsNaturalPerson = true
        PartyState state = new();
        state.Apply(new PartyCreated
        {
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails { LegalName = "Legal", IsNaturalPerson = true },
        });
        SetIsNaturalPerson command = PartyTestData.ValidSetIsNaturalPerson(true);

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Handle_SetIsNaturalPerson_NullCommand_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => PartyAggregate.Handle((SetIsNaturalPerson)null!, null));
    }

    [Fact]
    public void Handle_SetIsNaturalPerson_ApplyEvent_StateReflectsChange()
    {
        // Arrange
        SetIsNaturalPerson command = PartyTestData.ValidSetIsNaturalPerson(true);
        PartyState state = PartyTestData.CreateOrganizationState();
        state.IsNaturalPerson.ShouldBeFalse(); // precondition: default is false

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Apply event
        IsNaturalPersonChanged changed = result.Events[0].ShouldBeOfType<IsNaturalPersonChanged>();
        state.Apply(changed);

        // Assert
        state.IsNaturalPerson.ShouldBeTrue();
    }
}
