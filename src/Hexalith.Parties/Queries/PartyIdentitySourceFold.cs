using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
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
        if (stream.Identity is null || stream.Identity.Domain != "party" || stream.Head < 0
            || stream.Events is null || stream.Events.Count != stream.Head || stream.ObservedAt == default
            || string.IsNullOrWhiteSpace(stream.ObservationId))
        {
            throw new InvalidOperationException("Identity source observation is unsupported.");
        }

        var state = new PartyState();
        Assembly contracts = typeof(CreateParty).Assembly;
        long sequence = 0;
        foreach (StreamReadEvent item in stream.Events)
        {
            if (item is null || item.SequenceNumber != ++sequence || item.Payload is null
                || item.SerializationFormat != "json"
                || item.ProtectionMetadata is { } protection && (protection.State is not (PayloadProtectionState.Unprotected or PayloadProtectionState.Protected)
                    || protection.MetadataVersion != EventStorePayloadProtectionMetadata.CurrentMetadataVersion))
            {
                throw new InvalidOperationException("Identity source is incomplete or unreadable.");
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
            DateTimeOffset? effectiveAt = value switch
            {
                HumanActorBindingEstablished established => established.EffectiveAt,
                HumanActorBindingRebound rebound => rebound.EffectiveAt,
                HumanActorBindingRevoked revoked => revoked.EffectiveAt,
                _ => null,
            };
            if (effectiveAt > stream.ObservedAt)
            {
                throw new InvalidOperationException("Identity source attribution follows its observation.");
            }

            if (value is HumanActorBindingRevoked revocation && !HasCompleteCustody(revocation.Custody))
            {
                throw new InvalidOperationException("Identity source revocation custody is unsupported.");
            }

            if (value is AgentPartyProvisioned marker && (marker.Result.Identity.TenantId != stream.Identity.TenantId
                || marker.Result.Identity.PartyId != stream.Identity.AggregateId || marker.Result.ProvisioningRevision != sequence))
            {
                throw new InvalidOperationException("Identity source provenance differs from its stream.");
            }

            _ = apply.Invoke(state, [value]);
        }

        var logicalIds = new HashSet<string>(StringComparer.Ordinal);
        if (sequence != stream.Head || state.HumanActorBindings.Any(binding =>
                binding.Evidence.TenantId != stream.Identity.TenantId || binding.Evidence.PartyId != stream.Identity.AggregateId
                || !HasCompleteAttribution(binding))
            || state.HumanActorTransitions.Any(transition => string.IsNullOrWhiteSpace(transition.LogicalId)
                || string.IsNullOrWhiteSpace(transition.IntentDigest) || !logicalIds.Add(transition.LogicalId)))
        {
            throw new InvalidOperationException("Identity source scope or attribution history is unsupported.");
        }

        return state;
    }

    private static bool HasCompleteAttribution(HumanActorBinding binding)
        => binding.Evidence.ActorRevision > 0 && IsCanonicalActorId(binding.Evidence.ActorId)
            && !string.IsNullOrWhiteSpace(binding.Evidence.SourceId)
            && !string.IsNullOrWhiteSpace(binding.Evidence.ProvenanceId)
            && HasCompleteCustody(binding.Evidence.Custody);

    private static bool HasCompleteCustody(IdentityHistoryCustodyEvidence? evidence)
        => evidence is { Purpose: RetainedIdentityHistoryReadRequest.AttributionPurpose, LifecycleRevision: > 0,
                SourceExpiryEnforced: true, RestoreSafe: true, DerivedCopiesCovered: true }
            && !string.IsNullOrWhiteSpace(evidence.PolicyId) && !string.IsNullOrWhiteSpace(evidence.EvidenceId);

    private static bool IsCanonicalActorId(string? actorId)
        => actorId is { Length: 26 } && actorId[0] <= '7'
            && actorId.All(character => "0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(character, StringComparison.Ordinal));
}
