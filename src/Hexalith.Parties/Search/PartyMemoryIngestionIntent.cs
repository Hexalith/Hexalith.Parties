namespace Hexalith.Parties.Search;

/// <summary>Content-free durable obligation created before an ingestion can be submitted.</summary>
/// <param name="AttemptId">Stable opaque idempotency identity for this projection revision and generation.</param>
/// <param name="CaseId">Captured ingestion case.</param>
/// <param name="SourceUri">Captured source identity.</param>
/// <param name="WorkflowInstanceId">Acknowledged workflow, or null for uncertain acceptance.</param>
/// <param name="MemoryUnitId">Canonical output, when known; imported output can still require terminal evidence.</param>
/// <param name="Generation">Admission generation that reserved this attempt.</param>
/// <param name="SubmissionRejected">Whether submission was definitively rejected before acceptance.</param>
/// <param name="CleanupSettled">Whether scoped terminal ingestion and subsequent absence have already been verified.</param>
/// <param name="RequiresTerminalEvidence">Whether output is known but the admitted producer still needs independent terminal proof.</param>
/// <param name="ProducerAuthority">Qualified producer endpoint for an admitted/discovered obligation; null preserves unknown legacy origin.</param>
/// <param name="AbsenceAuthority">Producer endpoint where terminal physical absence was independently verified.</param>
/// <param name="RefreshProtocolVersion">Explicit refresh protocol retained before submission; null leaves legacy delivery unproved.</param>
/// <param name="SourceRevisionNumber">Authoritative canonical sequence retained before submission.</param>
/// <param name="RefreshIdentityDigest">Original admitted body digest, independent of later delivery context and ignored collections.</param>
/// <param name="SuccessorAttemptId">Durably linked retry after a definitively compensated source publication conflict.</param>
internal sealed record PartyMemoryIngestionIntent(
    string AttemptId,
    string CaseId,
    string SourceUri,
    string? WorkflowInstanceId = null,
    string? MemoryUnitId = null,
    long Generation = 0,
    bool SubmissionRejected = false,
    bool CleanupSettled = false,
    bool RequiresTerminalEvidence = false,
    string? SuccessorAttemptId = null,
    string? ProducerAuthority = null,
    string? AbsenceAuthority = null,
    string? RefreshProtocolVersion = null,
    long? SourceRevisionNumber = null,
    string? RefreshIdentityDigest = null);
