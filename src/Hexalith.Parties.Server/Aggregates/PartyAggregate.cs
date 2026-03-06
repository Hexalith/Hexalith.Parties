using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Results;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Server.Aggregates;

public sealed class PartyAggregate : EventStoreAggregate<PartyState>
{
    private const int DefaultMaxSubOperations = 100;

    public static int MaxSubOperations { get; set; } = DefaultMaxSubOperations;

    public static int GetEffectiveMaxSubOperations() => MaxSubOperations <= 0 ? DefaultMaxSubOperations : MaxSubOperations;

    public static CompositeCommandResult Handle(CreatePartyComposite command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (MaxSubOperations <= 0)
        {
            MaxSubOperations = DefaultMaxSubOperations;
        }

        // D17: Payload size guard
        int subOps = 1 + command.ContactChannels.Count + command.Identifiers.Count;
        if (subOps > MaxSubOperations)
        {
            return new CompositeCommandResult(
                [new CompositeOperationConflict { Message = $"Payload size exceeded: {subOps} sub-operations (maximum {MaxSubOperations})." }],
                applied: [],
                skipped: [],
                rejected: [$"Payload size exceeded: {subOps} sub-operations (maximum {MaxSubOperations})."]);
        }

        // PartyId validation
        if (string.IsNullOrWhiteSpace(command.PartyId) || !Guid.TryParse(command.PartyId, out _))
        {
            return new CompositeCommandResult(
                [new PartyCannotBeCreatedWithInvalidId()],
                applied: [],
                skipped: [],
                rejected: ["Party ID is invalid."]);
        }

        // Idempotency: party already exists
        if (state is not null)
        {
            return new CompositeCommandResult(
                events: [],
                applied: [],
                skipped: [],
                rejected: []);
        }

        // Type validation
        if (command.Type == default)
        {
            return new CompositeCommandResult(
                [new PartyCannotBeCreatedWithoutType()],
                applied: [],
                skipped: [],
                rejected: ["Party type is required."]);
        }

        // PersonDetails/OrganizationDetails validation
        if (command.Type == PartyType.Person && command.PersonDetails is null)
        {
            return new CompositeCommandResult(
                [new PartyCannotBeCreatedWithoutPersonDetails()],
                applied: [],
                skipped: [],
                rejected: ["Person details are required for person party type."]);
        }

        if (command.Type == PartyType.Organization && command.OrganizationDetails is null)
        {
            return new CompositeCommandResult(
                [new PartyCannotBeCreatedWithoutOrganizationDetails()],
                applied: [],
                skipped: [],
                rejected: ["Organization details are required for organization party type."]);
        }

        for (int i = 0; i < command.ContactChannels.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(command.ContactChannels[i].ContactChannelId))
            {
                return new CompositeCommandResult(
                    [new CompositeOperationConflict { Message = "Contact channel ID is required." }],
                    applied: [],
                    skipped: [],
                    rejected: ["Contact channel ID is required."]);
            }
        }

