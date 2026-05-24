namespace Hexalith.Parties.Picker.Services;

public enum PartyPickerSelectionState
{
    Available,
    Pending,
    AuthenticationRequired,
    Unauthorized,
    Forbidden,
    NotFound,
    Gone,
    TransientFailure,
    Unavailable,
}
