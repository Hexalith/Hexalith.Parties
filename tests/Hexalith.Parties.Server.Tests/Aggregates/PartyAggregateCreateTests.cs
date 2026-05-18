using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
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
        // Spec line 146: aggregate identity must live in EventStore stream metadata, not in the
        // success-event payload. Pin the exact public property set so any future addition of
        // PartyId / AggregateId / StreamId / Id fails this test rather than silently re-introducing
        // identity duplication via a renamed field.
        HashSet<string> publicProperties = [.. typeof(PartyCreated)
            .GetProperties()
            .Select(p => p.Name)];
        publicProperties.ShouldBe(["Type", "PersonDetails", "OrganizationDetails"], ignoreOrder: true);
    }

    [Fact]
    public async Task ProcessAsync_CreateParty_AcceptsCommandPartyIdAsStreamIdentity()
    {
        // AC5 positive evidence: dispatch CreateParty through the EventStore aggregate harness
        // with the envelope's AggregateId bound to CreateParty.PartyId, and confirm the harness
        // accepts that binding and emits PartyCreated. This pins the contract that Parties' upstream
        // pipeline must satisfy when constructing envelopes from CreateParty commands.
        CreateParty command = PartyTestData.ValidCreatePerson();
        CommandEnvelope envelope = new(
            MessageId: Guid.NewGuid().ToString(),
            TenantId: "test-tenant",
            Domain: "parties",
            AggregateId: command.PartyId,
            CommandType: typeof(CreateParty).FullName!,
            Payload: JsonSerializer.SerializeToUtf8Bytes(command),
            CorrelationId: Guid.NewGuid().ToString(),
            CausationId: null,
            UserId: "test-user",
            Extensions: null);
        PartyAggregate aggregate = new();

        DomainResult result = await aggregate.ProcessAsync(envelope, currentState: null);

        result.IsSuccess.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyCreated>();
        envelope.AggregateId.ShouldBe(command.PartyId);
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
        // Arrange — start from a fully populated state (post-creation) so the assertions below
        // prove the duplicate handle truly leaves the existing state unchanged. A fresh
        // `new PartyState()` would make the post-call assertions tautological against initial defaults.
        PartyState state = PartyTestData.CreatePersonState();
        PartyType originalType = state.Type;
        PersonDetails? originalPerson = state.Person;
        string originalDisplayName = state.DisplayName;
        string originalSortName = state.SortName;
        DateTimeOffset originalCreatedAt = state.CreatedAt;
        bool originalIsActive = state.IsActive;

        CreateParty command = PartyTestData.ValidCreatePerson();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsNoOp.ShouldBeTrue();
        result.Events.ShouldBeEmpty();
        state.Type.ShouldBe(originalType);
        state.Person.ShouldBe(originalPerson);
        state.DisplayName.ShouldBe(originalDisplayName);
        state.SortName.ShouldBe(originalSortName);
        state.CreatedAt.ShouldBe(originalCreatedAt);
        state.IsActive.ShouldBe(originalIsActive);
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
        AssertContainsOnlyRejection<PartyCannotBeCreatedWithoutType>(result);
    }

    [Fact]
    public void Handle_CreatePartyWithDefaultType_WhenRetried_ReturnsSameTypedRejection()
    {
        CreateParty command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            Type = PartyType.Unknown,
        };

        var firstResult = PartyAggregate.Handle(command, null);
        var secondResult = PartyAggregate.Handle(command, null);

        AssertContainsOnlyRejection<PartyCannotBeCreatedWithoutType>(firstResult);
        AssertContainsOnlyRejection<PartyCannotBeCreatedWithoutType>(secondResult);
        secondResult.Events[0].ShouldBe(firstResult.Events[0]);
    }

    [Fact]
    public void Handle_CreatePartyWithDefaultType_AfterRejectionReplay_ReturnsNoOpWithNoDuplicateEvents()
    {
        CreateParty command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            Type = PartyType.Unknown,
        };

        var firstResult = PartyAggregate.Handle(command, null);
        AssertContainsOnlyRejection<PartyCannotBeCreatedWithoutType>(firstResult);

        PartyState replayedState = new();
        replayedState.Apply((PartyCannotBeCreatedWithoutType)firstResult.Events[0]);

        var retryResult = PartyAggregate.Handle(command, replayedState);

        retryResult.IsNoOp.ShouldBeTrue("Replaying a rejection must yield a non-null state and a retry must produce no duplicate events.");
        retryResult.Events.ShouldBeEmpty();
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
        state.DisplayName.ShouldBe(command.OrganizationDetails.LegalName);
        state.SortName.ShouldBe(command.OrganizationDetails.LegalName);
        state.IsActive.ShouldBeTrue();
    }

    private static void AssertContainsOnlyRejection<TRejection>(DomainResult result)
        where TRejection : class, IRejectionEvent
    {
        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<TRejection>();
    }
}
