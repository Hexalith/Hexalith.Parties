using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Domain;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

[Collection("Non-parallel")]
public class PartyAggregateConsentTests {
    // === Task 7.1 ===
    [Fact]
    public void Handle_RecordConsent_EmitsConsentRecordedEvent() {
        // Arrange
        RecordConsent command = PartyTestData.ValidRecordConsent();
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        ConsentRecorded recorded = result.Events[0].ShouldBeOfType<ConsentRecorded>();
        recorded.PartyId.ShouldBe(PartyTestData.DefaultPartyId);
        recorded.TenantId.ShouldBe(PartyTestData.DefaultTenantId);
        recorded.ChannelId.ShouldBe(PartyTestData.DefaultChannelId);
        recorded.Purpose.ShouldBe(PartyTestData.DefaultConsentPurpose);
        recorded.LawfulBasis.ShouldBe(LawfulBasis.Consent);
        recorded.ConsentId.ShouldBe($"{PartyTestData.DefaultChannelId}:{PartyTestData.DefaultConsentPurpose}");
        recorded.GrantedBy.ShouldBe("test-admin");
        recorded.Source.ShouldBe("admin-portal");
    }

    // === Task 7.2 ===
    [Fact]
    public void Handle_RecordConsent_DuplicateChannelPurpose_ReturnsNoOp() {
        // Arrange
        RecordConsent command = PartyTestData.ValidRecordConsent();
        PartyState state = PartyTestData.CreateStateWithConsent();

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsNoOp.ShouldBeTrue();
    }

    // === Task 7.3 ===
    [Fact]
    public void Handle_RecordConsent_InvalidChannel_RejectsWithContactChannelNotFound() {
        // Arrange
        RecordConsent command = new() {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            ChannelId = "nonexistent-channel",
            Purpose = "marketing",
            LawfulBasis = LawfulBasis.Consent,
        };
        PartyState state = PartyTestData.CreatePersonState(); // No channels

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        ContactChannelNotFound rejection = result.Events[0].ShouldBeOfType<ContactChannelNotFound>();
        string message = rejection.Message.ShouldNotBeNull();
        message.ShouldBe("Contact channel not found.");
        message.ShouldNotContain(command.ChannelId, Case.Sensitive);
    }

