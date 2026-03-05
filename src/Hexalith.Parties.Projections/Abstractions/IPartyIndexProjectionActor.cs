using Dapr.Actors;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Abstractions;

public interface IPartyIndexProjectionActor : IActor
{
    Task HandleEventAsync(string partyId, IEventPayload @event);

    Task FlushAsync();

    Task<IReadOnlyDictionary<string, PartyIndexEntry>> GetEntriesAsync();
}
