using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Security;
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

    public static CreatePartyComposite ValidCreatePersonComposite() => new()
    {
        PartyId = DefaultPartyId,
        Type = PartyType.Person,
        PersonDetails = ValidPersonDetails(),
        ContactChannels =
        [
            new AddContactChannel
            {
                PartyId = DefaultPartyId,
                ContactChannelId = "ch-email-1",
                Type = ContactChannelType.Email,
                Value = "john@example.com",
            },
            new AddContactChannel
            {
                PartyId = DefaultPartyId,
                ContactChannelId = "ch-email-2",
                Type = ContactChannelType.Email,
                Value = "john.alt@example.com",
            },
        ],
        Identifiers =
        [
            new AddIdentifier
            {
                PartyId = DefaultPartyId,
                IdentifierId = "id-vat-1",
                Type = IdentifierType.VAT,
                Value = "FR12345678901",
            },
        ],
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

    public static AddIdentifier ValidAddVatIdentifier() => new()
    {
        PartyId = DefaultPartyId,
        IdentifierId = "id-vat-1",
        Type = IdentifierType.VAT,
        Value = "FR12345678901",
    };

    public static AddIdentifier ValidAddSiretIdentifier() => new()
    {
        PartyId = DefaultPartyId,
        IdentifierId = "id-siret-1",
        Type = IdentifierType.SIRET,
        Value = "12345678901234",
    };

    public static AddIdentifier ValidAddNationalIdIdentifier() => new()
    {
        PartyId = DefaultPartyId,
        IdentifierId = "id-natid-1",
        Type = IdentifierType.NationalId,
        Value = "850101123456789",
    };

    public static RemoveIdentifier ValidRemoveIdentifier() => new()
    {
        PartyId = DefaultPartyId,
        IdentifierId = "id-vat-1",
    };

    public static PartyState CreatePersonStateWithIdentifier()
    {
        PartyState state = CreatePersonState();
        state.Apply(new IdentifierAdded
        {
            IdentifierId = "id-vat-1",
            Type = IdentifierType.VAT,
            Value = "FR12345678901",
        });
        return state;
    }

    public static PartyState CreatePersonStateWithChannelsAndIdentifiers()
    {
        PartyState state = CreatePersonState();
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "ch-email-1",
            Type = ContactChannelType.Email,
            Value = "john@example.com",
            IsPreferred = true,
        });
        state.Apply(new PreferredContactChannelChanged { ContactChannelId = "ch-email-1" });
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "ch-email-2",
            Type = ContactChannelType.Email,
            Value = "john.alt@example.com",
            IsPreferred = false,
        });
        state.Apply(new IdentifierAdded
        {
            IdentifierId = "id-vat-1",
            Type = IdentifierType.VAT,
            Value = "FR12345678901",
        });
        return state;
    }

    public static PartyState CreateOrganizationStateWithChannelsAndIdentifiers()
    {
        PartyState state = CreateOrganizationState();
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "ch-email-1",
            Type = ContactChannelType.Email,
            Value = "info@acme.com",
            IsPreferred = true,
        });
        state.Apply(new PreferredContactChannelChanged { ContactChannelId = "ch-email-1" });
        state.Apply(new IdentifierAdded
        {
            IdentifierId = "id-vat-1",
            Type = IdentifierType.VAT,
            Value = "FR98765432100",
        });
        return state;
    }

    public static CreatePartyComposite ValidCreateOrganizationComposite() => new()
    {
        PartyId = DefaultPartyId,
        Type = PartyType.Organization,
        OrganizationDetails = ValidOrganizationDetails(),
        ContactChannels =
        [
            new AddContactChannel
            {
                PartyId = DefaultPartyId,
                ContactChannelId = "ch-email-1",
                Type = ContactChannelType.Email,
                Value = "info@acme.com",
            },
        ],
        Identifiers =
        [
            new AddIdentifier
            {
                PartyId = DefaultPartyId,
                IdentifierId = "id-vat-1",
                Type = IdentifierType.VAT,
                Value = "FR98765432100",
            },
        ],
    };

    public static UpdatePartyComposite ValidUpdatePersonComposite() => new()
    {
        PartyId = DefaultPartyId,
        PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Smith" },
        AddContactChannels =
        [
            new AddContactChannel
            {
                PartyId = DefaultPartyId,
                ContactChannelId = "ch-phone-1",
                Type = ContactChannelType.Phone,
                Value = "+33111111111",
            },
        ],
        UpdateContactChannels =
        [
            new UpdateContactChannel
            {
                PartyId = DefaultPartyId,
                ContactChannelId = "ch-email-1",
                Value = "john.updated@example.com",
            },
        ],
        RemoveContactChannelIds = ["ch-email-2"],
        AddIdentifiers =
        [
            new AddIdentifier
            {
                PartyId = DefaultPartyId,
                IdentifierId = "id-siret-1",
                Type = IdentifierType.SIRET,
                Value = "12345678901234",
            },
        ],
    };

    public static UpdatePartyComposite ValidUpdateOrganizationComposite() => new()
    {
        PartyId = DefaultPartyId,
        OrganizationDetails = new OrganizationDetails
        {
            LegalName = "New Legal Name",
            TradingName = "New Trading Name",
        },
        AddContactChannels =
        [
            new AddContactChannel
            {
                PartyId = DefaultPartyId,
                ContactChannelId = "ch-phone-1",
                Type = ContactChannelType.Phone,
                Value = "+33222222222",
            },
        ],
    };

    public const string DefaultTenantId = "test-tenant";

    public static EraseParty ValidEraseParty() => new()
    {
        PartyId = DefaultPartyId,
        TenantId = DefaultTenantId,
    };

    public static RotatePartyKey ValidRotatePartyKey(int newVersion = 2, int previousVersion = 1) => new()
    {
        PartyId = DefaultPartyId,
        NewKeyVersion = newVersion,
        PreviousKeyVersion = previousVersion,
    };

    public static PartyState CreateErasurePendingState()
    {
        PartyState state = CreatePersonState();
        state.Apply(new ErasePartyRequested
        {
            PartyId = DefaultPartyId,
            TenantId = DefaultTenantId,
            RequestedAt = DateTimeOffset.UtcNow,
            RequestedBy = "admin",
        });
        return state;
    }

    public static PartyState CreateErasedState()
    {
        PartyState state = CreateErasurePendingState();
        state.Apply(new PartyEncryptionKeyDeleted
        {
            PartyId = DefaultPartyId,
            TenantId = DefaultTenantId,
            DeletedAt = DateTimeOffset.UtcNow,
        });
        state.Apply(new ErasureVerified
        {
            PartyId = DefaultPartyId,
            TenantId = DefaultTenantId,
            VerifiedAt = DateTimeOffset.UtcNow,
            VerificationReportId = "report-1",
        });
        state.Apply(new PartyErased
        {
            PartyId = DefaultPartyId,
            TenantId = DefaultTenantId,
            ErasedAt = DateTimeOffset.UtcNow,
        });
        return state;
    }
}
