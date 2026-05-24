namespace Hexalith.Parties.AdminPortal.Services;

public sealed record AdminPortalGdprCapability
{
    public const string ContractUnavailableReason =
        "Blocked on accepted EventStore-fronted Parties client/gateway contract";

    private const string TemporarilyUnavailableReason = "GDPR operations are temporarily unavailable";

    private AdminPortalGdprCapability(
        bool canRequestErasure,
        bool canReadErasureStatus,
        bool canReadErasureCertificate,
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
        CanReadErasureCertificate = canReadErasureCertificate;
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

    public bool CanReadErasureCertificate { get; }

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
            || CanReadErasureCertificate
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
            canReadErasureCertificate: true,
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
            canReadErasureCertificate: false,
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
            canReadErasureCertificate: false,
            canRetryVerification: false,
            canRestrictProcessing: false,
            canLiftRestriction: false,
            canManageConsent: false,
            canExportData: false,
            canReadProcessingRecords: false,
            reason: TemporarilyUnavailableReason);

    // Honest surface of the accepted provisional EventStore-fronted Parties client/gateway bridge:
    // the seven operations whose provisional methods genuinely work are enabled, while erasure
    // certificate retrieval and verification retry stay disabled with the exact bounded blocker
    // (ContractUnavailableReason) because the provisional client reports them contract-unavailable.
    public static AdminPortalGdprCapability ProvisionalBridge()
        => new(
            canRequestErasure: true,
            canReadErasureStatus: true,
            canReadErasureCertificate: false,
            canRetryVerification: false,
            canRestrictProcessing: true,
            canLiftRestriction: true,
            canManageConsent: true,
            canExportData: true,
            canReadProcessingRecords: true,
            reason: ContractUnavailableReason);

    public static AdminPortalGdprCapability Partial(
        bool canRequestErasure = false,
        bool canReadErasureStatus = false,
        bool canRetryVerification = false,
        bool canRestrictProcessing = false,
        bool canLiftRestriction = false,
        bool canManageConsent = false,
        bool canExportData = false,
        bool canReadProcessingRecords = false,
        bool canReadErasureCertificate = false)
        => new(
            canRequestErasure,
            canReadErasureStatus,
            canReadErasureCertificate,
            canRetryVerification,
            canRestrictProcessing,
            canLiftRestriction,
            canManageConsent,
            canExportData,
            canReadProcessingRecords,
            TemporarilyUnavailableReason);
}
