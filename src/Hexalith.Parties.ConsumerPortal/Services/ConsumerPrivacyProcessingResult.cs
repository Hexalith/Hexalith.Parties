using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.ConsumerPortal.Services;

public sealed record ConsumerPrivacyProcessingResult(
    ConsumerPrivacyProcessingOutcome Outcome,
    IReadOnlyList<ConsumerPrivacyProcessingRecord> Records,
    ProjectionFreshnessMetadata? Freshness = null)
{
    public static ConsumerPrivacyProcessingResult FromRecords(
        IReadOnlyList<ConsumerPrivacyProcessingRecord> records,
        ProjectionFreshnessMetadata? freshness = null)
    {
        ArgumentNullException.ThrowIfNull(records);

        return new ConsumerPrivacyProcessingResult(
            records.Count == 0 ? ConsumerPrivacyProcessingOutcome.Empty : ConsumerPrivacyProcessingOutcome.Ready,
            records,
            freshness);
    }

    public static ConsumerPrivacyProcessingResult Failure(ConsumerPrivacyProcessingOutcome outcome)
        => new(outcome, [], null);
}
