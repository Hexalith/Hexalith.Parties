using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Security;

/// <summary>Restores only complete snapshots emitted by the supported Party state writers.</summary>
internal static class PartyStateSnapshotCodec
{
    /// <summary>Maximum accepted serialized Party state bytes.</summary>
    internal const int MaximumBytes = 1024 * 1024;
    /// <summary>Maximum modeled JSON nesting depth.</summary>
    internal const int MaximumDepth = 64;
    private static readonly string[] s_properties =
    [
        "HasBeenCreated", "AgentProvisioning", "HumanActorBindings", "HumanActorTransitions", "HumanBindingVersion",
        "CreatedAt", "Type", "IsActive", "IsNaturalPerson", "DisplayName", "SortName", "Person", "Organization",
        "ContactChannels", "ConsentRecords", "Identifiers", "ErasureStatus", "ErasedAt", "IsRestricted", "RestrictedAt", "RestrictionReason",
    ];
    private static readonly JsonSerializerOptions s_reader = CreateReader();

    /// <summary>Restores modeled fields and collections, refusing any shape that would discard data.</summary>
    /// <param name="serialized">A complete Web or Parties writer object.</param>
    /// <returns>The fully reconstructed typed state.</returns>
    internal static PartyState Restore(JsonElement serialized)
    {
        if (serialized.ValueKind != JsonValueKind.Object
            || System.Text.Encoding.UTF8.GetByteCount(serialized.GetRawText()) > MaximumBytes)
            throw new JsonException("Party snapshot size or shape is unsupported.");
        Validate(serialized, 0);
        PartyState state = serialized.Deserialize<PartyState>(s_reader)
            ?? throw new JsonException("Party snapshot state is missing.");
        // Getter-only collections are modeled explicitly; no replay or silent setter omission.
        Populate<ContactChannel>(state.ContactChannels, serialized, "contactChannels");
        Populate<ConsentRecord>(state.ConsentRecords, serialized, "consentRecords");
        Populate<PartyIdentifier>(state.Identifiers, serialized, "identifiers");
        ValidateModeledState(state);
        JsonNode? input = Normalize(serialized);
        if (!JsonNode.DeepEquals(input, Normalize(JsonSerializer.SerializeToElement(state, JsonSerializerOptions.Web)))
            && !JsonNode.DeepEquals(input, Normalize(JsonSerializer.SerializeToElement(state, PartiesJsonOptions.Default))))
            throw new JsonException("Party snapshot is partial or cannot be restored without loss.");
        return state;
    }

    private static void ValidateModeledState(PartyState state)
    {
        if (state.HumanActorBindings is null || state.HumanActorTransitions is null
            || state.HumanActorBindings.Any(binding => binding is null || binding.Evidence is null
                || string.IsNullOrWhiteSpace(binding.LogicalId) || string.IsNullOrWhiteSpace(binding.IntentDigest))
            || state.HumanActorTransitions.Any(transition => transition is null
                || string.IsNullOrWhiteSpace(transition.LogicalId) || string.IsNullOrWhiteSpace(transition.IntentDigest)))
            throw new JsonException("Party snapshot history is incomplete.");
        foreach (HumanActorBinding binding in state.HumanActorBindings) ValidateEvidence(binding.Evidence);
        foreach (HumanActorBindingTransition transition in state.HumanActorTransitions)
            if (transition.OriginalEvidence is not null) ValidateEvidence(transition.OriginalEvidence);
        if (state.AgentProvisioning is { } provisioning
            && (provisioning.Identity is null || string.IsNullOrWhiteSpace(provisioning.SourceId)
                || string.IsNullOrWhiteSpace(provisioning.Identity.TenantId) || string.IsNullOrWhiteSpace(provisioning.Identity.AgentId)
                || string.IsNullOrWhiteSpace(provisioning.Identity.PartyId) || string.IsNullOrWhiteSpace(provisioning.Identity.LogicalId)
                || string.IsNullOrWhiteSpace(provisioning.Identity.CreationFingerprint)))
            throw new JsonException("Party snapshot provisioning evidence is incomplete.");
        if (!Enum.IsDefined(state.Type) || !Enum.IsDefined(state.ErasureStatus)
            || state.ContactChannels.Any(channel => !Enum.IsDefined(channel.Type))
            || state.ConsentRecords.Any(consent => !Enum.IsDefined(consent.LawfulBasis))
            || state.Identifiers.Any(identifier => !Enum.IsDefined(identifier.Type)))
            throw new JsonException("Party snapshot contains an undefined modeled enum value.");
    }

    private static void ValidateEvidence(HumanActorBindingEvidence evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence.TenantId) || string.IsNullOrWhiteSpace(evidence.PartyId)
            || string.IsNullOrWhiteSpace(evidence.ActorId) || string.IsNullOrWhiteSpace(evidence.SourceId)
            || string.IsNullOrWhiteSpace(evidence.ProvenanceId) || evidence.Custody is null
            || string.IsNullOrWhiteSpace(evidence.Custody.PolicyId) || string.IsNullOrWhiteSpace(evidence.Custody.Purpose)
            || string.IsNullOrWhiteSpace(evidence.Custody.EvidenceId))
            throw new JsonException("Party snapshot binding evidence is incomplete.");
    }

    private static void Populate<T>(IReadOnlyList<T> target, JsonElement serialized, string name)
    {
        if (!serialized.TryGetProperty(name, out JsonElement collection)) throw new JsonException("Party snapshot collection is missing.");
        List<T> items = collection.Deserialize<List<T>>(s_reader)
            ?? throw new JsonException("Party snapshot collection is missing.");
        foreach (T item in items)
        {
            if (item is null) throw new JsonException("Party snapshot collection contains a null item.");
            ((IList<T>)target).Add(item);
        }
    }

    // Both supported writers differ in null omission as well as enum representation.
    // Ignore only null object properties, preserving every non-null field and array slot.
    private static JsonNode? Normalize(JsonElement value)
    {
        JsonNode? node = JsonNode.Parse(value.GetRawText());
        RemoveNullProperties(node);
        return node;
    }

    private static void RemoveNullProperties(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (string key in obj.Select(item => item.Key).ToArray())
                if (obj[key] is null) obj.Remove(key);
                else RemoveNullProperties(obj[key]);
        }
        else if (node is JsonArray array)
            foreach (JsonNode? item in array) RemoveNullProperties(item);
    }

    private static JsonSerializerOptions CreateReader()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(PartyState)) return;
            if (info.Properties.Count != s_properties.Length) throw new InvalidOperationException("Party snapshot model requires an explicit codec update.");
            foreach (JsonPropertyInfo property in info.Properties)
            {
                PropertyInfo member = typeof(PartyState).GetProperty(s_properties.Single(name =>
                    JsonNamingPolicy.CamelCase.ConvertName(name) == property.Name))!;
                if (member.SetMethod is not null) property.Set = member.SetValue;
            }
        });
        var options = new JsonSerializerOptions(PartiesJsonOptions.Default)
        {
            TypeInfoResolver = resolver, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = MaximumDepth,
        };
        return options;
    }

    private static void Validate(JsonElement value, int depth)
    {
        if (depth > MaximumDepth) throw new JsonException("Party snapshot depth exceeds its limit.");
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Party snapshot contains duplicate fields.");
                Validate(property.Value, depth + 1);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray()) Validate(item, depth + 1);
    }
}
