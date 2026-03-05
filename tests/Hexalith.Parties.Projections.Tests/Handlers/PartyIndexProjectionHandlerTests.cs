using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Projections.Tests.Handlers;

public class PartyIndexProjectionHandlerTests
{
    private const string PartyId = PartyTestData.DefaultPartyId;

    // --- 5.2: PartyCreated Person ---

    [Fact]
    public void Apply_PartyCreated_Person_CreatesPartyIndexEntryWithDerivedDisplayName()
    {
        PartyCreated @event = new()
        {
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };

        PartyIndexEntry? result = PartyIndexProjectionHandler.Apply(PartyId, @event, null);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(PartyId);
        result.Type.ShouldBe(PartyType.Person);
        result.IsActive.ShouldBeTrue();
        result.DisplayName.ShouldBe("John Doe");
        result.CreatedAt.ShouldNotBe(default);
        result.LastModifiedAt.ShouldNotBe(default);
    }

    // --- 5.3: PartyCreated Organization ---

    [Fact]
    public void Apply_PartyCreated_Organization_CreatesPartyIndexEntryWithLegalName()
    {
        PartyCreated @event = new()
        {
            Type = PartyType.Organization,
            OrganizationDetails = PartyTestData.ValidOrganizationDetails(),
        };

        PartyIndexEntry? result = PartyIndexProjectionHandler.Apply(PartyId, @event, null);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(PartyId);
        result.Type.ShouldBe(PartyType.Organization);
        result.IsActive.ShouldBeTrue();
        result.DisplayName.ShouldBe("Acme Corp");
        result.CreatedAt.ShouldNotBe(default);
        result.LastModifiedAt.ShouldNotBe(default);
    }

    // --- 5.4: PartyDisplayNameDerived ---

    [Fact]
    public void Apply_PartyDisplayNameDerived_UpdatesDisplayName()
    {
        PartyIndexEntry state = CreatePersonIndexEntry();
        PartyDisplayNameDerived @event = new()
        {
            DisplayName = "Jane Smith",
            SortName = "Smith, Jane",
        };

        PartyIndexEntry? result = PartyIndexProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.DisplayName.ShouldBe("Jane Smith");
        result.LastModifiedAt.ShouldBeGreaterThan(state.LastModifiedAt);
    }

    // --- 5.5: PartyDeactivated ---

    [Fact]
    public void Apply_PartyDeactivated_SetsIsActiveFalse()
    {
        PartyIndexEntry state = CreatePersonIndexEntry();

        PartyIndexEntry? result = PartyIndexProjectionHandler.Apply(PartyId, new PartyDeactivated(), state);

        result.ShouldNotBeNull();
        result.IsActive.ShouldBeFalse();
        result.LastModifiedAt.ShouldBeGreaterThan(state.LastModifiedAt);
    }

    // --- 5.6: PartyReactivated ---

    [Fact]
    public void Apply_PartyReactivated_SetsIsActiveTrue()
    {
        PartyIndexEntry state = CreatePersonIndexEntry() with { IsActive = false };

        PartyIndexEntry? result = PartyIndexProjectionHandler.Apply(PartyId, new PartyReactivated(), state);

        result.ShouldNotBeNull();
        result.IsActive.ShouldBeTrue();
        result.LastModifiedAt.ShouldBeGreaterThan(state.LastModifiedAt);
    }

    // --- 5.7: ContactChannelAdded ---

    [Fact]
    public void Apply_ContactChannelAdded_UpdatesLastModifiedAtOnly()
    {
        PartyIndexEntry state = CreatePersonIndexEntry();
        ContactChannelAdded @event = new()
        {
            ContactChannelId = "cc-1",
            Type = ContactChannelType.Email,
            Value = "john@example.com",
            IsPreferred = true,
        };

        PartyIndexEntry? result = PartyIndexProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(state.Id);
        result.Type.ShouldBe(state.Type);
        result.IsActive.ShouldBe(state.IsActive);
        result.DisplayName.ShouldBe(state.DisplayName);
        result.CreatedAt.ShouldBe(state.CreatedAt);
        result.LastModifiedAt.ShouldBeGreaterThan(state.LastModifiedAt);
    }

    // --- 5.8: ContactChannelRemoved ---

    [Fact]
    public void Apply_ContactChannelRemoved_UpdatesLastModifiedAtOnly()
    {
        PartyIndexEntry state = CreatePersonIndexEntry();
        ContactChannelRemoved @event = new() { ContactChannelId = "cc-1" };

        PartyIndexEntry? result = PartyIndexProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(state.Id);
        result.DisplayName.ShouldBe(state.DisplayName);
        result.LastModifiedAt.ShouldBeGreaterThan(state.LastModifiedAt);
    }

    // --- 5.9: IdentifierAdded ---

