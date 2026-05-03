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
    public static PartyMemoryUnitMappingContext ForProjection(string tenantId, string caseId)
        => new(tenantId, caseId, SourceService: "Hexalith.Parties");
}

internal static class PartyMemoryUnitMapper
{
    public static PartyMemoryUnit? Map(PartyIndexEntry entry, PartyMemoryUnitMappingContext context)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(context);

        // Erased and inactive parties do not belong in the searchable index. The previous
        // implementation only filtered erased; inactive (deactivated) parties were still
        // pushed to Memories and surfaced in default-mode results that don't set ActiveFilter.
        if (entry.IsErased || !entry.IsActive)
        {
            return null;
        }

        string aggregateId = string.IsNullOrWhiteSpace(context.AggregateId)
            ? entry.Id
            : context.AggregateId!;
        string sourceUri = PartyMemoryUrn.Build(context.TenantId, aggregateId);

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
        _ = builder.AppendLine($"Party: {entry.DisplayName ?? string.Empty}");
        _ = builder.AppendLine($"Party type: {entry.Type}");
        _ = builder.AppendLine($"Party id: {entry.Id}");
        _ = builder.AppendLine($"State: active");
        _ = builder.AppendLine($"Event context: {context.EventType ?? "projection"} from {context.SourceService}");

        if (entry.SearchableContactChannels is not null)
        {
            foreach (ContactChannel channel in entry.SearchableContactChannels)
            {
                if (channel is null)
                {
                    continue;
                }

                _ = builder.AppendLine($"Contact {channel.Type}: {channel.Value}");
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

                _ = builder.AppendLine($"Identifier {identifier.Type}: {identifier.Value}");
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyDictionary<string, MetadataField> BuildMetadata(
        PartyIndexEntry entry,
        PartyMemoryUnitMappingContext context,
        string aggregateId)
    {
        var metadata = new Dictionary<string, MetadataField>(StringComparer.Ordinal)
        {
            ["tenantId"] = Field(context.TenantId),
            ["caseId"] = Field(context.CaseId),
            ["partyId"] = Field(entry.Id),
            ["aggregateId"] = Field(aggregateId),
            ["eventType"] = Field(context.EventType ?? "PartyProjectionChanged"),
            ["sourceService"] = Field(context.SourceService),
            ["partyType"] = Field(entry.Type.ToString()),
            ["displayName"] = Field(entry.DisplayName ?? string.Empty),
            ["isActive"] = Field(entry.IsActive.ToString().ToLowerInvariant()),
            ["isErased"] = Field(entry.IsErased.ToString().ToLowerInvariant()),
            ["createdAt"] = Field(entry.CreatedAt.ToString("O")),
            ["lastModifiedAt"] = Field(entry.LastModifiedAt.ToString("O")),
        };

        if (!string.IsNullOrWhiteSpace(context.CorrelationId))
        {
            metadata["correlationId"] = Field(context.CorrelationId!);
        }

        if (!string.IsNullOrWhiteSpace(context.CausationId))
        {
            metadata["causationId"] = Field(context.CausationId!);
        }

        if (context.Timestamp is not null)
        {
            metadata["timestamp"] = Field(context.Timestamp.Value.ToString("O"));
        }

        return metadata;
    }

    private static MetadataField Field(string value)
        => new(value ?? string.Empty, MetadataOrigin.Human, 1.0f);
}
