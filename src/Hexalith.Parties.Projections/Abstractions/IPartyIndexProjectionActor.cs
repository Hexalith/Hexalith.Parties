using Dapr.Actors;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Parties.Projections.Abstractions;

public interface IPartyIndexProjectionActor : IActor
{
    Task HandleEventAsync(string partyId, IEventPayload @event);

    Task FlushAsync();
}
