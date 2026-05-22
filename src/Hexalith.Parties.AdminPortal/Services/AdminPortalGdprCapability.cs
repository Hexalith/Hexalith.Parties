namespace Hexalith.Parties.AdminPortal.Services;

public sealed record AdminPortalGdprCapability
{
    public const string ContractUnavailableReason =
        "Blocked on accepted EventStore-fronted Parties client/gateway contract";

    private const string TemporarilyUnavailableReason = "GDPR operations are temporarily unavailable";

    private AdminPortalGdprCapability(
        bool canRequestErasure,
        bool canReadErasureStatus,
        bool canRetryVerification,
        bool canRestrictProcessing,
        bool canLiftRestriction,
        bool canManageConsent,
        bool canExportData,
        bool canReadProcessingRecords,
        string reason)
    {
        CanRequestErasure = canRequestErasure;
        CanReadErasureStatus = canReadErasureStatus;
        CanRetryVerification = canRetryVerification;
        CanRestrictProcessing = canRestrictProcessing;
        CanLiftRestriction = canLiftRestriction;
        CanManageConsent = canManageConsent;
        CanExportData = canExportData;
        CanReadProcessingRecords = canReadProcessingRecords;
        Reason = reason;
    }

    public bool CanRequestErasure { get; }

    public bool CanReadErasureStatus { get; }

    public bool CanRetryVerification { get; }

    public bool CanRestrictProcessing { get; }

    public bool CanLiftRestriction { get; }

    public bool CanManageConsent { get; }

    public bool CanExportData { get; }

    public bool CanReadProcessingRecords { get; }

    public string Reason { get; }

    public bool HasAnySupport
        => CanRequestErasure
            || CanReadErasureStatus
            || CanRetryVerification
            || CanRestrictProcessing
            || CanLiftRestriction
            || CanManageConsent
            || CanExportData
            || CanReadProcessingRecords;

    public static AdminPortalGdprCapability Available()
        => new(
            canRequestErasure: true,
            canReadErasureStatus: true,
            canRetryVerification: true,
            canRestrictProcessing: true,
            canLiftRestriction: true,
            canManageConsent: true,
            canExportData: true,
            canReadProcessingRecords: true,
            reason: string.Empty);

    public static AdminPortalGdprCapability Unavailable()
        => new(
            canRequestErasure: false,
            canReadErasureStatus: false,
            canRetryVerification: false,
            canRestrictProcessing: false,
            canLiftRestriction: false,
            canManageConsent: false,
            canExportData: false,
            canReadProcessingRecords: false,
            reason: ContractUnavailableReason);

    public static AdminPortalGdprCapability Degraded()
        => new(
            canRequestErasure: false,
            canReadErasureStatus: false,
            canRetryVerification: false,
            canRestrictProcessing: false,
            canLiftRestriction: false,
            canManageConsent: false,
            canExportData: false,
            canReadProcessingRecords: false,
            reason: TemporarilyUnavailableReason);

    public static AdminPortalGdprCapability Partial(
        bool canRequestErasure = false,
        bool canReadErasureStatus = false,
        bool canRetryVerification = false,
        bool canRestrictProcessing = false,
        bool canLiftRestriction = false,
        bool canManageConsent = false,
        bool canExportData = false,
        bool canReadProcessingRecords = false)
        => new(
            canRequestErasure,
            canReadErasureStatus,
            canRetryVerification,
            canRestrictProcessing,
            canLiftRestriction,
            canManageConsent,
            canExportData,
            canReadProcessingRecords,
            TemporarilyUnavailableReason);
}
