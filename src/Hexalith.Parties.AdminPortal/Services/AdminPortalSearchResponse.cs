using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.AdminPortal.Services;

// TODO(Story 10-1.1): surface ScoreMetadata and SourceMetadata in the detail panel for
// rich-search relevance display. Currently parsed but not propagated to the UI; the empty
// reads keep deserialization permissive against backend payloads that include the fields.
internal sealed record AdminPortalSearchResponse(
    PagedResult<PartySearchResult> Results,
    string? Status,
    string? DegradedReason);
