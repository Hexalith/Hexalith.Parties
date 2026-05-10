namespace Hexalith.Parties.AdminPortal.Services;

public sealed record AdminPortalLabels
{
    public string Title { get; init; } = "Parties";

    public string Search { get; init; } = "Search";

    public string SearchAriaLabel { get; init; } = "Search parties";

    public string SearchPlaceholder { get; init; } = "Display name";

    public string PartyType { get; init; } = "Party type";

    public string ActiveState { get; init; } = "Active state";

    public string AllTypes { get; init; } = "All types";

    public string AllStates { get; init; } = "All states";

    public string Submit { get; init; } = "Search";

    public string Clear { get; init; } = "Clear";

    public string DisplayNameMode { get; init; } = "Display name";

    public string EmailMode { get; init; } = "Email";

    public string IdentifierMode { get; init; } = "Identifier";

    public string PersonOption { get; init; } = "Person";

    public string OrganizationOption { get; init; } = "Organization";

    public string SearchModesRegion { get; init; } = "Search capabilities";

    public string PagingRegion { get; init; } = "Party pages";

    public string Retry { get; init; } = "Retry";

    public string PartiesGrid { get; init; } = "Parties";

    public string DisplayNameColumn { get; init; } = "Display name";

    public string TypeColumn { get; init; } = "Type";

    public string StateColumn { get; init; } = "State";

    public string CreatedColumn { get; init; } = "Created";

    public string ModifiedColumn { get; init; } = "Modified";

    public string Previous { get; init; } = "Previous";

    public string Next { get; init; } = "Next";

    public string Page { get; init; } = "Page";

    public string Of { get; init; } = "of";

    public string TotalCount { get; init; } = "total";

    public string Yes { get; init; } = "Yes";

    public string No { get; init; } = "No";

    public string DetailRegion { get; init; } = "Party detail";

    public string SelectParty { get; init; } = "Select a party";

    public string Loading { get; init; } = "Loading parties";

    public string Loaded { get; init; } = "Parties loaded";

    public string DisplayNameOnly { get; init; } = "Display-name search only";

    public string RichSearchProbeDegraded { get; init; } = "Rich search is temporarily unavailable";

    public string Degraded { get; init; } = "Data may be stale or degraded";

    public string NoParties { get; init; } = "No parties";

    public string NoMatches { get; init; } = "No parties match the current filters";

    public string SignInRequired { get; init; } = "Sign-in is required";

    public string SignInToBrowse { get; init; } = "Sign in to browse parties";

    public string TenantUnavailable { get; init; } = "Tenant context is unavailable";

    public string SelectTenant { get; init; } = "Select a tenant to browse parties";

    public string AdminRequired { get; init; } = "Administrator access is required";

    public string AccessDenied { get; init; } = "Access denied";

    public string TransientFailure { get; init; } = "Party data is temporarily unavailable";

    public string LoadFailure { get; init; } = "Party data could not be loaded";

    public string NoData { get; init; } = "No data available";

    public string Active { get; init; } = "Active";

    public string Inactive { get; init; } = "Inactive";

    public string Erased { get; init; } = "Erased";

    public string Restricted { get; init; } = "restricted";

    public string Summary { get; init; } = "Summary";

    public string Status { get; init; } = "Status";

    public string PersonDetails { get; init; } = "Person details";

    public string FirstName { get; init; } = "First name";

    public string LastName { get; init; } = "Last name";

    public string OrganizationDetails { get; init; } = "Organization details";

    public string LegalName { get; init; } = "Legal name";

    public string TradingName { get; init; } = "Trading name";

    public string LegalForm { get; init; } = "Legal form";

    public string ContactChannels { get; init; } = "Contact channels";

    public string NoContactChannels { get; init; } = "No contact channels";

    public string Preferred { get; init; } = "Preferred";

    public string Standard { get; init; } = "Standard";

    public string Identifiers { get; init; } = "Identifiers";

    public string NoIdentifiers { get; init; } = "No identifiers";

    public string ConsentRecords { get; init; } = "Consent records";

    public string NoConsentRecords { get; init; } = "No consent records";

    public string Revoked { get; init; } = "Revoked";

    public string Restrictions { get; init; } = "Restrictions";

    public string RestrictedAt { get; init; } = "Restricted at";

    public string ErasedAt { get; init; } = "Erased at";

    public string GdprOperations { get; init; } = "GDPR operations";

    public string GdprOperationsUnavailable { get; init; } =
        "GDPR operations are unavailable until the EventStore GDPR client contract is available.";

