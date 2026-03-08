using Dapr.Actors;

namespace Hexalith.Parties.Security;

public interface IPartyKeyRetryActor : IActor
{
    Task ScheduleRetryAsync(CryptoPendingRecord record);

    Task ClearRetryAsync();

    Task<bool> IsPendingAsync();
}