        for (int i = 0; i < command.Identifiers.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(command.Identifiers[i].IdentifierId))
            {
                return new CompositeCommandResult(
                    [new CompositeOperationConflict { Message = "Identifier ID is required." }],
                    applied: [],
                    skipped: [],
                    rejected: ["Identifier ID is required."]);
            }
        }

        // Emit PartyCreated + PartyDisplayNameDerived
        List<IEventPayload> events = [];
        List<string> applied = [];
        List<string> skipped = [];

        PartyCreated created = new()
        {
            Type = command.Type,
            PersonDetails = command.PersonDetails,
            OrganizationDetails = command.OrganizationDetails,
        };
        events.Add(created);
        applied.Add($"Created {command.Type.ToString().ToLowerInvariant()} party");

        (string displayName, string sortName) = DeriveDisplayName(command.Type, command.PersonDetails, command.OrganizationDetails);

        PartyDisplayNameDerived nameDerived = new()
        {
            DisplayName = displayName,
            SortName = sortName,
        };
        events.Add(nameDerived);
        applied.Add("Derived display name");

        // Process contact channels with duplicate ID detection
        HashSet<string> seenChannelIds = new(StringComparer.Ordinal);
        for (int i = 0; i < command.ContactChannels.Count; i++)
        {
            AddContactChannel channel = command.ContactChannels[i];
            if (!seenChannelIds.Add(channel.ContactChannelId))
            {
                skipped.Add($"Duplicate contact channel: {channel.ContactChannelId}");
                continue;
            }

            ContactChannelAdded channelAdded = new()
            {
                ContactChannelId = channel.ContactChannelId,
                Type = channel.Type,
                Value = channel.Value,
                IsPreferred = channel.IsPreferred,
            };
            events.Add(channelAdded);
            applied.Add($"Added contact channel: {channel.ContactChannelId} ({channel.Type})");

            if (channel.IsPreferred)
            {
                events.Add(new PreferredContactChannelChanged
                {
                    ContactChannelId = channel.ContactChannelId,
                });
                applied.Add($"Set preferred contact channel: {channel.ContactChannelId}");
            }
        }

        // Process identifiers with duplicate ID detection
        HashSet<string> seenIdentifierIds = new(StringComparer.Ordinal);
        for (int i = 0; i < command.Identifiers.Count; i++)
        {
            AddIdentifier identifier = command.Identifiers[i];
            if (!seenIdentifierIds.Add(identifier.IdentifierId))
            {
                skipped.Add($"Duplicate identifier: {identifier.IdentifierId}");
                continue;
            }

            IdentifierAdded identifierAdded = new()
            {
                IdentifierId = identifier.IdentifierId,
                Type = identifier.Type,
                Value = identifier.Value,
            };
            events.Add(identifierAdded);
            applied.Add($"Added identifier: {identifier.IdentifierId} ({identifier.Type})");
        }

        return new CompositeCommandResult(events, applied, skipped, []);
    }

    public static CompositeCommandResult Handle(UpdatePartyComposite command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (MaxSubOperations <= 0)
        {
            MaxSubOperations = DefaultMaxSubOperations;
        }

        // D17: Payload size guard — count all sub-operation lists
        int subOps = (command.PersonDetails is not null ? 1 : 0)
            + (command.OrganizationDetails is not null ? 1 : 0)
            + command.AddContactChannels.Count
            + command.UpdateContactChannels.Count
            + command.RemoveContactChannelIds.Count
            + command.AddIdentifiers.Count
            + command.RemoveIdentifierIds.Count;

        if (subOps > MaxSubOperations)
        {
            return new CompositeCommandResult(
                [new CompositeOperationConflict { Message = $"Payload size exceeded: {subOps} sub-operations (maximum {MaxSubOperations})." }],
                applied: [],
                skipped: [],
                rejected: [$"Payload size exceeded: {subOps} sub-operations (maximum {MaxSubOperations})."]);
        }

        // State null check — party must exist for update
        if (state is null)
        {
            return new CompositeCommandResult(
                [new PartyNotFound { Message = "Party does not exist." }],
                applied: [],
                skipped: [],
                rejected: ["Party does not exist."]);
        }

        // No-op check — all lists empty and no details
        if (subOps == 0)
        {
            return new CompositeCommandResult(
                events: [],
                applied: [],
                skipped: [],
                rejected: []);
        }

        // Build lookup structures from state for O(1) lookups
        Dictionary<string, ContactChannel> existingChannelsById = new(StringComparer.Ordinal);
        for (int i = 0; i < state.ContactChannels.Count; i++)
        {
            existingChannelsById[state.ContactChannels[i].Id] = state.ContactChannels[i];
        }

        HashSet<string> existingIdentifierIds = new(StringComparer.Ordinal);
        for (int i = 0; i < state.Identifiers.Count; i++)
        {
            existingIdentifierIds.Add(state.Identifiers[i].Id);
        }

        // Conflict detection — channel operations
        HashSet<string> addChannelIds = new(StringComparer.Ordinal);
        for (int i = 0; i < command.AddContactChannels.Count; i++)
        {
            addChannelIds.Add(command.AddContactChannels[i].ContactChannelId);
        }

        HashSet<string> updateChannelIds = new(StringComparer.Ordinal);
        for (int i = 0; i < command.UpdateContactChannels.Count; i++)
        {
            updateChannelIds.Add(command.UpdateContactChannels[i].ContactChannelId);
        }

        HashSet<string> removeChannelIds = new(StringComparer.Ordinal);
        List<string> orderedRemoveChannelIds = [];
        List<string> duplicateRemoveChannelIds = [];
        for (int i = 0; i < command.RemoveContactChannelIds.Count; i++)
        {
            string id = command.RemoveContactChannelIds[i];
            if (removeChannelIds.Add(id))
            {
                orderedRemoveChannelIds.Add(id);
            }
            else
            {
                duplicateRemoveChannelIds.Add(id);
            }
        }

        for (int i = 0; i < command.RemoveContactChannelIds.Count; i++)
        {
            string id = command.RemoveContactChannelIds[i];
            if (addChannelIds.Contains(id))
            {
                return new CompositeCommandResult(
                    [new CompositeOperationConflict { Message = $"Conflicting operations on same channel ID: {id}." }],
                    applied: [],
                    skipped: [],
                    rejected: [$"Conflicting operations on same channel ID: {id}."]);
            }

            if (updateChannelIds.Contains(id))
            {
                return new CompositeCommandResult(
                    [new CompositeOperationConflict { Message = $"Conflicting operations on same channel ID: {id}." }],
                    applied: [],
                    skipped: [],
                    rejected: [$"Conflicting operations on same channel ID: {id}."]);
            }
        }

        // Conflict detection — identifier operations
        HashSet<string> addIdentifierIds = new(StringComparer.Ordinal);
        for (int i = 0; i < command.AddIdentifiers.Count; i++)
        {
            addIdentifierIds.Add(command.AddIdentifiers[i].IdentifierId);
        }

        HashSet<string> removeIdentifierIds = new(StringComparer.Ordinal);
        List<string> orderedRemoveIdentifierIds = [];
        List<string> duplicateRemoveIdentifierIds = [];
        for (int i = 0; i < command.RemoveIdentifierIds.Count; i++)
        {
            string id = command.RemoveIdentifierIds[i];
            if (removeIdentifierIds.Add(id))
            {
                orderedRemoveIdentifierIds.Add(id);
            }
            else
            {
                duplicateRemoveIdentifierIds.Add(id);
            }

            if (addIdentifierIds.Contains(id))
            {
                return new CompositeCommandResult(
                    [new CompositeOperationConflict { Message = $"Conflicting operations on same identifier ID: {id}." }],
                    applied: [],
                    skipped: [],
                    rejected: [$"Conflicting operations on same identifier ID: {id}."]);
            }
        }

        // ID validation — blank IDs in all operation lists
        for (int i = 0; i < command.AddContactChannels.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(command.AddContactChannels[i].ContactChannelId))
            {
                return new CompositeCommandResult(
                    [new CompositeOperationConflict { Message = "Contact channel ID is required." }],
                    applied: [],
                    skipped: [],
                    rejected: ["Contact channel ID is required."]);
            }
        }

        for (int i = 0; i < command.UpdateContactChannels.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(command.UpdateContactChannels[i].ContactChannelId))
            {
                return new CompositeCommandResult(
                    [new CompositeOperationConflict { Message = "Contact channel ID is required." }],
                    applied: [],
                    skipped: [],
                    rejected: ["Contact channel ID is required."]);
            }
        }

        for (int i = 0; i < command.RemoveContactChannelIds.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(command.RemoveContactChannelIds[i]))
            {
                return new CompositeCommandResult(
                    [new CompositeOperationConflict { Message = "Contact channel ID is required." }],
                    applied: [],
                    skipped: [],
                    rejected: ["Contact channel ID is required."]);
            }
        }

        for (int i = 0; i < command.AddIdentifiers.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(command.AddIdentifiers[i].IdentifierId))
            {
                return new CompositeCommandResult(
                    [new CompositeOperationConflict { Message = "Identifier ID is required." }],
                    applied: [],
                    skipped: [],
                    rejected: ["Identifier ID is required."]);
            }
        }

        for (int i = 0; i < command.RemoveIdentifierIds.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(command.RemoveIdentifierIds[i]))
            {
                return new CompositeCommandResult(
                    [new CompositeOperationConflict { Message = "Identifier ID is required." }],
                    applied: [],
                    skipped: [],
                    rejected: ["Identifier ID is required."]);
            }
        }

        // Validate UpdateContactChannels IDs exist in state (D12 all-or-nothing)
        for (int i = 0; i < command.UpdateContactChannels.Count; i++)
        {
            if (!existingChannelsById.ContainsKey(command.UpdateContactChannels[i].ContactChannelId))
            {
                return new CompositeCommandResult(
                    [new ContactChannelNotFound { Message = $"Contact channel '{command.UpdateContactChannels[i].ContactChannelId}' not found." }],
                    applied: [],
                    skipped: [],
                    rejected: [$"Contact channel '{command.UpdateContactChannels[i].ContactChannelId}' not found."]);
            }
        }

        // Validate RemoveContactChannelIds exist in state (D12 all-or-nothing)
        for (int i = 0; i < command.RemoveContactChannelIds.Count; i++)
        {
            if (!existingChannelsById.ContainsKey(command.RemoveContactChannelIds[i]))
            {
                return new CompositeCommandResult(
                    [new ContactChannelNotFound { Message = $"Contact channel '{command.RemoveContactChannelIds[i]}' not found." }],
                    applied: [],
                    skipped: [],
                    rejected: [$"Contact channel '{command.RemoveContactChannelIds[i]}' not found."]);
            }
        }

        // Validate RemoveIdentifierIds exist in state (D12 all-or-nothing)
        for (int i = 0; i < command.RemoveIdentifierIds.Count; i++)
        {
            if (!existingIdentifierIds.Contains(command.RemoveIdentifierIds[i]))
            {
                return new CompositeCommandResult(
                    [new IdentifierNotFound { Message = $"Identifier '{command.RemoveIdentifierIds[i]}' not found." }],
                    applied: [],
                    skipped: [],
                    rejected: [$"Identifier '{command.RemoveIdentifierIds[i]}' not found."]);
            }
        }

        // PersonDetails type check
        if (command.PersonDetails is not null && state.Type != PartyType.Person)
        {
            return new CompositeCommandResult(
                [new PartyTypeMismatch { Message = $"Cannot update person details on a {state.Type} party." }],
                applied: [],
                skipped: [],
                rejected: [$"Cannot update person details on a {state.Type} party."]);
        }

        // OrganizationDetails type check
        if (command.OrganizationDetails is not null && state.Type != PartyType.Organization)
        {
            return new CompositeCommandResult(
                [new PartyTypeMismatch { Message = $"Cannot update organization details on a {state.Type} party." }],
                applied: [],
                skipped: [],
                rejected: [$"Cannot update organization details on a {state.Type} party."]);
        }

        // Phase 2 — Event emission (all validation passed)
        List<IEventPayload> events = [];
        List<string> applied = [];
        List<string> skipped = [];

        // PersonDetails update
        if (command.PersonDetails is not null)
        {
            events.Add(new PersonDetailsUpdated { PersonDetails = command.PersonDetails });
            applied.Add("Updated person details");

            (string displayName, string sortName) = DeriveDisplayName(state.Type, command.PersonDetails, null);
            events.Add(new PartyDisplayNameDerived { DisplayName = displayName, SortName = sortName });
            applied.Add("Derived display name");
        }

        // OrganizationDetails update
        if (command.OrganizationDetails is not null)
        {
            events.Add(new OrganizationDetailsUpdated { OrganizationDetails = command.OrganizationDetails });
            applied.Add("Updated organization details");

            (string displayName, string sortName) = DeriveDisplayName(state.Type, null, command.OrganizationDetails);
            events.Add(new PartyDisplayNameDerived { DisplayName = displayName, SortName = sortName });
            applied.Add("Derived display name");
        }

        // AddContactChannels processing with state-duplicate and payload-duplicate detection
        HashSet<string> seenChannelIds = new(StringComparer.Ordinal);
        for (int i = 0; i < command.AddContactChannels.Count; i++)
        {
            AddContactChannel channel = command.AddContactChannels[i];
            if (existingChannelsById.ContainsKey(channel.ContactChannelId))
            {
                skipped.Add($"Duplicate contact channel: {channel.ContactChannelId}");
                continue;
            }

            if (!seenChannelIds.Add(channel.ContactChannelId))
            {
                skipped.Add($"Duplicate contact channel: {channel.ContactChannelId}");
                continue;
            }

            events.Add(new ContactChannelAdded
            {
                ContactChannelId = channel.ContactChannelId,
                Type = channel.Type,
                Value = channel.Value,
                IsPreferred = channel.IsPreferred,
            });
            applied.Add($"Added contact channel: {channel.ContactChannelId} ({channel.Type})");

            if (channel.IsPreferred)
            {
                events.Add(new PreferredContactChannelChanged { ContactChannelId = channel.ContactChannelId });
                applied.Add($"Set preferred contact channel: {channel.ContactChannelId}");
            }
        }

        // UpdateContactChannels processing with within-list dedup and preferred channel logic
        HashSet<string> seenUpdateChannelIds = new(StringComparer.Ordinal);
        for (int i = 0; i < command.UpdateContactChannels.Count; i++)
        {
            UpdateContactChannel channel = command.UpdateContactChannels[i];
            if (!seenUpdateChannelIds.Add(channel.ContactChannelId))
            {
                skipped.Add($"Duplicate contact channel update: {channel.ContactChannelId}");
                continue;
            }

            ContactChannel existingChannel = existingChannelsById[channel.ContactChannelId];

            events.Add(new ContactChannelUpdated
            {
                ContactChannelId = channel.ContactChannelId,
                Type = channel.Type,
                Value = channel.Value,
                IsPreferred = channel.IsPreferred,
            });
            applied.Add($"Updated contact channel: {channel.ContactChannelId}");

            ContactChannelType targetType = channel.Type ?? existingChannel.Type;
            if (channel.IsPreferred == true && (!existingChannel.IsPreferred || targetType != existingChannel.Type))
            {
                events.Add(new PreferredContactChannelChanged { ContactChannelId = channel.ContactChannelId });
                applied.Add($"Set preferred contact channel: {channel.ContactChannelId}");
            }
        }

        // RemoveContactChannelIds processing (using deduplicated order-preserving list from validation)
        for (int i = 0; i < duplicateRemoveChannelIds.Count; i++)
        {
            skipped.Add($"Duplicate contact channel removal: {duplicateRemoveChannelIds[i]}");
        }

        foreach (string id in orderedRemoveChannelIds)
        {
            events.Add(new ContactChannelRemoved { ContactChannelId = id });
            applied.Add($"Removed contact channel: {id}");
        }

        // AddIdentifiers processing with state-duplicate and payload-duplicate detection
        HashSet<string> seenIdentifierIds = new(StringComparer.Ordinal);
        for (int i = 0; i < command.AddIdentifiers.Count; i++)
        {
            AddIdentifier identifier = command.AddIdentifiers[i];
            if (existingIdentifierIds.Contains(identifier.IdentifierId))
            {
                skipped.Add($"Duplicate identifier: {identifier.IdentifierId}");
                continue;
            }

            if (!seenIdentifierIds.Add(identifier.IdentifierId))
            {
                skipped.Add($"Duplicate identifier: {identifier.IdentifierId}");
                continue;
            }

            events.Add(new IdentifierAdded
            {
                IdentifierId = identifier.IdentifierId,
                Type = identifier.Type,
                Value = identifier.Value,
            });
            applied.Add($"Added identifier: {identifier.IdentifierId} ({identifier.Type})");
        }

        // RemoveIdentifierIds processing (using deduplicated order-preserving list from validation)
        for (int i = 0; i < duplicateRemoveIdentifierIds.Count; i++)
        {
            skipped.Add($"Duplicate identifier removal: {duplicateRemoveIdentifierIds[i]}");
        }

        foreach (string id in orderedRemoveIdentifierIds)
        {
            events.Add(new IdentifierRemoved { IdentifierId = id });
            applied.Add($"Removed identifier: {id}");
        }

        PartyDetail updatedDetail = BuildPartyDetailFromState(command.PartyId, state, events);
        return new CompositeCommandResult(events, applied, skipped, [], updatedDetail);
    }

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

    public static DomainResult Handle(AddContactChannel command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (state is null)
        {
            return DomainResult.Rejection([new PartyNotFound()]);
        }

        // Idempotent: skip if channel already exists (D10 — safe for MCP retries)
        if (state.ContactChannels.Any(c => c.Id == command.ContactChannelId))
        {
            return DomainResult.NoOp();
        }

        ContactChannelAdded added = new()
        {
            ContactChannelId = command.ContactChannelId,
            Type = command.Type,
            Value = command.Value,
            IsPreferred = command.IsPreferred,
        };

        // If marked as preferred, emit PreferredContactChannelChanged to clear others of same type
        if (command.IsPreferred)
        {
            return DomainResult.Success([added, new PreferredContactChannelChanged
            {
                ContactChannelId = command.ContactChannelId,
            }]);
        }

        return DomainResult.Success([added]);
    }

    public static DomainResult Handle(UpdateContactChannel command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (state is null)
        {
            return DomainResult.Rejection([new PartyNotFound()]);
        }

        // Channel not found check — use FindIndex to avoid exceptions (no LINQ First/Single)
        int channelIdx = -1;
        for (int i = 0; i < state.ContactChannels.Count; i++)
        {
            if (state.ContactChannels[i].Id == command.ContactChannelId)
            {
                channelIdx = i;
                break;
            }
        }

        if (channelIdx < 0)
        {
            return DomainResult.Rejection([new ContactChannelNotFound { Message = $"Contact channel '{command.ContactChannelId}' not found." }]);
        }

        ContactChannelUpdated updated = new()
        {
            ContactChannelId = command.ContactChannelId,
            Type = command.Type,
            Value = command.Value,
            IsPreferred = command.IsPreferred,
        };

        ContactChannel existingChannel = state.ContactChannels[channelIdx];
        ContactChannelType targetType = command.Type ?? existingChannel.Type;

        // Emit preferred change when explicitly marking preferred and either:
        // 1) channel was not already preferred, or
        // 2) channel type changes (must clear preferred on the new type)
        if (command.IsPreferred == true && (!existingChannel.IsPreferred || targetType != existingChannel.Type))
        {
            return DomainResult.Success([updated, new PreferredContactChannelChanged
            {
                ContactChannelId = command.ContactChannelId,
            }]);
        }

        return DomainResult.Success([updated]);
    }

    public static DomainResult Handle(RemoveContactChannel command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (state is null)
        {
            return DomainResult.Rejection([new PartyNotFound()]);
        }

        // Channel not found check
        if (!state.ContactChannels.Any(c => c.Id == command.ContactChannelId))
        {
            return DomainResult.Rejection([new ContactChannelNotFound { Message = $"Contact channel '{command.ContactChannelId}' not found." }]);
        }

        return DomainResult.Success([new ContactChannelRemoved { ContactChannelId = command.ContactChannelId }]);
    }

    public static DomainResult Handle(AddIdentifier command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (state is null)
        {
            return DomainResult.Rejection([new PartyNotFound()]);
        }

        // Idempotent: skip if identifier already exists (D10 — safe for MCP retries)
        if (state.Identifiers.Any(i => i.Id == command.IdentifierId))
        {
            return DomainResult.NoOp();
        }

        IdentifierAdded added = new()
        {
            IdentifierId = command.IdentifierId,
            Type = command.Type,
            Value = command.Value,
        };

        return DomainResult.Success([added]);
    }

    public static DomainResult Handle(RemoveIdentifier command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (state is null)
        {
            return DomainResult.Rejection([new PartyNotFound()]);
        }

        // Identifier not found check
        if (!state.Identifiers.Any(i => i.Id == command.IdentifierId))
        {
            return DomainResult.Rejection([new IdentifierNotFound { Message = $"Identifier '{command.IdentifierId}' not found." }]);
        }

        return DomainResult.Success([new IdentifierRemoved { IdentifierId = command.IdentifierId }]);
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

    private static PartyDetail BuildPartyDetailFromState(
        string partyId,
        PartyState state,
        IReadOnlyList<IEventPayload> events)
    {
        string displayName = state.DisplayName;
        string sortName = state.SortName;
        PersonDetails? person = state.Person;
        OrganizationDetails? org = state.Organization;
        bool isActive = state.IsActive;
        List<ContactChannel> channels = [.. state.ContactChannels];
        List<PartyIdentifier> identifiers = [.. state.Identifiers];

        foreach (IEventPayload evt in events)
        {
            switch (evt)
            {
                case PersonDetailsUpdated e:
                    person = e.PersonDetails;
                    break;
                case OrganizationDetailsUpdated e:
                    org = e.OrganizationDetails;
                    break;
                case PartyDisplayNameDerived e:
                    displayName = e.DisplayName;
                    sortName = e.SortName;
                    break;
                case ContactChannelAdded e:
                    channels.Add(new ContactChannel { Id = e.ContactChannelId, Type = e.Type, Value = e.Value, IsPreferred = e.IsPreferred });
                    break;
                case ContactChannelUpdated e:
                    {
                        int idx = channels.FindIndex(c => c.Id == e.ContactChannelId);
                        if (idx >= 0)
                        {
                            ContactChannel existing = channels[idx];
                            channels[idx] = existing with
                            {
                                Type = e.Type ?? existing.Type,
                                Value = e.Value ?? existing.Value,
                                IsPreferred = e.IsPreferred ?? existing.IsPreferred,
                            };
                        }

                        break;
                    }

                case ContactChannelRemoved e:
                    channels.RemoveAll(c => c.Id == e.ContactChannelId);
                    break;
                case PreferredContactChannelChanged e:
                    {
                        int targetIdx = channels.FindIndex(c => c.Id == e.ContactChannelId);
                        if (targetIdx >= 0)
                        {
                            ContactChannelType targetType = channels[targetIdx].Type;
                            for (int i = 0; i < channels.Count; i++)
                            {
                                if (channels[i].Type == targetType)
                                {
                                    channels[i] = channels[i] with { IsPreferred = channels[i].Id == e.ContactChannelId };
                                }
                            }
                        }

                        break;
                    }

                case IdentifierAdded e:
                    identifiers.Add(new PartyIdentifier { Id = e.IdentifierId, Type = e.Type, Value = e.Value });
                    break;
                case IdentifierRemoved e:
                    identifiers.RemoveAll(i => i.Id == e.IdentifierId);
                    break;
                case PartyDeactivated:
                    isActive = false;
                    break;
                case PartyReactivated:
                    isActive = true;
                    break;
            }
        }

        return new PartyDetail
        {
            Id = partyId,
            Type = state.Type,
            IsActive = isActive,
            DisplayName = displayName,
            SortName = sortName,
            PersonDetails = person,
            OrganizationDetails = org,
            ContactChannels = channels,
            Identifiers = identifiers,
            CreatedAt = state.CreatedAt,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
    }
}