    public string GdprOperationalSummary { get; init; } = "Operational summary";

    public string GdprRestrictionReason { get; init; } = "Restriction reason";

    public string GdprChannelId { get; init; } = "Channel id";

    public string GdprPurpose { get; init; } = "Purpose";

    public string GdprLawfulBasis { get; init; } = "Lawful basis";

    public string GdprRefreshStatus { get; init; } = "Refresh erasure status";

    public string GdprConfirmErasure { get; init; } = "Confirm erasure";

    public string GdprCancel { get; init; } = "Cancel";

    public string GdprErasureWarning { get; init; } =
        "Erasure destroys protected party data and starts irreversible verification.";

    public string GdprCorrelationId { get; init; } = "Correlation id";

    public string GdprErasureStatus { get; init; } = "Erasure status";

    public string GdprUpdatedAt { get; init; } = "Updated at";

    public string GdprCertificate { get; init; } = "Erasure certificate";

    public string GdprCertificateUnavailable { get; init; } = "Certificate unavailable";

    public string GdprRetryVerification { get; init; } = "Retry verification";

    public string GdprExportPrepared { get; init; } = "Export prepared";

    public string GdprProcessingRecords { get; init; } = "Processing activity records";

    public string GdprNoProcessingRecords { get; init; } = "No processing activity records";

    public string GdprOperationAccepted { get; init; } = "Operation accepted";

    public string GdprOperationCompleted { get; init; } = "Operation completed";

    public string GdprOperationRejected { get; init; } = "Operation rejected";

    public string GdprDpoPendingErasure { get; init; } = "Pending erasure";

    public string GdprDpoRestricted { get; init; } = "Restricted party";

    public string GdprDpoConsentOverview { get; init; } = "Consent records";

    public string GdprDpoAuditTrail { get; init; } = "Audit trail";

    public string GdprTerminalErased { get; init; } = "Party erased";

    public string RequestErasure { get; init; } = "Request erasure";

    public string RestrictProcessing { get; init; } = "Restrict processing";

    public string LiftRestriction { get; init; } = "Lift restriction";

    public string AddConsent { get; init; } = "Add consent";

    public string RevokeConsent { get; init; } = "Revoke consent";

    public string ExportPartyData { get; init; } = "Export party data";

    public string ProcessingRecords { get; init; } = "Processing records";

    public string EventStoreLinks { get; init; } = "EventStore links";

    public string OpenEventStoreStream { get; init; } = "Open EventStore stream";

    public string OpenEventStoreCorrelation { get; init; } = "Open EventStore correlation";

    public string EventStoreAdminUnavailable { get; init; } = "EventStore Admin UI unavailable";

    public string SystemMetadata { get; init; } = "System metadata";

    public string PartyId { get; init; } = "Party id";

    public string SortName { get; init; } = "Sort name";

    public string NameHistory { get; init; } = "Name history";

    public string NoNameHistory { get; init; } = "No name history";

    public string ChangedAt { get; init; } = "Changed at";

    public string TriggeredBy { get; init; } = "Triggered by";

    public string DataAge { get; init; } = "Data age";

    public string DetailUnavailable { get; init; } = "The selected party is unavailable";

    public string DetailErased { get; init; } = "The selected party is erased or no longer inspectable";

    public string DetailLoadFailure { get; init; } = "Detail could not be loaded";

    public string DetailLoading { get; init; } = "Loading party detail";

    public string MissingDate { get; init; } = "—";

    public string ContactPreferred { get; init; } = "Preferred";

    public string ContactStandard { get; init; } = "Standard";

    public string ConsentActive { get; init; } = "Active";

    public string ConsentRevoked { get; init; } = "Revoked";

    public string ValidationProblemPrefix { get; init; } = "Validation";

    public string PartyTypeLabel(string typeName) => typeName switch
    {
        "Person" => PersonOption,
        "Organization" => OrganizationOption,
        _ => typeName,
    };

    // The four enum-translation hooks below are virtual seams for hosts that need to localize
    // contract enum values. Default implementation is identity; overriding requires a derived
    // record. Kept as instance methods (not static) so a subclass record can override them.
#pragma warning disable CA1822 // Members do not access instance data — intentional for override seam.
    public string ContactChannelTypeLabel(string typeName) => typeName;

    public string IdentifierTypeLabel(string typeName) => typeName;

    public string ConsentPurposeLabel(string purposeName) => purposeName;
#pragma warning restore CA1822
}
