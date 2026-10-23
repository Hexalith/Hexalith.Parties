using Hexalith.Memories.Contracts.V1;

namespace Hexalith.Parties.Search;

/// <summary>Content and scope submitted for one Party search revision.</summary>
/// <param name="TenantId">The owning tenant.</param>
/// <param name="CaseId">The captured Memories case.</param>
/// <param name="PartyId">The authoritative party identity.</param>
/// <param name="SourceUri">The canonical source identity.</param>
/// <param name="SourceType">The source classification.</param>
/// <param name="Content">Transient searchable content submitted to Memories.</param>
/// <param name="Metadata">The captured submission metadata.</param>
internal sealed record PartyMemoryUnit(
    string TenantId,
    string CaseId,
    string PartyId,
    string SourceUri,
    SourceType SourceType,
    string Content,
    IReadOnlyDictionary<string, MetadataField> Metadata);
