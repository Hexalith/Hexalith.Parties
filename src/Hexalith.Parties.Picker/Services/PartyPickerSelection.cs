using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Picker.Services;

public sealed record PartyPickerSelection
{
    public required string PartyId { get; init; }

    public PartyPickerSelectionState State { get; init; } = PartyPickerSelectionState.Available;

    public string? DisplayName { get; init; }

    public PartyType? PartyType { get; init; }

    public bool? IsActive { get; init; }

    public bool? IsErased { get; init; }

    public string? SafeReason { get; init; }
}
