namespace Hexalith.Parties.Client.AdminPortal;

public static class AdminPortalGdprRoutes
{
    public const string EraseParty = "eventstore:command:party:EraseParty";
    public const string CancelErasure = "eventstore:command:party:CancelPartyErasure";
    public const string ErasureStatus = "eventstore:query:party:GetErasureStatus";
    public const string ErasureCertificate = "eventstore:query:party:GetErasureCertificate";
    public const string RetryVerification = "eventstore:command:party:RetryErasureVerification";
    public const string RestrictProcessing = "eventstore:command:party:RestrictProcessing";
    public const string LiftRestriction = "eventstore:command:party:LiftRestriction";
    public const string Consent = "eventstore:command:party:AddConsent";
    public const string ConsentById = "eventstore:command:party:RevokeConsent";
    public const string Export = "eventstore:query:party:ExportPartyData";
    public const string ProcessingRecords = "eventstore:query:party:GetProcessingRecords";
}
