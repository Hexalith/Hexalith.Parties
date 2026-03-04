using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.State;

public class PartyStateTests
{
    [Fact]
    public void Apply_PartyCreated_SetsTypeAndDetails()
    {
        var state = new PartyState();
        var person = new PersonDetails { FirstName = "Jane", LastName = "Doe" };

        state.Apply(new PartyCreated { Type = PartyType.Person, PersonDetails = person });

        state.Type.ShouldBe(PartyType.Person);
        state.Person.ShouldBe(person);
        state.Organization.ShouldBeNull();
        state.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Apply_PartyCreated_SetsIsNaturalPersonFromOrgDetails()
    {
        var state = new PartyState();
        var org = new OrganizationDetails { LegalName = "ACME", IsNaturalPerson = true };

        state.Apply(new PartyCreated { Type = PartyType.Organization, OrganizationDetails = org });

        state.IsNaturalPerson.ShouldBeTrue();
    }

    [Fact]
    public void Apply_PartyCreated_WithoutOrgDetails_IsNaturalPersonIsFalse()
    {
        var state = new PartyState();

        state.Apply(new PartyCreated { Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = "A", LastName = "B" } });

        state.IsNaturalPerson.ShouldBeFalse();
    }

    [Fact]
    public void Apply_PersonDetailsUpdated_UpdatesPerson()
    {
        var state = new PartyState();
        var updated = new PersonDetails { FirstName = "Updated", LastName = "Name" };

        state.Apply(new PersonDetailsUpdated { PersonDetails = updated });

        state.Person.ShouldBe(updated);
    }

    [Fact]
    public void Apply_OrganizationDetailsUpdated_UpdatesOrganization()
    {
        var state = new PartyState();
        var updated = new OrganizationDetails { LegalName = "New Corp" };

        state.Apply(new OrganizationDetailsUpdated { OrganizationDetails = updated });

        state.Organization.ShouldBe(updated);
    }

    [Fact]
    public void Apply_ContactChannelAdded_AddsToList()
    {
        var state = new PartyState();

        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "ch-1",
            Type = ContactChannelType.Email,
            Value = "test@example.com",
            IsPreferred = true,
        });

        state.ContactChannels.Count.ShouldBe(1);
        state.ContactChannels[0].Id.ShouldBe("ch-1");
        state.ContactChannels[0].Value.ShouldBe("test@example.com");
        state.ContactChannels[0].IsPreferred.ShouldBeTrue();
    }

    [Fact]
    public void Apply_ContactChannelUpdated_MergesNonNullFields()
    {
        var state = new PartyState();
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "ch-1",
            Type = ContactChannelType.Email,
            Value = "old@example.com",
        });

        state.Apply(new ContactChannelUpdated
        {
            ContactChannelId = "ch-1",
            Value = "new@example.com",
        });

        state.ContactChannels[0].Value.ShouldBe("new@example.com");
        state.ContactChannels[0].Type.ShouldBe(ContactChannelType.Email);
    }

    [Fact]
    public void Apply_ContactChannelUpdated_IgnoresUnknownId()
    {
        var state = new PartyState();

        state.Apply(new ContactChannelUpdated { ContactChannelId = "unknown" });

        state.ContactChannels.Count.ShouldBe(0);
    }

    [Fact]
    public void Apply_ContactChannelRemoved_RemovesById()
    {
        var state = new PartyState();
        state.Apply(new ContactChannelAdded { ContactChannelId = "ch-1", Type = ContactChannelType.Email, Value = "a@b.com" });
        state.Apply(new ContactChannelAdded { ContactChannelId = "ch-2", Type = ContactChannelType.Phone, Value = "123" });

        state.Apply(new ContactChannelRemoved { ContactChannelId = "ch-1" });

        state.ContactChannels.Count.ShouldBe(1);
        state.ContactChannels[0].Id.ShouldBe("ch-2");
    }

    [Fact]
    public void Apply_PreferredContactChannelChanged_SetsPreferredAndClearsOthersOfSameType()
    {
        var state = new PartyState();
        state.Apply(new ContactChannelAdded { ContactChannelId = "ch-1", Type = ContactChannelType.Email, Value = "a@b.com", IsPreferred = true });
        state.Apply(new ContactChannelAdded { ContactChannelId = "ch-2", Type = ContactChannelType.Email, Value = "b@b.com" });
        state.Apply(new ContactChannelAdded { ContactChannelId = "ch-3", Type = ContactChannelType.Phone, Value = "123", IsPreferred = true });

        state.Apply(new PreferredContactChannelChanged { ContactChannelId = "ch-2" });

        // ch-1 (Email) — cleared because same type as target
        state.ContactChannels[0].IsPreferred.ShouldBeFalse();
        // ch-2 (Email) — now preferred
        state.ContactChannels[1].IsPreferred.ShouldBeTrue();
        // ch-3 (Phone) — unchanged, different type
        state.ContactChannels[2].IsPreferred.ShouldBeTrue();
    }

    [Fact]
    public void Apply_IdentifierAdded_AddsToList()
    {
        var state = new PartyState();

        state.Apply(new IdentifierAdded { IdentifierId = "id-1", Type = IdentifierType.VAT, Value = "FR123" });

        state.Identifiers.Count.ShouldBe(1);
        state.Identifiers[0].Id.ShouldBe("id-1");
        state.Identifiers[0].Value.ShouldBe("FR123");
    }

    [Fact]
    public void Apply_IdentifierRemoved_RemovesById()
    {
        var state = new PartyState();
        state.Apply(new IdentifierAdded { IdentifierId = "id-1", Type = IdentifierType.VAT, Value = "FR123" });

        state.Apply(new IdentifierRemoved { IdentifierId = "id-1" });

        state.Identifiers.Count.ShouldBe(0);
    }

    [Fact]
    public void Apply_IsNaturalPersonChanged_UpdatesFlag()
    {
        var state = new PartyState();

        state.Apply(new IsNaturalPersonChanged { IsNaturalPerson = true });

        state.IsNaturalPerson.ShouldBeTrue();
    }

    [Fact]
    public void Apply_PartyDeactivated_SetsIsActiveFalse()
    {
        var state = new PartyState();
        state.IsActive.ShouldBeTrue();

        state.Apply(new PartyDeactivated());

        state.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Apply_PartyReactivated_SetsIsActiveTrue()
    {
        var state = new PartyState();
        state.Apply(new PartyDeactivated());

        state.Apply(new PartyReactivated());

        state.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Apply_PartyDisplayNameDerived_SetsNames()
    {
        var state = new PartyState();

        state.Apply(new PartyDisplayNameDerived { DisplayName = "Jane Doe", SortName = "Doe, Jane" });

        state.DisplayName.ShouldBe("Jane Doe");
        state.SortName.ShouldBe("Doe, Jane");
    }

    [Fact]
    public void Apply_PartyMerged_DoesNotThrow()
    {
        var state = new PartyState();

        Should.NotThrow(() => state.Apply(new PartyMerged { SurvivorPartyId = "p1", MergedPartyId = "p2" }));
    }

    [Fact]
    public void NewState_DefaultsToActive()
    {
        var state = new PartyState();

        state.IsActive.ShouldBeTrue();
        state.DisplayName.ShouldBe(string.Empty);
        state.SortName.ShouldBe(string.Empty);
        state.ContactChannels.ShouldBeEmpty();
        state.Identifiers.ShouldBeEmpty();
    }
}
