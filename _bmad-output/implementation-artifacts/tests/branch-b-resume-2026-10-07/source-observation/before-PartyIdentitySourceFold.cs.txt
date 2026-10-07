using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;

namespace Hexalith.Parties.Queries;

/// <summary>Pure identity fold of an already verified complete source, never a cache timestamp.</summary>
public static class PartyIdentitySourceFold
{
    /// <summary>Replays exact supported events and checks immutable evidence against stream scope.</summary>
    public static PartyState Fold(AuthoritativeEventStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var state = new PartyState();
        Assembly contracts = typeof(CreateParty).Assembly;
        long sequence = 0;
        foreach (StreamReadEvent item in stream.Events)
        {
            if (item.SequenceNumber != ++sequence)
            {
                throw new InvalidOperationException("Identity source is incomplete.");
            }

            string name = item.EventTypeName.Split(',', 2)[0].Trim();
            Type? type = contracts.GetType(name, throwOnError: false)
                ?? contracts.GetTypes().SingleOrDefault(candidate => candidate.Name == name && typeof(IEventPayload).IsAssignableFrom(candidate));
            MethodInfo? apply = type is null ? null : typeof(PartyState).GetMethod(nameof(PartyState.Apply), [type]);
            if (type is null || apply is null || !typeof(IEventPayload).IsAssignableFrom(type))
            {
                throw new InvalidOperationException("Identity source has unsupported history.");
            }

            object value = JsonSerializer.Deserialize(item.Payload, type, PartiesJsonOptions.Default)
                ?? throw new InvalidOperationException("Identity source has invalid history.");
            if (value is AgentPartyProvisioned marker && (marker.Result.Identity.TenantId != stream.Identity.TenantId
                || marker.Result.Identity.PartyId != stream.Identity.AggregateId || marker.Result.ProvisioningRevision != sequence))
            {
                throw new InvalidOperationException("Identity source provenance differs from its stream.");
            }

            _ = apply.Invoke(state, [value]);
        }

        if (sequence != stream.Head || state.HumanActorBindings.Any(binding =>
                binding.Evidence.TenantId != stream.Identity.TenantId || binding.Evidence.PartyId != stream.Identity.AggregateId))
        {
            throw new InvalidOperationException("Identity source scope differs from history.");
        }

        return state;
    }
}
