using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Projections.Handlers;

public sealed class PartyDetailProjectionHandler
{
    public static PartyDetail? Apply(string partyId, IEventPayload @event, PartyDetail? state)
    {
        return @event switch
        {
            PartyCreated e => state is null ? HandlePartyCreated(partyId, e) : state,
            PartyDisplayNameDerived e when state is not null => HandleNameDerived(state, e),
            PersonDetailsUpdated e when state is not null => HandlePersonDetailsUpdated(state, e),
            OrganizationDetailsUpdated e when state is not null => HandleOrganizationDetailsUpdated(state, e),
            ContactChannelAdded e when state is not null => HandleContactChannelAdded(state, e),
            ContactChannelUpdated e when state is not null => HandleContactChannelUpdated(state, e),
            ContactChannelRemoved e when state is not null => HandleContactChannelRemoved(state, e),
            PreferredContactChannelChanged e when state is not null => HandlePreferredContactChannelChanged(state, e),
            IdentifierAdded e when state is not null => HandleIdentifierAdded(state, e),
            IdentifierRemoved e when state is not null => HandleIdentifierRemoved(state, e),
            PartyDeactivated when state is not null => HandlePartyDeactivated(state),
            PartyReactivated when state is not null => HandlePartyReactivated(state),
            ConsentRecorded e when state is not null => HandleConsentRecorded(state, e),
            ConsentRevoked e when state is not null => HandleConsentRevoked(state, e),
            ProcessingRestricted e when state is not null => HandleProcessingRestricted(state, e),
            RestrictionLifted when state is not null => HandleRestrictionLifted(state),
            IsNaturalPersonChanged => null,
            PartyMerged => null,
            _ => null,
        };
    }

