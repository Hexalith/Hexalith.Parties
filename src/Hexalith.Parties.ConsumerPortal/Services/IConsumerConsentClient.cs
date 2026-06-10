namespace Hexalith.Parties.ConsumerPortal.Services;

public interface IConsumerConsentClient
{
    Task<ConsumerConsentOverview> GetMyConsentOverviewAsync(CancellationToken cancellationToken = default);

    Task<ConsumerConsentOperationResult> GrantMyConsentAsync(
        ConsumerConsentGrantRequest request,
        CancellationToken cancellationToken = default);

    Task<ConsumerConsentOperationResult> WithdrawMyConsentAsync(
        ConsumerConsentWithdrawRequest request,
        CancellationToken cancellationToken = default);
}
