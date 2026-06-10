namespace Hexalith.Parties.ConsumerPortal.Services;

public interface IConsumerProfileEditClient
{
    Task<ConsumerProfileUpdateResult> UpdateMyProfileAsync(
        ConsumerProfileUpdateRequest request,
        CancellationToken cancellationToken = default);
}
