namespace Hexalith.Parties.ConsumerPortal.Services;

public interface IConsumerPrivacyProcessingClient
{
    Task<ConsumerPrivacyProcessingResult> GetMyProcessingSummaryAsync(CancellationToken cancellationToken = default);
}
