using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Handlers;

namespace Hexalith.Parties.Projections.Actors;

public sealed class PartyDetailProjectionActor : Actor, IPartyDetailProjectionActor
{
    private const string ProjectionName = "party-detail";

    public PartyDetailProjectionActor(ActorHost host)
        : base(host)
    {
    }

    public async Task HandleEventAsync(string partyId, IEventPayload @event)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        (string actorPartyId, string stateKey) = ResolveStateContext(partyId);
        ConditionalValue<PartyDetail> currentState = await StateManager.TryGetStateAsync<PartyDetail>(stateKey, default).ConfigureAwait(false);
        PartyDetail? newState = PartyDetailProjectionHandler.Apply(actorPartyId, @event, currentState.HasValue ? currentState.Value : null);
        if (newState is not null)
        {
            await StateManager.SetStateAsync(stateKey, newState, default).ConfigureAwait(false);
        }
    }

    public async Task<PartyDetail?> GetDetailAsync()
    {
        string actorId = Host.Id.GetId();
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3)
        {
            return null;
        }

        string tenant = segments[0];
        string actorPartyId = segments[^1];
        string stateKey = $"{tenant}:{ProjectionName}:{actorPartyId}";

        ConditionalValue<PartyDetail> result =
            await StateManager.TryGetStateAsync<PartyDetail>(stateKey, default).ConfigureAwait(false);
        return result.HasValue ? result.Value : null;
    }

    private (string PartyId, string StateKey) ResolveStateContext(string incomingPartyId)
    {
        string actorId = Host.Id.GetId();
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3)
        {
            throw new InvalidOperationException($"Invalid actor id format '{actorId}'. Expected '{{tenant}}:{ProjectionName}:{{partyId}}'.");
        }

        string tenant = segments[0];
        string projection = segments[1];
        string actorPartyId = segments[^1];

        if (!string.Equals(projection, ProjectionName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Invalid actor projection segment '{projection}'. Expected '{ProjectionName}'.");
        }

        if (!string.Equals(actorPartyId, incomingPartyId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Party id mismatch. Actor id party '{actorPartyId}' does not match incoming '{incomingPartyId}'.");
        }

        return (actorPartyId, $"{tenant}:{ProjectionName}:{actorPartyId}");
    }
}
