namespace Hexalith.Parties.Picker.Services;

public sealed record PartyPickerSearchRequest
{
    public Uri? ApiBaseAddress { get; init; }

    public required string Query { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = PartyPickerDefaults.PageSize;

    public PartyPickerSearchMode? Mode { get; init; }

    public string? CaseId { get; init; }

    public Func<CancellationToken, ValueTask<string?>>? AccessTokenProvider { get; init; }

    public Func<HttpRequestMessage, CancellationToken, ValueTask>? RequestCustomizer { get; init; }
}
