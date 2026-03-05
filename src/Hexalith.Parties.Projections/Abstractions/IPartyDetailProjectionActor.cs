using Dapr.Actors;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Projections.Abstractions;

public interface IPartyDetailProjectionActor : IActor
{
    Task HandleEventAsync(string partyId, IEventPayload @event);
}
