using Dapr.Actors;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Abstractions;

public interface IPartyDetailProjectionActor : IActor
{
    Task HandleEventAsync(string partyId, IEventPayload @event);

    Task HandleSerializedEventAsync(string partyId, string eventTypeName, byte[] payload, string serializationFormat, long sequenceNumber, CancellationToken cancellationToken);

    Task<bool> PingAsync();

    Task<PartyDetail?> GetDetailAsync();

    Task<PartyDetailProjectionReadResult> GetDetailReadAsync();

    Task<string?> GetDetailJsonAsync();

    Task<byte[]?> GetSerializedDetailAsync();

    Task<bool> IsRebuildingAsync();

    Task EraseAsync(string partyId);
}
