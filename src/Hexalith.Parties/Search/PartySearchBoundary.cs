using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Search;

public interface IPartySearchService
{
    Task<PartySearchResponse> SearchAsync(
        PartySearchRequest request,
        IEnumerable<PartyIndexEntry> entries,
        CancellationToken cancellationToken);
}

public sealed record PartySearchRequest(
    string TenantId,
    string Query,
    PartySearchMode Mode,
    PartyType? TypeFilter,
    bool? ActiveFilter,
    int Page,
    int PageSize,
    string? CaseId = null,
    string? GraphContextPartyId = null,
    string? GraphContextMemoryUnitId = null,
    IReadOnlySet<string>? AuthorizedPartyIds = null);

public enum PartySearchMode
{
    Hybrid,
    Lexical,
    Semantic,
    Graph,
}

public sealed record PartySearchResponse(
    PagedResult<PartySearchResult> Results,
    PartySearchExecutionStatus Status,
    string? DegradedReason,
    IReadOnlyList<PartySearchScoreMetadata> ScoreMetadata,
    IReadOnlyList<PartySearchSourceMetadata> SourceMetadata);

public enum PartySearchExecutionStatus
{
    Rich,
    Degraded,
    LocalOnly,
}

public sealed record PartySearchScoreMetadata(
    string PartyId,
    double? RelevanceScore,
    double? LexicalScore,
    double? SemanticScore,
    double? GraphScore,
    double? CompositeScore);

public sealed record PartySearchSourceMetadata(
    string PartyId,
    string SourceSystem,
    string? SourceUri,
    string? MemoryUnitId,
    string? EventType);
