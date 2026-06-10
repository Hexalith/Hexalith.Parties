using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.ConsumerPortal.Services;

public sealed record ConsumerPrivacyExportResult(
    ConsumerPrivacyExportOutcome Outcome,
    string SafeFileName,
    string ContentType,
    byte[] Payload,
    ConsumerPrivacyExportStatus Status,
    ProjectionFreshnessMetadata? Freshness)
{
    public static ConsumerPrivacyExportResult Failure(ConsumerPrivacyExportOutcome outcome)
        => new(outcome, string.Empty, "application/json", [], ConsumerPrivacyExportStatus.Unknown, null);
}
