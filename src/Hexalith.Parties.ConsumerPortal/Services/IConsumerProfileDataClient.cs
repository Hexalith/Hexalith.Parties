using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.ConsumerPortal.Services;

public interface IConsumerProfileDataClient
{
    Task<PartyDetail> GetMyPartyAsync(CancellationToken cancellationToken = default);
}
