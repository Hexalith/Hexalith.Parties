using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.AdminPortal.Tests.Services;

internal sealed class RecordingAdminPortalApiClient : IPartiesAdminPortalApiClient
{
    private readonly Queue<Func<CancellationToken, Task<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>>>> _listResponses = [];
    private readonly Queue<Func<CancellationToken, Task<AdminPortalQueryResult<PagedResult<PartySearchResult>>>>> _searchResponses = [];
    private readonly Queue<Func<CancellationToken, Task<AdminPortalRichSearchCapability>>> _richSearchCapabilities = [];
    private readonly Queue<Func<CancellationToken, Task<AdminPortalQueryResult<PartyDetail>>>> _detailResponses = [];
    private readonly Queue<Func<CancellationToken, Task<PartyErasureStatusRecord?>>> _erasureStatusResponses = [];
    private readonly Queue<Func<CancellationToken, Task<ErasureCertificate?>>> _erasureCertificateResponses = [];
    private readonly Queue<Func<CancellationToken, Task<IReadOnlyList<ConsentRecord>>>> _consentResponses = [];
    private readonly Queue<Func<CancellationToken, Task<AdminPortalExportDownload>>> _exportResponses = [];
    private readonly Queue<Func<CancellationToken, Task<IReadOnlyList<ProcessingActivityRecord>>>> _processingRecordResponses = [];

    public List<AdminPortalListRequest> ListRequests { get; } = [];

    public List<AdminPortalSearchRequest> SearchRequests { get; } = [];

    public List<string> DetailRequests { get; } = [];

    public List<string> ErasureRequests { get; } = [];

    public List<string> ErasureStatusRequests { get; } = [];

    public List<string> ErasureCertificateRequests { get; } = [];

    public List<string> RetryVerificationRequests { get; } = [];

    public List<(string PartyId, string? Reason)> RestrictionRequests { get; } = [];

    public List<string> LiftRestrictionRequests { get; } = [];

    public List<(string PartyId, string ChannelId, string Purpose, LawfulBasis LawfulBasis)> AddConsentRequests { get; } = [];

    public List<(string PartyId, string ConsentId)> RevokeConsentRequests { get; } = [];

    public List<string> ConsentRequests { get; } = [];

    public List<string> ExportRequests { get; } = [];

    public List<string> ProcessingRecordRequests { get; } = [];

    public int RichSearchCapabilityProbeCount { get; private set; }

    public void EnqueueList(PagedResult<PartyIndexEntry> page, AdminPortalQueryMetadata? metadata = null)
        => _listResponses.Enqueue(_ => Task.FromResult(new AdminPortalQueryResult<PagedResult<PartyIndexEntry>>(page, metadata ?? AdminPortalQueryMetadata.Empty)));

    public void EnqueueListFailure(AdminPortalQueryFailureKind kind)
        => _listResponses.Enqueue(_ => Task.FromException<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>>(new AdminPortalQueryException(kind)));

    public void EnqueueList(Func<CancellationToken, Task<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>>> response)
        => _listResponses.Enqueue(response);

    public void EnqueueSearch(PagedResult<PartySearchResult> page, AdminPortalQueryMetadata? metadata = null)
        => _searchResponses.Enqueue(_ => Task.FromResult(new AdminPortalQueryResult<PagedResult<PartySearchResult>>(page, metadata ?? AdminPortalQueryMetadata.Empty)));

    public void EnqueueRichSearchCapability(AdminPortalRichSearchCapability capability)
        => _richSearchCapabilities.Enqueue(_ => Task.FromResult(capability));

    public void EnqueueDetail(PartyDetail detail, AdminPortalQueryMetadata? metadata = null)
        => _detailResponses.Enqueue(_ => Task.FromResult(new AdminPortalQueryResult<PartyDetail>(detail, metadata ?? AdminPortalQueryMetadata.Empty)));

    public void EnqueueDetailFailure(AdminPortalQueryFailureKind kind)
        => _detailResponses.Enqueue(_ => Task.FromException<AdminPortalQueryResult<PartyDetail>>(new AdminPortalQueryException(kind)));

    public void EnqueueErasureStatus(PartyErasureStatusRecord status)
        => _erasureStatusResponses.Enqueue(_ => Task.FromResult<PartyErasureStatusRecord?>(status));

    public void EnqueueErasureCertificate(ErasureCertificate certificate)
        => _erasureCertificateResponses.Enqueue(_ => Task.FromResult<ErasureCertificate?>(certificate));

    public void EnqueueConsent(params ConsentRecord[] consentRecords)
        => _consentResponses.Enqueue(_ => Task.FromResult<IReadOnlyList<ConsentRecord>>(consentRecords));

    public void EnqueueExport(AdminPortalExportDownload export)
        => _exportResponses.Enqueue(_ => Task.FromResult(export));

    public void EnqueueProcessingRecords(params ProcessingActivityRecord[] records)
        => _processingRecordResponses.Enqueue(_ => Task.FromResult<IReadOnlyList<ProcessingActivityRecord>>(records));

    public Task<AdminPortalQueryResult<PagedResult<PartyIndexEntry>>> ListPartiesAsync(AdminPortalListRequest request, CancellationToken cancellationToken)
    {
        // Record the request as supplied — do not re-bound page-size; tests must assert what
        // the component actually sends so a missing component-side bound surfaces as a test
        // failure rather than being silently masked by stub coercion (P20).
        ListRequests.Add(request);
        return _listResponses.Count == 0
            ? Task.FromResult(new AdminPortalQueryResult<PagedResult<PartyIndexEntry>>(Empty<PartyIndexEntry>(), AdminPortalQueryMetadata.Empty))
            : _listResponses.Dequeue()(cancellationToken);
    }

