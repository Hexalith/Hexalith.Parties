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
            ContactChannelAdded e when state is not null => HandleContactChannelAdded(state, e),
            ContactChannelUpdated e when state is not null => HandleContactChannelUpdated(state, e),
            ContactChannelRemoved e when state is not null => HandleContactChannelRemoved(state, e),
            IdentifierAdded e when state is not null => HandleIdentifierAdded(state, e),
            IdentifierRemoved e when state is not null => HandleIdentifierRemoved(state, e),
            _ => null,
        };
    }

    public static PartyIndexEntry? ApplyErasure(string partyId, PartyIndexEntry? state)
    {
        // Remove entry entirely from index — erased parties should not appear in search
        return null;
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
            SearchableContactChannels = [],
            SearchableIdentifiers = [],
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyIndexEntry HandleContactChannelAdded(PartyIndexEntry state, ContactChannelAdded e)
    {
        ContactChannel channel = new()
        {
            Id = e.ContactChannelId,
            Type = e.Type,
            Value = e.Value,
            IsPreferred = e.IsPreferred,
        };

        List<ContactChannel> channels = [.. state.SearchableContactChannels.Where(c => c.Id != e.ContactChannelId), channel];

        return state with
        {
            SearchableContactChannels = channels,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyIndexEntry? HandleContactChannelUpdated(PartyIndexEntry state, ContactChannelUpdated e)
    {
        List<ContactChannel> channels = [.. state.SearchableContactChannels];
        int index = channels.FindIndex(c => c.Id == e.ContactChannelId);
        if (index < 0)
        {
            return null;
        }

        ContactChannel existing = channels[index];
        ContactChannel updated = existing with
        {
            Type = e.Type ?? existing.Type,
            Value = e.Value ?? existing.Value,
            IsPreferred = e.IsPreferred ?? existing.IsPreferred,
        };

        channels[index] = updated;

        return state with
        {
            SearchableContactChannels = channels,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyIndexEntry HandleContactChannelRemoved(PartyIndexEntry state, ContactChannelRemoved e)
    {
        return state with
        {
            SearchableContactChannels = [.. state.SearchableContactChannels.Where(c => c.Id != e.ContactChannelId)],
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyIndexEntry HandleIdentifierAdded(PartyIndexEntry state, IdentifierAdded e)
    {
        PartyIdentifier identifier = new()
        {
            Id = e.IdentifierId,
            Type = e.Type,
            Value = e.Value,
        };

        List<PartyIdentifier> identifiers = [.. state.SearchableIdentifiers.Where(i => i.Id != e.IdentifierId), identifier];

        return state with
        {
            SearchableIdentifiers = identifiers,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyIndexEntry HandleIdentifierRemoved(PartyIndexEntry state, IdentifierRemoved e)
    {
        return state with
        {
            SearchableIdentifiers = [.. state.SearchableIdentifiers.Where(i => i.Id != e.IdentifierId)],
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
