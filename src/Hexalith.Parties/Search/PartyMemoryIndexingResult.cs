namespace Hexalith.Parties.Search;

/// <summary>Reports a party indexing attempt, including mapping or compensation failure.</summary>
/// <param name="PartyId">The party being indexed.</param>
/// <param name="SourceUri">The source URI submitted for ingestion.</param>
/// <param name="WorkflowInstanceId">The accepted ingestion workflow identifier, if any.</param>
/// <param name="Indexed">Whether ingestion and mapping recording succeeded.</param>
/// <param name="FailureReason">A bounded description of the failure, if any.</param>
internal sealed record PartyMemoryIndexingResult(
    string PartyId,
    string SourceUri,
    string? WorkflowInstanceId,
    bool Indexed,
    string? FailureReason);