    public static PartyDetail? ApplyErasure(string partyId, PartyDetail? state)
    {
        if (state is null)
        {
            return null;
        }

        return state with
        {
            DisplayName = string.Empty,
            SortName = string.Empty,
            PersonDetails = null,
            OrganizationDetails = null,
            ContactChannels = [],
            Identifiers = [],
            ConsentRecords = [],
            NameHistory = [],
            IsErased = true,
            ErasedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyDetail HandlePartyCreated(string partyId, PartyCreated e)
    {
        string displayName = DeriveDisplayName(e);
        string sortName = DeriveSortName(e);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new PartyDetail
        {
            Id = partyId,
            Type = e.Type,
            IsActive = true,
            DisplayName = displayName,
            SortName = sortName,
            PersonDetails = e.PersonDetails,
            OrganizationDetails = e.OrganizationDetails,
            NameHistory =
            [
                new NameHistoryEntry
                {
                    DisplayName = displayName,
                    SortName = sortName,
                    ChangedAt = now,
                    TriggeredBy = nameof(PartyCreated),
                },
            ],
            CreatedAt = now,
            LastModifiedAt = now,
        };
    }

    private static PartyDetail? HandleNameDerived(PartyDetail state, PartyDisplayNameDerived e)
    {
        // Deduplicate: skip when neither DisplayName nor SortName has changed.
        // Sort-only changes (locale tweak, contracted family-name spelling) ARE tracked,
        // because directory-style queries that order by SortName need the history.
        if (state.NameHistory.Count > 0 &&
            string.Equals(state.NameHistory[^1].DisplayName, e.DisplayName, StringComparison.Ordinal) &&
            string.Equals(state.NameHistory[^1].SortName, e.SortName, StringComparison.Ordinal))
        {
            return null;
        }

        return state with
        {
            DisplayName = e.DisplayName,
            SortName = e.SortName,
            NameHistory =
            [
                .. state.NameHistory,
                new NameHistoryEntry
                {
                    DisplayName = e.DisplayName,
                    SortName = e.SortName,
                    ChangedAt = DateTimeOffset.UtcNow,
                    TriggeredBy = nameof(PartyDisplayNameDerived),
                },
            ],
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyDetail? HandlePersonDetailsUpdated(PartyDetail state, PersonDetailsUpdated e)
    {
        return state.PersonDetails == e.PersonDetails
            ? null
            : state with
            {
                PersonDetails = e.PersonDetails,
                LastModifiedAt = DateTimeOffset.UtcNow,
            };
    }

    private static PartyDetail? HandleOrganizationDetailsUpdated(PartyDetail state, OrganizationDetailsUpdated e)
    {
        return state.OrganizationDetails == e.OrganizationDetails
            ? null
            : state with
            {
                OrganizationDetails = e.OrganizationDetails,
                LastModifiedAt = DateTimeOffset.UtcNow,
            };
    }

    private static PartyDetail? HandleContactChannelAdded(PartyDetail state, ContactChannelAdded e)
    {
        // Idempotent on replay: if the channel id is already present, skip the append rather
        // than duplicate it. Combined with the per-actor sequence checkpoint, this protects
        // against a host crash between state-key persistence and checkpoint persistence
        // (Dapr batches both writes per turn, but non-transactional state stores can still
        // surface a divergence). The index handler's HandleContactChannelRemoved relies on
        // .Where(c.Id != e.ContactChannelId) so dedup-by-id is the canonical key.
        if (state.ContactChannels.Any(c => c.Id == e.ContactChannelId))
        {
            return null;
        }

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

    private static PartyDetail? HandleContactChannelRemoved(PartyDetail state, ContactChannelRemoved e)
    {
        if (!state.ContactChannels.Any(c => c.Id == e.ContactChannelId))
        {
            return null;
        }

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

    private static PartyDetail? HandleIdentifierAdded(PartyDetail state, IdentifierAdded e)
    {
        // Idempotent on replay (see HandleContactChannelAdded for full rationale).
        if (state.Identifiers.Any(i => i.Id == e.IdentifierId))
        {
            return null;
        }

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

    private static PartyDetail? HandleIdentifierRemoved(PartyDetail state, IdentifierRemoved e)
    {
        if (!state.Identifiers.Any(i => i.Id == e.IdentifierId))
        {
            return null;
        }

        return state with
        {
            Identifiers = state.Identifiers.Where(i => i.Id != e.IdentifierId).ToList(),
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyDetail? HandleConsentRecorded(PartyDetail state, ConsentRecorded e)
    {
        // Idempotent on replay (see HandleContactChannelAdded for full rationale).
        if (state.ConsentRecords.Any(c => c.ConsentId == e.ConsentId))
        {
            return null;
        }

        ConsentRecord record = new()
        {
            ConsentId = e.ConsentId,
            ChannelId = e.ChannelId,
            Purpose = e.Purpose,
            LawfulBasis = e.LawfulBasis,
            GrantedAt = e.GrantedAt,
            GrantedBy = e.GrantedBy,
        };
        return state with
        {
            ConsentRecords = [.. state.ConsentRecords, record],
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyDetail? HandleConsentRevoked(PartyDetail state, ConsentRevoked e)
    {
        List<ConsentRecord> records = state.ConsentRecords.ToList();
        int idx = records.FindIndex(c => c.ConsentId == e.ConsentId);
        if (idx < 0)
        {
            return null;
        }

        records[idx] = records[idx] with
        {
            RevokedAt = e.RevokedAt,
            RevokedBy = e.RevokedBy,
        };

        return state with
        {
            ConsentRecords = records,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyDetail? HandlePartyDeactivated(PartyDetail state)
    {
        return state.IsActive
            ? state with
            {
                IsActive = false,
                LastModifiedAt = DateTimeOffset.UtcNow,
            }
            : null;
    }

    private static PartyDetail? HandlePartyReactivated(PartyDetail state)
    {
        return state.IsActive
            ? null
            : state with
            {
                IsActive = true,
                LastModifiedAt = DateTimeOffset.UtcNow,
            };
    }

    private static PartyDetail? HandleProcessingRestricted(PartyDetail state, ProcessingRestricted e)
    {
        if (state.IsRestricted && state.RestrictedAt == e.RestrictedAt)
        {
            return null;
        }

        return state with
        {
            IsRestricted = true,
            RestrictedAt = e.RestrictedAt,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }

    private static PartyDetail? HandleRestrictionLifted(PartyDetail state)
    {
        return state.IsRestricted || state.RestrictedAt is not null
            ? state with
            {
                IsRestricted = false,
                RestrictedAt = null,
                LastModifiedAt = DateTimeOffset.UtcNow,
            }
            : null;
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
