namespace Hexalith.Parties.Picker.Services;

public sealed record PartyPickerSelectedPartyRequest
{
    public required string PartyId { get; init; }

    public Func<CancellationToken, ValueTask<string?>>? AccessTokenProvider { get; init; }

    public Func<HttpRequestMessage, CancellationToken, ValueTask>? RequestCustomizer { get; init; }
}
