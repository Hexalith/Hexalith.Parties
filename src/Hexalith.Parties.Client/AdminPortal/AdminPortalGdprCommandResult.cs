namespace Hexalith.Parties.Client.AdminPortal;

public sealed record AdminPortalGdprCommandResult(
    AdminPortalGdprOutcome Outcome,
    string? CorrelationId,
    string? Detail = null);
