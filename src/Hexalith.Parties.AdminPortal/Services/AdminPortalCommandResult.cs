using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.AdminPortal.Services;

public sealed record AdminPortalCommandResult(
    AdminPortalCommandOutcome Outcome,
    string? CorrelationId,
    PartyDetail? Detail = null,
    IReadOnlyList<AdminPortalCommandValidationFailure>? ValidationFailures = null);

public sealed record AdminPortalCommandValidationFailure(string PropertyName, string ErrorCode);

public enum AdminPortalCommandOutcome
{
    Accepted,
    ValidationRejected,
    AuthenticationRequired,
    MissingTenant,
    Forbidden,
    NotFound,
    Erased,
    Conflict,
    TransientFailure,
    ContractUnavailable,
    Unknown,
}
