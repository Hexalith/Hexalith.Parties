using Dapr.Actors;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Abstractions;

public interface IPartyDetailProjectionActor : IActor
{
    Task HandleEventAsync(string partyId, IEventPayload @event);

    Task<PartyDetail?> GetDetailAsync();
}
