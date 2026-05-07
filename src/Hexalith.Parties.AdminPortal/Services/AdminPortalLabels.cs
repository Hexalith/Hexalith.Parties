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

    public string DetailRegion { get; init; } = "Party detail";

    public string SelectParty { get; init; } = "Select a party";

    public string Loading { get; init; } = "Loading parties";

    public string Loaded { get; init; } = "Parties loaded";

    public string DisplayNameOnly { get; init; } = "Display-name search only";

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

    public string DetailUnavailable { get; init; } = "The selected party is unavailable";

    public string DetailErased { get; init; } = "The selected party is erased or no longer inspectable";

    public string DetailLoadFailure { get; init; } = "Detail could not be loaded";
}
