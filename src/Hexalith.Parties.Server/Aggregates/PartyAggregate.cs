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
