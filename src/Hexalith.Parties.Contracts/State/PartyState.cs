using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.State;

public sealed class PartyState
{
    private readonly List<ContactChannel> _contactChannels = [];
    private readonly List<ConsentRecord> _consentRecords = [];
    private readonly List<PartyIdentifier> _identifiers = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public PartyType Type { get; private set; }

    public bool IsActive { get; private set; } = true;

    public bool IsNaturalPerson { get; private set; }

    [PersonalData]
    public string DisplayName { get; private set; } = string.Empty;

    [PersonalData]
    public string SortName { get; private set; } = string.Empty;

    public PersonDetails? Person { get; private set; }

    public OrganizationDetails? Organization { get; private set; }

    public IReadOnlyList<ContactChannel> ContactChannels => _contactChannels;

    public IReadOnlyList<ConsentRecord> ConsentRecords => _consentRecords;

    public IReadOnlyList<PartyIdentifier> Identifiers => _identifiers;

    public ErasureStatus ErasureStatus { get; private set; } = ErasureStatus.Active;

    public DateTimeOffset? ErasedAt { get; private set; }

    public bool IsRestricted { get; private set; }

    public DateTimeOffset? RestrictedAt { get; private set; }

    public string? RestrictionReason { get; private set; }

    // Rejection events (IRejectionEvent) are persisted to the stream as normal events (EventStore D3)
    // and replayed during state rehydration. They never mutate state, but the rehydrator requires an
    // Apply overload per concrete event type — otherwise it throws MissingApplyMethodException.
    //
    // ORDERING NOTE: these no-op Applies are declared BEFORE the success Applies on purpose. The
    // rehydrator resolves event types by short-name suffix match, iterating method discovery order.
    // For example, event "PartyProcessingRestricted" would otherwise be incorrectly matched to
    // Apply(ProcessingRestricted) because "PartyProcessingRestricted".EndsWith("ProcessingRestricted")
    // is true. Declaring the rejection Applies first ensures their keys appear earlier in the
    // discovery dictionary so the suffix match finds them first.
#pragma warning disable CA1822 // Member does not access instance data — required as instance method for EventStore Apply convention
    public void Apply(PartyNotFound e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(PartyTypeMismatch e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(PartyCannotBeCreatedWithInvalidId e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(PartyCannotBeCreatedWithoutType e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(PartyCannotBeCreatedWithoutPersonDetails e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(PartyCannotBeCreatedWithoutOrganizationDetails e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(PartyCannotBeDeactivatedWhenInactive e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(PartyCannotBeReactivatedWhenActive e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(PartyErasureInProgress e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(PartyProcessingRestricted e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(PartyNotRestricted e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(ContactChannelNotFound e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(IdentifierNotFound e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(ConsentNotFound e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(InvalidConsentPurpose e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(PartyCannotAddDuplicateChannel e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(PartyCannotAddDuplicateIdentifier e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(CompositeOperationConflict e) => ArgumentNullException.ThrowIfNull(e);

    public void Apply(PartyCommandValidationRejected e) => ArgumentNullException.ThrowIfNull(e);
#pragma warning restore CA1822

    public void Apply(PartyCreated e)
    {
        ArgumentNullException.ThrowIfNull(e);
        CreatedAt = DateTimeOffset.UtcNow;
        Type = e.Type;
        Person = e.PersonDetails;
        Organization = e.OrganizationDetails;
        IsNaturalPerson = e.OrganizationDetails?.IsNaturalPerson ?? false;
    }

    public void Apply(PersonDetailsUpdated e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Person = e.PersonDetails;
    }

    public void Apply(OrganizationDetailsUpdated e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Organization = e.OrganizationDetails;
    }

    public void Apply(ContactChannelAdded e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _contactChannels.Add(new ContactChannel
        {
            Id = e.ContactChannelId,
            Type = e.Type,
            Value = e.Value,
            IsPreferred = e.IsPreferred,
        });
    }

    public void Apply(ContactChannelUpdated e)
    {
        ArgumentNullException.ThrowIfNull(e);
        int idx = _contactChannels.FindIndex(c => c.Id == e.ContactChannelId);
        if (idx >= 0)
        {
            ContactChannel existing = _contactChannels[idx];
            _contactChannels[idx] = existing with
            {
                Type = e.Type ?? existing.Type,
                Value = e.Value ?? existing.Value,
                IsPreferred = e.IsPreferred ?? existing.IsPreferred,
            };
        }
    }

    public void Apply(ContactChannelRemoved e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _contactChannels.RemoveAll(c => c.Id == e.ContactChannelId);
    }

    public void Apply(PreferredContactChannelChanged e)
    {
        ArgumentNullException.ThrowIfNull(e);
        int targetIdx = _contactChannels.FindIndex(c => c.Id == e.ContactChannelId);
        if (targetIdx < 0)
        {
            return; // Channel not found — defensive; aggregate Handle should prevent this
        }

        ContactChannelType targetType = _contactChannels[targetIdx].Type;
        for (int i = 0; i < _contactChannels.Count; i++)
        {
            if (_contactChannels[i].Type == targetType)
            {
                _contactChannels[i] = _contactChannels[i] with
                {
                    IsPreferred = _contactChannels[i].Id == e.ContactChannelId,
                };
            }
        }
    }

    public void Apply(IdentifierAdded e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _identifiers.Add(new PartyIdentifier
        {
            Id = e.IdentifierId,
            Type = e.Type,
            Value = e.Value,
        });
    }

    public void Apply(IdentifierRemoved e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _identifiers.RemoveAll(i => i.Id == e.IdentifierId);
    }

    public void Apply(IsNaturalPersonChanged e)
    {
        ArgumentNullException.ThrowIfNull(e);
        IsNaturalPerson = e.IsNaturalPerson;
    }

    public void Apply(PartyDeactivated e)
    {
        ArgumentNullException.ThrowIfNull(e);
        IsActive = false;
    }

    public void Apply(PartyReactivated e)
    {
        ArgumentNullException.ThrowIfNull(e);
        IsActive = true;
    }

    public void Apply(PartyDisplayNameDerived e)
    {
        ArgumentNullException.ThrowIfNull(e);
        DisplayName = e.DisplayName;
        SortName = e.SortName;
    }

    public void Apply(ConsentRecorded e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _consentRecords.Add(new ConsentRecord
        {
            ConsentId = e.ConsentId,
            ChannelId = e.ChannelId,
            Purpose = e.Purpose,
            LawfulBasis = e.LawfulBasis,
            GrantedAt = e.GrantedAt,
            GrantedBy = e.GrantedBy,
            Source = e.Source,
        });
    }

    public void Apply(ConsentRevoked e)
    {
        ArgumentNullException.ThrowIfNull(e);
        int idx = _consentRecords.FindIndex(c => c.ConsentId == e.ConsentId);
        if (idx >= 0)
        {
            _consentRecords[idx] = _consentRecords[idx] with
            {
                RevokedAt = e.RevokedAt,
                RevokedBy = e.RevokedBy,
                RevocationReason = e.Reason,
                RevocationSource = e.Source,
            };
        }
    }

    public void Apply(ProcessingRestricted e)
    {
        ArgumentNullException.ThrowIfNull(e);
        IsRestricted = true;
        RestrictedAt = e.RestrictedAt;
        RestrictionReason = e.Reason;
    }

    public void Apply(RestrictionLifted e)
    {
        ArgumentNullException.ThrowIfNull(e);
        IsRestricted = false;
        RestrictedAt = null;
        RestrictionReason = null;
    }

    public void Apply(ErasePartyRequested e)
    {
        ArgumentNullException.ThrowIfNull(e);
        ErasureStatus = ErasureStatus.ErasurePending;
    }

    public void Apply(PartyEncryptionKeyDeleted e)
    {
        ArgumentNullException.ThrowIfNull(e);
        ErasureStatus = ErasureStatus.KeyDestroyed;
    }

#pragma warning disable CA1822 // Member does not access instance data — key rotation does not change party state
    public void Apply(PartyEncryptionKeyRotated e)
    {
        ArgumentNullException.ThrowIfNull(e);
        // Key rotation is an infrastructure event — no party state change.
        // This Apply method exists for EventStore framework convention.
    }
#pragma warning restore CA1822

    public void Apply(ErasureVerified e)
    {
        ArgumentNullException.ThrowIfNull(e);
        ErasureStatus = ErasureStatus.Verified;
    }

    public void Apply(PartyErased e)
    {
        ArgumentNullException.ThrowIfNull(e);
        ErasureStatus = ErasureStatus.Erased;
        ErasedAt = e.ErasedAt;
    }

#pragma warning disable CA1822 // Member does not access instance data — required as instance method for EventStore Apply convention
    public void Apply(PartyMerged e)
    {
        ArgumentNullException.ThrowIfNull(e);
        // No-op placeholder for v2.
    }
#pragma warning restore CA1822
}
