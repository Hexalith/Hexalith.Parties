namespace Hexalith.Parties.Contracts.Authorization;

public sealed record PartiesClaimExtractionResult
{
    public bool Succeeded { get; init; }

    public string? Value { get; init; }

    public PartiesClaimExtractionFailure Failure { get; init; }

    public static PartiesClaimExtractionResult Success(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new()
        {
            Succeeded = true,
            Value = value,
            Failure = PartiesClaimExtractionFailure.None,
        };
    }

    public static PartiesClaimExtractionResult Missing { get; } = new() { Failure = PartiesClaimExtractionFailure.Missing };

    public static PartiesClaimExtractionResult Empty { get; } = new() { Failure = PartiesClaimExtractionFailure.Empty };

    public static PartiesClaimExtractionResult Ambiguous { get; } = new() { Failure = PartiesClaimExtractionFailure.Ambiguous };
}
