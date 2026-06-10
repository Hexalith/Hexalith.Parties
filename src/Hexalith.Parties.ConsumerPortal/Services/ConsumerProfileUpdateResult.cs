using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.ConsumerPortal.Services;

public sealed record ConsumerProfileUpdateResult(
    ConsumerProfileUpdateOutcome Outcome,
    PartyDetail? Detail = null,
    IReadOnlyList<ConsumerProfileValidationFailure>? ValidationFailures = null);
