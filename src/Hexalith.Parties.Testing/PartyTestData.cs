using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Testing;

public static class PartyTestData
{
    public const string DefaultPartyId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

    public static PersonDetails ValidPersonDetails() => new()
    {
        FirstName = "John",
        LastName = "Doe",
    };

    public static OrganizationDetails ValidOrganizationDetails() => new()
    {
        LegalName = "Acme Corp",
        TradingName = "Acme Trading",
    };

    public static CreateParty ValidCreatePerson() => new()
    {
        PartyId = DefaultPartyId,
        Type = PartyType.Person,
        PersonDetails = ValidPersonDetails(),
    };

    public static CreateParty ValidCreateOrganization() => new()
    {
        PartyId = DefaultPartyId,
        Type = PartyType.Organization,
        OrganizationDetails = ValidOrganizationDetails(),
    };

    public static UpdatePersonDetails ValidUpdatePersonDetails() => new()
    {
        PartyId = DefaultPartyId,
        PersonDetails = new PersonDetails
        {
            FirstName = "Jane",
            LastName = "Smith",
        },
    };

    public static UpdateOrganizationDetails ValidUpdateOrganizationDetails() => new()
    {
        PartyId = DefaultPartyId,
        OrganizationDetails = new OrganizationDetails
        {
            LegalName = "New Legal Name",
            TradingName = "New Trading Name",
        },
    };

    public static SetIsNaturalPerson ValidSetIsNaturalPerson(bool value = true) => new()
    {
        PartyId = DefaultPartyId,
        IsNaturalPerson = value,
    };

    public static DeactivateParty ValidDeactivateParty() => new()
    {
        PartyId = DefaultPartyId,
    };

    public static ReactivateParty ValidReactivateParty() => new()
    {
        PartyId = DefaultPartyId,
    };

    public static PartyState CreatePersonState()
    {
        PartyState state = new();
        state.Apply(new PartyCreated
        {
            Type = PartyType.Person,
            PersonDetails = ValidPersonDetails(),
        });
        state.Apply(new PartyDisplayNameDerived
        {
            DisplayName = "John Doe",
            SortName = "Doe, John",
        });
        return state;
    }

    public static PartyState CreateOrganizationState()
    {
        PartyState state = new();
        state.Apply(new PartyCreated
        {
            Type = PartyType.Organization,
            OrganizationDetails = ValidOrganizationDetails(),
        });
        state.Apply(new PartyDisplayNameDerived
        {
            DisplayName = "Acme Corp",
            SortName = "Acme Corp",
        });
        return state;
    }

    public static PartyState CreateDeactivatedPersonState()
    {
        PartyState state = CreatePersonState();
        state.Apply(new PartyDeactivated());
        return state;
    }

    public static PartyState CreateDeactivatedOrganizationState()
    {
        PartyState state = CreateOrganizationState();
        state.Apply(new PartyDeactivated());
        return state;
    }
}
