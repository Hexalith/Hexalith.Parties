using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Projections.Tests.Handlers;

public class PartyDetailProjectionHandlerTests
{
    private const string PartyId = PartyTestData.DefaultPartyId;

    // --- Task 3.2: PartyCreated ---

    [Fact]
    public void Apply_PartyCreated_Person_CreatesPartyDetailWithCorrectFields()
    {
        PartyCreated @event = new()
        {
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, null);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(PartyId);
        result.Type.ShouldBe(PartyType.Person);
        result.IsActive.ShouldBeTrue();
        result.PersonDetails.ShouldNotBeNull();
        result.PersonDetails.FirstName.ShouldBe("John");
        result.PersonDetails.LastName.ShouldBe("Doe");
        result.OrganizationDetails.ShouldBeNull();
        result.DisplayName.ShouldBe("John Doe");
        result.SortName.ShouldBe("Doe, John");
        result.ContactChannels.ShouldBeEmpty();
        result.Identifiers.ShouldBeEmpty();
        result.CreatedAt.ShouldNotBe(default);
        result.LastModifiedAt.ShouldNotBe(default);
    }

    [Fact]
    public void Apply_PartyCreated_Organization_CreatesPartyDetailWithCorrectFields()
    {
        PartyCreated @event = new()
        {
            Type = PartyType.Organization,
            OrganizationDetails = PartyTestData.ValidOrganizationDetails(),
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, null);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(PartyId);
        result.Type.ShouldBe(PartyType.Organization);
        result.IsActive.ShouldBeTrue();
        result.PersonDetails.ShouldBeNull();
        result.OrganizationDetails.ShouldNotBeNull();
        result.OrganizationDetails.LegalName.ShouldBe("Acme Corp");
        result.DisplayName.ShouldBe("Acme Corp");
        result.SortName.ShouldBe("Acme Corp");
    }

    // --- Task 3.3: PartyDisplayNameDerived ---

