using System.Globalization;
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

/// <summary>
/// Context attached to every memory unit indexed for a party. <see cref="EventType"/> is
/// required (P15) — callers must thread the originating envelope's <c>EventTypeName</c>
/// through so AC1's "useful event context" metadata is never replaced by a generic marker.
/// Parameter order on the record matches <see cref="ForProjection"/> so positional argument
/// usage cannot silently miswire fields between the two signatures (P38).
/// </summary>
internal sealed record PartyMemoryUnitMappingContext(
    string TenantId,
    string CaseId,
    string EventType,
    string? AggregateId = null,
    string? CorrelationId = null,
    string? CausationId = null,
    string SourceService = "Hexalith.Parties",
    DateTimeOffset? Timestamp = null)
{
    public static PartyMemoryUnitMappingContext ForProjection(
        string tenantId,
        string caseId,
        string eventType,
        string? aggregateId = null,
        string? correlationId = null,
        string? causationId = null,
        DateTimeOffset? timestamp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return new(
            tenantId,
            caseId,
            eventType,
            aggregateId,
            correlationId,
            causationId,
            Timestamp: timestamp);
    }
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
        // only — the content blob deliberately omits "State:" so a literal query like
        // "inactive" cannot match an inactive party via semantic embeddings.
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
        // P20: Active/inactive state is recorded in metadata (`isActive`), not in the
        // content blob. Embedding "State: inactive" as a content line lets a query for
        // "inactive" match inactive parties via semantic embeddings even when the caller
        // did not request `ActiveFilter=false` — contradicting the comment in
        // MemoriesPartySearchService.Hydrate.
        _ = builder.AppendLine($"Event context: {SanitizeLine(context.EventType)} from {SanitizeLine(context.SourceService)}");

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
    /// Replace newline-equivalent characters in user-controlled text so an attacker cannot
    /// smuggle fake structured lines into the content blob (e.g. a DisplayName of
    /// <c>"Alice\nIdentifier SSN: 999-99-9999"</c> would otherwise produce a forged
    /// Identifier line that semantic embeddings treat as real).
    /// <para>
    /// P16: covers the full Unicode line/paragraph-separator surface, not just <c>\r\n</c>.
    /// Strips C0 controls (<c>\v</c>, <c>\f</c>), NEL (<c>U+0085</c>), the line/paragraph
    /// separators (<c>U+2028</c>, <c>U+2029</c>), and other Unicode whitespace categories
    /// that text renderers typically treat as line breaks.
    /// </para>
    /// </summary>
    private static string SanitizeLine(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder sb = new(value.Length);
        foreach (char c in value)
        {
            if (IsLineBreakLike(c))
            {
                _ = sb.Append(' ');
                continue;
            }

            _ = sb.Append(c);
        }

        return sb.ToString();
    }

    private static bool IsLineBreakLike(char c)
    {
        if (c is '\r' or '\n' or '\v' or '\f' or '\u0085')
        {
            return true;
        }

        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category == UnicodeCategory.LineSeparator
            || category == UnicodeCategory.ParagraphSeparator;
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
        // EventType is required at the call site (PartyMemoryUnitMappingContext ctor) so
        // there is no fallback to a hard-coded marker; the originating envelope's event
        // type name flows through directly. This satisfies AC1's "useful event context"
        // metadata clause.
        AddRequired(metadata, "eventType", context.EventType);
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
    /// directly couples persisted metadata to the C# identifier — a future rename would
    /// silently break facet queries against historical memory units. P33: throw on unknown
    /// variants so a new enum value is forced through this switch at compile/test time
    /// rather than silently producing a `enum.ToString().ToLowerInvariant()` wire form.
    /// </summary>
    private static string MapPartyTypeToWire(PartyType type) => type switch
    {
        PartyType.Person => "person",
        PartyType.Organization => "organization",
        _ => throw new InvalidOperationException(
            $"PartyType '{type}' is not mapped to a stable wire string. Add a case to PartyMemoryUnitMapper.MapPartyTypeToWire when extending the enum."),
    };

    private static MetadataField Field(string value)
        => new(value, MetadataOrigin.Human, 1.0f);
}
