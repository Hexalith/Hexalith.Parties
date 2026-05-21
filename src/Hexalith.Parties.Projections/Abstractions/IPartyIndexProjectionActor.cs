using Dapr.Actors;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Projections.Abstractions;

public interface IPartyIndexProjectionActor : IActor
{
    Task HandleEventAsync(string partyId, IEventPayload @event);

    Task HandleSerializedEventAsync(string partyId, string eventTypeName, byte[] payload, string serializationFormat, long sequenceNumber, CancellationToken cancellationToken);

    Task FlushAsync();

    Task<bool> PingAsync();

    Task<IReadOnlyDictionary<string, PartyIndexEntry>> GetEntriesAsync();

    Task<PartyIndexProjectionReadResult> GetEntriesReadAsync();

    Task<string?> GetEntriesJsonAsync();

    Task<bool> IsRebuildingAsync();

    Task EraseAsync(string partyId);
}
