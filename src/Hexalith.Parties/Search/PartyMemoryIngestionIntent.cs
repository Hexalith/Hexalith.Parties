namespace Hexalith.Parties.Search;

/// <summary>Content-free durable obligation created before an ingestion can be submitted.</summary>
/// <param name="AttemptId">Stable opaque idempotency identity for this projection revision and generation.</param>
/// <param name="CaseId">Captured ingestion case.</param>
/// <param name="SourceUri">Captured source identity.</param>
/// <param name="WorkflowInstanceId">Acknowledged workflow, or null for uncertain acceptance.</param>
/// <param name="MemoryUnitId">Validated canonical terminal output, when known.</param>
/// <param name="Generation">Admission generation that reserved this attempt.</param>
/// <param name="SubmissionRejected">Whether submission was definitively rejected before acceptance.</param>
/// <param name="CleanupSettled">Whether scoped terminal ingestion and subsequent absence have already been verified.</param>
internal sealed record PartyMemoryIngestionIntent(
    string AttemptId,
    string CaseId,
    string SourceUri,
    string? WorkflowInstanceId = null,
    string? MemoryUnitId = null,
    long Generation = 0,
    bool SubmissionRejected = false,
    bool CleanupSettled = false);
