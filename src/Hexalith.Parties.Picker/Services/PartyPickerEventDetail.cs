namespace Hexalith.Parties.Picker.Services;

public sealed record PartyPickerEventDetail
{
    public required string PartyId { get; init; }

    public string? PartyType { get; init; }

    public string? Status { get; init; }

    public static PartyPickerEventDetail FromSelection(PartyPickerSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return new PartyPickerEventDetail
        {
            PartyId = selection.PartyId,
            PartyType = selection.PartyType?.ToString(),
            Status = selection.IsErased == true
                ? "erased"
                : selection.IsActive == false
                    ? "inactive"
                    : "active",
        };
    }
}
