using System.Net;

using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;

namespace Hexalith.Parties.Search;

/// <summary>Validates the published workflow output before trusting its canonical identity.</summary>
internal static class PartyMemoryIngestionEvidence
{
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
