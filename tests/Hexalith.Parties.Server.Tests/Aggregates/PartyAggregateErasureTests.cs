using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Server.Aggregates;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public class PartyAggregateErasureTests
{
    [Fact]
    public void Handle_ErasePartyCommand_EmitsErasePartyRequestedEvent()
    {
        // Arrange
        EraseParty command = PartyTestData.ValidEraseParty();
        PartyState state = PartyTestData.CreatePersonState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        var requested = result.Events[0].ShouldBeOfType<ErasePartyRequested>();
        requested.PartyId.ShouldBe(PartyTestData.DefaultPartyId);
        requested.TenantId.ShouldBe(PartyTestData.DefaultTenantId);
    }

    [Fact]
    public void Handle_ErasePartyCommand_AlreadyErased_ReturnsNoOp()
    {
        // Arrange
        EraseParty command = PartyTestData.ValidEraseParty();
        PartyState state = PartyTestData.CreateErasedState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Handle_ErasePartyCommand_ErasurePending_ReturnsNoOp()
    {
        // Arrange
        EraseParty command = PartyTestData.ValidEraseParty();
        PartyState state = PartyTestData.CreateErasurePendingState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public void Handle_ErasePartyCommand_NullState_ReturnsRejection()
    {
        // Arrange
        EraseParty command = PartyTestData.ValidEraseParty();

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyNotFound>();
    }

    [Fact]
    public void Handle_AnyCommand_WhenErasurePending_RejectsWithErasureError()
    {
        // Arrange
        PartyState state = PartyTestData.CreateErasurePendingState();
        DeactivateParty command = PartyTestData.ValidDeactivateParty();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyErasureInProgress>();
    }

    [Fact]
    public void Handle_AnyCommand_WhenErased_RejectsWithErasureError()
    {
        // Arrange
        PartyState state = PartyTestData.CreateErasedState();
        UpdatePersonDetails command = PartyTestData.ValidUpdatePersonDetails();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyErasureInProgress>();
    }

    [Fact]
    public void Apply_ErasePartyRequested_SetsErasurePendingStatus()
    {
        // Arrange
        PartyState state = PartyTestData.CreatePersonState();

        // Act
        state.Apply(new ErasePartyRequested
        {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            RequestedAt = DateTimeOffset.UtcNow,
            RequestedBy = "admin",
        });

        // Assert
        state.ErasureStatus.ShouldBe(ErasureStatus.ErasurePending);
    }

    [Fact]
    public void Apply_PartyEncryptionKeyDeleted_SetsKeyDestroyedStatus()
    {
        // Arrange
        PartyState state = PartyTestData.CreateErasurePendingState();

        // Act
        state.Apply(new PartyEncryptionKeyDeleted
        {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            DeletedAt = DateTimeOffset.UtcNow,
        });

        // Assert
        state.ErasureStatus.ShouldBe(ErasureStatus.KeyDestroyed);
    }

    [Fact]
    public void Apply_ErasureVerified_SetsVerifiedStatus()
    {
        // Arrange
        PartyState state = PartyTestData.CreateErasurePendingState();
        state.Apply(new PartyEncryptionKeyDeleted
        {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            DeletedAt = DateTimeOffset.UtcNow,
        });

        // Act
        state.Apply(new ErasureVerified
        {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            VerifiedAt = DateTimeOffset.UtcNow,
            VerificationReportId = "report-1",
        });

        // Assert
        state.ErasureStatus.ShouldBe(ErasureStatus.Verified);
    }

    [Fact]
    public void Apply_PartyErased_SetsTerminalErasedState()
    {
        // Arrange & Act
        PartyState state = PartyTestData.CreateErasedState();

        // Assert
        state.ErasureStatus.ShouldBe(ErasureStatus.Erased);
        state.ErasedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Handle_AddContactChannel_WhenErasurePending_RejectsWithErasureError()
    {
        // Arrange
        PartyState state = PartyTestData.CreateErasurePendingState();
        AddContactChannel command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-new",
            Type = Contracts.ValueObjects.ContactChannelType.Email,
            Value = "test@test.com",
        };

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyErasureInProgress>();
    }

    [Fact]
    public void Handle_UpdateComposite_WhenErasurePending_RejectsWithErasureError()
    {
        // Arrange
        PartyState state = PartyTestData.CreateErasurePendingState();
        // Add channels/identifiers to state so the composite has something to update
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            PersonDetails = new Contracts.ValueObjects.PersonDetails { FirstName = "Test", LastName = "Test" },
        };

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.Events[0].ShouldBeOfType<PartyErasureInProgress>();
    }

    [Fact]
    public void Handle_RotatePartyKey_EmitsPartyEncryptionKeyRotatedEvent()
    {
        // Arrange
        RotatePartyKey command = PartyTestData.ValidRotatePartyKey();
        PartyState state = PartyTestData.CreatePersonState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        var rotated = result.Events[0].ShouldBeOfType<PartyEncryptionKeyRotated>();
        rotated.PartyId.ShouldBe(PartyTestData.DefaultPartyId);
        rotated.NewKeyVersion.ShouldBe(2);
        rotated.PreviousKeyVersion.ShouldBe(1);
    }

    [Fact]
    public void Handle_RotatePartyKey_NullState_ReturnsRejection()
    {
        // Arrange
        RotatePartyKey command = PartyTestData.ValidRotatePartyKey();

        // Act
        var result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyNotFound>();
    }

    [Fact]
    public void Handle_RotatePartyKey_WhenErasurePending_RejectsWithErasureError()
    {
        // Arrange
        RotatePartyKey command = PartyTestData.ValidRotatePartyKey();
        PartyState state = PartyTestData.CreateErasurePendingState();

        // Act
        var result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyErasureInProgress>();
    }
}
