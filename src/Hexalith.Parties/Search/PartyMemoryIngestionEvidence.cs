using System.Net;

using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;

namespace Hexalith.Parties.Search;

/// <summary>Validates the published workflow output before trusting its canonical identity.</summary>
internal static class PartyMemoryIngestionEvidence
{
    /// <summary>Identifies unacknowledged reservations that carry typed refresh admission evidence.</summary>
    internal static bool IsTypedRetryReservation(PartyMemoryIngestionIntent intent)
        => intent.WorkflowInstanceId is null && HasTypedRefreshAdmission(intent);

    /// <summary>Identifies an intent written with typed refresh fields, including incomplete earlier records.</summary>
    internal static bool HasTypedRefreshAdmission(PartyMemoryIngestionIntent intent)
        => intent.RefreshProtocolVersion is not null || intent.SourceRevisionNumber is not null || intent.RefreshIdentityDigest is not null;

    /// <summary>Computes the token hash returned by source inventory for a persisted attempt.</summary>
    internal static string RetryIdentityHash(string attemptId)
        => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(attemptId)));

    /// <summary>Matches source inventory admission evidence to the original durable typed refresh.</summary>
    internal static bool MatchesRefreshAdmission(PartyMemoryIngestionIntent intent,
        string? protocolVersion, long? sourceRevision, string? identityDigest)
        => intent.RefreshProtocolVersion == SourceRefreshProtocol.Version && protocolVersion == intent.RefreshProtocolVersion
            && sourceRevision.HasValue && sourceRevision == intent.SourceRevisionNumber
            && !string.IsNullOrWhiteSpace(identityDigest) && identityDigest == intent.RefreshIdentityDigest;

    /// <summary>Recognizes only selected-server responses issued before reservation or scheduling.</summary>
    internal static bool IsDefiniteSubmissionRejection(MemoriesRemoteException exception)
        => exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || (exception.StatusCode == HttpStatusCode.BadRequest && exception.Error.Code == "INVALID_INPUT")
            || (exception.StatusCode == HttpStatusCode.NotFound && exception.Error.Code == "TENANT_NOT_FOUND")
            || (exception.StatusCode == HttpStatusCode.TooManyRequests && exception.Error.Code == "RATE_LIMIT_EXCEEDED")
            || (exception.StatusCode == HttpStatusCode.Conflict && exception.Error.Code is
                "TENANT_PROVISIONING" or "TENANT_DELETING" or "TENANT_FAILED" or "TENANT_UNAVAILABLE"
                or "SOURCE_REOPEN_CONFLICT" or "SOURCE_ADMISSION_CLOSED" or "SOURCE_REVISION_CONFLICT" or "SOURCE_REVISION_STALE" or "SOURCE_REVISION_IDENTITY_MISMATCH");

    /// <summary>Recognizes the selected producer's scoped terminal no-publication outcome for a compensated revision.</summary>
    internal static bool IsSuperseded(IngestionWorkflowStatus status, string instanceId, string tenantId, string caseId)
        => status.InstanceId == instanceId && status.TenantId == tenantId && status.CaseId == caseId
            && status.RuntimeStatus == "Completed" && status.MemoryUnitStatus == MemoryUnitStatus.Failed
            && status.FailureSummary == "source-refresh-superseded" && !string.IsNullOrWhiteSpace(status.MemoryUnitId);

    /// <summary>Identifies a durable typed revision that can be compared with an independently admitted refresh.</summary>
    internal static bool HasVerifiedRefreshAdmission(PartyMemoryIngestionIntent intent)
        => intent.RefreshProtocolVersion == SourceRefreshProtocol.Version && intent.SourceRevisionNumber is >= 0
            && !string.IsNullOrWhiteSpace(intent.RefreshIdentityDigest);

    /// <summary>Returns only a scoped, successful terminal identity; all uncertain evidence remains unresolved.</summary>
    internal static string? Resolve(IngestionWorkflowStatus? status, string instanceId, string tenantId, string caseId)
        => status is not null
            && string.Equals(status.InstanceId, instanceId, StringComparison.Ordinal)
            && string.Equals(status.TenantId, tenantId, StringComparison.Ordinal)
            && string.Equals(status.CaseId, caseId, StringComparison.Ordinal)
            && string.Equals(status.RuntimeStatus, "Completed", StringComparison.Ordinal)
            && status.MemoryUnitStatus == MemoryUnitStatus.Indexed
            && string.IsNullOrWhiteSpace(status.FailureSummary)
            && !string.IsNullOrWhiteSpace(status.MemoryUnitId)
                ? status.MemoryUnitId : null;
}
