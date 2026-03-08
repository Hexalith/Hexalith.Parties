using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.State;

public sealed class PartyState
{
    private readonly List<ContactChannel> _contactChannels = [];
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

    public IReadOnlyList<PartyIdentifier> Identifiers => _identifiers;

    public ErasureStatus ErasureStatus { get; private set; } = ErasureStatus.Active;

    public DateTimeOffset? ErasedAt { get; private set; }

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