    [Fact]
    public void Apply_PartyDisplayNameDerived_UpdatesDisplayNameAndSortName()
    {
        PartyDetail state = CreatePersonDetail();
        PartyDisplayNameDerived @event = new()
        {
            DisplayName = "John Doe",
            SortName = "Doe, John",
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.DisplayName.ShouldBe("John Doe");
        result.SortName.ShouldBe("Doe, John");
        result.LastModifiedAt.ShouldBeGreaterThan(state.LastModifiedAt);
    }

    // --- Task 3.4: PersonDetailsUpdated ---

    [Fact]
    public void Apply_PersonDetailsUpdated_UpdatesPersonDetails()
    {
        PartyDetail state = CreatePersonDetail();
        PersonDetails newDetails = new()
        {
            FirstName = "Jane",
            LastName = "Smith",
        };
        PersonDetailsUpdated @event = new() { PersonDetails = newDetails };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.PersonDetails.ShouldNotBeNull();
        result.PersonDetails.FirstName.ShouldBe("Jane");
        result.PersonDetails.LastName.ShouldBe("Smith");
    }

    // --- Task 3.5: OrganizationDetailsUpdated ---

    [Fact]
    public void Apply_OrganizationDetailsUpdated_UpdatesOrganizationDetails()
    {
        PartyDetail state = CreateOrganizationDetail();
        OrganizationDetails newDetails = new()
        {
            LegalName = "New Corp",
            TradingName = "New Trading",
        };
        OrganizationDetailsUpdated @event = new() { OrganizationDetails = newDetails };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.OrganizationDetails.ShouldNotBeNull();
        result.OrganizationDetails.LegalName.ShouldBe("New Corp");
        result.OrganizationDetails.TradingName.ShouldBe("New Trading");
    }

    // --- Task 3.6: ContactChannelAdded ---

    [Fact]
    public void Apply_ContactChannelAdded_AddsChannelToCollection()
    {
        PartyDetail state = CreatePersonDetail();
        ContactChannelAdded @event = new()
        {
            ContactChannelId = "cc-1",
            Type = ContactChannelType.Email,
            Value = "john@example.com",
            IsPreferred = true,
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.ContactChannels.Count.ShouldBe(1);
        result.ContactChannels[0].Id.ShouldBe("cc-1");
        result.ContactChannels[0].Type.ShouldBe(ContactChannelType.Email);
        result.ContactChannels[0].Value.ShouldBe("john@example.com");
        result.ContactChannels[0].IsPreferred.ShouldBeTrue();
    }

    // --- Task 3.7: ContactChannelUpdated ---

    [Fact]
    public void Apply_ContactChannelUpdated_UpdatesExistingChannel()
    {
        PartyDetail state = CreatePersonDetailWithChannel();
        ContactChannelUpdated @event = new()
        {
            ContactChannelId = "cc-1",
            Value = "updated@example.com",
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.ContactChannels.Count.ShouldBe(1);
        result.ContactChannels[0].Value.ShouldBe("updated@example.com");
        result.ContactChannels[0].Type.ShouldBe(ContactChannelType.Email);
        result.ContactChannels[0].IsPreferred.ShouldBeTrue();
    }

    [Fact]
    public void Apply_ContactChannelUpdated_NoEffectiveChanges_ReturnsNull()
    {
        PartyDetail state = CreatePersonDetailWithChannel();
        ContactChannelUpdated @event = new()
        {
            ContactChannelId = "cc-1",
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldBeNull();
    }

    [Fact]
    public void Apply_ContactChannelUpdated_UnknownChannel_ReturnsNull()
    {
        PartyDetail state = CreatePersonDetailWithChannel();
        ContactChannelUpdated @event = new()
        {
            ContactChannelId = "unknown",
            Value = "updated@example.com",
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldBeNull();
    }

    // --- Task 3.8: ContactChannelRemoved ---

    [Fact]
    public void Apply_ContactChannelRemoved_RemovesChannelFromCollection()
    {
        PartyDetail state = CreatePersonDetailWithChannel();
        ContactChannelRemoved @event = new() { ContactChannelId = "cc-1" };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.ContactChannels.ShouldBeEmpty();
    }

    // --- Task 3.9: PreferredContactChannelChanged ---

    [Fact]
    public void Apply_PreferredContactChannelChanged_UpdatesIsPreferredFlags()
    {
        PartyDetail state = CreatePersonDetailWithMultipleChannels();
        PreferredContactChannelChanged @event = new() { ContactChannelId = "cc-2" };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.ContactChannels.Count.ShouldBe(3);

        // cc-1 (Email, was preferred) -> not preferred
        result.ContactChannels[0].Id.ShouldBe("cc-1");
        result.ContactChannels[0].IsPreferred.ShouldBeFalse();

        // cc-2 (Email, was not preferred) -> preferred
        result.ContactChannels[1].Id.ShouldBe("cc-2");
        result.ContactChannels[1].IsPreferred.ShouldBeTrue();

        // cc-3 (Phone, was not preferred) -> unchanged
        result.ContactChannels[2].Id.ShouldBe("cc-3");
        result.ContactChannels[2].IsPreferred.ShouldBeFalse();
    }

    [Fact]
    public void Apply_PreferredContactChannelChanged_UnknownChannel_ReturnsNull()
    {
        PartyDetail state = CreatePersonDetailWithMultipleChannels();
        PreferredContactChannelChanged @event = new() { ContactChannelId = "unknown" };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldBeNull();
    }

    // --- Task 3.10: IdentifierAdded ---

    [Fact]
    public void Apply_IdentifierAdded_AddsIdentifierToCollection()
    {
        PartyDetail state = CreatePersonDetail();
        IdentifierAdded @event = new()
        {
            IdentifierId = "id-vat-1",
            Type = IdentifierType.VAT,
            Value = "FR12345678901",
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.Identifiers.Count.ShouldBe(1);
        result.Identifiers[0].Id.ShouldBe("id-vat-1");
        result.Identifiers[0].Type.ShouldBe(IdentifierType.VAT);
        result.Identifiers[0].Value.ShouldBe("FR12345678901");
    }

    // --- Task 3.11: IdentifierRemoved ---

    [Fact]
    public void Apply_IdentifierRemoved_RemovesIdentifierFromCollection()
    {
        PartyDetail state = CreatePersonDetailWithIdentifier();
        IdentifierRemoved @event = new() { IdentifierId = "id-vat-1" };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.Identifiers.ShouldBeEmpty();
    }

    // --- Task 3.12: PartyDeactivated ---

    [Fact]
    public void Apply_PartyDeactivated_SetsIsActiveFalse()
    {
        PartyDetail state = CreatePersonDetail();

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, new PartyDeactivated(), state);

        result.ShouldNotBeNull();
        result.IsActive.ShouldBeFalse();
    }

    // --- Task 3.13: PartyReactivated ---

    [Fact]
    public void Apply_PartyReactivated_SetsIsActiveTrue()
    {
        PartyDetail state = CreatePersonDetail() with { IsActive = false };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, new PartyReactivated(), state);

        result.ShouldNotBeNull();
        result.IsActive.ShouldBeTrue();
    }

    // --- Task 3.14: Multi-event sequence ---

    [Fact]
    public void Apply_MultiEventSequence_ProducesCorrectFinalState()
    {
        // PartyCreated
        PartyCreated created = new()
        {
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };
        PartyDetail? state = PartyDetailProjectionHandler.Apply(PartyId, created, null);
        state.ShouldNotBeNull();

        // PartyDisplayNameDerived
        PartyDisplayNameDerived displayName = new()
        {
            DisplayName = "John Doe",
            SortName = "Doe, John",
        };
        state = PartyDetailProjectionHandler.Apply(PartyId, displayName, state);
        state.ShouldNotBeNull();

        // ContactChannelAdded x3
        ContactChannelAdded email = new()
        {
            ContactChannelId = "cc-email",
            Type = ContactChannelType.Email,
            Value = "john@example.com",
            IsPreferred = true,
        };
        state = PartyDetailProjectionHandler.Apply(PartyId, email, state);
        state.ShouldNotBeNull();

        ContactChannelAdded phone = new()
        {
            ContactChannelId = "cc-phone",
            Type = ContactChannelType.Phone,
            Value = "+33123456789",
        };
        state = PartyDetailProjectionHandler.Apply(PartyId, phone, state);
        state.ShouldNotBeNull();

        ContactChannelAdded social = new()
        {
            ContactChannelId = "cc-social",
            Type = ContactChannelType.SocialMedia,
            Value = "@johndoe",
        };
        state = PartyDetailProjectionHandler.Apply(PartyId, social, state);
        state.ShouldNotBeNull();

        // IdentifierAdded x2
        IdentifierAdded vat = new()
        {
            IdentifierId = "id-vat",
            Type = IdentifierType.VAT,
            Value = "FR12345678901",
        };
        state = PartyDetailProjectionHandler.Apply(PartyId, vat, state);
        state.ShouldNotBeNull();

        IdentifierAdded siret = new()
        {
            IdentifierId = "id-siret",
            Type = IdentifierType.SIRET,
            Value = "12345678901234",
        };
        state = PartyDetailProjectionHandler.Apply(PartyId, siret, state);
        state.ShouldNotBeNull();

        // Verify complete state
        state.Id.ShouldBe(PartyId);
        state.Type.ShouldBe(PartyType.Person);
        state.IsActive.ShouldBeTrue();
        state.DisplayName.ShouldBe("John Doe");
        state.SortName.ShouldBe("Doe, John");
        state.PersonDetails.ShouldNotBeNull();
        state.ContactChannels.Count.ShouldBe(3);
        state.ContactChannels[0].Id.ShouldBe("cc-email");
        state.ContactChannels[1].Id.ShouldBe("cc-phone");
        state.ContactChannels[2].Id.ShouldBe("cc-social");
        state.Identifiers.Count.ShouldBe(2);
        state.Identifiers[0].Id.ShouldBe("id-vat");
        state.Identifiers[1].Id.ShouldBe("id-siret");
    }

    // --- Task 3.4 AC #1: Full event sequence with intermediate state verification ---

    [Fact]
    public void Apply_EventSequence_PartyCreatedThroughDeactivated_VerifiesStateAtEachStep()
    {
        // Step 1: PartyCreated
        PartyCreated created = new()
        {
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };
        PartyDetail? state = PartyDetailProjectionHandler.Apply(PartyId, created, null);
        state.ShouldNotBeNull();
        state.Id.ShouldBe(PartyId);
        state.Type.ShouldBe(PartyType.Person);
        state.IsActive.ShouldBeTrue();
        state.ContactChannels.ShouldBeEmpty();
        state.Identifiers.ShouldBeEmpty();
        DateTimeOffset createdAt = state.CreatedAt;

        // Step 2: ContactChannelAdded
        ContactChannelAdded channelAdded = new()
        {
            ContactChannelId = "cc-1",
            Type = ContactChannelType.Email,
            Value = "john@example.com",
            IsPreferred = true,
        };
        state = PartyDetailProjectionHandler.Apply(PartyId, channelAdded, state);
        state.ShouldNotBeNull();
        state.ContactChannels.Count.ShouldBe(1);
        state.ContactChannels[0].Id.ShouldBe("cc-1");
        state.ContactChannels[0].Value.ShouldBe("john@example.com");
        state.Identifiers.ShouldBeEmpty();
        state.IsActive.ShouldBeTrue();
        state.CreatedAt.ShouldBe(createdAt);

        // Step 3: ContactChannelUpdated
        ContactChannelUpdated channelUpdated = new()
        {
            ContactChannelId = "cc-1",
            Value = "updated@example.com",
        };
        state = PartyDetailProjectionHandler.Apply(PartyId, channelUpdated, state);
        state.ShouldNotBeNull();
        state.ContactChannels.Count.ShouldBe(1);
        state.ContactChannels[0].Value.ShouldBe("updated@example.com");
        state.ContactChannels[0].Type.ShouldBe(ContactChannelType.Email);

        // Step 4: IdentifierAdded
        IdentifierAdded identifierAdded = new()
        {
            IdentifierId = "id-vat-1",
            Type = IdentifierType.VAT,
            Value = "FR12345678901",
        };
        state = PartyDetailProjectionHandler.Apply(PartyId, identifierAdded, state);
        state.ShouldNotBeNull();
        state.ContactChannels.Count.ShouldBe(1);
        state.Identifiers.Count.ShouldBe(1);
        state.Identifiers[0].Id.ShouldBe("id-vat-1");
        state.IsActive.ShouldBeTrue();
        state.CreatedAt.ShouldBe(createdAt);

        // Step 5: PartyDeactivated
        state = PartyDetailProjectionHandler.Apply(PartyId, new PartyDeactivated(), state);
        state.ShouldNotBeNull();
        state.IsActive.ShouldBeFalse();
        state.ContactChannels.Count.ShouldBe(1);
        state.Identifiers.Count.ShouldBe(1);
        state.CreatedAt.ShouldBe(createdAt);
    }

    // --- Task 3.15: Unrecognized event ---

    [Fact]
    public void Apply_UnrecognizedEvent_ReturnsNull()
    {
        PartyDetail state = CreatePersonDetail();

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, new UnrecognizedEvent(), state);

        result.ShouldBeNull();
    }

    // --- Task 3.16: Null state + non-PartyCreated event ---

    [Fact]
    public void Apply_NullState_NonCreatedEvent_ReturnsNull()
    {
        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, new PartyDeactivated(), null);

        result.ShouldBeNull();
    }

    [Fact]
    public void Apply_NullState_PersonDetailsUpdated_ReturnsNull()
    {
        PersonDetailsUpdated @event = new()
        {
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, null);

        result.ShouldBeNull();
    }

    // --- Erasure tests ---

    [Fact]
    public void ApplyErasure_WithPersonDetail_NullifiesPiiAndSetsErasedFlags()
    {
        PartyDetail state = CreatePersonDetailWithChannel() with
        {
            DisplayName = "John Doe",
            SortName = "Doe, John",
            Identifiers =
            [
                new PartyIdentifier
                {
                    Id = "id-vat-1",
                    Type = IdentifierType.VAT,
                    Value = "FR12345678901",
                },
            ],
        };

        PartyDetail? result = PartyDetailProjectionHandler.ApplyErasure(PartyId, state);

        result.ShouldNotBeNull();
        result.DisplayName.ShouldBe(string.Empty);
        result.SortName.ShouldBe(string.Empty);
        result.PersonDetails.ShouldBeNull();
        result.OrganizationDetails.ShouldBeNull();
        result.ContactChannels.ShouldBeEmpty();
        result.Identifiers.ShouldBeEmpty();
        result.IsErased.ShouldBeTrue();
        result.ErasedAt.ShouldNotBeNull();
    }

    [Fact]
    public void ApplyErasure_WithOrganizationDetail_NullifiesPiiAndSetsErasedFlags()
    {
        PartyDetail state = CreateOrganizationDetail() with
        {
            DisplayName = "Acme Corp",
            SortName = "Acme Corp",
        };

        PartyDetail? result = PartyDetailProjectionHandler.ApplyErasure(PartyId, state);

        result.ShouldNotBeNull();
        result.DisplayName.ShouldBe(string.Empty);
        result.SortName.ShouldBe(string.Empty);
        result.OrganizationDetails.ShouldBeNull();
        result.IsErased.ShouldBeTrue();
        result.ErasedAt.ShouldNotBeNull();
    }

    [Fact]
    public void ApplyErasure_NullState_ReturnsNull()
    {
        PartyDetail? result = PartyDetailProjectionHandler.ApplyErasure(PartyId, null);

        result.ShouldBeNull();
    }

    [Fact]
    public void ApplyErasure_PreservesNonPiiFields()
    {
        PartyDetail state = CreatePersonDetail() with
        {
            IsActive = false,
        };

        PartyDetail? result = PartyDetailProjectionHandler.ApplyErasure(PartyId, state);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(PartyId);
        result.Type.ShouldBe(PartyType.Person);
        result.IsActive.ShouldBeFalse();
        result.CreatedAt.ShouldBe(state.CreatedAt);
    }

    // --- Task 7.20: Consent and restriction projection tests ---

    [Fact]
    public void Apply_ConsentRecorded_AddsConsentRecordToProjection()
    {
        PartyDetail state = CreatePersonDetail();
        ConsentRecorded @event = new()
        {
            PartyId = PartyId,
            TenantId = "t1",
            ConsentId = "ch-1:marketing",
            ChannelId = "ch-1",
            Purpose = "marketing",
            LawfulBasis = LawfulBasis.Consent,
            GrantedAt = DateTimeOffset.UtcNow,
            GrantedBy = "admin",
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.ConsentRecords.Count.ShouldBe(1);
        result.ConsentRecords[0].ConsentId.ShouldBe("ch-1:marketing");
        result.ConsentRecords[0].IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Apply_ConsentRevoked_SetsRevokedAtOnProjection()
    {
        PartyDetail state = CreatePersonDetail() with
        {
            ConsentRecords =
            [
                new ConsentRecord
                {
                    ConsentId = "ch-1:marketing",
                    ChannelId = "ch-1",
                    Purpose = "marketing",
                    LawfulBasis = LawfulBasis.Consent,
                    GrantedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    GrantedBy = "admin",
                },
            ],
        };
        ConsentRevoked @event = new()
        {
            PartyId = PartyId,
            TenantId = "t1",
            ConsentId = "ch-1:marketing",
            RevokedAt = DateTimeOffset.UtcNow,
            RevokedBy = "admin",
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.ConsentRecords.Count.ShouldBe(1);
        result.ConsentRecords[0].IsActive.ShouldBeFalse();
        result.ConsentRecords[0].RevokedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Apply_ProcessingRestricted_SetsIsRestrictedTrue()
    {
        PartyDetail state = CreatePersonDetail();
        DateTimeOffset restrictedAt = DateTimeOffset.UtcNow;
        ProcessingRestricted @event = new()
        {
            PartyId = PartyId,
            TenantId = "t1",
            RestrictedAt = restrictedAt,
            Reason = "Investigation",
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.IsRestricted.ShouldBeTrue();
        result.RestrictedAt.ShouldBe(restrictedAt);
    }

    [Fact]
    public void Apply_RestrictionLifted_SetsIsRestrictedFalse()
    {
        PartyDetail state = CreatePersonDetail() with
        {
            IsRestricted = true,
            RestrictedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };
        RestrictionLifted @event = new()
        {
            PartyId = PartyId,
            TenantId = "t1",
            LiftedAt = DateTimeOffset.UtcNow,
        };

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.IsRestricted.ShouldBeFalse();
        result.RestrictedAt.ShouldBeNull();
    }

    [Fact]
    public void ApplyErasure_ClearsConsentRecords()
    {
        PartyDetail state = CreatePersonDetail() with
        {
            ConsentRecords =
            [
                new ConsentRecord
                {
                    ConsentId = "ch-1:marketing",
                    ChannelId = "ch-1",
                    Purpose = "marketing",
                    LawfulBasis = LawfulBasis.Consent,
                    GrantedAt = DateTimeOffset.UtcNow,
                    GrantedBy = "admin",
                },
            ],
        };

        PartyDetail? result = PartyDetailProjectionHandler.ApplyErasure(PartyId, state);

        result.ShouldNotBeNull();
        result.ConsentRecords.ShouldBeEmpty();
        result.IsErased.ShouldBeTrue();
    }

    // --- Helper methods ---

    private static PartyDetail CreatePersonDetail()
    {
        return new PartyDetail
        {
            Id = PartyId,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = string.Empty,
            SortName = string.Empty,
            PersonDetails = PartyTestData.ValidPersonDetails(),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
    }

    private static PartyDetail CreateOrganizationDetail()
    {
        return new PartyDetail
        {
            Id = PartyId,
            Type = PartyType.Organization,
            IsActive = true,
            DisplayName = string.Empty,
            SortName = string.Empty,
            OrganizationDetails = PartyTestData.ValidOrganizationDetails(),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
    }

    private static PartyDetail CreatePersonDetailWithChannel()
    {
        return CreatePersonDetail() with
        {
            ContactChannels =
            [
                new ContactChannel
                {
                    Id = "cc-1",
                    Type = ContactChannelType.Email,
                    Value = "john@example.com",
                    IsPreferred = true,
                },
            ],
        };
    }

    private static PartyDetail CreatePersonDetailWithMultipleChannels()
    {
        return CreatePersonDetail() with
        {
            ContactChannels =
            [
                new ContactChannel
                {
                    Id = "cc-1",
                    Type = ContactChannelType.Email,
                    Value = "john@example.com",
                    IsPreferred = true,
                },
                new ContactChannel
                {
                    Id = "cc-2",
                    Type = ContactChannelType.Email,
                    Value = "john2@example.com",
                    IsPreferred = false,
                },
                new ContactChannel
                {
                    Id = "cc-3",
                    Type = ContactChannelType.Phone,
                    Value = "+33123456789",
                    IsPreferred = false,
                },
            ],
        };
    }

    private static PartyDetail CreatePersonDetailWithIdentifier()
    {
        return CreatePersonDetail() with
        {
            Identifiers =
            [
                new PartyIdentifier
                {
                    Id = "id-vat-1",
                    Type = IdentifierType.VAT,
                    Value = "FR12345678901",
                },
            ],
        };
    }

    private sealed record UnrecognizedEvent : IEventPayload;
}
