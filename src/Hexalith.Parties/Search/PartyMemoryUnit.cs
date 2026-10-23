using Hexalith.Memories.Contracts.V1;

namespace Hexalith.Parties.Search;

/// <summary>Content and scope submitted for one Party search revision.</summary>
internal sealed record PartyMemoryUnit(
    string TenantId,
    string CaseId,
    string PartyId,
    string SourceUri,
    SourceType SourceType,
    string Content,
    IReadOnlyDictionary<string, MetadataField> Metadata);
