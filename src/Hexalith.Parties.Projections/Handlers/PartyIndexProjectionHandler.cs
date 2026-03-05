using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Projections.Handlers;

public sealed class PartyIndexProjectionHandler
{
    public static PartyIndexEntry? Apply(string partyId, IEventPayload @event, PartyIndexEntry? state)
    {
        return @event switch
        {
            PartyCreated e => HandlePartyCreated(partyId, e),
            PartyDisplayNameDerived e when state is not null => state with
            {
                DisplayName = e.DisplayName,
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
            PartyDeactivated when state is not null => state with
            {
                IsActive = false,
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
            PartyReactivated when state is not null => state with
            {
                IsActive = true,
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
            ContactChannelAdded when state is not null => state with
            {
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
            ContactChannelRemoved when state is not null => state with
            {
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
            IdentifierAdded when state is not null => state with
            {
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
            IdentifierRemoved when state is not null => state with
            {
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
            _ => null,
        };
    }

    private static PartyIndexEntry HandlePartyCreated(string partyId, PartyCreated e)
    {
        string displayName = DeriveDisplayName(e);
        return new PartyIndexEntry
        {
            Id = partyId,
            Type = e.Type,
            IsActive = true,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static string DeriveDisplayName(PartyCreated e)
    {
        if (e.Type == PartyType.Person && e.PersonDetails is not null)
        {
            return string.Join(' ', [e.PersonDetails.FirstName, e.PersonDetails.LastName]).Trim();
        }

        return e.OrganizationDetails?.LegalName ?? string.Empty;
    }
}
