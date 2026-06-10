namespace Hexalith.Parties.ConsumerPortal.Services;

public interface IConsumerPrivacyErasureClient
{
    Task<ConsumerPrivacyErasureResult> GetMyErasureStatusAsync(CancellationToken cancellationToken = default);

    Task<ConsumerPrivacyErasureResult> RequestMyErasureAsync(CancellationToken cancellationToken = default);

    Task<ConsumerPrivacyErasureResult> CancelMyErasureAsync(CancellationToken cancellationToken = default);
}
