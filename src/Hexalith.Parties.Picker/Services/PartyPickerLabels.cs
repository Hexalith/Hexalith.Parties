namespace Hexalith.Parties.Picker.Services;

public sealed record PartyPickerLabels
{
    public string SearchLabel { get; init; } = "Search parties";

    public string Placeholder { get; init; } = "Search parties";

    public string Results { get; init; } = "Party search results";

    public string Loading { get; init; } = "Searching parties";

    public string Idle { get; init; } = "Enter a party name to search";

    public string Empty { get; init; } = "No matching parties";

    public string NoResults { get; init; } = "No matching parties in the current authorized context";

    public string ResultsSummary { get; init; } = "Showing {0} of {1} matching parties";

    public string VisibleResultsSummary { get; init; } = "Showing {0} matching parties";

    public string AuthenticationRequired { get; init; } = "Authentication is required";

    public string Unauthorized { get; init; } = "Sign in again to search parties";

    public string Forbidden { get; init; } = "You do not have access to these parties";

    public string Error { get; init; } = "Parties search is unavailable";

    public string TransientFailure { get; init; } = "Parties search is temporarily unavailable";

    public string LocalOnly { get; init; } = "Local search results";

    public string Degraded { get; init; } = "Limited search results";

    public string Selected { get; init; } = "Selected party";

    public string ClearSelection { get; init; } = "Clear selected party";

    public string Retry { get; init; } = "Retry search";

    public string SelectedRetry { get; init; } = "Retry selected party";

    public string Active { get; init; } = "Active";

    public string Inactive { get; init; } = "Inactive";

    public string Erased { get; init; } = "Erased";

    public string PersonType { get; init; } = "Person";

    public string OrganizationType { get; init; } = "Organization";

    public string UnknownType { get; init; } = "Unknown party type";

    public string SelectedLoading { get; init; } = "Loading selected party";

    public string SelectedAuthenticationRequired { get; init; } = "Authentication is required to view the selected party";

    public string SelectedUnauthorized { get; init; } = "Sign in again to view the selected party";

    public string SelectedForbidden { get; init; } = "Selected party is not available in this authorized context";

    public string SelectedNotFound { get; init; } = "Selected party was not found";

    public string SelectedGone { get; init; } = "Selected party is no longer available";

    public string SelectedTransientFailure { get; init; } = "Selected party details are temporarily unavailable";

    public string SelectedUnavailable { get; init; } = "Selected party details are unavailable";
}
