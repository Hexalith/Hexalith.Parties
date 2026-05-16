using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Server.Aggregates;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public class PartyAggregateCreateTests
{
    [Fact]
    public void Handle_CreatePersonParty_EmitsPartyCreatedAndDisplayNameDerived()
    {
        // Arrange
        CreateParty command = PartyTestData.ValidCreatePerson();

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
        PartyCreated created = result.Events[0].ShouldBeOfType<PartyCreated>();
        created.Type.ShouldBe(PartyType.Person);
        created.PersonDetails.ShouldNotBeNull();
        created.PersonDetails.FirstName.ShouldBe(command.PersonDetails!.FirstName);
        created.PersonDetails.LastName.ShouldBe(command.PersonDetails.LastName);
        created.OrganizationDetails.ShouldBeNull();
        result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
    }

    [Fact]
    public void Handle_CreateOrganizationParty_EmitsPartyCreatedAndDisplayNameDerived()
    {
        // Arrange
        CreateParty command = PartyTestData.ValidCreateOrganization();

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
        PartyCreated created = result.Events[0].ShouldBeOfType<PartyCreated>();
        created.Type.ShouldBe(PartyType.Organization);
        created.PersonDetails.ShouldBeNull();
        created.OrganizationDetails.ShouldNotBeNull();
        created.OrganizationDetails.LegalName.ShouldBe(command.OrganizationDetails!.LegalName);
        result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
    }

    [Fact]
    public void PartyCreated_DoesNotDuplicateAggregateIdentity()
    {
        typeof(PartyCreated).GetProperty("PartyId").ShouldBeNull();
    }

    [Fact]
    public void Handle_CreatePersonParty_DisplayNameIsDerivedCorrectly()
    {
        // Arrange
        CreateParty command = PartyTestData.ValidCreatePerson();

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        PartyDisplayNameDerived nameDerived = result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        nameDerived.DisplayName.ShouldBe("John Doe");
        nameDerived.SortName.ShouldBe("Doe, John");
    }

    [Fact]
    public void Handle_CreateOrganizationParty_DisplayNameUsesLegalName()
    {
        // Arrange
        CreateParty command = PartyTestData.ValidCreateOrganization();

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        PartyDisplayNameDerived nameDerived = result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        nameDerived.DisplayName.ShouldBe("Acme Corp");
        nameDerived.SortName.ShouldBe("Acme Corp");
    }

    [Fact]
    public void Handle_CreateOrganizationParty_NullTradingName_UsesLegalName()
    {
        // Arrange
        CreateParty command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails
            {
                LegalName = "Solo Legal Name",
            },
        };

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        PartyDisplayNameDerived nameDerived = result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        nameDerived.DisplayName.ShouldBe("Solo Legal Name");
        nameDerived.SortName.ShouldBe("Solo Legal Name");
    }

    [Fact]
    public void Handle_CreatePartyWhenAlreadyExists_ReturnsNoOp()
    {
        // Arrange
        CreateParty command = PartyTestData.ValidCreatePerson();
        PartyState state = new();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsNoOp.ShouldBeTrue();
        result.Events.ShouldBeEmpty();
        state.Type.ShouldBe(default);
        state.DisplayName.ShouldBeEmpty();
    }

    [Fact]
    public void Handle_CreatePartyWithDefaultType_ReturnsRejection()
    {
        // Arrange
        CreateParty command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            Type = PartyType.Unknown,
        };

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        AssertContainsOnlyRejection<PartyCannotBeCreatedWithoutType>(result);
    }

    [Fact]
    public void Handle_CreatePersonWithoutPersonDetails_ReturnsRejection()
    {
        // Arrange
        CreateParty command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            Type = PartyType.Person,
        };

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        AssertContainsOnlyRejection<PartyCannotBeCreatedWithoutPersonDetails>(result);
    }

    [Fact]
    public void Handle_CreateOrganizationWithoutOrgDetails_ReturnsRejection()
    {
        // Arrange
        CreateParty command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            Type = PartyType.Organization,
        };

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        AssertContainsOnlyRejection<PartyCannotBeCreatedWithoutOrganizationDetails>(result);
    }

    [Fact]
    public void Handle_CreatePersonWithOnlyOrganizationDetails_ReturnsRejectionOnly()
    {
        // Arrange
        CreateParty command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            Type = PartyType.Person,
            OrganizationDetails = PartyTestData.ValidOrganizationDetails(),
        };

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        AssertContainsOnlyRejection<PartyCannotBeCreatedWithoutPersonDetails>(result);
    }

    [Fact]
    public void Handle_CreateOrganizationWithOnlyPersonDetails_ReturnsRejectionOnly()
    {
        // Arrange
        CreateParty command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            Type = PartyType.Organization,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        AssertContainsOnlyRejection<PartyCannotBeCreatedWithoutOrganizationDetails>(result);
    }

    [Fact]
    public void Handle_CreatePartyWithNullCommand_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => PartyAggregate.Handle((CreateParty)null!, null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    public void Handle_CreatePartyWithInvalidId_ReturnsRejection(string partyId)
    {
        // Arrange
        CreateParty command = new()
        {
            PartyId = partyId,
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        AssertContainsOnlyRejection<PartyCannotBeCreatedWithInvalidId>(result);
    }

    [Fact]
    public void Handle_CreatePersonParty_ApplyEventsToState_ProducesCorrectState()
    {
        // Arrange
        CreateParty command = PartyTestData.ValidCreatePerson();

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Apply events to state
        PartyState state = new();
        foreach (object evt in result.Events)
        {
            switch (evt)
            {
                case PartyCreated created:
                    state.Apply(created);
                    break;
                case PartyDisplayNameDerived nameDerived:
                    state.Apply(nameDerived);
                    break;
            }
        }

        // Assert
        state.Type.ShouldBe(PartyType.Person);
        state.Person.ShouldNotBeNull();
        state.Person.FirstName.ShouldBe(command.PersonDetails!.FirstName);
        state.Person.LastName.ShouldBe(command.PersonDetails.LastName);
        state.DisplayName.ShouldBe("John Doe");
        state.SortName.ShouldBe("Doe, John");
        state.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Handle_CreateOrganizationParty_ApplyEventsToState_ProducesCorrectState()
    {
        // Arrange
        CreateParty command = PartyTestData.ValidCreateOrganization();

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Apply events to state
        PartyState state = new();
        foreach (object evt in result.Events)
        {
            switch (evt)
            {
                case PartyCreated created:
                    state.Apply(created);
                    break;
                case PartyDisplayNameDerived nameDerived:
                    state.Apply(nameDerived);
                    break;
            }
        }

        // Assert
        state.Type.ShouldBe(PartyType.Organization);
        state.Organization.ShouldNotBeNull();
        state.Organization.LegalName.ShouldBe(command.OrganizationDetails!.LegalName);
        state.Person.ShouldBeNull();
        state.DisplayName.ShouldBe("Acme Corp");
        state.SortName.ShouldBe("Acme Corp");
        state.IsActive.ShouldBeTrue();
    }

    private static void AssertContainsOnlyRejection<TRejection>(Hexalith.EventStore.Contracts.Results.DomainResult result)
        where TRejection : class
    {
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<TRejection>();
        result.Events.OfType<PartyCreated>().ShouldBeEmpty();
        result.Events.OfType<PartyDisplayNameDerived>().ShouldBeEmpty();
    }
}
