namespace Hexalith.Parties.Picker.Services;

public enum PartyPickerSearchState
{
    Idle,
    Loading,
    AuthenticationRequired,
    Empty,
    Ready,
    LocalOnly,
    Degraded,
    Unauthorized,
    Forbidden,
    NotFound,
    Gone,
    TransientFailure,
    Error,
}
