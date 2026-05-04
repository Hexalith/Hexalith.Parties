using System.Text;

using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.CommandApi.Search;

internal sealed record PartyMemoryUnit(
    string TenantId,
    string CaseId,
    string PartyId,
    string SourceUri,
    SourceType SourceType,
    string Content,
    IReadOnlyDictionary<string, MetadataField> Metadata);

internal sealed record PartyMemoryUnitMappingContext(
    string TenantId,
    string CaseId,
    string? AggregateId = null,
    string? EventType = null,
    string? CorrelationId = null,
    string? CausationId = null,
    string SourceService = "Hexalith.Parties",
    DateTimeOffset? Timestamp = null)
{
    public static PartyMemoryUnitMappingContext ForProjection(
        string tenantId,
        string caseId,
        string? eventType = null,
        string? aggregateId = null,
        string? correlationId = null,
        string? causationId = null,
        DateTimeOffset? timestamp = null)
        => new(
            tenantId,
            caseId,
            aggregateId,
            eventType,
            correlationId,
            causationId,
            Timestamp: timestamp);
}

internal static class PartyMemoryUnitMapper
{
    public static PartyMemoryUnit? Map(PartyIndexEntry entry, PartyMemoryUnitMappingContext context)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(context);

        // Erased parties are removed from the index entirely (the URN is purged via the
        // erasure-cleanup flow). Inactive parties are still indexed so callers passing
        // ActiveFilter=false can find them; the active/erased state is recorded in metadata
        // and content so callers can reason about lifecycle without a separate lookup.
        if (entry.IsErased)
        {
            return null;
        }

        // The canonical source URI is keyed by the authoritative party id (entry.Id), not the
        // aggregate id. Hydration parses the URN as partyId; using AggregateId here would
        // cause every Memories hit to fail the entriesById lookup whenever aggregate and
        // party ids diverge, and would also miss erasure cleanup for the same reason.
        string sourceUri = PartyMemoryUrn.Build(context.TenantId, entry.Id);
        string aggregateId = string.IsNullOrWhiteSpace(context.AggregateId) ? entry.Id : context.AggregateId!;

        return new PartyMemoryUnit(
            context.TenantId,
            context.CaseId,
            entry.Id,
            sourceUri,
            SourceType.Event,
            BuildContent(entry, context),
            BuildMetadata(entry, context, aggregateId));
    }

    private static string BuildContent(PartyIndexEntry entry, PartyMemoryUnitMappingContext context)
    {
        StringBuilder builder = new();
        _ = builder.AppendLine($"Party: {SanitizeLine(entry.DisplayName)}");
        _ = builder.AppendLine($"Party type: {MapPartyTypeToWire(entry.Type)}");
        _ = builder.AppendLine($"Party id: {SanitizeLine(entry.Id)}");
        _ = builder.AppendLine($"State: {(entry.IsActive ? "active" : "inactive")}");
        _ = builder.AppendLine($"Event context: {SanitizeLine(context.EventType ?? "projection")} from {SanitizeLine(context.SourceService)}");

        if (entry.SearchableContactChannels is not null)
        {
            foreach (ContactChannel channel in entry.SearchableContactChannels)
            {
                if (channel is null)
                {
                    continue;
                }

                _ = builder.AppendLine($"Contact {channel.Type}: {SanitizeLine(channel.Value)}");
            }
        }

        if (entry.SearchableIdentifiers is not null)
        {
            foreach (PartyIdentifier identifier in entry.SearchableIdentifiers)
            {
                if (identifier is null)
                {
                    continue;
                }

                _ = builder.AppendLine($"Identifier {identifier.Type}: {SanitizeLine(identifier.Value)}");
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Replace newline characters in user-controlled text so an attacker cannot smuggle
    /// fake structured lines into the content blob (e.g. a DisplayName of
    /// <c>"Alice\nIdentifier SSN: 999-99-9999"</c> would otherwise produce a forged
    /// Identifier line that semantic embeddings treat as real).
    /// </summary>
    private static string SanitizeLine(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }

    private static IReadOnlyDictionary<string, MetadataField> BuildMetadata(
        PartyIndexEntry entry,
        PartyMemoryUnitMappingContext context,
        string aggregateId)
    {
        var metadata = new Dictionary<string, MetadataField>(StringComparer.Ordinal);

        AddRequired(metadata, "tenantId", context.TenantId);
        AddRequired(metadata, "caseId", context.CaseId);
        AddRequired(metadata, "partyId", entry.Id);
        AddRequired(metadata, "aggregateId", aggregateId);
        // EventType is now required at the call site so the cumulative "PartyProjectionChanged"
        // marker is no longer hardcoded — callers thread the real envelope event type through
        // the context, which AC1 requires for "useful event context" in metadata.
        AddOptional(metadata, "eventType", context.EventType);
        AddRequired(metadata, "sourceService", context.SourceService);
        AddRequired(metadata, "partyType", MapPartyTypeToWire(entry.Type));
        AddOptional(metadata, "displayName", entry.DisplayName);
        AddRequired(metadata, "isActive", entry.IsActive ? "true" : "false");
        AddRequired(metadata, "isErased", entry.IsErased ? "true" : "false");
        AddRequired(metadata, "createdAt", entry.CreatedAt.ToString("O"));
        AddRequired(metadata, "lastModifiedAt", entry.LastModifiedAt.ToString("O"));
        AddOptional(metadata, "correlationId", context.CorrelationId);
        AddOptional(metadata, "causationId", context.CausationId);
        if (context.Timestamp is { } ts)
        {
            metadata["timestamp"] = Field(ts.ToString("O"));
        }

        return metadata;
    }

    private static void AddRequired(Dictionary<string, MetadataField> metadata, string key, string value)
        => metadata[key] = Field(value ?? string.Empty);

    private static void AddOptional(Dictionary<string, MetadataField> metadata, string key, string? value)
    {
        // Drop empty-valued optional metadata so Memories does not have to disambiguate
        // "absent" vs "explicit empty" when callers query metadata facets.
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        metadata[key] = Field(value);
    }

    /// <summary>
    /// Map enum values to a stable lowercase wire string. Using <see cref="Enum.ToString()"/>
    /// directly couples persisted metadata to the C# identifier — a future rename would silently
    /// break facet queries against historical memory units.
    /// </summary>
    private static string MapPartyTypeToWire(PartyType type) => type switch
    {
        PartyType.Person => "person",
        PartyType.Organization => "organization",
        _ => type.ToString().ToLowerInvariant(),
    };

    private static MetadataField Field(string value)
        => new(value, MetadataOrigin.Human, 1.0f);
}
