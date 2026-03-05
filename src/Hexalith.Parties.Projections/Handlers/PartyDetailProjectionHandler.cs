using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Projections.Handlers;

public sealed class PartyDetailProjectionHandler
{
    public static PartyDetail? Apply(string partyId, IEventPayload @event, PartyDetail? state)
    {
        return @event switch
        {
            PartyCreated e => HandlePartyCreated(partyId, e),
            PartyDisplayNameDerived e when state is not null => state with
            {
                DisplayName = e.DisplayName,
                SortName = e.SortName,
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
            PersonDetailsUpdated e when state is not null => state with
            {
                PersonDetails = e.PersonDetails,
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
            OrganizationDetailsUpdated e when state is not null => state with
            {
                OrganizationDetails = e.OrganizationDetails,
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
            ContactChannelAdded e when state is not null => HandleContactChannelAdded(state, e),
            ContactChannelUpdated e when state is not null => HandleContactChannelUpdated(state, e),
            ContactChannelRemoved e when state is not null => HandleContactChannelRemoved(state, e),
            PreferredContactChannelChanged e when state is not null => HandlePreferredContactChannelChanged(state, e),
            IdentifierAdded e when state is not null => HandleIdentifierAdded(state, e),
            IdentifierRemoved e when state is not null => HandleIdentifierRemoved(state, e),
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
            IsNaturalPersonChanged => null,
            PartyMerged => null,
            _ => null,
        };
    }

    private static PartyDetail HandlePartyCreated(string partyId, PartyCreated e)
    {
        string displayName = DeriveDisplayName(e);
        string sortName = DeriveSortName(e);

        return new PartyDetail
        {
            Id = partyId,
            Type = e.Type,
            IsActive = true,
            DisplayName = displayName,
            SortName = sortName,
            PersonDetails = e.PersonDetails,
            OrganizationDetails = e.OrganizationDetails,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyDetail HandleContactChannelAdded(PartyDetail state, ContactChannelAdded e)
    {
        ContactChannel channel = new()
        {
            Id = e.ContactChannelId,
            Type = e.Type,
            Value = e.Value,
            IsPreferred = e.IsPreferred,
        };
        return state with
        {
            ContactChannels = [.. state.ContactChannels, channel],
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyDetail? HandleContactChannelUpdated(PartyDetail state, ContactChannelUpdated e)
    {
        List<ContactChannel> channels = state.ContactChannels.ToList();
        int idx = channels.FindIndex(c => c.Id == e.ContactChannelId);
        if (idx < 0)
        {
            return null;
        }

        ContactChannel existing = channels[idx];
        ContactChannel updated = existing with
        {
            Type = e.Type ?? existing.Type,
            Value = e.Value ?? existing.Value,
            IsPreferred = e.IsPreferred ?? existing.IsPreferred,
        };

        if (updated == existing)
        {
            return null;
        }

        channels[idx] = updated;

        return state with
        {
            ContactChannels = channels,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyDetail HandleContactChannelRemoved(PartyDetail state, ContactChannelRemoved e)
    {
        return state with
        {
            ContactChannels = state.ContactChannels.Where(c => c.Id != e.ContactChannelId).ToList(),
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyDetail? HandlePreferredContactChannelChanged(PartyDetail state, PreferredContactChannelChanged e)
    {
        List<ContactChannel> channels = state.ContactChannels.ToList();
        int targetIdx = channels.FindIndex(c => c.Id == e.ContactChannelId);
        if (targetIdx < 0)
        {
            return null;
        }

        ContactChannelType targetType = channels[targetIdx].Type;
        bool changed = false;
        for (int i = 0; i < channels.Count; i++)
        {
            if (channels[i].Type == targetType)
            {
                bool shouldBePreferred = channels[i].Id == e.ContactChannelId;
                if (channels[i].IsPreferred != shouldBePreferred)
                {
                    changed = true;
                    channels[i] = channels[i] with
                    {
                        IsPreferred = shouldBePreferred,
                    };
                }
            }
        }

        if (!changed)
        {
            return null;
        }

        return state with
        {
            ContactChannels = channels,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyDetail HandleIdentifierAdded(PartyDetail state, IdentifierAdded e)
    {
        PartyIdentifier identifier = new()
        {
            Id = e.IdentifierId,
            Type = e.Type,
            Value = e.Value,
        };
        return state with
        {
            Identifiers = [.. state.Identifiers, identifier],
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyDetail HandleIdentifierRemoved(PartyDetail state, IdentifierRemoved e)
    {
        return state with
        {
            Identifiers = state.Identifiers.Where(i => i.Id != e.IdentifierId).ToList(),
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

    private static string DeriveSortName(PartyCreated e)
    {
        if (e.Type == PartyType.Person && e.PersonDetails is not null)
        {
            return $"{e.PersonDetails.LastName}, {e.PersonDetails.FirstName}".Trim().TrimEnd(',');
        }

        return e.OrganizationDetails?.LegalName ?? string.Empty;
    }
}
