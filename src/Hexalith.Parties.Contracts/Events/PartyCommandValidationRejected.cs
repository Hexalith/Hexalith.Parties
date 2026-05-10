using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Contracts.Events;

/// <summary>
/// Platform-level rejection event emitted when a Parties command payload fails FluentValidation
/// at the actor-host/domain invoker boundary, before aggregate processing. Carries property
/// paths and stable rule codes so callers can present field-level validation errors. Excludes
/// raw payload fragments and validator-supplied messages to avoid PII leakage in persisted/published events.
/// </summary>
public sealed record PartyCommandValidationRejected : IRejectionEvent
{
    public required string CommandType { get; init; }

    public required IReadOnlyList<PartyValidationFailure> Failures { get; init; }
}

public sealed record PartyValidationFailure
{
    public required string PropertyName { get; init; }

    public required string ErrorCode { get; init; }
}
