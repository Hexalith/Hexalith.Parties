using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Server.Aggregates;

public sealed class PartyAggregate : EventStoreAggregate<PartyState>
{
    public static DomainResult Handle(CreateParty command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.PartyId) || !Guid.TryParse(command.PartyId, out _))
        {
            return DomainResult.Rejection([new PartyCannotBeCreatedWithInvalidId()]);
        }

        // AC#3: Idempotent — if state already exists, party was already created
        if (state is not null)
        {
            return DomainResult.NoOp();
        }

        // AC#4: Reject if no party type specified (default enum = 0)
        if (command.Type == default)
        {
            return DomainResult.Rejection([new PartyCannotBeCreatedWithoutType()]);
        }

        if (command.Type == PartyType.Person && command.PersonDetails is null)
        {
            return DomainResult.Rejection([new PartyCannotBeCreatedWithoutPersonDetails()]);
        }

        if (command.Type == PartyType.Organization && command.OrganizationDetails is null)
        {
            return DomainResult.Rejection([new PartyCannotBeCreatedWithoutOrganizationDetails()]);
        }

        // AC#1 + AC#2: Emit PartyCreated + PartyDisplayNameDerived
        PartyCreated created = new()
        {
            Type = command.Type,
            PersonDetails = command.PersonDetails,
            OrganizationDetails = command.OrganizationDetails,
        };

        (string displayName, string sortName) = DeriveDisplayName(command.Type, command.PersonDetails, command.OrganizationDetails);

        PartyDisplayNameDerived nameDerived = new()
        {
            DisplayName = displayName,
            SortName = sortName,
        };

        return DomainResult.Success([created, nameDerived]);
    }

    public static DomainResult Handle(UpdatePersonDetails command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (state is null)
        {
            return DomainResult.Rejection([new PartyTypeMismatch { Message = "Party does not exist." }]);
        }

        if (state.Type != PartyType.Person)
        {
            return DomainResult.Rejection([new PartyTypeMismatch { Message = $"Cannot update person details on a {state.Type} party." }]);
        }

        if (command.PersonDetails is null)
        {
            return DomainResult.Rejection([new PartyTypeMismatch { Message = "Person details are required." }]);
        }

        PersonDetailsUpdated updated = new()
        {
            PersonDetails = command.PersonDetails,
        };

        (string displayName, string sortName) = DeriveDisplayName(PartyType.Person, command.PersonDetails, null);

        PartyDisplayNameDerived nameDerived = new()
        {
            DisplayName = displayName,
            SortName = sortName,
        };

        return DomainResult.Success([updated, nameDerived]);
    }

    public static DomainResult Handle(UpdateOrganizationDetails command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (state is null)
        {
            return DomainResult.Rejection([new PartyTypeMismatch { Message = "Party does not exist." }]);
        }

        if (state.Type != PartyType.Organization)
        {
            return DomainResult.Rejection([new PartyTypeMismatch { Message = $"Cannot update organization details on a {state.Type} party." }]);
        }

        if (command.OrganizationDetails is null)
        {
            return DomainResult.Rejection([new PartyTypeMismatch { Message = "Organization details are required." }]);
        }

        OrganizationDetailsUpdated updated = new()
        {
            OrganizationDetails = command.OrganizationDetails,
        };

        (string displayName, string sortName) = DeriveDisplayName(PartyType.Organization, null, command.OrganizationDetails);

        PartyDisplayNameDerived nameDerived = new()
        {
            DisplayName = displayName,
            SortName = sortName,
        };

        return DomainResult.Success([updated, nameDerived]);
    }

    public static DomainResult Handle(SetIsNaturalPerson command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (state is null)
        {
            return DomainResult.Rejection([new PartyTypeMismatch { Message = "Party does not exist." }]);
        }

        if (state.Type != PartyType.Organization)
        {
            return DomainResult.Rejection([new PartyTypeMismatch { Message = $"SetIsNaturalPerson only applies to organization parties." }]);
        }

        // Idempotency: no change needed if already at desired value
        if (state.IsNaturalPerson == command.IsNaturalPerson)
        {
            return DomainResult.NoOp();
        }

        IsNaturalPersonChanged changed = new()
        {
            IsNaturalPerson = command.IsNaturalPerson,
        };

        return DomainResult.Success([changed]);
    }

    public static DomainResult Handle(DeactivateParty command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (state is null)
        {
            return DomainResult.Rejection([new PartyCannotBeDeactivatedWhenInactive()]);
        }

        // AC#6: Idempotent — already deactivated
        if (!state.IsActive)
        {
            return DomainResult.NoOp();
        }

        return DomainResult.Success([new PartyDeactivated()]);
    }

    public static DomainResult Handle(ReactivateParty command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (state is null)
        {
            return DomainResult.Rejection([new PartyCannotBeReactivatedWhenActive()]);
        }

        // Idempotent — already active
        if (state.IsActive)
        {
            return DomainResult.NoOp();
        }

        return DomainResult.Success([new PartyReactivated()]);
    }

    private static (string DisplayName, string SortName) DeriveDisplayName(
        PartyType type,
        PersonDetails? person,
        OrganizationDetails? organization)
    {
        return type switch
        {
            PartyType.Person when person is not null =>
                ($"{person.FirstName} {person.LastName}", $"{person.LastName}, {person.FirstName}"),
            PartyType.Organization when organization is not null =>
                (organization.LegalName, organization.LegalName),
            _ => throw new InvalidOperationException($"Unsupported party type: {type}"),
        };
    }
}
