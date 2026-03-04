using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Server.Aggregates;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public class PartyAggregateLifecycleTests
{
    // --- DeactivateParty ---

    [Fact]
    public void Handle_DeactivateParty_ActiveParty_EmitsDeactivatedEvent()
    {
        // Arrange
        DeactivateParty command = PartyTestData.ValidDeactivateParty();
        PartyState state = PartyTestData.CreatePersonState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyDeactivated>();
    }

    [Fact]
    public void Handle_DeactivateParty_NullState_ReturnsRejection()
    {
        // Arrange
        DeactivateParty command = PartyTestData.ValidDeactivateParty();

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyCannotBeDeactivatedWhenInactive>();
    }

    [Fact]
    public void Handle_DeactivateParty_AlreadyInactive_ReturnsNoOp()
    {
        // Arrange
        DeactivateParty command = PartyTestData.ValidDeactivateParty();
        PartyState state = PartyTestData.CreateDeactivatedPersonState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Handle_DeactivateParty_NullCommand_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => PartyAggregate.Handle((DeactivateParty)null!, null));
    }

    [Fact]
    public void Handle_DeactivateParty_ApplyEvent_StateReflectsInactive()
    {
        // Arrange
        DeactivateParty command = PartyTestData.ValidDeactivateParty();
        PartyState state = PartyTestData.CreatePersonState();
        state.IsActive.ShouldBeTrue(); // precondition

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Apply event
        PartyDeactivated deactivated = result.Events[0].ShouldBeOfType<PartyDeactivated>();
        state.Apply(deactivated);

        // Assert
        state.IsActive.ShouldBeFalse();
    }

    // --- ReactivateParty ---

    [Fact]
    public void Handle_ReactivateParty_DeactivatedParty_EmitsReactivatedEvent()
    {
        // Arrange
        ReactivateParty command = PartyTestData.ValidReactivateParty();
        PartyState state = PartyTestData.CreateDeactivatedPersonState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyReactivated>();
    }

    [Fact]
    public void Handle_ReactivateParty_NullState_ReturnsRejection()
    {
        // Arrange
        ReactivateParty command = PartyTestData.ValidReactivateParty();

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyCannotBeReactivatedWhenActive>();
    }

    [Fact]
    public void Handle_ReactivateParty_AlreadyActive_ReturnsNoOp()
    {
        // Arrange
        ReactivateParty command = PartyTestData.ValidReactivateParty();
        PartyState state = PartyTestData.CreatePersonState(); // active by default

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Handle_ReactivateParty_NullCommand_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => PartyAggregate.Handle((ReactivateParty)null!, null));
    }

    [Fact]
    public void Handle_ReactivateParty_ApplyEvent_StateReflectsActive()
    {
        // Arrange
        ReactivateParty command = PartyTestData.ValidReactivateParty();
        PartyState state = PartyTestData.CreateDeactivatedPersonState();
        state.IsActive.ShouldBeFalse(); // precondition

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Apply event
        PartyReactivated reactivated = result.Events[0].ShouldBeOfType<PartyReactivated>();
        state.Apply(reactivated);

        // Assert
        state.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Handle_DeactivateThenReactivate_FullCycle_StateIsActive()
    {
        // Arrange
        PartyState state = PartyTestData.CreatePersonState();
        state.IsActive.ShouldBeTrue();

        // Act — Deactivate
        var deactivateResult = PartyAggregate.Handle(PartyTestData.ValidDeactivateParty(), state);
        deactivateResult.IsSuccess.ShouldBeTrue();
        state.Apply(deactivateResult.Events[0].ShouldBeOfType<PartyDeactivated>());
        state.IsActive.ShouldBeFalse();

        // Act — Reactivate
        var reactivateResult = PartyAggregate.Handle(PartyTestData.ValidReactivateParty(), state);
        reactivateResult.IsSuccess.ShouldBeTrue();
        state.Apply(reactivateResult.Events[0].ShouldBeOfType<PartyReactivated>());

        // Assert
        state.IsActive.ShouldBeTrue();
    }
}
