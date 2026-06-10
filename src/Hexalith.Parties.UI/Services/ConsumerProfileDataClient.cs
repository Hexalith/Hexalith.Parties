using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.UI.Services;

internal sealed class ConsumerProfileDataClient(ISelfScopedPartiesClient selfScopedPartiesClient) : IConsumerProfileDataClient
{
    public async Task<PartyDetail> GetMyPartyAsync(CancellationToken cancellationToken = default)
        => await selfScopedPartiesClient.GetMyPartyAsync(cancellationToken).ConfigureAwait(false);
}
