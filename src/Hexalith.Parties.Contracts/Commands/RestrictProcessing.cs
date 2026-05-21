namespace Hexalith.Parties.Contracts.Commands;

public sealed record RestrictProcessing
{
    public required string PartyId { get; init; }

    public required string TenantId { get; init; }

    public string? Reason { get; init; }

    public string? ActorUserId { get; init; }

    public string? CorrelationId { get; init; }
}