    public Task<AdminPortalQueryResult<PagedResult<PartySearchResult>>> SearchPartiesAsync(AdminPortalSearchRequest request, CancellationToken cancellationToken)
    {
        SearchRequests.Add(request);
        return _searchResponses.Count == 0
            ? Task.FromResult(new AdminPortalQueryResult<PagedResult<PartySearchResult>>(Empty<PartySearchResult>(), AdminPortalQueryMetadata.Empty))
            : _searchResponses.Dequeue()(cancellationToken);
    }

    public Task<AdminPortalRichSearchCapability> GetRichSearchCapabilityAsync(CancellationToken cancellationToken)
    {
        RichSearchCapabilityProbeCount++;
        return _richSearchCapabilities.Count == 0
            ? Task.FromResult(AdminPortalRichSearchCapability.LocalOnly())
            : _richSearchCapabilities.Dequeue()(cancellationToken);
    }

    public Task<AdminPortalQueryResult<PartyDetail>> GetPartyAsync(string partyId, CancellationToken cancellationToken)
    {
        DetailRequests.Add(partyId);
        return _detailResponses.Count == 0
            ? Task.FromResult(new AdminPortalQueryResult<PartyDetail>(EmptyDetail(partyId), AdminPortalQueryMetadata.Empty))
            : _detailResponses.Dequeue()(cancellationToken);
    }

    public Task<AdminPortalGdprCommandResult> RequestErasureAsync(string partyId, CancellationToken cancellationToken)
    {
        ErasureRequests.Add(partyId);
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-erasure"));
    }

    public Task<PartyErasureStatusRecord?> GetErasureStatusAsync(string partyId, CancellationToken cancellationToken)
    {
        ErasureStatusRequests.Add(partyId);
        return _erasureStatusResponses.Count == 0
            ? Task.FromResult<PartyErasureStatusRecord?>(null)
            : _erasureStatusResponses.Dequeue()(cancellationToken);
    }

    public Task<ErasureCertificate?> GetErasureCertificateAsync(string partyId, CancellationToken cancellationToken)
    {
        ErasureCertificateRequests.Add(partyId);
        return _erasureCertificateResponses.Count == 0
            ? Task.FromResult<ErasureCertificate?>(null)
            : _erasureCertificateResponses.Dequeue()(cancellationToken);
    }

    public Task<AdminPortalGdprCommandResult> RetryErasureVerificationAsync(string partyId, CancellationToken cancellationToken)
    {
        RetryVerificationRequests.Add(partyId);
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-retry"));
    }

    public Task<AdminPortalGdprCommandResult> RestrictProcessingAsync(
        string partyId,
        string? reason,
        CancellationToken cancellationToken)
    {
        RestrictionRequests.Add((partyId, reason));
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-restrict"));
    }

    public Task<AdminPortalGdprCommandResult> LiftRestrictionAsync(string partyId, CancellationToken cancellationToken)
    {
        LiftRestrictionRequests.Add(partyId);
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-lift"));
    }

    public Task<AdminPortalGdprCommandResult> AddConsentAsync(
        string partyId,
        string channelId,
        string purpose,
        LawfulBasis lawfulBasis,
        CancellationToken cancellationToken)
    {
        AddConsentRequests.Add((partyId, channelId, purpose, lawfulBasis));
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-consent"));
    }

    public Task<AdminPortalGdprCommandResult> RevokeConsentAsync(
        string partyId,
        string consentId,
        CancellationToken cancellationToken)
    {
        RevokeConsentRequests.Add((partyId, consentId));
        return Task.FromResult(new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, "corr-revoke"));
    }

    public Task<IReadOnlyList<ConsentRecord>> GetConsentAsync(string partyId, CancellationToken cancellationToken)
    {
        ConsentRequests.Add(partyId);
        return _consentResponses.Count == 0
            ? Task.FromResult<IReadOnlyList<ConsentRecord>>([])
            : _consentResponses.Dequeue()(cancellationToken);
    }

    public Task<AdminPortalExportDownload> ExportPartyDataAsync(string partyId, CancellationToken cancellationToken)
    {
        ExportRequests.Add(partyId);
        return _exportResponses.Count == 0
            ? Task.FromResult(new AdminPortalExportDownload($"party-{partyId}-export.json", "application/json", []))
            : _exportResponses.Dequeue()(cancellationToken);
    }

    public Task<IReadOnlyList<ProcessingActivityRecord>> GetProcessingRecordsAsync(string partyId, CancellationToken cancellationToken)
    {
        ProcessingRecordRequests.Add(partyId);
        return _processingRecordResponses.Count == 0
            ? Task.FromResult<IReadOnlyList<ProcessingActivityRecord>>([])
            : _processingRecordResponses.Dequeue()(cancellationToken);
    }

    private static PagedResult<T> Empty<T>() => new()
    {
        Items = [],
        Page = 1,
        PageSize = 20,
        TotalCount = 0,
        TotalPages = 0,
    };

    private static PartyDetail EmptyDetail(string id) => new()
    {
        Id = id,
        Type = default,
        IsActive = false,
        DisplayName = string.Empty,
        SortName = string.Empty,
        ContactChannels = [],
        Identifiers = [],
        ConsentRecords = [],
        CreatedAt = default,
        LastModifiedAt = default,
    };
}