    [Fact]
    public void Apply_IdentifierAdded_UpdatesLastModifiedAtOnly()
    {
        PartyIndexEntry state = CreatePersonIndexEntry();
        IdentifierAdded @event = new()
        {
            IdentifierId = "id-vat-1",
            Type = IdentifierType.VAT,
            Value = "FR12345678901",
        };

        PartyIndexEntry? result = PartyIndexProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(state.Id);
        result.DisplayName.ShouldBe(state.DisplayName);
        result.LastModifiedAt.ShouldBeGreaterThan(state.LastModifiedAt);
    }

    // --- 5.10: IdentifierRemoved ---

    [Fact]
    public void Apply_IdentifierRemoved_UpdatesLastModifiedAtOnly()
    {
        PartyIndexEntry state = CreatePersonIndexEntry();
        IdentifierRemoved @event = new() { IdentifierId = "id-vat-1" };

        PartyIndexEntry? result = PartyIndexProjectionHandler.Apply(PartyId, @event, state);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(state.Id);
        result.DisplayName.ShouldBe(state.DisplayName);
        result.LastModifiedAt.ShouldBeGreaterThan(state.LastModifiedAt);
    }

    // --- 5.11: Unrecognized event ---

    [Fact]
    public void Apply_UnrecognizedEvent_ReturnsNull()
    {
        PartyIndexEntry state = CreatePersonIndexEntry();

        PartyIndexEntry? result = PartyIndexProjectionHandler.Apply(PartyId, new UnrecognizedEvent(), state);

        result.ShouldBeNull();
    }

    // --- 5.12: Null state + non-PartyCreated ---

    [Fact]
    public void Apply_NullState_NonCreatedEvent_ReturnsNull()
    {
        PartyIndexEntry? result = PartyIndexProjectionHandler.Apply(PartyId, new PartyDeactivated(), null);

        result.ShouldBeNull();
    }

    [Fact]
    public void Apply_NullState_PartyDisplayNameDerived_ReturnsNull()
    {
        PartyDisplayNameDerived @event = new()
        {
            DisplayName = "Test",
            SortName = "Test",
        };

        PartyIndexEntry? result = PartyIndexProjectionHandler.Apply(PartyId, @event, null);

        result.ShouldBeNull();
    }

    [Fact]
    public void Apply_NullState_ContactChannelAdded_ReturnsNull()
    {
        ContactChannelAdded @event = new()
        {
            ContactChannelId = "cc-1",
            Type = ContactChannelType.Email,
            Value = "test@example.com",
            IsPreferred = false,
        };

        PartyIndexEntry? result = PartyIndexProjectionHandler.Apply(PartyId, @event, null);

        result.ShouldBeNull();
    }

    // --- 5.13: Multi-event sequence ---

    [Fact]
    public void Apply_MultiEventSequence_ProducesCorrectFinalState()
    {
        // PartyCreated
        PartyCreated created = new()
        {
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
        };
        PartyIndexEntry? state = PartyIndexProjectionHandler.Apply(PartyId, created, null);
        state.ShouldNotBeNull();
        state.DisplayName.ShouldBe("John Doe");
        state.IsActive.ShouldBeTrue();

        // PartyDisplayNameDerived
        PartyDisplayNameDerived displayName = new()
        {
            DisplayName = "Johnathan Doe",
            SortName = "Doe, Johnathan",
        };
        state = PartyIndexProjectionHandler.Apply(PartyId, displayName, state);
        state.ShouldNotBeNull();
        state.DisplayName.ShouldBe("Johnathan Doe");

        // ContactChannelAdded x2
        ContactChannelAdded email = new()
        {
            ContactChannelId = "cc-email",
            Type = ContactChannelType.Email,
            Value = "john@example.com",
            IsPreferred = true,
        };
        state = PartyIndexProjectionHandler.Apply(PartyId, email, state);
        state.ShouldNotBeNull();

        ContactChannelAdded phone = new()
        {
            ContactChannelId = "cc-phone",
            Type = ContactChannelType.Phone,
            Value = "+33123456789",
        };
        state = PartyIndexProjectionHandler.Apply(PartyId, phone, state);
        state.ShouldNotBeNull();

        // IdentifierAdded
        IdentifierAdded vat = new()
        {
            IdentifierId = "id-vat",
            Type = IdentifierType.VAT,
            Value = "FR12345678901",
        };
        state = PartyIndexProjectionHandler.Apply(PartyId, vat, state);
        state.ShouldNotBeNull();

        // PartyDeactivated
        state = PartyIndexProjectionHandler.Apply(PartyId, new PartyDeactivated(), state);
        state.ShouldNotBeNull();

        // Verify final state
        state.Id.ShouldBe(PartyId);
        state.Type.ShouldBe(PartyType.Person);
        state.IsActive.ShouldBeFalse();
        state.DisplayName.ShouldBe("Johnathan Doe");
        state.CreatedAt.ShouldNotBe(default);
        state.LastModifiedAt.ShouldNotBe(default);
        state.LastModifiedAt.ShouldBeGreaterThan(state.CreatedAt);
    }

    // --- Helper methods ---

    private static PartyIndexEntry CreatePersonIndexEntry()
    {
        return new PartyIndexEntry
        {
            Id = PartyId,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "John Doe",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
    }

    private sealed record UnrecognizedEvent : IEventPayload;
}
