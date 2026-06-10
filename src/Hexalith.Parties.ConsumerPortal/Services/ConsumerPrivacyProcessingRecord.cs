namespace Hexalith.Parties.ConsumerPortal.Services;

public sealed record ConsumerPrivacyProcessingRecord(
    ConsumerPrivacyProcessingCategory Category,
    ConsumerPrivacyProcessingRecordOutcome Outcome,
    DateTimeOffset Timestamp,
    string Summary);
