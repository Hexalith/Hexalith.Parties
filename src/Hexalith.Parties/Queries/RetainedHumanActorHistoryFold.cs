using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.State;

namespace Hexalith.Parties.Queries;

/// <summary>Folds an authenticated sparse attribution partition without reading an erased profile.</summary>
internal static class RetainedHumanActorHistoryFold
{
    /// <summary>Preserves original binding positions and rejects incomplete, foreign or inconsistent lifecycle history.</summary>
    public static IReadOnlyList<RetainedHumanActorBinding> Fold(RetainedIdentityHistoryStream stream, string policyId)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (stream.Identity.Domain != "party" || !RetainedIdentityHistoryValidator.IsComplete(
                new(stream.Identity, RetainedIdentityHistoryReadRequest.AttributionPurpose), stream, stream.ObservedAt))
        {
            throw InvalidHistory();
        }

        var bindings = new List<RetainedHumanActorBinding>();
        var logicalIds = new HashSet<string>(StringComparer.Ordinal);
        long version = 0;
        var transitions = stream.Events.Select(item => (Position: item.SequenceNumber, Event: (StreamReadEvent?)item,
                Expired: (ExpiredIdentityHistoryCertificate?)null))
            .Concat(stream.ExpiredEvents.Select(certificate => (Position: certificate.SourceSequence,
                Event: (StreamReadEvent?)null, Expired: (ExpiredIdentityHistoryCertificate?)certificate)))
            .OrderBy(item => item.Position);
        foreach (var transition in transitions)
        {
            if (transition.Expired is { } expired)
            {
                // Existing ciphertext contract/position and current terminal destruction proof
                // authenticate version continuity without recovering any expired relationship.
                // The accepted fixed effective-at lifetime permits only an expired prefix.
                if (bindings.Count != 0 || expired.PolicyId != policyId) { throw InvalidHistory(); }
                if (expired.EventTypeName == typeof(HumanActorBindingEstablished).FullName)
                {
                    if (version != 0) { throw InvalidHistory(); }
                }
                else if (expired.EventTypeName != typeof(HumanActorBindingRebound).FullName
                    && expired.EventTypeName != typeof(HumanActorBindingRevoked).FullName || version == 0)
                { throw InvalidHistory(); }
                version = checked(version + 1);
                continue;
            }
            StreamReadEvent item = transition.Event ?? throw InvalidHistory();
            string eventName = item.EventTypeName;
            if (eventName == typeof(HumanActorBindingEstablished).FullName)
            {
                HumanActorBindingEstablished value = JsonSerializer.Deserialize<HumanActorBindingEstablished>(item.Payload, PartiesJsonOptions.Default)
                    ?? throw InvalidHistory();
                if (version != 0 || value.ExpectedBindingVersion != 0)
                {
                    throw InvalidHistory();
                }

                Append(value.Binding, value.EffectiveAt, value.ExpectedBindingVersion, item.SequenceNumber);
            }
            else if (eventName == typeof(HumanActorBindingRebound).FullName)
            {
                HumanActorBindingRebound value = JsonSerializer.Deserialize<HumanActorBindingRebound>(item.Payload, PartiesJsonOptions.Default)
                    ?? throw InvalidHistory();
                if (version == 0 || value.ExpectedBindingVersion != version)
                {
                    throw InvalidHistory();
                }

                Close(value.ExpectedBindingVersion, value.EffectiveAt, requireLivePredecessor: false);
                Append(value.Binding, value.EffectiveAt, value.ExpectedBindingVersion, item.SequenceNumber);
            }
            else if (eventName == typeof(HumanActorBindingRevoked).FullName)
            {
                HumanActorBindingRevoked value = JsonSerializer.Deserialize<HumanActorBindingRevoked>(item.Payload, PartiesJsonOptions.Default)
                    ?? throw InvalidHistory();
                if (value.ExpectedBindingVersion != version || !ValidCustody(value.Custody)
                    || value.EffectiveAt > stream.ObservedAt || string.IsNullOrWhiteSpace(value.IntentDigest)
                    || string.IsNullOrWhiteSpace(value.LogicalId) || !logicalIds.Add(value.LogicalId))
                {
                    throw InvalidHistory();
                }

                Close(value.ExpectedBindingVersion, value.EffectiveAt, requireLivePredecessor: true);
                version = checked(version + 1);
            }
            else
            {
                // An excluded profile position is certified separately; a readable profile or unknown
                // event may never be substituted for a retained attribution transition.
                throw InvalidHistory();
            }
        }

        return bindings.AsReadOnly();

        void Append(HumanActorBinding binding, DateTimeOffset at, long expectedVersion, long position)
        {
            HumanActorBindingEvidence? evidence = binding?.Evidence;
            if (evidence is null || evidence.TenantId != stream.Identity.TenantId || evidence.PartyId != stream.Identity.AggregateId
                || expectedVersion != version || evidence.BindingVersion != checked(expectedVersion + 1)
                || evidence.ActorRevision <= 0 || !CanonicalActorId(evidence.ActorId)
                || evidence.ValidFrom != at || at > stream.ObservedAt
                || evidence.ValidUntil is not { } end || end <= at || !ValidCustody(evidence.Custody)
                || end > evidence.Custody.ExpiresAt || string.IsNullOrWhiteSpace(evidence.SourceId)
                || string.IsNullOrWhiteSpace(evidence.ProvenanceId) || string.IsNullOrWhiteSpace(binding!.IntentDigest)
                || string.IsNullOrWhiteSpace(binding.LogicalId) || !logicalIds.Add(binding.LogicalId)
                || bindings.Any(previous => previous.Evidence.ValidFrom >= at || previous.Evidence.ValidUntil > at))
            {
                throw InvalidHistory();
            }

            bindings.Add(new(evidence, position));
            version = evidence.BindingVersion;
        }

        void Close(long expectedVersion, DateTimeOffset at, bool requireLivePredecessor)
        {
            if (version != expectedVersion || at > stream.ObservedAt)
            {
                throw InvalidHistory();
            }

            int index = bindings.FindIndex(binding => binding.Evidence.BindingVersion == expectedVersion);
            if (index < 0)
            {
                if (requireLivePredecessor)
                {
                    throw InvalidHistory();
                }

                return;
            }

            RetainedHumanActorBinding previous = bindings[index];
            if (previous.Evidence.ValidFrom >= at || requireLivePredecessor && previous.Evidence.ValidUntil <= at)
            {
                throw InvalidHistory();
            }

            if (previous.Evidence.ValidUntil > at)
            {
                bindings[index] = previous with { Evidence = previous.Evidence with { ValidUntil = at } };
            }
        }
    }

    private static bool ValidCustody(IdentityHistoryCustodyEvidence? evidence)
        => evidence is { Purpose: RetainedIdentityHistoryReadRequest.AttributionPurpose, LifecycleRevision: > 0,
                SourceExpiryEnforced: true, RestoreSafe: true, DerivedCopiesCovered: true }
            && !string.IsNullOrWhiteSpace(evidence.PolicyId) && !string.IsNullOrWhiteSpace(evidence.EvidenceId);

    private static bool CanonicalActorId(string? actorId)
        => actorId is { Length: 26 } && actorId[0] <= '7'
            && actorId.All(character => "0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(character, StringComparison.Ordinal));

    private static InvalidOperationException InvalidHistory() => new("Retained attribution history is inconsistent.");
}
