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

        if (entry.IsErased)
        {
            return null;
        }

        string aggregateId = string.IsNullOrWhiteSpace(context.AggregateId)
            ? entry.Id
            : context.AggregateId!;
        string sourceUri = $"urn:hexalith:parties:{context.TenantId}:party:{aggregateId}";

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
        _ = builder.AppendLine($"Party: {entry.DisplayName}");
        _ = builder.AppendLine($"Party type: {entry.Type}");
        _ = builder.AppendLine($"Party id: {entry.Id}");
        _ = builder.AppendLine($"State: {(entry.IsActive ? "active" : "inactive")}, erased: {entry.IsErased}");
        _ = builder.AppendLine($"Event context: {context.EventType ?? "projection"} from {context.SourceService}");

        foreach (ContactChannel channel in entry.SearchableContactChannels)
        {
            _ = builder.AppendLine($"Contact {channel.Type}: {channel.Value}");
        }

        foreach (PartyIdentifier identifier in entry.SearchableIdentifiers)
        {
            _ = builder.AppendLine($"Identifier {identifier.Type}: {identifier.Value}");
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
            ["displayName"] = Field(entry.DisplayName),
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
        => new(value, MetadataOrigin.Human, 1.0f);
}