    [Fact]
    public void Handle_RecordConsent_CompatibleChannelPreservesWriteNormalization()
    {
        const string channelId = "Ch-Email-1";
        PartyState state = PartyTestData.CreatePersonState();
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = channelId,
            Type = ContactChannelType.Email,
            Value = "person@example.test",
        });
        RecordConsent command = PartyTestData.ValidRecordConsent() with
        {
            ChannelId = channelId,
            Purpose = " Marketing ",
        };

        DomainResult result = PartyAggregate.Handle(command, state);

        ConsentRecorded recorded = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConsentRecorded>();
        recorded.ChannelId.ShouldBe(channelId);
        recorded.Purpose.ShouldBe("marketing");
        recorded.ConsentId.ShouldBe("ch-email-1:marketing");
    }

    [Fact]
    public void Handle_RecordConsent_UnsafeChannelRejectsBeforeLookupWithoutEcho()
    {
        const string unsafeChannelId = "channel/unsafe-sensitive";
        RecordConsent command = PartyTestData.ValidRecordConsent() with { ChannelId = unsafeChannelId };
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        CompositeOperationConflict rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<CompositeOperationConflict>();
        string message = rejection.Message.ShouldNotBeNull();
        message.ShouldBe("Channel ID is invalid.");
        message.ShouldNotContain(unsafeChannelId, Case.Sensitive);
    }

    [Fact]
    public void Handle_RecordConsent_UnsafePartyIdRejectsBeforeChannelLookupWithoutEcho()
    {
        const string unsafePartyId = "party/unsafe-sensitive";
        RecordConsent command = PartyTestData.ValidRecordConsent() with { PartyId = unsafePartyId };
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        CompositeOperationConflict rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<CompositeOperationConflict>();
        string message = rejection.Message.ShouldNotBeNull();
        message.ShouldBe("Party ID is invalid.");
        message.ShouldNotContain(unsafePartyId, Case.Sensitive);
    }

    [Fact]
    public void Handle_RevokeConsent_UnsafePartyIdRejectsBeforeConsentLookupWithoutEcho()
    {
        const string unsafePartyId = "party/unsafe-sensitive";
        RevokeConsent command = PartyTestData.ValidRevokeConsent("consent-1") with { PartyId = unsafePartyId };
        PartyState state = PartyTestData.CreateStateWithConsent();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        CompositeOperationConflict rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<CompositeOperationConflict>();
        string message = rejection.Message.ShouldNotBeNull();
        message.ShouldBe("Party ID is invalid.");
        message.ShouldNotContain(unsafePartyId, Case.Sensitive);
    }

    [Fact]
    public void Handle_RecordConsent_OverLimitChannelRejectsWithoutEcho()
    {
        string channelId = new('c', 129);
        RecordConsent command = PartyTestData.ValidRecordConsent() with { ChannelId = channelId };
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();

        DomainResult result = PartyAggregate.Handle(command, state);

        CompositeOperationConflict rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<CompositeOperationConflict>();
        string message = rejection.Message.ShouldNotBeNull();
        message.ShouldBe("Channel ID is invalid.");
        message.ShouldNotContain(channelId, Case.Sensitive);
    }

    [Fact]
    public void Handle_RecordConsent_PartyWideShortcut_RejectsWithoutApplyingConsent()
    {
        RecordConsent command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            ChannelId = "*",
            Purpose = "all",
            LawfulBasis = LawfulBasis.Consent,
            ActorUserId = "test-admin",
            Source = "admin-portal",
        };
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        CompositeOperationConflict rejection = result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        rejection.Message.ShouldBe("Channel ID is invalid.");
        state.ConsentRecords.ShouldBeEmpty();
    }

    // === Task 7.4 ===
    [Fact]
    public void Handle_RecordConsent_WhenRestricted_Succeeds() {
        // Arrange
        RecordConsent command = PartyTestData.ValidRecordConsent();
        PartyState state = PartyTestData.CreateRestrictedState();

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<ConsentRecorded>();
    }

    // === Task 7.5 ===
    [Fact]
    public void Handle_RevokeConsent_EmitsConsentRevokedEvent() {
        // Arrange
        PartyState state = PartyTestData.CreateStateWithConsent();
        string consentId = $"{PartyTestData.DefaultChannelId}:{PartyTestData.DefaultConsentPurpose}";
        RevokeConsent command = PartyTestData.ValidRevokeConsent(consentId);

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        ConsentRevoked revoked = result.Events[0].ShouldBeOfType<ConsentRevoked>();
        revoked.ConsentId.ShouldBe(consentId);
        revoked.RevokedBy.ShouldBe("test-admin");
        revoked.Reason.ShouldBe("withdrawn");
        revoked.Source.ShouldBe("admin-portal");
    }

    [Theory]
    [InlineData("consent-1")]
    [InlineData("ch-email-1:marketing")]
    public void Handle_RevokeConsent_CompatibleStoredIdEmitsExactIdentifier(string consentId)
    {
        PartyState state = PartyTestData.CreatePersonState();
        state.Apply(new ConsentRecorded
        {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            ConsentId = consentId,
            ChannelId = PartyTestData.DefaultChannelId,
            Purpose = PartyTestData.DefaultConsentPurpose,
            LawfulBasis = LawfulBasis.Consent,
            GrantedAt = DateTimeOffset.UtcNow,
            GrantedBy = "admin",
        });
        RevokeConsent command = PartyTestData.ValidRevokeConsent(consentId);

        DomainResult result = PartyAggregate.Handle(command, state);

        ConsentRevoked revoked = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ConsentRevoked>();
        revoked.ConsentId.ShouldBe(consentId);
    }

    [Fact]
    public void Handle_RevokeConsent_UnsafeIdentifierRejectsBeforeLookupWithoutEcho()
    {
        const string unsafeConsentId = "channel:purpose:unsafe-sensitive";
        RevokeConsent command = PartyTestData.ValidRevokeConsent(unsafeConsentId);
        PartyState state = PartyTestData.CreateStateWithConsent();

        DomainResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        CompositeOperationConflict rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<CompositeOperationConflict>();
        string message = rejection.Message.ShouldNotBeNull();
        message.ShouldBe("Consent ID is invalid.");
        message.ShouldNotContain(unsafeConsentId, Case.Sensitive);
    }

    [Fact]
    public void Handle_RevokeConsent_OverLimitIdentifierRejectsWithoutEcho()
    {
        string consentId = $"channel:{new string('p', 101)}";
        RevokeConsent command = PartyTestData.ValidRevokeConsent(consentId);
        PartyState state = PartyTestData.CreateStateWithConsent();

        DomainResult result = PartyAggregate.Handle(command, state);

        CompositeOperationConflict rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<CompositeOperationConflict>();
        string message = rejection.Message.ShouldNotBeNull();
        message.ShouldBe("Consent ID is invalid.");
        message.ShouldNotContain(consentId, Case.Sensitive);
    }

    [Fact]
    public void Handle_ConsentCommands_UnsafeIdentifiersPreservePartyNotFoundPrecedence()
    {
        RecordConsent record = PartyTestData.ValidRecordConsent() with { ChannelId = "channel/unsafe" };
        RevokeConsent revoke = PartyTestData.ValidRevokeConsent("channel:purpose:unsafe");

        PartyAggregate.Handle(record, null).Events.ShouldHaveSingleItem().ShouldBeOfType<PartyNotFound>();
        PartyAggregate.Handle(revoke, null).Events.ShouldHaveSingleItem().ShouldBeOfType<PartyNotFound>();
    }

    [Fact]
    public void Handle_ConsentCommands_UnsafeIdentifiersPreserveErasurePrecedence()
    {
        PartyState state = PartyTestData.CreateErasurePendingState();
        RecordConsent record = PartyTestData.ValidRecordConsent() with { ChannelId = "channel/unsafe" };
        RevokeConsent revoke = PartyTestData.ValidRevokeConsent("channel:purpose:unsafe");

        PartyAggregate.Handle(record, state).Events.ShouldHaveSingleItem().ShouldBeOfType<PartyErasureInProgress>();
        PartyAggregate.Handle(revoke, state).Events.ShouldHaveSingleItem().ShouldBeOfType<PartyErasureInProgress>();
    }

    // === Task 7.6 ===
    [Fact]
    public void Handle_RevokeConsent_AlreadyRevoked_ReturnsNoOp() {
        // Arrange
        PartyState state = PartyTestData.CreateStateWithConsent();
        string consentId = $"{PartyTestData.DefaultChannelId}:{PartyTestData.DefaultConsentPurpose}";
        // Revoke the consent first
        state.Apply(new ConsentRevoked {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            ConsentId = consentId,
            RevokedAt = DateTimeOffset.UtcNow,
            RevokedBy = "admin",
        });
        RevokeConsent command = PartyTestData.ValidRevokeConsent(consentId);

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsNoOp.ShouldBeTrue();
    }

    // === Task 7.7 ===
    [Fact]
    public void Handle_RevokeConsent_NonExistent_RejectsWithConsentNotFound() {
        // Arrange
        PartyState state = PartyTestData.CreatePersonState();
        RevokeConsent command = PartyTestData.ValidRevokeConsent("nonexistent:consent");

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        ConsentNotFound rejection = result.Events[0].ShouldBeOfType<ConsentNotFound>();
        rejection.PartyId.ShouldBe(PartyTestData.DefaultPartyId);
        rejection.TenantId.ShouldBe(PartyTestData.DefaultTenantId);
        rejection.ConsentId.ShouldBe("nonexistent:consent");
        string message = rejection.Message.ShouldNotBeNull();
        message.ShouldBe("Consent not found.");
        message.ShouldNotContain(command.ConsentId, Case.Sensitive);
    }

    // === Task 7.15 ===
    [Fact]
    public void Apply_ConsentRecorded_AddsToConsentRecordsList() {
        // Arrange
        PartyState state = PartyTestData.CreatePersonState();
        ConsentRecorded e = new() {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            ConsentId = "ch-1:marketing",
            ChannelId = "ch-1",
            Purpose = "marketing",
            LawfulBasis = LawfulBasis.Consent,
            GrantedAt = DateTimeOffset.UtcNow,
            GrantedBy = "admin",
            Source = "api",
        };

        // Act
        state.Apply(e);

        // Assert
        state.ConsentRecords.Count.ShouldBe(1);
        state.ConsentRecords[0].ConsentId.ShouldBe("ch-1:marketing");
        state.ConsentRecords[0].IsActive.ShouldBeTrue();
        state.ConsentRecords[0].Source.ShouldBe("api");
    }

    // === Task 7.16 ===
    [Fact]
    public void Apply_ConsentRevoked_SetsRevokedAtOnConsent() {
        // Arrange
        PartyState state = PartyTestData.CreateStateWithConsent();
        string consentId = $"{PartyTestData.DefaultChannelId}:{PartyTestData.DefaultConsentPurpose}";
        DateTimeOffset revokedAt = DateTimeOffset.UtcNow;
        ConsentRevoked e = new() {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            ConsentId = consentId,
            RevokedAt = revokedAt,
            RevokedBy = "admin",
            Reason = "withdrawn",
            Source = "admin-portal",
        };

        // Act
        state.Apply(e);

        // Assert
        ConsentRecord consent = state.ConsentRecords.First(c => c.ConsentId == consentId);
        consent.RevokedAt.ShouldBe(revokedAt);
        consent.IsActive.ShouldBeFalse();
        consent.RevocationReason.ShouldBe("withdrawn");
        consent.RevocationSource.ShouldBe("admin-portal");
    }

    // === Task 7.19 ===
    [Fact]
    public void Apply_FullConsentSequence_ReconstructsCorrectState() {
        // Arrange — create party → add channel → consent A → revoke A → consent B
        PartyState state = new();
        state.Apply(new PartyCreated {
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        });
        state.Apply(new ContactChannelAdded {
            ContactChannelId = "ch-1",
            Type = ContactChannelType.Email,
            Value = "test@example.com",
            IsPreferred = true,
        });
        state.Apply(new ConsentRecorded {
            PartyId = "p1",
            TenantId = "t1",
            ConsentId = "ch-1:marketing",
            ChannelId = "ch-1",
            Purpose = "marketing",
            LawfulBasis = LawfulBasis.Consent,
            GrantedAt = DateTimeOffset.UtcNow.AddDays(-10),
            GrantedBy = "admin",
            Source = "api",
        });
        state.Apply(new ConsentRevoked {
            PartyId = "p1",
            TenantId = "t1",
            ConsentId = "ch-1:marketing",
            RevokedAt = DateTimeOffset.UtcNow.AddDays(-5),
            RevokedBy = "admin",
            Reason = "withdrawn",
            Source = "api",
        });
        state.Apply(new ConsentRecorded {
            PartyId = "p1",
            TenantId = "t1",
            ConsentId = "ch-1:billing",
            ChannelId = "ch-1",
            Purpose = "billing",
            LawfulBasis = LawfulBasis.ContractualNecessity,
            GrantedAt = DateTimeOffset.UtcNow,
            GrantedBy = "admin",
            Source = "api",
        });

        // Assert
        state.ConsentRecords.Count.ShouldBe(2);
        ConsentRecord revoked = state.ConsentRecords.First(c => c.ConsentId == "ch-1:marketing");
        revoked.IsActive.ShouldBeFalse();
        revoked.RevokedAt.ShouldNotBeNull();
        revoked.RevocationReason.ShouldBe("withdrawn");
        ConsentRecord active = state.ConsentRecords.First(c => c.ConsentId == "ch-1:billing");
        active.IsActive.ShouldBeTrue();
        active.Source.ShouldBe("api");
    }

    [Fact]
    public void Handle_RecordConsent_NullState_ReturnsRejection() {
        // Arrange
        RecordConsent command = PartyTestData.ValidRecordConsent();

        // Act
        DomainResult result = PartyAggregate.Handle(command, null);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyNotFound>();
    }

    [Fact]
    public void Handle_RevokeConsent_WhenRestricted_Succeeds() {
        // Arrange — consent management allowed during restriction
        PartyState state = PartyTestData.CreateRestrictedState();
        // Add consent to revoke
        state.Apply(new ConsentRecorded {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            ConsentId = "ch-email-1:marketing",
            ChannelId = "ch-email-1",
            Purpose = "marketing",
            LawfulBasis = LawfulBasis.Consent,
            GrantedAt = DateTimeOffset.UtcNow,
            GrantedBy = "admin",
            Source = "api",
        });
        RevokeConsent command = PartyTestData.ValidRevokeConsent("ch-email-1:marketing");

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<ConsentRevoked>();
    }

    [Fact]
    public void Handle_RecordConsent_InvalidPurpose_EmptyString_Rejects() {
        // Arrange
        RecordConsent command = new() {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            ChannelId = PartyTestData.DefaultChannelId,
            Purpose = "",
            LawfulBasis = LawfulBasis.Consent,
        };
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        InvalidConsentPurpose rejection = result.Events[0].ShouldBeOfType<InvalidConsentPurpose>();
        rejection.TenantId.ShouldBe(PartyTestData.DefaultTenantId);
        rejection.Purpose.ShouldBeNull();
        rejection.Message.ShouldBe("Purpose is required.");
    }

    [Fact]
    public void Handle_RecordConsent_InvalidPurpose_SpecialChars_Rejects() {
        // Arrange
        RecordConsent command = new() {
            PartyId = PartyTestData.DefaultPartyId,
            TenantId = PartyTestData.DefaultTenantId,
            ChannelId = PartyTestData.DefaultChannelId,
            Purpose = "invalid purpose!",
            LawfulBasis = LawfulBasis.Consent,
        };
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        InvalidConsentPurpose rejection = result.Events[0].ShouldBeOfType<InvalidConsentPurpose>();
        rejection.TenantId.ShouldBe(PartyTestData.DefaultTenantId);
        rejection.Purpose.ShouldBeNull();
        rejection.Message.ShouldBe("Purpose must contain only alphanumeric characters, hyphens, and underscores.");
    }

    [Fact]
    public void Handle_RecordConsent_OverLimitPurposeReturnsSanitizedRejection()
    {
        string purpose = new('p', 101);
        RecordConsent command = PartyTestData.ValidRecordConsent() with { Purpose = purpose };
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();

        DomainResult result = PartyAggregate.Handle(command, state);

        InvalidConsentPurpose rejection = result.Events.ShouldHaveSingleItem().ShouldBeOfType<InvalidConsentPurpose>();
        rejection.TenantId.ShouldBe(PartyTestData.DefaultTenantId);
        rejection.Purpose.ShouldBeNull();
        string message = rejection.Message.ShouldNotBeNull();
        message.ShouldBe("Purpose must not exceed 100 characters.");
        message.ShouldNotContain(purpose, Case.Sensitive);
    }

    [Fact]
    public void Handle_RecordConsent_ErasurePending_RejectsWithErasureInProgress() {
        // Arrange
        RecordConsent command = PartyTestData.ValidRecordConsent();
        PartyState state = PartyTestData.CreateErasurePendingState();

        // Act
        DomainResult result = PartyAggregate.Handle(command, state);

        // Assert
        result.IsRejection.ShouldBeTrue();
        result.Events[0].ShouldBeOfType<PartyErasureInProgress>();
    }
}
