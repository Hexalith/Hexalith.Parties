namespace Hexalith.Parties.Picker.Services;

public sealed record PartyPickerLabels
{
    public string SearchLabel { get; init; } = "Search parties";

    public string Placeholder { get; init; } = "Search parties";

    public string Loading { get; init; } = "Searching parties";

    public string Empty { get; init; } = "No matching parties";

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

    public string Active { get; init; } = "Active";

    public string Inactive { get; init; } = "Inactive";

    public string Erased { get; init; } = "Erased";
}
